using IEnumerableExtensions;       // For GetContentHash()
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

/// <summary>
/// Runtime manager for persistent game progression data.
/// </summary>
/// <remarks>
/// @ingroup game_data
/// @brief Central manager for saving/loading <see cref="GameData"/>, tracking the selected
/// <see cref="LevelMap"/>, and notifying listeners about changes.
///
/// Responsibilities:
/// - Load and save <see cref="GameData"/> to PlayerPrefs as JSON under the key <c>GameData</c>.
/// - Maintain the currently selected <see cref="LevelMap"/> used by gameplay scene transitions.
/// - Provide helper APIs for adding, removing, clearing, editing, and replacing levels.
/// - Record best times, checkpoint splits, and best replays.
/// - Store assist settings.
/// - Compute and expose a content hash of the level list.
/// - Notify observers when the level-list hash changes.
///
/// Threading:
/// - Unity main thread only.
/// </remarks>
public class GameDataManager : Generic.Singleton<GameDataManager>
{
	#region Data Records

	/// <summary>
	/// Serializable record containing a level map and its best saved run data.
	/// </summary>
	/// <remarks>
	/// @ingroup game_data
	/// @brief Stores the best time, checkpoint splits, and replay associated with one level.
	/// </remarks>
	[Serializable]
	public class LevelData : ISerializable
	{
		/// <summary>
		/// Associated level map.
		/// </summary>
		public LevelMap LevelMap;

		/// <summary>
		/// Best completion time in seconds.
		/// </summary>
		/// <remarks>
		/// Uses <see cref="float.MaxValue"/> when no completion time is known.
		/// </remarks>
		public float Time = float.MaxValue;

		/// <summary>
		/// Best checkpoint split times for this level.
		/// </summary>
		public float[] CheckpointTimeSplits;

		/// <summary>
		/// Replay associated with the stored best time.
		/// </summary>
		public Replay BestReplay;

		/// <summary>
		/// Parameterless constructor for Unity serialization.
		/// </summary>
		public LevelData() { }

		/// <summary>
		/// Creates a level record with an unknown best time.
		/// </summary>
		/// <param name="levelMap">Target level map.</param>
		public LevelData(LevelMap levelMap) : this(levelMap, float.MaxValue) { }

		/// <summary>
		/// Creates a level record with a specified best time.
		/// </summary>
		/// <param name="levelMap">Target level map.</param>
		/// <param name="time">Best time in seconds.</param>
		/// <param name="checkpointTimeSplits">Optional checkpoint split times.</param>
		/// <param name="replay">Optional replay associated with this time.</param>
		public LevelData(LevelMap levelMap, float time, float[] checkpointTimeSplits = null, Replay replay = null)
		{
			LevelMap = levelMap;
			Time = time;
			CheckpointTimeSplits = checkpointTimeSplits ?? CreateDefaultSplits(levelMap);
			BestReplay = replay;
		}

		/// <summary>
		/// Deserialization constructor used by serializers that rely on <see cref="ISerializable"/>.
		/// </summary>
		/// <param name="info">Serialized level-data values.</param>
		/// <param name="context">Serialization context.</param>
		public LevelData(SerializationInfo info, StreamingContext context)
		{
			LevelMap = GetValueOrDefault<LevelMap>(info, "LevelMap", null);
			Time = GetValueOrDefault(info, "Time", float.MaxValue);
			CheckpointTimeSplits = GetValueOrDefault<float[]>(info, "CheckpointTimeSplits", null);
			BestReplay = GetValueOrDefault<Replay>(info, "BestReplay", null);

			EnsureValid();
		}

		/// <summary>
		/// Writes this level-data record into the serialization store.
		/// </summary>
		/// <param name="info">Serialization target.</param>
		/// <param name="context">Serialization context.</param>
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("LevelMap", LevelMap, typeof(LevelMap));
			info.AddValue("Time", Time);
			info.AddValue("CheckpointTimeSplits", CheckpointTimeSplits, typeof(float[]));
			info.AddValue("BestReplay", BestReplay, typeof(Replay));
		}

