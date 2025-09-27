using UnityEngine;
using TMPro;
using System;

/// <summary>
/// HUD controller for race status: start countdown overlay, lap & total time, lap/checkpoint counters,
/// and a final results screen. Polls <see cref="TrackManager"/> each frame and reflects its state in TMP labels.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Non-intrusive overlay; read-only view of gameplay timing/progression.
/// 
/// Responsibilities:
/// - Show a full-screen filter and countdown during restarts/respawns (based on <see cref="TrackManager.RespawnTimer"/>).
/// - Display current lap time and total elapsed track time (after the race has begun).
/// - Display lap counter and current checkpoint index.
/// - Show a finish screen with the final time when the race ends.
/// 
/// Threading:
/// - Unity main thread only (driven by <see cref="Update"/>).
/// 
/// Dependencies:
/// - <see cref="TrackManager"/> for race state.
/// - TextMeshPro (<see cref="TMP_Text"/>) for labels.
/// - Assigned overlay GameObjects in the scene/inspector.
/// 
/// Usage:
/// - Place on a Canvas GameObject.
/// - Wire all serialized fields in the Inspector (labels and overlay panels).
/// - Optionally leave <see cref="trackManager"/> unassigned; it will be discovered on validate/start.
/// </remarks>
public class RaceOverLay : MonoBehaviour
{
    #region Inspector : Start Overlay

    /// <summary>Panel/filter that covers the screen during countdown/respawn.</summary>
    [SerializeField] private GameObject startFilter;

    /// <summary>Countdown text rendered on top of <see cref="startFilter"/>.</summary>
    [SerializeField] private TMP_Text startTimer;

    #endregion

    #region Inspector : Timers

    /// <summary>Label for the current lap's running time.</summary>
    [SerializeField] private TMP_Text lapTime;

    /// <summary>Label for the total running time of the track (from race start).</summary>
    [SerializeField] private TMP_Text trackTime;

    #endregion

    #region Inspector : Counters

    /// <summary>Label for the current lap and total laps (e.g., "1/3").</summary>
    [SerializeField] private TMP_Text lapCounter;

    /// <summary>Label for the current checkpoint index and total count (e.g., "2/8").</summary>
    [SerializeField] private TMP_Text checkPointCount;

    #endregion

    #region Inspector : Finish Screen

    /// <summary>Finish/results panel shown when the race is completed.</summary>
    [SerializeField] private GameObject finishScreen;

    /// <summary>Label for the final total time shown on the finish screen.</summary>
    [SerializeField] private TMP_Text finalTime;

    #endregion

    #region References

    /// <summary>Source of race state. Auto-assigned via <see cref="FindFirstObjectByType{T}"/> when missing.</summary>
    [SerializeField, ReadOnly] private TrackManager trackManager;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Editor-time validation: backfills <see cref="trackManager"/> if not assigned.
    /// </summary>
    private void OnValidate()
    {
        if (trackManager == null)
        {
            trackManager = FindFirstObjectByType<TrackManager>();
        }
    }

    /// <summary>
    /// Initializes references (if needed) and clears/initializes all UI elements to hidden/empty.
    /// </summary>
    private void Start()
    {
        if (trackManager == null)
        {
            trackManager = FindFirstObjectByType<TrackManager>();
        }

        lapCounter.text = "";
        checkPointCount.text = "";
        lapTime.text = "";
        trackTime.text = "";
        finalTime.text = "";
        startTimer.text = "";
        startFilter.SetActive(false);
        finishScreen.SetActive(false);
    }

    /// <summary>
    /// Per-frame UI sync: updates countdown/overlay, times, counters, and finish panel
    /// from the current <see cref="TrackManager"/> state.
    /// </summary>
    private void Update()
    {
        if (trackManager == null) return;
        if (trackManager.RespawnTimer > 0)
        {
            startFilter.SetActive(true);
            startTimer.text = $"{trackManager.RespawnTimer:0}";
        }
        else
        {
            startTimer.text = "";
            startFilter.SetActive(false);
        }

        lapTime.text = FormatTime(trackManager.CurrentLapTime);

        if (trackManager.CurrentLap > 0)
        {
            trackTime.text = FormatTime(trackManager.TotalTrackTime);
        }
        else
        {
            trackTime.text = "";
        }

        lapCounter.text = $"{trackManager.CurrentLap}/{trackManager.TotalLaps}";
        checkPointCount.text = $"{trackManager.CurrentCheckPointIndex}/{trackManager.TotalCheckPoints}";

        if (trackManager.IsRaceFinished)
        {
            finishScreen.SetActive(true);
            finalTime.text = $"Final Time: {FormatTime(trackManager.TotalTrackTime)}";
        }
        else
        {
            finalTime.text = "";
            finishScreen.SetActive(false);
        }
    }

    #endregion

    #region Formatting

    /// <summary>
    /// Formats seconds into "HH:MM:SS.mmm" when hours &gt; 0, otherwise "MM:SS.mmm".
    /// </summary>
    /// <param name="time">Seconds to format.</param>
    /// <returns>Human-readable time string.</returns>
    private static string FormatTime(float time)
    {
        TimeSpan t = TimeSpan.FromSeconds(time);
        if (t.Hours > 0)
            return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", t.Hours, t.Minutes, t.Seconds, t.Milliseconds);
        else
            return string.Format("{0:D2}:{1:D2}.{2:D3}", t.Minutes, t.Seconds, t.Milliseconds);
    }

    #endregion
}
