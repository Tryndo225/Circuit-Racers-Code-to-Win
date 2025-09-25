using UnityEngine;
using System;
using System.Threading.Tasks;

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

[Serializable]
public abstract class ButtonType
{
    public abstract void Action();
}

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
    [SerializeField] private GameObject PopUp;

    public GenerateLevelButton()
    { }

    public override async void Action()  // ok for top-level UI handlers
    {
        try
        {
            Debug.Log("Starting level generation...");
            var map = await Task.Run(() =>
            {
                return GameDataManager.Instance.Generator.GenerateLevel(50, 50, true, SeedFactory.Next());
            });

            Debug.Log("Level generated, generating checkpoints...");

            await Task.Run(() =>
            {
                LevelCheckPointMaker.GenerateCheckPoints(map);
            });

            Debug.Log("Checkpoints generated, showing map...");

            if (map != null)
            {
                PopUp.GetComponent<LevelPopUp>().ShowMap(map);
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