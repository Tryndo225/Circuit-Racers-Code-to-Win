/**
 * @file Docs_Wheels_Deprecated.cs
 * @brief Documentation entry for the deprecated custom wheel controller prototypes.
 *
 * @defgroup car_ctrl_deprecated Deprecated Wheel Controller
 * @ingroup car_ctrl
 * @brief Legacy experimental wheel physics (kept for reference only).
 *
 * @details
 * This group documents an **experimental, deprecated** attempt to implement a bespoke wheel
 * solution using mesh colliders and configurable joints. It is **not** used by the current
 * vehicle system and should not be integrated into new code or scenes.
 *
 * Implementations:
 * - ::Wheel_Collider_3D — a prototype "wheel" built from a child Rigidbody, MeshCollider (convex),
 *   and ConfigurableJoint connected to the car body. Contact points are relayed to apply a crude
 *   forward force based on a pending torque command.
 * - ::WheelContactRelay — forwards collision contacts to observers via callbacks.
 * - ::WheelSpec — early data container for wheel assignment (collider/visual/powered/steering).
 *
 * Rationale for deprecation:
 * - Unrealistic tire behavior (insufficient friction model, no pacejka or slip curves).
 * - Fragile tuning: mass, springs, damping, and joint limits were highly sensitive.
 * - Complex self-collision filtering and susceptibility to tunneling/instability.
 * - Unity’s built-in WheelCollider plus the current drivetrain stack provides better stability,
 *   performance, and maintainability.
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_overview Overview
 *
 * Goals (original):
 * - Replace WheelCollider with an explicit joint-driven, mesh-collider "wheel".
 * - Drive traction by applying forces at contact points sourced from collision callbacks.
 * - Split visuals into steer (yaw) and roll (spin) transforms.
 *
 * Actual behavior:
 * - Contact points are gathered by ::WheelContactRelay and cached each physics step.
 * - ::Wheel_Collider_3D converts a queued torque value into a forward force at the wheel child’s
 *   position if contacts were detected, then clears the cache.
 * - Visuals are updated for yaw and spin from the child’s pose and linear speed.
 *
 * Limitations:
 * - No robust slip/adhesion model; force projection is simplistic.
 * - Requires precise collider filtering to avoid the car colliding with its own wheel.
 * - Joint/spring tuning is brittle across frame rates and surface setups.
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_components Components
 *
 * Wheel_Collider_3D:
 * - Spawns "WheelColliderObject" child with:
 *   - Rigidbody (mass, CCD, interpolation).
 *   - MeshCollider (convex) using a rotated built-in cylinder mesh.
 *   - ConfigurableJoint to the car body (linear limit = suspension travel).
 *   - WheelContactRelay to surface collision contacts.
 * - Public API:
 *   - ApplyTorque(float): queues torque proxy for next FixedUpdate.
 *   - ApplySteering(float): sets visual yaw angle for the wheel.
 *
 * WheelContactRelay:
 * - Emits Collision.contacts arrays to registered observers each OnCollisionStay.
 *
 * WheelSpec:
 * - Aggregates references used by early controller drafts (collider, visual, flags).
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_lifecycle Lifecycle
 *
 * Setup:
 * - On Reset/Awake: creates or validates the child wheel object and components.
 * - Ignores self-collision between car body collider and wheel MeshCollider.
 *
 * FixedUpdate:
 * - Applies steer yaw to the wheel child.
 * - If any contacts were reported, applies a forward force proportional to queued torque.
 * - Spins visual mesh based on child rigidbody linear speed and radius.
 * - Clears contact cache.
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_usage Usage (Not Recommended)
 *
 * This subsystem is deprecated and retained only for historical reference. Do **not** integrate it
 * into production vehicles. Prefer the current drivetrain with Unity WheelCollider and the supported
 * controllers (see @ingroup car_ctrl).
 *
 * Migration guidance:
 * - Replace Wheel_Collider_3D with standard WheelCollider components.
 * - Use your active drivetrain/transmission controllers to drive torque, braking, and steering.
 * - Map visual steering/rolling via WheelCollider APIs and your wheel mesh updaters.
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_performance Performance & Stability
 *
 * - Mesh collisions on moving wheels are comparatively heavy and prone to jitter.
 * - Joint-driven suspension without proper constraints can introduce oscillations.
 * - The simplified traction model leads to non-physical handling at speed and under load.
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_troubleshooting Troubleshooting (Historical)
 *
 * - Wheel shakes violently: reduce SuspensionDistance, tune Spring/Damper, verify joint axes/limits.
 * - Car collides with its own wheel: ensure Physics.IgnoreCollision between body and wheel collider.
 * - No traction: ensure contacts are being relayed; confirm wheel child is not airborne or penetrating.
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_versions Version History
 *
 * - v0.2 (deprecated): Added contact relay and visual split (steer vs roll).
 * - v0.1 (experimental): Initial joint/mesh-based wheel prototype.
 *
 * ----------------------------------------------------------------------
 * @section car_ctrl_deprecated_status Deprecation Notice
 *
 * Status: **Deprecated**
 * Replacement: Unity WheelCollider + current drivetrain/vehicle controllers in @ingroup car_ctrl.
 * Intent: Retained for reference and archival documentation only.
 */
