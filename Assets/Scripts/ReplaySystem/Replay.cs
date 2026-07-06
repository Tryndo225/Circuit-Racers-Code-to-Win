using System;
using System.Collections.Generic;
using UnityEngine;
using static ReplaySnapshot;

/// <summary>
/// Serializable snapshot of the vehicle state at a specific replay time.
/// </summary>
/// <remarks>
/// @ingroup replay_system
/// @brief Stores one recorded replay sample, including transform, timing, lap, speed, steering, and snapshot type.
///
/// Snapshots are used by <see cref="Replay"/> to reconstruct or preview a recorded race over time.
/// The <see cref="Type"/> field marks whether the snapshot is a normal fixed-update sample or an important
/// replay event such as race start, checkpoint, or race end.
/// </remarks>
[Serializable]
public struct ReplaySnapshot
{
	/// <summary>
	/// Replay time, in seconds, at which this snapshot was recorded.
	/// </summary>
	[Tooltip("Replay time in seconds at which this snapshot was recorded.")]
	public float TrackTime;

	/// <summary>
	/// Recorded vehicle/world position at <see cref="TrackTime"/>.
	/// </summary>
	[Tooltip("Recorded vehicle position for this replay snapshot.")]
	public Vector3 TrackPosition;

	/// <summary>
	/// Recorded vehicle/world rotation at <see cref="TrackTime"/>.
	/// </summary>
	[Tooltip("Recorded vehicle rotation for this replay snapshot.")]
	public Quaternion TrackRotation;

	/// <summary>
	/// Lap index recorded at this snapshot.
	/// </summary>
	[Tooltip("Lap index recorded at this snapshot.")]
	public int LapIndex;

	/// <summary>
	/// Vehicle speed recorded at this snapshot.
	/// </summary>
	[Tooltip("Vehicle speed recorded at this snapshot.")]
	public float VehicleSpeed;

	/// <summary>
	/// Steering angle recorded at this snapshot.
	/// </summary>
	[Tooltip("Steering angle recorded at this snapshot.")]
	public float SteeringAngle;

	/// <summary>
	/// Kind of replay sample or replay event represented by this snapshot.
	/// </summary>
	[Tooltip("Type of replay sample or event represented by this snapshot.")]
	public SnapshotType Type;

	/// <summary>
	/// Type of replay snapshot.
	/// </summary>
	public enum SnapshotType
	{
		/// <summary>
		/// Regular replay sample recorded during fixed update.
		/// </summary>
		FixedUpdate,

		/// <summary>
		/// Snapshot recorded at a checkpoint event.
		/// </summary>
		Checkpoint,

		/// <summary>
		/// Snapshot marking the start of the race.
		/// </summary>
		RaceStart,

		/// <summary>
		/// Snapshot marking the end of the race.
		/// </summary>
		RaceEnd
	}

	/// <summary>
	/// Creates a replay snapshot from recorded vehicle and race state.
	/// </summary>
	/// <param name="trackTime">Replay time, in seconds, for this snapshot.</param>
	/// <param name="trackPosition">Recorded vehicle position.</param>
	/// <param name="trackRotation">Recorded vehicle rotation.</param>
	/// <param name="currentLap">Lap index at the time of recording.</param>
	/// <param name="vehicleSpeed">Vehicle speed at the time of recording.</param>
	/// <param name="steeringAngle">Steering angle at the time of recording.</param>
	/// <param name="type">Snapshot type describing whether this is a regular sample or replay event.</param>
	public ReplaySnapshot(float trackTime, Vector3 trackPosition, Quaternion trackRotation, int currentLap, float vehicleSpeed, float steeringAngle, SnapshotType type)
	{
		TrackTime = trackTime;
		TrackPosition = trackPosition;
		TrackRotation = trackRotation;
		LapIndex = currentLap;
		VehicleSpeed = vehicleSpeed;
		SteeringAngle = steeringAngle;
		Type = type;
	}
}

