using UnityEngine;

public class ButtonScript : MonoBehaviour
{
    [SerializeField] private ActionType actionType;
    [SerializeField] private SceneAssetHelper sceneToLoad;

    private enum ActionType
    {
        ChangeScene
    }

    public void OnButtonClick()
    {
        switch (actionType)
        {
            case ActionType.ChangeScene:
                Debug.Log($"Changing scene to: {sceneToLoad.Name}");
                SceneChange(sceneToLoad);
                break;

            default:
                Debug.LogWarning("No action assigned to button.");
                break;
        }
    }

    public void SceneChange(SceneAssetHelper scene)
    {
        SceneManagement.Instance.ChangeScene(scene);
    }
}