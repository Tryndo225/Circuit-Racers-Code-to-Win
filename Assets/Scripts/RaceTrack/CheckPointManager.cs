using Generic;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry and activation controller for checkpoints in the current track scene.
/// </summary>
/// <remarks>
/// @ingroup track_mng
/// @brief Stores ordered <see cref="CheckPointListener"/> instances and provides helper methods for enabling,
/// disabling, registering listeners, and rebuilding the checkpoint list from the scene.
///
/// This manager does not decide race progression by itself. It provides shared checkpoint operations for
/// systems that need to activate checkpoints, deactivate them, listen to checkpoint claims, or discover all
/// checkpoint listeners placed in the scene.
/// </remarks>
public class CheckPointManager : SceneSingleton<CheckPointManager>
{
	/// <summary>
	/// Ordered list of checkpoints managed for the current scene.
	/// </summary>
	public List<CheckPointListener> CheckPoints { get; private set; } = new List<CheckPointListener>();

	/// <summary>
	/// Gets the number of registered checkpoints.
	/// </summary>
	public int TotalCheckpoints => CheckPoints.Count;

	/// <summary>
	/// Activates the checkpoint at the given index.
	/// </summary>
	/// <param name="index">Index of the checkpoint in <see cref="CheckPoints"/>.</param>
	public void ActivateCheckpoint(int index)
	{
		CheckPoints[index].SetActive(true);
	}

	/// <summary>
	/// Deactivates the checkpoint at the given index.
	/// </summary>
	/// <param name="index">Index of the checkpoint in <see cref="CheckPoints"/>.</param>
	public void DeactivateCheckpoint(int index)
	{
		CheckPoints[index].SetActive(false);
	}

	/// <summary>
	/// Deactivates all registered checkpoints.
	/// </summary>
	public void DeactivateAllCheckpoints()
	{
		foreach (var checkpoint in CheckPoints)
		{
			checkpoint.SetActive(false);
		}
	}

	/// <summary>
	/// Adds a checkpoint to the managed checkpoint list.
	/// </summary>
	/// <param name="checkpoint">Checkpoint listener to register.</param>
	public void AddCheckPoint(CheckPointListener checkpoint)
	{
		CheckPoints.Add(checkpoint);
	}

	/// <summary>
	/// Removes a checkpoint from the managed checkpoint list.
	/// </summary>
	/// <param name="checkpoint">Checkpoint listener to unregister.</param>
	public void RemoveCheckPoint(CheckPointListener checkpoint)
	{
		CheckPoints.Remove(checkpoint);
	}

	/// <summary>
	/// Registers the same callback on all currently managed checkpoints.
	/// </summary>
	/// <param name="callback">Callback invoked when any registered checkpoint is claimed.</param>
	public void AddListenerToCheckpoints(System.Action callback)
	{
		foreach (var checkpoint in CheckPoints)
		{
			checkpoint.AddListener(callback);
		}
	}

	/// <summary>
	/// Removes the same callback from all currently managed checkpoints.
	/// </summary>
	/// <param name="callback">Callback to remove from each registered checkpoint.</param>
	public void RemoveListenerFromCheckpoints(System.Action callback)
	{
		foreach (var checkpoint in CheckPoints)
		{
			checkpoint.RemoveListener(callback);
		}
	}

	/// <summary>
	/// Clears the managed checkpoint list without destroying checkpoint objects.
	/// </summary>
	public void ClearCheckPoints()
	{
		CheckPoints.Clear();
	}

	/// <summary>
	/// Finds all <see cref="CheckPointListener"/> instances in the scene and registers them in checkpoint order.
	/// </summary>
	/// <remarks>
	/// Existing entries are cleared first. Found checkpoints are sorted by
	/// <see cref="CheckPointListener.CheckpointOrder"/> before being added to <see cref="CheckPoints"/>.
	/// </remarks>
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