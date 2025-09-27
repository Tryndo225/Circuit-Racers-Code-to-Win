/**
 * @file Docs_GameData.cs
 * @brief Documentation entry for the Game Data subsystem.
 *
 * @defgroup game_data_mgr Game Data Manager
 * @ingroup systems
 * @brief Persistent progression (levels list, selected level, best times) managed by ::GameDataManager.
 *
 * @details
 * The Game Data subsystem centers on ::GameDataManager (a Generic::Singleton<GameDataManager>).
 * It owns a ::GameData instance that stores a list of ::LevelData records
 * (each linking a ::LevelMap and its best completion time). Data is persisted as JSON via
 * Unity's PlayerPrefs under the "GameData" key.
 *
 * Contents:
 * - see gdm_overview
 * - see gdm_data_model
 * - see gdm_lifecycle
 * - see gdm_usage
 * - see gdm_api
 * - see gdm_integration
 * - see gdm_persistence
 * - see gdm_performance
 * - see gdm_troubleshooting
 * - see gdm_versions
 *
 * ----------------------------------------------------------------------
 * @section gdm_overview Overview
 *
 * Responsibilities:
 * - Load and save ::GameData to PlayerPrefs (JSON).
 * - Track the currently selected ::LevelMap.
 * - Add, remove, clear levels; update best times.
 * - Maintain a content hash (Hash) of the Levels list and notify listeners on change.
 *
 * Dependencies:
 * - Generic::Singleton<T> base type.
 * - IEnumerableExtention::IEnumerableExtensions::GetContentHash for hash computation.
 * - UnityEngine.PlayerPrefs and JsonUtility for persistence.
 * - ::SceneManagement for scene changes (e.g., "Level" scene).
 *
 * Threading:
 * - Unity main thread.
 *
 * Invariants:
 * - Exactly one ::GameDataManager exists (Singleton).
 * - Best times are only improved (never made worse).
 *
 * ----------------------------------------------------------------------
 * @section gdm_data_model Data Model
 *
 * Types:
 * - ::LevelData: { LevelMap, Time } where Time is best completion in seconds
 *   (float.MaxValue indicates unknown).
 * - ::GameData: { List<LevelData> Levels }.
 * - ::GameDataManager: owns the active ::GameData and selected ::LevelMap.
 *
 * Equality:
 * - LevelData equality compares by LevelMap reference; Time does not affect equality.
 *
 * Hashing:
 * - GameDataManager::Hash is computed over Levels using GetContentHash() to detect changes.
 *
 * ----------------------------------------------------------------------
 * @section gdm_lifecycle Lifecycle
 *
 * Start():
 * - If PlayerPrefs has "GameData", loads it; otherwise starts with an empty ::GameData.
 *
 * OnApplicationQuit():
 * - Saves ::GameData to PlayerPrefs.
 *
 * Data change:
 * - Any structural change to Levels triggers a recomputation of Hash and invokes observers.
 *
 * ----------------------------------------------------------------------
 * @section gdm_usage Usage
 *
 * Quick start: load, select, play
 * @code{.cs}
 * // Load is automatic in Start if PlayerPrefs has "GameData".
 * // To select a level that already exists in data:
 * GameDataManager.Instance.SelectingLevelMap(levelMap);
 * GameDataManager.Instance.GoToSelectedLevel(); // loads "Level" scene
 * @endcode
 *
 * Add a level and record a best time
 * @code{.cs}
 * var mgr = GameDataManager.Instance;
 * mgr.AddLevel(levelMap);           // adds with Time = float.MaxValue if new
 * mgr.SelectingLevelMap(levelMap);  // select for play
 * // ... after finishing the level:
 * mgr.CompleteLevel(elapsedSeconds); // improves stored best time if better
 * @endcode
 *
 * Observe changes
 * @code{.cs}
 * void OnGameDataChanged() { Debug.Log("Data changed! Hash = " + GameDataManager.Instance.Hash); }
 * void OnEnable() { GameDataManager.Instance.AddListener(OnGameDataChanged); }
 * void OnDisable(){ GameDataManager.Instance.RemoveListener(OnGameDataChanged); }
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section gdm_api Public API Reference
 *
 * Properties:
 * - GameData CurrentGameData: active data set (Levels list).
 * - LevelMap CurrentLevelMap: currently selected level (may be null).
 * - int Hash: content hash of CurrentGameData.Levels.
 *
 * Selection and flow:
 * - void SelectingLevelMap(LevelMap map): selects an existing level by reference.
 * - void GoToSelectedLevel(): loads the "Level" scene if a level is selected.
 *
 * Progression updates:
 * - void CompleteLevel(float time): for CurrentLevelMap, improves best time if lower and saves.
 * - void AddLevel(LevelMap map): adds a new level if not present; triggers hash change.
 * - void RemoveLevel(LevelMap map): removes if present; triggers hash change.
 * - void ClearLevels(): clears Levels; triggers hash change.
 *
 * Observers:
 * - void AddListener(Action action): registers a hash-change callback.
 * - void RemoveListener(Action action): unregisters the callback.
 *
 * Persistence (internal):
 * - Saves to PlayerPrefs key "GameData" on quit and after improvements.
 *
 * ----------------------------------------------------------------------
 * @section gdm_integration Integration Notes
 *
 * Scene flow:
 * - Pair with ::SceneManagement to swap scenes after selection (GoToSelectedLevel uses "Level").
 *
 * UI:
 * - Build menus that list GameData.Levels; use SelectingLevelMap and GoToSelectedLevel.
 *
 * Content building:
 * - Ensure LevelMap references remain valid between sessions (e.g., do not destroy and recreate).
 *
 * ----------------------------------------------------------------------
 * @section gdm_persistence Persistence and Format
 *
 * Storage:
 * - PlayerPrefs string under key "GameData".
 *
 * Format:
 * - JsonUtility serialization of ::GameData containing a List<LevelData>.
 * - LevelMap fields must be serializable or reference-stable across sessions.
 *
 * Versioning:
 * - If the schema changes, consider a migration step before JsonUtility.FromJson().
 *
 * ----------------------------------------------------------------------
 * @section gdm_performance Performance and GC
 *
 * - PlayerPrefs writes are small and infrequent (on quit and when best times improve).
 * - GetContentHash iterates Levels; cost is linear in number of entries.
 * - Avoid excessive Save operations inside tight loops.
 *
 * ----------------------------------------------------------------------
 * @section gdm_troubleshooting Troubleshooting
 *
 * - Data not loading:
 *   - Ensure PlayerPrefs has "GameData". Check platform-specific PlayerPrefs persistence rules.
 * - Level selection fails:
 *   - SelectingLevelMap requires the map to exist in CurrentGameData; call AddLevel first.
 * - Hash not changing:
 *   - Only structural changes to Levels (add/remove/clear) affect the hash. Updating a time may not
 *     change the structure but should still be followed by persistence.
 *
 * ----------------------------------------------------------------------
 * @section gdm_versions Version History
 *
 * - v1.1: Added hash-based change notifications and observer list.
 * - v1.0: Basic load/save, level selection, add/remove/clear, best time updates.
 */
