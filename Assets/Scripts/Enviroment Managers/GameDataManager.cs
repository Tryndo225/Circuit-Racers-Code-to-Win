using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using IEnumerableExtention;       // For GetContentHash()
using UnityEngine;

/// <summary>
/// Runtime manager for persistent game progression data (levels, best times, current selection).
/// </summary>
/// <remarks>
/// @defgroup game_data_mgr Game Data Manager
/// @ingroup systems
/// @brief Central orchestrator for saving/loading <see cref="GameData"/>, tracking the selected
/// <see cref="LevelMap"/>, and notifying listeners about changes.
/// 
/// Responsibilities:
/// - Load/save <see cref="GameData"/> to PlayerPrefs as JSON under key "GameData".
/// - Maintain the currently selected <see cref="LevelMap"/> used by gameplay scene transitions.
/// - Provide helper APIs for adding/removing/clearing levels and recording best times.
/// - Compute and expose a content hash of the level list; notify observers when it changes.
/// 
/// @thread Unity main thread only.
/// @invariant Exactly one instance exists (via <c>Generic.Singleton&lt;T&gt;</c>).
/// @invariant <see cref="Hash"/> reflects <see cref="CurrentGameData"/>.Levels via <c>GetContentHash()</c>.
/// @invariant Best time is monotonically improved (never worsened by <see cref="CompleteLevel"/>).
/// </remarks>
public class GameDataManager : Generic.Singleton<GameDataManager>
{
    #region Data Records

    /// <summary>
    /// Serializable record containing a <see cref="LevelMap"/> reference and its best (lowest) time.
    /// </summary>
    [Serializable]
    public class LevelData : ISerializable
    {
        /// <summary>Associated level map.</summary>
        public LevelMap LevelMap;

        /// <summary>Best completion time (seconds). Use <see cref="float.MaxValue"/> for unknown.</summary>
        public float Time;

        /// <summary>
        /// Creates a record with an unknown best time.
        /// </summary>
        /// <param name="levelMap">Target level map.</param>
        public LevelData(LevelMap levelMap) : this(levelMap, float.MaxValue) { }

        /// <summary>
        /// Creates a record with a specified best time.
        /// </summary>
        /// <param name="levelMap">Target level map.</param>
        /// <param name="time">Best time in seconds.</param>
        public LevelData(LevelMap levelMap, float time)
        {
            LevelMap = levelMap;
            Time = time;
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        public LevelData(SerializationInfo info, StreamingContext context)
        {
            LevelMap = (LevelMap)info.GetValue("LevelMap", typeof(LevelMap));
            Time = info.GetSingle("Time");
        }

        /// <inheritdoc />
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("LevelMap", LevelMap, typeof(LevelMap));
            info.AddValue("Time", Time);
        }

        /// <summary>
        /// Equality compares the <see cref="LevelMap"/> identity (best time does not affect equality).
        /// </summary>
        public static bool operator ==(LevelData a, LevelData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.LevelMap == b.LevelMap;
        }

        /// <inheritdoc cref="operator =="/>
        public static bool operator !=(LevelData a, LevelData b) => !(a == b);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is LevelData other && this == other;

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(LevelMap, Time);
    }

    /// <summary>
    /// Serializable container for all saved progression data.
    /// </summary>
    [Serializable]
    public class GameData : ISerializable
    {
        /// <summary>List of known levels and their best times.</summary>
        public List<LevelData> Levels;

        /// <summary>Creates an empty game data set.</summary>
        public GameData() => Levels = new List<LevelData>();

        /// <summary>Deserialization constructor.</summary>
        public GameData(SerializationInfo info, StreamingContext context)
        {
            Levels = (List<LevelData>)info.GetValue("Levels", typeof(List<LevelData>));
        }

        /// <inheritdoc />
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Levels", Levels, typeof(List<LevelData>));
        }

