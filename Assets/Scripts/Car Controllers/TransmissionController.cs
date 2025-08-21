using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TransmissionController : MonoBehaviour
{
    [Header("Transmission & RPM")]
    [Tooltip("Forward gear ratios (1..N)")]
    public float[] forwardGears;

    public float finalDrive;

    [Tooltip("Engine idle RPM")]
    public float idleRPM;

    [Tooltip("Engine redline RPM (max RPM)")]
    public float redlineRPM;

    [Tooltip("Auto shift up when RPM exceeds this")]
    public float shiftUpRPM;

    [Tooltip("Auto shift down when RPM falls below this")]
    public float shiftDownRPM;

    [Tooltip("Seconds torque is cut during a shift")]
    public float shiftDuration;

    [Tooltip("Delegetes for shifting event")]
    public List<System.Action> OnShift;

    private bool _isShifting = false;

    public int CurrentGear { get; private set; } = 0;
    public float EngineRPM { get; private set; } = 0f;

    public bool HandleShifting(float wheelRPM)

    {
        if (_isShifting)
        {
            return true;
        }

        float rpm = CalculateRPM(wheelRPM);
        EngineRPM = rpm;

        if (rpm >= shiftUpRPM && CurrentGear < forwardGears.Length - 1)
        {
            StartCoroutine(ShiftUp());
            return true;
        }
        else if (rpm <= shiftDownRPM && CurrentGear > 0)
        {
            StartCoroutine(ShiftDown());
            return true;
        }

        return false;
    }

    // --- Trasmission Logic Helper Methods ---
    private float CurrentGearRatio()
    {
        return forwardGears[CurrentGear];
    }

    private float CalculateRPM(float wheelRPM)
    {
        // engine rpm = wheel rpm * (gear * final drive)
        float gr = Mathf.Abs(CurrentGearRatio()) * Mathf.Abs(finalDrive);
        float engine = wheelRPM * gr;

        // keep above idle, clamp to redline (tiny headroom)
        engine = Mathf.Max(idleRPM, engine);
        engine = Mathf.Min(engine, redlineRPM);

        return engine;
    }

    // --- Coroutine Methods for Shifting ---
    private IEnumerator ShiftUp()
    {
        if (_isShifting || CurrentGear >= forwardGears.Length - 1)
            yield break;

        _isShifting = true;

        foreach (var action in OnShift)
        {
            action?.Invoke();
        }

        float originalRPM = EngineRPM;

        for (float t = 0; t < shiftDuration; t += Time.deltaTime)
        {
            EngineRPM = Mathf.Lerp(originalRPM, idleRPM, t / shiftDuration);
            yield return null;
        }

        // Shift up
        CurrentGear++;
        _isShifting = false;
    }

    private IEnumerator ShiftDown()
    {
        if (_isShifting || CurrentGear <= 0)
            yield break;

        _isShifting = true;

        foreach (var action in OnShift)
        {
            action?.Invoke();
        }

        float originalRPM = EngineRPM;

        for (float t = 0; t < shiftDuration; t += Time.deltaTime)
        {
            EngineRPM = Mathf.Lerp(originalRPM, redlineRPM, t / shiftDuration);
            yield return null;
        }

        // Shift down
        CurrentGear--;
        _isShifting = false;
    }
}