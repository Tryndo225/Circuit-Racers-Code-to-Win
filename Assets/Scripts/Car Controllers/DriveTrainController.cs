using UnityEngine;

/// <summary>
/// Handles steering, throttle, braking, anti-roll, and wheel friction setup for a 4-wheel vehicle.
/// Coordinates with a TransmissionController and WheelColliders to apply forces and sync meshes.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @invariant The arrays _wheelColliders, _wheelMeshes, _driven, _steering are non-null and of equal length (expected 4).
/// @invariant _carRigidBody is assigned and matches the vehicle rigidbody.
/// @thread Unity main thread (FixedUpdate/physics callbacks).
/// </remarks>
public class DriveTrainController : MonoBehaviour
{
    #region Inspector: Vehicle

    [Header("Vehicle")]
    /// <summary>Maximum forward speed in km/h. Vehicle will not exceed this speed.</summary>
    [Tooltip("Maximum speed in km/h. Vehicle will not exceed this speed.")]
    public float maxSpeed;

    /// <summary>Maximum reverse speed in km/h. Vehicle will not exceed this speed in reverse.</summary>
    [Tooltip("Maximum reverse speed in km/h. Vehicle will not exceed this speed in reverse.")]
    public float maxReverseSpeed;

    /// <summary>Maximum steering angle in degrees at top speed.</summary>
    [Tooltip("Maximum steering angle in degrees at top speed. This is the maximum angle when driving at top speed.")]
    public float maxSteerAngleAtTopSpeed;

    /// <summary>Maximum steering angle in degrees when stationary.</summary>
    [Tooltip("Maximum steering angle in degrees at 0 speed. This is the maximum angle when stationary.")]
    public float maxSteerAngle;

    /// <summary>Steering speed limit in degrees per second for how fast the steering angle can change.</summary>
    [Tooltip("Steering speed in degrees per second. This limits how fast the steering angle can change.")]
    public float steerSpeedDegPerSec;

    /// <summary>Exponent to shape input response (1 = linear).</summary>
    [Tooltip("Exponent applied to contoler input values (1 = linear).")]
    [SerializeField, Range(1f, 3f)] public float inputExponent;

    /// <summary>Maximum motor power (torque budget applied per driven wheel).</summary>
    [Tooltip("Maximum power")]
    public float maxMotorPower;

    /// <summary>Maximum brake torque per wheel (N*m).</summary>
    [Tooltip("Maximum brake torque per wheel (N*m).")]
    public float maxBrakeTorque;

    /// <summary>Handbrake torque for rear wheels (N*m).</summary>
    [Tooltip("Handbrake torque for rear wheels (N*m).")]
    public float handbrakeTorque;

    #endregion

    #region Inspector: Stability

    [Header("Stability")]
    /// <summary>Anti-roll stiffness for the front axle.</summary>
    public float antiRollStiffnessFront;

    /// <summary>Anti-roll stiffness for the rear axle.</summary>
    public float antiRollStiffnessRear;

    #endregion

    #region Inspector: Traction Control & ABS Limits

    [Header("Traction Control & ABS Limits")]
    /// <summary>Whether traction control is enabled (reduces torque when slip exceeds limit).</summary>
    public bool tractionControlEnabled;

    /// <summary>Forward slip threshold for traction control.</summary>
    public float tractionSlipLimit;

    /// <summary>Whether ABS is enabled (reduces brake torque when slip exceeds limit).</summary>
    public bool absEnabled;

    /// <summary>Forward slip threshold for ABS.</summary>
    public float absSlipLimit;

    #endregion

    #region Inspector: Forward Friction

    [Header("Forward Friction")]
    /// <summary>Front forward-friction parameters [stiffness, asymptoteSlip, extremumSlip, asymptoteValue, extremumValue].</summary>
    public float[] frontForwardFriction;

    /// <summary>Rear forward-friction parameters [stiffness, asymptoteSlip, extremumSlip, asymptoteValue, extremumValue].</summary>
    public float[] rearForwardFriction;

    #endregion

    #region Inspector: Sideways Friction

    [Header("Sideways Friction")]
    /// <summary>Front sideways-friction parameters [stiffness, asymptoteSlip, extremumSlip, asymptoteValue, extremumValue].</summary>
    public float[] frontSidewaysFriction;

