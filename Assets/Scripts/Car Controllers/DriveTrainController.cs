using UnityEngine;

/// <summary>
/// Serializable tuning container for one Unity <see cref="WheelFrictionCurve"/>.
/// </summary>
/// <remarks>
/// The values are copied into a <see cref="WheelCollider"/> friction curve by
/// <see cref="DriveTrainController.ApplyFrictionSettings"/>. Keeping them in a struct
/// makes the inspector safer than using positional float arrays.
/// </remarks>
[System.Serializable]
public struct WheelFrictionSettings
{
	/// <summary>
	/// Global multiplier applied to the whole friction curve.
	/// Higher values increase the total available grip.
	/// </summary>
	public float stiffness;

	/// <summary>
	/// Slip value at which the tire reaches peak grip.
	/// </summary>
	public float extremumSlip;

	/// <summary>
	/// Peak grip value at <see cref="extremumSlip"/>.
	/// </summary>
	public float extremumValue;

	/// <summary>
	/// Slip value at which the tire reaches its sliding-grip region.
	/// This should usually be greater than <see cref="extremumSlip"/>.
	/// </summary>
	public float asymptoteSlip;

	/// <summary>
	/// Grip value when the tire is already sliding heavily.
	/// This should usually be lower than <see cref="extremumValue"/>.
	/// </summary>
	public float asymptoteValue;
}

/// <summary>
/// Handles steering, throttle, braking, anti-roll, dynamic tire-friction modifiers,
/// and wheel-mesh synchronization for a four-wheel vehicle.
/// </summary>
/// <remarks>
/// This component is configured by <c>VehicleController</c> and coordinates with
/// <c>TransmissionController</c>. The class intentionally remains a single Unity component,
/// but the logic is split into smaller private methods so the main control flow is easier to read.
/// </remarks>
public class DriveTrainController : MonoBehaviour
{
	#region Inspector: Vehicle

	[Header("Vehicle")]
	[Tooltip("Maximum speed in km/h. Vehicle will not exceed this speed.")]
	public float maxSpeed;

	[Tooltip("Maximum reverse speed in km/h. Vehicle will not exceed this speed in reverse.")]
	public float maxReverseSpeed;

	[Tooltip("Maximum steering angle in degrees at top speed.")]
	public float maxSteerAngleAtTopSpeed;

	[Tooltip("Maximum steering angle in degrees at 0 speed.")]
	public float maxSteerAngle;

	[Tooltip("Steering speed in degrees per second.")]
	public float steerSpeedDegPerSec;

	[Tooltip("Exponent applied to controller input values (1 = linear).")]
	[SerializeField, Range(1f, 3f)] public float inputExponent = 1.5f;

	[Tooltip("Maximum motor torque budget.")]
	public float maxMotorPower;

	[Tooltip("Maximum brake torque per wheel.")]
	public float maxBrakeTorque;

	[Tooltip("Handbrake torque for rear wheels.")]
	public float handbrakeTorque;

	#endregion

	#region Inspector: Engine / Drivetrain

	[Header("Engine / Drivetrain")]
	[Tooltip("Normalized RPM value at which engine torque starts fading near redline.")]
	[SerializeField, Range(0f, 1f)] public float redlineFadeStart = 0.95f;

	#endregion

	#region Inspector: Stability

	[Header("Stability")]
	[Tooltip("Anti-roll force toggle")]
	public bool antiRollToggle;

	[Tooltip("Anti-roll stiffness for the front axle.")]
	public float antiRollStiffnessFront;

	[Tooltip("Anti-roll stiffness for the rear axle.")]
	public float antiRollStiffnessRear;

	#endregion

	#region Inspector: Traction Control & ABS Limits

	[Header("Traction Control & ABS Limits")]
	[Tooltip("When enabled, driven-wheel torque is reduced when slip exceeds tractionSlipLimit.")]
	public bool tractionControlEnabled;

	[Tooltip("Combined forward/sideways slip above which traction control starts reducing torque.")]
	public float tractionSlipLimit = 0.35f;

	[Tooltip("When enabled, brake torque is reduced when forward braking slip exceeds absSlipLimit.")]
	public bool absEnabled;

	[Tooltip("Forward slip above which ABS starts reducing brake torque.")]
	public float absSlipLimit = 0.35f;

	#endregion

	#region Inspector: Dynamic Grip

	[Header("Dynamic Grip")]
	[Tooltip("Rear sideways grip multiplier while handbrake is held. Lower = easier drift.")]
	public float rearSidewaysGripHandbrakeMultiplier = 0.35f;

	[Tooltip("Rear forward grip multiplier while handbrake is held.")]
	public float rearForwardGripHandbrakeMultiplier = 0.85f;

	[Tooltip("Grip multiplier for front wheels when they are near lock.")]
	public float frontGripLockedMultiplier = 0.45f;

	[Tooltip("Grip multiplier for rear wheels when they are near lock.")]
	public float rearGripLockedMultiplier = 0.60f;

	[Tooltip("Forward slip above this counts as near-lock when braking hard.")]
	public float lockForwardSlipThreshold = 0.35f;

