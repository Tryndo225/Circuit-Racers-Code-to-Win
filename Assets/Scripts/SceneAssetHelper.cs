using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class SceneAssetHelper : ISerializationCallbackReceiver
{
    [SerializeField, ReadOnly] protected string sceneName;
    [SerializeField, ReadOnly] protected string scenePath;

#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

    public string Name => sceneAsset.name;
    public string Path => scenePath;

    public SceneAssetHelper()
    {
        sceneName = string.Empty;
        scenePath = string.Empty;
    }

    public SceneAssetHelper(UnityEditor.SceneAsset scene)
    {
        sceneAsset = scene;
        ConsistencyKeep();
    }

    public SceneAssetHelper(string sceneName, string scenePath)
    {
        this.sceneName = sceneName;
        this.scenePath = scenePath;
    }

#if UNITY_EDITOR

    public void OnBeforeSerialize()
    {
        ConsistencyKeep();
    }

    public void OnAfterDeserialize()
    { }

#else
    public void OnBeforeSerialize()
    { }
    public void OnAfterDeserialize()
    { }
#endif

    private void ConsistencyKeep()
    {
        if (sceneAsset != null)
        {
            sceneName = sceneAsset.name;
            scenePath = UnityEditor.AssetDatabase.GetAssetPath(sceneAsset);
        }
        else
        {
            sceneName = string.Empty;
            scenePath = string.Empty;
        }
    }

    public static implicit operator SceneAssetHelper(Scene scene)
    {
        return new SceneAssetHelper(scene.name, scene.path);
    }

    public static implicit operator Scene(SceneAssetHelper sceneAssetHelper)
    {
        return SceneManager.GetSceneByPath(sceneAssetHelper.scenePath);
    }

    public static bool operator ==(SceneAssetHelper a, SceneAssetHelper b)
    {
        return a.scenePath == b.scenePath;
    }

    public static bool operator !=(SceneAssetHelper a, SceneAssetHelper b)
    {
        return a.scenePath != b.scenePath;
    }

    public override bool Equals(object obj)
    {
        if (obj is SceneAssetHelper other)
        {
            return scenePath == other.scenePath;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return scenePath != null ? scenePath.GetHashCode() : 0;
    }

#if UNITY_EDITOR

    public static implicit operator SceneAssetHelper(UnityEditor.SceneAsset sceneAsset)
    {
        return new SceneAssetHelper(sceneAsset);
    }

#endif
}

[Serializable]
public class SceneAssetHelperAudioClipPair : SceneAssetHelper
{
    [SerializeField] private AudioClip audioClip;
    public AudioClip AudioClip => audioClip;
}