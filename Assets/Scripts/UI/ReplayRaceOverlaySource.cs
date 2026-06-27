using UnityEngine;

public class ReplayRaceOverlaySource : RaceOverlaySource
{
	[SerializeField] private ReplayPreviewer replayPreviewer;

	private void OnValidate()
	{
		if (replayPreviewer == null)
		{
			replayPreviewer = ReplayPreviewer.Instance;
		}
	}

	private void Start()
	{
		if (replayPreviewer == null)
		{
			replayPreviewer = ReplayPreviewer.Instance;
		}
	}

	public override bool TryGetState(out RaceOverlayState state)
	{
		state = new RaceOverlayState();

		if (replayPreviewer == null || !replayPreviewer.HasReplay)
		{
			return false;
		}

		state.HasData = true;
		state.RespawnTimer = 0f;

		state.LapTime = replayPreviewer.CurrentReplayTime;
		state.TrackTime = replayPreviewer.CurrentReplayTime;
		state.ShowTrackTime = true;

		state.LapCounterText = "Replay";
		state.CheckpointCounterText = "";

		state.IsFinished = replayPreviewer.IsReplayFinished;
		state.FinalText = $"Replay Finished: {RaceOverLay.FormatTime(replayPreviewer.ReplayDuration)}";

		return true;
	}
}