	[Tooltip("Brake torque must exceed this fraction of maxBrakeTorque to count as hard braking.")]
	[Range(0f, 1f)] public float lockBrakeTorqueThreshold = 0.9f;

	#endregion

	#region Inspector: Grip Circle

	[Header("Grip Circle")]
	[Tooltip("When enabled, combined forward/sideways tire usage reduces available grip.")]
	public bool gripCircleEnabled = true;

	[Tooltip("Combined slip value where grip-circle reduction starts.")]
	public float gripCircleStartSlip = 1f;

	[Tooltip("Combined slip value where grip-circle reduction reaches its maximum.")]
	public float gripCircleFullSlip = 2f;

	[Tooltip("Minimum forward grip multiplier when the tire is overloaded.")]
	[Range(0f, 1f)] public float minForwardGripCircleMultiplier = 0.9f;

	[Tooltip("Minimum sideways grip multiplier when the tire is overloaded.")]
	[Range(0f, 1f)] public float minSidewaysGripCircleMultiplier = 0.9f;

	#endregion

	#region Inspector: Forward Friction

	[Header("Forward Friction")]
	[Tooltip("Base forward friction used by front wheels.")]
	public WheelFrictionSettings frontForwardFriction;

	[Tooltip("Base forward friction used by rear wheels.")]
	public WheelFrictionSettings rearForwardFriction;

	#endregion

	#region Inspector: Sideways Friction

	[Header("Sideways Friction")]
	[Tooltip("Base sideways friction used by front wheels.")]
	public WheelFrictionSettings frontSidewaysFriction;

	[Tooltip("Base sideways friction used by rear wheels.")]
	public WheelFrictionSettings rearSidewaysFriction;

	#endregion

	#region Inspector: Limited Slip Differential

	[Header("Limited Slip Differential")]
	[Tooltip("When enabled, driven wheels are torque-balanced to prevent one wheel from spinning much faster than the others.")]
	public bool limitedSlipEnabled = true;

	[Tooltip("Driven-wheel RPM difference above which limited-slip correction starts.")]
	public float limitedSlipStartRpmDifference = 120f;

	[Tooltip("Driven-wheel RPM difference at which limited-slip correction reaches full strength.")]
	public float limitedSlipFullRpmDifference = 700f;

	[Tooltip("Maximum fraction of motor torque removed from an over-spinning driven wheel.")]
	[Range(0f, 1f)] public float limitedSlipMaxTorqueCut = 0.65f;

	[Tooltip("Torque multiplier applied to slower driven wheels when another driven wheel is over-spinning.")]
	[Range(1f, 2f)] public float limitedSlipGripWheelBoost = 1.25f;

	[Tooltip("Optional brake torque applied to an over-spinning driven wheel.")]
	public float limitedSlipBrakeTorque = 250f;

	#endregion

	#region Inspector: Miscellaneous

	[Header("Miscellaneous")]
	[Tooltip("Local center of mass assigned to the vehicle rigidbody.")]
	public Vector3 coMPosition;

	[Tooltip("Simple Ackermann steering multiplier. 1 = neutral, higher exaggerates inside-wheel steering.")]
	public float ackermannFactor = 1f;

	#endregion

	#region State & Dependencies

	/// <summary>Vehicle rigidbody used for speed calculation and anti-roll forces.</summary>
	private Rigidbody _carRigidBody;

	/// <summary>Transmission controller used for shifting and normalized RPM values.</summary>
	private TransmissionController _transmissionController;

	/// <summary>Wheel colliders ordered as front-left, front-right, rear-left, rear-right.</summary>
	private WheelCollider[] _wheelColliders;

	/// <summary>Visual wheel meshes synchronized with the wheel colliders.</summary>
	private Transform[] _wheelMeshes;

	/// <summary>Flags saying which wheels receive motor torque.</summary>
	private bool[] _driven;

	/// <summary>Number of wheels marked as driven.</summary>
	private int _drivenWheelCount;

	/// <summary>Flags saying which wheels receive steering angle.</summary>
	private bool[] _steering;

	/// <summary>True when the car is currently using brake input as reverse throttle.</summary>
	private bool _isReversing = false;

	/// <summary>Current normalized brake input stored for external read-only state.</summary>
	private float _braking = 0f;

	/// <summary>Current steering angle in degrees after input shaping and smoothing.</summary>
	private float steeringAngle = 0f;

	/// <summary>Per-wheel target motor torque state, used by traction-control decay.</summary>
	private float[] _targetThrottlePower;

	/// <summary>Per-wheel target brake torque state, used by ABS decay.</summary>
	private float[] _targetBrakingPower;

	/// <summary>True if the vehicle is currently in reverse mode.</summary>
	public bool Reversing => _isReversing;

	/// <summary>True if the service brake input is currently active.</summary>
	public bool Braking => _braking > 0.1f;

	#endregion

	#region Public API

