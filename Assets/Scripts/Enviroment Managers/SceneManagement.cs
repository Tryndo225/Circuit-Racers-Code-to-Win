using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralized scene loader and background-music router.
/// Loads scenes by helper or name, unpauses time, and selects a music clip
/// based on a configured list of scene-to-clip pairs.
/// </summary>
/// <remarks>
/// @ingroup scene_mgmt
/// @thread Unity main thread only.
/// @invariant Scene music is matched by scene name first, then scene path as fallback.
/// @invariant Each configured pair maps one scene to one AudioClip.
/// </remarks>
public class SceneManagement : Generic.Singleton<SceneManagement>
{
	#region Inspector

	[Header("Scene -> Music Mapping")]
	/// <summary>
	/// List of mappings from scene to music clip.
	/// When a scene loads, the first pair with a matching scene name or path is played.
	/// </summary>
	[Tooltip("List of scene and corresponding audio clip pairs. The audio clip will be played when the scene is loaded.")]
	[SerializeField] private List<SceneAssetHelperAudioClipPair> sceneAudioClipPairs = new List<SceneAssetHelperAudioClipPair>();

	#endregion

	#region Unity methods

	/// <summary>
	/// On startup, waits for SoundManager and plays music for the currently active scene.
	/// </summary>
	private IEnumerator Start()
	{
		yield return WaitForSoundManager();

		Scene currentScene = SceneManager.GetActiveScene();

		Debug.Log($"[SceneManagement] Matching music to starting scene: {currentScene.name} ({currentScene.path})");

		MatchMusicClip(currentScene.name, currentScene.path);
	}

	#endregion

	#region Public API

	/// <summary>
	/// Loads a scene by a SceneAssetHelper, unpauses time,
	/// and plays the mapped music clip if configured.
	/// </summary>
	/// <param name="scene">Helper carrying the scene name and path.</param>
	/// <remarks>
	/// If the helper's name equals "Quit Game", the application will quit instead of loading.
	/// </remarks>
	public void ChangeScene(SceneAssetHelper scene)
	{
		if (scene == null)
		{
			Debug.LogError("[SceneManagement] ChangeScene called with null scene.");
			return;
		}

		Debug.Log($"[SceneManagement] Changing scene to: {scene.Name}");

		if (scene.Name == "Quit Game")
		{
			Debug.Log("[SceneManagement] Quitting game...");
			QuitGame();
			return;
		}

		string sceneToLoad = !string.IsNullOrWhiteSpace(scene.Name) ? scene.Name : scene.Path;

		if (string.IsNullOrWhiteSpace(sceneToLoad))
		{
			Debug.LogError("[SceneManagement] Cannot change scene; scene name and path are empty.");
			return;
		}

		SceneManager.LoadScene(sceneToLoad);
		Time.timeScale = 1f;

		StartCoroutine(MatchMusicWhenReady(scene.Name, scene.Path));
	}

	/// <summary>
	/// Loads a scene by string name, unpauses time, and plays the mapped music clip if configured.
	/// </summary>
	/// <param name="sceneName">Scene name as added in Build Settings.</param>
	public void ChangeScene(string sceneName)
	{
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			Debug.LogError("[SceneManagement] ChangeScene called with empty scene name.");
			return;
		}

		Debug.Log($"[SceneManagement] Changing scene to: {sceneName}");

		SceneManager.LoadScene(sceneName);
		Time.timeScale = 1f;

		// The string may be either a scene name or a scene path, so pass it as both.
		StartCoroutine(MatchMusicWhenReady(sceneName, sceneName));
	}

	#endregion

	#region Internals

	/// <summary>
	/// Waits until SoundManager exists.
	/// </summary>
	private IEnumerator WaitForSoundManager()
	{
		while (SoundManager.Instance == null)
		{
			yield return null;
		}
	}

	/// <summary>
	/// Waits until SoundManager is ready, then plays music for the scene.
	/// </summary>
	/// <param name="sceneName">Scene name to match.</param>
	/// <param name="scenePath">Scene path to use as fallback.</param>
	private IEnumerator MatchMusicWhenReady(string sceneName, string scenePath)
	{
		yield return WaitForSoundManager();

		MatchMusicClip(sceneName, scenePath);
	}

	/// <summary>
	/// Attempts to find and play a music clip for the given scene name or path.
	/// Matching prefers scene name and falls back to scene path.
	/// </summary>
	/// <param name="sceneName">Scene name to match first.</param>
	/// <param name="scenePath">Scene path to match as fallback.</param>
	private void MatchMusicClip(string sceneName, string scenePath)
	{
		if (SoundManager.Instance == null)
		{
			Debug.LogWarning("[SceneManagement] SoundManager.Instance is null; cannot play scene music.");
			return;
		}

		for (int i = 0; i < sceneAudioClipPairs.Count; i++)
		{
			SceneAssetHelperAudioClipPair pair = sceneAudioClipPairs[i];

			if (pair == null)
			{
				continue;
			}

			if (!SceneMatches(pair, sceneName, scenePath))
			{
				continue;
			}

			if (pair.AudioClip == null)
			{
				Debug.LogWarning($"[SceneManagement] Matched scene '{sceneName}', but its music clip is null.");
				return;
			}

			Debug.Log($"[SceneManagement] Playing music for scene: {pair.Name}");
			SoundManager.Instance.PlayMusic(pair.AudioClip);
			return;
		}

		Debug.LogWarning($"[SceneManagement] No matched music pair for scene: {sceneName} ({scenePath})");
	}

	/// <summary>
	/// Returns true if the pair matches the supplied scene name or scene path.
	/// Name matching is preferred; path matching is used as a fallback.
	/// </summary>
	/// <param name="pair">Configured scene/audio pair.</param>
	/// <param name="sceneName">Runtime scene name.</param>
	/// <param name="scenePath">Runtime scene path.</param>
	private bool SceneMatches(SceneAssetHelperAudioClipPair pair, string sceneName, string scenePath)
	{
		if (!string.IsNullOrEmpty(sceneName) && pair.Name == sceneName)
		{
			return true;
		}

		if (!string.IsNullOrEmpty(scenePath) && pair.Path == scenePath)
		{
			return true;
		}

		// Extra fallback: useful when a scene was passed as a string path.
		if (!string.IsNullOrEmpty(sceneName) && pair.Path == sceneName)
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// Quits the application.
	/// </summary>
	private void QuitGame()
	{
		Debug.Log("Quitting game...");
		Application.Quit();
	}

	#endregion
}