		/// <summary>
		/// Ensures loaded data is usable even if it came from an older save format.
		/// </summary>
		public void EnsureValid()
		{
			if (CheckpointTimeSplits == null)
				CheckpointTimeSplits = CreateDefaultSplits(LevelMap);
		}

		/// <summary>
		/// Checks whether two level-data records contain the same saved run data.
		/// </summary>
		/// <param name="a">First level-data record.</param>
		/// <param name="b">Second level-data record.</param>
		/// <returns><c>true</c> when both records are equal; otherwise <c>false</c>.</returns>
		public static bool operator ==(LevelData a, LevelData b)
		{
			if (ReferenceEquals(a, b))
				return true;

			if (a is null || b is null)
				return false;

			return a.LevelMap == b.LevelMap && a.Time == b.Time && FloatArrayEquals(a.CheckpointTimeSplits, b.CheckpointTimeSplits) && EqualityComparer<Replay>.Default.Equals(a.BestReplay, b.BestReplay);
		}

		/// <summary>
		/// Checks whether two level-data records differ.
		/// </summary>
		/// <param name="a">First level-data record.</param>
		/// <param name="b">Second level-data record.</param>
		/// <returns><c>true</c> when the records differ; otherwise <c>false</c>.</returns>
		public static bool operator !=(LevelData a, LevelData b)
		{
			return !(a == b);
		}

		/// <summary>
		/// Checks whether this level-data record equals another object.
		/// </summary>
		/// <param name="obj">Object to compare with this record.</param>
		/// <returns><c>true</c> when the object is an equal <see cref="LevelData"/>; otherwise <c>false</c>.</returns>
		public override bool Equals(object obj)
		{
			return obj is LevelData other && this == other;
		}

		/// <summary>
		/// Computes a hash code from the stored level map, time, splits, and replay.
		/// </summary>
		/// <returns>Hash code for this level-data record.</returns>
		public override int GetHashCode()
		{
			int hash = HashCode.Combine(LevelMap, Time, BestReplay);

			if (CheckpointTimeSplits != null)
				for (int i = 0; i < CheckpointTimeSplits.Length; i++)
					hash = HashCode.Combine(hash, CheckpointTimeSplits[i]);

			return hash;
		}

		/// <summary>
		/// Creates a default checkpoint split array for a level.
		/// </summary>
		/// <param name="levelMap">Level map whose checkpoint and lap count should be used.</param>
		/// <returns>Default split array for the given level.</returns>
		private static float[] CreateDefaultSplits(LevelMap levelMap)
		{
			if (levelMap == null)
				return new float[0];

			int splitCount = levelMap.CheckpointCountPerLap * levelMap.Laps + 1;
			splitCount = Mathf.Max(0, splitCount);

			return new float[splitCount];
		}

		/// <summary>
		/// Compares two float arrays by value.
		/// </summary>
		/// <param name="a">First array.</param>
		/// <param name="b">Second array.</param>
		/// <returns><c>true</c> when both arrays contain the same values; otherwise <c>false</c>.</returns>
		private static bool FloatArrayEquals(float[] a, float[] b)
		{
			if (ReferenceEquals(a, b))
				return true;

			if (a == null || b == null)
				return false;

			if (a.Length != b.Length)
				return false;

			for (int i = 0; i < a.Length; i++)
				if (a[i] != b[i])
					return false;

			return true;
		}

