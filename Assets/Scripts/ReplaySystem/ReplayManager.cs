using Generic;
using UnityEngine;

/// <summary>
/// Records replay snapshots during a race.
/// </summary>
/// <remarks>
/// @ingroup replay_system
/// @brief Captures vehicle transform, speed, steering angle, lap index, timing, and replay events into a <see cref="Replay"/>.
///
/// The manager records regular snapshots during <see cref="FixedUpdate"/> while recording is active.
/// Regular snapshots can be downsampled by skipping a configured number of physics ticks between captures.
/// Special snapshots for race start, checkpoints, and race end are still recorded immediately.
/// </remarks>
public class ReplayManager : SceneSingleton<ReplayManager>
{
	/// <summary>
	/// Number of fixed physics ticks skipped between regular replay snapshots.
	/// </summary>
	/// <remarks>
	/// A value of <c>0</c> records a snapshot every <see cref="FixedUpdate"/>.
	/// A value of <c>1</c> records every second physics tick, a value of <c>2</c>
	/// records every third physics tick, and so on. This reduces replay size while
	/// keeping special snapshots such as checkpoints and race start/end unaffected.
	/// </remarks>
	[Tooltip("Number of FixedUpdate ticks skipped between regular replay snapshots. 0 = record every physics tick.")]
	[SerializeField] private uint timeStepSkip = 0;

	/// <summary>
	/// Cached transform of the vehicle currently being recorded.
	/// </summary>
	private Transform vehicleTransform_;

	/// <summary>
	/// Replay currently being recorded.
	/// </summary>
	private Replay currentReplay_;

	/// <summary>
	/// Whether replay recording is currently active.
	/// </summary>
	private bool isRecording_;

	/// <summary>
	/// Cached rigidbody of the recorded vehicle, used to read vehicle speed.
	/// </summary>
	private Rigidbody vehicleRigidBody_;

	/// <summary>
	/// Cached drivetrain controller of the recorded vehicle, used to read steering angle.
	/// </summary>
	private DriveTrainController driveTrainController_;

	/// <summary>
	/// Number of fixed physics ticks skipped since the last regular replay snapshot.
	/// </summary>
	/// <remarks>
	/// This counter is reset after a regular <see cref="ReplaySnapshot.SnapshotType.FixedUpdate"/>
	/// snapshot is captured.
	/// </remarks>
	private uint skippedSteps_;

	/// <summary>
	/// Starts recording a new replay.
	/// </summary>
	/// <remarks>
	/// Reuses and resets the existing <see cref="Replay"/> instance when possible.
	/// The method clears cached vehicle references, subscribes to checkpoint events, resets the
	/// regular-snapshot skip counter, and records a
	/// <see cref="ReplaySnapshot.SnapshotType.RaceStart"/> snapshot.
	/// </remarks>
	public void StartReplay()
	{
		if (currentReplay_ == null)
			currentReplay_ = new Replay();
		else
			currentReplay_.Reset();

		vehicleTransform_ = null;
		isRecording_ = true;

		vehicleRigidBody_ = null;
		driveTrainController_ = null;

		if (CheckPointManager.Instance != null)
		{
			CheckPointManager.Instance.RemoveListenerFromCheckpoints(TakeCheckpointSnapshot);
			CheckPointManager.Instance.AddListenerToCheckpoints(TakeCheckpointSnapshot);
		}

		TakeSnapshot(ReplaySnapshot.SnapshotType.RaceStart);
		skippedSteps_ = 0;

		Debug.Log("[ReplayManager] Replay recording started.");
	}

	/// <summary>
	/// Stops recording and returns a copy of the completed replay.
	/// </summary>
	/// <returns>
	/// A copied <see cref="Replay"/> containing the recorded snapshots, or <c>null</c> if recording was not active.
	/// </returns>
	/// <remarks>
	/// Records a final <see cref="ReplaySnapshot.SnapshotType.RaceEnd"/> snapshot before stopping.
	/// Checkpoint listeners are removed after recording ends.
	/// </remarks>
	public Replay SaveReplay()
	{
		if (!isRecording_ || currentReplay_ == null)
		{
			Debug.LogWarning("[ReplayManager] Tried to save replay, but replay is not recording.");
			return null;
		}

		TakeSnapshot(ReplaySnapshot.SnapshotType.RaceEnd);

		isRecording_ = false;

		if (CheckPointManager.Instance != null)
			CheckPointManager.Instance.RemoveListenerFromCheckpoints(TakeCheckpointSnapshot);

		Debug.Log($"[ReplayManager] Saved replay with {currentReplay_.Snapshots.Count} snapshots. Duration: {currentReplay_.Duration}");

		return currentReplay_.Copy();
	}

	/// <summary>
	/// Records regular replay snapshots while replay recording is active.
	/// </summary>
	/// <remarks>
	/// Regular snapshots are recorded according to <see cref="timeStepSkip"/>.
	/// Special snapshots such as checkpoint, race start, and race end are recorded outside this method
	/// and are not affected by the skip counter.
	/// </remarks>
	private void FixedUpdate()
	{
		if (!isRecording_ || currentReplay_ == null)
			return;

		if (skippedSteps_ < timeStepSkip)
		{
			++skippedSteps_;
			return;
		}

		TakeSnapshot(ReplaySnapshot.SnapshotType.FixedUpdate);
		skippedSteps_ = 0;
	}

	/// <summary>
	/// Records a checkpoint replay snapshot when a checkpoint event is received.
	/// </summary>
	/// <remarks>
	/// Checkpoint snapshots are recorded immediately and are not affected by <see cref="timeStepSkip"/>.
	/// </remarks>
	private void TakeCheckpointSnapshot()
	{
		if (!isRecording_)
			return;

		TakeSnapshot(ReplaySnapshot.SnapshotType.Checkpoint);
	}

	/// <summary>
	/// Captures the current race and vehicle state as a replay snapshot.
	/// </summary>
	/// <param name="type">Replay snapshot type describing why the snapshot is being recorded.</param>
	/// <remarks>
	/// The vehicle reference is resolved lazily by finding a <see cref="VehicleController"/> in the scene.
	/// Snapshot capture is skipped if required race managers or vehicle references are unavailable.
	/// The captured speed is read from the vehicle rigidbody velocity magnitude.
	/// </remarks>
	private void TakeSnapshot(ReplaySnapshot.SnapshotType type)
	{
		if (currentReplay_ == null)
			return;

		if (RaceTimeManager.Instance == null || TrackManager.Instance == null)
			return;

		if (vehicleTransform_ == null)
		{
			VehicleController vehicle = FindFirstObjectByType<VehicleController>();

			if (vehicle == null)
				return;

			vehicleTransform_ = vehicle.transform;
			vehicleRigidBody_ = vehicle.GetComponent<Rigidbody>();
			driveTrainController_ = vehicle.GetComponent<DriveTrainController>();
		}

		float vehicleSpeed = 0f;

		if (vehicleRigidBody_ != null)
			vehicleSpeed = vehicleRigidBody_.linearVelocity.magnitude;

		float steeringAngle = 0f;

		if (driveTrainController_ != null)
			steeringAngle = driveTrainController_.GetSteeringAngle();

		currentReplay_.AddSnapshot(RaceTimeManager.Instance.GetCurrentRaceTime(), vehicleTransform_.position, vehicleTransform_.rotation, TrackManager.Instance.CurrentLap, vehicleSpeed, steeringAngle, type);
	}
}