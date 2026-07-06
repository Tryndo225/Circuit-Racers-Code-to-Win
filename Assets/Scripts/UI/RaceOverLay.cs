using Generic;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Snapshot of race information displayed by <see cref="RaceOverlay"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Transfers timing, counter, countdown, and finish-state data from a race state source to the HUD.
///
/// The overlay uses this struct so the UI does not need to know whether the data comes from
/// normal gameplay, replay playback, or another implementation of <see cref="RaceOverlaySource"/>.
/// </remarks>
public struct RaceOverlayState
{
	/// <summary>
	/// Whether this state contains valid data that should be displayed.
	/// </summary>
	public bool HasData;

	/// <summary>
	/// Remaining respawn/countdown time. Values greater than zero display the start overlay.
	/// </summary>
	public float RespawnTimer;

	/// <summary>
	/// Current lap time in seconds.
	/// </summary>
	public float LapTime;

	/// <summary>
	/// Total track time in seconds.
	/// </summary>
	public float TrackTime;

	/// <summary>
	/// Whether <see cref="TrackTime"/> should currently be displayed.
	/// </summary>
	public bool ShowTrackTime;

	/// <summary>
	/// Preformatted lap counter text, for example <c>1/3</c>.
	/// </summary>
	public string LapCounterText;

	/// <summary>
	/// Preformatted checkpoint counter text, for example <c>2/8</c>.
	/// </summary>
	public string CheckpointCounterText;

	/// <summary>
	/// Whether the race has finished and the finish screen should be shown.
	/// </summary>
	public bool IsFinished;

	/// <summary>
	/// Final result text shown on the finish screen.
	/// </summary>
	public string FinalText;
}

/// <summary>
/// HUD controller for race status, countdown, timing, checkpoint splits, counters, and final results.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Non-intrusive overlay that displays race progress from a <see cref="RaceOverlaySource"/>.
///
/// Responsibilities:
/// - Show a full-screen filter and countdown during restarts or respawns.
/// - Display current lap time and total elapsed track time.
/// - Display lap and checkpoint counters.
/// - Display checkpoint split feedback with fade-out.
/// - Show a finish/results screen when the race ends.
/// - Allow the player to manually open an unfinished result screen with Escape.
///
/// Dependencies:
/// - <see cref="RaceOverlaySource"/> for race or replay state.
/// - <see cref="CheckPointManager"/> for hiding checkpoints when the finish screen is shown.
/// - TextMeshPro labels and UI panels assigned in the Inspector.
///
/// Threading:
/// - Unity main thread only.
/// - UI is synchronized during <see cref="Update"/>.
/// </remarks>
public class RaceOverlay : SceneSingleton<RaceOverlay>
{
	#region Inspector : Start Overlay

	/// <summary>
	/// Panel/filter that covers the screen during countdown or respawn.
	/// </summary>
	[Tooltip("Panel or filter shown during race countdown or respawn.")]
	[SerializeField] private GameObject startFilter;

	/// <summary>
	/// Countdown text rendered on top of <see cref="startFilter"/>.
	/// </summary>
	[Tooltip("Text label that displays the countdown or respawn timer.")]
	[SerializeField] private TMP_Text startTimer;

	#endregion Inspector : Start Overlay

	#region Inspector : Splits

	/// <summary>
	/// Panel used to display checkpoint split information.
	/// </summary>
	[Tooltip("Panel shown temporarily when a checkpoint split is displayed.")]
	[SerializeField] private GameObject cpSplitScreen;

	/// <summary>
	/// Background/overlay image used by the checkpoint split panel.
	/// </summary>
	[Tooltip("Background image of the checkpoint split panel.")]
	[SerializeField] private Image cpPanelOverlay;

	/// <summary>
	/// Text label showing the current checkpoint split time.
	/// </summary>
	[Tooltip("Text label showing the checkpoint split time.")]
	[SerializeField] private TMP_Text cpTimeText;

	/// <summary>
	/// Text label showing the difference compared to the reference split.
	/// </summary>
	[Tooltip("Text label showing whether the checkpoint split is faster or slower.")]
	[SerializeField] private TMP_Text cpDiffText;

