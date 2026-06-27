using System;
using System.Collections.Generic;
using UnityEngine;
using static ReplaySnapshot;

[Serializable]
public struct ReplaySnapshot
{
	public float TrackTime;
	public Vector3 TrackPosition;
	public Quaternion TrackRotation;
	public int LapIndex;
	public float VehicleSpeed;
	public float SteeringAngle;
	public SnapshotType Type;

	public enum SnapshotType
	{
		FixedUpdate,
		Checkpoint,
		RaceStart,
		RaceEnd
	}

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

[Serializable]
public class Replay
{
	public float OverallTime;
	public List<ReplaySnapshot> Snapshots = new List<ReplaySnapshot>();

	public Replay()
	{
		Snapshots = new List<ReplaySnapshot>();
	}

	public Replay(float overallTime, List<ReplaySnapshot> snapshots)
	{
		OverallTime = overallTime;
		Snapshots = snapshots != null ? new List<ReplaySnapshot>(snapshots) : new List<ReplaySnapshot>();
	}

	public Replay(Replay other)
	{
		OverallTime = other != null ? other.OverallTime : 0f;
		Snapshots = other != null ? new List<ReplaySnapshot>(other.Snapshots) : new List<ReplaySnapshot>();
	}

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

	public void AddSnapshot(float trackTime, Vector3 trackPosition, Quaternion trackRotation, int currentLap, float currentSpeed, float steeringAngle, SnapshotType snapshotType)
	{
		Snapshots.Add(new ReplaySnapshot(trackTime, trackPosition, trackRotation, currentLap, currentSpeed, steeringAngle, snapshotType));
		OverallTime = Mathf.Max(OverallTime, trackTime);
	}

	public Replay Copy()
	{
		return new Replay(this);
	}

	public void Reset()
	{
		OverallTime = 0f;
		Snapshots.Clear();
	}
}