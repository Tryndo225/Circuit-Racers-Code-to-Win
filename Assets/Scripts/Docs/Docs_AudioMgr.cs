/**
 * @file Docs_AudioMgr.cs
 * @brief Documentation entry for the Audio Manager subsystem.
 *
 * @defgroup audio_mgr Audio Manager
 * @ingroup systems
 * @brief Centralized music and SFX playback with pooling, per-clip reuse, and low-pass control.
 *
 * @details
 * The Audio Manager is implemented by ::SoundManager (a Singleton<SoundManager>).
 * It provides a single looping music source (with an AudioLowPassFilter) and a lightweight,
 * allocation-friendly SFX layer built on pooled ::SoundManagerHelper instances. SFX can be
 * played as 2D or 3D, reused per clip, paused/reset/stopped, and "continued" for a short
 * tail using a grace timer.
 *
 * Contents:
 * - see audio_mgr_overview
 * - see audio_mgr_inspector
 * - see audio_mgr_lifecycle
 * - see audio_mgr_usage
 * - see audio_mgr_api
 * - see audio_mgr_integration
 * - see audio_mgr_performance
 * - see audio_mgr_troubleshooting
 * - see audio_mgr_versions
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_overview Overview
 *
 * Responsibilities:
 * - Music: single AudioSource for background music, optional low-pass control.
 * - SFX: pooled helpers keyed by AudioClip for reuse and minimal allocations.
 * - Routing: optional AudioMixerGroup for music and SFX.
 * - Listener-follow: manager and helpers follow Camera.main position each frame.
 *
 * Dependencies:
 * - Generic.Singleton<T> base type.
 * - ::SoundManagerHelper (pooled SFX player component).
 * - UnityEngine.Audio for AudioMixerGroup and filters.
 *
 * Threading:
 * - Unity main thread. Initialization in Awake(), maintenance in Update().
 *
 * Invariants:
 * - Exactly one ::SoundManager instance is active (enforced by Singleton).
 * - Pooled helpers are reused; free helpers are marked IsUsed == false.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_inspector Inspector (SoundManager)
 *
 * Fields:
 * - musicVolume (float, 0..1): master music volume.
 * - sfxVolume   (float, 0..1): global SFX multiplier applied to all SFX plays.
 * - musicMixerGroup (AudioMixerGroup): optional bus for music.
 * - sfxMixerGroup   (AudioMixerGroup): optional bus for SFX.
 * - continueThreshold (float, seconds): grace period for ContinueSFXClip before auto-stop.
 *
 * Internals created at runtime:
 * - One AudioSource for music (looping).
 * - One AudioLowPassFilter (exposed via SoundManager::MusicLowPass).
 * - A pool of SoundManagerHelper objects for SFX (created on demand).
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_lifecycle Lifecycle
 *
 * Awake:
 * - Creates and configures the music AudioSource and AudioLowPassFilter.
 * - Reads initial volumes from inspector.
 *
 * Update:
 * - Tracks Camera.main; sets manager transform to the camera position.
 * - Mirrors manager position to all pooled SFX helpers.
 * - Services "continue" timers and auto-stops expired looping SFX; unmaps them.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_usage Usage
 *
 * Quick start (music):
 * @code{.cs}
 * public class TitleMusic : MonoBehaviour
 * {
 *     public AudioClip theme;
 *     void Start()
 *     {
 *         SoundManager.Instance.PlayMusic(theme);
 *         SoundManager.Instance.SetMusicVolume(0.7f);
 *     }
 * }
 * @endcode
 *
 * 3D SFX on a Transform:
 * @code{.cs}
 * // One-shot crash at impact point
 * SoundManager.Instance.PlaySFXClip(crashClip, hitTransform,
 *     volume: 1.0f, pitch: 1.0f, loop: false, specialBlend: 1.0f, reset: true);
 * @endcode
 *
 * Looping engine layer with reuse:
 * @code{.cs}
 * // Start looping (no reset if already playing this clip)
 * SoundManager.Instance.PlaySFXClip(engineLoop, carTransform,
 *     volume: 0.8f, pitch: 1.0f, loop: true, specialBlend: 1.0f, reset: false);
 *
 * // Later: pause / reset / stop
 * SoundManager.Instance.PauseSFXClip(engineLoop);
 * SoundManager.Instance.ResetSFXClip(engineLoop); // rewind to start
 * SoundManager.Instance.StopSFXClip(engineLoop);
 * @endcode
 *
 * "Continue" tail:
 * @code{.cs}
 * // Keeps playing for continueThreshold seconds after last call, then auto-stops.
 * SoundManager.Instance.ContinueSFXClip(skidLoop);
 * SoundManager.Instance.ContinueSFXClip(skidLoop, 0.6f); // explicit start volume
 * @endcode
 *
 * Music duck or muffle:
 * @code{.cs}
 * var lp = SoundManager.Instance.MusicLowPass;
 * lp.cutoffFrequency = 1200f;     // muffle during pause
 * // restore later to 22000f
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_api Public API Reference
 *
 * Properties:
 * - AudioLowPassFilter MusicLowPass: access the music low-pass component.
 *
 * Music:
 * - void PlayMusic(AudioClip clip): starts or resumes looping music for clip.
 * - void StopMusic(): stops the current music source if playing.
 * - void SetMusicVolume(float volume01): sets the [0..1] music volume.
 *
 * SFX (pooled per-clip):
 * - void PlaySFXClip(AudioClip clip): plays at manager position (2D by default).
 * - void PlaySFXClip(AudioClip clip, float volume): same with starting volume.
 * - void PlaySFXClip(AudioClip clip, Transform transform): plays spatially at a target.
 * - void PlaySFXClip(AudioClip clip, Transform transform,
 *                    float volume = 1f, float pitch = 1f,
 *                    bool loop = false, float specialBlend = 0.0f, bool reset = true):
 *     Plays or loops with full control.
 *     specialBlend = 0 for 2D, 1 for fully 3D.
 *     If this clip is already mapped:
 *       - reset == true: rewinds and plays from the start.
 *       - reset == false: resumes/continues if available.
 * - void ContinueSFXClip(AudioClip clip): loops and schedules auto-stop after continueThreshold seconds since last call.
 * - void ContinueSFXClip(AudioClip clip, float volume): same with explicit starting volume.
 * - void PauseSFXClip(AudioClip clip): pauses a mapped clip.
 * - void ResetSFXClip(AudioClip clip): rewinds a mapped clip to time = 0.
 * - void StopSFXClip(AudioClip clip): stops and unmaps the helper for this clip.
 * - void SetSFXVolume(float volume01): sets global SFX multiplier (affects subsequent plays).
 *
 * Notes:
 * - All SFX are routed through sfxMixerGroup if assigned.
 * - The internal SoundManagerHelper applies pitch, spatial blend, looping, and 3D rolloff.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_integration Integration Notes
 *
 * Vehicle systems:
 * - Pair engine audio with transmission events by calling EngineSound.OnShift().
 * - Use ContinueSFXClip for short-lived loop tails (for example, skid).
 *
 * UI:
 * - Use PlaySFXClip(clip) overloads for non-positional UI feedback.
 *
 * Scene setup:
 * - Ensure a single AudioListener exists (usually on the main camera).
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_performance Performance and GC
 *
 * - SFX helpers are pooled and reused per clip to minimize allocations.
 * - Avoid calling ContinueSFXClip every frame for the same clip unless you want to extend the tail.
 * - Keep continueThreshold small (about 0.1 to 0.25 s) to prevent build-up.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_troubleshooting Troubleshooting
 *
 * - No sound: check mixer routing, ensure an AudioListener exists, and verify volumes > 0.
 * - Multiple managers: only one SoundManager should be active (Singleton prevents duplicates).
 * - Clip does not stop: repeated ContinueSFXClip calls refresh the timer; reduce calls or use StopSFXClip.
 *
 * ----------------------------------------------------------------------
 * @section audio_mgr_versions Version History
 *
 * - v1.1: Pooled helpers, per-clip mapping, ContinueSFXClip, MusicLowPass.
 * - v1.0: Basic music and SFX playback with volumes and mixer routing.
 */
