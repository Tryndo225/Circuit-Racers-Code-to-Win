using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Lightweight pooled SFX helper owned by <c>SoundManager</c>.
/// Holds a runtime-created <see cref="AudioSource"/> and optionally follows a target
/// <see cref="Transform"/> for positional sound playback.
/// </summary>
/// <remarks>
/// @ingroup audio_mgr
/// @thread Unity main thread.
/// @invariant If <see cref="Loop"/> is false, the helper automatically returns itself
///            to the pool after playback finishes.
/// @invariant While <see cref="Target"/> is assigned, this helper follows the target position.
/// @invariant Runtime-adjustable playback values such as <see cref="Volume"/>,
///            <see cref="Pitch"/>, and <see cref="SpatialBlend"/> are synchronized every frame.
/// </remarks>
public class SoundManagerHelper : MonoBehaviour
{
	#region Inspector Fields

	/// <summary>
	/// True while this helper is reserved by the sound pool.
	/// </summary>
	[Tooltip("Set by SoundManager when this helper is in use.")]
	public bool IsUsed = false;

	/// <summary>
	/// Optional target transform followed by this helper.
	/// Useful for 3D sounds attached to moving objects.
	/// </summary>
	[Tooltip("Optional Transform to follow for 3D playback.")]
	public Transform Target;

	/// <summary>
	/// Audio clip assigned to this helper for playback.
	/// </summary>
	[Tooltip("Clip to play on this helper.")]
	public AudioClip Clip;

	/// <summary>
	/// Playback volume in the range [0, 1].
	/// </summary>
	[Tooltip("Playback volume (0..1).")]
	[Range(0f, 1f)]
	public float Volume = 1.0f;

	/// <summary>
	/// Playback pitch multiplier.
	/// Values above 1 increase pitch and playback speed; values below 1 decrease them.
	/// </summary>
	[Tooltip("Playback pitch multiplier.")]
	public float Pitch = 1.0f;

	/// <summary>
	/// If true, the assigned clip loops until explicitly stopped.
	/// If false, the helper is automatically released after playback ends.
	/// </summary>
	[Tooltip("Loop the clip.")]
	public bool Loop = false;

	/// <summary>
	/// Spatial blend of the internal AudioSource.
	/// 0 means fully 2D, 1 means fully 3D.
	/// </summary>
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
	/// True if the internal AudioSource exists and is currently playing.
	/// </summary>
	public bool IsPlaying
	{
		get { return _audioSource != null && _audioSource.isPlaying; }
	}

	#endregion

	#region Private State

	/// <summary>
	/// Runtime AudioSource used for actual playback.
	/// </summary>
	private AudioSource _audioSource;

	/// <summary>
	/// Coroutine used to release this helper after a non-looping sound finishes.
	/// Stored so it can be stopped without cancelling unrelated coroutines.
	/// </summary>
	private Coroutine _finishCoroutine;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Creates or reuses an AudioSource on this GameObject and prevents the helper
	/// from being destroyed when scenes change.
	/// </summary>
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
	/// Follows the assigned target, if any, and synchronizes values that may change
	/// during playback.
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
	/// If the sound is not looping, a coroutine is started to return the helper
	/// to the pool after playback finishes.
	/// </summary>
	/// <remarks>
	/// This method resets playback to the beginning of the clip before playing.
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
	/// Pauses playback if the AudioSource is currently playing.
	/// The helper remains reserved until it is resumed, stopped, or reset by the owner.
	/// </summary>
	public void PauseSound()
	{
		if (_audioSource != null && _audioSource.isPlaying)
		{
			_audioSource.Pause();
		}
	}

	/// <summary>
	/// Stops playback and immediately returns this helper to the pool.
	/// Runtime state such as target, clip, loop flag, mixer group, and playback values
	/// is cleared to safe defaults.
	/// </summary>
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
	/// Stops playback and rewinds the AudioSource to the beginning.
	/// The helper remains reserved, allowing the owner to immediately call
	/// <see cref="PlaySound"/> again with the same or updated settings.
	/// </summary>
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
	/// Destroys this helper GameObject.
	/// Prefer returning helpers to the pool with <see cref="StopSound"/> unless the
	/// whole pool is being shut down.
	/// </summary>
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
	/// Clears all runtime state so the helper can be safely reused by the pool.
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
	/// Waits until a non-looping sound finishes, then releases this helper back
	/// to the pool.
	/// </summary>
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