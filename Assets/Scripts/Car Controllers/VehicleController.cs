using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// High-level vehicle controller that connects player input to vehicle subsystems.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Wires input actions to drivetrain, transmission, engine audio, and lights.
///
/// This component acts as the main setup and coordination point for a playable vehicle. It owns the
/// inspector-facing tuning values and pushes them into the specialized runtime components:
/// - <see cref="DriveTrainController"/> for wheel physics and vehicle movement.
/// - <see cref="TransmissionController"/> for RPM and shifting.
/// - <see cref="EngineSound"/> for layered engine audio.
/// - <see cref="LightsController"/> for vehicle lights.
///
/// Requirements:
/// - Exactly four wheels should be configured in FL, FR, RL, RR order.
/// - Each wheel entry should contain both a <see cref="WheelCollider"/> and a visual wheel transform.
/// - A <see cref="Rigidbody"/> must be present on the same object.
///
/// Threading:
/// - Unity main thread only.
/// - Vehicle control is applied from <see cref="FixedUpdate"/>.
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
	#region Inspector: References

	[Header("References")]
	/// <summary>
	/// Vehicle rigidbody used by drivetrain physics.
	/// </summary>
	/// <remarks>
	/// Auto-assigned during <see cref="SetUp"/>.
	/// </remarks>
	[Tooltip("Vehicle Rigidbody. Auto-assigned during setup.")]
	[SerializeField, ReadOnly] private Rigidbody carRigidbody;

	/// <summary>
	/// Wheel specifications used by the drivetrain.
	/// </summary>
	/// <remarks>
	/// Expected order is front-left, front-right, rear-left, rear-right.
	/// </remarks>
	[Tooltip("Wheel Collider, Wheel Visual, Powered, Steering. Order: FL, FR, RL, RR.")]
	[SerializeField] private WheelSpec[] wheels = new WheelSpec[4];

	#endregion Inspector: References

	#region Inspector: Inputs & Actions

	[Header("Inputs")]
	/// <summary>
	/// Current input device classification.
	/// </summary>
	/// <remarks>
	/// Auto-detected from the last performed input action and used to choose steering behaviour.
	/// </remarks>
	[Tooltip("Input source: Keyboard/Mouse or Gamepad. Automatically detected.")]
	[SerializeField, ReadOnly] private InputSource currentInputDevice;

	/// <summary>
	/// Throttle input action.
	/// </summary>
	[Tooltip("Throttle axis. Usually -1..1 for keyboard composite or 0..1 for trigger input.")]
	[SerializeField] private InputActionProperty throttleAction;

	/// <summary>
	/// Steering input action.
	/// </summary>
	[Tooltip("Steering axis. Usually -1..1 for left/right input.")]
	[SerializeField] private InputActionProperty steerAction;

	/// <summary>
	/// Brake input action.
	/// </summary>
	[Tooltip("Brake button or axis. Values above 0.5 count as pressed.")]
	[SerializeField] private InputActionProperty brakeAction;

	/// <summary>
	/// Handbrake input action.
	/// </summary>
	[Tooltip("Handbrake button or axis. Values above 0.5 count as pressed.")]
	[SerializeField] private InputActionProperty handbrakeAction;

	/// <summary>
	/// Lights toggle input action.
	/// </summary>
	[Tooltip("Button used to toggle vehicle lights on and off.")]
	[SerializeField] private InputActionProperty lightsToggleAction;

	/// <summary>
	/// Whether default input actions should be created at runtime when no user action is assigned.
	/// </summary>
	[Tooltip("Create default runtime input bindings if no input actions are assigned.")]
	[SerializeField] private bool autoCreateDefaultBindingsIfMissing = false;

	#endregion Inspector: Inputs & Actions

	#region Inspector: Vehicle Tuning

	[Header("Vehicle")]
	/// <summary>
	/// Local center of mass assigned to the vehicle rigidbody.
	/// </summary>
	[Tooltip("Center of Mass position relative to the vehicle transform. Adjust for better handling.")]
	[SerializeField] private Vector3 centreOfMassPosition = new Vector3(0f, -0.4f, 0.5f);

	/// <summary>
	/// Simplified Ackermann steering multiplier.
	/// </summary>
	[Tooltip("Ackermann steering factor. 1 = neutral, lower can understeer, higher can exaggerate inside-wheel steering.")]
	[SerializeField] private float ackermannFactor = 1.1f;

	/// <summary>
	/// Maximum forward speed in km/h.
	/// </summary>
	[Tooltip("Maximum speed in km/h. Vehicle will not exceed this speed.")]
	[SerializeField] private float maxSpeed = 200f;

	/// <summary>
	/// Maximum reverse speed in km/h.
	/// </summary>
	[Tooltip("Maximum reverse speed in km/h. Vehicle will not exceed this speed in reverse.")]
	[SerializeField] private float maxReverseSpeed = 40f;

	/// <summary>
	/// Maximum steering angle at top speed.
	/// </summary>
	[Tooltip("Maximum steering angle in degrees at top speed.")]
	[SerializeField] private float maxSteerAngleAtTopSpeed = 5f;

	/// <summary>
	/// Maximum steering angle at zero speed.
	/// </summary>
	[Tooltip("Maximum steering angle in degrees at zero speed.")]
	[SerializeField] private float maxSteerAngle = 30f;

	/// <summary>
	/// Steering slew rate in degrees per second.
	/// </summary>
	[Tooltip("Steering speed in degrees per second. Limits how fast the steering angle can change.")]
	[SerializeField] private float steerSpeedDegPerSec = 180f;

	/// <summary>
	/// Exponent used to shape analog steering input.
	/// </summary>
	[Tooltip("Exponent applied to controller input values. 1 = linear.")]
	[SerializeField, Range(1f, 3f)] private float inputExponent = 1.8f;

	/// <summary>
	/// Maximum motor torque budget distributed across driven wheels.
	/// </summary>
	[Tooltip("Maximum motor torque budget distributed across driven wheels.")]
	[SerializeField] private float maxMotorPower = 1200f;

	/// <summary>
	/// Maximum service brake torque per wheel.
	/// </summary>
	[Tooltip("Maximum brake torque per wheel.")]
	[SerializeField] private float maxBrakeTorque = 3000f;

	/// <summary>
	/// Handbrake torque applied to rear wheels.
	/// </summary>
	[Tooltip("Handbrake torque for rear wheels.")]
	[SerializeField] private float handbrakeTorque = 6000f;

	#endregion Inspector: Vehicle Tuning

	#region Inspector: Engine / Drivetrain

	[Header("Engine / Drivetrain")]
	/// <summary>
	/// Normalized RPM value at which engine torque starts fading near redline.
	/// </summary>
	[Tooltip("Normalized RPM value at which engine torque starts fading near redline.")]
	[SerializeField, Range(0f, 1f)] private float redlineFadeStart = 0.92f;

	#endregion Inspector: Engine / Drivetrain

	#region Inspector: Transmission & RPM

	[Header("Transmission & RPM")]
	/// <summary>
	/// Forward gear ratios.
	/// </summary>
	/// <remarks>
	/// Index 0 is first gear.
	/// </remarks>
	[Tooltip("Forward gear ratios. Index 0 is first gear.")]
	[SerializeField] private float[] forwardGears = new float[] { 3.2f, 2.1f, 1.5f, 1.0f, 0.82f };

	/// <summary>
	/// Final drive ratio.
	/// </summary>
	[Tooltip("Final drive ratio.")]
	[SerializeField] private float finalDrive = 3.42f;

	/// <summary>
	/// Engine idle RPM.
	/// </summary>
	[Tooltip("Engine idle RPM.")]
	[SerializeField] private float idleRPM = 900f;

	/// <summary>
	/// Engine redline RPM.
	/// </summary>
	[Tooltip("Engine redline RPM.")]
	[SerializeField] private float redlineRPM = 6000f;

	/// <summary>
	/// RPM threshold used for automatic upshifts.
	/// </summary>
	[Tooltip("Automatic shift-up threshold in RPM.")]
	[SerializeField] private float shiftUpRPM = 4000f;

	/// <summary>
	/// RPM threshold used for automatic downshifts.
	/// </summary>
	[Tooltip("Automatic shift-down threshold in RPM.")]
	[SerializeField] private float shiftDownRPM = 2000f;

	/// <summary>
	/// Duration in seconds during which torque is cut while shifting.
	/// </summary>
	[Tooltip("Duration in seconds during which torque is cut while shifting.")]
	[SerializeField] private float shiftDuration = 0.2f;

	/// <summary>
	/// Maximum wheel slip allowed for automatic shifting.
	/// </summary>
	[Tooltip("Wheel-slip threshold above which automatic shifting is suppressed.")]
	[SerializeField] private float shiftSlipThreshold = 0.5f;

	#endregion Inspector: Transmission & RPM

	#region Inspector: Stability & Limits

	[Header("Stability")]
	/// <summary>
	/// Whether anti-roll forces should be applied.
	/// </summary>
	[Tooltip("Enable anti-roll forces.")]
	[SerializeField] private bool antiRollToggle = false;

	/// <summary>
	/// Anti-roll stiffness for the front axle.
	/// </summary>
	[Tooltip("Anti-roll stiffness for the front axle.")]
	[SerializeField] private float antiRollStiffnessFront = 40f;

	/// <summary>
	/// Anti-roll stiffness for the rear axle.
	/// </summary>
	[Tooltip("Anti-roll stiffness for the rear axle.")]
	[SerializeField] private float antiRollStiffnessRear = 50f;

	[Header("Traction Control & ABS Limits")]
	/// <summary>
	/// Whether traction control should reduce torque on high slip.
	/// </summary>
	[Tooltip("Enable traction control.")]
	[SerializeField] private bool tractionControlEnabled = true;

	/// <summary>
	/// Combined slip limit used by traction control.
	/// </summary>
	[Tooltip("Combined slip above which traction control reduces torque.")]
	[SerializeField] private float tractionSlipLimit = 0.45f;

	/// <summary>
	/// Whether ABS should reduce brake torque on high forward slip.
	/// </summary>
	[Tooltip("Enable anti-lock braking.")]
	[SerializeField] private bool absEnabled = true;

	/// <summary>
	/// Forward slip limit used by ABS.
	/// </summary>
	[Tooltip("Forward slip above which ABS reduces brake torque.")]
	[SerializeField] private float absSlipLimit = 0.55f;

	[Header("Dynamic Grip")]
	/// <summary>
	/// Rear sideways grip multiplier while handbrake is active.
	/// </summary>
	[Tooltip("Rear sideways grip multiplier while handbrake is held. Lower values make drifting easier.")]
	[SerializeField] private float rearSidewaysGripHandbrakeMultiplier = 0.35f;

	/// <summary>
	/// Rear forward grip multiplier while handbrake is active.
	/// </summary>
	[Tooltip("Rear forward grip multiplier while handbrake is held.")]
	[SerializeField] private float rearForwardGripHandbrakeMultiplier = 0.85f;

	/// <summary>
	/// Forward grip multiplier when a wheel is near lock-up.
	/// </summary>
	[Tooltip("Forward grip multiplier when a wheel is near lock-up.")]
	[SerializeField] private float forwardGripLockedMultiplier = 1f;

	/// <summary>
	/// Sideways grip multiplier when a wheel is near lock-up.
	/// </summary>
	[Tooltip("Sideways grip multiplier when a wheel is near lock-up.")]
	[SerializeField] private float sidewaysGripLockedMultiplier = 0.60f;

	/// <summary>
	/// Forward slip threshold used to detect near-lock braking.
	/// </summary>
	[Tooltip("Forward slip above which a braking wheel counts as near lock-up.")]
	[SerializeField] private float lockForwardSlipThreshold = 0.35f;

	/// <summary>
	/// Brake torque fraction required before near-lock behaviour is considered.
	/// </summary>
	[Tooltip("Brake torque must exceed this fraction of max brake torque to count as hard braking.")]
	[SerializeField, Range(0f, 1f)] private float lockBrakeTorqueThreshold = 0.90f;

	#region Inspector: Grip Circle

	[Header("Grip Circle")]
	/// <summary>
	/// Whether combined forward/sideways tire usage should reduce available grip.
	/// </summary>
	[Tooltip("When enabled, combined forward/sideways tire usage reduces available grip.")]
	public bool gripCircleEnabled = true;

	/// <summary>
	/// Combined slip value where grip-circle reduction starts.
	/// </summary>
	[Tooltip("Combined slip value where grip-circle reduction starts.")]
	public float gripCircleStartSlip = 1f;

	/// <summary>
	/// Combined slip value where grip-circle reduction reaches maximum strength.
	/// </summary>
	[Tooltip("Combined slip value where grip-circle reduction reaches its maximum.")]
	public float gripCircleFullSlip = 2f;

	/// <summary>
	/// Minimum forward grip multiplier when the tire is overloaded.
	/// </summary>
	[Tooltip("Minimum forward grip multiplier when the tire is overloaded.")]
	[Range(0f, 1f)] public float minForwardGripCircleMultiplier = 0.9f;

	/// <summary>
	/// Minimum sideways grip multiplier when the tire is overloaded.
	/// </summary>
	[Tooltip("Minimum sideways grip multiplier when the tire is overloaded.")]
	[Range(0f, 1f)] public float minSidewaysGripCircleMultiplier = 0.9f;

	#endregion Inspector: Grip Circle

	#endregion Inspector: Stability & Limits

	#region Inspector: Limited Slip Differential

	[Header("Limited Slip Differential")]
	/// <summary>
	/// Whether the limited-slip differential approximation is enabled.
	/// </summary>
	[Tooltip("When enabled, driven wheels are torque-balanced to prevent one wheel from spinning much faster than the others.")]
	[SerializeField] private bool limitedSlipEnabled = true;

	/// <summary>
	/// Driven-wheel RPM difference above which limited-slip correction starts.
	/// </summary>
	[Tooltip("Driven-wheel RPM difference above which limited-slip correction starts.")]
	[SerializeField] private float limitedSlipStartRpmDifference = 120f;

	/// <summary>
	/// Driven-wheel RPM difference at which limited-slip correction reaches full strength.
	/// </summary>
	[Tooltip("Driven-wheel RPM difference at which limited-slip correction reaches full strength.")]
	[SerializeField] private float limitedSlipFullRpmDifference = 700f;

	/// <summary>
	/// Maximum fraction of motor torque removed from an over-spinning driven wheel.
	/// </summary>
	[Tooltip("Maximum fraction of motor torque removed from an over-spinning driven wheel.")]
	[SerializeField, Range(0f, 1f)] private float limitedSlipMaxTorqueCut = 0.65f;

	/// <summary>
	/// Torque multiplier applied to slower driven wheels when another driven wheel over-spins.
	/// </summary>
	[Tooltip("Torque multiplier applied to slower driven wheels when another driven wheel is over-spinning.")]
	[SerializeField, Range(1f, 2f)] private float limitedSlipGripWheelBoost = 1.25f;

	/// <summary>
	/// Optional brake torque applied to an over-spinning driven wheel.
	/// </summary>
	[Tooltip("Optional brake torque applied to an over-spinning driven wheel.")]
	[SerializeField] private float limitedSlipBrakeTorque = 250f;

	#endregion Inspector: Limited Slip Differential

	#region Inspector: Friction

	[Header("Forward Friction")]
	/// <summary>
	/// Base forward friction used by front wheels.
	/// </summary>
	[Tooltip("Base forward friction used by front wheels.")]
	[SerializeField] private WheelFrictionSettings frontForwardFriction;

	/// <summary>
	/// Base forward friction used by rear wheels.
	/// </summary>
	[Tooltip("Base forward friction used by rear wheels.")]
	[SerializeField] private WheelFrictionSettings rearForwardFriction;

	[Header("Sideways Friction")]
	/// <summary>
	/// Base sideways friction used by front wheels.
	/// </summary>
	[Tooltip("Base sideways friction used by front wheels.")]
	[SerializeField] private WheelFrictionSettings frontSidewaysFriction;

	/// <summary>
	/// Base sideways friction used by rear wheels.
	/// </summary>
	[Tooltip("Base sideways friction used by rear wheels.")]
	[SerializeField] private WheelFrictionSettings rearSidewaysFriction;

	#endregion Inspector: Friction

	#region Inspector: Lights

	[Header("Lights")]
	/// <summary>
	/// Front light intensity when enabled.
	/// </summary>
	[Tooltip("Intensity of the front lights when turned on.")]
	[SerializeField] private float frontLightsIntensity = 1000;

	/// <summary>
	/// Front light color when enabled.
	/// </summary>
	[Tooltip("Color of the front lights when turned on.")]
	[SerializeField] private Color frontLightsColor;

	/// <summary>
	/// Front light components controlled by the lights controller.
	/// </summary>
	[Tooltip("List of front lights.")]
	[SerializeField] private List<Light> frontLights;

	/// <summary>
	/// Daylight intensity when enabled.
	/// </summary>
	[Tooltip("Intensity of the day lights when turned on.")]
	[SerializeField] private float dayLightsIntensity = 1;

	/// <summary>
	/// Daylight color when enabled.
	/// </summary>
	[Tooltip("Color of the day lights when turned on.")]
	[SerializeField] private Color dayLightsColor;

	/// <summary>
	/// Daylight components controlled by the lights controller.
	/// </summary>
	[Tooltip("List of day lights.")]
	[SerializeField] private List<Light> dayLights;

	/// <summary>
	/// Rear light intensity when enabled.
	/// </summary>
	[Tooltip("Intensity of the rear lights when turned on.")]
	[SerializeField] private float rearLightsIntensity = 1;

	/// <summary>
	/// Rear light color when enabled.
	/// </summary>
	[Tooltip("Color of the rear lights when turned on.")]
	[SerializeField] private Color rearLightsColor;

	/// <summary>
	/// Rear light components controlled by the lights controller.
	/// </summary>
	[Tooltip("List of rear lights.")]
	[SerializeField] private List<Light> rearLights;

	/// <summary>
	/// Reverse light intensity when reversing.
	/// </summary>
	[Tooltip("Intensity of the reverse lights when turned on.")]
	[SerializeField] private float reverseLightsIntensity = 5;

	/// <summary>
	/// Reverse light color when reversing.
	/// </summary>
	[Tooltip("Color of the reverse lights when turned on.")]
	[SerializeField] private Color reverseLightsColor;

	/// <summary>
	/// Reverse light components controlled by the lights controller.
	/// </summary>
	[Tooltip("List of reverse lights.")]
	[SerializeField] private List<Light> reverseLights;

	/// <summary>
	/// Brake light intensity when braking.
	/// </summary>
	[Tooltip("Intensity of the brake lights when turned on.")]
	[SerializeField] private float brakeLightsIntensity = 5;

	/// <summary>
	/// Brake light color when braking.
	/// </summary>
	[Tooltip("Color of the brake lights when turned on.")]
	[SerializeField] private Color brakeLightsColor;

	/// <summary>
	/// Brake light components controlled by the lights controller.
	/// </summary>
	[Tooltip("List of brake lights.")]
	[SerializeField] private List<Light> brakeLights;

	[Header("Lights : Fade Settings")]
	/// <summary>
	/// Fade duration in seconds for normal light groups.
	/// </summary>
	[Tooltip("Duration in seconds for fading lights on and off.")]
	[SerializeField] private float fadeDuration = 0.1f;

	[Header("Lights : Initial State")]
	/// <summary>
	/// Whether front, day, and rear lights start enabled.
	/// </summary>
	[Tooltip("If enabled, front, day, and rear lights start enabled.")]
	[SerializeField] private bool startLightsOn = false;

	#endregion Inspector: Lights

	#region Inspector: Engine Sound

	[Header("Engine Sound")]
	/// <summary>
	/// Audio mixer group used by engine sound sources.
	/// </summary>
	[Tooltip("Audio mixer group used by the engine sound sources.")]
	[SerializeField] private AudioMixerGroup outputGroup;

	/// <summary>
	/// Master volume for engine sound.
	/// </summary>
	[Tooltip("Master volume for engine sound.")]
	[SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

	/// <summary>
	/// Spatial blend of engine audio.
	/// </summary>
	[Tooltip("Spatial blend of engine audio. 0 = 2D, 1 = 3D.")]
	[SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;

	/// <summary>
	/// Doppler effect intensity for engine sources.
	/// </summary>
	[Tooltip("Doppler effect intensity for engine sources.")]
	[SerializeField, Range(0f, 5f)] private float dopplerLevel = 0f;

	[Header("Engine Sound : Clips (On-Throttle)")]
	/// <summary>
	/// On-throttle idle RPM band clip.
	/// </summary>
	[Tooltip("On-throttle idle RPM band clip.")]
	[SerializeField] private AudioClip on_Idle;

	/// <summary>
	/// On-throttle low RPM band clip.
	/// </summary>
	[Tooltip("On-throttle low RPM band clip.")]
	[SerializeField] private AudioClip on_Low;

	/// <summary>
	/// On-throttle mid RPM band clip.
	/// </summary>
	[Tooltip("On-throttle mid RPM band clip.")]
	[SerializeField] private AudioClip on_Mid;

	/// <summary>
	/// On-throttle high RPM band clip.
	/// </summary>
	[Tooltip("On-throttle high RPM band clip.")]
	[SerializeField] private AudioClip on_High;

	[Header("Engine Sound : Clips (Off-Throttle)")]
	/// <summary>
	/// Off-throttle idle RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle idle RPM band clip.")]
	[SerializeField] private AudioClip off_Idle;

	/// <summary>
	/// Off-throttle low RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle low RPM band clip.")]
	[SerializeField] private AudioClip off_Low;

	/// <summary>
	/// Off-throttle mid RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle mid RPM band clip.")]
	[SerializeField] private AudioClip off_Mid;

	/// <summary>
	/// Off-throttle high RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle high RPM band clip.")]
	[SerializeField] private AudioClip off_High;

	[Header("Engine Sound : Smoothing")]
	/// <summary>
	/// RPM smoothing speed constant.
	/// </summary>
	[Tooltip("RPM smoothing speed. Higher values make RPM audio respond faster.")]
	[SerializeField] private float rpmLerpSpeed = 6f;

	/// <summary>
	/// Throttle smoothing speed constant.
	/// </summary>
	[Tooltip("Throttle smoothing speed. Higher values make throttle audio respond faster.")]
	[SerializeField] private float throttleLerpSpeed = 8f;

	[Header("Engine Sound : Pitch Mapping")]
	/// <summary>
	/// Curve mapping normalized RPM to pitch multiplier.
	/// </summary>
	[Tooltip("AnimationCurve maps normalized RPM from 0 to 1 to pitch multiplier.")]
	[SerializeField] private AnimationCurve pitchVsRpm = AnimationCurve.EaseInOut(0f, 0.7f, 1f, 2.0f);

	[Header("Engine Sound : Band Crossfade")]
	/// <summary>
	/// Center points of the idle, low, mid, and high RPM bands.
	/// </summary>
	[Tooltip("Center points of the four RPM bands over normalized RPM.")]
	[SerializeField] private Vector4 bandCenters = new Vector4(0f, 0.33f, 0.66f, 1.0f);

	/// <summary>
	/// Crossfade sharpness between RPM bands.
	/// </summary>
	[Tooltip("Crossfade sharpness between bands. Higher values create narrower bands.")]
	[SerializeField] private float bandSharpness = 6f;

	[Header("Engine Sound : On/Off Balance")]
	/// <summary>
	/// Throttle shaping exponent for on/off-throttle blending.
	/// </summary>
	[Tooltip("Exponent shaping for throttle to on-throttle weight. 1 = linear.")]
	[SerializeField] private float throttleShape = 1.25f;

	/// <summary>
	/// Extra gain applied to the on-throttle layer.
	/// </summary>
	[Tooltip("Extra volume for on-throttle audio compared to off-throttle audio.")]
	[SerializeField] private float onThrottleBoost = 1f;

	[Header("Engine Sound : Shift & Limiter")]
	/// <summary>
	/// Pitch flare amount applied during gear shifts.
	/// </summary>
	[Tooltip("Pitch flare amount applied during gear shifts.")]
	[SerializeField] private float shiftFlareAmount = 0.06f;

	/// <summary>
	/// Limiter activation threshold in normalized RPM.
	/// </summary>
	[Tooltip("Limiter activation threshold in normalized RPM.")]
	[SerializeField] private float limiterStart = 0.96f;

	/// <summary>
	/// Limiter depth near redline.
	/// </summary>
	[Tooltip("Limiter depth, controlling how much volume is reduced near redline.")]
	[SerializeField] private float limiterDepth = 0.25f;

	#endregion Inspector: Engine Sound

	#region Components (cached)

	/// <summary>
	/// Runtime reference to the engine sound component.
	/// </summary>
	[SerializeField, ReadOnly] private EngineSound _engineSound;

	/// <summary>
	/// Runtime reference to the drivetrain controller.
	/// </summary>
	[SerializeField, ReadOnly] private DriveTrainController _driveTrainController;

	/// <summary>
	/// Runtime reference to the transmission controller.
	/// </summary>
	[SerializeField, ReadOnly] private TransmissionController _transmissionController;

	/// <summary>
	/// Runtime reference to the lights controller.
	/// </summary>
	[SerializeField, ReadOnly] private LightsController _lightsController;

	#endregion Components (cached)

	#region Runtime State

	/// <summary>
	/// Input actions created and owned by this controller when automatic default bindings are enabled.
	/// </summary>
	private readonly List<InputAction> _ownedActions = new();

	/// <summary>
	/// Input device classification used for steering model decisions.
	/// </summary>
	private enum InputSource { KeyboardMouse, Gamepad }

	#endregion Runtime State

	#region Unity Methods

	/// <summary>
	/// Enables input actions and subscribes input callbacks.
	/// </summary>
	private void OnEnable()
	{
		EnableAction(throttleAction);
		EnableAction(steerAction);
		EnableAction(brakeAction);
		EnableAction(handbrakeAction);
		EnableAction(lightsToggleAction);

		if (throttleAction.action != null) throttleAction.action.performed += DetectDevice;
		if (steerAction.action != null) steerAction.action.performed += DetectDevice;
		if (brakeAction.action != null) brakeAction.action.performed += DetectDevice;
		if (handbrakeAction.action != null) handbrakeAction.action.performed += DetectDevice;

		if (lightsToggleAction.action != null) lightsToggleAction.action.performed += OnLightsPerformed;
	}

	/// <summary>
	/// Unsubscribes input callbacks and disables input actions.
	/// </summary>
	private void OnDisable()
	{
		if (throttleAction.action != null) throttleAction.action.performed -= DetectDevice;
		if (steerAction.action != null) steerAction.action.performed -= DetectDevice;
		if (brakeAction.action != null) brakeAction.action.performed -= DetectDevice;
		if (handbrakeAction.action != null) handbrakeAction.action.performed -= DetectDevice;

		if (lightsToggleAction.action != null) lightsToggleAction.action.performed -= OnLightsPerformed;

		DisableAction(throttleAction);
		DisableAction(steerAction);
		DisableAction(brakeAction);
		DisableAction(handbrakeAction);
		DisableAction(lightsToggleAction);
	}

	/// <summary>
	/// Validates wheel configuration and applies setup in the editor.
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
	/// Initializes component references and pushes inspector values into subsystems.
	/// </summary>
	private void Start() => SetUp();

	/// <summary>
	/// Ensures dependent components exist when the component is reset in the editor.
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
	/// Cleans up input actions created internally by this controller.
	/// </summary>
	private void OnDestroy() => DeleteActions();

	/// <summary>
	/// Reads player input, updates drivetrain controls, updates vehicle lights, and feeds engine audio.
	/// </summary>
	private void FixedUpdate()
	{
		float throttle = ReadFloat(throttleAction);
		float steer = ReadFloat(steerAction);
		float braking = ReadFloat(brakeAction);
		bool handbrake = ReadBool(handbrakeAction);

		_driveTrainController.ApplyWheelControls(throttle, braking, handbrake, steer, currentInputDevice == InputSource.Gamepad);
		_lightsController.SetBrakeLights(_driveTrainController.Braking);
		_lightsController.SetReverseLights(_driveTrainController.Reversing);

		UpdateAudio(throttle);
	}

	#endregion Unity Methods

	#region Setup Helpers

	/// <summary>
	/// Populates component references, pushes inspector tuning into subsystems, and creates default input actions if requested.
	/// </summary>
	private void SetUp()
	{
		tractionControlEnabled = GameDataManager.Instance != null ? GameDataManager.Instance.GetTC() : tractionControlEnabled;
		absEnabled = GameDataManager.Instance != null ? GameDataManager.Instance.GetABS() : absEnabled;

		if (!carRigidbody)
			carRigidbody = GetComponent<Rigidbody>();

		if (!carRigidbody)
			Debug.LogError("VehicleController requires a Rigidbody component on the same GameObject.");

		if (_engineSound == null)
			_engineSound = GetOrAddComponent<EngineSound>();

		if (_transmissionController == null)
			_transmissionController = GetOrAddComponent<TransmissionController>();

		if (_driveTrainController == null)
			_driveTrainController = GetOrAddComponent<DriveTrainController>();

		if (_lightsController == null)
			_lightsController = GetOrAddComponent<LightsController>();

		SetUpLightsController();
		SetUpEngineSoundController();
		SetUpTransmissionController();
		SetUpDriveTrainController(_transmissionController);

		CreateDefaultInputActions();
	}

	/// <summary>
	/// Gets an existing component of type <typeparamref name="T"/> or adds one to this GameObject.
	/// </summary>
	/// <typeparam name="T">Component type to get or add.</typeparam>
	/// <returns>Existing or newly added component.</returns>
	private T GetOrAddComponent<T>() where T : Component
	{
		T component = GetComponent<T>();

		if (component == null)
		{
			component = gameObject.AddComponent<T>();
		}

		return component;
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
	/// Configures the transmission with gear ratios, RPM limits, shift behaviour, and shift callbacks.
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
	/// Transfers wheel specs and vehicle tuning into <see cref="DriveTrainController"/>.
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
		_driveTrainController.antiRollToggle = antiRollToggle;
		_driveTrainController.antiRollStiffnessFront = antiRollStiffnessFront;
		_driveTrainController.antiRollStiffnessRear = antiRollStiffnessRear;
		_driveTrainController.tractionControlEnabled = tractionControlEnabled;
		_driveTrainController.tractionSlipLimit = tractionSlipLimit;
		_driveTrainController.absEnabled = absEnabled;
		_driveTrainController.absSlipLimit = absSlipLimit;
		_driveTrainController.rearSidewaysGripHandbrakeMultiplier = rearSidewaysGripHandbrakeMultiplier;
		_driveTrainController.rearForwardGripHandbrakeMultiplier = rearForwardGripHandbrakeMultiplier;

		_driveTrainController.forwardGripLockedMultiplier = forwardGripLockedMultiplier;
		_driveTrainController.sidewaysGripLockedMultiplier = sidewaysGripLockedMultiplier;
		_driveTrainController.lockForwardSlipThreshold = lockForwardSlipThreshold;
		_driveTrainController.lockBrakeTorqueThreshold = lockBrakeTorqueThreshold;

		_driveTrainController.gripCircleEnabled = gripCircleEnabled;
		_driveTrainController.gripCircleStartSlip = gripCircleStartSlip;
		_driveTrainController.gripCircleFullSlip = gripCircleFullSlip;
		_driveTrainController.minForwardGripCircleMultiplier = minForwardGripCircleMultiplier;
		_driveTrainController.minSidewaysGripCircleMultiplier = minSidewaysGripCircleMultiplier;

		_driveTrainController.frontForwardFriction = frontForwardFriction;
		_driveTrainController.rearForwardFriction = rearForwardFriction;
		_driveTrainController.frontSidewaysFriction = frontSidewaysFriction;
		_driveTrainController.rearSidewaysFriction = rearSidewaysFriction;

		_driveTrainController.limitedSlipEnabled = limitedSlipEnabled;
		_driveTrainController.limitedSlipStartRpmDifference = limitedSlipStartRpmDifference;
		_driveTrainController.limitedSlipFullRpmDifference = limitedSlipFullRpmDifference;
		_driveTrainController.limitedSlipMaxTorqueCut = limitedSlipMaxTorqueCut;
		_driveTrainController.limitedSlipGripWheelBoost = limitedSlipGripWheelBoost;
		_driveTrainController.limitedSlipBrakeTorque = limitedSlipBrakeTorque;

		_driveTrainController.redlineFadeStart = redlineFadeStart;

		_driveTrainController.coMPosition = centreOfMassPosition;
		_driveTrainController.ackermannFactor = ackermannFactor;

		_driveTrainController.SetUp();
	}

	#endregion Setup Helpers

	#region Audio

	/// <summary>
	/// Pushes current transmission RPM and throttle input into the engine sound system.
	/// </summary>
	/// <param name="throttle">Current throttle input.</param>
	private void UpdateAudio(float throttle)
	{
		_engineSound.RPM = _transmissionController.EngineRPM;
		_engineSound.throttle = throttle;
	}

	#endregion Audio

	#region Input Helpers

	/// <summary>
	/// Detects the active input device from an input callback.
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

	#region Input Action Creation

	/// <summary>
	/// Creates default input actions when automatic binding is enabled and no user actions are assigned.
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
	/// <returns>Newly created input action.</returns>
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
			.With("negative", "<Gamepad>/leftTrigger")
			.With("positive", "<Gamepad>/rightTrigger");
		return throttle;
	}

	/// <summary>
	/// Creates a default steering action with keyboard and gamepad bindings.
	/// </summary>
	/// <returns>Newly created input action.</returns>
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
	/// <returns>Newly created input action.</returns>
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
	/// Creates a default handbrake action.
	/// </summary>
	/// <returns>Newly created input action.</returns>
	private InputAction CreateDefaultHandbrakeBind()
	{
		var handbrake = new InputAction("Handbrake", InputActionType.Button, expectedControlType: "Button");
		handbrake.AddBinding("<Keyboard>/space");
		handbrake.AddBinding("<DualShockGamepad>/crossButton");
		handbrake.AddBinding("<Gamepad>/buttonSouth");
		return handbrake;
	}

	/// <summary>
	/// Creates a default lights-toggle action.
	/// </summary>
	/// <returns>Newly created input action.</returns>
	private InputAction CreateDefaultLightsBind()
	{
		var lights = new InputAction("LightsToggle", InputActionType.Button, expectedControlType: "Button");
		lights.AddBinding("<Keyboard>/l").WithInteraction("Press");
		lights.AddBinding("<DualShockGamepad>/dpad/up").WithInteractions("Press");
		lights.AddBinding("<Gamepad>/dpad/up").WithInteraction("Press");
		return lights;
	}

	#endregion Default Action Creators

	#region Input Action Deletion & Cleanup

	/// <summary>
	/// Checks whether an input action property already has a user-assigned action or reference.
	/// </summary>
	/// <param name="p">Property to check.</param>
	/// <returns><c>true</c> if there is a user assignment; otherwise <c>false</c>.</returns>
	private static bool HasUserAssignment(InputActionProperty p)
	{
		if (p.reference != null) return true;
		var a = p.action;
		return a != null && a.bindings.Count > 0;
	}

	/// <summary>
	/// Disposes all input actions created internally by this controller and clears properties.
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
	/// Disposes a specific property action if it is owned by this controller.
	/// </summary>
	/// <param name="property">Property to clear.</param>
	/// <remarks>
	/// The property is replaced with an empty placeholder action after cleanup.
	/// </remarks>
	private void DeleteAction(ref InputActionProperty property)
	{
		if (property.action != null && _ownedActions.Contains(property.action))
		{
			property.action.Dispose();
			_ownedActions.Remove(property.action);
		}
		property = new InputActionProperty(new InputAction("Action"));
	}

	#endregion Input Action Deletion & Cleanup

	#region Input Action Readers

	/// <summary>
	/// Handles the lights toggle input action.
	/// </summary>
	/// <param name="ctx">Input callback context.</param>
	private void OnLightsPerformed(InputAction.CallbackContext ctx) => _lightsController.ToggleLights();

	/// <summary>
	/// Reads a float value from an input action property.
	/// </summary>
	/// <param name="prop">Action property to read.</param>
	/// <returns>Float value, or 0 if no action is assigned.</returns>
	private static float ReadFloat(InputActionProperty prop)
		=> prop.action != null ? prop.action.ReadValue<float>() : 0f;

	/// <summary>
	/// Reads a button-like boolean value from an input action property.
	/// </summary>
	/// <param name="prop">Action property to read.</param>
	/// <returns><c>true</c> if the action value is greater than 0.5; otherwise <c>false</c>.</returns>
	private static bool ReadBool(InputActionProperty prop)
		=> prop.action != null && prop.action.ReadValue<float>() > 0.5f;

	#endregion Input Action Readers

	#region Action Enable/Disable

	/// <summary>
	/// Enables an input action property if it contains a disabled action.
	/// </summary>
	/// <param name="prop">Action property to enable.</param>
	private static void EnableAction(InputActionProperty prop)
	{
		if (prop.action != null && !prop.action.enabled)
			prop.action.Enable();
	}

	/// <summary>
	/// Disables an input action property if it contains an enabled action.
	/// </summary>
	/// <param name="prop">Action property to disable.</param>
	private static void DisableAction(InputActionProperty prop)
	{
		if (prop.action != null && prop.action.enabled)
			prop.action.Disable();
	}

	#endregion Action Enable/Disable

	#endregion Input Action Creation

	#endregion Input Helpers
}