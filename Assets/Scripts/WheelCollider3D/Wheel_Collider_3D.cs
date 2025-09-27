using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Experimental custom wheel collider prototype that attempted to emulate suspension and contact-driven
/// traction forces using a <see cref="ConfigurableJoint"/> and a convex <see cref="MeshCollider"/>.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl_deprecated
/// @deprecated This was an early attempt at a bespoke wheel solution and is **not** used by the current drivetrain.
/// Prefer Unity's built-in <see cref="WheelCollider"/> workflow and the active drivetrain/vehicle code.
///
/// Design notes:
/// - Spawns a child "WheelColliderObject" with its own <see cref="Rigidbody"/>, <see cref="MeshCollider"/> (convex),
///   and <see cref="ConfigurableJoint"/> connected to the car body.
/// - Uses <see cref="WheelContactRelay"/> to forward contact points gathered on the wheel mesh,
///   then applies a forward force at the contact area based on the requested torque.
/// - Visuals are split into two transforms: one for steer yaw (<see cref="WheelVisualSteer"/>) and
///   one for rolling rotation (<see cref="WheelVisualRotation"/>).
///
/// Limitations:
/// - Contact/friction/torque model is extremely simplified and does not produce reliable tire behavior.
/// - Requires careful collider filtering to avoid self-collisions; mass/spring/damper need heavy tuning.
/// - Kept here only for reference/documentation.
/// </remarks>
public class Wheel_Collider_3D : MonoBehaviour
{
    #region Inspector: References

    [Header("References")]
    /// <summary>Car body rigidbody this wheel attaches to (connected to the joint).</summary>
    [SerializeField] private Rigidbody CarRigidbody;

    /// <summary>Transform that receives steering yaw (visual only).</summary>
    [SerializeField] private Transform WheelVisualSteer;

    /// <summary>Transform that receives rolling rotation (visual only).</summary>
    [SerializeField] private Transform WheelVisualRotation;

    #endregion

    #region Inspector: Wheel Collider Settings

    [Header("Wheel Collider Settings")]
    /// <summary>Mass of the spawned wheel rigidbody.</summary>
    [SerializeField] private float Mass = 20f;

    /// <summary>Visual/physical radius (used for visual spin speed and child scale).</summary>
    [SerializeField] private float Radius = 0.5f;

    /// <summary>Visual/physical width (used for child scale).</summary>
    [SerializeField] private float Width = 0.2f;

    /// <summary>Total suspension travel (mapped to joint linear limit).</summary>
    [SerializeField] private float SuspensionDistance = 0.2f;

    #endregion

    #region Inspector: Suspension

    [Header("Suspension Settings")]
    /// <summary>Suspension spring constant for the joint's linear limit spring.</summary>
    [SerializeField] private float Spring = 35000f;

    /// <summary>Suspension damper for the joint's linear limit spring.</summary>
    [SerializeField] private float Damper = 4500f;

    #endregion

    #region Debug: Collision Points

    [Header("Collision Points")]
    /// <summary>Per-physics-step cache of contact points relayed by <see cref="WheelContactRelay"/>.</summary>
    [ReadOnly, SerializeField] private readonly List<Vector3> CollisionPoints = new List<Vector3>();

    #endregion

    #region Runtime Components (spawned/cached)

    private GameObject _colliderGO;
    private MeshFilter _wheelMeshFilter;
    private MeshCollider _wheelMeshCollider;
    private ConfigurableJoint _suspention;
    private Rigidbody _wheelRB;
    private WheelContactRelay _relay;

    #endregion

    #region Runtime State

    /// <summary>Current torque command to apply along the wheel forward axis.</summary>
    private float _currentTorque { get; set; } = 0f;

    /// <summary>Current steering yaw angle (degrees) applied to the wheel transform.</summary>
    private float _currentSteerAngle { get; set; } = 0f;

    /// <summary>Instantaneous linear wheel speed magnitude (m/s) used for visual spin.</summary>
    private float _currentWheelSpeed = 0f;

    #endregion

    #region Public API

    /// <summary>
    /// Queues a torque value to be applied during the next physics tick at the wheel contact.
    /// </summary>
    /// <param name="torque">Torque proxy (mapped to a forward force).</param>
    public void ApplyTorque(float torque)
    {
        _currentTorque = torque;
    }

