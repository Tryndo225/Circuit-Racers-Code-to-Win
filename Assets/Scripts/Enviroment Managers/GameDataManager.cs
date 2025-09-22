using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.Serialization;
using Generic;
using Unity.VisualScripting;

[Serializable]
public struct LevelData : ISerializable
{
    public LevelMap LevelMap;
    public float Time;

    public LevelData(LevelMap levelMap, float time)
    {
        LevelMap = levelMap;
        Time = time;
    }

    public LevelData(SerializationInfo info, StreamingContext context)
    {
        LevelMap = (LevelMap)info.GetValue("LevelMap", typeof(LevelMap));
        Time = info.GetSingle("Time");
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("LevelMap", LevelMap, typeof(LevelMap));
        info.AddValue("Time", Time);
    }
}

[Serializable]
public class GameData : ISerializable
{
    public List<LevelData> Levels;

    public GameData()
    {
        Levels = new List<LevelData>();
    }

    public GameData(SerializationInfo info, StreamingContext context)
    {
        Levels = (List<LevelData>)info.GetValue("Levels", typeof(List<LevelData>));
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("Levels", Levels, typeof(List<LevelData>));
    }

    public void UpdateLevelTime(LevelMap map, float time)
    {
        int index = Levels.FindIndex(ld => ld.LevelMap == map);
        if (index >= 0)
        {
            if (time < Levels[index].Time)
            {
                Levels[index] = new LevelData(map, time);
            }
        }
        else
        {
            Levels.Add(new LevelData(map, time));
        }
    }
}

public class GameDataManager : Generic.Singleton<GameDataManager>
{
    private void Start()
    {
        if (PlayerPrefs.HasKey("GameData"))
        {
            LoadGameData();
        }
    }

    private void OnApplicationQuit()
    {
        SaveGameData();
    }

    public GameData CurrentGameData { get; private set; } = new GameData();
    public LevelMap CurrentLevelMap { get; private set; } = null;

    private void SaveGameData()
    {
        try
        {
            string json = JsonUtility.ToJson(CurrentGameData);
            PlayerPrefs.SetString("GameData", json);
            PlayerPrefs.Save();
            Debug.Log("Game data saved.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game data: {e.Message}");
        }
    }

    private void LoadGameData()
    {
        try
        {
            string json = PlayerPrefs.GetString("GameData");
            CurrentGameData = JsonUtility.FromJson<GameData>(json);
            Debug.Log("Game data loaded.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load game data: {e.Message}");
            CurrentGameData = new GameData();
        }
    }

    public void SelectingLevelMap(LevelMap map)
    {
        bool validMap = false;
        for (int i = 0; i < CurrentGameData.Levels.Count; i++)
        {
            if (CurrentGameData.Levels[i].LevelMap == map)
            {
                validMap = true;
            }
        }
        if (!validMap)
        {
            Debug.LogError("Selected level map is not in game data.");
            return;
        }

        CurrentLevelMap = map;
    }

    public void CompleteLevel(float time)
    {
        if (CurrentLevelMap == null)
        {
            Debug.LogError("No level map selected.");
            return;
        }
        CurrentGameData.UpdateLevelTime(CurrentLevelMap, time);
        SaveGameData();
    }
}