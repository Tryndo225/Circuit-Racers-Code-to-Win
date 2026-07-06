using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Click-dismissable UI popup used to display a notification message.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Represents the visual part of a notification shown by <see cref="NotificationManager"/>.
///
/// This component controls the displayed message text, text color, and popup alpha.
/// It also implements <see cref="IPointerClickHandler"/> so the user can dismiss the current
/// notification by clicking the popup.
/// </remarks>
public class NotificationPopup : MonoBehaviour, IPointerClickHandler
{
	/// <summary>
	/// Text component used to display the notification message.
	/// </summary>
	[Tooltip("Text component used to display the notification message.")]
	[SerializeField] private TMP_Text messageText;

	/// <summary>
	/// Canvas group used to control popup visibility through alpha.
	/// </summary>
	[Tooltip("CanvasGroup used to control the popup alpha during display and fade-out.")]
	[SerializeField] private CanvasGroup canvasGroup;

	/// <summary>
	/// Unity lifecycle method that resolves missing component references from this GameObject or its children.
	/// </summary>
	private void Awake()
	{
		if (canvasGroup == null)
			canvasGroup = GetComponent<CanvasGroup>();

		if (messageText == null)
			messageText = GetComponentInChildren<TMP_Text>();
	}

	/// <summary>
	/// Sets the notification message displayed by this popup.
	/// </summary>
	/// <param name="message">Message text to display.</param>
	public void SetText(string message)
	{
		if (messageText != null)
			messageText.text = message;
	}

	/// <summary>
	/// Sets the popup opacity.
	/// </summary>
	/// <param name="alpha">Alpha value applied to the popup <see cref="CanvasGroup"/>.</param>
	public void SetAlpha(float alpha)
	{
		if (canvasGroup != null)
			canvasGroup.alpha = alpha;
	}

	/// <summary>
	/// Sets the color of the notification text.
	/// </summary>
	/// <param name="color">Color applied to the message text.</param>
	public void SetTextColor(Color color)
	{
		if (messageText != null)
		{
			messageText.color = color;
		}
	}

	/// <summary>
	/// Handles pointer clicks on the popup by clearing the active notification.
	/// </summary>
	/// <param name="eventData">Pointer event data provided by the Unity EventSystem.</param>
	public void OnPointerClick(PointerEventData eventData)
	{
		if (NotificationManager.Instance != null)
		{
			NotificationManager.Instance.Clear();
		}
	}
}