		/// <summary>
		/// Reads an optional serialized value.
		/// </summary>
		/// <typeparam name="T">Expected value type.</typeparam>
		/// <param name="info">Serialized data source.</param>
		/// <param name="name">Serialized entry name.</param>
		/// <param name="fallback">Fallback value used when the entry is missing.</param>
		/// <returns>The stored value when present; otherwise <paramref name="fallback"/>.</returns>
		/// <remarks>
		/// This is used for backwards compatibility with older save data.
		/// </remarks>
		private static T GetValueOrDefault<T>(SerializationInfo info, string name, T fallback)
		{
			foreach (SerializationEntry entry in info)
				if (entry.Name == name)
					return (T)entry.Value;

			return fallback;
		}
	}

	/// <summary>
	/// Serializable assist-settings record.
	/// </summary>
	/// <remarks>
	/// @ingroup game_data
	/// @brief Stores whether driving assists are enabled in saved game data.
	/// </remarks>
	[Serializable]
	public class AssistsSettings : ISerializable
	{
		/// <summary>
		/// Whether anti-lock braking assist is enabled.
		/// </summary>
		public bool ABS;

		/// <summary>
		/// Whether traction-control assist is enabled.
		/// </summary>
		public bool TC;

		/// <summary>
		/// Creates assist settings with assists disabled by default.
		/// </summary>
		public AssistsSettings()
		{
			ABS = false;
			TC = false;
		}

		/// <summary>
		/// Deserialization constructor for assist settings.
		/// </summary>
		/// <param name="info">Serialized assist settings.</param>
		/// <param name="context">Serialization context.</param>
		protected AssistsSettings(SerializationInfo info, StreamingContext context)
		{
			ABS = info.GetBoolean(nameof(ABS));
			TC = info.GetBoolean(nameof(TC));
		}

		/// <summary>
		/// Writes assist settings into the serialization store.
		/// </summary>
		/// <param name="info">Serialization target.</param>
		/// <param name="context">Serialization context.</param>
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue(nameof(ABS), ABS);
			info.AddValue(nameof(TC), TC);
		}
	}

	/// <summary>
	/// Serializable container for all saved progression data.
	/// </summary>
	/// <remarks>
	/// @ingroup game_data
	/// @brief Stores custom levels, practice-map records, replays, and assist settings.
	/// </remarks>
	[Serializable]
	public class GameData : ISerializable
	{
		/// <summary>
		/// List of known levels and their best times.
		/// </summary>
		public List<LevelData> Levels;

		/// <summary>
		/// Best practice/test-map time.
		/// </summary>
		/// <remarks>
		/// Uses <see cref="float.MaxValue"/> when unknown.
		/// </remarks>
		public float PracticeMapTime = float.MaxValue;

		/// <summary>
		/// Best practice/test-map checkpoint splits.
		/// </summary>
		public float[] PracticeMapSplits = new float[30];

		/// <summary>
		/// Best practice/test-map replay.
		/// </summary>
		public Replay PracticeMapReplay;

		/// <summary>
		/// Saved driving assist settings.
		/// </summary>
		public AssistsSettings AssistsSettings;

		/// <summary>
		/// Creates an empty game data set.
		/// </summary>
		public GameData()
		{
			Levels = new List<LevelData>();
			AssistsSettings = new AssistsSettings();
		}

		/// <summary>
		/// Deserialization constructor used by serializers that rely on <see cref="ISerializable"/>.
		/// </summary>
		/// <param name="info">Serialized game-data values.</param>
		/// <param name="context">Serialization context.</param>
		public GameData(SerializationInfo info, StreamingContext context)
		{
			Levels = GetValueOrDefault(info, "Levels", new List<LevelData>());
			PracticeMapTime = GetValueOrDefault(info, "PracticeMapTime", float.MaxValue);
			PracticeMapSplits = GetValueOrDefault(info, "PracticeMapSplits", new float[30]);
			PracticeMapReplay = GetValueOrDefault<Replay>(info, "PracticeMapReplay", null);
			AssistsSettings = GetValueOrDefault(info, "AssistsSettings", new AssistsSettings());

			EnsureValid();
		}

		/// <summary>
		/// Writes saved game data into the serialization store.
		/// </summary>
		/// <param name="info">Serialization target.</param>
		/// <param name="context">Serialization context.</param>
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("Levels", Levels, typeof(List<LevelData>));
			info.AddValue("PracticeMapTime", PracticeMapTime);
			info.AddValue("PracticeMapSplits", PracticeMapSplits, typeof(float[]));
			info.AddValue("PracticeMapReplay", PracticeMapReplay, typeof(Replay));
			info.AddValue("AssistsSettings", AssistsSettings, typeof(AssistsSettings));
		}

		/// <summary>
		/// Ensures loaded game data is usable even if it came from an older save format.
		/// </summary>
		public void EnsureValid()
		{
			if (Levels == null)
				Levels = new List<LevelData>();

			for (int i = Levels.Count - 1; i >= 0; i--)
			{
				if (Levels[i] == null)
				{
					Levels.RemoveAt(i);
					continue;
				}

				Levels[i].EnsureValid();
			}

			if (PracticeMapSplits == null)
				PracticeMapSplits = new float[30];

			if (AssistsSettings == null)
				AssistsSettings = new AssistsSettings();
		}

		/// <summary>
		/// Gets a saved checkpoint split for a custom level.
		/// </summary>
		/// <param name="map">Level map whose stored split should be read.</param>
		/// <param name="index">Split index.</param>
		/// <returns>Saved split time, or zero if the map or split is unavailable.</returns>
		public float GetCheckpointSplit(LevelMap map, int index)
		{
			int levelIndex = Levels.FindIndex(ld => ld.LevelMap == map);

			if (levelIndex < 0)
			{
				Debug.LogError("[GameData] Cannot get checkpoint split; level not found.");
				return 0f;
			}

			float[] splits = Levels[levelIndex].CheckpointTimeSplits;

			if (splits == null)
			{
				Debug.LogError("[GameData] Cannot get checkpoint split; splits are missing.");
				return 0f;
			}

			if (index < 0 || index >= splits.Length)
			{
				Debug.LogError("[GameData] Checkpoint split index out of bounds.");
				return 0f;
			}

			return splits[index];
		}

		/// <summary>
		/// Gets a saved checkpoint split for the practice/test map.
		/// </summary>
		/// <param name="index">Split index.</param>
		/// <returns>Saved practice split time, or zero if the split is unavailable.</returns>
		public float GetTestLevelCheckpointSplit(int index)
		{
			if (PracticeMapSplits == null)
			{
				Debug.LogError("[GameData] Cannot get checkpoint split; practice splits are missing.");
				return 0f;
			}

			if (index < 0 || index >= PracticeMapSplits.Length)
			{
				Debug.LogError("[GameData] Checkpoint split index out of bounds.");
				return 0f;
			}

			return PracticeMapSplits[index];
		}

		/// <summary>
		/// Adds or improves a custom level time.
		/// </summary>
		/// <param name="map">Target level map.</param>
		/// <param name="time">New completion time in seconds.</param>
		/// <param name="splits">Checkpoint splits for the completed run.</param>
		/// <param name="replay">Replay for the completed run.</param>
		/// <remarks>
		/// Existing records are replaced only when <paramref name="time"/> is lower than the stored best time.
		/// </remarks>
		public void UpdateLevelTime(LevelMap map, float time, float[] splits, Replay replay)
		{
			int index = Levels.FindIndex(ld => ld.LevelMap == map);

			if (index >= 0)
			{
				if (time < Levels[index].Time)
				{
					Levels[index].Time = time;
					Levels[index].CheckpointTimeSplits = splits;
					Levels[index].BestReplay = replay;
					Levels[index].EnsureValid();
				}
			}
			else
			{
				Levels.Add(new LevelData(map, time, splits, replay));
			}
		}

		/// <summary>
		/// Updates the practice/test-map best time if the new time is better.
		/// </summary>
		/// <param name="time">New completion time in seconds.</param>
		/// <param name="splits">Checkpoint splits for the completed run.</param>
		/// <param name="replay">Replay for the completed run.</param>
		public void UpdateTestLevelTime(float time, float[] splits, Replay replay)
		{
			if (time < PracticeMapTime)
			{
				PracticeMapSplits = splits ?? new float[30];
				PracticeMapTime = time;
				PracticeMapReplay = replay;
			}
		}

		/// <summary>
		/// Gets the best saved time for a custom level.
		/// </summary>
		/// <param name="map">Level map to look up.</param>
		/// <returns>Best saved time, or <see cref="float.MaxValue"/> if the level is not stored.</returns>
		public float GetBestLevelTime(LevelMap map)
		{
			int index = Levels.FindIndex(ld => ld.LevelMap == map);

			if (index >= 0)
				return Levels[index].Time;

			return float.MaxValue;
		}

		/// <summary>
		/// Gets the best saved practice/test-map time.
		/// </summary>
		/// <returns>Best saved practice/test-map time.</returns>
		public float GetBestTestLevelTime()
		{
			return PracticeMapTime;
		}

		/// <summary>
		/// Gets the best replay for a custom level.
		/// </summary>
		/// <param name="map">Level map to look up.</param>
		/// <returns>Best replay if one exists and contains snapshots; otherwise <c>null</c>.</returns>
		public Replay GetBestReplay(LevelMap map)
		{
			int index = Levels.FindIndex(ld => ld.LevelMap == map);

			if (index < 0)
				return null;

			Replay replay = Levels[index].BestReplay;

			if (replay != null && replay.Duration > 0f && replay.Snapshots != null && replay.Snapshots.Count > 0)
				return replay;

			return null;
		}

		/// <summary>
		/// Gets the best replay for the practice/test map.
		/// </summary>
		/// <returns>Best practice replay if one exists and contains snapshots; otherwise <c>null</c>.</returns>
		public Replay GetBestTestLevelReplay()
		{
			if (PracticeMapReplay != null && PracticeMapReplay.Duration > 0f && PracticeMapReplay.Snapshots != null && PracticeMapReplay.Snapshots.Count > 0)
				return PracticeMapReplay;

			return null;
		}

		/// <summary>
		/// Checks whether the level exists in the data set.
		/// </summary>
		/// <param name="map">Level map to check.</param>
		/// <returns><c>true</c> if the level is stored; otherwise <c>false</c>.</returns>
		public bool ContainsLevel(LevelMap map) => Levels.Exists(ld => ld.LevelMap == map);

		/// <summary>
		/// Reads an optional serialized value.
		/// </summary>
		/// <typeparam name="T">Expected value type.</typeparam>
		/// <param name="info">Serialized data source.</param>
		/// <param name="name">Serialized entry name.</param>
		/// <param name="fallback">Fallback value used when the entry is missing.</param>
		/// <returns>The stored value when present; otherwise <paramref name="fallback"/>.</returns>
		/// <remarks>
		/// This is used for backwards compatibility with older save data.
		/// </remarks>
		private static T GetValueOrDefault<T>(SerializationInfo info, string name, T fallback)
		{
			foreach (SerializationEntry entry in info)
				if (entry.Name == name)
					return (T)entry.Value;

			return fallback;
		}
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Active saved game-data set.
	/// </summary>
	public GameData CurrentGameData { get; private set; } = new GameData();

	/// <summary>
	/// Currently selected level to be played.
	/// </summary>
	/// <remarks>
	/// A <c>null</c> value means the practice/test map is selected.
	/// </remarks>
	public LevelMap CurrentLevelMap { get; private set; } = null;

	/// <summary>
	/// Best replay for the currently selected map.
	/// </summary>
	/// <remarks>
	/// If no custom level is selected, this returns the practice/test-map replay.
	/// </remarks>
	public Replay CurrentLevelReplay => GetCurrentMapReplay();

	/// <summary>
	/// Gets whether ABS assist is enabled in saved settings.
	/// </summary>
	/// <returns><c>true</c> if ABS is enabled; otherwise <c>false</c>.</returns>
	public bool GetABS()
	{
		return CurrentGameData.AssistsSettings.ABS;
	}

	/// <summary>
	/// Sets the saved ABS assist state and persists game data.
	/// </summary>
	/// <param name="enabled">Whether ABS should be enabled.</param>
	public void SetABS(bool enabled)
	{
		CurrentGameData.AssistsSettings.ABS = enabled;
		SaveGameData();
	}

	/// <summary>
	/// Gets whether traction control assist is enabled in saved settings.
	/// </summary>
	/// <returns><c>true</c> if traction control is enabled; otherwise <c>false</c>.</returns>
	public bool GetTC()
	{
		return CurrentGameData.AssistsSettings.TC;
	}

	/// <summary>
	/// Sets the saved traction control assist state and persists game data.
	/// </summary>
	/// <param name="enabled">Whether traction control should be enabled.</param>
	public void SetTC(bool enabled)
	{
		CurrentGameData.AssistsSettings.TC = enabled;
		SaveGameData();
	}

	/// <summary>
	/// Hash of the current level-list content.
	/// </summary>
	/// <remarks>
	/// Used for change detection by level-list UI and other listeners.
	/// </remarks>
	public int Hash { get; private set; }

	#endregion

	#region Observers

	/// <summary>
	/// Registered listeners notified whenever the game-data content hash changes.
	/// </summary>
	private readonly List<Action> onGameDataChanged = new List<Action>();

	/// <summary>
	/// Subscribes a listener to game-data hash-change notifications.
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
	/// Loads saved game data from PlayerPrefs if present.
	/// </summary>
	protected override void Awake()
	{
		base.Awake();

		if (PlayerPrefs.HasKey("GameData"))
		{
			LoadGameData();
		}
		else
		{
			CurrentGameData.EnsureValid();
			Hash = CurrentGameData.Levels.GetContentHash();
		}
	}

	/// <summary>
	/// Saves game data to PlayerPrefs when the application quits.
	/// </summary>
	private void OnApplicationQuit()
	{
		SaveGameData();
	}

	#endregion

	#region Persistence

	/// <summary>
	/// Serializes <see cref="CurrentGameData"/> to JSON and writes it to PlayerPrefs.
	/// </summary>
	/// <remarks>
	/// Data is stored under the PlayerPrefs key <c>GameData</c>.
	/// </remarks>
	private void SaveGameData()
	{
		try
		{
			CurrentGameData.EnsureValid();

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
	/// Loads game data JSON from PlayerPrefs.
	/// </summary>
	/// <remarks>
	/// Data is loaded from the PlayerPrefs key <c>GameData</c>. The content hash is recomputed after loading.
	/// If loading fails, a new empty <see cref="GameData"/> instance is created.
	/// </remarks>
	private void LoadGameData()
	{
		try
		{
			string json = PlayerPrefs.GetString("GameData");
			CurrentGameData = JsonUtility.FromJson<GameData>(json) ?? new GameData();
			CurrentGameData.EnsureValid();

			Hash = CurrentGameData.Levels.GetContentHash();

			Debug.Log($"[GameDataManager] Loaded {CurrentGameData.Levels.Count} level(s).");

			for (int i = 0; i < CurrentGameData.Levels.Count; i++)
			{
				LevelData entry = CurrentGameData.Levels[i];

				if (entry == null || entry.LevelMap == null)
					Debug.LogWarning($"[GameDataManager] Level map at index {i} is null.");
				else
					Debug.Log($"[GameDataManager] Level {i}: Best {entry.Time:0.###} s");
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"[GameDataManager] Failed to load game data: {e.Message}");
			CurrentGameData = new GameData();
			CurrentGameData.EnsureValid();
			Hash = 0;
		}
	}

	#endregion

	#region Selection & Flow

	/// <summary>
	/// Attempts to start gameplay by loading the level scene.
	/// </summary>
	/// <remarks>
	/// Requires <see cref="CurrentLevelMap"/> to be set by <see cref="SelectingLevelMap(LevelMap)"/>.
	/// </remarks>
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
	/// Gets the saved checkpoint split for the currently selected map.
	/// </summary>
	/// <param name="splitIndex">Checkpoint split index.</param>
	/// <returns>
	/// Saved split time for the selected custom level, or the practice/test-map split if no custom level is selected.
	/// </returns>
	public float GetCurrentMapSplit(int splitIndex)
	{
		if (CurrentLevelMap == null)
		{
			Debug.Log("[GameDataManager] No level selected, assuming Test Track.");
			return CurrentGameData.GetTestLevelCheckpointSplit(splitIndex);
		}

		return CurrentGameData.GetCheckpointSplit(CurrentLevelMap, splitIndex);
	}

	/// <summary>
	/// Gets the best replay for the currently selected map.
	/// </summary>
	/// <returns>
	/// Best replay for the selected custom level, or the practice/test-map replay if no custom level is selected.
	/// </returns>
	public Replay GetCurrentMapReplay()
	{
		if (CurrentLevelMap == null)
			return CurrentGameData.GetBestTestLevelReplay();

		return CurrentGameData.GetBestReplay(CurrentLevelMap);
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

	/// <summary>
	/// Clears the currently selected level map.
	/// </summary>
	/// <remarks>
	/// This removes the current level selection by setting <see cref="CurrentLevelMap"/>
	/// to <c>null</c>. It is useful when leaving generated-track flow or starting a
	/// scene-defined race where no saved level map should be active.
	/// </remarks>
	public void UnselectLevelMap()
	{
		CurrentLevelMap = null;
	}

	#endregion

	#region Progression

	/// <summary>
	/// Records completion of the currently selected level and updates the best time if improved.
	/// </summary>
	/// <param name="time">Completion time in seconds.</param>
	/// <param name="checkpointSplits">Checkpoint splits for the completed run.</param>
	/// <param name="replay">Replay for the completed run.</param>
	/// <remarks>
	/// If no custom level is selected, the completion is stored as a practice/test-map result.
	/// The data set is saved after the result is recorded.
	/// </remarks>
	public void CompleteLevel(float time, float[] checkpointSplits, Replay replay)
	{
		if (CurrentLevelMap == null)
		{
			Debug.Log("[GameDataManager] No level selected, assuming Test Track.");
			CurrentGameData.UpdateTestLevelTime(time, checkpointSplits, replay);
			SaveGameData();
			return;
		}

		CurrentGameData.UpdateLevelTime(CurrentLevelMap, time, checkpointSplits, replay);
		PotentialHashChange(CurrentGameData.Levels.GetContentHash());
		SaveGameData();
	}

	/// <summary>
	/// Adds a level to the data set if it does not already exist.
	/// </summary>
	/// <param name="map">Level to add.</param>
	/// <remarks>
	/// Triggers hash-change detection and saves game data when a level is added.
	/// </remarks>
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
			SaveGameData();
		}
		else
		{
			Debug.LogWarning("[GameDataManager] Level already exists; not adding duplicate.");
		}
	}

	/// <summary>
	/// Removes a level from the data set if present.
	/// </summary>
	/// <param name="levelMap">Level to remove.</param>
	/// <remarks>
	/// Triggers hash-change detection and saves game data when a level is removed.
	/// </remarks>
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

				if (CurrentLevelMap == levelMap)
					CurrentLevelMap = null;

				PotentialHashChange(CurrentGameData.Levels.GetContentHash());
				SaveGameData();

				return;
			}
		}

		Debug.LogWarning("[GameDataManager] Level not found; nothing removed.");
	}

	/// <summary>
	/// Clears all custom levels from the data set.
	/// </summary>
	/// <remarks>
	/// Also clears the current custom-level selection, triggers hash-change detection, and saves game data.
	/// </remarks>
	public void ClearLevels()
	{
		CurrentGameData.Levels.Clear();
		CurrentLevelMap = null;

		PotentialHashChange(CurrentGameData.Levels.GetContentHash());
		SaveGameData();
	}

	#endregion

	#region Level Editing

	/// <summary>
	/// Creates an editable copy of a stored level map.
	/// </summary>
	/// <param name="sourceMap">Stored level map to copy for editing.</param>
	/// <returns>Editable copy of the level map, or <c>null</c> if the source map is invalid or not stored.</returns>
	public LevelMap CreateEditableCopy(LevelMap sourceMap)
	{
		if (sourceMap == null)
		{
			Debug.LogError("[GameDataManager] CreateEditableCopy called with null map.");
			return null;
		}

		int index = CurrentGameData.Levels.FindIndex(ld => ld.LevelMap == sourceMap);

		if (index < 0)
		{
			Debug.LogWarning("[GameDataManager] Cannot edit level; map is not present in CurrentGameData.");
			return null;
		}

		return CurrentGameData.Levels[index].LevelMap.Copy();
	}

	/// <summary>
	/// Replaces a stored level with an edited version.
	/// </summary>
	/// <param name="originalMap">Original stored level map to replace.</param>
	/// <param name="editedMap">Edited level map to save in its place.</param>
	/// <returns><c>true</c> if the stored level was replaced; otherwise <c>false</c>.</returns>
	/// <remarks>
	/// Replacing a level resets its associated best time, splits, and replay by creating a new
	/// <see cref="LevelData"/> record for the edited map.
	/// </remarks>
	public bool ReplaceLevel(LevelMap originalMap, LevelMap editedMap)
	{
		if (originalMap == null)
		{
			Debug.LogError("[GameDataManager] ReplaceLevel called with null original map.");
			return false;
		}

		if (editedMap == null)
		{
			Debug.LogError("[GameDataManager] ReplaceLevel called with null edited map.");
			return false;
		}

		int index = CurrentGameData.Levels.FindIndex(ld => ld.LevelMap == originalMap);

		if (index < 0)
		{
			Debug.LogWarning("[GameDataManager] Cannot replace level; original map was not found.");
			return false;
		}

		LevelMap savedMap = editedMap.Copy();

		CurrentGameData.Levels[index] = new LevelData(savedMap);

		if (CurrentLevelMap == originalMap)
			CurrentLevelMap = savedMap;

		PotentialHashChange(CurrentGameData.Levels.GetContentHash());
		SaveGameData();

		return true;
	}

	#endregion

	#region Internals

	/// <summary>
	/// Updates the stored hash and notifies listeners when the content hash changes.
	/// </summary>
	/// <param name="hash">Newly computed content hash.</param>
	private void PotentialHashChange(int hash)
	{
		if (hash == Hash)
			return;

		Hash = hash;

		Action[] listeners = onGameDataChanged.ToArray();

		for (int i = 0; i < listeners.Length; i++)
		{
			try
			{
				listeners[i]?.Invoke();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}
	}

	#endregion

	#region Context Menu

	/// <summary>
	/// Copies the raw saved game-data JSON from PlayerPrefs to the system clipboard.
	/// </summary>
	[ContextMenu("Copy Saved GameData To Clipboard")]
	public void CopySavedGameDataToClipboard()
	{
		string json = PlayerPrefs.GetString("GameData", "");

		if (string.IsNullOrEmpty(json))
		{
			Debug.LogWarning("[GameDataManager] No saved GameData found.");
			return;
		}

		GUIUtility.systemCopyBuffer = json;
		Debug.Log("[GameDataManager] Saved GameData copied to clipboard.");
	}

	/// <summary>
	/// Deletes the saved game-data JSON from PlayerPrefs.
	/// </summary>
	[ContextMenu("Delete Saved GameData")]
	public void DeleteSavedGameData()
	{
		if (!PlayerPrefs.HasKey("GameData"))
		{
			Debug.LogWarning("[GameDataManager] No saved GameData found to delete.");
			return;
		}

		PlayerPrefs.DeleteKey("GameData");
		PlayerPrefs.Save();

		Debug.Log("[GameDataManager] Saved GameData deleted.");
	}

	#endregion Context Menu
}