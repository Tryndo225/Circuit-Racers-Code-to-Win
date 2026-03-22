using UnityEngine;

/// <summary>
/// Handles steering, throttle, braking, anti-roll, and wheel friction setup for a 4-wheel vehicle.
/// Coordinates with a TransmissionController and WheelColliders to apply forces and sync meshes.
/// </summary>
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

	#region Inspector: Stability

	[Header("Stability")]
	public float antiRollStiffnessFront;
	public float antiRollStiffnessRear;

	#endregion

	#region Inspector: Traction Control & ABS Limits

	[Header("Traction Control & ABS Limits")]
	public bool tractionControlEnabled;
	public float tractionSlipLimit = 0.35f;

	public bool absEnabled;
	public float absSlipLimit = 0.35f;

	#endregion

	#region Inspector: Dynamic Grip

	[Header("Dynamic Grip")]
	[Tooltip("Rear sideways grip multiplier while handbrake is held. Lower = easier drift.")]
	public float rearSidewaysGripHandbrakeMultiplier = 0.35f;

	[Tooltip("Rear forward grip multiplier while handbrake is held.")]
	public float rearForwardGripHandbrakeMultiplier = 0.85f;

	[Tooltip("Front sideways grip multiplier when front wheels are near lock.")]
	public float frontSidewaysGripLockedMultiplier = 0.45f;

	[Tooltip("Rear sideways grip multiplier when rear wheels are near lock.")]
	public float rearSidewaysGripLockedMultiplier = 0.60f;

	[Tooltip("Forward slip above this counts as near-lock when braking hard.")]
	public float lockForwardSlipThreshold = 0.35f;

	[Tooltip("Brake torque must exceed this fraction of maxBrakeTorque to count as hard braking.")]
	[Range(0f, 1f)] public float lockBrakeTorqueThreshold = 0.9f;

	#endregion

	#region Inspector: Forward Friction

	[Header("Forward Friction")]
	public float[] frontForwardFriction;
	public float[] rearForwardFriction;

	#endregion

	#region Inspector: Sideways Friction

	[Header("Sideways Friction")]
	public float[] frontSidewaysFriction;
	public float[] rearSidewaysFriction;

	#endregion

	#region Inspector: Miscellaneous

	[Header("Miscellaneous")]
	public Vector3 coMPosition;
	public float ackermannFactor = 1f;

	#endregion

	#region State & Dependencies

	private Rigidbody _carRigidBody;
	private TransmissionController _transmissionController;
	private WheelCollider[] _wheelColliders;
	private Transform[] _wheelMeshes;
	private bool[] _driven;
	private bool[] _steering;

	private bool _isReversing = false;
	private bool _braking = false;
	private float steeringAngle = 0f;

	public bool Reversing => _isReversing;
	public bool Braking => _braking;

	#endregion

	#region Public API

	public void Init(
		Rigidbody carRigitBody,
		TransmissionController transmissionController,
		WheelCollider[] wheelsColliders,
		Transform[] wheelMeshes,
		bool[] driven,
		bool[] steering)
	{
		_carRigidBody = carRigitBody;
		_transmissionController = transmissionController;
		_wheelColliders = wheelsColliders;
		_wheelMeshes = wheelMeshes;
		_driven = driven;
		_steering = steering;
	}

	public void SetUp()
	{
		foreach (WheelCollider w in _wheelColliders)
		{
			w.ConfigureVehicleSubsteps(0.5f, 20, 30);
		}

		_carRigidBody.solverIterations = 12;
		_carRigidBody.solverVelocityIterations = 12;

		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			SetupFriction(_wheelColliders[i], i < 2);
		}

		_carRigidBody.centerOfMass = coMPosition;
	}

	public void ApplyWheelControls(float throttle, bool braking, bool handbrake, float steering, bool gamepadSteering)
	{
		ControlSteering(steering, gamepadSteering);

		if (ReverseCheck(braking))
		{
			braking = false;
		}

		_braking = braking;

		if (braking)
		{
			throttle = 0f;
		}

		ApplyDrive(throttle, handbrake);
		ApplyBraking(braking, handbrake);
		ApplyAntiRoll();
		SyncMeshes();
	}

	#endregion

	#region Private Helpers: Averages & Speed

	private float AverageDrivenWheelRPM()
	{
		float sum = 0f;
		int count = 0;

		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_driven[i])
			{
				continue;
			}

			WheelHit hit;
			if (_wheelColliders[i].GetGroundHit(out hit))
			{
				sum += _wheelColliders[i].rpm;
				count++;
			}
		}

		return count > 0 ? sum / count : 0f;
	}

	private float AverageDrivenWheelSlip()
	{
		float sum = 0f;
		int count = 0;

		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_driven[i])
			{
				continue;
			}

			WheelHit hit;
			if (_wheelColliders[i].GetGroundHit(out hit))
			{
				sum += Mathf.Abs(hit.forwardSlip + hit.sidewaysSlip);
				count++;
			}
		}

		return count > 0 ? sum / count : 0f;
	}

	private float CalculateSpeed()
	{
		return _carRigidBody.linearVelocity.magnitude * 3.6f;
	}

	#endregion

	#region Private Helpers: Steering

	private void ControlSteering(float steerInput, bool gamepadSteering)
	{
		float kph = CalculateSpeed();
		float speedCoefficient = Mathf.InverseLerp(0f, maxSpeed, kph);
		float maxAngle = Mathf.Lerp(maxSteerAngle, maxSteerAngleAtTopSpeed, speedCoefficient);

		if (gamepadSteering)
		{
			float shaped = Mathf.Sign(steerInput) * Mathf.Pow(Mathf.Abs(steerInput), inputExponent);
			float targetAngle = shaped * maxAngle;

			steeringAngle = Mathf.MoveTowards(
				steeringAngle,
				targetAngle,
				steerSpeedDegPerSec * Time.fixedDeltaTime);

			ApplySteering(steeringAngle);
		}
		else
		{
			float targetAngle = steerInput * maxAngle;

			steeringAngle = Mathf.MoveTowards(
				steeringAngle,
				targetAngle,
				steerSpeedDegPerSec * Time.fixedDeltaTime);

			ApplySteering(steeringAngle);
		}
	}

	private void ApplySteering(float steer)
	{
		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			if (!_steering[i])
			{
				continue;
			}

			bool isLeft = (i % 2 == 0);
			float angle = steer;

			if (ackermannFactor != 1f)
			{
				angle *= isLeft ? (1f + 0.1f * ackermannFactor) : (1f - 0.1f * ackermannFactor);
			}

			_wheelColliders[i].steerAngle = angle;
		}
	}

	private bool ReverseCheck(bool braking)
	{
		if (braking && CalculateSpeed() < 1f)
		{
			_isReversing = true;
			return true;
		}
		else if (!braking && _isReversing)
		{
			_isReversing = false;
			return false;
		}

		return _isReversing;
	}

	#endregion

	#region Private Helpers: Drive / Braking / Mesh Sync

	private void ApplyDrive(float throttle, bool handbrake)
	{
		if (CalculateSpeed() > maxSpeed
			|| (_isReversing && CalculateSpeed() > maxReverseSpeed)
			|| _transmissionController.HandleShifting(AverageDrivenWheelRPM(), AverageDrivenWheelSlip()))
		{
			throttle = 0f;
		}

		for (int i = 0; i < _driven.Length; i++)
		{
			if (!_driven[i])
			{
				continue;
			}

			WheelCollider wc = _wheelColliders[i];
			bool isRear = i >= 2;

			float motor = 0f;

			if (handbrake && isRear)
			{
				wc.motorTorque = 0f;
				continue;
			}

			WheelHit hit;
			bool grounded = wc.GetGroundHit(out hit);
			float targetTorque = maxMotorPower / _driven.Length * throttle;

			if (grounded && Mathf.Abs(hit.forwardSlip) > tractionSlipLimit && tractionControlEnabled)
			{
				targetTorque *= 0.5f;
			}

			motor = targetTorque;
			wc.motorTorque = motor;
		}
	}

	private void ApplyBraking(bool braking, bool handbrake)
	{
		for (int i = 0; i < _wheelColliders.Length; i++)
		{
			WheelCollider wc = _wheelColliders[i];
			bool isFront = i < 2;
			bool isRear = i >= 2;

			float brake = 0f;

			WheelHit hit;
			bool grounded = wc.GetGroundHit(out hit);

			ResetWheelFriction(wc, isFront);

			if (braking)
			{
				float targetBrake = maxBrakeTorque;

				if (grounded && Mathf.Abs(hit.forwardSlip) > absSlipLimit && absEnabled)
				{
					targetBrake *= 0.6f;
				}

				brake = targetBrake;
			}

			if (handbrake && isRear)
			{
				brake = Mathf.Max(brake, handbrakeTorque);

				ApplyHandbrakeDriftFriction(wc);
				wc.motorTorque = 0f;
			}

			bool nearLock =
				grounded
				&& brake >= maxBrakeTorque * lockBrakeTorqueThreshold
				&& Mathf.Abs(hit.forwardSlip) > lockForwardSlipThreshold;

			if (nearLock)
			{
				ApplyLockedWheelFriction(wc, isFront);
			}

			wc.brakeTorque = brake;
		}
	}

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

	private void ApplyAntiRoll()
	{
		ApplyAntiRollPair(0, 1, antiRollStiffnessFront);
		ApplyAntiRollPair(2, 3, antiRollStiffnessRear);
	}

	private void ApplyAntiRollPair(int leftIndex, int rightIndex, float stiffness)
	{
		WheelCollider wl = _wheelColliders[leftIndex];
		WheelCollider wr = _wheelColliders[rightIndex];

		float travelL = GetSuspensionTravel(wl);
		float travelR = GetSuspensionTravel(wr);

		float antiRollForce = (travelL - travelR) * stiffness;

		if (travelL > -1f)
		{
			_carRigidBody.AddForceAtPosition(wl.transform.up * -antiRollForce, wl.transform.position, ForceMode.Force);
		}

		if (travelR > -1f)
		{
			_carRigidBody.AddForceAtPosition(wr.transform.up * antiRollForce, wr.transform.position, ForceMode.Force);
		}
	}

	private float GetSuspensionTravel(WheelCollider wc)
	{
		WheelHit hit;

		if (wc.GetGroundHit(out hit))
		{
			return (-wc.transform.InverseTransformPoint(hit.point).y - wc.radius) / wc.suspensionDistance;
		}

		return -1f;
	}

	#endregion

	#region Private Helpers: Friction Setup

	private void SetupFriction(WheelCollider wc, bool front)
	{
		if (front)
		{
			SetupFrontFriction(wc);
		}
		else
		{
			SetupRearFriction(wc);
		}
	}

	private void SetupFrontFriction(WheelCollider wc)
	{
		WheelFrictionCurve f = wc.forwardFriction;
		f.stiffness = frontForwardFriction[0];
		f.asymptoteSlip = frontForwardFriction[1];
		f.extremumSlip = frontForwardFriction[2];
		f.asymptoteValue = frontForwardFriction[3];
		f.extremumValue = frontForwardFriction[4];
		wc.forwardFriction = f;

		WheelFrictionCurve s = wc.sidewaysFriction;
		s.stiffness = frontSidewaysFriction[0];
		s.asymptoteSlip = frontSidewaysFriction[1];
		s.extremumSlip = frontSidewaysFriction[2];
		s.asymptoteValue = frontSidewaysFriction[3];
		s.extremumValue = frontSidewaysFriction[4];
		wc.sidewaysFriction = s;
	}

	private void SetupRearFriction(WheelCollider wc)
	{
		WheelFrictionCurve f = wc.forwardFriction;
		f.stiffness = rearForwardFriction[0];
		f.asymptoteSlip = rearForwardFriction[1];
		f.extremumSlip = rearForwardFriction[2];
		f.asymptoteValue = rearForwardFriction[3];
		f.extremumValue = rearForwardFriction[4];
		wc.forwardFriction = f;

		WheelFrictionCurve s = wc.sidewaysFriction;
		s.stiffness = rearSidewaysFriction[0];
		s.asymptoteSlip = rearSidewaysFriction[1];
		s.extremumSlip = rearSidewaysFriction[2];
		s.asymptoteValue = rearSidewaysFriction[3];
		s.extremumValue = rearSidewaysFriction[4];
		wc.sidewaysFriction = s;
	}

	private void ResetWheelFriction(WheelCollider wc, bool isFront)
	{
		if (isFront)
		{
			SetupFrontFriction(wc);
		}
		else
		{
			SetupRearFriction(wc);
		}
	}

	private void ApplyHandbrakeDriftFriction(WheelCollider wc)
	{
		WheelFrictionCurve sideways = wc.sidewaysFriction;
		sideways.stiffness *= rearSidewaysGripHandbrakeMultiplier;
		wc.sidewaysFriction = sideways;

		WheelFrictionCurve forward = wc.forwardFriction;
		forward.stiffness *= rearForwardGripHandbrakeMultiplier;
		wc.forwardFriction = forward;
	}

	private void ApplyLockedWheelFriction(WheelCollider wc, bool isFront)
	{
		WheelFrictionCurve sideways = wc.sidewaysFriction;

		if (isFront)
		{
			sideways.stiffness *= frontSidewaysGripLockedMultiplier;
		}
		else
		{
			sideways.stiffness *= rearSidewaysGripLockedMultiplier;
		}

		wc.sidewaysFriction = sideways;
	}

	#endregion
}