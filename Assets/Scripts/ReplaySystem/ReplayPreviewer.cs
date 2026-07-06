using Generic;
using UnityEngine;

/// <summary>
/// Plays back recorded race replays by moving a replay car through recorded snapshots.
/// </summary>
/// <remarks>
/// @ingroup replay_system
/// @brief Instantiates or reuses a replay vehicle, samples <see cref="ReplaySnapshot"/> data over time, and updates replay playback state.
///
/// This component is responsible for previewing a recorded <see cref="Replay"/>. It advances replay time,
/// interpolates between recorded snapshots, applies the interpolated position/rotation to a replay vehicle,
/// updates replay wheel visuals when possible, and displays checkpoint split information through
/// <see cref="RaceOverlay"/>.
///
/// The previewer can either instantiate a configured replay car prefab or use its own transform when no prefab
/// is assigned.
/// </remarks>
public class ReplayPreviewer : SceneSingleton<ReplayPreviewer>
{
	#region Inspector

	/// <summary>
	/// Replay data currently loaded for playback.
	/// </summary>
	[Tooltip("Replay data currently loaded for playback.")]
	[SerializeField] private Replay replay;

	/// <summary>
	/// Prefab instantiated as the visible replay car.
	/// </summary>
	[Tooltip("Prefab used as the visible replay car. If empty, this object's transform is used instead.")]
	[SerializeField] private GameObject replayCarPrefab;

	/// <summary>
	/// Whether playback starts automatically after a replay is assigned.
	/// </summary>
	[Tooltip("If enabled, playback starts automatically when a replay is assigned.")]
	[SerializeField] private bool playOnSetReplay = true;

	/// <summary>
	/// Whether playback loops after reaching the replay duration.
	/// </summary>
	[Tooltip("If enabled, the replay restarts from the beginning after reaching the end.")]
	[SerializeField] private bool loopReplay = false;

	/// <summary>
	/// Whether the instantiated replay car is destroyed when the replay is cleared.
	/// </summary>
	[Tooltip("If enabled, the instantiated replay car is destroyed when the replay is cleared.")]
	[SerializeField] private bool destroyCarWhenCleared = true;

	/// <summary>
	/// Whether most MonoBehaviour scripts on the instantiated replay car should be disabled for playback.
	/// </summary>
	[Tooltip("If enabled, scripts on the replay car are disabled so recorded playback controls the car.")]
	[SerializeField] private bool disableScriptsOnReplayCar = true;

	/// <summary>
	/// Playback speed multiplier applied to replay time.
	/// </summary>
	[Tooltip("Replay playback speed multiplier.")]
	[SerializeField] private float playbackSpeed = 1f;

	#endregion

	#region State

	/// <summary>
	/// Instantiated replay car GameObject, if a prefab is used.
	/// </summary>
	private GameObject replayCarInstance_;

	/// <summary>
	/// Transform moved during replay playback.
	/// </summary>
	private Transform replayCarTransform_;

	/// <summary>
	/// Current replay playback time in seconds.
	/// </summary>
	private float replayTime_;

	/// <summary>
	/// Whether replay playback is currently advancing.
	/// </summary>
	private bool isPlaying_;

	/// <summary>
	/// Cached index of the current replay snapshot used for interpolation.
	/// </summary>
	private int currentSnapshotIndex_;

	/// <summary>
	/// Index of the next checkpoint snapshot that should trigger a split display.
	/// </summary>
	private int nextCheckpointSnapshotIndex_;

	/// <summary>
	/// Current interpolated vehicle speed from replay data.
	/// </summary>
	private float currentVehicleSpeed_;

	/// <summary>
	/// Current interpolated steering angle from replay data.
	/// </summary>
	private float currentSteeringAngle_;

	/// <summary>
	/// Drive train controller on the replay car, used to update wheel visuals during playback.
	/// </summary>
	private DriveTrainController replayDriveTrainController_;

	/// <summary>
	/// Gets whether a replay with at least one snapshot is currently loaded.
	/// </summary>
	public bool HasReplay => replay != null && replay.Snapshots != null && replay.Snapshots.Count > 0;

	/// <summary>
	/// Gets the current replay playback time in seconds.
	/// </summary>
	public float CurrentReplayTime => replayTime_;

	/// <summary>
	/// Gets the duration of the loaded replay.
	/// </summary>
	/// <remarks>
	/// Returns zero when no replay is loaded.
	/// </remarks>
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

	/// <summary>
	/// Gets whether replay playback is currently active.
	/// </summary>
	public bool IsPlaying => isPlaying_;

	/// <summary>
	/// Gets whether a non-looping replay has reached its end.
	/// </summary>
	public bool IsReplayFinished => HasReplay && !loopReplay && replayTime_ >= ReplayDuration;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Advances replay playback and applies the current replay pose once per frame.
	/// </summary>
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

	/// <summary>
	/// Assigns replay data, resets playback state, creates the replay car, and optionally starts playback.
	/// </summary>
	/// <param name="newReplay">Replay data to preview. If null, the current replay is cleared.</param>
	/// <remarks>
	/// The replay is copied before being stored so later changes to the original replay object do not affect playback.
	/// </remarks>
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

