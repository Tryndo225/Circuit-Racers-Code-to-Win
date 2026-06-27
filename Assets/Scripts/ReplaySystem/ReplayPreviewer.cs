using Generic;
using UnityEngine;

public class ReplayPreviewer : SceneSingleton<ReplayPreviewer>
{
	#region Inspector

	[SerializeField] private Replay replay;

	[SerializeField] private GameObject replayCarPrefab;

	[SerializeField] private bool playOnSetReplay = true;

	[SerializeField] private bool loopReplay = false;

	[SerializeField] private bool destroyCarWhenCleared = true;

	[SerializeField] private bool disableScriptsOnReplayCar = true;

	[SerializeField] private float playbackSpeed = 1f;

	#endregion

	#region State

	private GameObject replayCarInstance_;
	private Transform replayCarTransform_;

	private float replayTime_;
	private bool isPlaying_;

	private int currentSnapshotIndex_;
	private int nextCheckpointSnapshotIndex_;

	private float currentVehicleSpeed_;
	private float currentSteeringAngle_;

	private DriveTrainController replayDriveTrainController_;

	public bool HasReplay => replay != null && replay.Snapshots != null && replay.Snapshots.Count > 0;

	public float CurrentReplayTime => replayTime_;

	public float ReplayDuration
	{
		get
		{
			if (!HasReplay)
			{
				return 0f;
			}

			return replay.Duration;
		}
	}

	public bool IsPlaying => isPlaying_;

	public bool IsReplayFinished => HasReplay && !loopReplay && replayTime_ >= ReplayDuration;

	#endregion

	#region Unity Methods

	private void Update()
	{
		if (!isPlaying_ || !HasReplay || replayCarTransform_ == null)
		{
			return;
		}

		UpdateReplayTimer();

		ApplyReplayAtTime(replayTime_);
	}

	#endregion

	#region Public API

	public void SetReplay(Replay newReplay)
	{
		replay = newReplay != null ? newReplay.Copy() : null;


		ResetReplayState();


		if (!HasReplay)
		{
			ClearReplayCar();
			return;
		}

		CreateReplayCar();


		if (playOnSetReplay)
		{
			Play();
		}
		else
		{
			ApplyReplayAtTime(0f);
		}
	}

	public void SetReplay(Replay newReplay, GameObject carPrefab)
	{
		replayCarPrefab = carPrefab;
		SetReplay(newReplay);
	}

	public void Play()
	{
		if (!HasReplay || replayCarTransform_ == null)
		{
			return;
		}

		isPlaying_ = true;
	}

	public void Pause()
	{
		isPlaying_ = false;
	}

	public void Stop()
	{
		isPlaying_ = false;
		ResetReplayState();

		if (HasReplay && replayCarTransform_ != null)
		{
			ApplyReplayAtTime(0f);
		}
	}

	public void ClearReplay()
	{
		replay = null;
		isPlaying_ = false;
		ResetReplayState();

		if (destroyCarWhenCleared)
		{
			ClearReplayCar();
		}
	}

	public float GetCurrentSpeed()
	{
		return currentVehicleSpeed_ * 3.6f;
	}

	public float GetCurrentSteeringAngle()
	{
		return currentSteeringAngle_;
	}

	#endregion

	#region Replay Timer

	private void UpdateReplayTimer()
	{
		float duration = ReplayDuration;

		if (duration <= 0f)
		{
			isPlaying_ = false;
			return;
		}

		float previousTime = replayTime_;
		float nextTime = replayTime_ + Time.deltaTime * playbackSpeed;

		if (loopReplay && nextTime > duration)
		{
			ProcessCheckpointSplits(previousTime, duration);

			nextTime %= duration;

			replayTime_ = nextTime;
			currentSnapshotIndex_ = 0;
			nextCheckpointSnapshotIndex_ = FindNextCheckpointSnapshotIndex(0f);

			ProcessCheckpointSplits(0f, replayTime_);
			return;
		}

		if (!loopReplay && nextTime >= duration)
		{
			replayTime_ = duration;
			ProcessCheckpointSplits(previousTime, replayTime_);
			isPlaying_ = false;
			return;
		}

		replayTime_ = nextTime;
		ProcessCheckpointSplits(previousTime, replayTime_);
	}

