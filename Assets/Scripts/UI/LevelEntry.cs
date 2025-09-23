using UnityEngine;
using TMPro;
using System;

public class LevelEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text levelTime;

    public LevelData levelData;

    public LevelEntry(LevelData data)
    {
        levelData = data;
    }

    public void LevelSelected()
    {
        GameDataManager.Instance.SelectingLevelMap(levelData.LevelMap);
    }

    public void RemoveLevel()
    {
        GameDataManager.Instance.RemoveLevel(levelData.LevelMap);
        Destroy(gameObject);
    }

    public void SetUp(LevelData levelData)
    {
        GetComponent<LevelPreviewer>()?.ShowPreviewAsync(levelData.LevelMap);
        if (levelData.Time < float.MaxValue)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(levelData.Time);
            levelTime.text = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        }
        else
        {
            levelTime.text = "No Time";
        }
    }
}