	/// <summary>
	/// Initializes the drivetrain with required references and wheel configuration.
	/// </summary>
	/// <param name="carRigidBody">Vehicle rigidbody.</param>
	/// <param name="transmissionController">Transmission controller used for shifting and RPM.</param>
	/// <param name="wheelsColliders">Wheel colliders, expected in FL, FR, RL, RR order.</param>
	/// <param name="wheelMeshes">Visual wheel meshes matching <paramref name="wheelsColliders"/>.</param>
	/// <param name="driven">Per-wheel flags saying which wheels receive motor torque.</param>
	/// <param name="steering">Per-wheel flags saying which wheels receive steering angle.</param>
	public void Init(Rigidbody carRigidBody, TransmissionController transmissionController, WheelCollider[] wheelsColliders, Transform[] wheelMeshes, bool[] driven, bool[] steering)
	{
		_carRigidBody = carRigidBody;
		_transmissionController = transmissionController;
		_wheelColliders = wheelsColliders;
		_wheelMeshes = wheelMeshes;
		_driven = driven;
		_steering = steering;

		_drivenWheelCount = CountDrivenWheels();

		_targetThrottlePower = new float[driven.Length];
		_targetBrakingPower = new float[wheelMeshes.Length];
	}

	/// <summary>
	/// Applies initial WheelCollider, rigidbody, and friction settings.
	/// </summary>
	public void SetUp()
	{
		ConfigureWheelColliderSubsteps();
		ConfigureRigidbodySolver();
		SetupAllWheelFriction();

		_carRigidBody.centerOfMass = coMPosition;
	}

	/// <summary>
	/// Applies all wheel-related controls for one physics tick.
	/// </summary>
	/// <param name="throttle">Throttle input, usually in range [0, 1].</param>
	/// <param name="braking">Brake input, usually in range [0, 1].</param>
	/// <param name="handbrake">Whether the handbrake is active.</param>
	/// <param name="steering">Steering input, usually in range [-1, 1].</param>
	/// <param name="gamepadSteering">Whether the steering input comes from an analog gamepad source.</param>
	public void ApplyWheelControls(float throttle, float braking, bool handbrake, float steering, bool gamepadSteering)
	{
		ControlSteering(steering, gamepadSteering);

		braking = ApplyReverseLogic(braking);
		throttle = RemoveThrottleWhileBraking(throttle, braking);

		_braking = braking;

		ApplyDrive(throttle, handbrake);
		ApplyBraking(braking, handbrake);
		ApplyLimitedSlipDifferential();
		ApplyAntiRoll();
		SyncMeshes();
	}

	/// <summary>
	/// Returns current vehicle speed in km/h.
	/// </summary>
	public float GetSpeed()
	{
		return CalculateSpeed();
	}

	/// <summary>
	/// Returns configured maximum forward speed in km/h.
	/// </summary>
	public float GetMaxSpeed()
	{
		return maxSpeed;
	}

	/// <summary>
	/// Returns configured maximum reverse speed in km/h.
	/// </summary>
	public float GetMaxReverseSpeed()
	{
		return maxReverseSpeed;
	}

	/// <summary>
	/// Returns current smoothed steering angle in degrees.
	/// </summary>
	public float GetSteeringAngle()
	{
		return steeringAngle;
	}

	/// <summary>
	/// Returns configured maximum low-speed steering angle in degrees.
	/// </summary>
	public float GetMaxSteeringAngle()
	{
		return maxSteerAngle;
	}

	/// <summary>
	/// Returns configured maximum top-speed steering angle in degrees.
	/// </summary>
	public float GetMaxSteeringAngleAtTopSpeed()
	{
		return maxSteerAngleAtTopSpeed;
	}

	public void ApplyReplayWheelVisuals(float replaySteeringAngle)
	{
		steeringAngle = replaySteeringAngle;

		if (_wheelColliders == null || _wheelMeshes == null || _steering == null)
		{
			return;
		}

		ApplySteering(steeringAngle);
		SyncMeshes();
	}

	#endregion

	#region Private Helpers: Setup

	/// <summary>
	/// Counts how many wheel flags in <see cref="_driven"/> are enabled.
	/// </summary>
	private int CountDrivenWheels()
	{
		int count = 0;

		foreach (bool isDriven in _driven)
		{
			if (isDriven)
				count++;
		}

		return count;
	}

	/// <summary>
	/// Configures Unity's vehicle substeps for all wheel colliders.
	/// </summary>
	private void ConfigureWheelColliderSubsteps()
	{
		foreach (WheelCollider wheel in _wheelColliders)
			wheel.ConfigureVehicleSubsteps(0.5f, 20, 30);
	}

	/// <summary>
	/// Increases rigidbody solver iterations to improve wheel and suspension stability.
	/// </summary>
	private void ConfigureRigidbodySolver()
	{
		_carRigidBody.solverIterations = 12;
		_carRigidBody.solverVelocityIterations = 12;
	}

	#endregion

	#region Private Helpers: Input State

	/// <summary>
	/// Applies reverse-mode switching based on brake input at very low speed.
	/// </summary>
	/// <param name="braking">Original braking input.</param>
	/// <returns>Brake input after reverse handling.</returns>
	private float ApplyReverseLogic(float braking)
	{
		if (ReverseCheck(braking))
			return 0f;

		return braking;
	}

	/// <summary>
	/// Prevents throttle and brake from being applied at the same time.
	/// </summary>
	/// <param name="throttle">Original throttle input.</param>
	/// <param name="braking">Current braking input.</param>
	/// <returns>Throttle input after brake conflict handling.</returns>
	private float RemoveThrottleWhileBraking(float throttle, float braking)
	{
		if (braking > 0.1f)
			return 0f;

		return throttle;
	}

