using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public class FadingScreen : MonoBehaviour
{
	#region Inspector Fields

	[SerializeField, Min(0f)]
	private float visibleTime = 2f;

	[SerializeField, Min(0f)]
	private float fadingTime = 1f;

	[SerializeField, Min(1)]
	private int displayAmount = 1;

	[SerializeField]
	private bool onlyOnFirstSceneLoad = true;

	[SerializeField]
	private bool useUnscaledTime = true;

	[SerializeField]
	private bool disableAfterFade = true;

	[SerializeField, HideInInspector]
	private string instanceId;

	#endregion

	#region Private Fields

	private static readonly Dictionary<string, int> InstanceDisplayCounts = new Dictionary<string, int>();
	private static readonly HashSet<string> LoadedScenes = new HashSet<string>();

	private CanvasGroup _canvasGroup;

	#endregion

	#region Unity Methods

	private void Awake()
	{
		_canvasGroup = GetComponent<CanvasGroup>();

		if (string.IsNullOrEmpty(instanceId))
		{
			instanceId = System.Guid.NewGuid().ToString();
		}
	}

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

	private IEnumerator Wait(float duration)
	{
		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += GetDeltaTime();
			yield return null;
		}
	}

	private float GetDeltaTime()
	{
		return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
	}

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

	[ContextMenu("Regenerate Instance ID")]
	private void RegenerateInstanceId()
	{
		instanceId = System.Guid.NewGuid().ToString();
	}

	#endregion
}