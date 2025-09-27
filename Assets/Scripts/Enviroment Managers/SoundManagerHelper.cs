using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight pooled SFX helper owned by SoundManager.
/// Holds an AudioSource and mirrors runtime parameters (clip, volume, pitch, loop, spatialBlend),
/// optionally following a target Transform in world space.
/// </summary>
/// <remarks>
/// @ingroup audio_mgr
/// @thread Unity main thread (Start/Update/Coroutines).
/// @invariant If <see cref="Loop"/> is false, the helper auto-flags <see cref="IsUsed"/> = false after playback completes.
/// @invariant Parameters are pushed to the internal AudioSource each Update.
/// </remarks>
public class SoundManagerHelper : MonoBehaviour
{
    #region Inspector Fields

    /// <summary>
    /// True while this helper is reserved by SoundManager's pool.
    /// </summary>
    [Tooltip("Set by SoundManager when this helper is in use.")]
    public bool IsUsed = false;

    /// <summary>
    /// Transform to follow (if not null). Helper position is set to Target.position each frame.
    /// </summary>
    [Tooltip("Optional Transform to follow for 3D playback.")]
    public Transform Target;

    /// <summary>
    /// AudioClip to play.
    /// </summary>
    [Tooltip("Clip to play on this helper.")]
    public AudioClip Clip;

    /// <summary>
    /// Output volume [0..1].
    /// </summary>
    [Tooltip("Playback volume (0..1).")]
    [Range(0f, 1f)]
    public float Volume = 1.0f;

    /// <summary>
    /// Pitch multiplier.
    /// </summary>
    [Tooltip("Playback pitch multiplier.")]
    public float Pitch = 1.0f;

    /// <summary>
    /// If true, AudioSource.playOnAwake will be set.
    /// </summary>
    [Tooltip("If true, plays automatically when enabled (via AudioSource.playOnAwake).")]
    public bool PlayOnStart = false;

    /// <summary>
    /// If true, the clip will loop.
    /// </summary>
    [Tooltip("Loop the clip.")]
    public bool Loop = false;

    /// <summary>
    /// Spatial blend (0 = 2D, 1 = fully 3D).
    /// </summary>
    [Tooltip("0 = 2D, 1 = 3D.")]
    [Range(0f, 1f)]
    public float SpatialBlend = 0.0f;

    #endregion

    #region Properties

    /// <summary>
    /// True if the internal AudioSource exists and is currently playing.
    /// </summary>
    public bool IsPlaying
    {
        get { return audioSource ? audioSource.isPlaying : false; }
    }

    #endregion

    #region Private State

    /// <summary>
    /// Backing AudioSource created at runtime.
    /// </summary>
    private AudioSource audioSource;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Creates and initializes the AudioSource and marks this object persistent across scenes.
    /// </summary>
    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = Clip;
        audioSource.volume = Volume;
        audioSource.pitch = Pitch;
        audioSource.loop = Loop;
        audioSource.spatialBlend = SpatialBlend;
        audioSource.playOnAwake = PlayOnStart;

        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Follows the target transform (if assigned) and pushes public parameters to the AudioSource.
    /// </summary>
    private void Update()
    {
        if (Target != null)
        {
            transform.position = Target.position;
        }

        if (audioSource != null)
        {
            // Keep the AudioSource in sync with public fields.
            audioSource.clip = Clip;
            audioSource.volume = Volume;
            audioSource.pitch = Pitch;
            audioSource.loop = Loop;
            audioSource.spatialBlend = SpatialBlend;
            audioSource.playOnAwake = PlayOnStart;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Starts playback. If not looping, schedules a coroutine to mark this helper unused after the clip ends.
    /// </summary>
    public void PlaySound()
    {
        if (audioSource != null && Clip != null)
        {
            StopAllCoroutines();
            IsUsed = true;
            audioSource.Play();

            if (!Loop)
            {
                StartCoroutine(SetUnused(audioSource.clip.length));
            }
        }
    }

    /// <summary>
    /// Pauses playback if currently playing. Cancels any pending "SetUnused" coroutine.
    /// </summary>
    public void PauseSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            StopAllCoroutines();
            audioSource.Pause();
        }
    }

    /// <summary>
    /// Stops playback and returns this helper to the pool (<see cref="IsUsed"/> = false).
    /// Cancels any pending "SetUnused" coroutine.
    /// </summary>
    public void StopSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            StopAllCoroutines();
            audioSource.Stop();
            IsUsed = false;
        }
    }

    /// <summary>
    /// Rewinds to the beginning. Does not automatically resume playback.
    /// Cancels any pending "SetUnused" coroutine.
    /// </summary>
    public void ResetSound()
    {
        if (audioSource != null)
        {
            StopAllCoroutines();
            audioSource.Stop();
            audioSource.time = 0f;
        }
    }

    /// <summary>
    /// Destroys the helper GameObject. Prefer letting SoundManager pool reclaim helpers instead.
    /// </summary>
    public void Destroy()
    {
        Destroy(gameObject);
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Waits for the given time (clip duration) and marks this helper as unused for pooling.
    /// </summary>
    /// <param name="time">Time in seconds to wait before releasing.</param>
    private IEnumerator SetUnused(float time)
    {
        yield return new WaitForSeconds(time);
        IsUsed = false;
    }

    #endregion
}