	#endregion

	#region Replay Car

	private void CreateReplayCar()
	{
		ClearReplayCar();

		if (!HasReplay)
		{
			return;
		}

		ReplaySnapshot first = replay.Snapshots[0];

		if (replayCarPrefab != null)
		{
			replayCarInstance_ = Instantiate(replayCarPrefab, first.TrackPosition, first.TrackRotation, transform);

			replayCarTransform_ = replayCarInstance_.transform;
			replayDriveTrainController_ = replayCarInstance_.GetComponentInChildren<DriveTrainController>(true);

			PrepareReplayCar();
		}
		else
		{
			replayCarTransform_ = transform;
			replayCarTransform_.SetPositionAndRotation(first.TrackPosition, first.TrackRotation);
		}

		FindFirstObjectByType<FloatCamera>().SetTarget(replayCarTransform_);

		Debug.Log("Replay Car Created");
	}

	private void ClearReplayCar()
	{
		if (replayCarInstance_ != null)
		{
			Destroy(replayCarInstance_);
			replayCarInstance_ = null;
		}

		replayCarTransform_ = null;
		replayDriveTrainController_ = null;
	}

	private void PrepareReplayCar()
	{
		if (replayCarInstance_ == null)
		{
			return;
		}

		Rigidbody[] rigidbodies = replayCarInstance_.GetComponentsInChildren<Rigidbody>();

		for (int i = 0; i < rigidbodies.Length; i++)
		{
			rigidbodies[i].linearVelocity = Vector3.zero;
			rigidbodies[i].angularVelocity = Vector3.zero;
			rigidbodies[i].isKinematic = true;
			rigidbodies[i].detectCollisions = false;
		}

		if (!disableScriptsOnReplayCar)
		{
			return;
		}

		MonoBehaviour[] behaviours = replayCarInstance_.GetComponentsInChildren<MonoBehaviour>();

		for (int i = 0; i < behaviours.Length; i++)
		{
			if (behaviours[i] == replayDriveTrainController_)
			{
				continue;
			}

			if (behaviours[i] != this)
			{
				behaviours[i].enabled = false;
			}
		}
	}

	#endregion

	#region Replay Sampling

	private void ResetReplayState()
	{
		replayTime_ = 0f;
		currentSnapshotIndex_ = 0;
		nextCheckpointSnapshotIndex_ = FindNextCheckpointSnapshotIndex(0f);
	}

	private void ApplyReplayAtTime(float trackTime)
	{
		if (!TryGetReplayPose(trackTime, out Vector3 position, out Quaternion rotation, out float vehicleSpeed, out float steeringAngle))
		{
			return;
		}

		currentVehicleSpeed_ = vehicleSpeed;
		currentSteeringAngle_ = steeringAngle;

		replayCarTransform_.SetPositionAndRotation(position, rotation);

		if (replayDriveTrainController_ != null)
		{
			replayDriveTrainController_.ApplyReplayWheelVisuals(currentSteeringAngle_);
		}
	}

