using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Serializable, editor-friendly scene reference that stores a scene name and path.
/// </summary>
/// <remarks>
/// @ingroup scene_mgmt
/// @brief Wraps a Unity scene reference so it can be serialized, compared, converted, and executed as a scene load action.
///
/// This helper stores the scene name and scene path. In the Unity Editor, it can also keep those
/// values synchronized with a selected <c>SceneAsset</c>.
///
/// It can be used as a runnable scene action through <see cref="Run"/>, which loads the scene through
/// <see cref="SceneManagement"/>.
///
/// Threading:
/// - Unity main thread only for <see cref="Run"/> and scene loading.
/// </remarks>
[Serializable]
public class SceneAssetHelper : SerializableRunnable
{
	#region Inspector (backing data)

	/// <summary>
	/// Backing scene name.
	/// </summary>
	/// <remarks>
	/// In the editor, this is kept in sync with the selected scene asset when available.
	/// </remarks>
	[Tooltip("Scene name stored from the selected scene asset.")]
	[SerializeField, ReadOnly] protected string sceneName;

	/// <summary>
	/// Backing scene path.
	/// </summary>
	/// <remarks>
	/// In the editor, this is kept in sync with the selected scene asset when available.
	/// </remarks>
	[Tooltip("Scene path stored from the selected scene asset.")]
	[SerializeField, ReadOnly] protected string scenePath;

#if UNITY_EDITOR
	/// <summary>
	/// Editor-only scene asset reference used to maintain <see cref="sceneName"/> and <see cref="scenePath"/>.
	/// </summary>
	[Tooltip("Editor-only scene asset reference used to fill the scene name and scene path.")]
	[SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

	#endregion

	#region Public properties

	/// <summary>
	/// Gets the stored scene name.
	/// </summary>
	public string Name => sceneName;

	/// <summary>
	/// Gets the stored scene path.
	/// </summary>
	public string Path => scenePath;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates an empty scene reference.
	/// </summary>
	public SceneAssetHelper()
	{
		sceneName = string.Empty;
		scenePath = string.Empty;
	}

#if UNITY_EDITOR
	/// <summary>
	/// Creates a scene reference from an editor scene asset.
	/// </summary>
	/// <param name="scene">Scene asset selected in the editor.</param>
	public SceneAssetHelper(UnityEditor.SceneAsset scene)
	{
		sceneAsset = scene;
		ConsistencyKeep();
	}
#endif

	/// <summary>
	/// Creates a scene reference from a known scene name and scene path.
	/// </summary>
	/// <param name="sceneName">Scene name.</param>
	/// <param name="scenePath">Scene path.</param>
	public SceneAssetHelper(string sceneName, string scenePath)
	{
		this.sceneName = sceneName;
		this.scenePath = scenePath;
	}

	#endregion

	#region Execution

	/// <summary>
	/// Loads this scene through <see cref="SceneManagement"/>.
	/// </summary>
	public override void Run()
	{
		SceneManagement.Instance.ChangeScene(this);
	}

	#endregion

	#region Serialization hooks

#if UNITY_EDITOR
	/// <summary>
	/// Updates stored scene data from the editor scene asset before serialization.
	/// </summary>
	public override void OnBeforeSerialize()
	{
		ConsistencyKeep();
	}

	/// <summary>
	/// Handles Unity deserialization.
	/// </summary>
	/// <remarks>
	/// This is intentionally empty because editor synchronization is handled before serialization.
	/// </remarks>
	public override void OnAfterDeserialize()
	{
	}
#else
	/// <summary>
	/// Handles Unity pre-serialization at runtime.
	/// </summary>
	/// <remarks>
	/// This is intentionally empty because the editor scene asset is not available at runtime.
	/// </remarks>
	public override void OnBeforeSerialize() { }

	/// <summary>
	/// Handles Unity post-deserialization at runtime.
	/// </summary>
	/// <remarks>
	/// This is intentionally empty because the stored scene name and path are already serialized directly.
	/// </remarks>
	public override void OnAfterDeserialize() { }
#endif

	/// <summary>
	/// Synchronizes <see cref="sceneName"/> and <see cref="scenePath"/> with the editor scene asset.
	/// </summary>
	/// <remarks>
	/// In the editor, assigned scene assets populate the stored scene name and path.
	/// When no asset is assigned, both fields are cleared.
	/// </remarks>
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
	/// Creates a scene helper from a loaded Unity scene.
	/// </summary>
	/// <param name="scene">Loaded Unity scene.</param>
	public static implicit operator SceneAssetHelper(Scene scene)
	{
		return new SceneAssetHelper(scene.name, scene.path);
	}

	/// <summary>
	/// Resolves a Unity scene from a scene helper's stored path.
	/// </summary>
	/// <param name="sceneAssetHelper">Scene helper to convert.</param>
	/// <returns>Scene resolved by path.</returns>
	/// <remarks>
	/// If the scene is not loaded, Unity returns an invalid scene.
	/// </remarks>
	public static implicit operator Scene(SceneAssetHelper sceneAssetHelper)
	{
		return SceneManager.GetSceneByPath(sceneAssetHelper.scenePath);
	}

#if UNITY_EDITOR
	/// <summary>
	/// Creates a scene helper from an editor scene asset.
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
	/// Checks whether two scene helpers reference the same scene path.
	/// </summary>
	/// <param name="a">First scene helper.</param>
	/// <param name="b">Second scene helper.</param>
	/// <returns><c>true</c> when both helpers are null or have the same scene path; otherwise <c>false</c>.</returns>
	public static bool operator ==(SceneAssetHelper a, SceneAssetHelper b)
	{
		if (a is not null && b is not null)
		{
			return a.scenePath == b.scenePath;
		}
		return a is null && b is null;
	}

	/// <summary>
	/// Checks whether two scene helpers reference different scene paths.
	/// </summary>
	/// <param name="a">First scene helper.</param>
	/// <param name="b">Second scene helper.</param>
	/// <returns><c>true</c> when the helpers differ; otherwise <c>false</c>.</returns>
	public static bool operator !=(SceneAssetHelper a, SceneAssetHelper b)
	{
		return !(a == b);
	}

	/// <summary>
	/// Checks whether this scene helper equals another object.
	/// </summary>
	/// <param name="obj">Object to compare with this scene helper.</param>
	/// <returns><c>true</c> when the other object is a scene helper with the same scene path; otherwise <c>false</c>.</returns>
	public override bool Equals(object obj)
	{
		if (obj is SceneAssetHelper other)
		{
			return scenePath == other.scenePath;
		}
		return false;
	}

	/// <summary>
	/// Computes a hash code from the stored scene path.
	/// </summary>
	/// <returns>Hash code for this scene helper.</returns>
	public override int GetHashCode()
	{
		return scenePath != null ? scenePath.GetHashCode() : 0;
	}

	#endregion
}

/// <summary>
/// Scene reference paired with an audio clip.
/// </summary>
/// <remarks>
/// @ingroup scene_mgmt
/// @brief Extends <see cref="SceneAssetHelper"/> with a music clip used by <see cref="SceneManagement"/>.
/// </remarks>
[Serializable]
public class SceneAssetHelperAudioClipPair : SceneAssetHelper
{
	#region Inspector

	/// <summary>
	/// Music clip associated with this scene.
	/// </summary>
	[Tooltip("Music clip to play when this scene is loaded.")]
	[SerializeField] private AudioClip audioClip;

	#endregion

	#region Public API

	/// <summary>
	/// Gets the music clip associated with this scene.
	/// </summary>
	public AudioClip AudioClip => audioClip;

	#endregion
}