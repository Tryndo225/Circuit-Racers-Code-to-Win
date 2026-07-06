using Generic;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages race timing, lap timing, checkpoint splits, and race completion.
/// </summary>
/// <remarks>
/// @ingroup track_mng
/// @brief Tracks race progress times and forwards completed race data to replay and game-data systems.
///
/// This manager records:
/// - Race start time.
/// - Lap start and lap durations.
/// - Checkpoint split times.
/// - Final race time.
///
/// It also connects checkpoint events to split recording, starts replay recording when the race begins,
/// saves the replay when the race ends, and passes completed race results to <see cref="GameDataManager"/>.
/// </remarks>
public class RaceTimeManager : SceneSingleton<RaceTimeManager>
{
	/// <summary>
	/// Unity time at which the current race started.
	/// </summary>
	public float RaceStartTime { get; private set; }

	/// <summary>
	/// Unity time at which the current lap started.
	/// </summary>
	public float LapStartTime { get; private set; }

	/// <summary>
	/// Gets the most recent checkpoint split time.
	/// </summary>
	/// <remarks>
	/// Returns zero when no checkpoint split has been recorded yet.
	/// </remarks>
	public float LastCheckpointTime => CheckPointSplitsTimes.Count > 0 ? CheckPointSplitsTimes[CheckPointSplitsTimes.Count - 1] : 0;

	/// <summary>
	/// Gets the most recent completed lap time.
	/// </summary>
	/// <remarks>
	/// Returns zero when no lap time has been recorded yet.
	/// </remarks>
	public float LastLapTime => LapTimes.Count > 0 ? LapTimes[LapTimes.Count - 1] : 0;

	/// <summary>
	/// Final race duration after the race has ended.
	/// </summary>
	public float RaceEndTime { get; private set; }

	/// <summary>
	/// Gets whether the race has already finished.
	/// </summary>
	public bool IsRaceFinished => RaceEndTime > 0;

	/// <summary>
	/// Completed lap times recorded during the current race.
	/// </summary>
	public List<float> LapTimes { get; private set; } = new List<float>();

	/// <summary>
	/// Checkpoint split times recorded during the current race.
	/// </summary>
	public List<float> CheckPointSplitsTimes { get; private set; } = new List<float>();

	/// <summary>
	/// Starts a new race timer and prepares race-related timing state.
	/// </summary>
	/// <remarks>
	/// Clears previous lap and checkpoint split data, registers checkpoint split callbacks,
	/// and starts replay recording through <see cref="ReplayManager"/>.
	/// </remarks>
	public void RaceStart()
	{
		RaceStartTime = Time.time;
		LapStartTime = RaceStartTime;
		RaceEndTime = 0;

		LapTimes.Clear();
		CheckPointSplitsTimes.Clear();
		CheckPointManager.Instance.AddListenerToCheckpoints(CheckpointPassed);
		ReplayManager.Instance.StartReplay();
	}

	/// <summary>
	/// Records the current lap duration and starts timing the next lap.
	/// </summary>
	public void LapPassed()
	{
		if (LapStartTime > 0)
		{
			LapTimes.Add(Time.time - LapStartTime);
		}
		LapStartTime = Time.time;
	}

	/// <summary>
	/// Records a checkpoint split for the current race.
	/// </summary>
	/// <remarks>
	/// The split is displayed through <see cref="RaceOverlay"/>. When previous saved split data exists,
	/// the displayed difference compares the current split against the stored split for the same checkpoint index.
	/// </remarks>
	public void CheckpointPassed()
	{
		if (IsRaceFinished)
			return;

		float currentCpSplit = GetCurrentRaceTime();
		CheckPointSplitsTimes.Add(currentCpSplit);

		float lastSplitTime = GameDataManager.Instance.GetCurrentMapSplit(CheckPointSplitsTimes.Count - 1);

		if (lastSplitTime == 0)
			RaceOverlay.Instance.DisplaySplit(currentCpSplit, 0);
		else
			RaceOverlay.Instance.DisplaySplit(currentCpSplit, currentCpSplit - lastSplitTime);
	}

	/// <summary>
	/// Gets the current race time.
	/// </summary>
	/// <returns>
	/// Elapsed race time while the race is active, or the stored final race time after the race has finished.
	/// </returns>
	public float GetCurrentRaceTime()
	{
		if (!IsRaceFinished)
			return Time.time - RaceStartTime;
		return RaceEndTime;
	}

	/// <summary>
	/// Gets the elapsed time of the current lap.
	/// </summary>
	/// <returns>Elapsed time since <see cref="LapStartTime"/>.</returns>
	public float GetCurrentLapTime()
	{
		return Time.time - LapStartTime;
	}

	/// <summary>
	/// Finishes the race, saves race results, and returns the final race time.
	/// </summary>
	/// <returns>Total race duration in seconds.</returns>
	/// <remarks>
	/// Records the final lap time, saves the replay through <see cref="ReplayManager"/>,
	/// and sends the completed result to <see cref="GameDataManager"/>.
	/// </remarks>
	public float RaceEnd()
	{
		if (LapStartTime > 0)
		{
			LapTimes.Add(Time.time - LapStartTime);
		}
		float totalRaceTime = Time.time - RaceStartTime;
		Debug.Log($"Race finished! Total time: {totalRaceTime} seconds");
		Replay replay = ReplayManager.Instance.SaveReplay();
		GameDataManager.Instance.CompleteLevel(totalRaceTime, CheckPointSplitsTimes.ToArray(), replay);
		RaceEndTime = totalRaceTime;
		return totalRaceTime;
	}
}