	/// <summary>
	/// Time, in seconds, that the checkpoint split remains fully visible before fading.
	/// </summary>
	[Tooltip("Time in seconds that the checkpoint split stays visible before fading out.")]
	[SerializeField] private float cpVisibleTime;

	/// <summary>
	/// Time, in seconds, used to fade out the checkpoint split panel.
	/// </summary>
	[Tooltip("Time in seconds used to fade out the checkpoint split panel.")]
	[SerializeField] private float cpFadeDuration;

	#endregion Inspector : Splits

	#region Inspector : Timers

	/// <summary>
	/// Label for the current lap's running time.
	/// </summary>
	[Tooltip("Text label displaying the current lap time.")]
	[SerializeField] private TMP_Text lapTime;

	/// <summary>
	/// Label for the total running time of the track.
	/// </summary>
	[Tooltip("Text label displaying the total track time.")]
	[SerializeField] private TMP_Text trackTime;

	#endregion Inspector : Timers

	#region Inspector : Counters

	/// <summary>
	/// Label for the current lap and total laps.
	/// </summary>
	[Tooltip("Text label displaying the current lap counter.")]
	[SerializeField] private TMP_Text lapCounter;

	/// <summary>
	/// Label for the current checkpoint index and total checkpoint count.
	/// </summary>
	[Tooltip("Text label displaying the current checkpoint counter.")]
	[SerializeField] private TMP_Text checkPointCount;

	#endregion Inspector : Counters

	#region Inspector : Finish Screen

	/// <summary>
	/// Finish/results panel shown when the race is completed or abandoned.
	/// </summary>
	[Tooltip("Finish/results panel shown when the race ends or is manually stopped.")]
	[SerializeField] private GameObject finishScreen;

	/// <summary>
	/// Label for the final total time shown on the finish screen.
	/// </summary>
	[Tooltip("Text label displaying the final race result.")]
	[SerializeField] private TMP_Text finalTime;

	#endregion Inspector : Finish Screen

	#region References

	/// <summary>
	/// Source that provides race overlay state for normal gameplay or replay playback.
	/// </summary>
	[Tooltip("Source object that provides race or replay state to the overlay.")]
	[SerializeField] private RaceOverlaySource overlaySource;

	#endregion References

	#region Private Members

	/// <summary>
	/// Tracks whether the finish overlay has already been shown to avoid repeated toggling.
	/// </summary>
	private bool _toggled = false;

	/// <summary>
	/// Currently running checkpoint split fade coroutine, if any.
	/// </summary>
	private Coroutine _fadeCoroutine = null;

	/// <summary>
	/// Graphics affected by checkpoint split fade-out.
	/// </summary>
	private Graphic[] _splitGraphics;

	/// <summary>
	/// Original colors of checkpoint split graphics, used to restore alpha after fading.
	/// </summary>
	private Color[] _splitBaseColors;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Editor-time validation that backfills <see cref="overlaySource"/> if it is not assigned.
	/// </summary>
	private void OnValidate()
	{
		if (overlaySource == null)
		{
			overlaySource = FindFirstObjectByType<RaceOverlaySource>();
		}
	}

	/// <summary>
	/// Initializes overlay references and clears all UI labels/panels to their initial hidden state.
	/// </summary>
	private void Start()
	{
		if (overlaySource == null)
		{
			overlaySource = FindFirstObjectByType<RaceOverlaySource>();
		}

		lapCounter.text = "";
		checkPointCount.text = "";
		lapTime.text = "";
		trackTime.text = "";
		finalTime.text = "";
		startTimer.text = "";

		_toggled = false;

		startFilter.SetActive(false);
		finishScreen.SetActive(false);

		_splitGraphics = new Graphic[] { cpPanelOverlay, cpTimeText, cpDiffText };
		_splitBaseColors = StoreBaseColors(_splitGraphics);
		RestoreBaseColors(_splitGraphics, _splitBaseColors);

		cpSplitScreen.SetActive(false);
	}

