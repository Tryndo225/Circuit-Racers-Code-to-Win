using UnityEngine;
using System.Collections.Generic;
using System;
using System.Runtime.Serialization;
using IEnumerableExtention;

[Serializable]
public class LevelData : ISerializable
{
    public LevelMap LevelMap;
    public float Time;

    public LevelData(LevelMap levelMap) : this(levelMap, float.MaxValue)
    {
    }

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

    public static bool operator ==(LevelData a, LevelData b)
    {
        return a.LevelMap == b.LevelMap;
    }

    public static bool operator !=(LevelData a, LevelData b)
    {
        return !(a == b);
    }

    public override bool Equals(object obj)
    {
        if (obj is LevelData other)
        {
            return this == other;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LevelMap, Time);
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

    public bool ContainsLevel(LevelMap map)
    {
        return Levels.Exists(ld => ld.LevelMap == map);
    }
}

public class GameDataManager : Generic.Singleton<GameDataManager>
{
    public GameData CurrentGameData { get; private set; } = new GameData();
    public LevelMap CurrentLevelMap { get; private set; } = null;
    public int Hash { get; private set; }

    public LevelGenerator Generator { get; private set; } = new LevelGenerator(20, 10, 1000);

    private List<Action> onGameDataChanged = new List<Action>();

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

    public void AddListener(Action action)
    {
        onGameDataChanged.Add(action);
    }

    public void RemoveListener(Action action)
    {
        onGameDataChanged.Remove(action);
    }

    private void SaveGameData()
    {
        try
        {
            Debug.Log("Game data saved.");
            string json = JsonUtility.ToJson(CurrentGameData);
            PlayerPrefs.SetString("GameData", json);
            PlayerPrefs.Save();
            Debug.Log($"Saved {CurrentGameData.Levels.Count} levels.");
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
            Hash = CurrentGameData.Levels.GetContentHash();
            Debug.Log("Game data loaded.");
            Debug.Log($"Loaded {CurrentGameData.Levels.Count} levels.");
            for (int i = 0; i < CurrentGameData.Levels.Count; i++)
            {
                if (CurrentGameData.Levels[i].LevelMap == null)
                {
                    Debug.LogWarning($"Level map at index {i} is null.");
                }
                else
                {
                    Debug.Log($"Level {i}: {CurrentGameData.Levels[i].LevelMap.Tiles.Print()}, Time: {CurrentGameData.Levels[i].Time}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load game data: {e.Message}");
            CurrentGameData = new GameData();
        }
    }

    public void GoToSelectedLevel()
    {
        if (CurrentLevelMap == null)
        {
            Debug.LogError("No level map selected.");
            return;
        }
        SceneManagement.Instance.ChangeScene("Level");
    }

    public void SelectingLevelMap(LevelMap map)
    {
        Debug.Log("Selecting level map.");
        bool validMap = CurrentGameData.ContainsLevel(map);

        if (!validMap)
        {
            Debug.LogWarning("Selected level map is not in game data.");
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

    public void AddLevel(LevelMap map)
    {
        if (map == null)
        {
            Debug.LogError("No level map provided.");
            return;
        }
        if (!CurrentGameData.ContainsLevel(map))
        {
            Debug.Log("Adding new level map to game data.");
            CurrentGameData.Levels.Add(new LevelData(map));
            var newHash = CurrentGameData.Levels.GetContentHash();
            PotentialHashChange(newHash);
        }
        else
        {
            Debug.LogWarning("Level map already exists in game data.");
        }
    }

    public void RemoveLevel(LevelMap levelMap)
    {
        if (levelMap == null)
        {
            Debug.LogError("No level map provided.");
            return;
        }
        for (int i = 0; i < CurrentGameData.Levels.Count; i++)
        {
            if (CurrentGameData.Levels[i].LevelMap == levelMap)
            {
                Debug.Log("Removing level map from game data.");
                CurrentGameData.Levels.RemoveAt(i);
                var newHash = CurrentGameData.Levels.GetContentHash();
                PotentialHashChange(newHash);
                return;
            }
        }
        Debug.LogWarning("Level map not found in game data.");
    }

    public void ClearLevels()
    {
        CurrentGameData.Levels.Clear();
        var newHash = CurrentGameData.Levels.GetContentHash();
        PotentialHashChange(newHash);
    }

    private void PotentialHashChange(int hash)
    {
        Debug.Log($"Current Hash: {Hash}, New Hash: {hash}");
        if (hash != Hash)
        {
            Hash = hash;
            Debug.Log("Game data changed, notifying listeners.");
            foreach (var action in onGameDataChanged)
            {
                action.Invoke();
            }
        }
    }
}