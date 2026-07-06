using Generic;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Centralized audio service for background music and pooled SFX playback.
/// Supports looping music, pooled one-shot SFX, mapped looping SFX, and short
/// continuation windows for sounds that should keep playing while repeatedly requested.
/// </summary>
/// <remarks>
/// @ingroup audio_mgr
/// @thread Unity main thread.
/// @invariant Exactly one active instance exists through <see cref="Singleton{T}"/>.
/// @invariant Non-looping SFX are not mapped by clip, so repeated one-shots can overlap.
/// @invariant Looping SFX are mapped by clip, so only one looping helper exists per clip.
/// @req <see cref="SoundManagerHelper"/> exposes:
///      <c>IsUsed</c>, <c>IsPlaying</c>, <c>Target</c>, <c>Clip</c>, <c>Volume</c>,
///      <c>Pitch</c>, <c>SpatialBlend</c>, <c>Loop</c>, <c>OutputMixerGroup</c>,
///      and methods <c>PlaySound()</c>, <c>PauseSound()</c>, <c>ResetSound()</c>,
///      <c>StopSound()</c>.
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
				{
					helper.StopSound();
					_sfxMapping.Remove(clip);
				}

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

		Debug.Log($"[SoundManager]: Playing Music Clip: {clip}");
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
	/// Plays (or reuses) a pooled SFX helper for the given clip at a target target, with
	/// optional volume/pitch/loop and spatial blend control.
	/// </summary>
	/// <param name="clip">Clip to play (ignored if null).</param>
	/// <param name="target">World target whose position is tracked by the helper.</param>
	/// <param name="volume">Per-call volume scalar (multiplied by global <see cref="sfxVolume"/>).</param>
	/// <param name="pitch">Per-call pitch multiplier.</param>
	/// <param name="loop">Whether the SFX should loop.</param>
	/// <param name="spatialBlend">
	/// Spatial blend for the helper [0..1]. 0 = 2D, 1 = fully 3D.
	/// </param>
	/// <param name="reset">
	/// If true and the clip is already playing, the helper is reset and restarted. If false,
	/// an already-playing clip continues uninterrupted.
	/// </param>
	public void PlaySFXClip(AudioClip clip, Transform target, float volume = 1f, float pitch = 1f, bool loop = false, float spatialBlend = 0.0f, bool reset = true)
	{
		if (clip == null)
		{
			return;
		}

		float finalVolume = Mathf.Clamp01(volume * sfxVolume);

		// One-shot sounds, such as crashes, should NOT be mapped by clip.
		// Otherwise the same crash clip cuts/restarts itself.
		if (!loop)
		{
			SoundManagerHelper oneShotHelper = GetFreeHelper();
			SetUpHelper(oneShotHelper, clip, target, finalVolume, pitch, false, spatialBlend);
			oneShotHelper.PlaySound();
			return;
		}

		// Looping sounds still use mapping, because engine/continuous sounds
		// should usually have one active source per clip.
		if (!_sfxMapping.TryGetValue(clip, out SoundManagerHelper helper) || helper == null)
		{
			helper = GetFreeHelper();
			_sfxMapping[clip] = helper;

			SetUpHelper(helper, clip, target, finalVolume, pitch, true, spatialBlend);
			helper.PlaySound();
			return;
		}

		SetUpHelper(helper, clip, target, finalVolume, pitch, true, spatialBlend);

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
	/// Convenience overload: plays a non-looping SFX at the given target with default volume/pitch.
	/// </summary>
	/// <param name="clip">Clip to play.</param>
	/// <param name="target">World target.</param>
	public void PlaySFXClip(AudioClip clip, Transform target)
	{
		PlaySFXClip(clip, target, 1f, 1f, false, 0.0f, true);
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
		if (clip == null)
		{
			return;
		}

		_toBeContinued.Remove(clip);

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
	/// <param name="spatialBlend">Spatial blend [0..1].</param>
	private void SetUpHelper(SoundManagerHelper helper, AudioClip clip, Transform target, float volume, float pitch, bool loop, float spatialBlend)
	{
		helper.IsUsed = true;
		helper.Target = target;
		helper.Clip = clip;
		helper.Volume = volume;
		helper.Pitch = pitch;
		helper.SpatialBlend = spatialBlend;
		helper.Loop = loop;
		helper.OutputMixerGroup = sfxMixerGroup;
	}

	/// <summary>
	/// Returns an unused helper from the pool, or creates a new helper if none are free.
	/// Any stale clip mappings pointing to the reused helper are removed before reuse.
	/// </summary>
	private SoundManagerHelper GetFreeHelper()
	{
		for (int i = 0; i < _sfxSources.Count; i++)
		{
			SoundManagerHelper helper = _sfxSources[i];

			if (helper != null && !helper.IsUsed)
			{
				RemoveMappingsForHelper(helper);
				return helper;
			}
		}

		SoundManagerHelper newHelper = new GameObject("SFXHelper").AddComponent<SoundManagerHelper>();
		_sfxSources.Add(newHelper);
		return newHelper;
	}

	/// <summary>
	/// Removes all clip mappings that point to the given helper.
	/// This prevents stale mappings when a pooled helper is reused for another sound.
	/// </summary>
	/// <param name="helper">Helper whose previous mappings should be removed.</param>
	private void RemoveMappingsForHelper(SoundManagerHelper helper)
	{
		if (helper == null)
		{
			return;
		}

		_keys = _keys ?? new List<AudioClip>(4);
		_keys.Clear();
		_keys.AddRange(_sfxMapping.Keys);

		for (int i = 0; i < _keys.Count; i++)
		{
			AudioClip clip = _keys[i];

			if (_sfxMapping.TryGetValue(clip, out SoundManagerHelper mappedHelper) &&
				mappedHelper == helper)
			{
				_sfxMapping.Remove(clip);
			}
		}
	}

	#endregion
}
