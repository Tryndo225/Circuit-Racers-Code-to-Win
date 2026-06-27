using Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent UI notification manager.
/// Displays at most one text popup at a time.
/// </summary>
public class NotificationManager : Singleton<NotificationManager>
{
	#region Inspector

	[Header("Notification Setup")]
	[SerializeField] private NotificationPopup notificationPrefab;

	[Header("Timing")]
	[SerializeField] private float defaultVisibleTime = 2f;

	[SerializeField] private float defaultFadeTime = 0.5f;

	#endregion

	#region Private Fields

	private NotificationPopup currentPopup;
	private Coroutine currentCoroutine;

	#endregion

	#region Unity Methods

	protected override void Awake()
	{
		base.Awake();
	}

	#endregion

	#region Public API

	public void Show(string message)
	{
		Show(message, null, defaultVisibleTime, defaultFadeTime);
	}

	public void Show(string message, Color textColor)
	{
		Show(message, textColor, defaultVisibleTime, defaultFadeTime);
	}

	public void Show(string message, float visibleTime)
	{
		Show(message, null, visibleTime, defaultFadeTime);
	}

	public void Show(string message, float visibleTime, float fadeTime)
	{
		Show(message, null, visibleTime, fadeTime);
	}

	public void Show(string message, Color textColor, float visibleTime)
	{
		Show(message, textColor, visibleTime, defaultFadeTime);
	}

	public void Show(string message, Color? textColor, float visibleTime, float fadeTime)
	{
		if (string.IsNullOrEmpty(message))
		{
			return;
		}

		if (notificationPrefab == null)
		{
			Debug.LogWarning("NotificationManager has no notification prefab assigned.");
			return;
		}

		ClearCurrent();

		currentPopup = Instantiate(notificationPrefab, transform);
		currentPopup.SetText(message);

		if (textColor.HasValue)
		{
			currentPopup.SetTextColor(textColor.Value);
		}

		currentPopup.SetAlpha(1f);

		currentCoroutine = StartCoroutine(NotificationCoroutine(visibleTime, fadeTime));
	}

	public void Clear()
	{
		ClearCurrent();
	}

	#endregion

	#region Private Helpers

	private void ClearCurrent()
	{
		if (currentCoroutine != null)
		{
			StopCoroutine(currentCoroutine);
			currentCoroutine = null;
		}

		if (currentPopup != null)
		{
			Destroy(currentPopup.gameObject);
			currentPopup = null;
		}
	}

	private IEnumerator NotificationCoroutine(float visibleTime, float fadeTime)
	{
		float startTime = Time.unscaledTime;

		while (Time.unscaledTime - startTime < visibleTime)
		{
			yield return null;
		}

		startTime = Time.unscaledTime;

		while (Time.unscaledTime - startTime < fadeTime)
		{
			if (currentPopup == null)
			{
				yield break;
			}

			float t = Mathf.Clamp01((Time.unscaledTime - startTime) / fadeTime);
			currentPopup.SetAlpha(Mathf.Lerp(1f, 0f, t));

			yield return null;
		}

		ClearCurrent();
	}

	#endregion
}