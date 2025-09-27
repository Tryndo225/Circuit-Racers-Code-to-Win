using System;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Serializable, editor-friendly scene reference that stores scene name and path,
/// supports implicit conversions to and from <see cref="Scene"/>, and can be
/// invoked to load the scene via <see cref="SceneManagement"/>.
/// </summary>
/// <remarks>
/// @ingroup scene_mgmt
/// @thread Unity main thread only (for <see cref="Run"/> and scene loads).
/// @invariant When assigned via the Unity Editor, <c>sceneName</c> and <c>scenePath</c>
///            mirror the selected <c>SceneAsset</c> (kept in sync in editor-only code).
/// @req <c>SerializableRunnable</c> base provides <c>Run</c>, <c>OnBeforeSerialize</c>, <c>OnAfterDeserialize</c>.
/// @req <see cref="SceneManagement"/> singleton exists for <see cref="Run"/> to work.
/// </remarks>
[Serializable]
public class SceneAssetHelper : SerializableRunnable
{
    #region Inspector (backing data)

    /// <summary>
    /// Backing scene name. Kept in sync with the editor scene asset when available.
    /// </summary>
    [SerializeField, ReadOnly] protected string sceneName;

    /// <summary>
    /// Backing scene path (as in Build Settings / AssetDatabase). Kept in sync with editor asset.
    /// </summary>
    [SerializeField, ReadOnly] protected string scenePath;

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only scene asset reference used to maintain <see cref="sceneName"/> and <see cref="scenePath"/>.
    /// </summary>
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

    #endregion

    #region Public properties

    /// <summary>
    /// Scene name (read-only). If no scene is assigned, this is an empty string.
    /// </summary>
    public string Name => sceneName;

    /// <summary>
    /// Scene path (read-only). If no scene is assigned, this is an empty string.
    /// </summary>
    public string Path => scenePath;

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor. Initializes with empty name and path.
    /// </summary>
    public SceneAssetHelper()
    {
        sceneName = string.Empty;
        scenePath = string.Empty;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only constructor from a <c>SceneAsset</c>. Populates name and path.
    /// </summary>
    /// <param name="scene">Scene asset selected in the editor.</param>
    public SceneAssetHelper(UnityEditor.SceneAsset scene)
    {
        sceneAsset = scene;
        ConsistencyKeep();
    }
#endif

    /// <summary>
    /// Direct constructor when you already know name and path.
    /// </summary>
    /// <param name="sceneName">Scene name.</param>
    /// <param name="scenePath">Scene path (Assets/... or build path).</param>
    public SceneAssetHelper(string sceneName, string scenePath)
    {
        this.sceneName = sceneName;
        this.scenePath = scenePath;
    }

    #endregion

    #region Execution

    /// <summary>
    /// Loads this scene via <see cref="SceneManagement.ChangeScene(SceneAssetHelper)"/>.
    /// </summary>
    public override void Run()
    {
        SceneManagement.Instance.ChangeScene(this);
    }

    #endregion

    #region Serialization hooks

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only pre-serialize hook. Ensures <see cref="sceneName"/> and <see cref="scenePath"/> reflect the editor asset.
    /// </summary>
    public override void OnBeforeSerialize()
    {
        ConsistencyKeep();
    }

    /// <summary>
    /// Editor-only post-deserialize hook. No-op (data comes from <see cref="ConsistencyKeep"/>).
    /// </summary>
    public override void OnAfterDeserialize()
    {
    }
#else
    /// <summary>
    /// Runtime pre-serialize hook. No-op at runtime (no editor asset available).
    /// </summary>
    public override void OnBeforeSerialize() { }

    /// <summary>
    /// Runtime post-deserialize hook. No-op at runtime.
    /// </summary>
    public override void OnAfterDeserialize() { }
#endif

    /// <summary>
    /// Synchronizes <see cref="sceneName"/> and <see cref="scenePath"/> with the editor asset (editor only).
    /// If the asset is null, clears both fields.
    /// </summary>
    private void ConsistencyKeep()
    {
#if UNITY_EDITOR
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
#endif
    }

    #endregion

    #region Implicit conversions

    /// <summary>
    /// Creates a helper from a loaded <see cref="Scene"/> by copying name and path.
    /// </summary>
    /// <param name="scene">Loaded Unity scene.</param>
    public static implicit operator SceneAssetHelper(Scene scene)
    {
        return new SceneAssetHelper(scene.name, scene.path);
    }

    /// <summary>
    /// Converts the helper to a loaded <see cref="Scene"/> using its stored <see cref="scenePath"/>.
    /// </summary>
    /// <param name="sceneAssetHelper">Helper to convert.</param>
    /// <returns>A scene resolved by path. If not loaded, returns an invalid Scene (Scene.isLoaded == false).</returns>
    public static implicit operator Scene(SceneAssetHelper sceneAssetHelper)
    {
        return SceneManager.GetSceneByPath(sceneAssetHelper.scenePath);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only implicit conversion from <c>SceneAsset</c> to <see cref="SceneAssetHelper"/>.
    /// </summary>
    /// <param name="sceneAsset">Scene asset reference.</param>
    public static implicit operator SceneAssetHelper(UnityEditor.SceneAsset sceneAsset)
    {
        return new SceneAssetHelper(sceneAsset);
    }
#endif

    #endregion

    #region Equality and hashing

    /// <summary>
    /// Equality operator. Compares by <see cref="scenePath"/>. Treats two nulls as equal.
    /// </summary>
    public static bool operator ==(SceneAssetHelper a, SceneAssetHelper b)
    {
        if (a is not null && b is not null)
        {
            return a.scenePath == b.scenePath;
        }
        return a is null && b is null;
    }

    /// <summary>
    /// Inequality operator. Logical negation of <see cref="operator =="/>.
    /// </summary>
    public static bool operator !=(SceneAssetHelper a, SceneAssetHelper b)
    {
        return !(a == b);
    }

    /// <summary>
    /// Object equality override. Compares by <see cref="scenePath"/>.
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is SceneAssetHelper other)
        {
            return scenePath == other.scenePath;
        }
        return false;
    }

    /// <summary>
    /// Hash code override based on <see cref="scenePath"/>.
    /// </summary>
    public override int GetHashCode()
    {
        return scenePath != null ? scenePath.GetHashCode() : 0;
    }

    #endregion

    #region ISerializable

    /// <summary>
    /// Adds <see cref="sceneName"/> and <see cref="scenePath"/> to the serialization info.
    /// </summary>
    /// <param name="info">Serialization info bag.</param>
    /// <param name="context">Streaming context.</param>
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("sceneName", sceneName);
        info.AddValue("scenePath", scenePath);
    }

    #endregion
}

/// <summary>
/// A <see cref="SceneAssetHelper"/> extended with an <see cref="AudioClip"/> to be used
/// by <see cref="SceneManagement"/> for automatic background music selection.
/// </summary>
/// <remarks>
/// @ingroup scene_mgmt
/// </remarks>
[Serializable]
public class SceneAssetHelperAudioClipPair : SceneAssetHelper
{
    #region Inspector

    /// <summary>
    /// Music clip to play when this scene is loaded.
    /// </summary>
    [SerializeField] private AudioClip audioClip;

    #endregion

    #region Public API

    /// <summary>
    /// Read-only access to the configured music clip.
    /// </summary>
    public AudioClip AudioClip => audioClip;

    #endregion
}
