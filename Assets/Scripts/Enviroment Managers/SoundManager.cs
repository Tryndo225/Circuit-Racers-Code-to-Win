using Generic;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Centralized audio service for background music and pooled sound-effect playback.
/// </summary>
/// <remarks>
/// @ingroup audio_mgr
/// @brief Manages looping music, pooled one-shot SFX, looping SFX, and short continuation windows for repeated sounds.
///
/// The manager owns one music <see cref="AudioSource"/> and a pool of <see cref="SoundManagerHelper"/>
/// objects used for sound effects.
///
/// Behaviour:
/// - Background music is played through a dedicated looping audio source.
/// - Non-looping SFX are not mapped by clip, so repeated one-shots can overlap.
/// - Looping SFX are mapped by clip, so one looping helper is reused per clip.
/// - Continued SFX are kept alive for a short duration and stopped unless continued again.
/// - The manager follows the main camera so manager-position SFX play near the listener.
///
/// Threading:
/// - Unity main thread only.
/// </remarks>
public class SoundManager : Singleton<SoundManager>
{
	#region Inspector: Audio Settings

	[Header("Audio Settings")]
	/// <summary>
	/// Volume scalar for background music.
	/// </summary>
	[Tooltip("Volume for background music.")]
	[SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

	/// <summary>
	/// Global SFX volume scalar.
	/// </summary>
	/// <remarks>
	/// This value multiplies the per-call SFX volume.
	/// </remarks>
	[Tooltip("Global volume multiplier for sound effects.")]
	[SerializeField, Range(0f, 1f)] private float sfxVolume = 0.5f;

	/// <summary>
	/// Optional output mixer group for background music.
	/// </summary>
	[Tooltip("Audio Mixer Group for background music.")]
	[SerializeField] private AudioMixerGroup musicMixerGroup;

	/// <summary>
	/// Optional output mixer group for sound effects.
	/// </summary>
	[Tooltip("Audio Mixer Group for sound effects.")]
	[SerializeField] private AudioMixerGroup sfxMixerGroup;

	/// <summary>
	/// Duration in seconds for which a continued SFX clip remains active without another continue request.
	/// </summary>
	[Tooltip("How long a continued SFX keeps playing before it is stopped unless continued again.")]
	[SerializeField] private float continueThreshold = 0.1f;

	#endregion

	#region State & Components

	/// <summary>
	/// Looping music audio source managed by this component.
	/// </summary>
	private AudioSource _musicSource;

	/// <summary>
	/// Low-pass filter applied to music output.
	/// </summary>
	private AudioLowPassFilter _musicLowPass;

	/// <summary>
	/// Countdown values for clips scheduled to stop after a continuation window.
	/// </summary>
	private readonly Dictionary<AudioClip, float> _toBeContinued = new Dictionary<AudioClip, float>();

	/// <summary>
	/// Temporary list of continued clips to remove after their countdown elapses.
	/// </summary>
	private readonly List<AudioClip> _toBeRemoved = new List<AudioClip>();

	/// <summary>
	/// Scratch list used for enumerating dictionary keys while the dictionary may be mutated.
	/// </summary>
	private List<AudioClip> _keys;

	/// <summary>
	/// Pool of helper components used to play and track active sound effects.
	/// </summary>
	private readonly List<SoundManagerHelper> _sfxSources = new List<SoundManagerHelper>();

	/// <summary>
	/// Maps looping or continued clips to the helper currently responsible for playing them.
	/// </summary>
	private readonly Dictionary<AudioClip, SoundManagerHelper> _sfxMapping = new Dictionary<AudioClip, SoundManagerHelper>();

	/// <summary>
	/// Cached main camera used to keep this manager near the listener.
	/// </summary>
	private Camera _camera;

	/// <summary>
	/// Gets the music low-pass filter for external audio effects.
	/// </summary>
	public AudioLowPassFilter MusicLowPass => _musicLowPass;

	#endregion

	#region Unity methods

	/// <summary>
	/// Initializes the singleton, creates the music source, and adds a default low-pass filter.
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
	/// Keeps this object at the listener position and updates SFX continuation countdowns.
	/// </summary>
	private void Update()
	{
		if (_camera == null) _camera = Camera.main;
		if (_camera != null) transform.position = _camera.transform.position;

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
	/// Starts or resumes background music.
	/// </summary>
	/// <param name="clip">Music clip to play.</param>
	/// <remarks>
	/// Null clips are ignored. If the requested clip differs from the current music clip,
	/// the source is swapped and playback begins. If the same clip is already assigned but paused,
	/// playback resumes.
	/// </remarks>
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

	/// <summary>
	/// Stops the current background music if it is playing.
	/// </summary>
	public void StopMusic()
	{
		if (_musicSource.isPlaying)
			_musicSource.Stop();
	}

	/// <summary>
	/// Sets the background music volume.
	/// </summary>
	/// <param name="volume">Target volume in the range 0 to 1.</param>
	public void SetMusicVolume(float volume)
	{
		musicVolume = Mathf.Clamp01(volume);
		_musicSource.volume = musicVolume;
	}

	#endregion

	#region SFX API

	/// <summary>
	/// Plays a sound effect through the pooled helper system.
	/// </summary>
	/// <param name="clip">Clip to play.</param>
	/// <param name="target">Target transform followed by the helper during playback.</param>
	/// <param name="volume">Per-call volume multiplier.</param>
	/// <param name="pitch">Per-call pitch multiplier.</param>
	/// <param name="loop">Whether the sound effect should loop.</param>
	/// <param name="spatialBlend">Spatial blend in the range 0 to 1. A value of 0 is 2D; a value of 1 is fully 3D.</param>
	/// <param name="reset">
	/// Whether an already-playing mapped clip should be reset and restarted.
	/// If false, an already-playing mapped clip continues uninterrupted.
	/// </param>
	/// <remarks>
	/// Non-looping sounds always use a free helper and are allowed to overlap. Looping sounds are mapped by clip
	/// so repeated calls reuse the same helper.
	/// </remarks>
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
	/// Keeps a looping SFX clip playing for the configured continuation window.
	/// </summary>
	/// <param name="clip">Clip to continue.</param>
	/// <remarks>
	/// The clip is played as a looping SFX and then scheduled to stop after <see cref="continueThreshold"/>
	/// seconds unless this method is called again before the countdown expires.
	/// </remarks>
	public void ContinueSFXClip(AudioClip clip)
	{
		PlaySFXClip(clip, transform, 1f, 1f, true, 0.0f, false);
		if (clip != null) _toBeContinued[clip] = continueThreshold;
	}

	/// <summary>
	/// Keeps a looping SFX clip playing for the configured continuation window with a custom volume.
	/// </summary>
	/// <param name="clip">Clip to continue.</param>
	/// <param name="volume">Per-call volume multiplier.</param>
	public void ContinueSFXClip(AudioClip clip, float volume)
	{
		PlaySFXClip(clip, transform, volume, 1f, true, 0.0f, false);
		if (clip != null) _toBeContinued[clip] = continueThreshold;
	}

	/// <summary>
	/// Plays a non-looping sound effect at the manager position with a custom volume.
	/// </summary>
	/// <param name="clip">Clip to play.</param>
	/// <param name="volume">Per-call volume multiplier.</param>
	public void PlaySFXClip(AudioClip clip, float volume)
	{
		PlaySFXClip(clip, transform, volume);
	}

	/// <summary>
	/// Plays a non-looping sound effect at the given target with default volume and pitch.
	/// </summary>
	/// <param name="clip">Clip to play.</param>
	/// <param name="target">Target transform followed by the helper during playback.</param>
	public void PlaySFXClip(AudioClip clip, Transform target)
	{
		PlaySFXClip(clip, target, 1f, 1f, false, 0.0f, true);
	}

	/// <summary>
	/// Plays a non-looping sound effect at the manager position with default volume and pitch.
	/// </summary>
	/// <param name="clip">Clip to play.</param>
	public void PlaySFXClip(AudioClip clip)
	{
		PlaySFXClip(clip, transform, 1f, 1f, false, 0.0f, true);
	}

	/// <summary>
	/// Pauses a mapped sound effect associated with the given clip.
	/// </summary>
	/// <param name="clip">Clip whose helper should be paused.</param>
	public void PauseSFXClip(AudioClip clip)
	{
		if (clip == null) return;
		if (_sfxMapping.TryGetValue(clip, out var helper) && helper != null)
			helper.PauseSound();
	}

	/// <summary>
	/// Resets a mapped sound effect associated with the given clip.
	/// </summary>
	/// <param name="clip">Clip whose helper should be reset to the start.</param>
	public void ResetSFXClip(AudioClip clip)
	{
		if (clip == null) return;
		if (_sfxMapping.TryGetValue(clip, out var helper) && helper != null)
			helper.ResetSound();
	}

	/// <summary>
	/// Stops and unmaps a sound effect associated with the given clip.
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
	/// Sets the global SFX volume scalar for future sound effects.
	/// </summary>
	/// <param name="volume">Target volume in the range 0 to 1.</param>
	/// <remarks>
	/// Existing one-shot sounds are not retroactively updated.
	/// </remarks>
	public void SetSFXVolume(float volume)
	{
		sfxVolume = Mathf.Clamp01(volume);
	}

	#endregion

	#region Internals

	/// <summary>
	/// Initializes a pooled helper with clip, target, and playback parameters.
	/// </summary>
	/// <param name="helper">Helper instance to configure.</param>
	/// <param name="clip">Clip to play.</param>
	/// <param name="target">Target transform followed by the helper during playback.</param>
	/// <param name="volume">Computed volume after applying the global SFX volume.</param>
	/// <param name="pitch">Pitch multiplier.</param>
	/// <param name="loop">Whether playback should loop.</param>
	/// <param name="spatialBlend">Spatial blend in the range 0 to 1.</param>
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
	/// Gets an unused helper from the pool, or creates a new helper when none are free.
	/// </summary>
	/// <returns>Free helper ready for setup.</returns>
	/// <remarks>
	/// Any stale clip mappings pointing to a reused helper are removed before reuse.
	/// </remarks>
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
	/// </summary>
	/// <param name="helper">Helper whose previous mappings should be removed.</param>
	/// <remarks>
	/// This prevents stale mappings when a pooled helper is reused for another sound.
	/// </remarks>
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