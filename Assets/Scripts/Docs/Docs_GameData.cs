/**
 * @file Docs_GameData.cs
 * @brief Documentation entry for the Game Data subsystem.
 *
 * @defgroup game_data Game Data
 * @ingroup systems
 * @brief Persistent progression data, selected level state, best times, checkpoint splits, replays, and assist settings.
 *
 * The Game Data subsystem centers on ::GameDataManager, which derives from Generic::Singleton<GameDataManager>.
 * It owns a ::GameData instance, persists it through Unity PlayerPrefs as GZip-compressed,
 * Base64-encoded JSON under the key "GameDataCompressed", and keeps backwards compatibility
 * with older raw JSON saves stored under the key "GameData". It also tracks the currently selected
 * ::LevelMap and exposes APIs for level lists, best times, checkpoint splits, best replays,
 * assist settings, and editable level replacement.
 *
 * Contents:
 * - @ref gdm_overview
 * - @ref gdm_data_model
 * - @ref gdm_lifecycle
 * - @ref gdm_usage
 * - @ref gdm_api
 * - @ref gdm_level_editing
 * - @ref gdm_integration
 * - @ref gdm_persistence
 * - @ref gdm_performance
 * - @ref gdm_troubleshooting
 * - @ref gdm_versions
 *
 * ----------------------------------------------------------------------
 * @section gdm_overview Overview
 *
 * Responsibilities:
 * - Load and save ::GameData to PlayerPrefs as compressed Base64-encoded JSON.
 * - Maintain the currently selected ::LevelMap for gameplay scene transitions.
 * - Store custom levels and their best completion data.
 * - Store practice/test-map best time, checkpoint splits, and replay.
 * - Store driving assist settings: ABS and traction control.
 * - Add, remove, clear, edit, and replace saved levels.
 * - Record best times only when a completed run improves the stored result.
 * - Store checkpoint split arrays for best-run comparison.
 * - Store best ::Replay objects for custom levels and the practice/test map.
 * - Compute a content hash of the level list and notify listeners when it changes.
 *
 * Dependencies:
 * - Generic::Singleton<T> base type.
 * - IEnumerableExtensions::IEnumerableExtensions::GetContentHash for level-list hash computation.
 * - UnityEngine.PlayerPrefs, JsonUtility, System.IO.Compression, and Base64 conversion for persistence.
 * - ::SceneManagement for loading the gameplay scene.
 * - ::LevelMap for level layout data.
 * - ::Replay for best-run replay storage.
 *
 * Threading:
 * - Unity main thread only.
 *
 * Invariants:
 * - Exactly one ::GameDataManager instance should be active.
 * - Custom level best times are only replaced when the new time is lower.
 * - Practice/test-map best time is only replaced when the new time is lower.
 * - A null CurrentLevelMap means the practice/test map is being used.
 * - Listener callbacks are invoked only when the stored level-list hash changes.
 *
 * ----------------------------------------------------------------------
 * @section gdm_data_model Data Model
 *
 * ::GameDataManager
 * - Owns the active ::GameData through CurrentGameData.
 * - Stores the currently selected custom level in CurrentLevelMap.
 * - Exposes CurrentLevelReplay for the selected custom level or the practice/test map.
 * - Exposes Hash for level-list change detection.
 *
 * ::GameDataManager::GameData
 * - Levels: List<GameDataManager::LevelData> containing custom levels and their best runs.
 * - PracticeMapTime: best practice/test-map time, or float.MaxValue when unknown.
 * - PracticeMapSplits: best practice/test-map checkpoint splits.
 * - PracticeMapReplay: best practice/test-map replay.
 * - AssistsSettings: saved ABS and traction-control settings.
 *
 * ::GameDataManager::LevelData
 * - LevelMap: associated custom level.
 * - Time: best completion time in seconds, or float.MaxValue when unknown.
 * - CheckpointTimeSplits: checkpoint split times for the stored best run.
 * - BestReplay: replay associated with the stored best run.
 *
 * ::GameDataManager::AssistsSettings
 * - ABS: whether anti-lock braking assist is enabled.
 * - TC: whether traction-control assist is enabled.
 *
 * Equality and hashing:
 * - LevelData equality compares LevelMap, Time, CheckpointTimeSplits, and BestReplay.
 * - LevelData.GetHashCode includes the level map, time, replay, and split values.
 * - GameDataManager::Hash is computed from CurrentGameData.Levels using GetContentHash().
 *
 * Validation and fallback:
 * - GameData.EnsureValid() removes null level records and repairs missing arrays/settings.
 * - LevelData.EnsureValid() creates default split arrays when missing.
 * - Default custom-level split count is based on CheckpointCountPerLap * Laps + 1.
 * - PracticeMapSplits defaults to an array of length 30.
 *
 * ----------------------------------------------------------------------
 * @section gdm_lifecycle Lifecycle
 *
 * GameDataManager.Awake:
 * - Enforces the singleton rule.
 * - If PlayerPrefs contains "GameDataCompressed" or the legacy "GameData" key, loads it.
 * - Otherwise initializes a fresh ::GameData instance and computes the initial Hash.
 *
 * GameDataManager.OnApplicationQuit:
 * - Saves CurrentGameData to PlayerPrefs using the compressed save format.
 *
 * SaveGameData:
 * - Calls CurrentGameData.EnsureValid().
 * - Serializes CurrentGameData with JsonUtility.ToJson().
 * - Compresses the JSON with GZip.
 * - Converts the compressed bytes to a Base64 string.
 * - Stores the compressed Base64 payload in PlayerPrefs under "GameDataCompressed".
 * - Removes the legacy raw-JSON "GameData" key after a successful compressed save.
 * - Calls PlayerPrefs.Save().
 *
 * LoadGameData:
 * - Reads the compressed Base64 payload from PlayerPrefs key "GameDataCompressed" when present.
 * - Decompresses it back into readable JSON before deserialization.
 * - Falls back to the legacy raw JSON key "GameData" when no compressed save exists.
 * - Deserializes the JSON with JsonUtility.FromJson<GameData>().
 * - Repairs missing or older-format data with EnsureValid().
 * - Recomputes Hash from CurrentGameData.Levels.
 * - Falls back to a new empty ::GameData if loading fails.
 *
 * Data change:
 * - Add, remove, clear, replace, and improved custom-level completion can recompute Hash.
 * - When the hash changes, registered listeners are invoked safely.
 *
 * ----------------------------------------------------------------------
 * @section gdm_usage Usage
 *
 * Select an existing level and start gameplay:
 * @code{.cs}
 * GameDataManager.Instance.SelectingLevelMap(levelMap);
 * GameDataManager.Instance.GoToSelectedLevel();
 * @endcode
 *
 * Add a new custom level:
 * @code{.cs}
 * GameDataManager.Instance.AddLevel(levelMap);
 * @endcode
 *
 * Complete the current level:
 * @code{.cs}
 * float elapsedSeconds = RaceTimeManager.Instance.GetCurrentRaceTime();
 * float[] splits = RaceTimeManager.Instance.CheckPointSplitsTimes.ToArray();
 * Replay replay = ReplayManager.Instance.SaveReplay();
 *
 * GameDataManager.Instance.CompleteLevel(elapsedSeconds, splits, replay);
 * @endcode
 *
 * Read the current best replay:
 * @code{.cs}
 * Replay replay = GameDataManager.Instance.CurrentLevelReplay;
 * @endcode
 *
 * Read a saved checkpoint split:
 * @code{.cs}
 * float referenceSplit = GameDataManager.Instance.GetCurrentMapSplit(splitIndex);
 * @endcode
 *
 * Read and update assists:
 * @code{.cs}
 * bool absEnabled = GameDataManager.Instance.GetABS();
 * bool tcEnabled = GameDataManager.Instance.GetTC();
 *
 * GameDataManager.Instance.SetABS(true);
 * GameDataManager.Instance.SetTC(true);
 * @endcode
 *
 * Observe level-list changes:
 * @code{.cs}
 * private void OnEnable()
 * {
 *     GameDataManager.Instance.AddListener(OnGameDataChanged);
 * }
 *
 * private void OnDisable()
 * {
 *     if (GameDataManager.Instance != null)
 *     {
 *         GameDataManager.Instance.RemoveListener(OnGameDataChanged);
 *     }
 * }
 *
 * private void OnGameDataChanged()
 * {
 *     Debug.Log("Game data changed. Hash = " + GameDataManager.Instance.Hash);
 * }
 * @endcode
 *
 * Create and save an edited level:
 * @code{.cs}
 * LevelMap original = GameDataManager.Instance.CurrentLevelMap;
 * LevelMap editable = GameDataManager.Instance.CreateEditableCopy(original);
 *
 * if (editable != null)
 * {
 *     editable.Name = "Edited Level";
 *     GameDataManager.Instance.ReplaceLevel(original, editable);
 * }
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section gdm_api Public API Reference
 *
 * Properties:
 * - GameData CurrentGameData:
 *   Active saved game-data set.
 *
 * - LevelMap CurrentLevelMap:
 *   Currently selected custom level. Null means the practice/test map is selected.
 *
 * - Replay CurrentLevelReplay:
 *   Best replay for the selected custom level, or the practice/test-map replay when no custom level is selected.
 *
 * - int Hash:
 *   Content hash of the current custom-level list.
 *
 * Assist settings:
 * - bool GetABS()
 *   Returns whether ABS assist is enabled.
 *
 * - void SetABS(bool enabled)
 *   Updates and saves the ABS assist setting.
 *
 * - bool GetTC()
 *   Returns whether traction control assist is enabled.
 *
 * - void SetTC(bool enabled)
 *   Updates and saves the traction-control assist setting.
 *
 * Observers:
 * - void AddListener(Action action)
 *   Registers a callback for level-list hash changes.
 *
 * - void RemoveListener(Action action)
 *   Removes a previously registered callback.
 *
 * Selection and flow:
 * - void SelectingLevelMap(LevelMap map)
 *   Selects a custom level for play. The map must already exist in CurrentGameData.
 *
 * - void GoToSelectedLevel()
 *   Loads the "Level" scene through ::SceneManagement when CurrentLevelMap is set.
 *
 * - float GetCurrentMapSplit(int splitIndex)
 *   Returns a saved split for the selected custom level, or the practice/test-map split if no custom level is selected.
 *
 * - Replay GetCurrentMapReplay()
 *   Returns the best replay for the selected custom level, or the practice/test-map replay if no custom level is selected.
 *
 * Progression:
 * - void CompleteLevel(float time, float[] checkpointSplits, Replay replay)
 *   Records completion of the currently selected level. If CurrentLevelMap is null, the result is stored as
 *   a practice/test-map result. Otherwise, the selected custom level is updated if the time improves.
 *
 * - void AddLevel(LevelMap map)
 *   Adds a custom level if it is not already present.
 *
 * - void RemoveLevel(LevelMap levelMap)
 *   Removes a custom level if present. Clears CurrentLevelMap if the removed level was selected.
 *
 * - void ClearLevels()
 *   Removes all custom levels and clears the current custom-level selection.
 *
 * Level editing:
 * - LevelMap CreateEditableCopy(LevelMap sourceMap)
 *   Returns a copy of a stored level map for editing, or null if the map is invalid or not stored.
 *
 * - bool ReplaceLevel(LevelMap originalMap, LevelMap editedMap)
 *   Replaces a stored level with an edited copy. This resets best time, splits, and replay for that level.
 *
 * Context menu helpers:
 * - void CopySavedGameDataToClipboard()
 *   Copies the readable decoded JSON to the clipboard, even when the saved PlayerPrefs value is compressed.
 *
 * - void DeleteSavedGameData()
 *   Deletes both the compressed save key and the legacy raw-JSON save key.
 *
 * - void PrintSavedGameDataSize()
 *   Prints the decoded JSON size and, when available, the compressed Base64 payload size.
 *
 * ----------------------------------------------------------------------
 * @section gdm_level_editing Level Editing
 *
 * The level editing flow avoids directly mutating the stored level until the user confirms the edit.
 *
 * Recommended flow:
 * - Call CreateEditableCopy(originalMap).
 * - Modify the returned copy in the editor UI.
 * - Validate the edited map.
 * - Call ReplaceLevel(originalMap, editedMap) to save the result.
 *
 * Replacement behavior:
 * - The edited map is copied before storage.
 * - The old LevelData record is replaced with a new LevelData record.
 * - Best time, checkpoint splits, and replay are reset for the edited level.
 * - If the original map was selected, CurrentLevelMap is moved to the saved edited copy.
 * - Hash change detection and saving are triggered.
 *
 * ----------------------------------------------------------------------
 * @section gdm_integration Integration Notes
 *
 * Race flow:
 * - ::RaceTimeManager calls ::GameDataManager::CompleteLevel with final time, checkpoint splits, and replay.
 * - ::GameDataManager stores custom-level records or practice/test-map records depending on CurrentLevelMap.
 *
 * Replay system:
 * - ::ReplayManager produces the replay passed into CompleteLevel.
 * - ::ReplayPreviewer can read CurrentLevelReplay or GetCurrentMapReplay() to preview the best run.
 *
 * Track building:
 * - ::RaceTrackPlacer reads GameDataManager::CurrentLevelMap when building custom tracks.
 * - A null CurrentLevelMap represents the practice/test-map flow.
 *
 * UI:
 * - Saved-level lists should read CurrentGameData.Levels.
 * - Level list UI can use AddListener and RemoveListener to rebuild when Hash changes.
 * - Level editor UI should use CreateEditableCopy and ReplaceLevel.
 *
 * Vehicle settings:
 * - Settings UI can use GetABS, SetABS, GetTC, and SetTC.
 * - VehicleController can read these settings when applying assist configuration.
 *
 * Scene flow:
 * - GoToSelectedLevel loads the "Level" scene through ::SceneManagement.
 * - SelectingLevelMap only succeeds for levels already present in CurrentGameData.
 *
 * Import/export:
 * - Imported levels should be added with AddLevel.
 * - Removing or replacing levels triggers hash-change notifications.
 *
 * ----------------------------------------------------------------------
 * @section gdm_persistence Persistence and Format
 *
 * Storage:
 * - Main PlayerPrefs key: "GameDataCompressed".
 * - Main value: Base64 text produced from GZip-compressed JsonUtility JSON.
 * - Legacy PlayerPrefs key: "GameData".
 * - Legacy value: raw JSON produced by JsonUtility.ToJson(CurrentGameData).
 * - The legacy key is kept only for backwards-compatible loading of older saves.
 *
 * Saved data includes:
 * - Custom level list.
 * - Best custom-level times.
 * - Best custom-level checkpoint split arrays.
 * - Best custom-level replays.
 * - Practice/test-map best time.
 * - Practice/test-map split array.
 * - Practice/test-map replay.
 * - ABS and traction-control settings.
 *
 * Loading:
 * - The compressed key is preferred when both save formats exist.
 * - The legacy raw-JSON key is used as a fallback when no compressed save exists.
 * - Missing older fields are repaired through EnsureValid().
 * - Missing split arrays are recreated.
 * - Missing assist settings are recreated.
 * - Null level records are removed.
 *
 * Limitations:
 * - PlayerPrefs is convenient for small saves, but not ideal for very large data.
 * - Compression reduces repeated JSON text but does not remove the cost of serializing and deserializing large objects.
 * - Stored replay data can still grow quickly if many snapshots are saved.
 * - Base64 is used only so compressed binary data can be stored as a PlayerPrefs string.
 * - JsonUtility has Unity serialization limitations and does not support every C# type equally.
 *
 * ----------------------------------------------------------------------
 * @section gdm_performance Performance and GC
 *
 * - Saving is done on quit and after meaningful data changes.
 * - GetContentHash iterates the Levels list and is linear in the number of saved levels.
 * - Listener invocation copies callbacks to an array before calling them, avoiding mutation issues during callbacks.
 * - Avoid saving repeatedly in tight loops.
 * - Large replay objects increase decoded JSON size and save/load cost, even though the stored PlayerPrefs payload is compressed.
 *
 * ----------------------------------------------------------------------
 * @section gdm_troubleshooting Troubleshooting
 *
 * Data not loading:
 * - Check that PlayerPrefs contains either the "GameDataCompressed" key or the legacy "GameData" key.
 * - Check the console for compression, Base64, JsonUtility, or EnsureValid errors.
 * - Check whether an older schema needs fallback handling.
 *
 * Selected level does not start:
 * - SelectingLevelMap requires the map to already exist in CurrentGameData.
 * - Call AddLevel before SelectingLevelMap for imported or newly generated custom levels.
 * - GoToSelectedLevel refuses to load if CurrentLevelMap is null.
 *
 * Practice/test-map result is being updated instead of a custom level:
 * - CurrentLevelMap is null.
 * - Select a custom level before starting the race.
 *
 * Best time does not update:
 * - New times only replace stored times when they are lower.
 * - Check whether the race is storing to the practice/test map or a custom level.
 *
 * Checkpoint split is zero:
 * - No split was stored at that index.
 * - The split index may be out of range.
 * - The level may not have a saved best run yet.
 *
 * Replay missing:
 * - A replay is returned only when it exists, has snapshots, and has duration greater than zero.
 * - Check that ReplayManager saved a replay before CompleteLevel was called.
 *
 * UI does not refresh:
 * - AddListener only fires when the level-list hash changes.
 * - Practice-map time changes or assist-setting changes may save data without changing the custom-level list hash.
 *
 * Level replacement loses best time:
 * - This is intentional. ReplaceLevel creates a fresh LevelData record for the edited map.
 *
 * ----------------------------------------------------------------------
 * @section gdm_versions Version History
 *
 * - v1.5: Added GZip-compressed Base64 PlayerPrefs storage with legacy raw-JSON loading support.
 * - v1.4: Added assist settings, best replays, checkpoint splits, editable copies, and level replacement.
 * - v1.3: Added practice/test-map best time and split storage.
 * - v1.2: Added hash-based change notifications and observer list.
 * - v1.1: Added selected-level scene flow and custom-level management.
 * - v1.0: Basic save/load, level list, and best time tracking.
 */