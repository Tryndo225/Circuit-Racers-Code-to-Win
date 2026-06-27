using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class NotificationPopup : MonoBehaviour, IPointerClickHandler
{
	[SerializeField] private TMP_Text messageText;
	[SerializeField] private CanvasGroup canvasGroup;

	private void Awake()
	{
		if (canvasGroup == null)
			canvasGroup = GetComponent<CanvasGroup>();

		if (messageText == null)
			messageText = GetComponentInChildren<TMP_Text>();
	}

	public void SetText(string message)
	{
		if (messageText != null)
			messageText.text = message;
	}

	public void SetAlpha(float alpha)
	{
		if (canvasGroup != null)
			canvasGroup.alpha = alpha;
	}

	public void SetTextColor(Color color)
	{
		if (messageText != null)
		{
			messageText.color = color;
		}
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (NotificationManager.Instance != null)
		{
			NotificationManager.Instance.Clear();
		}
	}
}