using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEditor;

public class DropDownMenuButtonSceneChanger : MonoBehaviour
{
    [Header("References")]
    [SerializeField, ReadOnly] private Button buttonToChange;

    [SerializeField, ReadOnly] private TMP_Dropdown dropdown;

    [Header("Dropdown Apperance")]
    [SerializeField] private List<TMP_Dropdown.OptionData> options = new();

    [SerializeField] private Sprite dropdownArrow;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightedColor = Color.gray;
    [SerializeField] private Color pressedColor = Color.black;
    [SerializeField] private Color selectedColor = Color.blue;

    [Header("Actions")]
    [SerializeField] private SceneAssetHelper[] scenes;

    private float dropdownWidth = 150f;
    private float dropdownHeight = 150f;
    private float buttonWidth = 1000f;
    private float buttonHeight = 200f;

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

    public void OnButtonClicked()
    {
        if (dropdown.value < scenes.Length && scenes[dropdown.value] != null)
            scenes[dropdown.value].Run();
        else
            Debug.LogWarning("No action assigned for the selected dropdown option.");
    }
}