using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI controller for the driving assist settings panel.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
/// @brief Synchronizes ABS and traction-control toggles with saved game settings.
///
/// This panel reads the current assist settings from <see cref="GameDataManager"/> when enabled
/// and writes changes back through the public toggle callback methods.
///
/// Usage:
/// - Assign <see cref="absToggle"/> and <see cref="tcToggle"/> in the Inspector.
/// - Connect the toggles' OnValueChanged events to <see cref="SetABS"/> and <see cref="SetTC"/>.
/// - The panel hides itself on start and can be opened by another UI button.
/// </remarks>
public class SettingsPanel : MonoBehaviour
{
	/// <summary>
	/// Toggle controlling whether ABS is enabled in the saved assist settings.
	/// </summary>
	[Tooltip("Toggle controlling whether ABS is enabled in the saved assist settings.")]
	[SerializeField] private Toggle absToggle;

	/// <summary>
	/// Toggle controlling whether traction control is enabled in the saved assist settings.
	/// </summary>
	[Tooltip("Toggle controlling whether traction control is enabled in the saved assist settings.")]
	[SerializeField] private Toggle tcToggle;


	/// <summary>
	/// Refreshes toggle values from <see cref="GameDataManager"/> whenever the panel is shown.
	/// </summary>
	private void OnEnable()
	{
		absToggle.isOn = GameDataManager.Instance.GetABS();
		tcToggle.isOn = GameDataManager.Instance.GetTC();
	}

	/// <summary>
	/// Hides the settings panel after initial scene setup.
	/// </summary>
	private void Start()
	{
		gameObject.SetActive(false);
	}

	/// <summary>
	/// Updates the saved ABS setting.
	/// </summary>
	/// <param name="value">Whether ABS should be enabled.</param>
	public void SetABS(bool value)
	{
		GameDataManager.Instance.SetABS(value);
	}

	/// <summary>
	/// Updates the saved traction-control setting.
	/// </summary>
	/// <param name="value">Whether traction control should be enabled.</param>
	public void SetTC(bool value)
	{
		GameDataManager.Instance.SetTC(value);
	}
}