/**
 * @file Docs_TrackManager.cs
 * @brief Documentation entry for the Track Management subsystem.
 *
 * @defgroup track_mng Track Manager
 * @ingroup systems
 * @brief Race flow control (start/restart, laps, checkpoints, respawn) and player car spawning.
 *
 * @details
 * The Track Manager is implemented by ::TrackManager (a MonoBehaviour placed in the race scene).
 * It owns race lifecycle (start/restart/finish), lap counting (for circuit mode), checkpoint
 * progression, respawn logic with a short freeze/teleport, and player car instantiation +
 * camera target assignment.
 *
 * It also exposes timing properties (current lap time, last lap time, total time) and forwards
 * completion to ::GameDataManager for persistence.
 *
 * Contents:
 * - see track_mng_overview
 * - see track_mng_inspector
 * - see track_mng_lifecycle
 * - see track_mng_checkpoints
 * - see track_mng_respawn
 * - see track_mng_usage
 * - see track_mng_api
 * - see track_mng_integration
 * - see track_mng_troubleshooting
 * - see track_mng_versions
 *
 * ----------------------------------------------------------------------
 * @section track_mng_overview Overview
 *
 * Responsibilities:
 * - Spawning the player car at a designated Start/Finish transform (or self as fallback).
 * - Wiring the FollowCamera target to the spawned car and syncing it on respawns.
 * - Managing race flow for circuit or point-to-point tracks.
 * - Tracking checkpoint order and enabling only the next required checkpoint.
 * - Handling player-initiated respawn and full restart actions (Input System).
 * - Computing lap and total timers; finalizing results and notifying ::GameDataManager.
 *
 * Dependencies:
 * - UnityEngine.InputSystem (InputActionProperty).
 * - ::CheckPointListener components placed in the scene by the track builder.
 * - ::FollowCamera for target and SyncCamera() calls.
 * - ::GameDataManager for persistence and level completion submission.
 *
 * Threading:
 * - Unity main thread (MonoBehaviour lifecycle + coroutines).
 *
 * Invariants:
 * - Checkpoints are visited in the configured order; only the next checkpoint is active.
 * - In circuit mode, finishing occurs after the configured lap count with index 0 checkpoint as gate.
 * - Respawn places the car at the last valid checkpoint claim (position/rotation/velocities).
 *
 * ----------------------------------------------------------------------
 * @section track_mng_inspector Inspector (TrackManager)
 *
 * Fields:
 * - CheckPoints (List<CheckPointListener>): ordered checkpoint sensors making up the course.
 * - CarSpawn (Transform): preferred spawn transform; falls back to TrackManager transform if null.
 *
 * Input Settings:
 * - respawnLastCheckPoint (InputActionProperty): user input to respawn at previous checkpoint.
 * - restartRace (InputActionProperty): user input to restart the whole race.
 * - defaultBindings (bool): if true, default actions/bindings are created in OnValidate().
 *
 * Car Prefab Reference:
 * - carPrefab (GameObject): the player car prefab to instantiate.
 * - carSpawnVerticalOffset (float): vertical offset applied on spawn.
 * - carSpawnHorizontalOffset (float): forward offset from spawn rotation.
 *
 * Track Settings:
 * - isCircuit (bool): true for lap-based circuit; false for point-to-point.
 * - laps (int, ShowIf(isCircuit)): number of laps required to finish.
 *
 * Respawn Delay:
 * - respawnDelay (float, 0..5): freeze timer (real-time) before resuming after a respawn.
 *
 * Runtime Properties (read-only):
 * - LastLapTime (float): duration of the last completed lap (seconds).
 * - CurrentLapTime (float): duration since current lap start (seconds).
 * - TotalTrackTime (float): elapsed since race start or final total when finished.
 * - CurrentLap (int), TotalLaps (int): lap progress.
 * - CurrentCheckPointIndex (int), TotalCheckPoints (int): checkpoint progress.
 * - IsRaceFinished (bool): whether the race has ended.
 * - RespawnTimer (float): remaining freeze time during respawn/restart countdowns.
 *
 * ----------------------------------------------------------------------
 * @section track_mng_lifecycle Lifecycle
 *
 * OnValidate:
 * - Optionally creates default input actions/bindings (Backspace/Triangle/ButtonNorth for respawn; Delete/Start for restart).
 *
 * OnEnable:
 * - Enables actions; subscribes performed callbacks for respawn/restart.
 *
 * OnDisable:
 * - Unsubscribes callbacks; disables actions.
 *
 * Start:
 * - If checkpoints exist, calls StartRace() to wire listeners and enter ready state (with a restart countdown).
 *
 * FixedUpdate:
 * - Services pending respawn (kicks off RespawnDelayCoroutine).
 *
 * OnApplication Flow:
 * - Race can be restarted at any time via restart input (RestartCoroutine).
 *
 * ----------------------------------------------------------------------
 * @section track_mng_checkpoints Checkpoints & Laps
 *
 * - Each ::CheckPointListener toggles active state; only the next expected checkpoint is active.
 * - Upon trigger, TrackManager::CheckPointTaken() advances the index and manages lap logic.
 * - Circuit mode:
 *   * Checkpoint 0 acts as lap gate.
 *   * When _currentLap == laps on reaching gate, race is finished.
 *   * lapStartTime is updated at each gate pass; LastLapTime recorded.
 * - Point-to-point mode:
 *   * Finishes when the last checkpoint (Count-1) is taken.
 *
 * ----------------------------------------------------------------------
 * @section track_mng_respawn Respawn & Restart
 *
 * Respawn (player initiated):
 * - If finished: triggers restart countdown.
 * - Else if at first checkpoint:
 *   * Circuit and later laps: respawn to last checkpoint (Count-1).
 *   * Otherwise: full restart countdown.
 * - Else: respawn to previous checkpoint (index-1).
 *
 * Respawn mechanics:
 * - Temporarily sets Rigidbody interpolation/kinematic to ensure a clean teleport.
 * - Teleports car to saved pose & velocities captured by ::CheckPointListener.
 * - Syncs transforms and FollowCamera, then applies a short real-time freeze (respawnDelay).
 *
 * Restart:
 * - Destroys existing car (if any) and respawns near CarSpawn (with offsets).
 * - Wires FollowCamera target, resets times/lap/checkpoint counters, and reactivates the first checkpoint.
 * - Runs a fixed 3-second real-time countdown (timeScale = 0 during countdown).
 *
 * ----------------------------------------------------------------------
 * @section track_mng_usage Usage
 *
 * Quick start:
 * - Place TrackManager in the scene.
 * - Ensure CheckPointListener objects exist and are ordered in the CheckPoints list.
 * - Optionally assign CarSpawn; otherwise TrackManager transform is used.
 * - Assign carPrefab and FollowCamera in the scene.
 * - Press the restart key to begin the countdown and spawn the car.
 *
 * Custom inputs:
 * - Turn off defaultBindings and assign respawnLastCheckPoint / restartRace via your Input Actions asset.
 *
 * ----------------------------------------------------------------------
 * @section track_mng_api Public API Reference
 *
 * Race control:
 * - void StartRace(Vector3? carPosition = null): wires checkpoint listeners and starts the restart countdown.
 * - void CheckPointTaken(): advances checkpoint/lap, records times, finishes when appropriate.
 * - void GoToSelectedLevel(): (via GameDataManager) loads the gameplay scene for the selected level.
 *
 * Respawn/restart:
 * - void Respawn(): schedules a respawn to the previous checkpoint per race state rules.
 * - IEnumerator RespawnDelayCoroutine(float delay): performs teleport + freeze, then resumes.
 * - IEnumerator RestartCoroutine(): restarts race with a 3-second countdown pause.
 *
 * Level management helpers:
 * - (Internal) Add/Remove listeners on ::CheckPointListener; ResetCheckPoints() re-arms the first checkpoint.
 *
 * Timing properties:
 * - float LastLapTime, CurrentLapTime, TotalTrackTime; bool IsRaceFinished.
 *
 * ----------------------------------------------------------------------
 * @section track_mng_integration Integration Notes
 *
 * - ::RaceTrackPlacer builds the course and populates TrackManager.CheckPoints and CarSpawn.
 * - ::CheckPointListener captures respawn pose/velocities; TrackManager consumes these during respawn.
 * - ::FollowCamera must exist and expose target + SyncCamera().
 * - ::GameDataManager.CompleteLevel(totalTime) is called on finish for persistence/best time tracking.
 *
 * ----------------------------------------------------------------------
 * @section track_mng_troubleshooting Troubleshooting
 *
 * - Car not spawning:
 *   * Ensure carPrefab is assigned; verify CarSpawn exists or TrackManager transform is sensible.
 * - Checkpoints not advancing:
 *   * Confirm CheckPoints list order; ensure only one is active and player GameObject is tagged "Player".
 * - Respawn puts car in the wrong place:
 *   * Verify each CheckPointListener is placed on the track and claims data when triggered.
 * - Timer not moving:
 *   * Race is paused during countdown/respawn (timeScale=0). Ensure countdown completes.
 *
 * ----------------------------------------------------------------------
 * @section track_mng_versions Version History
 *
 * - v1.1: Respawn freeze timer & countdown UI-ready timer, circuit/point-to-point flows unified.
 * - v1.0: Basic checkpoints, lap counting, restart/respawn, and timing.
 */
