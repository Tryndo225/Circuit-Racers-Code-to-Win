using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple automatic transmission controller for a vehicle.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Computes engine RPM from wheel RPM, handles automatic gear changes, and exposes shift callbacks.
///
/// The controller maps driven-wheel RPM through the current gear ratio and final drive to estimate
/// engine RPM. It then shifts up or down based on configured RPM thresholds, as long as wheel slip
/// is below the configured slip threshold.
///
/// Behaviour:
/// - Keeps track of the current forward gear.
/// - Calculates clamped engine RPM.
/// - Starts upshift/downshift coroutines when thresholds are reached.
/// - Temporarily reports torque cut while shifting.
/// - Invokes registered shift callbacks when a shift starts.
///
/// Requirements:
/// - <see cref="forwardGears"/> should contain at least one gear ratio.
/// - <see cref="idleRPM"/> should be less than or equal to <see cref="redlineRPM"/>.
/// - <see cref="shiftDownRPM"/> should usually be lower than <see cref="shiftUpRPM"/>.
///
/// Threading:
/// - Unity main thread only.
/// - Shift timing uses Unity coroutines.
/// </remarks>
public class TransmissionController : MonoBehaviour
{
	#region Inspector: Transmission & RPM

	[Header("Transmission & RPM")]
	/// <summary>
	/// Forward gear ratios.
	/// </summary>
	/// <remarks>
	/// Index 0 is first gear.
	/// </remarks>
	[Tooltip("Forward gear ratios. Index 0 is first gear.")]
	public float[] forwardGears;

	/// <summary>
	/// Final drive ratio.
	/// </summary>
	/// <remarks>
	/// Multiplies the selected forward gear ratio when calculating engine RPM.
	/// </remarks>
	[Tooltip("Final drive ratio. Multiplies the selected gear ratio.")]
	public float finalDrive;

	/// <summary>
	/// Engine idle RPM.
	/// </summary>
	[Tooltip("Engine idle RPM.")]
	public float idleRPM;

	/// <summary>
	/// Engine redline RPM.
	/// </summary>
	[Tooltip("Engine redline RPM.")]
	public float redlineRPM;

	/// <summary>
	/// RPM threshold used for automatic upshifts.
	/// </summary>
	[Tooltip("Automatic shift-up threshold in RPM.")]
	public float shiftUpRPM;

	/// <summary>
	/// RPM threshold used for automatic downshifts.
	/// </summary>
	[Tooltip("Automatic shift-down threshold in RPM.")]
	public float shiftDownRPM;

	/// <summary>
	/// Duration in seconds during which torque is cut while shifting.
	/// </summary>
	[Tooltip("Duration in seconds during which torque is cut while shifting.")]
	public float shiftDuration;

	/// <summary>
	/// Wheel-slip threshold above which shifting is suppressed.
	/// </summary>
	[Tooltip("Wheel-slip threshold above which automatic shifting is suppressed.")]
	public float slipThreshold;

	/// <summary>
	/// Runtime callbacks invoked when a shift starts.
	/// </summary>
	/// <remarks>
	/// This list is intended for runtime registration. Unity does not serialize delegate lists
	/// as normal Inspector events.
	/// </remarks>
	[Tooltip("Runtime callbacks invoked when a shift starts.")]
	public List<System.Action> OnShift;

	#endregion

	#region State & Properties

	/// <summary>
	/// Whether a shift coroutine is currently active.
	/// </summary>
	private bool _isShifting = false;

	/// <summary>
	/// Gets the zero-based index of the current gear.
	/// </summary>
	public int CurrentGear { get; private set; } = 0;

	/// <summary>
	/// Gets the current calculated engine RPM.
	/// </summary>
	/// <remarks>
	/// This value is mapped from wheel RPM through the current gear and final drive,
	/// then clamped between <see cref="idleRPM"/> and <see cref="redlineRPM"/>.
	/// </remarks>
	public float EngineRPM { get; private set; } = 0f;

	#endregion

	#region Public API

	/// <summary>
	/// Updates engine RPM and handles automatic shifting.
	/// </summary>
	/// <param name="wheelRPM">Average RPM of grounded driven wheels.</param>
	/// <param name="wheelSlip">Average wheel slip measure.</param>
	/// <returns>
	/// <c>true</c> if drive torque should be cut because a shift is active or has just started;
	/// otherwise <c>false</c>.
	/// </returns>
	/// <remarks>
	/// Shifting is suppressed while wheel slip is above <see cref="slipThreshold"/>.
	/// </remarks>
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

	/// <summary>
	/// Gets the current engine RPM normalized between idle and redline.
	/// </summary>
	/// <returns>Normalized RPM value, usually in the range 0 to 1.</returns>
	public float GetNormalizedRPM()
	{
		return Mathf.InverseLerp(idleRPM, redlineRPM, EngineRPM);
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Gets the ratio of the current forward gear.
	/// </summary>
	/// <returns>Current gear ratio.</returns>
	private float CurrentGearRatio()
	{
		return forwardGears[CurrentGear];
	}

	/// <summary>
	/// Calculates engine RPM from wheel RPM.
	/// </summary>
	/// <param name="wheelRPM">Average driven-wheel RPM.</param>
	/// <returns>Engine RPM clamped between <see cref="idleRPM"/> and <see cref="redlineRPM"/>.</returns>
	/// <remarks>
	/// Engine RPM is estimated as wheel RPM multiplied by the current gear ratio and final drive ratio.
	/// </remarks>
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
	/// Performs an automatic upshift.
	/// </summary>
	/// <returns>Coroutine enumerator.</returns>
	/// <remarks>
	/// Shift callbacks are invoked when the shift starts. During the shift duration,
	/// <see cref="EngineRPM"/> is eased toward idle RPM before the current gear is incremented.
	/// </remarks>
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
	/// Performs an automatic downshift.
	/// </summary>
	/// <returns>Coroutine enumerator.</returns>
	/// <remarks>
	/// Shift callbacks are invoked when the shift starts. During the shift duration,
	/// <see cref="EngineRPM"/> is eased toward redline RPM before the current gear is decremented.
	/// </remarks>
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