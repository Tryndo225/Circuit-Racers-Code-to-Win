using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// High-level vehicle controller that wires player inputs to drivetrain, transmission,
/// engine audio, and lights. Manages component setup, input device detection, and per-tick
/// control calls in the physics loop.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @invariant Exactly 4 wheels are configured and each has both collider and visual assigned.
/// @invariant Required components (Rigidbody, EngineSound, TransmissionController, DriveTrainController, LightsController) are present after <see cref="SetUp"/>.
/// @thread Unity main thread for Unity methods; physics control in <see cref="FixedUpdate"/>.
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    #region Inspector: References

    [Header("References")]
    /// <summary>Vehicle rigidbody (auto-assigned in <see cref="SetUp"/>).</summary>
    [SerializeField, ReadOnly] private Rigidbody carRigidbody;

    /// <summary>
    /// Per-wheel specification: collider, visual mesh, drive and steering flags.
    /// Order: Front-Left, Front-Right, Rear-Left, Rear-Right (FL, FR, RL, RR).
    /// </summary>
    [Tooltip("Wheel Collider, Wheel Visual, Powered, Steering; Order: FL, FR, RL, RR")]
    [SerializeField] private WheelSpec[] wheels = new WheelSpec[4];

    #endregion

    #region Inspector: Inputs & Actions

    [Header("Inputs")]
    /// <summary>
    /// Current input device (Keyboard/Mouse or Gamepad). Auto-detected from the last
    /// performed input action. Read-only in the Inspector.
    /// </summary>
    [Tooltip("Input source: Keyboard/Mouse or Gamepad. Automatically detected.")]
    [SerializeField, ReadOnly] private InputSource currentInputDevice;

    /// <summary>
    /// Throttle action value. Typically mapped to W/S, Up/Down, or gamepad triggers via a 1D Axis composite.
    /// </summary>
    [Tooltip("Float -1..1 (stick) or 0..1 (trigger). Map in your actions asset.")]
    [SerializeField] private InputActionProperty throttleAction;

    /// <summary>
    /// Steering action value. Typically mapped to A/D, Left/Right, or gamepad left stick X.
    /// </summary>
    [Tooltip("Float -1..1 (left/right).")]
    [SerializeField] private InputActionProperty steerAction;

    /// <summary>
    /// Brake action. Button or axis; values &gt; 0.5 are considered pressed.
    /// </summary>
    [Tooltip("Button/float. > 0.5f counts as pressed.")]
    [SerializeField] private InputActionProperty brakeAction;

    /// <summary>
    /// Handbrake action. Button or axis; values &gt; 0.5 are considered pressed.
    /// </summary>
    [Tooltip("Button/float for handbrake.")]
    [SerializeField] private InputActionProperty handbrakeAction;

    /// <summary>
    /// Lights toggle action. Triggers <see cref="LightsController.ToggleLights"/>.
    /// </summary>
    [Tooltip("Button for Lights can be used for toggling lights on/off.")]
    [SerializeField] private InputActionProperty lightsToggleAction;

    /// <summary>
    /// When true, default actions and bindings are created at runtime if no user assignments exist.
    /// </summary>
    [SerializeField] private bool autoCreateDefaultBindingsIfMissing = false;

    #endregion

    #region Inspector: Vehicle Tuning

    [Header("Vehicle")]
    /// <summary>Center of Mass offset in local space (tuning for handling).</summary>
    [Tooltip("Center of Mass position relative to the vehicle's transform. Adjust for better handling.")]
    [SerializeField] private Vector3 coMPosition = new Vector3(0f, -0.4f, 0.5f);

    /// <summary>
    /// Ackermann steering factor. 1 ~ perfect Ackerman; lower prone to understeer, higher to oversteer.
    /// </summary>
    [Tooltip("Ackermann steering factor. Adjust for better cornering. 1 = perfect Ackermann, < 1 = understeer, > 1 = oversteer.")]
    [SerializeField] private float ackermannFactor = 1.1f;

    /// <summary>Maximum forward speed (km/h).</summary>
    [Tooltip("Maximum speed in km/h. Vehicle will not exceed this speed.")]
    [SerializeField] private float maxSpeed = 200f;

    /// <summary>Maximum reverse speed (km/h).</summary>
    [Tooltip("Maximum reverse speed in km/h. Vehicle will not exceed this speed in reverse.")]
    [SerializeField] private float maxReverseSpeed = 40f;

    /// <summary>Maximum steering angle (deg) at top speed.</summary>
    [Tooltip("Maximum steering angle in degrees at top speed. This is the maximum angle when driving at top speed.")]
    [SerializeField] private float maxSteerAngleAtTopSpeed = 5f;

    /// <summary>Maximum steering angle (deg) at 0 km/h.</summary>
    [Tooltip("Maximum steering angle in degrees at 0 speed. This is the maximum angle when stationary.")]
    [SerializeField] private float maxSteerAngle = 30f;

    /// <summary>Steering slew rate (deg/s).</summary>
    [Tooltip("Steering speed in degrees per second. This limits how fast the steering angle can change.")]
    [SerializeField] private float steerSpeedDegPerSec = 180f;

    /// <summary>Input shaping exponent (1 = linear response).</summary>
    [Tooltip("Exponent applied to contoler input values (1 = linear).")]
    [SerializeField, Range(1f, 3f)] private float inputExponent = 1.8f;

    /// <summary>Maximum motor power proxy, distributed across driven wheels.</summary>
    [Tooltip("Maximum power")]
    [SerializeField] private float maxMotorPower = 1200f;

    /// <summary>Maximum service brake torque per wheel (N·m).</summary>
    [Tooltip("Maximum brake torque per wheel (N·m).")]
    [SerializeField] private float maxBrakeTorque = 3000f;

    /// <summary>Handbrake torque applied to rear wheels (N·m).</summary>
    [Tooltip("Handbrake torque for rear wheels (N·m).")]
    [SerializeField] private float handbrakeTorque = 6000f;

    #endregion

    #region Inspector: Transmission & RPM

    [Header("Transmission & RPM")]
    /// <summary>Forward gear ratios (index 0 = 1st gear).</summary>
    [Tooltip("Forward gear ratios (1..N)")]
    [SerializeField] private float[] forwardGears = new float[] { 3.2f, 2.1f, 1.5f, 1.0f, 0.82f };

    /// <summary>Final drive ratio.</summary>
    [SerializeField] private float finalDrive = 3.42f;

    /// <summary>Engine idle RPM.</summary>
    [Tooltip("Engine idle RPM")]
    [SerializeField] private float idleRPM = 900f;

    /// <summary>Engine redline RPM (hard cap).</summary>
    [Tooltip("Engine redline RPM (max RPM)")]
    [SerializeField] private float redlineRPM = 6000f;

    /// <summary>Auto shift-up threshold (RPM).</summary>
    [Tooltip("Auto shift up when RPM exceeds this")]
    [SerializeField] private float shiftUpRPM = 4000f;

    /// <summary>Auto shift-down threshold (RPM).</summary>
    [Tooltip("Auto shift down when RPM falls below this")]
    [SerializeField] private float shiftDownRPM = 2000f;

    /// <summary>Shift torque-cut duration (s).</summary>
    [Tooltip("Seconds torque is cut during a shift")]
    [SerializeField] private float shiftDuration = 0.2f;

    /// <summary>Maximum wheel slip allowed to trigger/sustain a shift.</summary>
    [Tooltip("Slip threshold for shifting")]
    [SerializeField] private float shiftSlipThreshold = 0.5f;

    #endregion

    #region Inspector: Stability & Limits

    [Header("Stability")]
    /// <summary>Anti-roll stiffness for the front axle.</summary>
    [SerializeField] private float antiRollStiffnessFront = 400f;

    /// <summary>Anti-roll stiffness for the rear axle.</summary>
    [SerializeField] private float antiRollStiffnessRear = 500f;

    [Header("Traction Control & ABS Limits")]
    /// <summary>Enable traction control (reduces torque on high slip).</summary>
    [SerializeField] private bool tractionControlEnabled = true;

    /// <summary>Forward slip limit used by traction control.</summary>
    [SerializeField] private float tractionSlipLimit = 0.45f;

    /// <summary>Enable anti-lock braking (reduces brake torque on high slip).</summary>
    [SerializeField] private bool absEnabled = true;

    /// <summary>Forward slip limit used by ABS.</summary>
    [SerializeField] private float absSlipLimit = 0.55f;

    #endregion

    #region Inspector: Friction

    [Header("Forward Friction")]
    /// <summary>Front forward-friction stiffness.</summary>
    [SerializeField] private float frontForwardStiffness = 2.0f;
    /// <summary>Front forward-friction extremum slip.</summary>
    [SerializeField] private float frontForwardExtremumSlip = 0.4f;
    /// <summary>Front forward-friction extremum value.</summary>
    [SerializeField] private float frontForwardExtremumValue = 1f;
    /// <summary>Front forward-friction asymptote slip.</summary>
    [SerializeField] private float frontForwardAsymptoteSlip = 0.8f;
    /// <summary>Front forward-friction asymptote value.</summary>
    [SerializeField] private float frontForwardAsymptoteValue = 0.6f;

    /// <summary>Rear forward-friction stiffness.</summary>
    [SerializeField] private float rearForwardStiffness = 2.0f;
    /// <summary>Rear forward-friction extremum slip.</summary>
    [SerializeField] private float rearForwardExtremumSlip = 0.4f;
    /// <summary>Rear forward-friction extremum value.</summary>
    [SerializeField] private float rearForwardExtremumValue = 1f;
    /// <summary>Rear forward-friction asymptote slip.</summary>
    [SerializeField] private float rearForwardAsymptoteSlip = 0.8f;
    /// <summary>Rear forward-friction asymptote value.</summary>
    [SerializeField] private float rearForwardAsymptoteValue = 0.6f;

    [Header("Sideways Friction")]
    /// <summary>Front sideways-friction stiffness.</summary>
    [SerializeField] private float frontSidewaysStiffness = 2.1f;
    /// <summary>Front sideways-friction extremum slip.</summary>
    [SerializeField] private float frontSidewaysExtremumSlip = 0.3f;
    /// <summary>Front sideways-friction extremum value.</summary>
    [SerializeField] private float frontSidewaysExtremumValue = 1f;
    /// <summary>Front sideways-friction asymptote slip.</summary>
    [SerializeField] private float frontSidewaysAsymptoteSlip = 0.7f;
    /// <summary>Front sideways-friction asymptote value.</summary>
    [SerializeField] private float frontSidewaysAsymptoteValue = 0.5f;

    /// <summary>Rear sideways-friction stiffness.</summary>
    [SerializeField] private float rearSidewaysStiffness = 2.1f;
    /// <summary>Rear sideways-friction extremum slip.</summary>
    [SerializeField] private float rearSidewaysExtremumSlip = 0.3f;
    /// <summary>Rear sideways-friction extremum value.</summary>
    [SerializeField] private float rearSidewaysExtremumValue = 1f;
    /// <summary>Rear sideways-friction asymptote slip.</summary>
    [SerializeField] private float rearSidewaysAsymptoteSlip = 0.7f;
    /// <summary>Rear sideways-friction asymptote value.</summary>
    [SerializeField] private float rearSidewaysAsymptoteValue = 0.5f;

    #endregion

    #region Inspector: Lights

    [Header("Lights")]
    /// <summary>Front light intensity when on.</summary>
    [Tooltip("Intensity of the front lights when turned on.")]
    [SerializeField] private float frontLightsIntensity = 1000;
    /// <summary>Front light color when on.</summary>
    [Tooltip("Color of the front lights when turned on.")]
    [SerializeField] private Color frontLightsColor;
    /// <summary>List of front light components.</summary>
    [Tooltip("List of front lights")]
    [SerializeField] private List<Light> frontLights;

    /// <summary>Daylight intensity when on.</summary>
    [Tooltip("Intensity of the day lights when turned on.")]
    [SerializeField] private float dayLightsIntensity = 1;
    /// <summary>Daylight color when on.</summary>
    [Tooltip("Color of the day lights when turned on.")]
    [SerializeField] private Color dayLightsColor;
    /// <summary>List of daylight components.</summary>
    [Tooltip("List of daylights")]
    [SerializeField] private List<Light> dayLights;

    /// <summary>Rear light intensity when on.</summary>
    [Tooltip("Intensity of the rear lights when turned on.")]
    [SerializeField] private float rearLightsIntensity = 1;
    /// <summary>Rear light color when on.</summary>
    [Tooltip("Color of the rear lights when turned on.")]
    [SerializeField] private Color rearLightsColor;
    /// <summary>List of rear light components.</summary>
    [Tooltip("List of rear lights")]
    [SerializeField] private List<Light> rearLights;

    /// <summary>Reverse light intensity when on.</summary>
    [Tooltip("Intensity of the reverse lights when turned on.")]
    [SerializeField] private float reverseLightsIntensity = 5;
    /// <summary>Reverse light color when on.</summary>
    [Tooltip("Color of the reverse lights when turned on.")]
    [SerializeField] private Color reverseLightsColor;
    /// <summary>List of reverse light components.</summary>
    [Tooltip("List of reverse lights")]
    [SerializeField] private List<Light> reverseLights;

    /// <summary>Brake light intensity when braking.</summary>
    [Tooltip("Intensity of the brake lights when turned on.")]
    [SerializeField] private float brakeLightsIntensity = 5;
    /// <summary>Brake light color when braking.</summary>
    [Tooltip("Color of the brake lights when turned on.")]
    [SerializeField] private Color brakeLightsColor;
    /// <summary>List of brake light components.</summary>
    [Tooltip("List of brake lights")]
    [SerializeField] private List<Light> brakeLights;

    [Header("Lights : Fade Settings")]
    /// <summary>Fade time for lights on/off (seconds).</summary>
    [Tooltip("Duration for fading lights on and off.")]
    [SerializeField] private float fadeDuration = 0.1f;

    [Header("Lights : Initial State")]
    /// <summary>If true, front/day/rear lights start enabled.</summary>
    [SerializeField] private bool startLightsOn = false;

    #endregion

    #region Inspector: Engine Sound

    [Header("Engine Sound")]
    /// <summary>Audio mixer group used by the engine sound sources.</summary>
    [Tooltip("Main Output")]
    [SerializeField] private AudioMixerGroup outputGroup;
    /// <summary>Engine sound master volume [0..1].</summary>
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    /// <summary>Spatial blend of audio (0 = 2D, 1 = 3D).</summary>
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    /// <summary>Doppler effect intensity for engine sources.</summary>
    [SerializeField, Range(0f, 5f)] private float dopplerLevel = 0f;

    [Header("Engine Sound : Clips (On-Throttle)")]
    /// <summary>On-throttle idle clip.</summary>
    [SerializeField] private AudioClip on_Idle;
    /// <summary>On-throttle low band clip.</summary>
    [SerializeField] private AudioClip on_Low;
    /// <summary>On-throttle mid band clip.</summary>
    [SerializeField] private AudioClip on_Mid;
    /// <summary>On-throttle high band clip.</summary>
    [SerializeField] private AudioClip on_High;

    [Header("Engine Sound : Clips (Off-Throttle)")]
    /// <summary>Off-throttle idle clip.</summary>
    [SerializeField] private AudioClip off_Idle;
    /// <summary>Off-throttle low band clip.</summary>
    [SerializeField] private AudioClip off_Low;
    /// <summary>Off-throttle mid band clip.</summary>
    [SerializeField] private AudioClip off_Mid;
    /// <summary>Off-throttle high band clip.</summary>
    [SerializeField] private AudioClip off_High;

    [Header("Engine Sound : Smoothing")]
    /// <summary>RPM smoothing speed constant (higher = faster response).</summary>
    [Tooltip("Higher = Faster response")]
    [SerializeField] private float rpmLerpSpeed = 6f;
    /// <summary>Throttle smoothing speed constant.</summary>
    [SerializeField] private float throttleLerpSpeed = 8f;

    [Header("Engine Sound : Pitch Mapping")]
    /// <summary>Pitch vs normalized RPM curve.</summary>
    [Tooltip("AnimationCurve maps normalized RPM [0..1] to pitch multiplier.")]
    [SerializeField] private AnimationCurve pitchVsRpm = AnimationCurve.EaseInOut(0f, 0.7f, 1f, 2.0f);

    [Header("Engine Sound : Band Crossfade")]
    /// <summary>Centers of the four RPM bands [0..1] (idle/low/mid/high).</summary>
    [Tooltip("Center points of the four bands over normalized RPM")]
    [SerializeField] private Vector4 bandCenters = new Vector4(0f, 0.33f, 0.66f, 1.0f);
    /// <summary>Crossfade sharpness between RPM bands (higher = narrower bands).</summary>
    [Tooltip("How sharp the crossfade between bands is (bigger = narrower band).")]
    [SerializeField] private float bandSharpness = 6f;

    [Header("Engine Sound : On/Off Balance")]
    /// <summary>Throttle shaping exponent for on/off-throttle blend (1 = linear).</summary>
    [Tooltip("Exponent shaping for throttle : On-throttle weight (1=linear, > 1 favors off at mid throttle).")]
    [SerializeField] private float throttleShape = 1.25f;
    /// <summary>Extra gain applied to on-throttle layer relative to off-throttle.</summary>
    [Tooltip("Extra volume on-throttle compared to off-throttle.")]
    [SerializeField] private float onThrottleBoost = 1f;

    [Header("Engine Sound : Shift & Limiter")]
    /// <summary>Pitch flare amount on gear shifts.</summary>
    [SerializeField] private float shiftFlareAmount = 0.06f;
    /// <summary>Limiter activation start (normalized RPM, 0..1).</summary>
    [SerializeField] private float limiterStart = 0.96f;
    /// <summary>Limiter depth near redline.</summary>
    [SerializeField] private float limiterDepth = 0.25f;

    #endregion

    #region Components (cached)

    /// <summary>Runtime reference to engine sound component.</summary>
    [SerializeField, ReadOnly] private EngineSound _engineSound;
    /// <summary>Runtime reference to drivetrain controller.</summary>
    [SerializeField, ReadOnly] private DriveTrainController _driveTrainController;
    /// <summary>Runtime reference to transmission controller.</summary>
    [SerializeField, ReadOnly] private TransmissionController _transmissionController;
    /// <summary>Runtime reference to lights controller.</summary>
    [SerializeField, ReadOnly] private LightsController _lightsController;

    #endregion

    #region Runtime State

    /// <summary>Input actions created internally when auto-binding is enabled.</summary>
    private readonly List<InputAction> _ownedActions = new();

    /// <summary>Input device classification for steering model decisions.</summary>
    private enum InputSource { KeyboardMouse, Gamepad }

    #endregion

    #region Unity Methods

    /// <summary>
    /// Unity method: subscribes handlers and enables actions on component enable.
    /// </summary>
    private void OnEnable()
    {
        EnableAction(throttleAction);
        EnableAction(steerAction);
        EnableAction(brakeAction);
        EnableAction(handbrakeAction);
        EnableAction(lightsToggleAction);

        foreach (var action in _ownedActions)
            action.performed += DetectDevice;

        lightsToggleAction.action.performed += OnLightsPerformed;
    }

    /// <summary>
    /// Unity method: unsubscribes handlers and disables actions on component disable.
    /// </summary>
    private void OnDisable()
    {
        for (int i = 0; i < _ownedActions.Count; i++)
            _ownedActions[i].performed -= DetectDevice;

        DisableAction(throttleAction);
        DisableAction(steerAction);
        DisableAction(brakeAction);
        DisableAction(handbrakeAction);
        lightsToggleAction.action.performed -= OnLightsPerformed;
        DisableAction(lightsToggleAction);
    }

    /// <summary>
    /// Unity method: validates wheel configuration and runs <see cref="SetUp"/> in the editor.
    /// </summary>
    private void OnValidate()
    {
        if (wheels.Length != 4)
        {
            Debug.LogError("VehicleController requires exactly 4 wheel colliders.");
            return;
        }

        int correctCount = 0;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == new WheelSpec() || wheels[i].collider == null || wheels[i].visual == null)
            {
                Debug.LogError($"Wheel {i} is not properly configured. Please assign WheelCollider and WheelMesh.");
                return;
            }
            else
            {
                correctCount++;
            }
        }

        if (correctCount == wheels.Length) SetUp();
        else Debug.LogError("VehicleController requires exactly 4 properly configured wheels.");
    }

    /// <summary>
    /// Unity method: initializes component references and parameters on scene start.
    /// </summary>
    private void Start() => SetUp();

    /// <summary>
    /// Unity method: ensures dependent components exist and are configured on Reset.
    /// </summary>
    private void Reset()
    {
        _engineSound ??= gameObject.GetComponent<EngineSound>() ?? gameObject.AddComponent<EngineSound>();
        _transmissionController ??= gameObject.GetComponent<TransmissionController>() ?? gameObject.AddComponent<TransmissionController>();
        _driveTrainController ??= gameObject.GetComponent<DriveTrainController>() ?? gameObject.AddComponent<DriveTrainController>();
        _lightsController ??= gameObject.GetComponent<LightsController>() ?? gameObject.AddComponent<LightsController>();
        SetUp();
    }

    /// <summary>
    /// Unity method: cleans up any input actions that were created internally.
    /// </summary>
    private void OnDestroy() => DeleteActions();

    #region Unity Methods: FixedUpdate Loop

    /// <summary>
    /// Unity method: physics tick. Reads inputs, calls drivetrain control, updates lights,
    /// and feeds RPM/throttle to the engine sound.
    /// </summary>
    private void FixedUpdate()
    {
        float throttle = ReadFloat(throttleAction);
        float steer = ReadFloat(steerAction);
        bool braking = ReadBool(brakeAction);
        bool handbrake = ReadBool(handbrakeAction);

        _driveTrainController.ApplyWheelControls(throttle, braking, handbrake, steer, currentInputDevice == InputSource.Gamepad);
        _lightsController.SetBrakeLights(_driveTrainController.Braking);
        _lightsController.SetReverseLights(_driveTrainController.Reversing);

        UpdateAudio();
    }

    #endregion
    #endregion

    #region Setup Helpers

    /// <summary>
    /// Populates component references, pushes inspector tuning into subsystems,
    /// and creates default input actions if requested.
    /// </summary>
    private void SetUp()
    {
        if (!carRigidbody) carRigidbody = GetComponent<Rigidbody>();
        if (!carRigidbody) Debug.LogError("VehicleController requires a Rigidbody component on the same GameObject.");

        _engineSound ??= gameObject.GetComponent<EngineSound>();
        _transmissionController ??= gameObject.GetComponent<TransmissionController>();
        _driveTrainController ??= gameObject.GetComponent<DriveTrainController>();
        _lightsController ??= gameObject.GetComponent<LightsController>();

        SetUpLightsController();
        SetUpEngineSoundController();
        SetUpTransmissionController();
        SetUpDriveTrainController(_transmissionController);

        CreateDefaultInputActions();
    }

    /// <summary>
    /// Copies all light-related inspector values into <see cref="LightsController"/>.
    /// </summary>
    private void SetUpLightsController()
    {
        _lightsController.frontLightsIntensity = frontLightsIntensity;
        _lightsController.frontLightsColor = frontLightsColor;
        _lightsController.frontLights = frontLights;

        _lightsController.dayLightsIntensity = dayLightsIntensity;
        _lightsController.dayLightsColor = dayLightsColor;
        _lightsController.dayLights = dayLights;

        _lightsController.rearLightsIntensity = rearLightsIntensity;
        _lightsController.rearLightsColor = rearLightsColor;
        _lightsController.rearLights = rearLights;

        _lightsController.reverseLightsIntensity = reverseLightsIntensity;
        _lightsController.reverseLightsColor = reverseLightsColor;
        _lightsController.reverseLights = reverseLights;

        _lightsController.brakeLightsIntensity = brakeLightsIntensity;
        _lightsController.brakeLightsColor = brakeLightsColor;
        _lightsController.brakeLights = brakeLights;

        _lightsController.fadeDuration = fadeDuration;
        _lightsController.startLightsOn = startLightsOn;
    }

    /// <summary>
    /// Copies audio tuning and clips into <see cref="EngineSound"/> and applies clamped settings.
    /// </summary>
    private void SetUpEngineSoundController()
    {
        _engineSound.minRPM = idleRPM;
        _engineSound.maxRPM = redlineRPM;
        _engineSound.outputGroup = outputGroup;
        _engineSound.masterVolume = masterVolume;
        _engineSound.spatialBlend = spatialBlend;
        _engineSound.dopplerLevel = dopplerLevel;

        _engineSound.on_Idle = on_Idle;
        _engineSound.on_Low = on_Low;
        _engineSound.on_Mid = on_Mid;
        _engineSound.on_High = on_High;

        _engineSound.off_Idle = off_Idle;
        _engineSound.off_Low = off_Low;
        _engineSound.off_Mid = off_Mid;
        _engineSound.off_High = off_High;

        _engineSound.rpmLerpSpeed = rpmLerpSpeed;
        _engineSound.throttleLerpSpeed = throttleLerpSpeed;
        _engineSound.pitchVsRpm = pitchVsRpm;
        _engineSound.bandCenters = bandCenters;
        _engineSound.bandSharpness = bandSharpness;
        _engineSound.throttleShape = throttleShape;
        _engineSound.onThrottleBoost = onThrottleBoost;
        _engineSound.shiftFlareAmount = shiftFlareAmount;
        _engineSound.shiftFlareTime = shiftDuration;
        _engineSound.limiterStart = limiterStart;
        _engineSound.limiterDepth = limiterDepth;

        _engineSound.SetUp();
    }

    /// <summary>
    /// Configures <see cref="TransmissionController"/> with gear ratios, RPM limits,
    /// shift behavior and shift callbacks.
    /// </summary>
    private void SetUpTransmissionController()
    {
        _transmissionController.forwardGears = forwardGears;
        _transmissionController.finalDrive = finalDrive;
        _transmissionController.idleRPM = idleRPM;
        _transmissionController.redlineRPM = redlineRPM;
        _transmissionController.shiftUpRPM = shiftUpRPM;
        _transmissionController.shiftDownRPM = shiftDownRPM;
        _transmissionController.shiftDuration = shiftDuration;
        _transmissionController.slipThreshold = shiftSlipThreshold;

        _transmissionController.OnShift = new List<System.Action>();
        _transmissionController.OnShift.Add(_engineSound.OnShift);
    }

    /// <summary>
    /// Transfers wheel specs and vehicle tuning into <see cref="DriveTrainController"/>,
    /// then calls its <see cref="DriveTrainController.SetUp"/> to apply physics settings.
    /// </summary>
    /// <param name="transmissionController">Transmission instance to pass to the drivetrain.</param>
    private void SetUpDriveTrainController(TransmissionController transmissionController)
    {
        var wheelMeshes = new Transform[wheels.Length];
        var wheelsColliders = new WheelCollider[wheels.Length];
        var driven = new bool[wheels.Length];
        var steering = new bool[wheels.Length];

        for (int i = 0; i < wheels.Length; i++)
        {
            wheelMeshes[i] = wheels[i].visual;
            wheelsColliders[i] = wheels[i].collider;
            driven[i] = wheels[i].powered;
            steering[i] = wheels[i].steering;
        }

        _driveTrainController.Init(carRigidbody, transmissionController, wheelsColliders, wheelMeshes, driven, steering);
        _driveTrainController.maxSpeed = maxSpeed;
        _driveTrainController.maxReverseSpeed = maxReverseSpeed;
        _driveTrainController.maxSteerAngleAtTopSpeed = maxSteerAngleAtTopSpeed;
        _driveTrainController.maxSteerAngle = maxSteerAngle;
        _driveTrainController.steerSpeedDegPerSec = steerSpeedDegPerSec;
        _driveTrainController.inputExponent = inputExponent;
        _driveTrainController.maxMotorPower = maxMotorPower;
        _driveTrainController.maxBrakeTorque = maxBrakeTorque;
        _driveTrainController.handbrakeTorque = handbrakeTorque;
        _driveTrainController.antiRollStiffnessFront = antiRollStiffnessFront;
        _driveTrainController.antiRollStiffnessRear = antiRollStiffnessRear;
        _driveTrainController.tractionControlEnabled = tractionControlEnabled;
        _driveTrainController.tractionSlipLimit = tractionSlipLimit;
        _driveTrainController.absEnabled = absEnabled;
        _driveTrainController.absSlipLimit = absSlipLimit;

        var frontForewardFriction = new float[]
        {
            frontForwardStiffness,
            frontForwardAsymptoteSlip,
            frontForwardExtremumSlip,
            frontForwardAsymptoteValue,
            frontForwardExtremumValue
        };
        _driveTrainController.frontForwardFriction = frontForewardFriction;

        var rearForwardFriction = new float[]
        {
            rearForwardStiffness,
            rearForwardAsymptoteSlip,
            rearForwardExtremumSlip,
            rearForwardAsymptoteValue,
            rearForwardExtremumValue
        };
        _driveTrainController.rearForwardFriction = rearForwardFriction;

        var frontSidewaysFriction = new float[]
        {
            frontSidewaysStiffness,
            frontSidewaysAsymptoteSlip,
            frontSidewaysExtremumSlip,
            frontSidewaysAsymptoteValue,
            frontSidewaysExtremumValue
        };
        _driveTrainController.frontSidewaysFriction = frontSidewaysFriction;

        var rearSidewaysFriction = new float[]
        {
            rearSidewaysStiffness,
            rearSidewaysAsymptoteSlip,
            rearSidewaysExtremumSlip,
            rearSidewaysAsymptoteValue,
            rearSidewaysExtremumValue
        };
        _driveTrainController.rearSidewaysFriction = rearSidewaysFriction;

        _driveTrainController.coMPosition = coMPosition;
        _driveTrainController.ackermannFactor = ackermannFactor;

        _driveTrainController.SetUp();
    }

    #endregion

    #region Audio

    /// <summary>
    /// Pushes current engine RPM from transmission and current throttle to the engine sound system.
    /// </summary>
    private void UpdateAudio()
    {
        _engineSound.RPM = _transmissionController.EngineRPM;
        _engineSound.throttle = ReadFloat(throttleAction);
    }

    #endregion

    #region Input Helpers
    #region Input Detection
    /// <summary>
    /// Action callback used to detect the active input device and switch steering model if needed.
    /// </summary>
    /// <param name="ctx">Input callback context provided by the Input System.</param>
    private void DetectDevice(InputAction.CallbackContext ctx)
    {
        var control = ctx.control ?? ctx.action.activeControl;
        if (control == null) return;

        var device = control.device;
        var newSource = (device is Gamepad) ? InputSource.Gamepad : InputSource.KeyboardMouse;

        if (newSource != currentInputDevice)
        {
            currentInputDevice = newSource;
            Debug.Log($"Switched to: {currentInputDevice} via {device.displayName}");
        }
    }
    #endregion

    #region Input Action Creation
    /// <summary>
    /// Creates default actions and bindings when <see cref="autoCreateDefaultBindingsIfMissing"/> is true
    /// and properties have no user-assigned actions.
    /// </summary>
    private void CreateDefaultInputActions()
    {
        if (autoCreateDefaultBindingsIfMissing)
        {
            _ownedActions.Clear();
            EnsureActionBound(ref throttleAction, CreateDefaultThrottleBind());
            EnsureActionBound(ref steerAction, CreateDefaultSteerBind());
            EnsureActionBound(ref brakeAction, CreateDefaultBrakeBind());
            EnsureActionBound(ref handbrakeAction, CreateDefaultHandbrakeBind());
            EnsureActionBound(ref lightsToggleAction, CreateDefaultLightsBind());
        }
        else
        {
            DeleteActions();
        }
    }

    /// <summary>
    /// Assigns a generated action to a property if the property has no user assignment.
    /// </summary>
    /// <param name="property">Property to populate.</param>
    /// <param name="action">Action to assign and track for cleanup.</param>
    private void EnsureActionBound(ref InputActionProperty property, InputAction action)
    {
        if (HasUserAssignment(property)) return;
        property = new InputActionProperty(action);
        _ownedActions.Add(action);
    }

    #region Default Action Creators
    /// <summary>
    /// Creates a default throttle action with keyboard and gamepad composite bindings.
    /// </summary>
    /// <returns>Newly created <see cref="InputAction"/>.</returns>
    private InputAction CreateDefaultThrottleBind()
    {
        var throttle = new InputAction("Throttle", InputActionType.Value, expectedControlType: "Axis");
        throttle.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/s")
            .With("positive", "<Keyboard>/w");
        throttle.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/downArrow")
            .With("positive", "<Keyboard>/upArrow");
        throttle.AddCompositeBinding("1DAxis")
            .With("negative", "<DualShockGamepad>/leftTrigger")
            .With("positive", "<DualShockGamepad>/rightTrigger");
        throttle.AddCompositeBinding("1DAxis")
            .With("negative", "<Gamepad>/rightTrigger")
            .With("positive", "<Gamepad>/leftTrigger");
        return throttle;
    }

    /// <summary>
    /// Creates a default steer action with keyboard and gamepad bindings (deadzone applied).
    /// </summary>
    /// <returns>Newly created <see cref="InputAction"/>.</returns>
    private InputAction CreateDefaultSteerBind()
    {
        var steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
        steer.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/a")
            .With("positive", "<Keyboard>/d");
        steer.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/leftArrow")
            .With("positive", "<Keyboard>/rightArrow");
        steer.AddBinding("<DualShockGamepad>/leftStick/x").WithProcessor("stickDeadzone(min=0.1)");
        steer.AddBinding("<Gamepad>/leftStick/x").WithProcessor("stickDeadzone(min=0.1)");
        return steer;
    }

    /// <summary>
    /// Creates a default brake action with keyboard and gamepad trigger bindings.
    /// </summary>
    /// <returns>Newly created <see cref="InputAction"/>.</returns>
    private InputAction CreateDefaultBrakeBind()
    {
        var brake = new InputAction("Brake", InputActionType.Value, expectedControlType: "Button");
        brake.AddBinding("<Keyboard>/s");
        brake.AddBinding("<Keyboard>/downArrow");
        brake.AddBinding("<DualShockGamepad>/leftTrigger");
        brake.AddBinding("<Gamepad>/leftTrigger");
        return brake;
    }

    /// <summary>
    /// Creates a default handbrake action (keyboard space / gamepad south button).
    /// </summary>
    /// <returns>Newly created <see cref="InputAction"/>.</returns>
    private InputAction CreateDefaultHandbrakeBind()
    {
        var handbrake = new InputAction("Handbrake", InputActionType.Button, expectedControlType: "Button");
        handbrake.AddBinding("<Keyboard>/space");
        handbrake.AddBinding("<DualShockGamepad>/crossButton");
        handbrake.AddBinding("<Gamepad>/buttonSouth");
        return handbrake;
    }

    /// <summary>
    /// Creates a default lights toggle action (keyboard L / gamepad D-Pad up).
    /// </summary>
    /// <returns>Newly created <see cref="InputAction"/>.</returns>
    private InputAction CreateDefaultLightsBind()
    {
        var lights = new InputAction("LightsToggle", InputActionType.Button, expectedControlType: "Button");
        lights.AddBinding("<Keyboard>/l").WithInteraction("Press");
        lights.AddBinding("<DualShockGamepad>/dpad/up").WithInteractions("Press");
        lights.AddBinding("<Gamepad>/dpad/up").WithInteraction("Press");
        return lights;
    }
    #endregion
    #endregion

    #region Input Action Deletion & Cleanup
    /// <summary>
    /// Checks whether the property already has a user-assigned action or reference.
    /// </summary>
    /// <param name="p">Property to check.</param>
    /// <returns>True if there is a user assignment; otherwise false.</returns>
    private static bool HasUserAssignment(InputActionProperty p)
    {
        if (p.reference != null) return true;
        var a = p.action;
        return a != null && a.bindings.Count > 0;
    }

    /// <summary>
    /// Disposes all actions that were created internally by this controller and clears properties.
    /// </summary>
    private void DeleteActions()
    {
        DeleteAction(ref throttleAction);
        DeleteAction(ref steerAction);
        DeleteAction(ref brakeAction);
        DeleteAction(ref handbrakeAction);
        DeleteAction(ref lightsToggleAction);
    }

    /// <summary>
    /// Disposes a specific property action if it was owned by this controller,
    /// then replaces it with a placeholder <see cref="InputAction"/>.
    /// </summary>
    /// <param name="property">Property to clear.</param>
    private void DeleteAction(ref InputActionProperty property)
    {
        if (property.action != null && _ownedActions.Contains(property.action))
        {
            property.action.Dispose();
            _ownedActions.Remove(property.action);
        }
        property = new InputActionProperty(new InputAction("Action"));
    }
    #endregion

    #region Input Action Readers
    /// <summary>
    /// Action callback for lights toggle; forwards to <see cref="LightsController.ToggleLights"/>.
    /// </summary>
    /// <param name="ctx">Input callback context.</param>
    private void OnLightsPerformed(InputAction.CallbackContext ctx) => _lightsController.ToggleLights();

    /// <summary>
    /// Reads a float value from the given action property; returns 0 if none assigned.
    /// </summary>
    /// <param name="prop">Action property to read.</param>
    /// <returns>Float value or 0 if not available.</returns>
    private static float ReadFloat(InputActionProperty prop)
        => prop.action != null ? prop.action.ReadValue<float>() : 0f;

    /// <summary>
    /// Reads a boolean from a button/axis action property; thresholds at 0.5.
    /// </summary>
    /// <param name="prop">Action property to read.</param>
    /// <returns>True if greater than 0.5, otherwise false.</returns>
    private static bool ReadBool(InputActionProperty prop)
        => prop.action != null && prop.action.ReadValue<float>() > 0.5f;
    #endregion

    #region Action Enable/Disable
    /// <summary>
    /// Enables the action in the given property if it exists and is not already enabled.
    /// </summary>
    /// <param name="prop">Action property to enable.</param>
    private static void EnableAction(InputActionProperty prop)
    {
        if (prop.action != null && !prop.action.enabled)
            prop.action.Enable();
    }

    /// <summary>
    /// Disables the action in the given property if it exists and is currently enabled.
    /// </summary>
    /// <param name="prop">Action property to disable.</param>
    private static void DisableAction(InputActionProperty prop)
    {
        if (prop.action != null && prop.action.enabled)
            prop.action.Disable();
    }
    #endregion
    #endregion
}