	/// <summary>
	/// Updates and returns whether brake input should be interpreted as reverse.
	/// </summary>
	/// <param name="braking">Current brake input.</param>
	/// <returns>True if the car is in reverse mode after this check.</returns>
	private bool ReverseCheck(float braking)
	{
		bool brakingCheck = braking > 0.1f;

		if (brakingCheck && CalculateSpeed() < 1f)
		{
			_isReversing = true;
			return true;
		}

		if (!brakingCheck && _isReversing)
		{
			_isReversing = false;
			return false;
		}

		return _isReversing;
	}

	#endregion

	#region Private Helpers: Averages & Speed

	/// <summary>
	/// Calculates the average absolute RPM of grounded driven wheels.
	/// </summary>
	/// <returns>Average absolute driven-wheel RPM, or 0 if no driven wheel is grounded.</returns>
	private float AverageGroundedDrivenWheelAbsRPM()
	{
		float sum = 0f;
		int count = 0;

		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_driven[i])
				continue;

			if (_wheelColliders[i].GetGroundHit(out WheelHit hit))
			{
				sum += Mathf.Abs(_wheelColliders[i].rpm);
				count++;
			}
		}

		return count > 0 ? sum / count : 0f;
	}

	/// <summary>
	/// Calculates the average combined slip of grounded driven wheels.
	/// </summary>
	/// <returns>Average combined slip, or 0 if no driven wheel is grounded.</returns>
	private float AverageDrivenWheelSlip()
	{
		float sum = 0f;
		int count = 0;

		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_driven[i])
				continue;

			if (_wheelColliders[i].GetGroundHit(out WheelHit hit))
			{
				sum += CalculateSlipMagnitude(hit);
				count++;
			}
		}

		return count > 0 ? sum / count : 0f;
	}

	/// <summary>
	/// Calculates vehicle body speed in km/h.
	/// </summary>
	private float CalculateSpeed()
	{
		return _carRigidBody.linearVelocity.magnitude * 3.6f;
	}

	/// <summary>
	/// Calculates a simple combined slip magnitude from a wheel contact.
	/// </summary>
	/// <param name="hit">Wheel ground-contact data.</param>
	/// <returns>Absolute forward slip plus absolute sideways slip.</returns>
	private float CalculateSlipMagnitude(WheelHit hit)
	{
		return Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);
	}

	/// <summary>
	/// Determines whether a wheel index belongs to the front axle.
	/// </summary>
	private bool IsFrontWheel(int wheelIndex)
	{
		return wheelIndex < 2;
	}

	/// <summary>
	/// Determines whether a wheel index belongs to the rear axle.
	/// </summary>
	private bool IsRearWheel(int wheelIndex)
	{
		return wheelIndex >= 2;
	}

	#endregion

	#region Private Helpers: Steering

	/// <summary>
	/// Shapes steering input, limits steering angle by speed, and applies the resulting wheel angles.
	/// </summary>
	/// <param name="steerInput">Raw steering input in range [-1, 1].</param>
	/// <param name="gamepadSteering">Whether analog gamepad shaping should be used.</param>
	private void ControlSteering(float steerInput, bool gamepadSteering)
	{
		float maxAngle = CalculateSpeedLimitedSteeringAngle();
		float targetAngle = CalculateTargetSteeringAngle(steerInput, maxAngle, gamepadSteering);

		steeringAngle = Mathf.MoveTowards(steeringAngle, targetAngle, steerSpeedDegPerSec * Time.fixedDeltaTime);

		ApplySteering(steeringAngle);
	}

	/// <summary>
	/// Calculates the maximum allowed steering angle for the current vehicle speed.
	/// </summary>
	private float CalculateSpeedLimitedSteeringAngle()
	{
		float kph = CalculateSpeed();
		float speedCoefficient = Mathf.InverseLerp(0f, maxSpeed, kph);

		return Mathf.Lerp(maxSteerAngle, maxSteerAngleAtTopSpeed, speedCoefficient);
	}

	/// <summary>
	/// Calculates the desired steering angle from raw input and current speed limit.
	/// </summary>
	/// <param name="steerInput">Raw steering input.</param>
	/// <param name="maxAngle">Maximum steering angle allowed at current speed.</param>
	/// <param name="gamepadSteering">Whether to apply analog input shaping.</param>
	private float CalculateTargetSteeringAngle(float steerInput, float maxAngle, bool gamepadSteering)
	{
		if (!gamepadSteering)
			return steerInput * maxAngle;

		float shaped = Mathf.Sign(steerInput) * Mathf.Pow(Mathf.Abs(steerInput), inputExponent);
		return shaped * maxAngle;
	}

	/// <summary>
	/// Applies final steering angles to all wheels marked as steering wheels.
	/// </summary>
	/// <param name="steer">Smoothed steering angle in degrees.</param>
	private void ApplySteering(float steer)
	{
		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_steering[i])
				continue;

			_wheelColliders[i].steerAngle = CalculateAckermannAdjustedAngle(i, steer);
		}
	}

	/// <summary>
	/// Applies a simplified inside/outside-wheel Ackermann multiplier to a steering angle.
	/// </summary>
	/// <param name="wheelIndex">Wheel index in FL, FR, RL, RR order.</param>
	/// <param name="steer">Base steering angle in degrees.</param>
	/// <returns>Ackermann-adjusted steering angle.</returns>
	private float CalculateAckermannAdjustedAngle(int wheelIndex, float steer)
	{
		if (ackermannFactor == 1f || Mathf.Abs(steer) <= 0.01f)
			return steer;

		bool isLeftWheel = wheelIndex % 2 == 0;
		bool turningLeft = steer < 0f;

		bool insideWheel = (turningLeft && isLeftWheel) || (!turningLeft && !isLeftWheel);

		float multiplier = insideWheel ? 1f + 0.1f * ackermannFactor : 1f - 0.1f * ackermannFactor;

		return steer * multiplier;
	}

	#endregion

	#region Private Helpers: Drive

	/// <summary>
	/// Applies motor torque to all driven wheels.
	/// </summary>
	/// <param name="throttle">Throttle input after brake/reverse preprocessing.</param>
	/// <param name="handbrake">Whether the handbrake is active.</param>
	private void ApplyDrive(float throttle, bool handbrake)
	{
		throttle = ApplyDriveInputModifiers(throttle);

		if (ShouldCutDriveTorque())
			throttle = 0f;

		for (int i = 0; i < _driven.Length; i++)
		{
			if (!_driven[i])
				continue;

			ApplyDriveToWheel(i, throttle, handbrake);
		}
	}

	/// <summary>
	/// Applies drivetrain-level modifiers to throttle, such as redline torque fade.
	/// </summary>
	/// <param name="throttle">Original throttle input.</param>
	/// <returns>Modified throttle input.</returns>
	private float ApplyDriveInputModifiers(float throttle)
	{
		return throttle * CalculateRedlineMultiplier();
	}

	/// <summary>
	/// Determines whether drivetrain torque should be fully cut this physics tick.
	/// </summary>
	private bool ShouldCutDriveTorque()
	{
		return CalculateSpeed() > maxSpeed || (_isReversing && CalculateSpeed() > maxReverseSpeed) || _transmissionController.HandleShifting(AverageGroundedDrivenWheelAbsRPM(), AverageDrivenWheelSlip());
	}

	/// <summary>
	/// Applies motor torque to one driven wheel.
	/// </summary>
	/// <param name="wheelIndex">Index of the driven wheel.</param>
	/// <param name="throttle">Current throttle input.</param>
	/// <param name="handbrake">Whether the handbrake is active.</param>
	private void ApplyDriveToWheel(int wheelIndex, float throttle, bool handbrake)
	{
		WheelCollider wheel = _wheelColliders[wheelIndex];

		if (ShouldDisableDriveForWheel(wheelIndex, handbrake))
		{
			_targetThrottlePower[wheelIndex] = 0f;
			wheel.motorTorque = 0f;
			return;
		}

		if (ShouldApplyTractionControl(wheel))
			ApplyTractionControlToWheel(wheelIndex);
		else
			_targetThrottlePower[wheelIndex] = CalculateWheelTorque(throttle);

		wheel.motorTorque = _targetThrottlePower[wheelIndex];
	}

	/// <summary>
	/// Determines whether motor torque should be disabled for a particular wheel.
	/// </summary>
	/// <param name="wheelIndex">Wheel index.</param>
	/// <param name="handbrake">Whether the handbrake is active.</param>
	private bool ShouldDisableDriveForWheel(int wheelIndex, bool handbrake)
	{
		return handbrake && IsRearWheel(wheelIndex);
	}

	/// <summary>
	/// Determines whether traction control should reduce torque on a wheel.
	/// </summary>
	/// <param name="wheel">Wheel being evaluated.</param>
	private bool ShouldApplyTractionControl(WheelCollider wheel)
	{
		if (!tractionControlEnabled)
			return false;

		if (!wheel.GetGroundHit(out WheelHit hit))
			return false;

		return CalculateSlipMagnitude(hit) > tractionSlipLimit;
	}

	/// <summary>
	/// Applies the current traction-control torque decay to one driven wheel.
	/// </summary>
	/// <param name="wheelIndex">Index of the driven wheel.</param>
	private void ApplyTractionControlToWheel(int wheelIndex)
	{
		_targetThrottlePower[wheelIndex] *= 0.7f;
	}

	/// <summary>
	/// Calculates base motor torque for one driven wheel.
	/// </summary>
	/// <param name="throttle">Throttle input after modifiers.</param>
	private float CalculateWheelTorque(float throttle)
	{
		if (_drivenWheelCount <= 0)
			return 0f;

		return maxMotorPower / _drivenWheelCount * throttle;
	}

	/// <summary>
	/// Calculates torque multiplier near engine redline.
	/// </summary>
	/// <returns>Multiplier from 1 down to 0.2 when normalized RPM reaches redline.</returns>
	private float CalculateRedlineMultiplier()
	{
		float rpm01 = _transmissionController.GetNormalizedRPM();

		if (rpm01 <= redlineFadeStart)
			return 1f;

		float t = Mathf.InverseLerp(redlineFadeStart, 1f, rpm01);

		return Mathf.Lerp(1f, 0.2f, t);
	}

	#endregion

	#region Private Helpers: Limited Slip Differenctial

	/// <summary>
	/// Applies a simplified limited-slip differential effect by reducing torque on driven wheels
	/// that spin much faster than the driven-wheel average, while optionally supporting slower
	/// driven wheels with more torque.
	/// </summary>
	private void ApplyLimitedSlipDifferential()
	{
		if (!limitedSlipEnabled)
			return;

		if (_drivenWheelCount <= 1)
			return;

		float averageRpm = AverageGroundedDrivenWheelAbsRPM();

		if (averageRpm <= 1f)
			return;

		bool hasOverSpinningWheel = false;

		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_driven[i])
				continue;

			WheelCollider wheel = _wheelColliders[i];

			if (!wheel.GetGroundHit(out WheelHit hit))
				continue;

			float rpmDifference = Mathf.Abs(wheel.rpm) - averageRpm;

			if (rpmDifference > limitedSlipStartRpmDifference)
			{
				hasOverSpinningWheel = true;
				break;
			}
		}

		if (!hasOverSpinningWheel)
			return;

		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_driven[i])
				continue;

			WheelCollider wheel = _wheelColliders[i];

			if (!wheel.GetGroundHit(out WheelHit hit))
				continue;

			float rpmDifference = Mathf.Abs(wheel.rpm) - averageRpm;

			if (rpmDifference > limitedSlipStartRpmDifference)
			{
				float correction = Mathf.InverseLerp(
					limitedSlipStartRpmDifference,
					limitedSlipFullRpmDifference,
					rpmDifference);

				correction = Mathf.Clamp01(correction);

				float torqueMultiplier = Mathf.Lerp(
					1f,
					1f - limitedSlipMaxTorqueCut,
					correction);

				wheel.motorTorque *= torqueMultiplier;

				float extraBrakeTorque = limitedSlipBrakeTorque * correction;
				wheel.brakeTorque = Mathf.Max(wheel.brakeTorque, extraBrakeTorque);
			}
			else if (rpmDifference < -limitedSlipStartRpmDifference)
			{
				float correction = Mathf.InverseLerp(
					limitedSlipStartRpmDifference,
					limitedSlipFullRpmDifference,
					-rpmDifference);

				correction = Mathf.Clamp01(correction);

				float torqueMultiplier = Mathf.Lerp(
					1f,
					limitedSlipGripWheelBoost,
					correction);

				wheel.motorTorque *= torqueMultiplier;
			}
		}
	}

	#endregion

	#region Private Helpers: Braking

	/// <summary>
	/// Applies service brake and handbrake behavior to all wheels.
	/// </summary>
	/// <param name="braking">Brake input after reverse preprocessing.</param>
	/// <param name="handbrake">Whether the handbrake is active.</param>
	private void ApplyBraking(float braking, bool handbrake)
	{
		for (int i = 0; i < _wheelColliders.Length; i++)
			ApplyBrakingToWheel(i, braking, handbrake);
	}

	/// <summary>
	/// Applies braking, handbrake friction, lock-related friction, and grip-circle
	/// friction to one wheel.
	/// </summary>
	/// <param name="wheelIndex">Wheel index.</param>
	/// <param name="braking">Brake input.</param>
	/// <param name="handbrake">Whether the handbrake is active.</param>
	private void ApplyBrakingToWheel(int wheelIndex, float braking, bool handbrake)
	{
		WheelCollider wheel = _wheelColliders[wheelIndex];

		bool isFront = IsFrontWheel(wheelIndex);
		bool isRear = IsRearWheel(wheelIndex);

		float brake = 0f;

		ResetWheelFriction(wheel, isFront);

		if (braking > 0.1f)
			brake = CalculateBrakeTorque(wheelIndex, wheel, braking);

		if (handbrake && isRear)
			brake = ApplyHandbrakeToWheel(wheel, brake);

		if (IsWheelNearLock(wheel, brake))
			ApplyLockedWheelFriction(wheel, isFront);

		ApplyGripCircleFriction(wheel);

		wheel.brakeTorque = brake;
	}

	/// <summary>
	/// Calculates brake torque for one wheel, including optional ABS reduction.
	/// </summary>
	/// <param name="wheelIndex">Wheel index.</param>
	/// <param name="wheel">Wheel being braked.</param>
	/// <param name="braking">Brake input.</param>
	/// <returns>Brake torque to apply to the wheel.</returns>
	private float CalculateBrakeTorque(int wheelIndex, WheelCollider wheel, float braking)
	{
		if (ShouldApplyAbs(wheel))
		{
			_targetBrakingPower[wheelIndex] *= 0.5f;
		}
		else
		{
			_targetBrakingPower[wheelIndex] = maxBrakeTorque * braking;
		}

		return _targetBrakingPower[wheelIndex];
	}

	/// <summary>
	/// Determines whether ABS should reduce brake torque for a wheel.
	/// </summary>
	/// <param name="wheel">Wheel being evaluated.</param>
	private bool ShouldApplyAbs(WheelCollider wheel)
	{
		if (!absEnabled)
			return false;

		if (!wheel.GetGroundHit(out WheelHit hit))
			return false;

		return Mathf.Abs(hit.forwardSlip) > absSlipLimit;
	}

	/// <summary>
	/// Applies handbrake torque and handbrake tire-friction changes to a rear wheel.
	/// </summary>
	/// <param name="wheel">Rear wheel receiving handbrake behavior.</param>
	/// <param name="currentBrake">Brake torque already calculated for the wheel.</param>
	/// <returns>Final brake torque after handbrake torque is considered.</returns>
	private float ApplyHandbrakeToWheel(WheelCollider wheel, float currentBrake)
	{
		float brake = Mathf.Max(currentBrake, handbrakeTorque);

		ApplyHandbrakeDriftFriction(wheel);
		wheel.motorTorque = 0f;

		return brake;
	}

	/// <summary>
	/// Determines whether a wheel is close enough to lock-up to receive locked-wheel grip reduction.
	/// </summary>
	/// <param name="wheel">Wheel being evaluated.</param>
	/// <param name="brake">Current brake torque for the wheel.</param>
	private bool IsWheelNearLock(WheelCollider wheel, float brake)
	{
		if (brake < maxBrakeTorque * lockBrakeTorqueThreshold)
			return false;

		if (!wheel.GetGroundHit(out WheelHit hit))
			return false;

		return Mathf.Abs(hit.forwardSlip) > lockForwardSlipThreshold;
	}

	#endregion

	#region Private Helpers: Mesh Sync

	/// <summary>
	/// Copies WheelCollider world poses to visual wheel meshes.
	/// </summary>
	private void SyncMeshes()
	{
		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			_wheelColliders[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
			_wheelMeshes[i].SetPositionAndRotation(pos, rot);
		}
	}

	#endregion

	#region Private Helpers: Anti-Roll

	/// <summary>
	/// Applies anti-roll forces to both axles.
	/// </summary>
	private void ApplyAntiRoll()
	{
		if (antiRollToggle)
		{
			ApplyAntiRollPair(0, 1, antiRollStiffnessFront);
			ApplyAntiRollPair(2, 3, antiRollStiffnessRear);
		}
	}

	/// <summary>
	/// Applies anti-roll force between two wheels on the same axle.
	/// </summary>
	/// <param name="leftIndex">Left wheel index.</param>
	/// <param name="rightIndex">Right wheel index.</param>
	/// <param name="stiffness">Anti-roll stiffness for this axle.</param>
	private void ApplyAntiRollPair(int leftIndex, int rightIndex, float stiffness)
	{
		WheelCollider leftWheel = _wheelColliders[leftIndex];
		WheelCollider rightWheel = _wheelColliders[rightIndex];

		float leftTravel = GetSuspensionTravel(leftWheel);
		float rightTravel = GetSuspensionTravel(rightWheel);

		float antiRollForce = (leftTravel - rightTravel) * stiffness;

		if (leftTravel > -1f)
			_carRigidBody.AddForceAtPosition(leftWheel.transform.up * -antiRollForce, leftWheel.transform.position, ForceMode.Force);

		if (rightTravel > -1f)
			_carRigidBody.AddForceAtPosition(rightWheel.transform.up * antiRollForce, rightWheel.transform.position, ForceMode.Force);
	}

	/// <summary>
	/// Calculates normalized suspension travel for one wheel.
	/// </summary>
	/// <param name="wheel">Wheel whose suspension travel should be calculated.</param>
	/// <returns>Suspension travel, or -1 if the wheel is not grounded.</returns>
	private float GetSuspensionTravel(WheelCollider wheel)
	{
		if (wheel.GetGroundHit(out WheelHit hit))
			return (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / wheel.suspensionDistance;

		return -1f;
	}

	#endregion

	#region Private Helpers: Friction Setup

	/// <summary>
	/// Applies base friction settings to every wheel collider.
	/// </summary>
	private void SetupAllWheelFriction()
	{
		for (int i = 0; i < _wheelColliders.Length; i++)
			SetupFriction(_wheelColliders[i], IsFrontWheel(i));
	}

	/// <summary>
	/// Applies front or rear base friction settings to one wheel.
	/// </summary>
	/// <param name="wheel">Wheel to configure.</param>
	/// <param name="isFront">Whether the wheel belongs to the front axle.</param>
	private void SetupFriction(WheelCollider wheel, bool isFront)
	{
		if (isFront)
			SetupFrontFriction(wheel);
		else
			SetupRearFriction(wheel);
	}

	/// <summary>
	/// Applies base front-axle friction settings to one wheel.
	/// </summary>
	/// <param name="wheel">Front wheel to configure.</param>
	private void SetupFrontFriction(WheelCollider wheel)
	{
		ApplyFrictionSettings(wheel, frontForwardFriction, frontSidewaysFriction);
	}

	/// <summary>
	/// Applies base rear-axle friction settings to one wheel.
	/// </summary>
	/// <param name="wheel">Rear wheel to configure.</param>
	private void SetupRearFriction(WheelCollider wheel)
	{
		ApplyFrictionSettings(wheel, rearForwardFriction, rearSidewaysFriction);
	}

	/// <summary>
	/// Resets a wheel to its base front or rear friction settings.
	/// </summary>
	/// <param name="wheel">Wheel to reset.</param>
	/// <param name="isFront">Whether the wheel belongs to the front axle.</param>
	private void ResetWheelFriction(WheelCollider wheel, bool isFront)
	{
		SetupFriction(wheel, isFront);
	}

	/// <summary>
	/// Reduces rear-wheel grip while the handbrake is held to allow controlled sliding.
	/// </summary>
	/// <param name="wheel">Rear wheel affected by the handbrake.</param>
	private void ApplyHandbrakeDriftFriction(WheelCollider wheel)
	{
		WheelFrictionCurve sideways = wheel.sidewaysFriction;
		sideways.stiffness *= rearSidewaysGripHandbrakeMultiplier;
		wheel.sidewaysFriction = sideways;

		WheelFrictionCurve forward = wheel.forwardFriction;
		forward.stiffness *= rearForwardGripHandbrakeMultiplier;
		wheel.forwardFriction = forward;
	}

	/// <summary>
	/// Reduces both forward and sideways grip for a wheel that is near lock-up.
	/// </summary>
	/// <param name="wheel">Wheel whose grip should be reduced.</param>
	/// <param name="isFront">Whether the wheel belongs to the front axle.</param>
	private void ApplyLockedWheelFriction(WheelCollider wheel, bool isFront)
	{
		float multiplier = isFront ? frontGripLockedMultiplier : rearGripLockedMultiplier;

		WheelFrictionCurve sideways = wheel.sidewaysFriction;
		sideways.stiffness *= multiplier;
		wheel.sidewaysFriction = sideways;

		WheelFrictionCurve forward = wheel.forwardFriction;
		forward.stiffness *= multiplier;
		wheel.forwardFriction = forward;
	}

	/// <summary>
	/// Copies serialized friction settings into a Unity WheelCollider.
	/// </summary>
	/// <param name="wheel">WheelCollider to configure.</param>
	/// <param name="forward">Forward-friction settings.</param>
	/// <param name="sideways">Sideways-friction settings.</param>
	private void ApplyFrictionSettings(WheelCollider wheel, WheelFrictionSettings forward, WheelFrictionSettings sideways)
	{
		WheelFrictionCurve f = wheel.forwardFriction;
		f.stiffness = forward.stiffness;
		f.extremumSlip = forward.extremumSlip;
		f.extremumValue = forward.extremumValue;
		f.asymptoteSlip = forward.asymptoteSlip;
		f.asymptoteValue = forward.asymptoteValue;
		wheel.forwardFriction = f;

		WheelFrictionCurve s = wheel.sidewaysFriction;
		s.stiffness = sideways.stiffness;
		s.extremumSlip = sideways.extremumSlip;
		s.extremumValue = sideways.extremumValue;
		s.asymptoteSlip = sideways.asymptoteSlip;
		s.asymptoteValue = sideways.asymptoteValue;
		wheel.sidewaysFriction = s;
	}

	/// <summary>
	/// Applies a simplified traction-circle effect by reducing available tire grip
	/// when the wheel uses too much combined forward and sideways slip.
	/// </summary>
	/// <remarks>
	/// Unity WheelCollider uses separate forward and sideways friction curves.
	/// This method links them together by reducing forward grip when sideways usage is high
	/// and reducing sideways grip when forward usage is high. This creates a simplified
	/// "grip budget" similar to a real traction circle.
	/// </remarks>
	/// <param name="wheel">Wheel whose friction should be modified.</param>
	private void ApplyGripCircleFriction(WheelCollider wheel)
	{
		if (!gripCircleEnabled)
		{
			return;
		}

		if (!wheel.GetGroundHit(out WheelHit hit))
		{
			return;
		}

		float forwardSlip = Mathf.Abs(hit.forwardSlip);
		float sidewaysSlip = Mathf.Abs(hit.sidewaysSlip);

		float forwardUsage = Mathf.InverseLerp(0f, gripCircleFullSlip, forwardSlip);
		float sidewaysUsage = Mathf.InverseLerp(0f, gripCircleFullSlip, sidewaysSlip);

		float combinedSlip = forwardSlip + sidewaysSlip;
		float combinedOverload = Mathf.InverseLerp(gripCircleStartSlip, gripCircleFullSlip, combinedSlip);

		float sidewaysMultiplier = Mathf.Lerp(
			1f,
			minSidewaysGripCircleMultiplier,
			Mathf.Clamp01(forwardUsage + combinedOverload));

		float forwardMultiplier = Mathf.Lerp(
			1f,
			minForwardGripCircleMultiplier,
			Mathf.Clamp01(sidewaysUsage + combinedOverload));

		WheelFrictionCurve forward = wheel.forwardFriction;
		forward.stiffness *= forwardMultiplier;
		wheel.forwardFriction = forward;

		WheelFrictionCurve sideways = wheel.sidewaysFriction;
		sideways.stiffness *= sidewaysMultiplier;
		wheel.sidewaysFriction = sideways;
	}

	#endregion
}