    /// <summary>
    /// Sets the current steering angle (degrees) for visual yaw.
    /// </summary>
    /// <param name="angle">Yaw angle in degrees.</param>
    public void ApplySteering(float angle)
    {
        _currentSteerAngle = angle;
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>Auto-wires references and creates internal wheel object on Reset in editor.</summary>
    private void Reset()
    {
        if (CarRigidbody == null)
            CarRigidbody = GetComponentInParent<Rigidbody>();

        EnsureSetup(createIfMissing: true);
    }

    /// <summary>Ensures setup and ignores self-collision on Awake.</summary>
    private void Awake()
    {
        if (CarRigidbody == null)
            CarRigidbody = GetComponentInParent<Rigidbody>();

        EnsureSetup(createIfMissing: true);
        IgnoreCarSelfCollision();
    }

    /// <summary>Rebuilds internal setup when inspector values change (editor only).</summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
            EnsureSetup(createIfMissing: true);
    }

    #endregion

    #region Physics

    /// <summary>
    /// Physics tick: applies steer yaw, converts pending torque into a forward force at the wheel,
    /// updates visual rotation, and clears contact cache.
    /// </summary>
    private void FixedUpdate()
    {
        if (_colliderGO == null || _wheelRB == null)
        {
            Debug.LogWarning("Wheel Collider is not properly set up! Please check the component.", this);
            return;
        }

        // Visual steer (y-axis)
        _colliderGO.transform.rotation = Quaternion.Euler(0, _currentSteerAngle, 0);

        int count = CollisionPoints.Count;

        // Apply a simple forward force if any contacts were reported
        if (count != 0)
        {
            CarRigidbody.AddForceAtPosition(
                _colliderGO.transform.forward * _currentTorque,
                _colliderGO.transform.position,
                ForceMode.Force);
        }

        // Consume torque and update spin
        _currentTorque = 0f;
        _currentWheelSpeed = _wheelRB.linearVelocity.magnitude;

        // Copy pose to visuals
        if (WheelVisualSteer != null)
        {
            WheelVisualSteer.position = _colliderGO.transform.position;
            WheelVisualSteer.rotation = _colliderGO.transform.rotation;
        }

        if (WheelVisualRotation != null)
        {
            // crude spin rate from linear speed
            WheelVisualRotation.Rotate(
                Vector3.right,
                (_currentWheelSpeed / (2 * Mathf.PI * Radius)) * 360f * Time.deltaTime);
        }

        // Clear contacts for the next step
        CollisionPoints.Clear();
    }

    #endregion

    #region Setup & Creation

    /// <summary>
    /// Ensures the internal wheel GameObject and components exist and are configured from inspector values.
    /// </summary>
    /// <param name="createIfMissing">Create internal objects if not found.</param>
    private void EnsureSetup(bool createIfMissing)
    {
        // Find or create child holder
        _colliderGO = transform.Find("WheelColliderObject")?.gameObject;
        if (_colliderGO == null && createIfMissing)
        {
            EnsureCreation();
        }

        _colliderGO = transform.Find("WheelColliderObject")?.gameObject;

        if (_colliderGO == null)
        {
            Debug.LogWarning("WheelColliderObject not found! Please reset the component.", this);
            return;
        }

        // Cache components
        _wheelMeshFilter = _colliderGO.GetComponent<MeshFilter>();
        _wheelMeshCollider = _colliderGO.GetComponent<MeshCollider>();
        _suspention = _colliderGO.GetComponent<ConfigurableJoint>();
        _wheelRB = _colliderGO.GetComponent<Rigidbody>();
        _relay = _colliderGO.GetComponent<WheelContactRelay>();

        // Basic sanity
        if (_wheelRB == null) Debug.LogWarning("Wheel Rigidbody is not assigned!", this);
        if (WheelVisualSteer == null || WheelVisualRotation == null) Debug.LogWarning("Wheel Visual Transforms are not assigned!", this);
        if (_suspention == null) Debug.LogWarning("Suspension Configurable Joint is not assigned!", this);
        if (CarRigidbody == null) Debug.LogWarning("Car Rigidbody is not assigned!", this);
        if (_wheelMeshFilter == null) Debug.LogWarning("Wheel Mesh Filter is not assigned!", this);
        if (_wheelMeshCollider == null) Debug.LogWarning("Wheel Mesh Collider is not assigned!", this);

        // Joint configuration
        if (_suspention != null && CarRigidbody != null)
        {
            _suspention.connectedBody = CarRigidbody;
            _suspention.axis = Vector3.right;
            _suspention.secondaryAxis = Vector3.up;
            _suspention.linearLimit = new SoftJointLimit { limit = SuspensionDistance / 2f };
            _suspention.linearLimitSpring = new SoftJointLimitSpring { spring = Spring, damper = Damper };
        }

        // Pose/scale and mass
        _colliderGO.transform.localPosition = new Vector3(0f, -SuspensionDistance, 0f);
        _colliderGO.transform.localScale = new Vector3(Width, Radius, Radius);
        _colliderGO.GetComponent<Rigidbody>().mass = Mass;

        // Relay contacts into our cache
        if (_relay != null)
            _relay.CollisionObservers.Add(OnMeshCollisionStay);
    }

