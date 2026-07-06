using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Displays a temporary fading overlay when a scene starts.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Controls a <see cref="CanvasGroup"/> that stays visible for a short time and then fades out.
///
/// This component is useful for intro overlays, title cards, loading covers, or short informational
/// screens shown when entering a scene. Display frequency can be limited per scene and per component
/// instance using <see cref="displayAmount"/>, <see cref="onlyOnFirstSceneLoad"/>, and the generated
/// <see cref="instanceId"/>.
///
/// The component requires a <see cref="CanvasGroup"/> because fading, interactivity, and raycast blocking
/// are controlled through that component.
/// </remarks>
[RequireComponent(typeof(CanvasGroup))]
public class FadingScreen : MonoBehaviour
{
	#region Inspector Fields

	/// <summary>
	/// Time, in seconds, for which the overlay remains fully visible before fading starts.
	/// </summary>
	[Tooltip("Time in seconds before the overlay starts fading out.")]
	[SerializeField, Min(0f)]
	private float visibleTime = 2f;

	/// <summary>
	/// Time, in seconds, used to fade the overlay from fully visible to invisible.
	/// </summary>
	[Tooltip("Time in seconds used to fade the overlay from visible to invisible.")]
	[SerializeField, Min(0f)]
	private float fadingTime = 1f;

	/// <summary>
	/// Maximum number of times this fading screen instance may be displayed for the same scene.
	/// </summary>
	[Tooltip("Maximum number of times this fading screen can appear for the same scene.")]
	[SerializeField, Min(1)]
	private int displayAmount = 1;

	/// <summary>
	/// Determines whether the overlay should only appear the first time the current scene is loaded.
	/// </summary>
	[Tooltip("If enabled, this overlay appears only the first time the scene is loaded.")]
	[SerializeField]
	private bool onlyOnFirstSceneLoad = true;

	/// <summary>
	/// Determines whether fading uses unscaled time instead of normal game time.
	/// </summary>
	[Tooltip("If enabled, fade timing ignores Time.timeScale and uses unscaled time.")]
	[SerializeField]
	private bool useUnscaledTime = true;

	/// <summary>
	/// Determines whether the GameObject should be disabled after the fade finishes or after it is hidden instantly.
	/// </summary>
	[Tooltip("If enabled, this GameObject is disabled after the overlay disappears.")]
	[SerializeField]
	private bool disableAfterFade = true;

	/// <summary>
	/// Persistent identifier used to track display counts for this specific fading screen instance.
	/// </summary>
	/// <remarks>
	/// The value is generated automatically and hidden in the Inspector so multiple fading screens in the
	/// same scene can be tracked independently.
	/// </remarks>
	[SerializeField, HideInInspector]
	private string instanceId;

	#endregion

	#region Private Fields

	/// <summary>
	/// Display count cache indexed by scene and fading screen instance.
	/// </summary>
	private static readonly Dictionary<string, int> InstanceDisplayCounts = new Dictionary<string, int>();

	/// <summary>
	/// Set of scene keys that have already been loaded during the current application session.
	/// </summary>
	private static readonly HashSet<string> LoadedScenes = new HashSet<string>();

	/// <summary>
	/// Canvas group used to control overlay opacity and input blocking.
	/// </summary>
	private CanvasGroup _canvasGroup;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Caches the required <see cref="CanvasGroup"/> and creates an instance identifier if missing.
	/// </summary>
	private void Awake()
	{
		_canvasGroup = GetComponent<CanvasGroup>();

		if (string.IsNullOrEmpty(instanceId))
		{
			instanceId = System.Guid.NewGuid().ToString();
		}
	}

