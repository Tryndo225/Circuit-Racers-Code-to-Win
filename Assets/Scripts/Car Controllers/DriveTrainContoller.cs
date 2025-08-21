using UnityEngine;

public class DriveTrainContoller : MonoBehaviour
{
    //--- Public Fields ---
    [Header("Vehicle")]
    [Tooltip("Maximum speed in km/h. Vehicle will not exceed this speed.")]
    public float maxSpeed;

    [Tooltip("Maximum reverse speed in km/h. Vehicle will not exceed this speed in reverse.")]
    public float maxReverseSpeed;

    [Tooltip("Maximum steering angle in degrees at top speed. This is the maximum angle when driving at top speed.")]
    public float maxSteerAngleAtTopSpeed;

    [Tooltip("Maximum steering angle in degrees at 0 speed. This is the maximum angle when stationary.")]
    public float maxSteerAngle;

    [Tooltip("Steering speed in degrees per second. This limits how fast the steering angle can change.")]
    public float steerSpeedDegPerSec;

    [Tooltip("Exponent applied to contoler input values (1 = linear).")]
    [SerializeField, Range(1f, 3f)] public float inputExponent;

    [Tooltip("Maximum power")]
    public float maxMotorPower;

    [Tooltip("Maximum brake torque per wheel (N�m).")]
    public float maxBrakeTorque;

    [Tooltip("Handbrake torque for rear wheels (N�m).")]
    public float handbrakeTorque;

    [Header("Stability")]
    public float antiRollStiffnessFront;

    public float antiRollStiffnessRear;

    [Header("Traction Control & ABS Limits")]
    public bool tractionControlEnabled;

    public float tractionSlipLimit;

    public bool absEnabled;
    public float absSlipLimit;

    [Header("Forward Friction")]
    public float[] frontForwardFriction;

    public float[] rearForwardFriction;

    [Header("Sideways Friction")]
    public float[] frontSidewaysFriction;

    public float[] rearSidewaysFriction;

    [Header("Miscellaneous")]
    public float comYOffset;

    public float ackermannFactor;

    //--- Private Fields ---
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

