using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralized scene loader and background-music router.
/// </summary>
/// <remarks>
/// @ingroup scene_mgmt
/// @brief Loads scenes by helper or name, restores normal time scale, and selects scene-specific background music.
///
/// Scene music is matched from the configured <see cref="sceneAudioClipPairs"/> list.
/// Matching prefers scene name and then falls back to scene path.
///
/// Threading:
/// - Unity main thread only.
/// - Uses Unity scene loading, coroutines, and audio manager access.
/// </remarks>
public class SceneManagement : Generic.Singleton<SceneManagement>
{
	#region Inspector

	[Header("Scene -> Music Mapping")]
	/// <summary>
	/// Scene-to-music mappings used when scenes are loaded.
	/// </summary>
	/// <remarks>
	/// When a scene loads, the first pair with a matching scene name or scene path is used.
	/// </remarks>
	[Tooltip("List of scene and corresponding audio clip pairs. The audio clip will be played when the scene is loaded.")]
	[SerializeField] private List<SceneAssetHelperAudioClipPair> sceneAudioClipPairs = new List<SceneAssetHelperAudioClipPair>();

	#endregion

	#region Unity methods

	/// <summary>
	/// Waits for the sound manager and plays music for the initially active scene.
	/// </summary>
	/// <returns>Coroutine enumerator.</returns>
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
	/// Loads a scene from a <see cref="SceneAssetHelper"/>.
	/// </summary>
	/// <param name="scene">Scene helper containing the scene name and path.</param>
	/// <remarks>
	/// The method restores <see cref="Time.timeScale"/> to normal after starting the scene load.
	/// If the helper's name is <c>Quit Game</c>, the application quits instead of loading a scene.
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
	/// Loads a scene by name.
	/// </summary>
	/// <param name="sceneName">Scene name as added in Build Settings.</param>
	/// <remarks>
	/// The method restores <see cref="Time.timeScale"/> to normal after starting the scene load.
	/// The provided string is passed as both scene name and scene path for music matching, so it can
	/// also work with path-like scene identifiers.
	/// </remarks>
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
	/// Waits until <see cref="SoundManager.Instance"/> is available.
	/// </summary>
	/// <returns>Coroutine enumerator.</returns>
	private IEnumerator WaitForSoundManager()
	{
		while (SoundManager.Instance == null)
		{
			yield return null;
		}
	}

	/// <summary>
	/// Waits for the sound manager and then matches music for a scene.
	/// </summary>
	/// <param name="sceneName">Scene name to match first.</param>
	/// <param name="scenePath">Scene path to use as fallback.</param>
	/// <returns>Coroutine enumerator.</returns>
	private IEnumerator MatchMusicWhenReady(string sceneName, string scenePath)
	{
		yield return WaitForSoundManager();

		MatchMusicClip(sceneName, scenePath);
	}

	/// <summary>
	/// Attempts to find and play a music clip for a scene.
	/// </summary>
	/// <param name="sceneName">Scene name to match first.</param>
	/// <param name="scenePath">Scene path to match as fallback.</param>
	/// <remarks>
	/// Matching is performed against the configured <see cref="sceneAudioClipPairs"/> list.
	/// Name matching is preferred, with path matching as a fallback.
	/// </remarks>
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
	/// Checks whether a configured scene/audio pair matches a runtime scene name or path.
	/// </summary>
	/// <param name="pair">Configured scene/audio pair.</param>
	/// <param name="sceneName">Runtime scene name.</param>
	/// <param name="scenePath">Runtime scene path.</param>
	/// <returns><c>true</c> if the pair matches the scene name or path; otherwise <c>false</c>.</returns>
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