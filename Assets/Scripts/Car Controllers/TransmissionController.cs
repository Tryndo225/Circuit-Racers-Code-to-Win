using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Simple automatic transmission: computes engine RPM from wheel RPM and current gear,
/// auto-shifts up/down based on RPM thresholds and slip, and exposes shift coroutines
/// that briefly cut torque while firing shift callbacks.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @invariant forwardGears is non-null and length >= 1.
/// @invariant 0 <= CurrentGear < forwardGears.Length.
/// @invariant idleRPM <= redlineRPM and shiftDownRPM < shiftUpRPM (recommended).
/// @thread Unity main thread (coroutines run on the main thread).
/// </remarks>
public class TransmissionController : MonoBehaviour
{
    #region Inspector: Transmission & RPM

    [Header("Transmission & RPM")]
    /// <summary>Forward gear ratios (index 0 is first gear).</summary>
    [Tooltip("Forward gear ratios (1..N)")]
    public float[] forwardGears;

    /// <summary>Final drive ratio (multiplies the selected gear ratio).</summary>
    public float finalDrive;

    /// <summary>Engine idle RPM.</summary>
    [Tooltip("Engine idle RPM")]
    public float idleRPM;

    /// <summary>Engine redline RPM (maximum RPM).</summary>
    [Tooltip("Engine redline RPM (max RPM)")]
    public float redlineRPM;

    /// <summary>Auto shift-up threshold (RPM).</summary>
    [Tooltip("Auto shift up when RPM exceeds this")]
    public float shiftUpRPM;

    /// <summary>Auto shift-down threshold (RPM).</summary>
    [Tooltip("Auto shift down when RPM falls below this")]
    public float shiftDownRPM;

    /// <summary>Seconds during which torque is cut while shifting.</summary>
    [Tooltip("Seconds torque is cut during a shift")]
    public float shiftDuration;

    /// <summary>Slip threshold above which shifting is suppressed.</summary>
    [Tooltip("Slip threshold for shifting")]
    public float slipThreshold;

    /// <summary>Callbacks invoked each time a shift starts (up or down).</summary>
    [Tooltip("Delegetes for shifting event")]
    public List<System.Action> OnShift;

    #endregion

    #region State & Properties

    /// <summary>True while a shift is in progress.</summary>
    private bool _isShifting = false;

    /// <summary>Zero-based index of the current gear.</summary>
    public int CurrentGear { get; private set; } = 0;

    /// <summary>Current engine RPM after mapping from wheel RPM and clamping.</summary>
    public float EngineRPM { get; private set; } = 0f;

    #endregion

    #region Public API

    /// <summary>
    /// Updates RPM and decides whether to start an upshift/downshift based on thresholds and slip.
    /// Returns true if torque should be cut this frame (during shift or when starting one).
    /// </summary>
    /// <param name="wheelRPM">Average RPM of driven wheels (grounded).</param>
    /// <param name="wheelSlip">Average slip measure; higher means more slip.</param>
    /// <returns>True if torque should be cut.</returns>
    public bool HandleShifting(float wheelRPM, float wheelSlip)
    {
        if (_isShifting)
        {
            return true;
        }

        EngineRPM = CalculateRPM(wheelRPM);

        if (wheelSlip > slipThreshold)
        {
            return false;
        }

        if (EngineRPM >= shiftUpRPM && CurrentGear < forwardGears.Length - 1)
        {
            StartCoroutine(ShiftUp());
            return true;
        }
        else if (EngineRPM <= shiftDownRPM && CurrentGear > 0)
        {
            StartCoroutine(ShiftDown());
            return true;
        }

        return false;
    }

    #endregion

    #region Private Helpers

    /// <summary>Returns the ratio of the current forward gear.</summary>
    private float CurrentGearRatio()
    {
        return forwardGears[CurrentGear];
    }

    /// <summary>
    /// Computes engine RPM from wheel RPM via current gear and final drive, then clamps to [idleRPM, redlineRPM].
    /// </summary>
    private float CalculateRPM(float wheelRPM)
    {
        // engine rpm = wheel rpm * (gear * final drive)
        float gr = Mathf.Abs(CurrentGearRatio()) * Mathf.Abs(finalDrive);
        float engine = wheelRPM * gr;

        // keep above idle, clamp to redline
        engine = Mathf.Max(idleRPM, engine);
        engine = Mathf.Min(engine, redlineRPM);

        return engine;
    }

    #endregion

    #region Coroutines: Shifting

    /// <summary>
    /// Starts an upshift: invokes OnShift callbacks, eases EngineRPM toward idle during shiftDuration,
    /// then increments CurrentGear and clears shifting flag.
    /// </summary>
    private IEnumerator ShiftUp()
    {
        if (_isShifting || CurrentGear >= forwardGears.Length - 1)
            yield break;

        _isShifting = true;

        if (OnShift != null)
        {
            foreach (var action in OnShift)
            {
                action?.Invoke();
            }
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

    /// <summary>
    /// Starts a downshift: invokes OnShift callbacks, eases EngineRPM toward redline during shiftDuration,
    /// then decrements CurrentGear and clears shifting flag.
    /// </summary>
    private IEnumerator ShiftDown()
    {
        if (_isShifting || CurrentGear <= 0)
            yield break;

        _isShifting = true;

        if (OnShift != null)
        {
            foreach (var action in OnShift)
            {
                action?.Invoke();
            }
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

    #endregion
}
