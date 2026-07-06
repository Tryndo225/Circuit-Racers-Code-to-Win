using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Dropdown-driven scene or action selector that mirrors the current dropdown choice onto a companion button.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup scene_mgmt
/// @brief Links a <see cref="TMP_Dropdown"/> to a generated/assigned <see cref="Button"/> so the dropdown selects an action
/// and the button executes it.
///
/// This component is designed for menu screens where a dropdown should choose between multiple configured actions,
/// while a large companion button displays the currently selected option and triggers the selected action.
/// The actions are represented by <see cref="SceneAssetHelper"/> entries stored in <see cref="scenes"/>.
///
/// Responsibilities:
/// - Configure the dropdown options, arrow sprite, template color, and selectable color states.
/// - Create and configure the companion button during editor reset.
/// - Keep the companion button label synchronized with the selected dropdown action.
/// - Execute the selected <see cref="SceneAssetHelper"/> action when the companion button is clicked.
///
/// Requirements:
/// - The GameObject should have a <see cref="TMP_Dropdown"/> component.
/// - The dropdown hierarchy should contain the expected child objects such as <c>Template</c> and <c>Arrow</c>.
/// - The <see cref="options"/> list and <see cref="scenes"/> array are index-matched.
///
/// Threading:
/// - Unity main thread only.
/// - Uses Unity lifecycle and UI callbacks.
/// </remarks>
public class DropDownMenuButtonSceneChanger : MonoBehaviour
{
	[Header("References")]
	/// <summary>
	/// Button that mirrors the selected dropdown action and executes it when clicked.
	/// </summary>
	/// <remarks>
	/// This button is created during <see cref="Reset"/> if the expected child does not already exist.
	/// At runtime, <see cref="OnChange"/> updates its label and <see cref="OnButtonClicked"/> executes the mapped action.
	/// </remarks>
	[Tooltip("Button that displays the currently selected dropdown action and executes it when clicked.")]
	[SerializeField, ReadOnly] private Button buttonToChange;

	/// <summary>
	/// Dropdown used to select which configured action should be shown on the companion button.
	/// </summary>
	[Tooltip("TMP dropdown used to select which action should be assigned to the companion button.")]
	[SerializeField, ReadOnly] private TMP_Dropdown dropdown;

	[Header("Dropdown Appearance")]
	/// <summary>
	/// Dropdown entries displayed to the user.
	/// </summary>
	/// <remarks>
	/// The entries should correspond by index to <see cref="scenes"/>. The dropdown option at index <c>i</c>
	/// selects the action stored in <c>scenes[i]</c>.
	/// </remarks>
	[Tooltip("Dropdown entries displayed to the user. Each entry corresponds by index to the Actions array.")]
	[SerializeField] private List<TMP_Dropdown.OptionData> options = new();

	/// <summary>
	/// Sprite assigned to the dropdown arrow image.
	/// </summary>
	[Tooltip("Sprite used by the dropdown arrow image.")]
	[SerializeField] private Sprite dropdownArrow;

	/// <summary>
	/// Normal selectable color used by the dropdown and companion button.
	/// </summary>
	[Tooltip("Color used by the dropdown and button in their normal state.")]
	[SerializeField] private Color normalColor = Color.white;

	/// <summary>
	/// Highlighted selectable color used by the dropdown and companion button.
	/// </summary>
	[Tooltip("Color used by the dropdown and button while highlighted.")]
	[SerializeField] private Color highlightedColor = Color.gray;

	/// <summary>
	/// Pressed selectable color used by the dropdown and companion button.
	/// </summary>
	[Tooltip("Color used by the dropdown and button while pressed.")]
	[SerializeField] private Color pressedColor = Color.black;

	/// <summary>
	/// Selected selectable color used by the dropdown and companion button.
	/// </summary>
	[Tooltip("Color used by the dropdown and button while selected.")]
	[SerializeField] private Color selectedColor = Color.blue;

	[Header("Actions")]
	/// <summary>
	/// Action array mapped to dropdown indices.
	/// </summary>
	/// <remarks>
	/// Each element corresponds to one dropdown option. When the user selects option <c>i</c>,
	/// <c>scenes[i]</c> provides the displayed action name and the action executed by <see cref="OnButtonClicked"/>.
	/// </remarks>
	[Tooltip("Actions mapped to dropdown entries by index. The selected action is executed by the companion button.")]
	[SerializeField] private SceneAssetHelper[] scenes;

	/// <summary>
	/// Editor-time width used when configuring the dropdown RectTransform in <see cref="InitializeDropdown"/>.
	/// </summary>
	private float dropdownWidth = 150f;

	/// <summary>
	/// Editor-time height used when configuring the dropdown RectTransform in <see cref="InitializeDropdown"/>.
	/// </summary>
	private float dropdownHeight = 150f;