	/// <summary>
	/// Synchronizes overlay UI with the current <see cref="RaceOverlayState"/> once per frame.
	/// </summary>
	/// <remarks>
	/// No UI is updated when the finish screen has already been toggled, when no source is assigned,
	/// or when the source reports that it has no valid data.
	/// </remarks>
	private void Update()
	{
		if (_toggled)
		{
			return;
		}

		if (overlaySource == null)
		{
			return;
		}

		if (!overlaySource.TryGetState(out RaceOverlayState state) || !state.HasData)
		{
			return;
		}

		UpdateStartOverlay(state);
		UpdateTimers(state);
		UpdateCounters(state);
		UpdateFinishScreen(state);
	}

	/// <summary>
	/// Updates the countdown/respawn overlay from the current state.
	/// </summary>
	/// <param name="state">Race overlay state to display.</param>
	private void UpdateStartOverlay(RaceOverlayState state)
	{
		if (state.RespawnTimer > 0f)
		{
			_toggled = false;
			startFilter.SetActive(true);
			startTimer.text = $"{state.RespawnTimer:0}";
		}
		else
		{
			startTimer.text = "";
			startFilter.SetActive(false);
		}
	}

	/// <summary>
	/// Updates lap and total track time labels.
	/// </summary>
	/// <param name="state">Race overlay state containing the timer values.</param>
	private void UpdateTimers(RaceOverlayState state)
	{
		lapTime.text = FormatTime(state.LapTime);
		trackTime.text = state.ShowTrackTime ? FormatTime(state.TrackTime) : "";
	}

	/// <summary>
	/// Updates lap and checkpoint counter labels.
	/// </summary>
	/// <param name="state">Race overlay state containing preformatted counter text.</param>
	private void UpdateCounters(RaceOverlayState state)
	{
		lapCounter.text = state.LapCounterText ?? "";
		checkPointCount.text = state.CheckpointCounterText ?? "";
	}

	/// <summary>
	/// Updates the finish screen and handles manual unfinished completion through Escape.
	/// </summary>
	/// <param name="state">Race overlay state containing finish information.</param>
	private void UpdateFinishScreen(RaceOverlayState state)
	{
		if (state.IsFinished)
		{
			finishScreen.SetActive(true);
			CheckPointManager.Instance.DeactivateAllCheckpoints();
			finalTime.text = state.FinalText;
			_toggled = true;
		}
		else if (Input.GetKeyDown(KeyCode.Escape) && !finishScreen.activeSelf)
		{
			finishScreen.SetActive(true);
			CheckPointManager.Instance.DeactivateAllCheckpoints();
			finalTime.text = "Unfinished";
			_toggled = true;
		}
		else
		{
			finalTime.text = "";
			finishScreen.SetActive(false);
		}
	}

	#endregion Unity Methods

	#region Public API

	/// <summary>
	/// Displays a checkpoint split popup with the split time and difference from the reference split.
	/// </summary>
	/// <param name="splitTime">Current split time in seconds.</param>
	/// <param name="splitDiff">Difference from the reference split in seconds. Positive is slower, negative is faster.</param>
	/// <remarks>
	/// Any currently running split fade is cancelled before the new split is shown.
	/// The difference text is colored red for slower, green for faster, and blue for equal.
	/// </remarks>
	public void DisplaySplit(float splitTime, float splitDiff)
	{
		if (_fadeCoroutine != null)
		{
			StopCoroutine(_fadeCoroutine);
			_fadeCoroutine = null;
		}

		RestoreBaseColors(_splitGraphics, _splitBaseColors);

		SetCheckpointDifferenceColor(splitDiff);

		cpTimeText.text = FormatTime(splitTime);

		char symbol = splitDiff > 0 ? '+' : '-';
		cpDiffText.text = $"{symbol}{FormatTime(Mathf.Abs(splitDiff))}";

		cpSplitScreen.SetActive(true);

		_fadeCoroutine = StartCoroutine(FadeOutCoroutine(cpVisibleTime, cpFadeDuration, _splitGraphics, _splitBaseColors, cpSplitScreen));
	}

