using UnityEngine;
using TMPro;
using System;

public class RaceOverLay : MonoBehaviour
{
    [SerializeField] private GameObject startFilter;
    [SerializeField] private TMP_Text startTimer;

    [SerializeField] private TMP_Text lapTime;
    [SerializeField] private TMP_Text trackTime;

    [SerializeField] private TMP_Text lapCounter;
    [SerializeField] private TMP_Text checkPointCount;

    [SerializeField] private GameObject finishScreen;
    [SerializeField] private TMP_Text finalTime;

    [SerializeField, ReadOnly] private TrackManager trackManager;

    private void OnValidate()
    {
        if (trackManager == null)
        {
            trackManager = FindFirstObjectByType<TrackManager>();
        }
    }

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

    private static string FormatTime(float time)
    {
        TimeSpan t = TimeSpan.FromSeconds(time);
        if (t.Hours > 0)
            return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", t.Hours, t.Minutes, t.Seconds, t.Milliseconds);
        else
            return string.Format("{0:D2}:{1:D2}.{2:D3}", t.Minutes, t.Seconds, t.Milliseconds);
    }
}