    public void Init(Rigidbody carRigitBody, TransmissionController transmissionController, WheelCollider[] wheelsColliders, Transform[] wheelMeshes, bool[] driven, bool[] steering)
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
        foreach (var w in _wheelColliders)
            w.ConfigureVehicleSubsteps(0.5f, 20, 30);

        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            SetupFriction(_wheelColliders[i], i < 2);
        }

        _carRigidBody.centerOfMass = new Vector3(0f, comYOffset, 0f);
    }

    private float AverageWheelRPM()
    {
        float sum = 0f; int count = 0;
        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            if (!_driven[i]) continue;

            WheelHit hit;
            if (_wheelColliders[i].GetGroundHit(out hit)) // trust only grounded wheels
            {
                sum += _wheelColliders[i].rpm;
                count++;
            }
        }

        return (count > 0) ? (sum / count) : 0f;
    }

    private float CalculateSpeed()
    {
        var speed = GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f; // Convert m/s to km/h
        return speed;
    }

    // Steering and Throttle Control
    // -----------------------------
    public void ControlSteering(float steerInput, bool gamepadSteering)
    {
        if (gamepadSteering)
        {
            float shaped = Mathf.Sign(steerInput) * Mathf.Pow(Mathf.Abs(steerInput), inputExponent);
            ApplySteering(steerInput);
        }
        else
        {
            // Speed-based max angle
            float kph = CalculateSpeed();

            float speedCoeficient = Mathf.InverseLerp(0f, maxSpeed, kph);

            float maxAngle = Mathf.Lerp(maxSteerAngle, maxSteerAngleAtTopSpeed, speedCoeficient);
            float targetAngle = steerInput * maxAngle;

            steeringAngle = Mathf.MoveTowards(steeringAngle, targetAngle, steerSpeedDegPerSec * Time.fixedDeltaTime);

            ApplySteering(steeringAngle);
        }
    }

    private void ApplySteering(float steer)
    {
        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            if (!_steering[i])
                continue;

            bool isLeft = (i % 2 == 0);
            float angle = steer;

            if (ackermannFactor != 1f)
                angle *= isLeft ? (1f + 0.1f * ackermannFactor) : (1f - 0.1f * ackermannFactor);

            _wheelColliders[i].steerAngle = angle;
        }
    }

    private bool ReverseCheck(bool braking)
    {
        if (braking && CalculateSpeed() < 1f)
        {
            _isReversing = true;
            return true; // allow reversing if braking and speed is low
        }
        else if (!braking && _isReversing)
        {
            _isReversing = false;
            return false; // stop reversing if not braking
        }

        return _isReversing;
    }

    public void ApplyWheelControls(float throttle, bool braking, bool handbrake, float steering, bool gamepadSteering)
    {
        ControlSteering(steering, gamepadSteering);

        if (ReverseCheck(braking))
        {
            braking = false; // disable braking if reversing
        }

        _braking = braking;

        ApplyDrive(throttle);
        ApplyBraking(braking, handbrake);

        ApplyAntiRoll();

        SyncMeshes();
    }

    private void ApplyDrive(float throttle)
    {
        if (CalculateSpeed() > maxSpeed || (_isReversing && CalculateSpeed() > maxReverseSpeed) || _transmissionController.HandleShifting(AverageWheelRPM()))
        {
            throttle = 0f; // limit speed to maxSpeed
        }

        for (int i = 0; i < _driven.Length; i++)
        {
            if (!_driven[i])
                continue;

            var wc = _wheelColliders[i];
            float motor = 0f;

            WheelHit hit;
            bool grounded = wc.GetGroundHit(out hit);
            float targetTorque = maxMotorPower / _driven.Length * throttle;

            if (grounded && Mathf.Abs(hit.forwardSlip) > tractionSlipLimit && tractionControlEnabled)
                targetTorque *= 0.5f;

            motor = targetTorque;

            wc.motorTorque = motor;
        }
    }

    private void ApplyBraking(bool braking, bool handbrake)
    {
        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            var wc = _wheelColliders[i];

            var brake = 0f;

            WheelHit hit;
            bool grounded = wc.GetGroundHit(out hit);

            if (braking)
            {
                float targetBrake = maxBrakeTorque;

                if (grounded && Mathf.Abs(hit.forwardSlip) > absSlipLimit && absEnabled)
                    targetBrake *= 0.6f;

                brake = targetBrake;
            }

            if (handbrake && i >= 2)
                brake = Mathf.Max(brake, handbrakeTorque);

            wc.brakeTorque = brake;
        }
    }

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
            _carRigidBody.AddForceAtPosition(wl.transform.up * -antiRollForce, wl.transform.position, ForceMode.Force);

        if (travelR > -1f)
            _carRigidBody.AddForceAtPosition(wr.transform.up * antiRollForce, wr.transform.position, ForceMode.Force);
    }

    private float GetSuspensionTravel(WheelCollider wc)
    {
        WheelHit hit;

        if (wc.GetGroundHit(out hit))
            return (-wc.transform.InverseTransformPoint(hit.point).y - wc.radius) / wc.suspensionDistance;

        return -1f;
    }

    private void SyncMeshes()
    {
        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            _wheelColliders[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
            _wheelMeshes[i].SetPositionAndRotation(pos, rot);
        }
    }

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
        var f = wc.forwardFriction;
        f.stiffness = frontForwardFriction[0];
        f.asymptoteSlip = frontForwardFriction[1];
        f.extremumSlip = frontForwardFriction[2];
        f.asymptoteValue = frontForwardFriction[3];
        f.extremumValue = frontForwardFriction[4];
        wc.forwardFriction = f;

        var s = wc.sidewaysFriction;
        s.stiffness = frontSidewaysFriction[0];
        s.asymptoteSlip = frontSidewaysFriction[1];
        s.extremumSlip = frontSidewaysFriction[2];
        s.asymptoteValue = frontSidewaysFriction[3];
        s.extremumValue = frontSidewaysFriction[4];
        wc.sidewaysFriction = s;
    }

    private void SetupRearFriction(WheelCollider wc)
    {
        var f = wc.forwardFriction;
        f.stiffness = rearForwardFriction[0];
        f.asymptoteSlip = rearForwardFriction[1];
        f.extremumSlip = rearForwardFriction[2];
        f.asymptoteValue = rearForwardFriction[3];
        f.extremumValue = rearForwardFriction[4];
        wc.forwardFriction = f;

        var s = wc.sidewaysFriction;
        s.stiffness = rearSidewaysFriction[0];
        s.asymptoteSlip = rearSidewaysFriction[1];
        s.extremumSlip = rearSidewaysFriction[2];
        s.asymptoteValue = rearSidewaysFriction[3];
        wc.sidewaysFriction = s;
    }
}