using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Lightweight pooled SFX helper owned by <see cref="SoundManager"/>.
/// </summary>
/// <remarks>
/// @ingroup audio_mgr
/// @brief Wraps a runtime <see cref="AudioSource"/> for pooled sound-effect playback.
///
/// This helper stores playback settings for one sound instance and applies them to an internal
/// <see cref="AudioSource"/>. It can optionally follow a target <see cref="Transform"/> for positional
/// 3D audio playback.
///
/// Behaviour:
/// - If <see cref="Loop"/> is false, the helper automatically releases itself after playback finishes.
/// - While <see cref="Target"/> is assigned, the helper follows the target position.
/// - Runtime-adjustable values such as <see cref="Volume"/>, <see cref="Pitch"/>, and
///   <see cref="SpatialBlend"/> are synchronized every frame.
///
/// Threading:
/// - Unity main thread only.
/// </remarks>
public class SoundManagerHelper : MonoBehaviour
{
	#region Inspector Fields

	/// <summary>
	/// Whether this helper is currently reserved by the sound pool.
	/// </summary>
	[Tooltip("Set by SoundManager when this helper is in use.")]
	public bool IsUsed = false;

	/// <summary>
	/// Optional target transform followed by this helper.
	/// </summary>
	/// <remarks>
	/// Useful for 3D sounds attached to moving objects.
	/// </remarks>
	[Tooltip("Optional Transform to follow for 3D playback.")]
	public Transform Target;

	/// <summary>
	/// Audio clip assigned to this helper for playback.
	/// </summary>
	[Tooltip("Clip to play on this helper.")]
	public AudioClip Clip;

	/// <summary>
	/// Playback volume.
	/// </summary>
	[Tooltip("Playback volume.")]
	[Range(0f, 1f)]
	public float Volume = 1.0f;

	/// <summary>
	/// Playback pitch multiplier.
	/// </summary>
	/// <remarks>
	/// Values above 1 increase pitch and playback speed. Values below 1 decrease them.
	/// </remarks>
	[Tooltip("Playback pitch multiplier.")]
	public float Pitch = 1.0f;

	/// <summary>
	/// Whether the assigned clip should loop until explicitly stopped.
	/// </summary>
	/// <remarks>
	/// Non-looping sounds return this helper to the pool automatically after playback finishes.
	/// </remarks>
	[Tooltip("Loop the clip.")]
	public bool Loop = false;

	/// <summary>
	/// Spatial blend of the internal <see cref="AudioSource"/>.
	/// </summary>
	/// <remarks>
	/// A value of 0 means fully 2D audio. A value of 1 means fully 3D audio.
	/// </remarks>
	[Tooltip("0 = 2D, 1 = 3D.")]
	[Range(0f, 1f)]
	public float SpatialBlend = 0.0f;

	/// <summary>
	/// Optional audio mixer group used as the output target for this helper.
	/// </summary>
	[Tooltip("Audio Mixer Group used by this helper.")]
	public AudioMixerGroup OutputMixerGroup;

	#endregion

	#region Properties

	/// <summary>
	/// Gets whether the internal <see cref="AudioSource"/> exists and is currently playing.
	/// </summary>
	public bool IsPlaying
	{
		get { return _audioSource != null && _audioSource.isPlaying; }
	}

	#endregion

	#region Private State

	/// <summary>
	/// Runtime audio source used for playback.
	/// </summary>
	private AudioSource _audioSource;

	/// <summary>
	/// Coroutine used to release this helper after a non-looping sound finishes.
	/// </summary>
	/// <remarks>
	/// Stored so it can be stopped without cancelling unrelated coroutines.
	/// </remarks>
	private Coroutine _finishCoroutine;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Creates or reuses an <see cref="AudioSource"/> on this object.
	/// </summary>
	/// <remarks>
	/// The helper is marked with <see cref="Object.DontDestroyOnLoad(Object)"/> so pooled helpers
	/// can survive scene changes.
	/// </remarks>
	private void Awake()
	{
		if (!TryGetComponent(out _audioSource))
		{
			_audioSource = gameObject.AddComponent<AudioSource>();
		}

		_audioSource.playOnAwake = false;

		DontDestroyOnLoad(gameObject);
	}

