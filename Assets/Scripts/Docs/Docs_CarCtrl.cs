/**
 * @file Docs_CarCtrl.cs
 * @brief Documentation entry for the Vehicle Control subsystem.
 *
 * @defgroup car_ctrl Vehicle Control
 * @ingroup systems
 * @brief Modular 4-wheel car stack: input, drivetrain, transmission, audio, lights, VFX, and camera follow.
 *
 * @details
 * The Vehicle Control subsystem is composed of small, focused MonoBehaviours:
 * - ::VehicleController orchestrates input, delegates to drivetrain/transmission, and wires audio/lights.
 * - ::DriveTrainContoller applies steering, traction/brake torques, anti-roll, and syncs wheel meshes.
 * - ::TransmissionController converts wheel RPM to engine RPM and manages auto-shifting with slip gating.
 * - ::EngineSound synthesizes engine audio from RPM and throttle using 4-band crossfades (on/off layers).
 * - ::LightsController drives head/day/rear/reverse/brake lights with optional fading and color presets.
 * - ::TyreEffects spawns skid VFX (particles, trail) and optional skid audio based on slip.
 * - ::CollisionDetection plays crash SFX using collision impulse with cooldown and layer filtering.
 * - ::FollowCamera is a simple, stable third-person follow camera with offset and fixed pitch.
 *
 * Key design goals:
 * - Explicit dependencies (Init/SetUp), small clear responsibilities, inspector-first tuning.
 * - Deterministic physics path (FixedUpdate), minimal GC, predictable audio.
 *
 * Contents:
 * - see car_overview
 * - see car_scene_setup
 * - see car_inspector
 * - see car_lifecycle
 * - see car_usage
 * - see car_api
 * - see car_integration
 * - see car_performance
 * - see car_troubleshooting
 * - see car_versions
 *
 * ----------------------------------------------------------------------
 * @section car_overview Overview
 *
 * Flow (per physics tick):
 * 1) ::VehicleController reads input (Input System), routes steering/throttle/brakes to ::DriveTrainContoller.
 * 2) ::DriveTrainContoller queries grounded driven wheels, computes average RPM and slip,
 *    calls ::TransmissionController::HandleShifting(...), applies motor/brake torques and anti-roll forces,
 *    then syncs wheel visuals.
 * 3) ::VehicleController updates ::EngineSound with current engine RPM and throttle.
 * 4) ::LightsController is updated for brake/reverse states; manual toggle handles head/day/rear sets.
 * 5) ::TyreEffects reacts to per-wheel slip and spawns smoke/trails/skid audio.
 * 6) ::CollisionDetection triggers crash SFX on significant impulses.
 * 7) ::FollowCamera smooth-follows the target with local-space offset and fixed pitch.
 *
 * Physics principles:
 * - WheelCollider drives traction; forward/sideways friction curves are configurable per axle.
 * - Anti-roll applies force difference across left/right to resist body roll.
 * - Traction control reduces motor torque when slip > threshold; ABS reduces brake torque similarly.
 *
 * Audio principles:
 * - Engine audio is 4 bands (idle/low/mid/high) with on/off-throttle layers crossfaded by RPM and load.
 * - Gear shift "flare" briefly boosts pitch; a soft limiter attenuates near redline.
 *
 * ----------------------------------------------------------------------
 * @section car_scene_setup Scene Setup
 *
 * Required:
 * - A GameObject with:
 *     - Rigidbody (non-kinematic).
 *     - 4 WheelCollider components (FL, FR, RL, RR).
 *     - 4 wheel visual Transforms in the same order for mesh sync.
 *     - ::VehicleController, ::DriveTrainContoller, ::TransmissionController, ::EngineSound, ::LightsController.
 * - Optional:
 *     - ::TyreEffects on each wheel (next to the WheelCollider).
 *     - ::CollisionDetection on the body (requires Rigidbody).
 *     - ::FollowCamera in the scene, target set to the vehicle root.
 *
 * Layering:
 * - Use a "Track" or "Ground" layer for drivable surfaces. Configure ::CollisionDetection ignoreLayers accordingly.
 *
 * Audio:
 * - Route engine and SFX to appropriate AudioMixerGroups. Ensure a single active AudioListener (usually main camera).
 *
 * ----------------------------------------------------------------------
 * @section car_inspector Inspector (Main Components)
 *
 * ::VehicleController
 * - wheels[4]: (WheelCollider, wheel visual, powered, steering) in order FL, FR, RL, RR.
 * - Input actions: throttle, steer, brake, handbrake, lights toggle (auto-create bindings optional).
 * - Vehicle: center of mass offset, Ackermann factor, max speeds, steering angles/speed, input exponent.
 * - Motor/Brake: max motor power, max brake torque, handbrake torque.
 * - Transmission: forward gear ratios, final drive, idle/redline/shift RPMs, shift duration, slip threshold.
 * - Stability: anti-roll front/rear stiffness.
 * - TCS/ABS: enable flags and slip limits.
 * - Friction: per-axle forward/sideways stiffness and curve values.
 * - Lights: intensities/colors and light lists; fade duration; initial on/off.
 * - EngineSound: mixer group, volumes, curves, centers, sharpness, on/off clips, shift flare, limiter.
 *
 * ::DriveTrainContoller
 * - Mirrors tuning from VehicleController (SetUp fills these).
 * - Requires Init(Rigidbody, TransmissionController, WheelCollider[], Transform[], bool[] driven, bool[] steering).
 *
 * ::TransmissionController
 * - forwardGears[], finalDrive, idle/redline, shiftUp/shiftDown RPM, shiftDuration, slipThreshold.
 * - OnShift: list of callbacks (EngineSound::OnShift is added by VehicleController).
 *
 * ::EngineSound
 * - min/max RPM, on/off clips (idle/low/mid/high), mixer group, volumes, smoothing speeds,
 *   pitch vs RPM curve, band centers/sharpness, on/off balance, shift flare, limiter parameters.
 *
 * ::LightsController
 * - front/day/rear/reverse/brake lights (lists), colors and intensities, fade duration, initial on/off.
 *
 * ::TyreEffects (per wheel)
 * - smoke prefab (ParticleSystem), skid TrailRenderer prefab, skid clip and mixer group.
 * - slipThreshold, maxEmissionRatePerSecond, maxEmissionRateAtSlip, groundOffset.
 *
 * ::CollisionDetection
 * - crashClips[], volume curve, min pitch/max pitch, cooldown.
 * - minImpulse/maxImpulse mapping, ignoreLayers.
 *
 * ::FollowCamera
 * - target, local offset, follow speed, yaw smoothness, fixed pitch angle.
 *
 * ----------------------------------------------------------------------
 * @section car_lifecycle Lifecycle
 *
 * Boot:
 * - VehicleController::Reset/Start/OnValidate wires missing components, pushes config via SetUp(), and
 *   creates default input bindings if enabled.
 *
 * Per physics tick:
 * - FixedUpdate in VehicleController:
 *   - Read input, call DriveTrainContoller::ApplyWheelControls(throttle, braking, handbrake, steer, isGamepad).
 *   - Update ::LightsController brake/reverse states.
 *   - Refresh engine audio inputs (RPM and throttle).
 *
 * Per frame:
 * - FollowCamera::Update smooths camera.
 * - EngineSound::Update applies smoothing, band mixing, limiter, and shift flare.
 * - TyreEffects::FixedUpdate spawns smoke/trails and skid audio based on slip.
 * - CollisionDetection::OnCollisionEnter plays crash SFX if severity passes threshold.
 *
 * ----------------------------------------------------------------------
 * @section car_usage Usage
 *
 * Minimal code to spawn and drive:
 * @code{.cs}
 * public class Spawner : MonoBehaviour
 * {
 *     public GameObject carPrefab;
 *     public Transform spawnPoint;
 *
 *     void Start()
 *     {
 *         var car = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);
 *         // Ensure wheels array and visuals are assigned in prefab's VehicleController.
 *     }
 * }
 * @endcode
 *
 * Reading vehicle state:
 * @code{.cs}
 * var vc = car.GetComponent<VehicleController>();
 * // Example: hook up UI to RPM
 * var rpm = car.GetComponent<TransmissionController>().EngineRPM;
 * @endcode
 *
 * Tuning friction (example):
 * @code{.cs}
 * // Increase front grip
 * var dt = car.GetComponent<DriveTrainContoller>();
 * dt.frontSidewaysFriction[0] = 2.5f; // stiffness
 * @endcode
 *
 * Manual lights:
 * @code{.cs}
 * car.GetComponent<LightsController>().ToggleLights();
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section car_api Public API Reference (Selected)
 *
 * ::VehicleController
 * - void SetUp(): resolves dependencies, pushes inspector config to subsystems.
 *
 * ::DriveTrainContoller
 * - void Init(Rigidbody rb, TransmissionController tx, WheelCollider[] wc, Transform[] meshes,
 *             bool[] driven, bool[] steering)
 * - void SetUp(): configures wheel substeps, solver iterations, friction, center of mass.
 * - void ApplyWheelControls(float throttle, bool braking, bool handbrake, float steering, bool gamepadSteering)
 * - bool Braking (property): true if service brakes active.
 * - bool Reversing (property): true if reversing mode active.
 *
 * ::TransmissionController
 * - bool HandleShifting(float wheelRPM, float wheelSlip): updates EngineRPM and schedules shift coroutines;
 *   returns true while shifting (torque cut).
 * - int  CurrentGear (property)
 * - float EngineRPM (property)
 *
 * ::EngineSound
 * - void OnShift(): triggers short pitch flare.
 * - void SetUp(): clamps and applies inspector parameters to sources.
 *
 * ::LightsController
 * - void ToggleLights()
 * - void SetBrakeLights(bool active)
 * - void SetReverseLights(bool active)
 * - void SetLights(bool active), SetFrontLights(bool), SetDayLights(bool), SetRearLights(bool)
 *
 * ::TyreEffects
 * - Emits particles/trail and plays skid audio automatically based on WheelCollider slip.
 *
 * ::CollisionDetection
 * - Plays one-shot crash SFX based on collision impulse; respects cooldown and layer mask.
 *
 * ::FollowCamera
 * - void SyncCamera(): immediate snap to target position/rotation with fixed pitch.
 *
 * ----------------------------------------------------------------------
 * @section car_integration Integration Notes
 *
 * Input:
 * - Uses the Unity Input System. VehicleController can auto-create basic bindings for keyboard/gamepad.
 * - Device detection switches "gamepad steering" which uses input exponent shaping.
 *
 * Audio:
 * - Connect TransmissionController::OnShift to EngineSound::OnShift (VehicleController does this).
 * - Use SoundManager (see audio_mgr) to unify SFX routing; CollisionDetection expects SoundManager.Instance.
 *
 * VFX:
 * - Assign TyreEffects prefabs (particles/trails) in the wheel objects; ensure proper layer/materials.
 *
 * Camera:
 * - Assign FollowCamera.target to the vehicle root; tune offset and smoothing for your game feel.
 *
 * Networking (out of scope here):
 * - Keep physics authority on server or host; send inputs and replicate high-level state (gear, RPM, lights).
 *
 * ----------------------------------------------------------------------
 * @section car_performance Performance and GC
 *
 * - Avoid frequent array reallocation in gameplay; arrays are configured once in SetUp().
 * - WheelCollider.GetGroundHit is used only when grounded; keep friction and solver iterations reasonable.
 * - EngineSound pre-creates sources in Awake; clips should be compressed/streamed as appropriate.
 * - TyreEffects accumulates fractional particles to keep emission stable at fixed delta.
 * - CollisionDetection uses a short cooldown to prevent SFX spam.
 *
 * Suggested physics defaults:
 * - Rigidbody.solverIterations and solverVelocityIterations set to 12 for stability.
 * - WheelCollider.ConfigureVehicleSubsteps(0.5, 20, 30) called for each wheel.
 *
 * ----------------------------------------------------------------------
 * @section car_troubleshooting Troubleshooting
 *
 * Vehicle will not move:
 * - Check WheelCollider mass/friction setup and that at least one axle is marked "powered."
 * - Verify ground colliders and layers; WheelColliders must contact a collider to generate forces.
 *
 * Steering feels unresponsive:
 * - Increase maxSteerAngle and/or steerSpeedDegPerSec; reduce maxSpeed or increase inputExponent.
 *
 * Excessive wheelspin:
 * - Enable tractionControlEnabled and reduce tractionSlipLimit; tune forward friction stiffness/curve.
 *
 * Brakes ineffective or lock instantly:
 * - Enable absEnabled and increase absSlipLimit; adjust maxBrakeTorque and friction curves.
 *
 * Audio missing or flat:
 * - Assign on/off band clips; verify minRPM/maxRPM and pitchVsRpm curve; check AudioMixer routing.
 *
 * Lights do not toggle:
 * - Ensure lists contain valid Light references; verify the lights input binding and that
 *   VehicleController hooks OnLightsPerformed.
 *
 * Crash SFX never plays:
 * - Lower minImpulse, raise baseVolume, ensure cooldown is not constantly active.
 * - Verify ignoreLayers mask does not include your collision targets.
 *
 * ----------------------------------------------------------------------
 * @section car_versions Version History
 *
 * - v1.5: Added Documentation; minor tuning improvements.
 * - v1.4: Rewamp of slip values.
 * - v1.3.5: Refractored VehicleController, DriveTrainController, TransmissionController.
 * - v1.3: TyreEffects skid VFX and audio, CollisionDetection crash SFX.
 * - v1.2: Engine audio with crossfades, skid effects, collision SFX.
 * - v1.1: Transmission auto-shift.
 * - v1.0: Initial 4-wheel drivetrain, Traction/ABS gates, anti-roll pair forces, friction setup arrays.
 */
