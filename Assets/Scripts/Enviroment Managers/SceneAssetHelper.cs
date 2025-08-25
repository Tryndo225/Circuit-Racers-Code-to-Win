using System;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class SceneAssetHelper : SerializableRunnable
{
    [SerializeField, ReadOnly] protected string sceneName;
    [SerializeField, ReadOnly] protected string scenePath;

#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

    public string Name => sceneName;

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

    public override void Run()
    {
        SceneManagement.Instance.ChangeScene(this);
    }

#if UNITY_EDITOR

    public override void OnBeforeSerialize()
    {
        ConsistencyKeep();
    }

    public override void OnAfterDeserialize()
    { }

#else
    public override void OnBeforeSerialize()
    { }
    public override void OnAfterDeserialize()
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
        if (a is not null && b is not null)
        {
            return a.scenePath == b.scenePath;
        }
        return a is null && b is null;
    }

    public static bool operator !=(SceneAssetHelper a, SceneAssetHelper b)
    {
        return !(a == b);
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

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("sceneName", sceneName);
        info.AddValue("scenePath", scenePath);
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