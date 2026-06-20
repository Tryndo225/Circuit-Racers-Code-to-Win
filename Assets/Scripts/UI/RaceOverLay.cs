using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

	#endregion Inspector : Start Overlay

	#region Inspector : Splits

	[SerializeField] private GameObject cpSplitScreen;
	[SerializeField] private Image cpPanelOverlay;
	[SerializeField] private TMP_Text cpTimeText;
	[SerializeField] private TMP_Text cpDiffText;
	[SerializeField] private float cpVisibleTime;
	[SerializeField] private float cpFadeDuration;

	#endregion Inspector : Splits

	#region Inspector : Timers

	/// <summary>Label for the current lap's running time.</summary>
	[SerializeField] private TMP_Text lapTime;

	/// <summary>Label for the total running time of the track (from race start).</summary>
	[SerializeField] private TMP_Text trackTime;

	#endregion Inspector : Timers

	#region Inspector : Counters

	/// <summary>Label for the current lap and total laps (e.g., "1/3").</summary>
	[SerializeField] private TMP_Text lapCounter;

	/// <summary>Label for the current checkpoint index and total count (e.g., "2/8").</summary>
	[SerializeField] private TMP_Text checkPointCount;

	#endregion Inspector : Counters

	#region Inspector : Finish Screen

	/// <summary>Finish/results panel shown when the race is completed.</summary>
	[SerializeField] private GameObject finishScreen;

	/// <summary>Label for the final total time shown on the finish screen.</summary>
	[SerializeField] private TMP_Text finalTime;

	#endregion Inspector : Finish Screen

	#region References

	/// <summary>Source of race state. Auto-assigned via <see cref="FindFirstObjectByType{T}"/> when missing.</summary>
	[SerializeField, ReadOnly] private TrackManager trackManager;

	#endregion References

	#region Private Members

	/// <summary>Tracks whether Overlay was shown to avoid repeated toggling.</summary>
	private bool _toggled = false;

	private Coroutine _fadeCoroutine = null;

	private Graphic[] _splitGraphics;
	private Color[] _splitBaseColors;

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
		_toggled = false;
		startFilter.SetActive(false);
		finishScreen.SetActive(false);

		_splitGraphics = new Graphic[] { cpPanelOverlay, cpTimeText, cpDiffText };
		_splitBaseColors = StoreBaseColors(_splitGraphics);
		RestoreBaseColors(_splitGraphics, _splitBaseColors);

		cpSplitScreen.SetActive(false);
	}

	/// <summary>
	/// Per-frame UI sync: updates countdown/overlay, times, counters, and finish panel
	/// from the current <see cref="TrackManager"/> state.
	/// </summary>
	private void Update()
	{
		if (!_toggled)
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

			lapTime.text = FormatTime(RaceTimeManager.Instance.GetCurrentLapTime());

			if (trackManager.CurrentLap > 0)
			{
				trackTime.text = FormatTime(RaceTimeManager.Instance.GetCurrentRaceTime());
			}
			else
			{
				trackTime.text = "";
			}

			lapCounter.text = $"{trackManager.CurrentLap}/{trackManager.TotalLaps}";
			checkPointCount.text = $"{trackManager.CurrentCheckPointIndex}/{CheckPointManager.Instance.TotalCheckpoints}";

			if (trackManager.IsRaceFinished)
			{
				finishScreen.SetActive(true);
				finalTime.text = $"Final Time: {FormatTime(RaceTimeManager.Instance.RaceEndTime)}";
				_toggled = true;
			}
			else if (Input.GetKeyDown(KeyCode.Escape) && !finishScreen.activeSelf)
			{
				finishScreen.SetActive(true);
				finalTime.text = $"Unfinished";
				_toggled = true;
			}
			else
			{
				finalTime.text = "";
				finishScreen.SetActive(false);
			}
		}
	}

	#endregion Unity Methods

	#region Public API

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

	private static Color[] StoreBaseColors(Graphic[] graphics)
	{
		Color[] colors = new Color[graphics.Length];

		for (int i = 0; i < graphics.Length; ++i)
		{
			colors[i] = graphics[i].color;
		}

		return colors;
	}

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
	/// Formats seconds into "HH:MM:SS.mmm" when hours &gt; 0, otherwise "MM:SS.mmm".
	/// </summary>
	/// <param name="time">Seconds to format.</param>
	/// <returns>Human-readable time string.</returns>
	private static string FormatTime(float time)
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