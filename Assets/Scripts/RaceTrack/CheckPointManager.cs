using Generic;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CheckPointManager : Singleton<CheckPointManager>
{
	public List<CheckPointListener> CheckPoints { get; private set; } = new List<CheckPointListener>();
	public int TotalCheckpoints => CheckPoints.Count;

	public void ActivateCheckpoint(int index)
	{
		CheckPoints[index].SetActive(true);
	}

	public void DeactivateCheckpoint(int index)
	{
		CheckPoints[index].SetActive(false);
	}

	public void DeactivateAllCheckpoints()
	{
		foreach (var checkpoint in CheckPoints)
		{
			checkpoint.SetActive(false);
		}
	}

	public void AddCheckPoint(CheckPointListener checkpoint)
	{
		CheckPoints.Add(checkpoint);
	}

	public void RemoveCheckPoint(CheckPointListener checkpoint)
	{
		CheckPoints.Remove(checkpoint);
	}

	public void AddListenerToCheckpoints(System.Action callback)
	{
		foreach (var checkpoint in CheckPoints)
		{
			checkpoint.AddListener(callback);
		}
	}

	public void RemoveListenerFromCheckpoints(System.Action callback)
	{
		foreach (var checkpoint in CheckPoints)
		{
			checkpoint.RemoveListener(callback);
		}
	}

	public void ClearCheckPoints()
	{
		CheckPoints.Clear();
	}

	public void AutoAddCheckpoints()
	{
		CheckPoints.Clear();
		var checkpointsInScene = FindObjectsByType<CheckPointListener>(FindObjectsSortMode.None);
		Array.Sort(checkpointsInScene, (a, b) => a.CheckpointOrder.CompareTo(b.CheckpointOrder));

		foreach (var checkpoint in checkpointsInScene)
		{
			CheckPoints.Add(checkpoint);
		}
	}
}