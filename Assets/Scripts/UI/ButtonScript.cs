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
        Debug.Log("Button clicked!");
    }

    public void SceneChange(SceneAssetHelper scene)
    {
        SceneManagement.Instance.ChangeScene(scene);
    }
}