	#endregion Public API

	#region Helper

	#region Coroutine

	/// <summary>
	/// Keeps the checkpoint split graphics visible for a duration, fades their alpha to zero, then hides the panel.
	/// </summary>
	/// <param name="timeVisible">Time in seconds to keep the split panel fully visible.</param>
	/// <param name="fadeTime">Time in seconds used for fade-out.</param>
	/// <param name="graphics">Graphics whose alpha should be faded.</param>
	/// <param name="baseColors">Original colors used as the fade source and restore target.</param>
	/// <param name="gameObject">Panel GameObject to disable after fading.</param>
	/// <returns>Coroutine enumerator used by Unity.</returns>
	private static IEnumerator FadeOutCoroutine(float timeVisible, float fadeTime, Graphic[] graphics, Color[] baseColors, GameObject gameObject)
	{
		float startTime = Time.time;

		while (Time.time - startTime < timeVisible)
		{
			yield return null;
		}

		startTime = Time.time;

		while (Time.time - startTime < fadeTime)
		{
			float t = Mathf.Clamp01((Time.time - startTime) / fadeTime);
			float alphaCoefficient = Mathf.Lerp(1f, 0f, t);

			for (int i = 0; i < graphics.Length; ++i)
			{
				Color color = baseColors[i];
				color.a = baseColors[i].a * alphaCoefficient;
				graphics[i].color = color;
			}

			yield return null;
		}

		gameObject.SetActive(false);

		RestoreBaseColors(graphics, baseColors);
	}

	#endregion Coroutine

	/// <summary>
	/// Sets the checkpoint difference text color based on whether the player is faster, slower, or equal.
	/// </summary>
	/// <param name="splitDiff">Difference from the reference split in seconds.</param>
	private void SetCheckpointDifferenceColor(float splitDiff)
	{
		Color color;

		if (splitDiff > 0)
		{
			color = Color.red;
		}
		else if (splitDiff < 0)
		{
			color = Color.green;
		}
		else
		{
			color = Color.blue;
		}

		color.a = _splitBaseColors[2].a;

		cpDiffText.color = color;
		_splitBaseColors[2] = color;
	}

	/// <summary>
	/// Stores the current colors of a list of graphics.
	/// </summary>
	/// <param name="graphics">Graphics whose colors should be copied.</param>
	/// <returns>Array containing the current color of each graphic.</returns>
	private static Color[] StoreBaseColors(Graphic[] graphics)
	{
		Color[] colors = new Color[graphics.Length];

		for (int i = 0; i < graphics.Length; ++i)
		{
			colors[i] = graphics[i].color;
		}

		return colors;
	}

	/// <summary>
	/// Restores graphics to previously stored colors.
	/// </summary>
	/// <param name="graphics">Graphics to restore.</param>
	/// <param name="baseColors">Colors that should be assigned back to the graphics.</param>
	private static void RestoreBaseColors(Graphic[] graphics, Color[] baseColors)
	{
		for (int i = 0; i < graphics.Length; ++i)
		{
			graphics[i].color = baseColors[i];
		}
	}

	#endregion Helper

	#region Formatting

	/// <summary>
	/// Formats seconds into a compact race timer string.
	/// </summary>
	/// <param name="time">Seconds to format.</param>
	/// <returns>
	/// Time formatted as <c>HH:MM:SS.mmm</c> when hours are present,
	/// <c>MM:SS.mmm</c> when minutes are present, otherwise <c>SS.mmm</c>.
	/// </returns>
	public static string FormatTime(float time)
	{
		TimeSpan t = TimeSpan.FromSeconds(time);
		if (t.Hours > 0)
			return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", t.Hours, t.Minutes, t.Seconds, t.Milliseconds);
		else if (t.Minutes > 0)
			return string.Format("{0:D2}:{1:D2}.{2:D3}", t.Minutes, t.Seconds, t.Milliseconds);
		else
			return string.Format("{0:D2}.{1:D3}", t.Seconds, t.Milliseconds);
	}

	#endregion Formatting
}