    /// <summary>
    /// Creates the internal wheel object, rigidbody, joint, mesh, and relay components.
    /// </summary>
    private void EnsureCreation()
    {
        // Ensure car body is found
        if (CarRigidbody == null)
            CarRigidbody = GetComponentInParent<Rigidbody>();
        if (CarRigidbody == null)
            Debug.LogWarning("Car Rigidbody is not assigned! Please assign it in the inspector or ensure it is present in the parent object.", this);

        // Create child
        _colliderGO = new GameObject("WheelColliderObject");
        _colliderGO.transform.SetParent(transform, false);
        _colliderGO.transform.localPosition = Vector3.zero;
        _colliderGO.transform.localScale = new Vector3(Width, Radius, Radius);

        // Components
        _wheelRB = _colliderGO.AddComponent<Rigidbody>();
        _suspention = _colliderGO.AddComponent<ConfigurableJoint>();
        _wheelMeshFilter = _colliderGO.AddComponent<MeshFilter>();
        _wheelMeshCollider = _colliderGO.AddComponent<MeshCollider>();
        _relay = _colliderGO.AddComponent<WheelContactRelay>();

        // Rigidbody
        _wheelRB.mass = Mass;
        _wheelRB.useGravity = true;
        _wheelRB.interpolation = RigidbodyInterpolation.Interpolate;
        _wheelRB.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Joint
        _suspention.connectedBody = CarRigidbody;
        _suspention.autoConfigureConnectedAnchor = true;
        _suspention.anchor = Vector3.zero;
        _suspention.axis = Vector3.up;
        _suspention.xMotion = _suspention.zMotion = ConfigurableJointMotion.Locked;
        _suspention.yMotion = ConfigurableJointMotion.Limited;
        _suspention.angularXMotion = _suspention.angularYMotion = _suspention.angularZMotion = ConfigurableJointMotion.Locked;
        _suspention.linearLimitSpring = new SoftJointLimitSpring { spring = Spring, damper = Damper };
        _suspention.linearLimit = new SoftJointLimit { limit = SuspensionDistance / 2f };

        // Mesh (rotated built-in cylinder to approximate a wheel)
        var originalMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var rotatedMesh = Instantiate(originalMesh);

        Quaternion rot = Quaternion.Euler(90f, 90f, 0f);
        var verts = rotatedMesh.vertices;
        var norms = rotatedMesh.normals;
        for (int i = 0; i < verts.Length; i++) verts[i] = rot * verts[i];
        for (int i = 0; i < norms.Length; i++) norms[i] = rot * norms[i];
        rotatedMesh.normals = norms;
        rotatedMesh.vertices = verts;
        rotatedMesh.RecalculateBounds();

        _wheelMeshFilter.sharedMesh = rotatedMesh;
        _wheelMeshCollider.sharedMesh = rotatedMesh;
        _wheelMeshCollider.convex = true;
    }

    #endregion

    #region Contacts & Utilities

    /// <summary>
    /// Observer callback from <see cref="WheelContactRelay"/>; caches contact points for the next physics step.
    /// </summary>
    /// <param name="contacts">Contact points reported by Unity collisions.</param>
    private void OnMeshCollisionStay(ContactPoint[] contacts)
    {
        for (int i = 0; i < contacts.Length; i++)
        {
            CollisionPoints.Add(contacts[i].point);
            Debug.DrawRay(contacts[i].point, contacts[i].normal * 0.2f, Color.red);
        }
    }

    /// <summary>
    /// Prevents the car's own collider from colliding with the wheel mesh collider.
    /// </summary>
    private void IgnoreCarSelfCollision()
    {
        if (CarRigidbody == null || _wheelMeshCollider == null) return;
        var carCol = CarRigidbody.GetComponent<Collider>();
        if (carCol != null) Physics.IgnoreCollision(carCol, _wheelMeshCollider, true);
    }

    /// <summary>
    /// Helper to rotate a vector around an axis by degrees (unused utility kept for reference).
    /// </summary>
    private Vector3 RotateAroundAxis(Vector3 v, Vector3 axis, float degrees)
    {
        return Quaternion.AngleAxis(degrees, axis.normalized) * v;
    }

    #endregion
}