/// <summary>
/// Serializable race replay containing an ordered list of recorded <see cref="ReplaySnapshot"/> values.
/// </summary>
/// <remarks>
/// @ingroup replay_system
/// @brief Stores replay duration metadata and the recorded snapshots used for replay playback or preview.
///
/// A replay can be built incrementally with <see cref="AddSnapshot"/>, copied with <see cref="Copy"/>,
/// or cleared with <see cref="Reset"/>.
/// </remarks>
[Serializable]
public class Replay
{
	/// <summary>
	/// Overall replay/race time in seconds.
	/// </summary>
	[Tooltip("Overall replay time in seconds.")]
	public float OverallTime;

	/// <summary>
	/// Recorded replay snapshots.
	/// </summary>
	[Tooltip("Recorded replay snapshots used to reconstruct replay playback.")]
	public List<ReplaySnapshot> Snapshots = new List<ReplaySnapshot>();

	/// <summary>
	/// Creates an empty replay.
	/// </summary>
	public Replay()
	{
		Snapshots = new List<ReplaySnapshot>();
	}

	/// <summary>
	/// Creates a replay from an overall time and a snapshot list.
	/// </summary>
	/// <param name="overallTime">Overall replay time in seconds.</param>
	/// <param name="snapshots">Snapshots to copy into this replay. If null, an empty list is used.</param>
	public Replay(float overallTime, List<ReplaySnapshot> snapshots)
	{
		OverallTime = overallTime;
		Snapshots = snapshots != null ? new List<ReplaySnapshot>(snapshots) : new List<ReplaySnapshot>();
	}

	/// <summary>
	/// Creates a copy of another replay.
	/// </summary>
	/// <param name="other">Replay to copy. If null, an empty replay is created.</param>
	public Replay(Replay other)
	{
		OverallTime = other != null ? other.OverallTime : 0f;
		Snapshots = other != null ? new List<ReplaySnapshot>(other.Snapshots) : new List<ReplaySnapshot>();
	}

	/// <summary>
	/// Gets the effective replay duration.
	/// </summary>
	/// <remarks>
	/// If snapshots exist, this returns the larger value between <see cref="OverallTime"/> and the last
	/// snapshot time. If no snapshots exist, it returns <see cref="OverallTime"/>.
	/// </remarks>
	public float Duration
	{
		get
		{
			if (Snapshots.Count == 0)
			{
				return OverallTime;
			}

			return Mathf.Max(OverallTime, Snapshots[Snapshots.Count - 1].TrackTime);
		}
	}

	/// <summary>
	/// Adds a new snapshot to the replay and updates <see cref="OverallTime"/> if needed.
	/// </summary>
	/// <param name="trackTime">Replay time, in seconds, for the new snapshot.</param>
	/// <param name="trackPosition">Recorded vehicle position.</param>
	/// <param name="trackRotation">Recorded vehicle rotation.</param>
	/// <param name="currentLap">Lap index at the time of recording.</param>
	/// <param name="currentSpeed">Vehicle speed at the time of recording.</param>
	/// <param name="steeringAngle">Steering angle at the time of recording.</param>
	/// <param name="snapshotType">Snapshot type describing whether this is a regular sample or replay event.</param>
	public void AddSnapshot(float trackTime, Vector3 trackPosition, Quaternion trackRotation, int currentLap, float currentSpeed, float steeringAngle, SnapshotType snapshotType)
	{
		Snapshots.Add(new ReplaySnapshot(trackTime, trackPosition, trackRotation, currentLap, currentSpeed, steeringAngle, snapshotType));
		OverallTime = Mathf.Max(OverallTime, trackTime);
	}

	/// <summary>
	/// Creates a copy of this replay.
	/// </summary>
	/// <returns>A new <see cref="Replay"/> instance containing copied replay data.</returns>
	public Replay Copy()
	{
		return new Replay(this);
	}

	/// <summary>
	/// Clears this replay and resets its overall time to zero.
	/// </summary>
	public void Reset()
	{
		OverallTime = 0f;
		Snapshots.Clear();
	}
}