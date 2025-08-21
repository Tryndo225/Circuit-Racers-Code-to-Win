using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField, ReadOnly] private Rigidbody carRigidbody;

    [Tooltip("Wheel Collider, Wheel Visual, Powered, Steering; Order: FL, FR, RL, RR")]
    [SerializeField] private WheelSpec[] wheels = new WheelSpec[4];

    [Header("Inputs")]
    [Tooltip("Input source: Keyboard/Mouse or Gamepad. Automatically detected.")]
    [SerializeField, ReadOnly] private InputSource currentInputDevice;

    [Tooltip("Float -1..1 (stick) or 0..1 (trigger). Map in your actions asset.")]
    [SerializeField] private InputActionProperty throttleAction;

    [Tooltip("Float -1..1 (left/right).")]
    [SerializeField] private InputActionProperty steerAction;

    [Tooltip("Button/float. > 0.5f counts as pressed.")]
    [SerializeField] private InputActionProperty brakeAction;

    [Tooltip("Button/float for handbrake.")]
    [SerializeField] private InputActionProperty handbrakeAction;

    [Tooltip("Button for Lights can be used for toggling lights on/off.")]
    [SerializeField] private InputActionProperty lightsToggleAction;

    [Header("Vehicle")]
    [Tooltip("Maximum speed in km/h. Vehicle will not exceed this speed.")]
    [SerializeField] private float maxSpeed = 200f;  // km/h

    [Tooltip("Maximum reverse speed in km/h. Vehicle will not exceed this speed in reverse.")]
    [SerializeField] private float maxReverseSpeed = 40f; // km/h

    [Tooltip("Maximum steering angle in degrees at top speed. This is the maximum angle when driving at top speed.")]
    [SerializeField] private float maxSteerAngleAtTopSpeed = 5f;

    [Tooltip("Maximum steering angle in degrees at 0 speed. This is the maximum angle when stationary.")]
    [SerializeField] private float maxSteerAngle = 30f;

    [Tooltip("Steering speed in degrees per second. This limits how fast the steering angle can change.")]
    [SerializeField] private float steerSpeedDegPerSec = 180f; // degrees per second

    [Tooltip("Exponent applied to contoler input values (1 = linear).")]
    [SerializeField, Range(1f, 3f)] private float inputExponent = 1.8f;

    [Tooltip("Maximum power")]
    [SerializeField] private float maxMotorPower = 1200f;

    [Tooltip("Maximum brake torque per wheel (N·m).")]
    [SerializeField] private float maxBrakeTorque = 3000f; // per wheel (N·m)

    [Tooltip("Handbrake torque for rear wheels (N·m).")]
    [SerializeField] private float handbrakeTorque = 6000f;

    [Header("Transmission & RPM")]
    [Tooltip("Forward gear ratios (1..N)")]
    [SerializeField] private float[] forwardGears = new float[] { 3.2f, 2.1f, 1.5f, 1.0f, 0.82f };

    [SerializeField] private float finalDrive = 3.42f;

    [Tooltip("Engine idle RPM")]
    [SerializeField] private float idleRPM = 900f;

    [Tooltip("Engine redline RPM (max RPM)")]
    [SerializeField] private float redlineRPM = 6000f;

    [Tooltip("Auto shift up when RPM exceeds this")]
    [SerializeField] private float shiftUpRPM = 4000f;

    [Tooltip("Auto shift down when RPM falls below this")]
    [SerializeField] private float shiftDownRPM = 2000f;

    [Tooltip("Seconds torque is cut during a shift")]
    [SerializeField] private float shiftDuration = 0.2f;

    [Header("Stability")]
    [SerializeField] private float antiRollStiffnessFront = 400f;

    [SerializeField] private float antiRollStiffnessRear = 500f;

    [Header("Traction Control & ABS Limits")]
    [SerializeField] private bool tractionControlEnabled = true;

    [SerializeField] private float tractionSlipLimit = 0.45f;
    [SerializeField] private bool absEnabled = true;
    [SerializeField] private float absSlipLimit = 0.55f;

    [Header("Forward Friction")]
    [SerializeField] private float frontForwardStiffness = 2.0f;

    [SerializeField] private float frontForwardExtremumSlip = 0.4f;
    [SerializeField] private float frontForwardExtremumValue = 1f;
    [SerializeField] private float frontForwardAsymptoteSlip = 0.8f;
    [SerializeField] private float frontForwardAsymptoteValue = 0.6f;

    [SerializeField] private float rearForwardStiffness = 2.0f;
    [SerializeField] private float rearForwardExtremumSlip = 0.4f;
    [SerializeField] private float rearForwardExtremumValue = 1f;
    [SerializeField] private float rearForwardAsymptoteSlip = 0.8f;
    [SerializeField] private float rearForwardAsymptoteValue = 0.6f;

    [Header("Sideways Friction")]
    [SerializeField] private float frontSidewaysStiffness = 2.1f;

    [SerializeField] private float frontSidewaysExtremumSlip = 0.3f;
    [SerializeField] private float frontSidewaysExtremumValue = 1f;
    [SerializeField] private float frontSidewaysAsymptoteSlip = 0.7f;
    [SerializeField] private float frontSidewaysAsymptoteValue = 0.5f;

    [SerializeField] private float rearSidewaysStiffness = 2.1f;
    [SerializeField] private float rearSidewaysExtremumSlip = 0.3f;
    [SerializeField] private float rearSidewaysExtremumValue = 1f;
    [SerializeField] private float rearSidewaysAsymptoteSlip = 0.7f;
    [SerializeField] private float rearSidewaysAsymptoteValue = 0.5f;

    [Header("Lights Configuration")]
    [Tooltip("Intensity of the front lights when turned on.")]
    [SerializeField] private float frontLightsIntensity = 1000;

    [Tooltip("Color of the front lights when turned on.")]
    [SerializeField] private Color frontLightsColor;

    [Tooltip("List of front lights")]
    [SerializeField] private List<Light> frontLights;

    [Tooltip("Intensity of the day lights when turned on.")]
    [SerializeField] private float dayLightsIntensity = 1;

    [Tooltip("Color of the day lights when turned on.")]
    [SerializeField] private Color dayLightsColor;

    [Tooltip("List of daylights")]
    [SerializeField] private List<Light> dayLights;

    [Tooltip("Intensity of the rear lights when turned on.")]
    [SerializeField] private float rearLightsIntensity = 1;

    [Tooltip("Color of the rear lights when turned on.")]
    [SerializeField] private Color rearLightsColor;

    [Tooltip("List of rear lights")]
    [SerializeField] private List<Light> rearLights;

    [Tooltip("Intensity of the reverse lights when turned on.")]
    [SerializeField] private float reverseLightsIntensity = 5;

    [Tooltip("Color of the reverse lights when turned on.")]
    [SerializeField] private Color reverseLightsColor;

    [Tooltip("List of reverse lights")]
    [SerializeField] private List<Light> reverseLights;

    [Tooltip("Intensity of the brake lights when turned on.")]
    [SerializeField] private float brakeLightsIntensity = 5;

    [Tooltip("Color of the brake lights when turned on.")]
    [SerializeField] private Color brakeLightsColor;

    [Tooltip("List of brake lights")]
    [SerializeField] private List<Light> brakeLights;

    [Header("Fade Settings")]
    [Tooltip("Duration for fading lights on and off.")]
    [SerializeField] private float fadeDuration = 0.1f;

    [Header("Initial State")]
    [SerializeField] private bool startLightsOn = false;

    [Header("EngineSound")]
    [Header("Main Output")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField, Range(0f, 5f)] private float dopplerLevel = 0f;

    [Header("Clips (On-Throttle)")]
    [SerializeField] private AudioClip on_Idle;

    [SerializeField] private AudioClip on_Low;
    [SerializeField] private AudioClip on_Mid;
    [SerializeField] private AudioClip on_High;

    [Header("Clips (Off-Throttle)")]
    [SerializeField] private AudioClip off_Idle;

    [SerializeField] private AudioClip off_Low;
    [SerializeField] private AudioClip off_Mid;
    [SerializeField] private AudioClip off_High;

    [Header("Smoothing")]
    [Tooltip("Higher = Faster response")]
    [SerializeField] private float rpmLerpSpeed = 6f;

    [SerializeField] private float throttleLerpSpeed = 8f;

    [Header("Pitch Mapping")]
    [Tooltip("AnimationCurve maps normalized RPM [0..1] to pitch multiplier.")]
    [SerializeField] private AnimationCurve pitchVsRpm = AnimationCurve.EaseInOut(0f, 0.7f, 1f, 2.0f);

    [Header("Band Crossfade")]
    [Tooltip("Center points of the four bands over normalized RPM")]
    [SerializeField] private Vector4 bandCenters = new Vector4(0f, 0.33f, 0.66f, 1.0f);

    [Tooltip("How sharp the crossfade between bands is (bigger = narrower band).")]
    [SerializeField] private float bandSharpness = 6f;

    [Header("On/Off Balance")]
    [Tooltip("Exponent shaping for throttle : On-throttle weight (1=linear, > 1 favors off at mid throttle).")]
    [SerializeField] private float throttleShape = 1.25f;

    [Tooltip("Extra volume on-throttle compared to off-throttle.")]
    [SerializeField] private float onThrottleBoost = 1f;

    [Header("Shift & Limiter")]
    [SerializeField] private bool enableShiftFlare = true;

    [SerializeField] private float shiftFlareAmount = 0.06f;
    [SerializeField] private float shiftFlareTime = 0.2f;

    [SerializeField] private bool enableSoftLimiter = true;
    [SerializeField] private float limiterStart = 0.96f;
    [SerializeField] private float limiterDepth = 0.25f;

    [Header("Miscellaneous")]
    [SerializeField] private float comYOffset = -0.4f;

    [SerializeField] private float ackermannFactor = 1f;
    [SerializeField] private bool autoCreateDefaultBindingsIfMissing = false;

    [SerializeField, ReadOnly] private EngineSound _engineSound;
    [SerializeField, ReadOnly] private DriveTrainContoller _driveTrainController;
    [SerializeField, ReadOnly] private TransmissionController _transmissionController;
    [SerializeField, ReadOnly] private LightsController _lightsController;

    private readonly List<InputAction> _ownedActions = new();

    private enum InputSource
    {
        KeyboardMouse,
        Gamepad
    }

    private void OnEnable()
    {
        EnableAction(throttleAction);
        EnableAction(steerAction);
        EnableAction(brakeAction);
        EnableAction(handbrakeAction);
        EnableAction(lightsToggleAction);

        foreach (var action in _ownedActions)
        {
            action.performed += DetectDevice;
        }
        lightsToggleAction.action.performed += OnLightsPerformed;
    }

    private void OnDisable()
    {
        for (int i = 0; i < _ownedActions.Count; i++)
        {
            _ownedActions[i].performed -= DetectDevice;
        }

        DisableAction(throttleAction);
        DisableAction(steerAction);
        DisableAction(brakeAction);
        DisableAction(handbrakeAction);
        lightsToggleAction.action.performed -= OnLightsPerformed;
        DisableAction(lightsToggleAction);
    }

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

        if (correctCount == wheels.Length)
        {
            SetUp();
        }
        else
        {
            Debug.LogError("VehicleController requires exactly 4 properly configured wheels.");
        }
    }

    private void Start()
    {
        SetUp();
    }

    private void Reset()
    {
        if (_engineSound == null)
            _engineSound = gameObject.GetComponent<EngineSound>();
        if (_engineSound == null)
            _engineSound = gameObject.AddComponent<EngineSound>();
        if (_transmissionController == null)
            _transmissionController = gameObject.GetComponent<TransmissionController>();
        if (_transmissionController == null)
            _transmissionController = gameObject.AddComponent<TransmissionController>();
        if (_driveTrainController == null)
            _driveTrainController = gameObject.GetComponent<DriveTrainContoller>();
        if (_driveTrainController == null)
            gameObject.AddComponent<DriveTrainContoller>();
        if (_lightsController == null)
            _lightsController = gameObject.GetComponent<LightsController>();
        if (_lightsController == null)
            _lightsController = gameObject.AddComponent<LightsController>();

        SetUp();
    }

    private void OnDestroy()
    {
        DeleteActions();
    }

    // --- Helper Properties Methods ---
    private void SetUp()
    {
        if (!carRigidbody)
            carRigidbody = GetComponent<Rigidbody>();

        if (!carRigidbody)
            Debug.LogError("VehicleController requires a Rigidbody component on the same GameObject.");

        if (_engineSound == null)
            _engineSound = gameObject.GetComponent<EngineSound>();
        if (_transmissionController == null)
            _transmissionController = gameObject.GetComponent<TransmissionController>();
        if (_driveTrainController == null)
            _driveTrainController = gameObject.GetComponent<DriveTrainContoller>();
        if (_lightsController == null)
            _lightsController = gameObject.GetComponent<LightsController>();

        SetUpLightsController();
        SetUpEngineSoundController();
        SetUpTransmissionController();
        SetUpDriveTrainController(_transmissionController);

        CreateDefaultInputActions();
    }

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
        _engineSound.enableShiftFlare = enableShiftFlare;
        _engineSound.shiftFlareAmount = shiftFlareAmount;
        _engineSound.shiftFlareTime = shiftFlareTime;
        _engineSound.enableSoftLimiter = enableSoftLimiter;
        _engineSound.limiterStart = limiterStart;
        _engineSound.limiterDepth = limiterDepth;

        _engineSound.SetUp();
    }

    private void SetUpTransmissionController()
    {
        _transmissionController.forwardGears = forwardGears;
        _transmissionController.finalDrive = finalDrive;
        _transmissionController.idleRPM = idleRPM;
        _transmissionController.redlineRPM = redlineRPM;
        _transmissionController.shiftUpRPM = shiftUpRPM;
        _transmissionController.shiftDownRPM = shiftDownRPM;
        _transmissionController.shiftDuration = shiftDuration;
        _transmissionController.OnShift = new List<System.Action>();
        _transmissionController.OnShift.Add(_engineSound.OnShift);
    }

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

        _driveTrainController.comYOffset = comYOffset;
        _driveTrainController.ackermannFactor = ackermannFactor;

        _driveTrainController.SetUp();
    }

    // --- FixedUpdate Logic ---
    // -------------------------
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

    // --- Audio Logic ---
    // -------------------

    private void UpdateAudio()
    {
        _engineSound.RPM = _transmissionController.EngineRPM;
        _engineSound.throttle = ReadFloat(throttleAction);
    }

    // --- Input helpers ---
    private void DetectDevice(InputAction.CallbackContext ctx)
    {
        // For composites, ctx.control is the exact part that changed (e.g., <Keyboard>/w or <Gamepad>/leftTrigger)
        var control = ctx.control ?? ctx.action.activeControl;
        if (control == null) return;

        var device = control.device;

        var newSource =
            (device is Gamepad) ? InputSource.Gamepad : InputSource.KeyboardMouse;

        if (newSource != currentInputDevice)
        {
            currentInputDevice = newSource;
            Debug.Log($"Switched to: {currentInputDevice} via {device.displayName}");
        }
    }

    private void OnLightsPerformed(InputAction.CallbackContext ctx)
    {
        _lightsController.ToggleLights();
    }

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

    private void EnsureActionBound(ref InputActionProperty property, InputAction action)
    {
        if (HasUserAssignment(property))
            return;

        property = new InputActionProperty(action);
        _ownedActions.Add(action);
    }

    private InputAction CreateDefaultThrottleBind()
    {
        var throttle = new InputAction("Throttle", InputActionType.Value, expectedControlType: "Axis");
        throttle.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/s")
            .With("positive", "<Keyboard>/w");
        throttle.AddCompositeBinding("1DAxis")
            .With("negative", "<DualShockGamepad>/leftTrigger")
            .With("positive", "<DualShockGamepad>/rightTrigger");
        throttle.AddCompositeBinding("1DAxis")
            .With("negative", "<Gamepad>/rightTrigger")
            .With("positive", "<Gamepad>/leftTrigger");

        return throttle;
    }

    private InputAction CreateDefaultSteerBind()
    {
        var steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
        steer.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/a")
            .With("positive", "<Keyboard>/d");
        steer.AddBinding("<DualShockGamepad>/leftStick/x").WithProcessor("stickDeadzone(min=0.1)");
        steer.AddBinding("<Gamepad>/leftStick/x").WithProcessor("stickDeadzone(min=0.1)");
        return steer;
    }

    private InputAction CreateDefaultBrakeBind()
    {
        var brake = new InputAction("Brake", InputActionType.Value, expectedControlType: "Button");
        brake.AddBinding("<Keyboard>/s");                               // 0..1
        brake.AddBinding("<DualShockGamepad>/leftTrigger");             // PS L2 (0..1)
        brake.AddBinding("<Gamepad>/leftTrigger");                      // fallback
        return brake;
    }

    private InputAction CreateDefaultHandbrakeBind()
    {
        var handbrake = new InputAction("Handbrake", InputActionType.Button, expectedControlType: "Button");
        handbrake.AddBinding("<Keyboard>/space");                       // space key
        handbrake.AddBinding("<DualShockGamepad>/crossButton");         // PS Cross (X)
        handbrake.AddBinding("<Gamepad>/buttonSouth");                  // fallback
        return handbrake;
    }

    private InputAction CreateDefaultLightsBind()
    {
        var lights = new InputAction("LightsToggle", InputActionType.Button, expectedControlType: "Button");
        lights.AddBinding("<Keyboard>/l").WithInteraction("Press");        // PC
        lights.AddBinding("<DualShockGamepad>/dpad/up").WithInteractions("Press"); // PS pads
        lights.AddBinding("<Gamepad>/dpad/up").WithInteraction("Press");   // fallback

        return lights;
    }

    private static bool HasUserAssignment(InputActionProperty p)
    {
        if (p.reference != null) return true;

        var a = p.action;
        return a != null && a.bindings.Count > 0;
    }

    private void DeleteActions()
    {
        DeleteAction(ref throttleAction);
        DeleteAction(ref steerAction);
        DeleteAction(ref brakeAction);
        DeleteAction(ref handbrakeAction);
        DeleteAction(ref lightsToggleAction);
    }

    private void DeleteAction(ref InputActionProperty property)
    {
        if (property.action != null && _ownedActions.Contains(property.action))
        {
            property.action.Dispose();
            _ownedActions.Remove(property.action);
        }
        property = new InputActionProperty(new InputAction("Action"));
    }

    private static float ReadFloat(InputActionProperty prop)
        => prop.action != null ? prop.action.ReadValue<float>() : 0f;

    private static bool ReadBool(InputActionProperty prop)
        => prop.action != null && prop.action.ReadValue<float>() > 0.5f;

    private static void EnableAction(InputActionProperty prop)
    {
        if (prop.action != null && !prop.action.enabled)
            prop.action.Enable();
    }

    private static void DisableAction(InputActionProperty prop)
    {
        if (prop.action != null && prop.action.enabled)
            prop.action.Disable();
    }
}