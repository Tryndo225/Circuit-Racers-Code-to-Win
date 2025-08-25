using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class SceneManagement : Generic.Singleton<SceneManagement>
{
    [Tooltip("List of scene and corresponding audio clip pairs. The audio clip will be played when the scene is loaded.")]
    [SerializeField] private List<SceneAssetHelperAudioClipPair> sceneAudioClipPairs = new List<SceneAssetHelperAudioClipPair>();

    private void Start()
    {
        MatchMusicClip(SceneManager.GetActiveScene());
    }

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
        Time.timeScale = 1;

        MatchMusicClip(scene);
    }

    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        Time.timeScale = 1;

        MatchMusicClip(new SceneAssetHelper(sceneName, sceneName));
    }

    private void MatchMusicClip(SceneAssetHelper scene)
    {
        for (int i = 0; i < sceneAudioClipPairs.Count; i++)
        {
            if (scene == sceneAudioClipPairs[i])
            {
                SoundManager.Instance.PlayMusic(sceneAudioClipPairs[i].AudioClip);
            }
        }
    }

    private void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}