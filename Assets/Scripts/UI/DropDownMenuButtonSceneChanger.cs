using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Dropdown-driven scene (or action) selector that mirrors the current option onto a companion button,
/// and invokes a configured action when the button is pressed.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Configures a <see cref="TMP_Dropdown"/> and a sibling <see cref="Button"/> so the dropdown chooses
/// an action and the button triggers it. Also supports custom visuals (arrow sprite and color states).
///
/// Responsibilities:
/// - Initialize and style the dropdown template, arrow, and selectable color states.
/// - Create/configure an adjacent button (when resetting) that displays the selected option's label.
/// - Keep the button label in sync with the currently selected dropdown item.
/// - Execute the mapped <see cref="SceneAssetHelper"/> action when the button is clicked.
///
/// Threading:
/// - Unity main thread only (MonoBehaviour lifecycle).
///
/// Usage:
/// - Add to a GameObject with a <see cref="TMP_Dropdown"/> (and children "Template" and "Arrow").
/// - Assign <see cref="options"/> to populate the dropdown and <see cref="scenes"/> for per-option actions.
/// - Optionally tweak colors and arrow sprite under "Dropdown Appearance".
/// - At runtime, user picks an option; the adjacent button shows that selection and triggers its action.
/// </remarks>
public class DropDownMenuButtonSceneChanger : MonoBehaviour
{
    [Header("References")]
    /// <summary>
    /// The button that mirrors the dropdown selection and triggers the mapped action.
    /// Auto-created on <see cref="Reset"/> if missing.
    /// </summary>
    [SerializeField, ReadOnly] private Button buttonToChange;

    /// <summary>
    /// The TMP dropdown that selects among available options/actions.
    /// </summary>
    [SerializeField, ReadOnly] private TMP_Dropdown dropdown;

    [Header("Dropdown Apperance")]
    /// <summary>
    /// Dropdown entries to display (text/icon).
    /// </summary>
    [SerializeField] private List<TMP_Dropdown.OptionData> options = new();

    /// <summary>
    /// Sprite used for the dropdown's "Arrow" image child.
    /// </summary>
    [SerializeField] private Sprite dropdownArrow;

    /// <summary>Selectable normal color.</summary>
    [SerializeField] private Color normalColor = Color.white;
    /// <summary>Selectable highlighted color.</summary>
    [SerializeField] private Color highlightedColor = Color.gray;
    /// <summary>Selectable pressed color.</summary>
    [SerializeField] private Color pressedColor = Color.black;
    /// <summary>Selectable selected color.</summary>
    [SerializeField] private Color selectedColor = Color.blue;

    [Header("Actions")]
    /// <summary>
    /// Per-option action map; typically scene loads or similar. Index corresponds to <see cref="TMP_Dropdown.value"/>.
    /// </summary>
    [SerializeField] private SceneAssetHelper[] scenes;

    /// <summary>Editor-time width of the dropdown root RectTransform (Reset-created layout).</summary>
    private float dropdownWidth = 150f;
    /// <summary>Editor-time height of the dropdown root RectTransform (Reset-created layout).</summary>
    private float dropdownHeight = 150f;
    /// <summary>Editor-time width of the companion button (Reset-created layout).</summary>
    private float buttonWidth = 1000f;
    /// <summary>Editor-time height of the companion button (Reset-created layout).</summary>
    private float buttonHeight = 200f;

    /// <summary>
    /// Unity Awake: wire dropdown and button callbacks if present.
    /// </summary>
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
    /// Unity Reset: editor-only setup. Rebuilds dropdown visuals and creates a companion button, then
    /// initializes the actions array to match the number of dropdown options.
    /// </summary>
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
    /// Unity OnValidate: keep serialized arrays/sizing and visuals in sync when values change in the Inspector.
    /// </summary>
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
    /// Editor-time creation and basic layout of the dropdown root, removing default background
    /// and wiring the arrow/image references before applying appearance via <see cref="ConfigureDropdown"/>.
    /// </summary>
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
    /// Applies the serialized appearance (<see cref="options"/>, colors, arrow sprite) to the dropdown.
    /// </summary>
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
    /// Editor-time creation and basic layout of the companion button, then applies styling via <see cref="ConfigureButton"/>.
    /// </summary>
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
    /// Applies selectable colors to the companion button and ensures we have a reference to it.
    /// </summary>
    private void ConfigureButton()
    {
        if (buttonToChange == null)
        {
            var buttonObject = transform.Find("ButtonToChange");
            buttonToChange = buttonObject ? buttonObject.GetComponent<Button>() : null;
        }

        buttonToChange.GetComponent<TMP_Text>().color = Color.white;
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
    /// Dropdown change callback: updates the button label to reflect the selected option/action.
    /// </summary>
    /// <param name="i">Selected index from <see cref="TMP_Dropdown.onValueChanged"/>.</param>
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
    /// Button click handler: runs the mapped action for the current dropdown value, if any.
    /// </summary>
    public void OnButtonClicked()
    {
        if (dropdown.value < scenes.Length && scenes[dropdown.value] != null)
            scenes[dropdown.value].Run();
        else
            Debug.LogWarning("No action assigned for the selected dropdown option.");
    }
}
