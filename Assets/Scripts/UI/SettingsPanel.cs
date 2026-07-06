using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
	[SerializeField] private Toggle absToggle;
	[SerializeField] private Toggle tcToggle;


	private void OnEnable()
	{
		absToggle.isOn = GameDataManager.Instance.GetABS();
		tcToggle.isOn = GameDataManager.Instance.GetTC();
	}

	private void Start()
	{
		gameObject.SetActive(false);
	}

	public void SetABS(bool value)
	{
		GameDataManager.Instance.SetABS(value);
	}

	public void SetTC(bool value)
	{
		GameDataManager.Instance.SetTC(value);
	}
}
