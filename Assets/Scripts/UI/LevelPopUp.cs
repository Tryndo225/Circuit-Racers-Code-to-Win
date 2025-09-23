using UnityEngine;

public class LevelPopUp : MonoBehaviour
{
    [SerializeField] private GameObject visuals;
    private LevelMap _levelMap;

    private void Start()
    {
        visuals.SetActive(false);
    }

    public void ShowMap(LevelMap map)
    {
        _levelMap = map;
        visuals.SetActive(true);
        var levelViewer = visuals.GetComponent<LevelPreviewer>();
        if (levelViewer != null)
        {
            _ = levelViewer.ShowPreviewAsync(map);
        }
    }

    public void HideMap()
    {
        visuals.GetComponent<LevelPreviewer>().Clear();
        _levelMap = null;
        visuals.SetActive(false);
    }

    public void KeepMap()
    {
        if (_levelMap != null)
        {
            GameDataManager.Instance.AddLevel(_levelMap);
        }
        HideMap();
    }
}