	/// <summary>
	/// Editor-time width used when configuring the companion button RectTransform in <see cref="InitializeButton"/>.
	/// </summary>
	private float buttonWidth = 1000f;

	/// <summary>
	/// Editor-time height used when configuring the companion button RectTransform in <see cref="InitializeButton"/>.
	/// </summary>
	private float buttonHeight = 200f;

	/// <summary>
	/// Unity lifecycle method that wires dropdown and button callbacks.
	/// </summary>
	/// <remarks>
	/// Registers <see cref="OnChange"/> as the dropdown value-change callback and registers
	/// <see cref="OnButtonClicked"/> as the companion button click callback when the button exists.
	/// </remarks>
	private void Awake()
	{
		dropdown.onValueChanged.AddListener(OnChange);

		if (buttonToChange != null)
		{
			buttonToChange.onClick.AddListener(OnButtonClicked);
		}
		else
		{
			Debug.LogWarning("ButtonToChange is not assigned or found in the hierarchy.");
		}
	}

	/// <summary>
	/// Unity editor reset method that rebuilds the dropdown/button setup from the current component state.
	/// </summary>
	/// <remarks>
	/// This method is intended for editor-time setup. It retrieves the local <see cref="TMP_Dropdown"/>,
	/// removes any existing generated companion button, initializes the dropdown visuals, creates the companion button,
	/// resizes <see cref="scenes"/> to match the dropdown option count, and refreshes the displayed selection label.
	/// </remarks>
	private void Reset()
	{
		dropdown = GetComponent<TMP_Dropdown>();
		if (dropdown == null)
		{
			Debug.LogError("TMP_Dropdown component is missing. Please add this script to a newly created TMP_Dropdown");
		}

		if (dropdown.transform.Find("ButtonToChange") != null)
		{
			DestroyImmediate(dropdown.transform.Find("ButtonToChange").gameObject);
		}
		InitializeDropdown();
		InitializeButton();

		scenes = new SceneAssetHelper[dropdown.options.Count];

		OnChange(-1);
	}

	/// <summary>
	/// Unity editor validation callback that keeps serialized references, action arrays, and visuals synchronized.
	/// </summary>
	/// <remarks>
	/// When Inspector values change, this method refreshes the dropdown reference, resizes the <see cref="scenes"/>
	/// array to match the current dropdown option count, and reapplies dropdown/button configuration.
	/// Existing action references are preserved by index where possible.
	/// </remarks>
	private void OnValidate()
	{
		if (dropdown == null)
		{
			dropdown = GetComponent<TMP_Dropdown>();
		}

		if (scenes == null)
		{
			scenes = new SceneAssetHelper[dropdown.options.Count];
		}
		else if (scenes.Length != dropdown.options.Count)
		{
			var temp = scenes;
			scenes = new SceneAssetHelper[dropdown.options.Count];
			for (int i = 0; i < dropdown.options.Count; i++)
			{
				if (i < temp.Length)
				{
					scenes[i] = temp[i];
				}
				else
				{
					continue;
				}
			}
		}
		ConfigureDropdown();
		ConfigureButton();
	}

	/// <summary>
	/// Initializes the dropdown object's editor-time layout and visual references.
	/// </summary>
	/// <remarks>
	/// This method removes the default dropdown background image in the editor, positions the dropdown,
	/// removes the default label child, configures the arrow image as the dropdown target graphic,
	/// and then applies the serialized appearance settings through <see cref="ConfigureDropdown"/>.
	/// </remarks>
	private void InitializeDropdown()
	{
		dropdown = GetComponent<TMP_Dropdown>();
		if (dropdown == null)
		{
			Debug.LogError("TMP_Dropdown component is missing. Please add this script to a newly created TMP_Dropdown");
		}

#if UNITY_EDITOR
		if (dropdown.GetComponent<Image>() != null)
		{
			Undo.DestroyObjectImmediate(dropdown.GetComponent<Image>());
		}
#else
		if (dropdown.GetComponent<Image>() != null)
		{
			Destroy(dropdown.GetComponent<Image>());
		}
#endif

		dropdown.transform.localPosition = Vector3.zero;

		RectTransform rt = (RectTransform)dropdown.transform;
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(dropdownWidth, dropdownHeight);

		if (dropdown.transform.Find("Label") != null)
			DestroyImmediate(dropdown.transform.Find("Label").gameObject);

		var arrow = transform.Find("Arrow");
		var arrowImage = arrow.GetComponent<Image>();
		arrowImage.transform.localPosition = Vector3.zero;

		rt = (RectTransform)arrowImage.transform;
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(dropdownWidth, dropdownHeight);
		rt.localPosition = Vector3.zero;

		dropdown.targetGraphic = arrowImage;

		ConfigureDropdown();
	}

