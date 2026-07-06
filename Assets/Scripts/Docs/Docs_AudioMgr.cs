/**
 * @file Docs_AudioMgr.cs
 * @brief Documentation entry for the Audio Manager subsystem.
 *
 * @defgroup audio_mgr Audio Manager
 * @ingroup systems
 * @brief Centralized background music and sound-effect playback with pooling, clip reuse for loops, and low-pass control.
 *
 * @details
 * The Audio Manager is implemented by ::SoundManager, which derives from Singleton<SoundManager>.
 * It provides one looping music AudioSource with an AudioLowPassFilter and a lightweight SFX layer
 * built from pooled ::SoundManagerHelper instances.
 *
 * Non-looping SFX are intentionally not mapped by clip, so repeated one-shot sounds, such as crashes,
 * may overlap naturally. Looping and continued SFX are mapped by AudioClip, so one active helper is
 * reused for each looping clip. Continued SFX use a short grace timer and are stopped automatically
 * unless they are continued again.
 *
 * Contents:
 * - @ref audio_mgr_overview
 * - @ref audio_mgr_inspector
 * - @ref audio_mgr_lifecycle
 * - @ref audio_mgr_usage
 * - @ref audio_mgr_api
 * - @ref audio_mgr_integration
 * - @ref audio_mgr_performance
 * - @ref audio_mgr_troubleshooting
 * - @ref audio_mgr_versions
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_overview Overview
 *
 * Responsibilities:
 * - Music playback through a single looping AudioSource.
 * - Optional low-pass filtering for music through SoundManager::MusicLowPass.
 * - Pooled SFX playback through SoundManagerHelper objects.
 * - Overlapping one-shot SFX for short repeated sounds.
 * - Per-clip reuse for looping and continued SFX.
 * - Optional AudioMixerGroup routing for music and SFX.
 * - Camera-position tracking for manager-position sounds.
 *
 * Dependencies:
 * - Generic.Singleton<T> base type.
 * - ::SoundManagerHelper for pooled SFX playback.
 * - UnityEngine.Audio for AudioMixerGroup and AudioLowPassFilter.
 *
 * Threading:
 * - Unity main thread only.
 * - Initialization happens in Awake().
 * - Pool maintenance and continuation timers are updated in Update().
 *
 * Invariants:
 * - Exactly one ::SoundManager instance should be active.
 * - Free pooled helpers have IsUsed == false.
 * - Non-looping SFX return their helper to the pool after playback finishes.
 * - Looping and continued SFX remain mapped until stopped, expired, or reused.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_inspector Inspector
 *
 * SoundManager fields:
 * - musicVolume: volume scalar for background music, in range 0..1.
 * - sfxVolume: global volume multiplier for sound effects, in range 0..1.
 * - musicMixerGroup: optional mixer group used by the music source.
 * - sfxMixerGroup: optional mixer group used by SFX helpers.
 * - continueThreshold: time in seconds for which a continued SFX remains active
 *   after the last ContinueSFXClip call.
 *
 * Runtime-created objects:
 * - One AudioSource for background music.
 * - One AudioLowPassFilter for music filtering.
 * - One or more SoundManagerHelper objects created on demand for SFX playback.
 *
 * SoundManagerHelper state:
 * - IsUsed marks whether the helper is currently reserved by the pool.
 * - Target is followed every frame when assigned.
 * - Clip, Volume, Pitch, Loop, SpatialBlend, and OutputMixerGroup are applied to the internal AudioSource.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_lifecycle Lifecycle
 *
 * SoundManager.Awake:
 * - Enforces the singleton rule through Singleton<SoundManager>.
 * - Creates the looping music AudioSource.
 * - Applies the initial music volume and music mixer group.
 * - Creates the AudioLowPassFilter and initializes it to an open cutoff.
 *
 * SoundManager.Update:
 * - Finds Camera.main when needed.
 * - Moves the manager object to the main camera position when a camera exists.
 * - Updates continuation timers for clips registered through ContinueSFXClip.
 * - Stops and unmaps continued looping SFX whose timers have expired.
 *
 * SoundManagerHelper.Awake:
 * - Creates or reuses an AudioSource.
 * - Disables play-on-awake.
 * - Marks the helper object as persistent.
 *
 * SoundManagerHelper.Update:
 * - Follows Target when one is assigned.
 * - Synchronizes volume, pitch, and spatial blend to the internal AudioSource.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_usage Usage
 *
 * Background music:
 * @code{.cs}
 * public class TitleMusic : MonoBehaviour
 * {
 *     public AudioClip theme;
 *
 *     private void Start()
 *     {
 *         SoundManager.Instance.PlayMusic(theme);
 *         SoundManager.Instance.SetMusicVolume(0.7f);
 *     }
 * }
 * @endcode
 *
 * One-shot UI or menu SFX:
 * @code{.cs}
 * SoundManager.Instance.PlaySFXClip(clickClip);
 * SoundManager.Instance.PlaySFXClip(clickClip, 0.8f);
 * @endcode
 *
 * One-shot 3D SFX on a target:
 * @code{.cs}
 * SoundManager.Instance.PlaySFXClip(
 *     crashClip,
 *     hitTransform,
 *     volume: 1.0f,
 *     pitch: 1.0f,
 *     loop: false,
 *     spatialBlend: 1.0f,
 *     reset: true);
 * @endcode
 *
 * Looping SFX with per-clip reuse:
 * @code{.cs}
 * SoundManager.Instance.PlaySFXClip(
 *     engineLoop,
 *     carTransform,
 *     volume: 0.8f,
 *     pitch: 1.0f,
 *     loop: true,
 *     spatialBlend: 1.0f,
 *     reset: false);
 *
 * SoundManager.Instance.PauseSFXClip(engineLoop);
 * SoundManager.Instance.ResetSFXClip(engineLoop);
 * SoundManager.Instance.StopSFXClip(engineLoop);
 * @endcode
 *
 * Continued looping SFX:
 * @code{.cs}
 * // Refreshes the continuation timer each time it is called.
 * // The clip stops automatically after continueThreshold seconds without another call.
 * SoundManager.Instance.ContinueSFXClip(skidLoop);
 * SoundManager.Instance.ContinueSFXClip(skidLoop, 0.6f);
 * @endcode
 *
 * Music low-pass effect:
 * @code{.cs}
 * AudioLowPassFilter lowPass = SoundManager.Instance.MusicLowPass;
 * lowPass.cutoffFrequency = 1200f;
 *
 * // Restore later.
 * lowPass.cutoffFrequency = 22000f;
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_api Public API Reference
 *
 * Properties:
 * - AudioLowPassFilter MusicLowPass:
 *   Provides access to the music low-pass filter.
 *
 * Music:
 * - void PlayMusic(AudioClip clip):
 *   Starts or resumes looping background music for the given clip.
 *
 * - void StopMusic():
 *   Stops the current music source if it is playing.
 *
 * - void SetMusicVolume(float volume):
 *   Sets the music volume after clamping the value to 0..1.
 *
 * SFX:
 * - void PlaySFXClip(AudioClip clip):
 *   Plays a non-looping SFX at the manager position.
 *
 * - void PlaySFXClip(AudioClip clip, float volume):
 *   Plays a non-looping SFX at the manager position with a custom volume.
 *
 * - void PlaySFXClip(AudioClip clip, Transform target):
 *   Plays a non-looping SFX following the given target.
 *
 * - void PlaySFXClip(AudioClip clip, Transform target, float volume = 1f,
 *                    float pitch = 1f, bool loop = false,
 *                    float spatialBlend = 0.0f, bool reset = true):
 *   Plays a sound effect with full control over volume, pitch, looping, spatial blend, and reset behaviour.
 *
 *   Behaviour:
 *   - If loop is false, a free helper is used and the sound may overlap with other plays of the same clip.
 *   - If loop is true, the clip is mapped to one helper and reused.
 *   - If a looping clip is already mapped and reset is true, playback is rewound and restarted.
 *   - If a looping clip is already mapped and reset is false, playback continues when already active.
 *
 * - void ContinueSFXClip(AudioClip clip):
 *   Starts or refreshes a looping SFX at the manager position and schedules it to stop after continueThreshold.
 *
 * - void ContinueSFXClip(AudioClip clip, float volume):
 *   Same as ContinueSFXClip(AudioClip), but with an explicit volume.
 *
 * - void PauseSFXClip(AudioClip clip):
 *   Pauses a mapped looping or continued clip.
 *
 * - void ResetSFXClip(AudioClip clip):
 *   Rewinds a mapped clip to the beginning.
 *
 * - void StopSFXClip(AudioClip clip):
 *   Stops a mapped clip, removes it from continuation tracking, and releases its helper.
 *
 * - void SetSFXVolume(float volume):
 *   Sets the global SFX volume multiplier after clamping the value to 0..1.
 *   Existing one-shot sounds are not retroactively changed.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_integration Integration Notes
 *
 * Vehicle systems:
 * - Use one-shot SFX for crash sounds so repeated impacts can overlap naturally.
 * - Use looping SFX or ContinueSFXClip for effects that should persist while a condition remains true.
 * - Engine audio can be handled separately through EngineSound, while one-shot events may still use SoundManager.
 *
 * UI:
 * - Use PlaySFXClip(AudioClip) or PlaySFXClip(AudioClip, float) for non-positional UI feedback.
 *
 * Scene management:
 * - SceneManagement may call SoundManager::PlayMusic when a scene-specific music clip is matched.
 *
 * Audio setup:
 * - Ensure exactly one active AudioListener exists, usually on the main camera.
 * - Assign mixer groups when music and SFX should be routed to separate mixer buses.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_performance Performance and GC
 *
 * - SFX helpers are pooled and reused.
 * - One-shot SFX allocate only when no free helper exists.
 * - Looping and continued SFX reuse one helper per mapped AudioClip.
 * - Continued SFX use a short timer instead of starting and stopping every frame.
 * - Keep continueThreshold small, usually around 0.1 to 0.25 seconds, to avoid unwanted lingering loops.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_troubleshooting Troubleshooting
 *
 * No sound:
 * - Check that an AudioListener exists.
 * - Check mixer routing.
 * - Check musicVolume, sfxVolume, and per-call volume.
 * - Check that the AudioClip reference is assigned.
 *
 * One-shot sound cuts itself off:
 * - Non-looping SFX should not be mapped. Use loop: false for crashes, clicks, and short impacts.
 *
 * Looping sound does not stop:
 * - Repeated ContinueSFXClip calls refresh the timer.
 * - Call StopSFXClip manually if the sound should stop immediately.
 *
 * 3D sound is not positioned correctly:
 * - Pass a valid target Transform.
 * - Use spatialBlend: 1.0f for fully spatialized playback.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_versions Version History
 *
 * - v1.2: One-shot SFX no longer mapped by clip, allowing repeated one-shots to overlap.
 * - v1.1: Added pooled helpers, per-clip loop mapping, ContinueSFXClip, and MusicLowPass.
 * - v1.0: Basic music and SFX playback with volume and mixer routing.
 */