        /// <summary>
        /// Adds or improves a level time; replaces only if <paramref name="time"/> is lower than the stored best.
        /// </summary>
        /// <param name="map">Target level map.</param>
        /// <param name="time">New completion time (seconds).</param>
        public void UpdateLevelTime(LevelMap map, float time)
        {
            int index = Levels.FindIndex(ld => ld.LevelMap == map);
            if (index >= 0)
            {
                if (time < Levels[index].Time)
                    Levels[index] = new LevelData(map, time);
            }
            else
            {
                Levels.Add(new LevelData(map, time));
            }
        }

        /// <summary>
        /// Returns true if the level exists in the data set.
        /// </summary>
        /// <param name="map">Level map to check.</param>
        public bool ContainsLevel(LevelMap map) => Levels.Exists(ld => ld.LevelMap == map);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// The active data set (list of <see cref="LevelData"/> entries).
    /// </summary>
    public GameData CurrentGameData { get; private set; } = new GameData();

    /// <summary>
    /// The currently selected level to be played (may be null if not selected).
    /// </summary>
    public LevelMap CurrentLevelMap { get; private set; } = null;

    /// <summary>
    /// Hash of <see cref="CurrentGameData"/>.Levels content for change detection.
    /// </summary>
    public int Hash { get; private set; }

    #endregion

    #region Observers

    /// <summary>
    /// Registered listeners notified whenever the game data content hash changes.
    /// </summary>
    private readonly List<Action> onGameDataChanged = new List<Action>();

    /// <summary>
    /// Subscribes a listener to hash-change notifications.
    /// </summary>
    /// <param name="action">Callback invoked when game data changes.</param>
    public void AddListener(Action action)
    {
        if (action != null && !onGameDataChanged.Contains(action))
            onGameDataChanged.Add(action);
    }

    /// <summary>
    /// Unsubscribes a previously registered listener.
    /// </summary>
    /// <param name="action">Callback to remove.</param>
    public void RemoveListener(Action action)
    {
        if (action != null)
            onGameDataChanged.Remove(action);
    }

    #endregion

    #region Unity Methods

    /// <summary>
    /// Unity method: loads saved game data from PlayerPrefs (if present) on startup.
    /// </summary>
    private void Start()
    {
        if (PlayerPrefs.HasKey("GameData"))
            LoadGameData();
    }

    /// <summary>
    /// Unity method: saves game data to PlayerPrefs on application quit.
    /// </summary>
    private void OnApplicationQuit() => SaveGameData();

    #endregion

    #region Persistence

    /// <summary>
    /// Serializes <see cref="CurrentGameData"/> to JSON and writes it to PlayerPrefs ("GameData").
    /// </summary>
    private void SaveGameData()
    {
        try
        {
            string json = JsonUtility.ToJson(CurrentGameData);
            PlayerPrefs.SetString("GameData", json);
            PlayerPrefs.Save();
            Debug.Log($"[GameDataManager] Saved {CurrentGameData.Levels.Count} level(s).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataManager] Failed to save game data: {e.Message}");
        }
    }

