/**
 * @file Docs_ReplaySystem.cs
 * @brief Documentation entry for the Replay System subsystem.
 *
 * @defgroup replay_system Replay System
 * @ingroup systems
 * @brief Race replay recording, replay data storage, replay playback, replay camera support, and replay HUD integration.
 *
 * @details
 * The Replay System records vehicle and race state during a race, stores that data in serializable
 * replay objects, and later reconstructs the race through replay playback.
 *
 * Main components:
 * - ::ReplayManager records replay snapshots during live races.
 * - ::ReplaySnapshot stores one recorded sample or replay event.
 * - ::Replay stores an ordered list of snapshots and duration metadata.
 * - ::ReplayPreviewer plays replay data back by moving a replay car through recorded/interpolated poses.
 * - ::FloatCamera provides a free-follow camera used during replay viewing.
 * - ::ReplayRaceOverlaySource adapts replay timing to the generic ::RaceOverlay system.
 *
 * Contents:
 * - @ref replay_system_overview
 * - @ref replay_system_data_model
 * - @ref replay_system_recording
 * - @ref replay_system_playback
 * - @ref replay_system_camera
 * - @ref replay_system_api
 * - @ref replay_system_integration
 * - @ref replay_system_performance
 * - @ref replay_system_troubleshooting
 * - @ref replay_system_versions
 *
 * ----------------------------------------------------------------------
 * @section replay_system_overview Overview
 *
 * Responsibilities:
 * - Record live race state into replay snapshots.
 * - Mark important replay events such as race start, checkpoint, and race end.
 * - Store replay duration and snapshot data in serializable objects.
 * - Save a copied replay when a race ends.
 * - Replay a saved run by instantiating or controlling a replay car.
 * - Interpolate replay pose, speed, and steering angle over time.
 * - Update replay car wheel visuals during playback.
 * - Display checkpoint split popups during replay playback.
 * - Feed replay timing and completion state into the race overlay.
 *
 * Threading:
 * - Unity main thread only.
 * - Recording uses FixedUpdate and checkpoint callbacks.
 * - Playback uses Update and Unity object transforms.
 *
 * Important invariants:
 * - Replay snapshots are ordered by TrackTime.
 * - ReplayManager records only while recording is active.
 * - SaveReplay returns a copy so later recording changes do not mutate saved data.
 * - ReplayPreviewer copies incoming replay data before storing it.
 * - ReplayPreviewer needs at least one snapshot to play a replay.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_data_model Data Model
 *
 * ::ReplaySnapshot:
 * - TrackTime:
 *   Time in seconds when the snapshot was recorded.
 *
 * - TrackPosition:
 *   Recorded vehicle world position.
 *
 * - TrackRotation:
 *   Recorded vehicle world rotation.
 *
 * - LapIndex:
 *   Lap index at the time of the snapshot.
 *
 * - VehicleSpeed:
 *   Vehicle speed at the time of the snapshot.
 *
 * - SteeringAngle:
 *   Steering angle at the time of the snapshot.
 *
 * - Type:
 *   Snapshot type describing whether the snapshot is a regular sample or a replay event.
 *
 * Snapshot types:
 * - ReplaySnapshot::SnapshotType::FixedUpdate:
 *   Regular physics sample.
 *
 * - ReplaySnapshot::SnapshotType::Checkpoint:
 *   Snapshot recorded when a checkpoint is passed.
 *
 * - ReplaySnapshot::SnapshotType::RaceStart:
 *   Snapshot marking race start.
 *
 * - ReplaySnapshot::SnapshotType::RaceEnd:
 *   Snapshot marking race end.
 *
 * ::Replay:
 * - OverallTime:
 *   Overall replay/race time in seconds.
 *
 * - Snapshots:
 *   Ordered list of ::ReplaySnapshot values.
 *
 * - Duration:
 *   Effective replay duration. If snapshots exist, this is the larger value between OverallTime
 *   and the last snapshot time.
 *
 * Replay copying:
 * - Replay(Replay other) copies OverallTime and the snapshot list.
 * - Copy() returns a new Replay instance.
 * - Reset() clears snapshots and resets OverallTime to zero.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_recording Recording
 *
 * ::ReplayManager records replay data during live races.
 *
 * StartReplay:
 * - Creates a new ::Replay or resets the existing one.
 * - Clears cached vehicle references.
 * - Sets recording state to active.
 * - Subscribes to checkpoint events through ::CheckPointManager.
 * - Records a RaceStart snapshot.
 *
 * FixedUpdate:
 * - Records regular FixedUpdate snapshots while recording is active.
 *
 * Checkpoint snapshots:
 * - ReplayManager registers a checkpoint callback.
 * - When a checkpoint is passed, TakeCheckpointSnapshot records a Checkpoint snapshot.
 *
 * SaveReplay:
 * - Records a final RaceEnd snapshot.
 * - Stops recording.
 * - Removes checkpoint listeners.
 * - Returns a copied ::Replay.
 * - Returns null if recording was not active.
 *
 * Snapshot capture:
 * - The vehicle is resolved lazily with FindFirstObjectByType<VehicleController>().
 * - Rigidbody is used to read vehicle speed.
 * - ::DriveTrainController is used to read steering angle.
 * - ::RaceTimeManager provides current race time.
 * - ::TrackManager provides current lap index.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_playback Playback
 *
 * ::ReplayPreviewer plays saved replay data.
 *
 * Playback setup:
 * - SetReplay(Replay) copies replay data.
 * - Playback state is reset to time zero.
 * - If no replay data exists, the replay car is cleared.
 * - If replay data exists, a replay car is created or the previewer transform is used.
 * - Playback starts automatically when playOnSetReplay is enabled.
 *
 * Replay car:
 * - replayCarPrefab may be instantiated for playback.
 * - The first snapshot determines the initial replay car pose.
 * - Rigidbody components on the replay car are made kinematic.
 * - Rigidbody collisions are disabled.
 * - Most MonoBehaviours on the replay car can be disabled so recorded data controls playback.
 * - The replay car's ::DriveTrainController may remain enabled to update wheel visuals.
 *
 * Playback update:
 * - Update advances replay time when playback is active.
 * - replayTime is multiplied by playbackSpeed.
 * - If loopReplay is enabled, playback wraps when it reaches the end.
 * - If loopReplay is disabled, playback stops at the replay duration.
 * - The replay pose is applied for the current replay time.
 *
 * Sampling:
 * - If replay time is before the first snapshot, the first snapshot is used.
 * - If replay time is after the final snapshot, the final snapshot is used.
 * - Otherwise, neighboring snapshots are selected and interpolated.
 * - Position, rotation, vehicle speed, and steering angle are sampled.
 * - Steering angle is passed to DriveTrainController::ApplyReplayWheelVisuals.
 *
 * Checkpoint display:
 * - ReplayPreviewer tracks the next checkpoint snapshot.
 * - When playback crosses a checkpoint snapshot time, it asks ::RaceOverlay to display a split popup.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_camera Replay Camera
 *
 * ::FloatCamera is used for replay viewing.
 *
 * Responsibilities:
 * - Follow or orbit around a replay target.
 * - Use a replay car transform as the camera target.
 * - Provide viewer control without affecting replay playback.
 *
 * ReplayPreviewer integration:
 * - When the replay car is created, the replay target is assigned to ::FloatCamera when available.
 * - The replay camera should exist in replay scenes where automatic camera targeting is expected.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_api Public API Reference
 *
 * ::ReplayManager:
 * - void StartReplay()
 *   Starts recording a new replay, resets previous replay data when needed, subscribes to checkpoint events,
 *   and records a RaceStart snapshot.
 *
 * - Replay SaveReplay()
 *   Stops recording, records a RaceEnd snapshot, removes checkpoint listeners, and returns a copied replay.
 *
 * ::ReplaySnapshot:
 * - ReplaySnapshot(float trackTime, Vector3 trackPosition, Quaternion trackRotation,
 *                  int currentLap, float vehicleSpeed, float steeringAngle,
 *                  ReplaySnapshot::SnapshotType type)
 *   Creates one replay snapshot from recorded vehicle and race state.
 *
 * ::Replay:
 * - Replay()
 *   Creates an empty replay.
 *
 * - Replay(float overallTime, List<ReplaySnapshot> snapshots)
 *   Creates a replay from duration metadata and a snapshot list.
 *
 * - Replay(Replay other)
 *   Creates a copied replay.
 *
 * - float Duration
 *   Gets the effective replay duration.
 *
 * - void AddSnapshot(float trackTime, Vector3 trackPosition, Quaternion trackRotation,
 *                    int currentLap, float currentSpeed, float steeringAngle,
 *                    ReplaySnapshot::SnapshotType snapshotType)
 *   Adds a snapshot and updates OverallTime.
 *
 * - Replay Copy()
 *   Returns a copied replay.
 *
 * - void Reset()
 *   Clears snapshot data and resets OverallTime.
 *
 * ::ReplayPreviewer:
 * - bool HasReplay
 *   True when a replay with at least one snapshot is loaded.
 *
 * - float CurrentReplayTime
 *   Current playback time in seconds.
 *
 * - float ReplayDuration
 *   Duration of the loaded replay, or zero when no replay exists.
 *
 * - bool IsPlaying
 *   True when replay playback is advancing.
 *
 * - bool IsReplayFinished
 *   True when a non-looping replay has reached the end.
 *
 * - void SetReplay(Replay newReplay)
 *   Copies replay data, resets playback state, creates the replay car, and optionally starts playback.
 *
 * - void SetReplay(Replay newReplay, GameObject carPrefab)
 *   Sets a replay and overrides the replay car prefab.
 *
 * - void Play()
 *   Starts or resumes playback.
 *
 * - void Pause()
 *   Pauses playback.
 *
 * - void Stop()
 *   Stops playback and returns to the start.
 *
 * - void ClearReplay()
 *   Clears loaded replay data and optionally destroys the replay car.
 *
 * ::ReplayRaceOverlaySource:
 * - bool TryGetState(out RaceOverlayState state)
 *   Provides replay timing and replay completion data to ::RaceOverlay.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_integration Integration Notes
 *
 * Race timing:
 * - ::RaceTimeManager starts replay recording when the race starts.
 * - ::RaceTimeManager calls ::ReplayManager::SaveReplay when the race ends.
 * - The saved replay is submitted to ::GameDataManager with the completed race result.
 *
 * Checkpoints:
 * - ::ReplayManager subscribes to ::CheckPointManager checkpoint callbacks.
 * - Checkpoint snapshots are recorded alongside regular fixed-update snapshots.
 *
 * Game data:
 * - ::GameDataManager stores best replays for custom levels and the practice/test map.
 * - Replay buttons should check whether a stored replay exists and contains snapshots before entering replay view.
 *
 * UI:
 * - ::ReplayRaceOverlaySource connects replay playback to ::RaceOverlay.
 * - Replay checkpoint snapshots can trigger split popups during playback.
 *
 * Vehicle visuals:
 * - ::ReplayPreviewer uses ::DriveTrainController::ApplyReplayWheelVisuals to animate wheel steering during replay.
 *
 * Camera:
 * - Replay scenes should contain a ::FloatCamera if automatic replay camera targeting is desired.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_performance Performance and GC
 *
 * - ReplayManager records one snapshot per FixedUpdate while recording is active.
 * - Longer races create more snapshots and larger save data.
 * - Replay objects are copied when saved and when assigned for playback.
 * - ReplayPreviewer interpolation is linear in normal playback and uses cached snapshot indices.
 * - Large replay objects increase PlayerPrefs JSON size when saved through ::GameDataManager.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_troubleshooting Troubleshooting
 *
 * Replay is null after race:
 * - Check that ::ReplayManager exists in the race scene.
 * - Check that ::RaceTimeManager::RaceStart called StartReplay.
 * - Check that ::RaceTimeManager::RaceEnd reached SaveReplay.
 *
 * Replay has no snapshots:
 * - Check that a ::VehicleController exists during recording.
 * - Check that ::RaceTimeManager and ::TrackManager exist during recording.
 * - Check that recording is active before FixedUpdate snapshots are expected.
 *
 * Replay button refuses to open:
 * - The saved replay may be null.
 * - The replay may have no snapshots.
 * - The replay duration may be zero.
 *
 * Replay car does not move:
 * - Check that SetReplay was called with valid data.
 * - Check that replayCarPrefab is assigned if a separate replay car should be instantiated.
 * - Check that playback is active.
 *
 * Replay camera does not follow:
 * - Ensure the replay scene contains ::FloatCamera.
 * - Ensure the replay car was created successfully.
 *
 * Split popup does not appear in replay:
 * - Check that the replay contains Checkpoint snapshots.
 * - Check that ::RaceOverlay.Instance exists.
 *
 * Replay data is too large:
 * - Reduce race duration.
 * - Reduce recording frequency if the system is extended.
 * - Avoid storing unnecessary fields in snapshots.
 *
 * ----------------------------------------------------------------------
 * @section replay_system_versions Version History
 *
 * - v1.4: Added replay overlay source and checkpoint split display during replay playback.
 * - v1.3: Added replay car preparation, interpolation, and replay wheel visual updates.
 * - v1.2: Added ReplayPreviewer playback controls and replay camera integration.
 * - v1.1: Added checkpoint and race start/end snapshot types.
 * - v1.0: Added replay data model and fixed-update replay recording.
 */