	/// <summary>
	/// Decides whether the overlay should be shown for the current scene and starts the fade routine if allowed.
	/// </summary>
	/// <remarks>
	/// The decision is based on the current scene key, whether the scene has already been loaded, and
	/// how many times this specific instance has already been displayed.
	/// </remarks>
	private void Start()
	{
		string sceneKey = GetCurrentSceneKey();
		string displayKey = $"{sceneKey}_{instanceId}";

		bool sceneWasLoadedBefore = LoadedScenes.Contains(sceneKey);

		if (!LoadedScenes.Contains(sceneKey))
		{
			LoadedScenes.Add(sceneKey);
		}

		if (onlyOnFirstSceneLoad && sceneWasLoadedBefore)
		{
			HideInstantly();
			return;
		}

		int currentDisplayCount = InstanceDisplayCounts.GetValueOrDefault(displayKey, 0);

		if (currentDisplayCount >= displayAmount)
		{
			HideInstantly();
			return;
		}

		InstanceDisplayCounts[displayKey] = currentDisplayCount + 1;

		StartCoroutine(FadeRoutine());
	}

#if UNITY_EDITOR

	/// <summary>
	/// Ensures the hidden instance identifier exists while editing the component in the Unity Editor.
	/// </summary>
	private void OnValidate()
	{
		if (string.IsNullOrEmpty(instanceId))
		{
			instanceId = System.Guid.NewGuid().ToString();
			UnityEditor.EditorUtility.SetDirty(this);
		}
	}

#endif

	#endregion

	#region Private Methods

	/// <summary>
	/// Shows the overlay, waits for <see cref="visibleTime"/>, fades it out, and optionally disables the GameObject.
	/// </summary>
	/// <returns>Coroutine enumerator used by Unity.</returns>
	private IEnumerator FadeRoutine()
	{
		_canvasGroup.alpha = 1f;
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;

		if (visibleTime > 0f)
		{
			yield return Wait(visibleTime);
		}

		if (fadingTime > 0f)
		{
			float elapsed = 0f;

			while (elapsed < fadingTime)
			{
				elapsed += GetDeltaTime();
				_canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadingTime);
				yield return null;
			}
		}

		_canvasGroup.alpha = 0f;

		if (disableAfterFade)
		{
			gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Immediately hides the overlay without running the fade animation.
	/// </summary>
	/// <remarks>
	/// Used when this fading screen should not be displayed because the scene or instance display limit
	/// has already been reached.
	/// </remarks>
	private void HideInstantly()
	{
		_canvasGroup.alpha = 0f;
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;

		if (disableAfterFade)
		{
			gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Waits for a duration using either scaled or unscaled delta time.
	/// </summary>
	/// <param name="duration">Duration to wait, in seconds.</param>
	/// <returns>Coroutine enumerator used by Unity.</returns>
	private IEnumerator Wait(float duration)
	{
		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += GetDeltaTime();
			yield return null;
		}
	}

	/// <summary>
	/// Gets the delta time value used by this fading screen.
	/// </summary>
	/// <returns>
	/// <see cref="Time.unscaledDeltaTime"/> when <see cref="useUnscaledTime"/> is enabled;
	/// otherwise <see cref="Time.deltaTime"/>.
	/// </returns>
	private float GetDeltaTime()
	{
		return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
	}

	/// <summary>
	/// Gets a stable key for the currently active scene.
	/// </summary>
	/// <returns>
	/// The active scene path when available; otherwise the active scene name.
	/// </returns>
	private string GetCurrentSceneKey()
	{
		Scene scene = SceneManager.GetActiveScene();

		if (!string.IsNullOrEmpty(scene.path))
		{
			return scene.path;
		}

		return scene.name;
	}

	#endregion

	#region Editor Helpers

	/// <summary>
	/// Regenerates the hidden instance identifier used for display-count tracking.
	/// </summary>
	/// <remarks>
	/// Use this context menu action when this fading screen should be treated as a new independent
	/// instance for scene/display-count tracking.
	/// </remarks>
	[ContextMenu("Regenerate Instance ID")]
	private void RegenerateInstanceId()
	{
		instanceId = System.Guid.NewGuid().ToString();
	}

	#endregion
}