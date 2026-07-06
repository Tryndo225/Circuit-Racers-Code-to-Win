using IEnumerableExtention;       // For GetContentHash()
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
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
	/// Serializable record containing a <see cref="LevelMap"/> reference and its best saved run data.
	/// </summary>
	[Serializable]
	public class LevelData : ISerializable
	{
		/// <summary>Associated level map.</summary>
		public LevelMap LevelMap;

		/// <summary>Best completion time in seconds. Use <see cref="float.MaxValue"/> for unknown.</summary>
		public float Time = float.MaxValue;

		/// <summary>Best checkpoint split times for this level.</summary>
		public float[] CheckpointTimeSplits;

		/// <summary>Replay associated with the stored best time.</summary>
		public Replay BestReplay;

		/// <summary>
		/// Parameterless constructor for Unity serialization.
		/// </summary>
		public LevelData() { }

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
		/// <param name="checkpointTimeSplits">Optional array of checkpoint time splits; if null, initialized to default size.</param>
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
		public LevelData(SerializationInfo info, StreamingContext context)
		{
			LevelMap = GetValueOrDefault<LevelMap>(info, "LevelMap", null);
			Time = GetValueOrDefault(info, "Time", float.MaxValue);
			CheckpointTimeSplits = GetValueOrDefault<float[]>(info, "CheckpointTimeSplits", null);
			BestReplay = GetValueOrDefault<Replay>(info, "BestReplay", null);

			EnsureValid();
		}

		/// <inheritdoc />
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("LevelMap", LevelMap, typeof(LevelMap));
			info.AddValue("Time", Time);
			info.AddValue("CheckpointTimeSplits", CheckpointTimeSplits, typeof(float[]));
			info.AddValue("BestReplay", BestReplay, typeof(Replay));
		}

		/// <summary>
		/// Ensures loaded data is usable even if it came from an older save.
		/// </summary>
		public void EnsureValid()
		{
			if (CheckpointTimeSplits == null)
				CheckpointTimeSplits = CreateDefaultSplits(LevelMap);
		}

		/// <summary>
		/// Equality compares saved run data by value.
		/// </summary>
		public static bool operator ==(LevelData a, LevelData b)
		{
			if (ReferenceEquals(a, b))
				return true;

			if (a is null || b is null)
				return false;

			return a.LevelMap == b.LevelMap && a.Time == b.Time && FloatArrayEquals(a.CheckpointTimeSplits, b.CheckpointTimeSplits) && EqualityComparer<Replay>.Default.Equals(a.BestReplay, b.BestReplay);
		}

		/// <inheritdoc cref="operator =="/>
		public static bool operator !=(LevelData a, LevelData b)
		{
			return !(a == b);
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return obj is LevelData other && this == other;
		}

		/// <inheritdoc />
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
		/// <param name="levelMap">Level map whose checkpoint/lap count should be used.</param>
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
		/// Reads an optional serialized value. Used for backwards compatibility with older save data.
		/// </summary>
		private static T GetValueOrDefault<T>(SerializationInfo info, string name, T fallback)
		{
			foreach (SerializationEntry entry in info)
				if (entry.Name == name)
					return (T)entry.Value;

			return fallback;
		}
	}

	[Serializable]
	public class AssistsSettings : ISerializable
	{
		public bool ABS;
		public bool TC;

		public AssistsSettings()
		{
			ABS = false;
			TC = false;
		}

		protected AssistsSettings(SerializationInfo info, StreamingContext context)
		{
			ABS = info.GetBoolean(nameof(ABS));
			TC = info.GetBoolean(nameof(TC));
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue(nameof(ABS), ABS);
			info.AddValue(nameof(TC), TC);
		}
	}

	/// <summary>
	/// Serializable container for all saved progression data.
	/// </summary>
	[Serializable]
	public class GameData : ISerializable
	{
		/// <summary>List of known levels and their best times.</summary>
		public List<LevelData> Levels;

		/// <summary>Best practice/test-map time. Uses <see cref="float.MaxValue"/> when unknown.</summary>
		public float PracticeMapTime = float.MaxValue;

		/// <summary>Best practice/test-map checkpoint splits.</summary>
		public float[] PracticeMapSplits = new float[30];

		/// <summary>Best practice/test-map replay.</summary>
		public Replay PracticeMapReplay;

		public AssistsSettings AssistsSettings;

		/// <summary>Creates an empty game data set.</summary>
		public GameData()
		{
			Levels = new List<LevelData>();
			AssistsSettings = new AssistsSettings();
		}

		/// <summary>
		/// Deserialization constructor used by serializers that rely on <see cref="ISerializable"/>.
		/// </summary>
		public GameData(SerializationInfo info, StreamingContext context)
		{
			Levels = GetValueOrDefault(info, "Levels", new List<LevelData>());
			PracticeMapTime = GetValueOrDefault(info, "PracticeMapTime", float.MaxValue);
			PracticeMapSplits = GetValueOrDefault(info, "PracticeMapSplits", new float[30]);
			PracticeMapReplay = GetValueOrDefault<Replay>(info, "PracticeMapReplay", null);
			AssistsSettings = GetValueOrDefault(info, "AssistsSettings", new AssistsSettings());

			EnsureValid();
		}

		/// <inheritdoc />
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("Levels", Levels, typeof(List<LevelData>));
			info.AddValue("PracticeMapTime", PracticeMapTime);
			info.AddValue("PracticeMapSplits", PracticeMapSplits, typeof(float[]));
			info.AddValue("PracticeMapReplay", PracticeMapReplay, typeof(Replay));
			info.AddValue("AssistsSettings", AssistsSettings, typeof(AssistsSettings));
		}

		/// <summary>
		/// Ensures loaded data is usable even if it came from an older save.
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
				PracticeMapSplits = new float[11];

			if (AssistsSettings == null)
				AssistsSettings = new AssistsSettings();
		}

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
		/// Adds or improves a level time; replaces only if <paramref name="time"/> is lower than the stored best.
		/// </summary>
		/// <param name="map">Target level map.</param>
		/// <param name="time">New completion time in seconds.</param>
		/// <param name="splits">Checkpoint splits for the completed run.</param>
		/// <param name="replay">Replay for the completed run.</param>
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
				PracticeMapSplits = splits ?? new float[11];
				PracticeMapTime = time;
				PracticeMapReplay = replay;
			}
		}

		public float GetBestLevelTime(LevelMap map)
		{
			int index = Levels.FindIndex(ld => ld.LevelMap == map);

			if (index >= 0)
				return Levels[index].Time;

			return float.MaxValue;
		}

		public float GetBestTestLevelTime()
		{
			return PracticeMapTime;
		}

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

		public Replay GetBestTestLevelReplay()
		{
			if (PracticeMapReplay != null && PracticeMapReplay.Duration > 0f && PracticeMapReplay.Snapshots != null && PracticeMapReplay.Snapshots.Count > 0)
				return PracticeMapReplay;

			return null;
		}

		/// <summary>
		/// Returns true if the level exists in the data set.
		/// </summary>
		/// <param name="map">Level map to check.</param>
		public bool ContainsLevel(LevelMap map) => Levels.Exists(ld => ld.LevelMap == map);

		/// <summary>
		/// Reads an optional serialized value. Used for backwards compatibility with older save data.
		/// </summary>
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
	/// The active data set (list of <see cref="LevelData"/> entries).
	/// </summary>
	public GameData CurrentGameData { get; private set; } = new GameData();

	/// <summary>
	/// The currently selected level to be played.
	/// Null means the practice/test map is selected.
	/// </summary>
	public LevelMap CurrentLevelMap { get; private set; } = null;

	/// <summary>
	/// Best replay for the current map.
	/// If no custom level is selected, this returns the practice/test-map replay.
	/// </summary>
	public Replay CurrentLevelReplay => GetCurrentMapReplay();

	public bool GetABS()
	{
		return CurrentGameData.AssistsSettings.ABS;
	}

	public void SetABS(bool enabled)
	{
		CurrentGameData.AssistsSettings.ABS = enabled;
		SaveGameData();
	}

	public bool GetTC()
	{
		return CurrentGameData.AssistsSettings.TC;
	}

	public void SetTC(bool enabled)
	{
		CurrentGameData.AssistsSettings.TC = enabled;
		SaveGameData();
	}

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
	/// Unity method: loads saved game data from PlayerPrefs if present.
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
	/// Unity method: saves game data to PlayerPrefs on application quit.
	/// </summary>
	private void OnApplicationQuit()
	{
		SaveGameData();
	}

	#endregion

	#region Persistence

	/// <summary>
	/// Serializes <see cref="CurrentGameData"/> to JSON and writes it to PlayerPrefs ("GameData").
	/// </summary>
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
	/// Loads JSON from PlayerPrefs ("GameData") and deserializes into <see cref="CurrentGameData"/>.
	/// Recomputes <see cref="Hash"/> and prints debug info.
	/// </summary>
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

	public float GetCurrentMapSplit(int splitIndex)
	{
		if (CurrentLevelMap == null)
		{
			Debug.Log("[GameDataManager] No level selected, assuming Test Track.");
			return CurrentGameData.GetTestLevelCheckpointSplit(splitIndex);
		}

		return CurrentGameData.GetCheckpointSplit(CurrentLevelMap, splitIndex);
	}

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

	#endregion

	#region Progression

	/// <summary>
	/// Records completion of the currently selected level and updates best time if improved.
	/// Automatically saves the data set.
	/// </summary>
	/// <param name="time">Completion time in seconds.</param>
	/// <param name="checkpointSplits">Checkpoint splits for the completed run.</param>
	/// <param name="replay">Replay for the completed run.</param>
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
			SaveGameData();
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
	/// Clears all levels from the data set; triggers change detection.
	/// </summary>
	public void ClearLevels()
	{
		CurrentGameData.Levels.Clear();
		CurrentLevelMap = null;

		PotentialHashChange(CurrentGameData.Levels.GetContentHash());
		SaveGameData();
	}

	#endregion

	#region Level Editing

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
	/// Compares a new hash with the current <see cref="Hash"/> and, if different,
	/// updates <see cref="Hash"/> and notifies all listeners.
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