	/// <summary>
	/// Applies serialized appearance settings to the dropdown.
	/// </summary>
	/// <remarks>
	/// Updates the dropdown option list, template color, arrow sprite, and selectable color block using
	/// the Inspector-configured values.
	/// </remarks>
	private void ConfigureDropdown()
	{
		dropdown = GetComponent<TMP_Dropdown>();
		if (dropdown == null)
		{
			Debug.LogError("TMP_Dropdown component is missing.");
		}

		dropdown.options = options;

		var template = dropdown.transform.Find("Template");
		template.GetComponent<Image>().color = normalColor;

		var arrowImage = transform.Find("Arrow").GetComponent<Image>();
		arrowImage.sprite = dropdownArrow;

		dropdown.colors = new ColorBlock
		{
			normalColor = normalColor,
			highlightedColor = highlightedColor,
			pressedColor = pressedColor,
			selectedColor = selectedColor,
			disabledColor = Color.gray,
			colorMultiplier = 1f
		};
	}

	/// <summary>
	/// Creates and lays out the companion button used to execute the selected dropdown action.
	/// </summary>
	/// <remarks>
	/// The button is created as a child named <c>ButtonToChange</c>, receives a TextMeshPro text component,
	/// receives a <see cref="Button"/> component, and is positioned beside the dropdown.
	/// Final styling is delegated to <see cref="ConfigureButton"/>.
	/// </remarks>
	private void InitializeButton()
	{
		GameObject buttonObject = new GameObject("ButtonToChange");
		buttonObject.transform.SetParent(transform);
		buttonObject.transform.localPosition = Vector3.zero;

		TMP_Text textComponent = buttonObject.AddComponent<TextMeshProUGUI>();
		buttonToChange = buttonObject.AddComponent<Button>();

		RectTransform rt = (RectTransform)buttonObject.transform;
		rt.anchorMin = new Vector2(0f, 0.5f);
		rt.anchorMax = new Vector2(0f, 0.5f);
		rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
		rt.localPosition = new Vector3(-buttonWidth / 2, 0, 0);

		ConfigureButton();
	}

	/// <summary>
	/// Applies visual settings to the companion button.
	/// </summary>
	/// <remarks>
	/// Ensures <see cref="buttonToChange"/> points to the generated child button when possible,
	/// sets its text color, and applies the same selectable color states used by the dropdown.
	/// </remarks>
	private void ConfigureButton()
	{
		if (buttonToChange == null)
		{
			var buttonObject = transform.Find("ButtonToChange");
			buttonToChange = buttonObject ? buttonObject.GetComponent<Button>() : null;
		}

		if (buttonToChange == null)
		{
			return;
		}

		var text = buttonToChange.GetComponent<TMP_Text>();

		if (text != null)
		{
			text.color = Color.white;
		}

		buttonToChange.colors = new ColorBlock
		{
			normalColor = normalColor,
			highlightedColor = highlightedColor,
			pressedColor = pressedColor,
			selectedColor = selectedColor,
			disabledColor = Color.gray,
			colorMultiplier = 1f
		};
	}

	/// <summary>
	/// Handles dropdown selection changes by updating the companion button label.
	/// </summary>
	/// <param name="i">Selected dropdown index provided by <see cref="TMP_Dropdown.onValueChanged"/>.</param>
	/// <remarks>
	/// If the selected index has a valid <see cref="SceneAssetHelper"/> with a non-empty name,
	/// that name is displayed on the companion button. Otherwise, the button displays
	/// <c>No Action Assigned</c>.
	/// </remarks>
	public void OnChange(int i)
	{
		if (buttonToChange != null)
		{
			var text = buttonToChange.GetComponent<TMP_Text>();
			if (text != null)
			{
				if (0 <= i && i < scenes.Length && scenes[i] != null && !string.IsNullOrEmpty(scenes[i].Name))
				{
					text.text = scenes[i].Name;
				}
				else
				{
					text.text = "No Action Assigned";
				}
			}
		}
		else
		{
			Debug.LogWarning("ButtonToChange is not assigned or found in the hierarchy.");
		}
	}

	/// <summary>
	/// Executes the action mapped to the currently selected dropdown index.
	/// </summary>
	/// <remarks>
	/// The selected entry is read from <see cref="TMP_Dropdown.value"/>. If the matching
	/// <see cref="SceneAssetHelper"/> exists, its <see cref="SceneAssetHelper.Run"/> method is called.
	/// Missing actions are reported as warnings.
	/// </remarks>
	public void OnButtonClicked()
	{
		if (dropdown.value < scenes.Length && scenes[dropdown.value] != null)
			scenes[dropdown.value].Run();
		else
			Debug.LogWarning("No action assigned for the selected dropdown option.");
	}
}