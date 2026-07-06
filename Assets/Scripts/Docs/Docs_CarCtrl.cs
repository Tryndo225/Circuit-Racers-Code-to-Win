/**
 * @file Docs_CarCtrl.cs
 * @brief Documentation entry for the Vehicle Control subsystem.
 *
 * @defgroup car_ctrl Vehicle Control
 * @ingroup systems
 * @brief Modular four-wheel car stack: input, drivetrain, transmission, audio, lights, VFX, collision SFX, and camera follow.
 *
 * @details
 * The Vehicle Control subsystem is composed of small, focused MonoBehaviours:
 * - ::VehicleController orchestrates input, reads saved assists, delegates to drivetrain/transmission, and wires audio/lights.
 * - ::DriveTrainController applies steering, motor torque, braking, reverse logic, ABS, traction control,
 *   limited-slip differential approximation, anti-roll, dynamic grip, grip-circle coupling, and wheel-mesh sync.
 * - ::TransmissionController converts grounded driven-wheel RPM into engine RPM and manages automatic shifting with slip gating.
 * - ::EngineSound blends on-throttle and off-throttle engine layers across four RPM bands.
 * - ::LightsController drives front, day, rear, reverse, and brake light groups with fading and shared-bulb restoration.
 * - ::TyreEffects emits smoke, skid trails, and looping skid audio based on WheelCollider slip.
 * - ::CollisionDetection plays crash SFX from collision impact speed, with cooldown and layer filtering.
 * - ::FollowCamera provides a stable third-person camera using movement-direction-based positioning.
 * - ::WheelSpec and ::WheelFrictionSettings provide inspector-friendly wheel and friction configuration.
 *
 * Key design goals:
 * - Inspector-first tuning.
 * - Explicit subsystem responsibilities.
 * - Centralized vehicle setup through VehicleController.
 * - Physics work kept in the FixedUpdate path.
 * - Low allocation during normal driving.
 *
 * Contents:
 * - @ref car_overview
 * - @ref car_scene_setup
 * - @ref car_inspector
 * - @ref car_lifecycle
 * - @ref car_usage
 * - @ref car_api
 * - @ref car_integration
 * - @ref car_performance
 * - @ref car_troubleshooting
 * - @ref car_versions
 *
 * ----------------------------------------------------------------------
 * @section car_overview Overview
 *
 * Runtime flow:
 * 1) ::VehicleController reads Unity Input System actions for throttle, steering, brake, handbrake, and lights.
 * 2) ::VehicleController calls ::DriveTrainController::ApplyWheelControls(...) during FixedUpdate.
 * 3) ::DriveTrainController handles steering, reverse logic, throttle/brake conflict handling, drive torque,
 *    braking torque, limited-slip correction, anti-roll forces, dynamic grip, grip-circle coupling, and wheel visuals.
 * 4) ::DriveTrainController queries ::TransmissionController for automatic shifting and torque-cut state.
 * 5) ::VehicleController updates ::EngineSound with engine RPM and throttle/load.
 * 6) ::VehicleController updates ::LightsController brake and reverse states.
 * 7) ::TyreEffects reads per-wheel slip and displays smoke, trails, and skid audio.
 * 8) ::CollisionDetection plays crash SFX when collision impact severity is high enough.
 * 9) ::FollowCamera follows the spawned car using planar movement direction and smoothing.
 *
 * Physics principles:
 * - Unity WheelCollider is used for wheel contact, suspension, and basic tire forces.
 * - Forward and sideways WheelFrictionCurve values are configured per axle.
 * - Traction control reduces driven-wheel motor torque when combined slip is too high.
 * - ABS reduces brake torque when braking slip is too high.
 * - Locked-wheel grip modifiers reduce grip when a wheel is near lock-up under hard braking.
 * - Handbrake reduces rear grip and applies rear brake torque.
 * - Limited-slip differential approximation cuts torque from over-spinning driven wheels and may add brake torque.
 * - Anti-roll forces compare left/right suspension travel and resist excessive body roll.
 * - Grip-circle coupling reduces sideways grip from forward slip and forward grip from sideways slip.
 *
 * Audio principles:
 * - Engine audio is handled by ::EngineSound using idle, low, mid, and high RPM bands.
 * - Each band has on-throttle and off-throttle layers.
 * - Gear shifts call ::EngineSound::OnShift to add a short pitch flare.
 * - Crash SFX are routed through ::SoundManager.
 * - Skid audio in ::TyreEffects uses a local AudioSource on the wheel effect object.
 *
 * ----------------------------------------------------------------------
 * @section car_scene_setup Scene Setup
 *
 * Required vehicle object:
 * - Rigidbody on the vehicle root.
 * - ::VehicleController on the vehicle root.
 * - ::DriveTrainController on the vehicle root.
 * - ::TransmissionController on the vehicle root.
 * - ::EngineSound on the vehicle root.
 * - ::LightsController on the vehicle root.
 * - Exactly four ::WheelSpec entries in the VehicleController inspector:
 *   - front-left,
 *   - front-right,
 *   - rear-left,
 *   - rear-right.
 *
 * Each WheelSpec should contain:
 * - WheelCollider reference.
 * - Visual wheel Transform reference.
 * - powered flag.
 * - steering flag.
 *
 * Optional vehicle components:
 * - ::TyreEffects on wheel objects that contain WheelCollider components.
 * - ::CollisionDetection on the vehicle body object.
 * - ::FollowCamera in the scene, with target assigned directly or through TrackManager spawning.
 *
 * Audio setup:
 * - Assign engine clips in ::EngineSound.
 * - Assign crash clips in ::CollisionDetection.
 * - Assign skid clip and mixer group in ::TyreEffects if skid audio is desired.
 * - Ensure a ::SoundManager exists when crash SFX should be played.
 * - Ensure one active AudioListener exists, usually on the main camera.
 *
 * ----------------------------------------------------------------------
 * @section car_inspector Inspector
 *
 * ::VehicleController
 * - wheels[4]: WheelCollider, visual Transform, powered flag, and steering flag in FL, FR, RL, RR order.
 * - Input actions: throttle, steer, brake, handbrake, lights toggle.
 * - autoCreateDefaultBindingsIfMissing: creates simple runtime bindings when no input actions are assigned.
 * - Vehicle tuning: center of mass, Ackermann factor, maximum speeds, steering angles, steering speed, and input exponent.
 * - Motor/brake tuning: max motor power, max brake torque, and handbrake torque.
 * - Transmission tuning: forward gears, final drive, idle RPM, redline RPM, shift thresholds, shift duration, and shift slip threshold.
 * - Stability: anti-roll toggle and front/rear anti-roll stiffness.
 * - Traction control and ABS: enable flags and slip thresholds.
 * - Dynamic grip: handbrake grip multipliers, locked-wheel grip multipliers, and lock detection thresholds.
 * - Grip circle: enable flag, start slip, full slip, and minimum forward/sideways grip multipliers.
 * - Friction: front/rear forward and sideways ::WheelFrictionSettings values.
 * - Lights: light lists, colors, intensities, fade duration, and initial light state.
 * - Engine sound: output group, volumes, on/off clips, smoothing, band centers, pitch curve, shift flare, and limiter.
 *
 * ::DriveTrainController
 * - Receives tuning from ::VehicleController.
 * - Requires Init(...) before normal control.
 * - SetUp() configures WheelCollider substeps, Rigidbody solver iterations, initial friction, and center of mass.
 *
 * ::TransmissionController
 * - forwardGears[]: forward gear ratios, where index 0 is first gear.
 * - finalDrive: final drive multiplier.
 * - idleRPM and redlineRPM: RPM normalization range.
 * - shiftUpRPM and shiftDownRPM: automatic shift thresholds.
 * - shiftDuration: time during which torque is cut while shifting.
 * - slipThreshold: wheel slip above which shifting is suppressed.
 * - OnShift: runtime callback list used for shift effects such as ::EngineSound::OnShift.
 *
 * ::EngineSound
 * - RPM and throttle: runtime values fed by ::VehicleController.
 * - minRPM and maxRPM: normalization range.
 * - outputGroup: mixer group used by engine audio sources.
 * - masterVolume, spatialBlend, and dopplerLevel.
 * - on_Idle, on_Low, on_Mid, on_High.
 * - off_Idle, off_Low, off_Mid, off_High.
 * - rpmLerpSpeed and throttleLerpSpeed.
 * - pitchVsRpm.
 * - bandCenters and bandSharpness.
 * - throttleShape and onThrottleBoost.
 * - shiftFlareAmount, shiftFlareTime, limiterStart, and limiterDepth.
 *
 * ::LightsController
 * - frontLights, dayLights, rearLights, reverseLights, and brakeLights.
 * - Color and intensity for each light group.
 * - fadeDuration for front/day/rear light fading.
 * - startLightsOn for initial normal-light state.
 *
 * ::TyreEffects
 * - smokePrefab: optional ParticleSystem prefab.
 * - skidTrailPrefab: optional TrailRenderer prefab.
 * - skidAudioClip: optional looping skid clip.
 * - skidAudioMixerGroup: optional mixer group for skid audio.
 * - slipThreshold: combined slip needed before effects begin.
 * - maxEmissionRatePerSecond: maximum smoke emission rate.
 * - maxEmissionRateAtSlip: slip value where smoke/audio reach maximum intensity.
 * - groundOffset: visual offset above the contact point.
 *
 * ::CollisionDetection
 * - crashClips: crash audio clips available for random selection.
 * - minimumVolume: lower volume bound for valid impacts.
 * - minImpactSpeed: impact speed that starts producing crash audio.
 * - maxImpactSpeed: impact speed that maps to full severity.
 * - minSeverityToPlay: minimum normalized severity required before a crash sound is played.
 * - volumeCurve and baseVolume: volume shaping.
 * - minPitch and maxPitch: pitch range.
 * - cooldown: minimum time between crash sounds.
 * - ignoreLayers: layers ignored for crash SFX.
 *
 * ::FollowCamera
 * - target: Transform followed by the camera.
 * - targetRb: optional Rigidbody used to choose movement direction.
 * - offset: camera side, height, and follow distance.
 * - positionSmoothTime: position smoothing.
 * - rotationSmoothTime: yaw smoothing.
 * - fixedPitchAngle: fixed camera pitch.
 * - lookAheadDistance: distance added along movement direction.
 * - minVelocityForDirection: minimum planar speed before velocity direction is used.
 *
 * ----------------------------------------------------------------------
 * @section car_lifecycle Lifecycle
 *
 * VehicleController.OnValidate:
 * - Validates that exactly four wheels are configured.
 * - Ensures each WheelSpec has a WheelCollider and visual Transform.
 * - Calls internal setup when the configuration is complete.
 *
 * VehicleController.Reset:
 * - Adds or resolves ::EngineSound, ::TransmissionController, ::DriveTrainController, and ::LightsController.
 * - Calls internal setup.
 *
 * VehicleController.Start:
 * - Calls internal setup.
 *
 * VehicleController.FixedUpdate:
 * - Reads throttle, steering, brake, and handbrake input.
 * - Calls ::DriveTrainController::ApplyWheelControls.
 * - Updates brake and reverse lights.
 * - Updates engine audio input values.
 *
 * DriveTrainController.SetUp:
 * - Configures WheelCollider substeps.
 * - Increases Rigidbody solver iterations.
 * - Applies initial friction settings.
 * - Sets the Rigidbody center of mass.
 *
 * TransmissionController.HandleShifting:
 * - Calculates engine RPM from driven-wheel RPM.
 * - Suppresses shifting during high wheel slip.
 * - Starts upshift or downshift coroutines when thresholds are crossed.
 * - Returns true while torque should be cut due to shifting.
 *
 * EngineSound.Update:
 * - Smooths RPM and throttle.
 * - Crossfades between RPM bands.
 * - Applies pitch mapping, shift flare, and limiter behavior.
 *
 * LightsController.Start:
 * - Applies the configured initial normal-light state.
 *
 * TyreEffects.FixedUpdate:
 * - Reads WheelCollider ground hit and slip.
 * - Emits smoke, enables skid trail, and plays skid audio when slip is high enough.
 * - Stops effects when the wheel is not grounded or slip is below threshold.
 *
 * CollisionDetection.OnCollisionEnter:
 * - Ignores configured layers.
 * - Calculates severity from collision velocity into contact normals.
 * - Applies severity threshold and cooldown.
 * - Plays a crash clip through ::SoundManager.
 *
 * FollowCamera.LateUpdate:
 * - Computes the movement direction.
 * - Smoothly moves and rotates the camera after the target has moved.
 *
 * ----------------------------------------------------------------------
 * @section car_usage Usage
 *
 * Minimal spawn:
 * @code{.cs}
 * public class Spawner : MonoBehaviour
 * {
 *     public GameObject carPrefab;
 *     public Transform spawnPoint;
 *
 *     private void Start()
 *     {
 *         Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);
 *     }
 * }
 * @endcode
 *
 * Reading vehicle state:
 * @code{.cs}
 * DriveTrainController drivetrain = car.GetComponent<DriveTrainController>();
 * TransmissionController transmission = car.GetComponent<TransmissionController>();
 *
 * float speedKmh = drivetrain.GetSpeed();
 * float engineRpm = transmission.EngineRPM;
 * int gear = transmission.CurrentGear;
 * bool braking = drivetrain.Braking;
 * bool reversing = drivetrain.Reversing;
 * @endcode
 *
 * Tuning friction at runtime:
 * @code{.cs}
 * DriveTrainController drivetrain = car.GetComponent<DriveTrainController>();
 *
 * WheelFrictionSettings frontSideways = drivetrain.frontSidewaysFriction;
 * frontSideways.stiffness = 2.5f;
 * drivetrain.frontSidewaysFriction = frontSideways;
 *
 * drivetrain.SetUp();
 * @endcode
 *
 * Manual light toggle:
 * @code{.cs}
 * LightsController lights = car.GetComponent<LightsController>();
 * lights.ToggleLights();
 * @endcode
 *
 * Snapping a follow camera after teleport or respawn:
 * @code{.cs}
 * FollowCamera camera = FindFirstObjectByType<FollowCamera>();
 *
 * if (camera != null)
 * {
 *     camera.SetTarget(car.transform);
 *     camera.SyncCamera();
 * }
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section car_api Public API Reference
 *
 * ::DriveTrainController
 * - void Init(Rigidbody carRigidBody, TransmissionController transmissionController,
 *             WheelCollider[] wheelsColliders, Transform[] wheelMeshes,
 *             bool[] driven, bool[] steering)
 *   Initializes required drivetrain references.
 *
 * - void SetUp()
 *   Applies WheelCollider, Rigidbody, friction, and center-of-mass setup.
 *
 * - void ApplyWheelControls(float throttle, float braking, bool handbrake,
 *                           float steering, bool gamepadSteering)
 *   Applies control input for one physics tick.
 *
 * - float GetSpeed()
 *   Returns current vehicle speed in km/h.
 *
 * - float GetMaxSpeed()
 *   Returns configured maximum forward speed.
 *
 * - float GetMaxReverseSpeed()
 *   Returns configured maximum reverse speed.
 *
 * - float GetSteeringAngle()
 *   Returns current smoothed steering angle.
 *
 * - float GetMaxSteeringAngle()
 *   Returns low-speed steering limit.
 *
 * - float GetMaxSteeringAngleAtTopSpeed()
 *   Returns top-speed steering limit.
 *
 * - void ApplyReplayWheelVisuals(float replaySteeringAngle)
 *   Updates visual wheel steering and mesh synchronization during replay playback.
 *
 * - bool Braking
 *   True when service braking is active.
 *
 * - bool Reversing
 *   True when reverse mode is active.
 *
 * ::TransmissionController
 * - bool HandleShifting(float wheelRPM, float wheelSlip)
 *   Updates RPM and automatic shifting. Returns true while torque should be cut.
 *
 * - float GetNormalizedRPM()
 *   Returns normalized RPM between idle and redline.
 *
 * - int CurrentGear
 *   Current zero-based forward gear index.
 *
 * - float EngineRPM
 *   Current calculated engine RPM.
 *
 * ::EngineSound
 * - void OnShift()
 *   Triggers the shift flare effect.
 *
 * ::LightsController
 * - void ToggleLights()
 *   Toggles front, day, and rear lights.
 *
 * - void SetLights(bool active)
 *   Sets front, day, and rear lights together.
 *
 * - void SetFrontLights(bool active)
 *   Fades front lights on or off.
 *
 * - void SetDayLights(bool active)
 *   Fades day lights on or off.
 *
 * - void SetRearLights(bool active)
 *   Fades rear lights on or off.
 *
 * - void SetBrakeLights(bool active)
 *   Sets brake lights instantly, restoring shared rear lights when needed.
 *
 * - void SetReverseLights(bool active)
 *   Sets reverse lights instantly, restoring shared rear lights when needed.
 *
 * ::FollowCamera
 * - void SetTarget(Transform newTarget)
 *   Assigns a target and immediately synchronizes the camera.
 *
 * - void SyncCamera()
 *   Instantly places and rotates the camera to its desired pose.
 *
 * ::CollisionDetection
 * - No public gameplay API. It reacts automatically to OnCollisionEnter.
 *
 * ::TyreEffects
 * - No public gameplay API. It reacts automatically during FixedUpdate.
 *
 * ----------------------------------------------------------------------
 * @section car_integration Integration Notes
 *
 * Input:
 * - Uses the Unity Input System.
 * - VehicleController can create default runtime bindings when autoCreateDefaultBindingsIfMissing is enabled.
 * - Device detection affects steering shaping for gamepad input.
 *
 * Saved assists:
 * - VehicleController reads ABS and traction-control settings from ::GameDataManager when available.
 *
 * Audio:
 * - VehicleController registers EngineSound::OnShift with the transmission shift callback list.
 * - CollisionDetection expects ::SoundManager.Instance for crash SFX.
 * - TyreEffects uses its own looping AudioSource for skid audio.
 *
 * Replay:
 * - ::DriveTrainController::ApplyReplayWheelVisuals updates wheel visuals without applying live torque or braking.
 *
 * Race spawning:
 * - ::TrackManager spawns the car prefab, tags it as Player, and assigns ::FollowCamera to the spawned car.
 *
 * ----------------------------------------------------------------------
 * @section car_performance Performance and GC
 *
 * - Wheel arrays and control arrays are initialized during setup and reused.
 * - WheelCollider.GetGroundHit is used only where wheel contact data is needed.
 * - EngineSound pre-creates its RPM-band AudioSources.
 * - TyreEffects accumulates fractional particles so smoke emission remains stable across fixed steps.
 * - CollisionDetection uses a cooldown to prevent crash SFX spam.
 * - SoundManager pooling is used for crash SFX.
 *
 * Suggested stability defaults:
 * - Rigidbody solverIterations and solverVelocityIterations are raised to 12 by DriveTrainController.
 * - WheelCollider.ConfigureVehicleSubsteps(0.5f, 20, 30) is applied to each wheel.
 *
 * ----------------------------------------------------------------------
 * @section car_troubleshooting Troubleshooting
 *
 * Vehicle will not move:
 * - Check that at least one wheel is marked powered.
 * - Check that WheelColliders touch ground colliders.
 * - Check maxMotorPower and forward friction stiffness.
 * - Check that the drivetrain was initialized by VehicleController.
 *
 * Steering feels unresponsive:
 * - Increase maxSteerAngle or steerSpeedDegPerSec.
 * - Increase maxSteerAngleAtTopSpeed if the car barely turns at speed.
 * - Adjust inputExponent for gamepad steering feel.
 *
 * Excessive wheelspin:
 * - Enable traction control.
 * - Reduce tractionSlipLimit.
 * - Increase forward friction stiffness.
 * - Tune limited-slip settings for driven wheels.
 *
 * Brakes lock too easily:
 * - Enable ABS.
 * - Tune absSlipLimit.
 * - Reduce maxBrakeTorque.
 * - Adjust locked-wheel grip multipliers.
 *
 * Car rolls too much:
 * - Enable antiRollToggle.
 * - Increase antiRollStiffnessFront and/or antiRollStiffnessRear.
 * - Lower the center of mass.
 *
 * Engine audio missing:
 * - Assign on/off throttle clips.
 * - Check minRPM, maxRPM, and masterVolume.
 * - Check output AudioMixerGroup routing.
 *
 * Lights do not toggle:
 * - Check light lists.
 * - Check lightsToggleAction binding.
 * - Check that VehicleController has a LightsController reference.
 *
 * Crash SFX never plays:
 * - Assign crashClips.
 * - Ensure ::SoundManager exists.
 * - Lower minImpactSpeed or minSeverityToPlay.
 * - Check ignoreLayers.
 * - Check cooldown.
 *
 * Skid effects never appear:
 * - Assign smokePrefab or skidTrailPrefab.
 * - Lower slipThreshold.
 * - Check WheelCollider grounding.
 * - Check that TyreEffects is placed on the same object as the WheelCollider.
 *
 * ----------------------------------------------------------------------
 * @section car_versions Version History
 *
 * - v1.6: Added grip-circle coupling, limited-slip approximation, replay wheel-visual support, and expanded documentation.
 * - v1.5: Added documentation and tuning cleanup.
 * - v1.4: Reworked slip values and dynamic grip behaviour.
 * - v1.3.5: Refactored VehicleController, DriveTrainController, and TransmissionController.
 * - v1.3: Added TyreEffects skid VFX/audio and CollisionDetection crash SFX.
 * - v1.2: Added engine audio with RPM-band crossfades.
 * - v1.1: Added automatic transmission shifting.
 * - v1.0: Initial four-wheel drivetrain, traction/ABS gates, anti-roll forces, and friction setup.
 */