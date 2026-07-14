using UnityEngine;

/// <summary>
/// Race overlay state source that feeds replay playback data into <see cref="RaceOverlay"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup replay_system
/// @brief Adapts <see cref="ReplayPreviewer"/> timing and completion state to the generic <see cref="RaceOverlaySource"/> API.
///
/// This source is used when the race HUD is displayed during replay playback instead of live gameplay.
/// It reports replay time as both lap time and track time, labels the lap counter as <c>Replay</c>,
/// and marks the overlay as finished when the replay previewer reaches the end of the replay.
/// </remarks>
public class ReplayRaceOverlaySource : RaceOverlaySource
{
	/// <summary>
	/// Replay previewer that provides replay timing and completion state.
	/// </summary>
	[Tooltip("Replay previewer used as the data source for the race overlay during replay playback.")]
	[SerializeField] private ReplayPreviewer replayPreviewer;

	/// <summary>
	/// Editor-time validation that backfills <see cref="replayPreviewer"/> from the scene singleton when possible.
	/// </summary>
	private void OnValidate()
	{
		if (replayPreviewer == null)
		{
			replayPreviewer = ReplayPreviewer.Instance;
		}
	}

	/// <summary>
	/// Runtime initialization that backfills <see cref="replayPreviewer"/> from the scene singleton when possible.
	/// </summary>
	private void Start()
	{
		if (replayPreviewer == null)
		{
			replayPreviewer = ReplayPreviewer.Instance;
		}
	}

	/// <summary>
	/// Attempts to build a race overlay state from the current replay playback data.
	/// </summary>
	/// <param name="state">Overlay state populated from <see cref="replayPreviewer"/> when replay data is available.</param>
	/// <returns>
	/// <c>true</c> if a replay previewer exists and currently has replay data; otherwise <c>false</c>.
	/// </returns>
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
		state.FinalText = $"Replay Finished:\n{RaceOverlay.FormatTime(replayPreviewer.ReplayDuration)}";

		return true;
	}
}