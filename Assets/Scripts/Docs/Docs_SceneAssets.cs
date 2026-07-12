/**
 * @file Docs_SceneAssets.cs
 * @brief Documentation entry for the Scene Management subsystem.
 *
 * @defgroup scene_mgmt Scene Management
 * @ingroup systems
 * @brief Centralized scene loading with editor-friendly scene references and scene-to-music pairing.
 *
 * @details
 * The scene system is implemented by:
 * - ::SceneManagement, a singleton scene loader and background-music router.
 * - ::SceneAssetHelper, a serializable editor-friendly scene reference that stores a scene name and path.
 * - ::SceneAssetHelperAudioClipPair, a scene reference paired with an AudioClip for automatic music selection.
 *
 * Scene changes can be triggered directly through ::SceneManagement or indirectly through
 * ::SceneAssetHelper::Run because SceneAssetHelper derives from ::SerializableRunnable.
 *
 * Contents:
 * - @ref scene_mgmt_overview
 * - @ref scene_mgmt_inspector
 * - @ref scene_mgmt_lifecycle
 * - @ref scene_mgmt_usage
 * - @ref scene_mgmt_api
 * - @ref scene_mgmt_integration
 * - @ref scene_mgmt_performance
 * - @ref scene_mgmt_troubleshooting
 * - @ref scene_mgmt_versions
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_overview Overview
 *
 * Responsibilities:
 * - Provide a single scene-change entry point.
 * - Load scenes from either a ::SceneAssetHelper or a string.
 * - Restore Time.timeScale to 1 after starting a scene load.
 * - Match loaded scenes to configured music clips.
 * - Play matched music through ::SoundManager.
 * - Support a "Quit Game" scene-name sentinel that calls Application.Quit().
 *
 * Dependencies:
 * - Generic::Singleton<T> base type for ::SceneManagement.
 * - ::SoundManager for background music playback.
 * - ::SerializableRunnable for runnable scene actions.
 * - UnityEngine.SceneManagement for scene loading and scene lookup.
 * - UnityEditor.SceneAsset and UnityEditor.AssetDatabase in editor-only synchronization code.
 *
 * Threading:
 * - Unity main thread only.
 * - Scene loading uses SceneManager.LoadScene.
 * - Music matching is performed through Unity coroutines.
 *
 * Invariants:
 * - Exactly one ::SceneManagement instance should be active.
 * - ::SceneAssetHelper stores scene name and scene path as serialized strings.
 * - Editor-only SceneAsset references are guarded behind UNITY_EDITOR.
 * - ::SceneAssetHelper equality and hashing are based on scene path.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_inspector Inspector
 *
 * ::SceneManagement:
 * - sceneAudioClipPairs:
 *   List of ::SceneAssetHelperAudioClipPair entries.
 *   When a scene is loaded, the first pair matching the scene name or scene path is used.
 *
 * ::SceneAssetHelper:
 * - sceneName:
 *   Serialized scene name, shown read-only in the Inspector.
 *
 * - scenePath:
 *   Serialized scene asset path, shown read-only in the Inspector.
 *
 * - sceneAsset:
 *   Editor-only UnityEditor.SceneAsset reference.
 *   When assigned in the Unity Editor, it fills sceneName and scenePath before serialization.
 *
 * - Name:
 *   Public getter for sceneName.
 *
 * - Path:
 *   Public getter for scenePath.
 *
 * ::SceneAssetHelperAudioClipPair:
 * - Inherits sceneName, scenePath, editor synchronization, conversions, and equality from ::SceneAssetHelper.
 * - audioClip:
 *   Music clip used when this scene pair is matched.
 *
 * - AudioClip:
 *   Public getter for the paired music clip.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_lifecycle Lifecycle
 *
 * SceneManagement.Start:
 * - Waits until ::SoundManager.Instance exists.
 * - Reads the currently active scene through SceneManager.GetActiveScene().
 * - Attempts to match the active scene to a configured music clip.
 * - Plays the matched music through ::SoundManager::PlayMusic.
 *
 * SceneManagement.ChangeScene(SceneAssetHelper):
 * - Rejects null helpers.
 * - If scene.Name is "Quit Game", calls Application.Quit() instead of loading a scene.
 * - Chooses scene.Name when available, otherwise scene.Path.
 * - Rejects empty scene identifiers.
 * - Calls SceneManager.LoadScene.
 * - Restores Time.timeScale to 1.
 * - Starts music matching for the requested scene name/path.
 *
 * SceneManagement.ChangeScene(string):
 * - Rejects null, empty, or whitespace scene names.
 * - Calls SceneManager.LoadScene with the provided string.
 * - Restores Time.timeScale to 1.
 * - Starts music matching and passes the string as both scene name and scene path.
 *
 * SceneAssetHelper.OnBeforeSerialize:
 * - In the editor, synchronizes sceneName and scenePath from the assigned SceneAsset.
 * - At runtime, does nothing because the editor-only SceneAsset is not available.
 *
 * SceneAssetHelper.OnAfterDeserialize:
 * - Intentionally empty.
 * - Stored sceneName and scenePath are already serialized directly.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_usage Usage
 *
 * Helper-based scene change:
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
 * String-based scene change:
 * @code{.cs}
 * SceneManagement.Instance.ChangeScene("Main Menu");
 * @endcode
 *
 * Runnable scene action:
 * @code{.cs}
 * [SerializeField] private SceneAssetHelper targetScene;
 *
 * public void RunAction()
 * {
 *     targetScene.Run();
 * }
 * @endcode
 *
 * Implicit conversions:
 * @code{.cs}
 * // Scene -> SceneAssetHelper
 * Scene active = SceneManager.GetActiveScene();
 * SceneAssetHelper helper = active;
 *
 * // SceneAssetHelper -> Scene
 * // This resolves by path and only returns a valid Scene if that scene is loaded.
 * Scene scene = helper;
 * @endcode
 *
 * Quit sentinel:
 * @code{.cs}
 * SceneAssetHelper quit = new SceneAssetHelper("Quit Game", string.Empty);
 * SceneManagement.Instance.ChangeScene(quit);
 * @endcode
 *
 * Scene-to-music pair:
 * @code{.cs}
 * // Configure SceneAssetHelperAudioClipPair entries in the SceneManagement inspector.
 * // When the scene name or path matches, SceneManagement plays pair.AudioClip.
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_api Public API Reference
 *
 * ::SceneManagement:
 * - void ChangeScene(SceneAssetHelper scene)
 *   Loads the scene described by the helper. Uses scene.Name first and scene.Path as fallback.
 *   If scene.Name equals "Quit Game", quits the application instead.
 *
 * - void ChangeScene(string sceneName)
 *   Loads a scene by string name or path and uses the same string for music matching.
 *
 * ::SceneAssetHelper:
 * - string Name
 *   Gets the serialized scene name.
 *
 * - string Path
 *   Gets the serialized scene path.
 *
 * - SceneAssetHelper()
 *   Creates an empty scene helper.
 *
 * - SceneAssetHelper(string sceneName, string scenePath)
 *   Creates a helper from known scene-name and scene-path strings.
 *
 * - void Run()
 *   Delegates to SceneManagement.Instance.ChangeScene(this).
 *
 * - void OnBeforeSerialize()
 *   Synchronizes editor scene data before serialization.
 *
 * - void OnAfterDeserialize()
 *   Runtime/editor deserialization hook. Intentionally does nothing.
 *
 * - static implicit operator SceneAssetHelper(Scene scene)
 *   Creates a helper from a loaded Unity scene.
 *
 * - static implicit operator Scene(SceneAssetHelper sceneAssetHelper)
 *   Resolves a Unity Scene by stored scene path.
 *
 * - static implicit operator SceneAssetHelper(UnityEditor.SceneAsset sceneAsset)
 *   Editor-only conversion from a SceneAsset to a helper.
 *
 * - operator == and operator !=
 *   Compare helpers by scene path, with null-safe handling.
 *
 * - bool Equals(object obj)
 *   Compares helpers by scene path.
 *
 * - int GetHashCode()
 *   Computes a hash from the stored scene path.
 *
 * ::SceneAssetHelperAudioClipPair:
 * - AudioClip AudioClip
 *   Gets the music clip associated with the scene helper.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_integration Integration Notes
 *
 * UI:
 * - ::ChangeSceneButton uses ::SceneManagement to load a configured ::SceneAssetHelper.
 * - Replay, edit, and gameplay buttons can validate game state before calling the scene-change action.
 * - SceneAssetHelper fields are useful for Inspector-configured menu buttons.
 *
 * Game data:
 * - Gameplay scene buttons may choose day or night scene helpers based on ::levelMap::IsDayTrack.
 * - ::GameDataManager::GoToSelectedLevel can also route into scene loading.
 *
 * Audio:
 * - SceneManagement waits for ::SoundManager before matching and playing music.
 * - Each scene can be paired with a music clip through sceneAudioClipPairs.
 * - Matching prefers scene name, then scene path.
 * - A string-based scene load passes the same string as both name and path for matching.
 *
 * Build settings:
 * - Scenes loaded by name must be included in Build Settings.
 * - Scene paths are useful in the editor and for matching, but loaded scenes still need to be build-accessible.
 *
 * Extensibility:
 * - Fades, loading screens, async loading, additive loading, or mixer snapshot transitions can be added centrally here.
 * - For additive scenes, define a music priority rule before extending music matching.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_performance Performance and GC
 *
 * - Scene music matching scans sceneAudioClipPairs linearly.
 * - This is fine for small menus and a small number of scenes.
 * - If the project grows many scene/music pairs, use a dictionary keyed by scene name or path.
 * - Avoid calling ChangeScene multiple times in the same frame.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_troubleshooting Troubleshooting
 *
 * Scene does not load:
 * - Check that the scene exists in Build Settings.
 * - Check that SceneAssetHelper.Name is not empty.
 * - If loading by path, check that the path is valid for the intended context.
 *
 * Helper shows empty name/path:
 * - Assign a SceneAsset in the Unity Editor.
 * - SceneAssetHelper clears sceneName and scenePath when the editor SceneAsset field is empty.
 *
 * No music plays:
 * - Ensure ::SoundManager exists.
 * - Ensure sceneAudioClipPairs contains a matching pair.
 * - Ensure the pair's AudioClip is assigned.
 * - Check whether the loaded scene name/path matches the stored helper name/path.
 *
 * Music appears delayed:
 * - SceneManagement waits for ::SoundManager.Instance before matching music.
 * - Make sure SoundManager exists early enough in the startup scene.
 *
 * Quit does nothing in the Unity Editor:
 * - Application.Quit() has no visible effect in editor play mode.
 * - Test quitting in a build, or add editor-specific handling if desired.
 *
 * Multiple scene managers:
 * - SceneManagement derives from Singleton, so duplicates are destroyed.
 * - Keep only one intended SceneManagement object in bootstrap/menu scenes.
 *
 * ----------------------------------------------------------------------
 * @section scene_mgmt_versions Version History
 *
 * - v1.4: Updated documentation to use the scene_mgmt group and current helper/music matching behaviour.
 * - v1.3: Added SceneAssetHelperAudioClipPair and scene-to-music matching.
 * - v1.2: Added SceneAssetHelper, editor SceneAsset synchronization, implicit conversions, and path-based equality.
 * - v1.1: Added "Quit Game" sentinel.
 * - v1.0: Initial centralized scene loading.
 */