using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.UI;

#region Generic Button Handler
public class ButtonScript : MonoBehaviour
{
    [SerializeReference] private ButtonType properties;

    public ButtonType Properties
    {
        get => properties;
        set => properties = value;
    }

    public void OnButtonClick()
    {
        if (properties == null)
        {
            Debug.LogWarning("Button properties are not set.");
            return;
        }
        properties.Action();
    }
}
#endregion Generic Button Handler

#region Button Types
[Serializable]
public abstract class ButtonType
{
    public abstract void Action();
}

#region Concrete Button Types
[Serializable]
public class ChangeSceneButton : ButtonType
{
    public ChangeSceneButton()
    { }

    [SerializeField] private SceneAssetHelper _sceneToLoad;

    public override void Action()
    {
        if (_sceneToLoad == null)
        {
            Debug.LogError("Scene to load is not assigned.");
            return;
        }
        SceneManagement.Instance.ChangeScene(_sceneToLoad);
    }
}

[Serializable]
public class SelectLevelButton : ButtonType
{
    public SelectLevelButton()
    { }

    public SelectLevelButton(LevelMap levelMap)
    {
        _levelMap = levelMap;
    }

    [SerializeField, ReadOnly] private LevelMap _levelMap;

    public override void Action()
    {
        if (_levelMap == null)
        {
            Debug.LogError("Level map is not assigned.");
            return;
        }
        GameDataManager.Instance.SelectingLevelMap(_levelMap);
    }
}

[Serializable]
public class RemoveLevelButton : ButtonType
{
    public RemoveLevelButton()
    { }

    [SerializeField, ReadOnly] private LevelMap _levelMap;

    public override void Action()
    {
        if (_levelMap == null)
        {
            Debug.LogError("Level map is not assigned.");
            return;
        }
        GameDataManager.Instance.RemoveLevel(_levelMap);
    }
}

[Serializable]
public class GenerateLevelButton : ButtonType
{
    [Header("References")]
    [SerializeField] private GameObject popUp;
    [SerializeField] private Slider sizeXSlider;
    [SerializeField] private Slider sizeYSlider;
    [SerializeField] private Slider stepCountSlider;
    [SerializeField] private Toggle createCircuitsToggle;

    [Header("Generation Settings")]
    [SerializeField] public bool createCircuits = true;
    [SerializeField] public int sizeX = 50;
    [SerializeField] public int sizeY = 50;

    [SerializeField] private int stepCount = 20;
    [SerializeField, ReadOnly] private int stepSize;
    [SerializeField] private int maxAttemps = 1000;

    public GenerateLevelButton()
    { }

    private void UpdateValues()
    {
        if (sizeXSlider != null)
            sizeX = Mathf.RoundToInt(sizeXSlider.value);
        if (sizeYSlider != null)
            sizeY = Mathf.RoundToInt(sizeYSlider.value);
        if (stepCountSlider != null)
            stepCount = Mathf.RoundToInt(stepCountSlider.value);
        if (createCircuitsToggle != null)
            createCircuits = createCircuitsToggle.isOn;
    }

    public override async void Action()  // ok for top-level UI handlers
    {
        try
        {
            if (popUp == null)
            {
                Debug.LogError("Pop-up is not assigned.");
                return;
            }

            UpdateValues();

            stepSize = Mathf.RoundToInt(Mathf.Sqrt(sizeX * sizeY)) * 4 / stepCount;

            Debug.Log("Starting level generation...");
            var map = await Task.Run(() =>
            {
                return LevelGenerator.GenerateLevel(sizeX, sizeY, createCircuits, stepCount, stepSize, maxAttemps, SeedFactory.Next());
            });

            Debug.Log("Level generated, generating checkpoints...");

            await Task.Run(() =>
            {
                LevelCheckPointMaker.GenerateCheckPoints(map);
            });

            Debug.Log("Checkpoints generated, showing map...");

            if (map != null)
            {
                popUp.GetComponent<LevelPopUp>().ShowMap(map);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Level generation failed: {ex}");
        }
    }
}

[Serializable]
public class ClearLevelsButton : ButtonType
{
    public ClearLevelsButton()
    { }

    public override void Action()
    {
        GameDataManager.Instance.ClearLevels();
    }
}

[Serializable]
public class GoToSelectedLevel : ButtonType
{
    public GoToSelectedLevel()
    { }

    public override void Action()
    {
        GameDataManager.Instance.GoToSelectedLevel();
    }
}
#endregion Concrete Button Types
#endregion Button Types