    /// <summary>
    /// Loads JSON from PlayerPrefs ("GameData") and deserializes into <see cref="CurrentGameData"/>.
    /// Recomputes <see cref="Hash"/> and prints debug info.
    /// </summary>
    private void LoadGameData()
    {
        try
        {
            string json = PlayerPrefs.GetString("GameData");
            CurrentGameData = JsonUtility.FromJson<GameData>(json) ?? new GameData();

            Hash = CurrentGameData.Levels.GetContentHash();
            Debug.Log($"[GameDataManager] Loaded {CurrentGameData.Levels.Count} level(s).");

            // Optional debug walk-through (guards for missing references)
            for (int i = 0; i < CurrentGameData.Levels.Count; i++)
            {
                var entry = CurrentGameData.Levels[i];
                if (entry == null || entry.LevelMap == null)
                {
                    Debug.LogWarning($"[GameDataManager] Level map at index {i} is null.");
                }
                else
                {
                    // Replace Tiles.Print() with your own pretty printer if needed
                    Debug.Log($"[GameDataManager] Level {i}: Best {entry.Time:0.###} s");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataManager] Failed to load game data: {e.Message}");
            CurrentGameData = new GameData();
            Hash = 0;
        }
    }

    #endregion

    #region Selection & Flow

    /// <summary>
    /// Attempts to start gameplay by loading the "Level" scene.
    /// Requires <see cref="CurrentLevelMap"/> to be set via <see cref="SelectingLevelMap"/>.
    /// </summary>
    public void GoToSelectedLevel()
    {
        if (CurrentLevelMap == null)
        {
            Debug.LogError("[GameDataManager] No level map selected.");
            return;
        }
        SceneManagement.Instance.ChangeScene("Level");
    }

    /// <summary>
    /// Selects a level for play, provided it exists in <see cref="CurrentGameData"/>.
    /// </summary>
    /// <param name="map">The level map to select.</param>
    public void SelectingLevelMap(LevelMap map)
    {
        if (map == null)
        {
            Debug.LogError("[GameDataManager] SelectingLevelMap called with null map.");
            return;
        }

        if (!CurrentGameData.ContainsLevel(map))
        {
            Debug.LogWarning("[GameDataManager] Selected level is not present in CurrentGameData.");
            return;
        }

        CurrentLevelMap = map;
    }

    #endregion

    #region Progression

    /// <summary>
    /// Records completion of the currently selected level and updates best time if improved.
    /// Automatically saves the data set.
    /// </summary>
    /// <param name="time">Completion time in seconds.</param>
    public void CompleteLevel(float time)
    {
        if (CurrentLevelMap == null)
        {
            Debug.LogError("[GameDataManager] Cannot complete level; no level selected.");
            return;
        }

        CurrentGameData.UpdateLevelTime(CurrentLevelMap, time);
        SaveGameData(); // Persist immediately after improvement
    }

    /// <summary>
    /// Adds a level to the data set if it does not already exist; triggers change detection.
    /// </summary>
    /// <param name="map">Level to add.</param>
    public void AddLevel(LevelMap map)
    {
        if (map == null)
        {
            Debug.LogError("[GameDataManager] AddLevel called with null map.");
            return;
        }

        if (!CurrentGameData.ContainsLevel(map))
        {
            CurrentGameData.Levels.Add(new LevelData(map));
            PotentialHashChange(CurrentGameData.Levels.GetContentHash());
        }
        else
        {
            Debug.LogWarning("[GameDataManager] Level already exists; not adding duplicate.");
        }
    }

    /// <summary>
    /// Removes a level from the data set if present; triggers change detection.
    /// </summary>
    /// <param name="levelMap">Level to remove.</param>
    public void RemoveLevel(LevelMap levelMap)
    {
        if (levelMap == null)
        {
            Debug.LogError("[GameDataManager] RemoveLevel called with null map.");
            return;
        }

        for (int i = 0; i < CurrentGameData.Levels.Count; i++)
        {
            if (CurrentGameData.Levels[i].LevelMap == levelMap)
            {
                CurrentGameData.Levels.RemoveAt(i);
                PotentialHashChange(CurrentGameData.Levels.GetContentHash());
                return;
            }
        }

        Debug.LogWarning("[GameDataManager] Level not found; nothing removed.");
    }

    /// <summary>
    /// Clears all levels from the data set; triggers change detection.
    /// </summary>
    public void ClearLevels()
    {
        CurrentGameData.Levels.Clear();
        PotentialHashChange(CurrentGameData.Levels.GetContentHash());
    }

    #endregion

    #region Internals

    /// <summary>
    /// Compares a new hash with the current <see cref="Hash"/> and, if different,
    /// updates <see cref="Hash"/> and notifies all listeners.
    /// </summary>
    /// <param name="hash">Newly computed content hash.</param>
    private void PotentialHashChange(int hash)
    {
        if (hash == Hash) return;

        Hash = hash;

        // Notify observers (iterate snapshot to avoid modification during enumeration)
        var listeners = onGameDataChanged.ToArray();
        for (int i = 0; i < listeners.Length; i++)
        {
            try { listeners[i]?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    #endregion
}