	/// <summary>
	/// Follows the assigned target and synchronizes mutable playback settings.
	/// </summary>
	private void Update()
	{
		if (Target != null)
		{
			transform.position = Target.position;
		}

		if (_audioSource != null)
		{
			_audioSource.volume = Volume;
			_audioSource.pitch = Pitch;
			_audioSource.spatialBlend = SpatialBlend;
		}
	}

	#endregion

	#region Public API

	/// <summary>
	/// Starts playback using the currently assigned clip and playback parameters.
	/// </summary>
	/// <remarks>
	/// Playback is reset to the beginning of the clip before playing. If the sound is not looping,
	/// a coroutine releases the helper back to the pool after playback finishes.
	/// </remarks>
	public void PlaySound()
	{
		if (_audioSource == null || Clip == null)
		{
			return;
		}

		StopFinishCoroutine();

		IsUsed = true;

		_audioSource.clip = Clip;
		_audioSource.volume = Volume;
		_audioSource.pitch = Pitch;
		_audioSource.loop = Loop;
		_audioSource.spatialBlend = SpatialBlend;
		_audioSource.playOnAwake = false;
		_audioSource.outputAudioMixerGroup = OutputMixerGroup;

		_audioSource.Stop();
		_audioSource.time = 0f;
		_audioSource.Play();

		if (!Loop)
		{
			_finishCoroutine = StartCoroutine(SetUnusedWhenFinished());
		}
	}

	/// <summary>
	/// Pauses playback if the audio source is currently playing.
	/// </summary>
	/// <remarks>
	/// The helper remains reserved until it is resumed, stopped, or reset by the owner.
	/// </remarks>
	public void PauseSound()
	{
		if (_audioSource != null && _audioSource.isPlaying)
		{
			_audioSource.Pause();
		}
	}

	/// <summary>
	/// Stops playback and immediately returns this helper to the pool.
	/// </summary>
	/// <remarks>
	/// Runtime state such as target, clip, loop flag, mixer group, and playback values is cleared
	/// to safe defaults.
	/// </remarks>
	public void StopSound()
	{
		StopFinishCoroutine();

		if (_audioSource != null)
		{
			_audioSource.Stop();
			_audioSource.clip = null;
		}

		ClearState();
	}

	/// <summary>
	/// Stops playback and rewinds the audio source to the beginning.
	/// </summary>
	/// <remarks>
	/// The helper remains reserved, allowing the owner to call <see cref="PlaySound"/> again
	/// with the same or updated settings.
	/// </remarks>
	public void ResetSound()
	{
		StopFinishCoroutine();

		if (_audioSource != null)
		{
			_audioSource.Stop();
			_audioSource.time = 0f;
		}
	}

	/// <summary>
	/// Destroys this helper object.
	/// </summary>
	/// <remarks>
	/// Prefer returning helpers to the pool with <see cref="StopSound"/> unless the whole pool
	/// is being shut down.
	/// </remarks>
	public void DestroyHelper()
	{
		StopFinishCoroutine();
		Destroy(gameObject);
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Stops the pending finish coroutine, if one exists.
	/// </summary>
	private void StopFinishCoroutine()
	{
		if (_finishCoroutine != null)
		{
			StopCoroutine(_finishCoroutine);
			_finishCoroutine = null;
		}
	}

	/// <summary>
	/// Clears runtime state so the helper can be safely reused by the pool.
	/// </summary>
	private void ClearState()
	{
		IsUsed = false;
		Target = null;
		Clip = null;
		Loop = false;
		Volume = 1f;
		Pitch = 1f;
		SpatialBlend = 0f;
		OutputMixerGroup = null;
	}

	#endregion

	#region Coroutines

	/// <summary>
	/// Waits until a non-looping sound finishes, then releases this helper back to the pool.
	/// </summary>
	/// <returns>Coroutine enumerator.</returns>
	private IEnumerator SetUnusedWhenFinished()
	{
		while (_audioSource != null && _audioSource.isPlaying)
		{
			yield return null;
		}

		_finishCoroutine = null;
		ClearState();
	}

	#endregion
}