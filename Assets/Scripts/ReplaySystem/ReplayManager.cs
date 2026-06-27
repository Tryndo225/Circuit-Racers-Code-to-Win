using Generic;
using UnityEngine;

public class ReplayManager : SceneSingleton<ReplayManager>
{
	private Transform vehicleTransform_;
	private Replay currentReplay_;
	private bool isRecording_;
	private Rigidbody vehicleRigidBody_;
	private DriveTrainController driveTrainController_;

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

	private void FixedUpdate()
	{
		if (!isRecording_ || currentReplay_ == null)
			return;

		TakeSnapshot(ReplaySnapshot.SnapshotType.FixedUpdate);
	}

	private void TakeCheckpointSnapshot()
	{
		if (!isRecording_)
			return;

		TakeSnapshot(ReplaySnapshot.SnapshotType.Checkpoint);
	}

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