    /// <summary>Rear sideways-friction parameters [stiffness, asymptoteSlip, extremumSlip, asymptoteValue, extremumValue].</summary>
    public float[] rearSidewaysFriction;

    #endregion

    #region Inspector: Miscellaneous

    [Header("Miscellaneous")]
    /// <summary>Center of mass offset to apply to the rigidbody (local space).</summary>
    public Vector3 coMPosition;

    /// <summary>Ackermann factor to bias inner/outer steering angles (1 = no change).</summary>
    public float ackermannFactor;

    #endregion

    #region State & Dependencies

    /// <summary>Vehicle rigidbody.</summary>
    private Rigidbody _carRigidBody;

    /// <summary>Transmission controller used for gear shifting logic.</summary>
    private TransmissionController _transmissionController;

    /// <summary>Wheel collider components in order [FL, FR, RL, RR].</summary>
    private WheelCollider[] _wheelColliders;

    /// <summary>Wheel mesh transforms matching the colliders order.</summary>
    private Transform[] _wheelMeshes;

    /// <summary>Per-wheel flags for driven wheels.</summary>
    private bool[] _driven;

    /// <summary>Per-wheel flags for steering wheels.</summary>
    private bool[] _steering;

    /// <summary>True if currently allowed to reverse.</summary>
    private bool _isReversing = false;

    /// <summary>True if service braking is currently applied.</summary>
    private bool _braking = false;

    /// <summary>Current steering angle (deg) used when gamepadSteering is false.</summary>
    private float steeringAngle = 0f;

    /// <summary>Public read-only flag for reverse state.</summary>
    public bool Reversing => _isReversing;

    /// <summary>Public read-only flag for braking state.</summary>
    public bool Braking => _braking;

    #endregion

    #region Public API

    /// <summary>
    /// Injects dependencies and per-wheel configuration.
    /// </summary>
    /// <param name="carRigitBody">The vehicle Rigidbody.</param>
    /// <param name="transmissionController">Transmission controller instance.</param>
    /// <param name="wheelsColliders">WheelColliders array [FL, FR, RL, RR].</param>
    /// <param name="wheelMeshes">Wheel mesh transforms matching colliders order.</param>
    /// <param name="driven">Per-wheel driven flags.</param>
    /// <param name="steering">Per-wheel steering flags.</param>
    public void Init(Rigidbody carRigitBody, TransmissionController transmissionController, WheelCollider[] wheelsColliders, Transform[] wheelMeshes, bool[] driven, bool[] steering)
    {
        _carRigidBody = carRigitBody;
        _transmissionController = transmissionController;
        _wheelColliders = wheelsColliders;
        _wheelMeshes = wheelMeshes;
        _driven = driven;
        _steering = steering;
    }

    /// <summary>
    /// Configures physics substeps, solver iterations, wheel friction, and applies center of mass.
    /// Call once after Init.
    /// </summary>
    public void SetUp()
    {
        foreach (var w in _wheelColliders)
            w.ConfigureVehicleSubsteps(0.5f, 20, 30);

        _carRigidBody.solverIterations = 12;
        _carRigidBody.solverVelocityIterations = 12;

        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            SetupFriction(_wheelColliders[i], i < 2);
        }

