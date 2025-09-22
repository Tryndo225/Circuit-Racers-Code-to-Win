using UnityEngine;
using System;

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

    [SerializeField] private LevelMap _levelMap;

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