	/// <summary>
	/// Assigns replay data and overrides the replay car prefab used for playback.
	/// </summary>
	/// <param name="newReplay">Replay data to preview.</param>
	/// <param name="carPrefab">Replay car prefab to instantiate for this replay.</param>
	public void SetReplay(Replay newReplay, GameObject carPrefab)
	{
		replayCarPrefab = carPrefab;
		SetReplay(newReplay);
	}

	/// <summary>
	/// Starts or resumes replay playback.
	/// </summary>
	public void Play()
	{
		if (!HasReplay || replayCarTransform_ == null)
		{
			return;
		}

		isPlaying_ = true;
	}

	/// <summary>
	/// Pauses replay playback without resetting replay time.
	/// </summary>
	public void Pause()
	{
		isPlaying_ = false;
	}

	/// <summary>
	/// Stops replay playback and returns to the beginning of the replay.
	/// </summary>
	public void Stop()
	{
		isPlaying_ = false;
		ResetReplayState();

		if (HasReplay && replayCarTransform_ != null)
		{
			ApplyReplayAtTime(0f);
		}
	}

	/// <summary>
	/// Clears the loaded replay and optionally destroys the replay car.
	/// </summary>
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

	/// <summary>
	/// Gets the current replay speed converted to kilometers per hour.
	/// </summary>
	/// <returns>Current replay vehicle speed multiplied by 3.6.</returns>
	public float GetCurrentSpeed()
	{
		return currentVehicleSpeed_ * 3.6f;
	}

	/// <summary>
	/// Gets the current interpolated steering angle from replay playback.
	/// </summary>
	/// <returns>Current replay steering angle.</returns>
	public float GetCurrentSteeringAngle()
	{
		return currentSteeringAngle_;
	}

	#endregion

	#region Replay Timer

	/// <summary>
	/// Advances replay time according to <see cref="playbackSpeed"/> and handles looping/end-of-replay behavior.
	/// </summary>
	/// <remarks>
	/// Also processes checkpoint split snapshots crossed between the previous and current replay time.
	/// </remarks>
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

	/// <summary>
	/// Creates the replay car and positions it at the first recorded snapshot.
	/// </summary>
	/// <remarks>
	/// If <see cref="replayCarPrefab"/> is assigned, it is instantiated as a child of this object.
	/// Otherwise, this component's own transform is used as the replay transform.
	/// </remarks>
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

		FloatCamera floatCamera = FindFirstObjectByType<FloatCamera>();

		if (floatCamera != null)
		{
			floatCamera.SetTarget(replayCarTransform_);
		}

		Debug.Log("Replay Car Created");
	}

	/// <summary>
	/// Destroys the instantiated replay car and clears cached replay car references.
	/// </summary>
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

	/// <summary>
	/// Prepares the instantiated replay car so it is controlled by replay data instead of physics or gameplay scripts.
	/// </summary>
	/// <remarks>
	/// Rigidbody components are made kinematic and collision detection is disabled. When
	/// <see cref="disableScriptsOnReplayCar"/> is enabled, MonoBehaviours on the replay car are disabled except
	/// for the cached <see cref="DriveTrainController"/>, which may still be used for wheel visuals.
	/// </remarks>
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

	/// <summary>
	/// Resets replay playback time and cached snapshot indices.
	/// </summary>
	private void ResetReplayState()
	{
		replayTime_ = 0f;
		currentSnapshotIndex_ = 0;
		nextCheckpointSnapshotIndex_ = FindNextCheckpointSnapshotIndex(0f);
	}

	/// <summary>
	/// Applies the interpolated replay pose and replay visual state for a specific replay time.
	/// </summary>
	/// <param name="trackTime">Replay time, in seconds, to sample.</param>
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

	/// <summary>
	/// Samples replay data at a specific time by selecting or interpolating between snapshots.
	/// </summary>
	/// <param name="trackTime">Replay time, in seconds, to sample.</param>
	/// <param name="position">Interpolated replay position.</param>
	/// <param name="rotation">Interpolated replay rotation.</param>
	/// <param name="vehicleSpeed">Interpolated replay vehicle speed.</param>
	/// <param name="steeringAngle">Interpolated replay steering angle.</param>
	/// <returns><c>true</c> if replay pose data was available; otherwise <c>false</c>.</returns>
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

	/// <summary>
	/// Finds the next checkpoint snapshot after the given replay time.
	/// </summary>
	/// <param name="currentTime">Replay time, in seconds, after which the next checkpoint should be found.</param>
	/// <returns>Index of the next checkpoint snapshot, or <c>-1</c> if none exists.</returns>
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

	/// <summary>
	/// Displays checkpoint split popups for checkpoint snapshots crossed between two replay times.
	/// </summary>
	/// <param name="previousTime">Previous replay time before the latest timer update.</param>
	/// <param name="currentTime">Current replay time after the latest timer update.</param>
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

	/// <summary>
	/// Displays a checkpoint split for a replay checkpoint snapshot.
	/// </summary>
	/// <param name="checkpointSnapshot">Checkpoint snapshot whose time should be displayed.</param>
	private void DisplayCheckpointSplit(ReplaySnapshot checkpointSnapshot)
	{
		if (RaceOverlay.Instance == null)
		{
			return;
		}

		RaceOverlay.Instance.DisplaySplit(checkpointSnapshot.TrackTime, 0f);
	}

	#endregion
}