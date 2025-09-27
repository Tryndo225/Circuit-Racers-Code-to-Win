using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralized scene loader and background-music router.
/// Loads scenes by helper or name, unpauses time, and selects a music clip
/// based on a configured list of scene-to-clip pairs.
/// </summary>
/// <remarks>
/// @ingroup scene_mgmt
/// @thread Unity main thread only (Start, scene loads).
/// @invariant SoundManager.Instance may be used to play music if available.
/// @invariant Each configured pair maps one scene (by name) to one AudioClip.
/// </remarks>
public class SceneManagement : Generic.Singleton<SceneManagement>
{
    #region Inspector

    [Header("Scene -> Music Mapping")]
    /// <summary>
    /// List of mappings from scene to music clip. When a scene loads, the first
    /// matching pair (by scene name) will have its AudioClip played via SoundManager.
    /// </summary>
    [Tooltip("List of scene and corresponding audio clip pairs. The audio clip will be played when the scene is loaded.")]
    [SerializeField] private List<SceneAssetHelperAudioClipPair> sceneAudioClipPairs = new List<SceneAssetHelperAudioClipPair>();

    #endregion

    #region Unity methods

    /// <summary>
    /// On startup, plays music for the currently active scene if a mapping exists.
    /// </summary>
    private void Start()
    {
        MatchMusicClip(SceneManager.GetActiveScene());
    }

    #endregion

    #region Public API

    /// <summary>
    /// Loads a scene by a SceneAssetHelper (name-based), unpauses time,
    /// and plays the mapped music clip if configured.
    /// </summary>
    /// <param name="scene">Helper carrying the scene name and optional display name.</param>
    /// <remarks>
    /// If the helper's name equals "Quit Game", the application will quit instead of loading.
    /// </remarks>
    public void ChangeScene(SceneAssetHelper scene)
    {
        Debug.Log($"Changing scene to: {scene.Name}");

        if (scene.Name == "Quit Game")
        {
            Debug.Log("Quitting game...");
            QuitGame();
            return;
        }

        SceneManager.LoadScene(scene.Name);
        Time.timeScale = 1f;

        MatchMusicClip(scene);
    }

    /// <summary>
    /// Loads a scene by string name, unpauses time, and plays the mapped music clip if configured.
    /// </summary>
    /// <param name="sceneName">Scene name as added in Build Settings.</param>
    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        Time.timeScale = 1f;

        // Create a lightweight helper to reuse the same music matching path.
        MatchMusicClip(new SceneAssetHelper(sceneName, sceneName));
    }

    #endregion

    #region Internals

    /// <summary>
    /// Attempts to find and play a music clip for the given scene using the configured list.
    /// </summary>
    /// <param name="scene">Scene helper used for name matching.</param>
    /// <remarks>
    /// A match is determined by equality against the pair's scene. Only the first match triggers music.
    /// Requires SoundManager.Instance to be present in the scene.
    /// </remarks>
    private void MatchMusicClip(SceneAssetHelper scene)
    {
        for (int i = 0; i < sceneAudioClipPairs.Count; i++)
        {
            if (scene == sceneAudioClipPairs[i])
            {
                // Defensive: SoundManager may not yet have initialized in edge cases.
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayMusic(sceneAudioClipPairs[i].AudioClip);
                }
                else
                {
                    Debug.LogWarning("SoundManager.Instance is null; cannot play scene music.");
                }

                // Stop after first match to avoid overlapping PlayMusic calls.
                return;
            }
        }
    }

    /// <summary>
    /// Attempts to find and play a music clip for the given Scene.
    /// </summary>
    /// <param name="scene">UnityEngine.SceneManagement.Scene to match by name.</param>
    private void MatchMusicClip(Scene scene)
    {
        // Reuse the helper-based path for consistency.
        MatchMusicClip(new SceneAssetHelper(scene.name, scene.name));
    }

    /// <summary>
    /// Quits the application. In the editor this has no effect at runtime builds only.
    /// </summary>
    private void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }

    #endregion
}
