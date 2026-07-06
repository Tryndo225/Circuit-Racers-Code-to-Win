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
/// It also records special snapshots for race start, checkpoints, and race end.
/// </remarks>
public class ReplayManager : SceneSingleton<ReplayManager>
{
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
	/// Starts recording a new replay.
	/// </summary>
	/// <remarks>
	/// Reuses and resets the existing <see cref="Replay"/> instance when possible.
	/// The method clears cached vehicle references, subscribes to checkpoint events, and records a
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
	/// Records regular replay samples while replay recording is active.
	/// </summary>
	private void FixedUpdate()
	{
		if (!isRecording_ || currentReplay_ == null)
			return;

		TakeSnapshot(ReplaySnapshot.SnapshotType.FixedUpdate);
	}

	/// <summary>
	/// Records a checkpoint replay snapshot when a checkpoint event is received.
	/// </summary>
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