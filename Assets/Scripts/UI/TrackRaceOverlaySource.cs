using UnityEngine;

/// <summary>
/// Race overlay state source that feeds live track/race data into <see cref="RaceOverlay"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Adapts <see cref="TrackManager"/>, <see cref="RaceTimeManager"/>, and <see cref="CheckPointManager"/>
/// data to the generic <see cref="RaceOverlaySource"/> API.
///
/// This source is used during normal gameplay. It collects countdown, lap timing, total race timing,
/// lap counter, checkpoint counter, and finish-state information, then packages it into a
/// <see cref="RaceOverlayState"/> for the HUD.
/// </remarks>
public class TrackRaceOverlaySource : RaceOverlaySource
{
	/// <summary>
	/// Track manager that provides race progress, lap count, checkpoint index, respawn timer, and finish state.
	/// </summary>
	[Tooltip("Track manager used as the live gameplay data source for the race overlay.")]
	[SerializeField] private TrackManager trackManager;

	/// <summary>
	/// Best time achieved on track before current attemp. Used for final time difference calculation.
	/// </summary>
	[ReadOnly, SerializeField] private float previousBestTime;

	/// <summary>
	/// Editor-time validation that backfills <see cref="trackManager"/> from the scene singleton when possible.
	/// </summary>
	private void OnValidate()
	{
		if (trackManager == null)
		{
			trackManager = TrackManager.Instance;
		}
	}

	/// <summary>
	/// Runtime initialization that backfills <see cref="trackManager"/> from the scene singleton when possible.
	/// </summary>
	private void Start()
	{
		if (trackManager == null)
		{
			trackManager = TrackManager.Instance;
		}

		previousBestTime = GameDataManager.Instance.CurrentGameData.GetBestLevelTime(GameDataManager.Instance.CurrentLevelMap);
	}

	/// <summary>
	/// Attempts to build a race overlay state from the current live gameplay state.
	/// </summary>
	/// <param name="state">
	/// Overlay state populated from <see cref="trackManager"/>, <see cref="RaceTimeManager"/>,
	/// and <see cref="CheckPointManager"/> when the required data is available.
	/// </param>
	/// <returns>
	/// <c>true</c> if the required live race data exists and <paramref name="state"/> was populated;
	/// otherwise <c>false</c>.
	/// </returns>
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

		if (trackManager.IsRaceFinished)
		{
			if (previousBestTime != float.MaxValue)
			{
				float timeDiff = RaceTimeManager.Instance.RaceEndTime - previousBestTime;
				char diffSign = timeDiff > 0 ? '+' : '-';
				state.FinalText = $"Final Time:\n{RaceOverlay.FormatTime(RaceTimeManager.Instance.RaceEndTime)} ({diffSign}{RaceOverlay.FormatTime(Mathf.Abs(timeDiff))})";
			}
			else
			{
				state.FinalText = $"Final Time:\n{RaceOverlay.FormatTime(RaceTimeManager.Instance.RaceEndTime)}";
			}
		}

		return true;
	}
}