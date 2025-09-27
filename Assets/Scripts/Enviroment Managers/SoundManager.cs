using Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

/// <summary>
/// Centralized audio service for background music and pooled 3D SFX playback, with
/// per-clip continuation support and optional low-pass filtering on music.
/// </summary>
/// <remarks>
/// @ingroup audio_mgr
/// @thread Unity main thread (Unity methods); audio rendering is on Unity’s audio thread.
/// @invariant Exactly one active instance via <see cref="Singleton{T}"/>.
/// @req <see cref="SoundManagerHelper"/> exists and exposes properties/methods used here:
///      <c>IsUsed</c>, <c>IsPlaying</c>, <c>Target</c>, <c>Clip</c>, <c>Volume</c>, <c>Pitch</c>,
///      <c>SpatialBlend</c>, <c>Loop</c>, and methods <c>PlaySound()</c>, <c>PauseSound()</c>,
///      <c>ResetSound()</c>, <c>StopSound()</c>.
/// </remarks>
public class SoundManager : Singleton<SoundManager>
{
    #region Inspector: Audio Settings

    [Header("Audio Settings")]
    /// <summary>
    /// Volume scalar for background music in [0,1]. Applied to the internal music source.
    /// </summary>
    [Tooltip("Volume for background music (0.0 to 1.0)")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

    /// <summary>
    /// Global SFX volume scalar in [0,1]. Multiplies per-call SFX volume.
    /// </summary>
    [Tooltip("Volume for sound effects (0.0 to 1.0)")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.5f;

    /// <summary>
    /// Output mixer group for the music AudioSource (optional).
    /// </summary>
    [Tooltip("Audio Mixer Group for background music")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;

    /// <summary>
    /// Output mixer group for pooled SFX AudioSources (optional).
    /// </summary>
    [Tooltip("Audio Mixer Group for sound effects")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    /// <summary>
    /// When a clip is “continued”, it will keep playing for this duration (seconds)
    /// before being automatically stopped (unless continued again).
    /// </summary>
    [SerializeField] private float continueThreshold = 0.1f;

    #endregion

    #region State & Components

    /// <summary>Looping music AudioSource managed by this component.</summary>
    private AudioSource _musicSource;

    /// <summary>Optional low-pass filter applied to music output.</summary>
    private AudioLowPassFilter _musicLowPass;

    /// <summary>Countdown (seconds) for clips scheduled to stop after a continue.</summary>
    private readonly Dictionary<AudioClip, float> _toBeContinued = new Dictionary<AudioClip, float>();

    /// <summary>Staging list of continued clips to remove when countdown elapses.</summary>
    private readonly List<AudioClip> _toBeRemoved = new List<AudioClip>();

    /// <summary>Scratch list for enumerating keys without allocation while mutating the dictionary.</summary>
    private List<AudioClip> _keys;

    /// <summary>Pool of helper components used to play/track active SFX.</summary>
    private readonly List<SoundManagerHelper> _sfxSources = new List<SoundManagerHelper>();

    /// <summary>Maps an AudioClip to a helper currently responsible for playing it.</summary>
    private readonly Dictionary<AudioClip, SoundManagerHelper> _sfxMapping = new Dictionary<AudioClip, SoundManagerHelper>();

    /// <summary>Cached main camera (used to co-locate the manager with the listener).</summary>
    private Camera _camera;

    /// <summary>Exposes the music low-pass filter for external tweaks (cutoff/Q).</summary>
    public AudioLowPassFilter MusicLowPass => _musicLowPass;

    #endregion

    #region Unity methods

    /// <summary>
    /// Initializes the singleton, creates a looping music source, and a default low-pass filter.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.volume = musicVolume;
        _musicSource.outputAudioMixerGroup = musicMixerGroup;

        _musicLowPass = gameObject.AddComponent<AudioLowPassFilter>();
        _musicLowPass.cutoffFrequency = 22000f;
        _musicLowPass.lowpassResonanceQ = 1f;
    }

    /// <summary>
    /// Keeps this object at the listener position and services SFX pooling/continuations.
    /// </summary>
    private void Update()
    {
        // Follow the main camera so “global” SFX use the listener position.
        if (_camera == null) _camera = Camera.main;
        if (_camera != null) transform.position = _camera.transform.position;

        // Pin all helper sources to this position (spatialized per-clip via blend on helper).
        for (int i = 0; i < _sfxSources.Count; i++)
        {
            var helper = _sfxSources[i];
            if (helper != null) helper.transform.position = transform.position;
        }

        // Manage “continue” countdowns.
        _toBeRemoved.Clear();
        _keys = _keys ?? new List<AudioClip>(4);
        _keys.Clear();
        _keys.AddRange(_toBeContinued.Keys);

        for (int i = 0; i < _keys.Count; i++)
        {
            var clip = _keys[i];
            float t = _toBeContinued[clip] - Time.deltaTime;
            if (t > 0f)
            {
                _toBeContinued[clip] = t;
            }
            else
            {
                if (_sfxMapping.TryGetValue(clip, out var helper) && helper != null)
                    helper.StopSound();
                _toBeRemoved.Add(clip);
            }
        }

        for (int i = 0; i < _toBeRemoved.Count; i++)
            _toBeContinued.Remove(_toBeRemoved[i]);
    }

    #endregion

    #region Music API

    /// <summary>
    /// Starts playing the given music clip. If a new clip is provided, the source is swapped
    /// and playback begins; if the same clip is already set but not playing, it resumes.
    /// </summary>
    /// <param name="clip">Music clip to play (ignored if null).</param>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;

        if (_musicSource.clip != clip)
        {
            _musicSource.clip = clip;
            _musicSource.Play();
        }
        else if (!_musicSource.isPlaying)
        {
            _musicSource.Play();
        }
    }

    /// <summary>Stops the current music if it is playing.</summary>
    public void StopMusic()
    {
        if (_musicSource.isPlaying)
            _musicSource.Stop();
    }

    /// <summary>
    /// Sets music volume and applies it immediately.
    /// </summary>
    /// <param name="volume">Target volume in [0,1].</param>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        _musicSource.volume = musicVolume;
    }

    #endregion

    #region SFX API

    /// <summary>
    /// Plays (or reuses) a pooled SFX helper for the given clip at a target transform, with
    /// optional volume/pitch/loop and spatial blend control.
    /// </summary>
    /// <param name="clip">Clip to play (ignored if null).</param>
    /// <param name="transform">World target whose position is tracked by the helper.</param>
    /// <param name="volume">Per-call volume scalar (multiplied by global <see cref="sfxVolume"/>).</param>
    /// <param name="pitch">Per-call pitch multiplier.</param>
    /// <param name="loop">Whether the SFX should loop.</param>
    /// <param name="specialBlend">
    /// Spatial blend for the helper [0..1]. 0 = 2D, 1 = fully 3D.
    /// </param>
    /// <param name="reset">
    /// If true and the clip is already playing, the helper is reset and restarted. If false,
    /// an already-playing clip continues uninterrupted.
    /// </param>
    public void PlaySFXClip(
        AudioClip clip,
        Transform transform,
        float volume = 1f,
        float pitch = 1f,
        bool loop = false,
        float specialBlend = 0.0f,
        bool reset = true)
    {
        if (clip == null) return;

        // Try to reuse existing helper for this clip.
        if (!_sfxMapping.ContainsKey(clip))
        {
            // Find an unused helper first.
            for (int i = 0; i < _sfxSources.Count; ++i)
            {
                if (!_sfxSources[i].IsUsed)
                {
                    _sfxMapping.Remove(_sfxSources[i].Clip);
                    SetUpHelper(_sfxSources[i], clip, transform, volume * sfxVolume, pitch, loop, specialBlend);
                    _sfxMapping[clip] = _sfxSources[i];
                    _sfxSources[i].PlaySound();
                    return;
                }
            }

            // None free—create a new helper.
            var helper = new GameObject("SFXHelper").AddComponent<SoundManagerHelper>();
            SetUpHelper(helper, clip, transform, volume * sfxVolume, pitch, loop, specialBlend);
            _sfxSources.Add(helper);
            _sfxMapping[clip] = helper;
            helper.PlaySound();
        }
        else
        {
            var helper = _sfxMapping[clip];
            if (helper == null)
            {
                // Defensive: mapping exists but helper missing—create a fresh one.
                helper = new GameObject("SFXHelper").AddComponent<SoundManagerHelper>();
                SetUpHelper(helper, clip, transform, volume * sfxVolume, pitch, loop, specialBlend);
                _sfxSources.Add(helper);
                _sfxMapping[clip] = helper;
                helper.PlaySound();
                return;
            }

            if (helper.IsPlaying)
            {
                if (reset)
                {
                    helper.ResetSound();
                    helper.PlaySound();
                }
            }
            else
            {
                helper.PlaySound();
            }
        }
    }

    /// <summary>
    /// “Continues” a looping SFX for <see cref="continueThreshold"/> seconds and then stops it,
    /// unless continued again in the meantime.
    /// </summary>
    /// <param name="clip">Clip to continue (ignored if null).</param>
    public void ContinueSFXClip(AudioClip clip)
    {
        PlaySFXClip(clip, transform, 1f, 1f, true, 0.0f, false);
        if (clip != null) _toBeContinued[clip] = continueThreshold;
    }

    /// <summary>
    /// Same as <see cref="ContinueSFXClip(AudioClip)"/> but with an explicit start volume.
    /// </summary>
    /// <param name="clip">Clip to continue.</param>
    /// <param name="volume">Per-call volume scalar.</param>
    public void ContinueSFXClip(AudioClip clip, float volume)
    {
        PlaySFXClip(clip, transform, volume, 1f, true, 0.0f, false);
        if (clip != null) _toBeContinued[clip] = continueThreshold;
    }

    /// <summary>
    /// Convenience overload: plays a non-looping SFX at the manager’s position with a volume scalar.
    /// </summary>
    /// <param name="clip">Clip to play.</param>
    /// <param name="volume">Per-call volume scalar.</param>
    public void PlaySFXClip(AudioClip clip, float volume)
    {
        PlaySFXClip(clip, transform, volume);
    }

    /// <summary>
    /// Convenience overload: plays a non-looping SFX at the given transform with default volume/pitch.
    /// </summary>
    /// <param name="clip">Clip to play.</param>
    /// <param name="transform">World target.</param>
    public void PlaySFXClip(AudioClip clip, Transform transform)
    {
        // FIX: avoid recursion—call the main overload with defaults.
        PlaySFXClip(clip, transform, 1f, 1f, false, 0.0f, true);
    }

    /// <summary>
    /// Convenience overload: plays a non-looping SFX at the manager’s position with default volume/pitch.
    /// </summary>
    /// <param name="clip">Clip to play.</param>
    public void PlaySFXClip(AudioClip clip)
    {
        PlaySFXClip(clip, transform, 1f, 1f, false, 0.0f, true);
    }

    /// <summary>
    /// Pauses a playing SFX associated with the given clip (if found).
    /// </summary>
    /// <param name="clip">Clip whose helper should be paused.</param>
    public void PauseSFXClip(AudioClip clip)
    {
        if (clip == null) return;
        if (_sfxMapping.TryGetValue(clip, out var helper) && helper != null)
            helper.PauseSound();
    }

    /// <summary>
    /// Resets a playing SFX (seek to start) for the given clip (if found).
    /// </summary>
    /// <param name="clip">Clip whose helper should be reset.</param>
    public void ResetSFXClip(AudioClip clip)
    {
        if (clip == null) return;
        if (_sfxMapping.TryGetValue(clip, out var helper) && helper != null)
            helper.ResetSound();
    }

    /// <summary>
    /// Stops and unmaps the helper for the given clip (if found).
    /// </summary>
    /// <param name="clip">Clip whose playback should stop.</param>
    public void StopSFXClip(AudioClip clip)
    {
        if (clip == null) return;
        if (_sfxMapping.TryGetValue(clip, out var helper) && helper != null)
        {
            helper.StopSound();
            _sfxMapping.Remove(clip);
        }
    }

    /// <summary>
    /// Sets the global SFX volume scalar for subsequent plays (existing one-shots are unaffected).
    /// </summary>
    /// <param name="volume">Target volume in [0,1].</param>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    #endregion

    #region Internals

    /// <summary>
    /// Initializes a pooled helper with the desired clip, target, and playback parameters.
    /// </summary>
    /// <param name="helper">Helper instance to configure.</param>
    /// <param name="clip">Clip to play.</param>
    /// <param name="transform">World target to track.</param>
    /// <param name="volume">Computed volume (already multiplied by <see cref="sfxVolume"/>).</param>
    /// <param name="pitch">Pitch multiplier.</param>
    /// <param name="loop">Loop flag.</param>
    /// <param name="specialBlend">Spatial blend [0..1].</param>
    private static void SetUpHelper(
        SoundManagerHelper helper,
        AudioClip clip,
        Transform transform,
        float volume,
        float pitch,
        bool loop,
        float specialBlend)
    {
        helper.IsUsed = true;
        helper.Target = transform;
        helper.Clip = clip;
        helper.Volume = volume;
        helper.Pitch = pitch;
        helper.SpatialBlend = specialBlend;
        helper.Loop = loop;
    }

    #endregion
}