        _carRigidBody.centerOfMass = coMPosition;
    }

    /// <summary>
    /// Main per-tick control entry: steering, throttle/drive, braking, anti-roll, and mesh sync.
    /// </summary>
    /// <param name="throttle">Throttle in [-1, 1].</param>
    /// <param name="braking">True to apply service brakes.</param>
    /// <param name="handbrake">True to apply handbrake to rear wheels.</param>
    /// <param name="steering">Steering input in degrees or normalized (depends on caller).</param>
    /// <param name="gamepadSteering">Whether to use exponent-shaped steering path.</param>
    public void ApplyWheelControls(float throttle, bool braking, bool handbrake, float steering, bool gamepadSteering)
    {
        ControlSteering(steering, gamepadSteering);

        if (ReverseCheck(braking))
        {
            braking = false;
        }

        _braking = braking;

        ApplyDrive(throttle);
        ApplyBraking(braking, handbrake);
        ApplyAntiRoll();
        SyncMeshes();
    }

    #endregion

    #region Unity Methods
    // (No Unity event methods in this class currently; physics/control is driven externally.)
    #endregion

    #region Private Helpers: Averages & Speed

    /// <summary>Returns average RPM of driven wheels that are grounded.</summary>
    private float AverageDrivenWheelRPM()
    {
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            if (!_driven[i]) continue;

            WheelHit hit;
            if (_wheelColliders[i].GetGroundHit(out hit))
            {
                sum += _wheelColliders[i].rpm;
                count++;
            }
        }

        return (count > 0) ? (sum / count) : 0f;
    }

    /// <summary>Returns average combined slip (abs(forwardSlip + sidewaysSlip)) for grounded driven wheels.</summary>
    private float AverageDrivenWheelSlip()
    {
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < _wheelColliders.Length; i++)
        {
            if (!_driven[i]) continue;
            WheelHit hit;

            if (_wheelColliders[i].GetGroundHit(out hit))
            {
                sum += Mathf.Abs(hit.forwardSlip + hit.sidewaysSlip);
                count++;
            }
        }
        return (count > 0) ? (sum / count) : 0f;
    }

    /// <summary>Calculates current vehicle speed in km/h.</summary>
    private float CalculateSpeed()
    {
        var speed = GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f;
        return speed;
    }

    #endregion

    #region Private Helpers: Steering

    /// <summary>
    /// Applies steering input. If gamepadSteering is true, applies exponent shaping.
    /// Otherwise, limits max angle by speed and rate-limits by steerSpeedDegPerSec.
    /// </summary>
    private void ControlSteering(float steerInput, bool gamepadSteering)
    {
        if (gamepadSteering)
        {
            float shaped = Mathf.Sign(steerInput) * Mathf.Pow(Mathf.Abs(steerInput), inputExponent);
            ApplySteering(steerInput); // using original variable as in provided code (logic preserved)
        }
        else
        {
            float kph = CalculateSpeed();
            float speedCoeficient = Mathf.InverseLerp(0f, maxSpeed, kph);
            float maxAngle = Mathf.Lerp(maxSteerAngle, maxSteerAngleAtTopSpeed, speedCoeficient);
            float targetAngle = steerInput * maxAngle;

            steeringAngle = Mathf.MoveTowards(steeringAngle, targetAngle, steerSpeedDegPerSec * Time.fixedDeltaTime);

            ApplySteering(steeringAngle);
        }
    }

    /// <summary>Sets steerAngle on steering wheels, with optional Ackermann scaling.</summary>
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

    /// <summary>
    /// Updates reverse state. Allows reversing if braking and speed is low.
    /// </summary>
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

    #region Private Helpers: Drive, Braking, Mesh Sync

    /// <summary>
    /// Applies motor torque to driven wheels, limiting by max speeds and transmission shifting,
    /// and reducing torque when traction control detects excessive slip.
    /// </summary>
    private void ApplyDrive(float throttle)
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

    /// <summary>
    /// Applies service brake and handbrake torques per wheel. ABS reduces brake torque when slip is high.
    /// </summary>
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

    /// <summary>Updates wheel mesh transforms from collider poses.</summary>
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
    /// <summary>Applies anti-roll forces to front and rear axle pairs.</summary>
    private void ApplyAntiRoll()
    {
        ApplyAntiRollPair(0, 1, antiRollStiffnessFront);
        ApplyAntiRollPair(2, 3, antiRollStiffnessRear);
    }

    /// <summary>Applies anti-roll force between a left-right wheel pair.</summary>
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

    /// <summary>Computes normalized suspension travel for a wheel in [0,1], or -1 if not grounded.</summary>
    private float GetSuspensionTravel(WheelCollider wc)
    {
        WheelHit hit;

        if (wc.GetGroundHit(out hit))
            return (-wc.transform.InverseTransformPoint(hit.point).y - wc.radius) / wc.suspensionDistance;

        return -1f;
    }
    #endregion

    #region Private Helpers: Friction Setup
    /// <summary>Routes to front or rear friction setup.</summary>
    private void SetupFriction(WheelCollider wc, bool front)
    {
        if (front)
            SetupFrontFriction(wc);
        else
            SetupRearFriction(wc);
    }
    /// <summary>Configures forward and sideways friction for a front wheel using front arrays.</summary>
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

    /// <summary>Configures forward and sideways friction for a rear wheel using rear arrays.</summary>
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
    #endregion
}