	private bool TryGetReplayPose(float trackTime, out Vector3 position, out Quaternion rotation, out float vehicleSpeed, out float steeringAngle)
	{
		position = replayCarTransform_ != null ? replayCarTransform_.position : transform.position;
		rotation = replayCarTransform_ != null ? replayCarTransform_.rotation : transform.rotation;
		vehicleSpeed = 0f;
		steeringAngle = 0f;

		if (!HasReplay)
		{
			return false;
		}

		if (trackTime <= replay.Snapshots[0].TrackTime)
		{
			ReplaySnapshot first = replay.Snapshots[0];

			position = first.TrackPosition;
			rotation = first.TrackRotation;
			vehicleSpeed = first.VehicleSpeed;
			steeringAngle = first.SteeringAngle;

			currentSnapshotIndex_ = 0;
			return true;
		}

		int lastIndex = replay.Snapshots.Count - 1;

		if (trackTime >= replay.Snapshots[lastIndex].TrackTime)
		{
			ReplaySnapshot last = replay.Snapshots[lastIndex];

			position = last.TrackPosition;
			rotation = last.TrackRotation;
			vehicleSpeed = last.VehicleSpeed;
			steeringAngle = last.SteeringAngle;

			currentSnapshotIndex_ = Mathf.Max(0, lastIndex - 1);
			return true;
		}

		while (currentSnapshotIndex_ < lastIndex - 1 &&
			   replay.Snapshots[currentSnapshotIndex_ + 1].TrackTime < trackTime)
		{
			currentSnapshotIndex_++;
		}

		while (currentSnapshotIndex_ > 0 &&
			   replay.Snapshots[currentSnapshotIndex_].TrackTime > trackTime)
		{
			currentSnapshotIndex_--;
		}

		ReplaySnapshot a = replay.Snapshots[currentSnapshotIndex_];
		ReplaySnapshot b = replay.Snapshots[currentSnapshotIndex_ + 1];

		float timeDifference = b.TrackTime - a.TrackTime;

		if (timeDifference <= 0.0001f)
		{
			position = b.TrackPosition;
			rotation = b.TrackRotation;
			vehicleSpeed = b.VehicleSpeed;
			steeringAngle = b.SteeringAngle;
			return true;
		}

		float t = Mathf.InverseLerp(a.TrackTime, b.TrackTime, trackTime);

		position = Vector3.Lerp(a.TrackPosition, b.TrackPosition, t);
		rotation = Quaternion.Slerp(a.TrackRotation, b.TrackRotation, t);
		vehicleSpeed = Mathf.Lerp(a.VehicleSpeed, b.VehicleSpeed, t);
		steeringAngle = Mathf.Lerp(a.SteeringAngle, b.SteeringAngle, t);

		return true;
	}

	#endregion

	#region Checkpoint Splits

	private int FindNextCheckpointSnapshotIndex(float currentTime)
	{
		if (!HasReplay)
		{
			return -1;
		}

		for (int i = 0; i < replay.Snapshots.Count; i++)
		{
			if (replay.Snapshots[i].Type != ReplaySnapshot.SnapshotType.Checkpoint)
			{
				continue;
			}

			if (replay.Snapshots[i].TrackTime > currentTime)
			{
				return i;
			}
		}

		return -1;
	}

	private void ProcessCheckpointSplits(float previousTime, float currentTime)
	{
		if (!HasReplay || nextCheckpointSnapshotIndex_ < 0)
		{
			return;
		}

		while (nextCheckpointSnapshotIndex_ >= 0 &&
			   nextCheckpointSnapshotIndex_ < replay.Snapshots.Count &&
			   replay.Snapshots[nextCheckpointSnapshotIndex_].TrackTime <= currentTime)
		{
			ReplaySnapshot checkpointSnapshot = replay.Snapshots[nextCheckpointSnapshotIndex_];

			if (checkpointSnapshot.Type == ReplaySnapshot.SnapshotType.Checkpoint &&
				checkpointSnapshot.TrackTime > previousTime)
			{
				DisplayCheckpointSplit(checkpointSnapshot);
			}

			nextCheckpointSnapshotIndex_ = FindNextCheckpointSnapshotIndex(checkpointSnapshot.TrackTime);
		}
	}

	private void DisplayCheckpointSplit(ReplaySnapshot checkpointSnapshot)
	{
		if (RaceOverLay.Instance == null)
		{
			return;
		}

		RaceOverLay.Instance.DisplaySplit(checkpointSnapshot.TrackTime, 0f);
	}

	#endregion
}