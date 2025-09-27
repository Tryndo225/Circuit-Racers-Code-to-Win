/**
 * @file Docs_Scene.cs
 * @brief Documentation entry for the Scene Management subsystem.
 *
 * @defgroup scene_mgr Scene Management
 * @ingroup systems
 * @brief Centralized scene loading with editor-friendly scene references and scene->music pairing.
 *
 * @details
 * The scene system is implemented by:
 * - ::SceneManagement — a Singleton that loads scenes, normalizes time scale, and selects background music.
 * - ::SceneAssetHelper — a serializable, editor-friendly scene reference (name + path) that stays in sync with a SceneAsset.
 * - ::SceneAssetHelperAudioClipPair — a scene reference paired with an AudioClip used for automatic music selection.
 *
 * Contents:
 * - see scene_mgr_overview
 * - see scene_mgr_inspector
 * - see scene_mgr_lifecycle
 * - see scene_mgr_usage
 * - see scene_mgr_api
 * - see scene_mgr_integration
 * - see scene_mgr_performance
 * - see scene_mgr_troubleshooting
 * - see scene_mgr_versions
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_overview Overview
 *
 * Responsibilities:
 * - Single entry point for scene changes (string or helper-based).
 * - Time normalization: sets Time.timeScale = 1 after scene loads.
 * - Music pairing: picks a background track based on configured scene->clip pairs.
 * - Quit sentinel: recognizes the scene name "Quit Game" and exits the application.
 *
 * Dependencies:
 * - Generic.Singleton<T> base type (for ::SceneManagement).
 * - ::SoundManager (for background music via PlayMusic).
 * - UnityEditor-only API (for ::SceneAssetHelper editor synchronization).
 *
 * Threading:
 * - Unity main thread. Initialization in Start(), scene operations via SceneManager.LoadScene().
 *
 * Invariants:
 * - Exactly one ::SceneManagement instance is active (enforced by Singleton).
 * - ::SceneAssetHelper equality is defined by scene path (string compare).
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_inspector Inspector (Key Components)
 *
 * SceneManagement:
 * - sceneAudioClipPairs (List<SceneAssetHelperAudioClipPair>):
 *   Array of “scene helper + AudioClip” items. When a scene is loaded, the manager searches this list
 *   and plays the first AudioClip whose helper matches the loaded scene.
 *
 * SceneAssetHelper (when nested/serialized in other components):
 * - sceneName (string, ReadOnly): cached name of the scene (auto-filled in Editor).
 * - scenePath (string, ReadOnly): cached asset path of the scene (auto-filled in Editor).
 * - (Editor only) sceneAsset (UnityEditor.SceneAsset): drag a scene here to keep name/path consistent.
 * - Name (property): public getter for sceneName.
 * - Path (property): public getter for scenePath.
 *
 * SceneAssetHelperAudioClipPair:
 * - Inherits all ::SceneAssetHelper fields.
 * - audioClip (AudioClip): the music track to play when this scene is loaded.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_lifecycle Lifecycle
 *
 * SceneManagement.Start:
 * - Reads the currently active scene (SceneManager.GetActiveScene()) and attempts to match a music clip
 *   via sceneAudioClipPairs. If a match is found, calls SoundManager.Instance.PlayMusic().
 *
 * Scene changes (any path):
 * - Load target scene using SceneManager.LoadScene(...).
 * - Reset Time.timeScale = 1 to ensure deterministic gameplay speed.
 * - Attempt to match and play a background track via sceneAudioClipPairs.
 *
 * Editor-only serialization (SceneAssetHelper):
 * - OnBeforeSerialize(): when a SceneAsset is assigned, updates sceneName and scenePath to match the asset.
 * - OnAfterDeserialize(): no-op (values are already consistent).
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_usage Usage
 *
 * Helper-based change (preferred):
 * @code{.cs}
 * public class PlayButton : MonoBehaviour
 * {
 *     [SerializeField] private SceneAssetHelper targetScene;
 *
 *     public void OnClick()
 *     {
 *         SceneManagement.Instance.ChangeScene(targetScene);
 *     }
 * }
 * @endcode
 *
 * String-based change (quick but fragile):
 * @code{.cs}
 * SceneManagement.Instance.ChangeScene("Main Menu");
 * @endcode
 *
 * Implicit conversions:
 * @code{.cs}
 * // Scene -> SceneAssetHelper
 * var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
 * SceneAssetHelper helper = active; // captures name and path
 *
 * // SceneAssetHelper -> Scene (identifies scene by path; not necessarily loaded)
 * Scene scene = helper;
 * @endcode
 *
 * Quit sentinel:
 * @code{.cs}
 * // If helper.Name == "Quit Game", the manager will call Application.Quit();
 * SceneManagement.Instance.ChangeScene(new SceneAssetHelper("Quit Game", "Assets/Scenes/Quit Game.unity"));
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_api Public API Reference
 *
 * SceneManagement (Singleton):
 * - void ChangeScene(SceneAssetHelper scene):
 *     Loads by scene.Name, resets time scale to 1, then attempts to play mapped music.
 *     If scene.Name == "Quit Game", calls Application.Quit() instead.
 * - void ChangeScene(string sceneName):
 *     Convenience overload for string-based loading; also resets time scale and matches music via a temporary helper.
 *
 * SceneAssetHelper (SerializableRunnable):
 * - string Name { get; } — scene name (serialized).
 * - string Path { get; } — scene asset path (serialized).
 * - void Run(): delegates to SceneManagement.Instance.ChangeScene(this).
 * - (Editor only) OnBeforeSerialize(): syncs Name/Path from SceneAsset.
 * - Implicit conversions:
 *     static implicit operator SceneAssetHelper(Scene scene)
 *     static implicit operator Scene(SceneAssetHelper helper)
 *     (Editor only) static implicit operator SceneAssetHelper(UnityEditor.SceneAsset sceneAsset)
 * - Equality:
 *     operator ==, operator !=, Equals(object), GetHashCode() — all based on Path.
 *
 * SceneAssetHelperAudioClipPair : SceneAssetHelper
 * - AudioClip AudioClip { get; } — the music to use for this scene.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_integration Integration Notes
 *
 * Audio:
 * - Requires ::SoundManager singleton with PlayMusic(AudioClip). Ensure exactly one AudioListener in the scene.
 *
 * UI:
 * - Serialize SceneAssetHelper fields on UI buttons or menus to provide robust scene selection in the Editor.
 *
 * Transitions / UX:
 * - This centralized manager is the best place to add fades, loading screens, or audio mixer snapshots
 *   before/after SceneManager.LoadScene().
 *
 * Async/Additive:
 * - For large scenes or streaming, extend with LoadSceneAsync, additive loading, and unload flows.
 *   Decide how music selection should behave across additive scenes (first-load wins, priority, etc.).
 *
 * Build Settings:
 * - All target scenes must be listed in File -> Build Settings, or runtime loads will fail in builds.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_performance Performance and GC
 *
 * - Helper-based references avoid string lookups at call sites and keep inspector data consistent.
 * - Music selection uses a linear scan of sceneAudioClipPairs; keep the list compact or swap to a dictionary keyed by path.
 * - Avoid redundant ChangeScene calls in a single frame to prevent spurious loads.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_troubleshooting Troubleshooting
 *
 * - Nothing loads:
 *   - Ensure the scene is added to Build Settings.
 *   - Verify the helper’s Name matches the actual scene name.
 *
 * - No music on scene load:
 *   - Confirm a matching SceneAssetHelperAudioClipPair exists (equality uses Path).
 *   - Ensure SoundManager exists in the scene and the clip is assigned.
 *
 * - Quit does nothing in Editor:
 *   - Application.Quit() is ignored in the Editor. Test in a build or wrap with editor handling.
 *
 * - Helper shows empty Name/Path:
 *   - Assign a SceneAsset in the Editor. The helper syncs Name/Path on serialization.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgr_versions Version History
 *
 * - v1.4: Added Docs_Scene.cs; improved documentation.
 * - v1.3: Made SceneAssetHelperAudioClipPair; added music pairing in SceneManagement.
 * - v1.2: Made SceneAssetHelper; added implicit conversions and equality.
 * - v1.1: Added Quit sentinel.
 * - v1.0: Initial version with scene loading.
 */
