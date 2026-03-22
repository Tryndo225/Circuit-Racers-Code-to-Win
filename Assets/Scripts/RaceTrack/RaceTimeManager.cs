using Generic;
using System.Collections.Generic;
using UnityEngine;

public class RaceTimeManager : Singleton<RaceTimeManager>
{
	public float RaceStartTime { get; private set; }
	public float LapStartTime { get; private set; }
	public float LastCheckpointTime => CheckPointSplitsTimes.Count > 0 ? CheckPointSplitsTimes[CheckPointSplitsTimes.Count - 1] : 0;
	public float LastLapTime => LapTimes.Count > 0 ? LapTimes[LapTimes.Count - 1] : 0;
	public float RaceEndTime { get; private set; }

	public bool IsRaceFinished => RaceEndTime > 0;
	public List<float> LapTimes { get; private set; } = new List<float>();
	public List<float> CheckPointSplitsTimes { get; private set; } = new List<float>();

	public void RaceStart()
	{
		RaceStartTime = Time.time;
		LapStartTime = RaceStartTime;
		RaceEndTime = 0;

		LapTimes.Clear();
		CheckPointSplitsTimes.Clear();
		CheckPointManager.Instance.AddListenerToCheckpoints(CheckpointPassed);
	}

	public void LapPassed()
	{
		if (LapStartTime > 0)
		{
			LapTimes.Add(Time.time - LapStartTime);
		}
		LapStartTime = Time.time;
	}

	public void CheckpointPassed()
	{
		float currentCpSplit = GetCurrentRaceTime();
		CheckPointSplitsTimes.Add(currentCpSplit);

		float lastSplitTime = GameDataManager.Instance.GetCurrentMapSplit(CheckPointSplitsTimes.Count - 1);
		var rcO = FindFirstObjectByType<RaceOverLay>();

		if (lastSplitTime == 0)
			rcO.DisplaySplit(currentCpSplit, 0);
		else
			rcO.DisplaySplit(currentCpSplit, currentCpSplit - lastSplitTime);
	}

	public float GetCurrentRaceTime()
	{
		if (!IsRaceFinished)
			return Time.time - RaceStartTime;
		return RaceEndTime;
	}

	public float GetCurrentLapTime()
	{
		return Time.time - LapStartTime;
	}

	public float RaceEnd()
	{
		LapPassed();
		float totalRaceTime = Time.time - RaceStartTime;
		Debug.Log($"Race finished! Total time: {totalRaceTime} seconds");
		GameDataManager.Instance.CompleteLevel(totalRaceTime, CheckPointSplitsTimes.ToArray());
		RaceStartTime = 0f;
		LapStartTime = 0f;
		RaceEndTime = totalRaceTime;
		return totalRaceTime;
	}
}
