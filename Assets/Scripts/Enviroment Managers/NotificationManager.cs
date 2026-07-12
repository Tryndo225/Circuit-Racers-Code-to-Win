using Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent UI notification manager that displays one text popup at a time.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Creates short-lived notification popups, optionally with custom text color and timing.
///
/// The manager instantiates a <see cref="NotificationPopup"/> prefab as a child of itself.
/// Showing a new notification clears the currently visible notification first, so only one popup
/// can be visible at a time.
///
/// Timing uses unscaled time, so notifications continue to count down even when
/// <see cref="Time.timeScale"/> is zero.
/// </remarks>
public class NotificationManager : Singleton<NotificationManager>
{
	#region Inspector

	[Header("Notification Setup")]
	/// <summary>
	/// Popup prefab used to display notification text.
	/// </summary>
	[Tooltip("Popup prefab used to display notification text.")]
	[SerializeField] private NotificationPopup notificationPrefab;

	[Header("Timing")]
	/// <summary>
	/// Default time in seconds that a notification remains fully visible.
	/// </summary>
	[Tooltip("Default time in seconds that a notification remains fully visible.")]
	[SerializeField] private float defaultVisibleTime = 2f;

	/// <summary>
	/// Default time in seconds used to fade out a notification.
	/// </summary>
	[Tooltip("Default time in seconds used to fade out a notification.")]
	[SerializeField] private float defaultFadeTime = 0.5f;

	#endregion

	#region Private Fields

	/// <summary>
	/// Currently displayed popup instance.
	/// </summary>
	private NotificationPopup currentPopup;

	/// <summary>
	/// Currently running notification lifetime coroutine.
	/// </summary>
	private Coroutine currentCoroutine;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Initializes the singleton instance.
	/// </summary>
	protected override void Awake()
	{
		base.Awake();
	}

	#endregion

	#region Public API

	/// <summary>
	/// Shows a notification with default timing.
	/// </summary>
	/// <param name="message">Notification text to display.</param>
	public void Show(string message)
	{
		Show(message, null, defaultVisibleTime, defaultFadeTime);
	}

	/// <summary>
	/// Shows a notification with a custom text color and default timing.
	/// </summary>
	/// <param name="message">Notification text to display.</param>
	/// <param name="textColor">Text color to apply to the popup.</param>
	public void Show(string message, Color textColor)
	{
		Show(message, textColor, defaultVisibleTime, defaultFadeTime);
	}

	/// <summary>
	/// Shows a notification with a custom visible time and default fade time.
	/// </summary>
	/// <param name="message">Notification text to display.</param>
	/// <param name="visibleTime">Time in seconds the notification remains fully visible.</param>
	public void Show(string message, float visibleTime)
	{
		Show(message, null, visibleTime, defaultFadeTime);
	}

	/// <summary>
	/// Shows a notification with custom visible and fade timing.
	/// </summary>
	/// <param name="message">Notification text to display.</param>
	/// <param name="visibleTime">Time in seconds the notification remains fully visible.</param>
	/// <param name="fadeTime">Time in seconds used to fade the notification out.</param>
	public void Show(string message, float visibleTime, float fadeTime)
	{
		Show(message, null, visibleTime, fadeTime);
	}

	/// <summary>
	/// Shows a notification with a custom text color and custom visible time.
	/// </summary>
	/// <param name="message">Notification text to display.</param>
	/// <param name="textColor">Text color to apply to the popup.</param>
	/// <param name="visibleTime">Time in seconds the notification remains fully visible.</param>
	public void Show(string message, Color textColor, float visibleTime)
	{
		Show(message, textColor, visibleTime, defaultFadeTime);
	}

	/// <summary>
	/// Shows a notification with optional text color and custom timing.
	/// </summary>
	/// <param name="message">Notification text to display.</param>
	/// <param name="textColor">Optional text color. When null, the prefab's current/default text color is used.</param>
	/// <param name="visibleTime">Time in seconds the notification remains fully visible.</param>
	/// <param name="fadeTime">Time in seconds used to fade the notification out.</param>
	/// <remarks>
	/// Empty messages are ignored. If a popup is already visible, it is destroyed before the new popup is created.
	/// </remarks>
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

	/// <summary>
	/// Clears the currently visible notification, if any.
	/// </summary>
	public void Clear()
	{
		ClearCurrent();
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Stops the active notification coroutine and destroys the current popup instance.
	/// </summary>
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

	/// <summary>
	/// Keeps the notification visible, fades it out, and then clears it.
	/// </summary>
	/// <param name="visibleTime">Time in seconds the popup remains fully visible.</param>
	/// <param name="fadeTime">Time in seconds used for fade-out.</param>
	/// <returns>Coroutine enumerator.</returns>
	/// <remarks>
	/// Uses <see cref="Time.unscaledTime"/> so notification timing is independent of gameplay time scale.
	/// </remarks>
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