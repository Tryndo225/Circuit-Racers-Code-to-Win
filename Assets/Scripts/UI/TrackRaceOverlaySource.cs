using UnityEngine;

public class TrackRaceOverlaySource : RaceOverlaySource
{
	[SerializeField] private TrackManager trackManager;

	private void OnValidate()
	{
		if (trackManager == null)
		{
			trackManager = TrackManager.Instance;
		}
	}

	private void Start()
	{
		if (trackManager == null)
		{
			trackManager = TrackManager.Instance;
		}
	}

	public override bool TryGetState(out RaceOverlayState state)
	{
		state = new RaceOverlayState();

		if (trackManager == null || RaceTimeManager.Instance == null)
		{
			return false;
		}

		int totalCheckpoints = 0;

		if (CheckPointManager.Instance != null)
		{
			totalCheckpoints = CheckPointManager.Instance.TotalCheckpoints;
		}

		state.HasData = true;
		state.RespawnTimer = trackManager.RespawnTimer;

		state.LapTime = RaceTimeManager.Instance.GetCurrentLapTime();
		state.TrackTime = RaceTimeManager.Instance.GetCurrentRaceTime();
		state.ShowTrackTime = trackManager.CurrentLap > 0;

		state.LapCounterText = $"{trackManager.CurrentLap}/{trackManager.TotalLaps}";
		state.CheckpointCounterText = $"{trackManager.CurrentCheckPointIndex}/{totalCheckpoints}";

		state.IsFinished = trackManager.IsRaceFinished;
		state.FinalText = $"Final Time: {RaceOverLay.FormatTime(RaceTimeManager.Instance.RaceEndTime)}";

		return true;
	}
}