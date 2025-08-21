using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class Wheel_Collider_3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody CarRigidbody;

    [SerializeField] private Transform WheelVisualSteer;

    [SerializeField] private Transform WheelVisualRotation;

    [Header("Wheel Collider Settings")]
    [SerializeField] private float Mass = 20f; // Mass of the wheel collider

    [SerializeField] private float Radius = 0.5f; // Radius of the wheel collider

    [SerializeField] private float Width = 0.2f; // Width of the wheel collider

    [SerializeField] private float SuspensionDistance = 0.2f; // Distance of the suspension travel

    [Header("Suspension Settings")]
    [SerializeField] private float Spring = 35000f; // Spring force for the suspension

    [SerializeField] private float Damper = 4500f; // Damping force for the suspension

    [Header("Collision Points")]
    [ReadOnly, SerializeField] private readonly List<Vector3> CollisionPoints = new List<Vector3>(); // Array to store collision points

    private GameObject _colliderGO;
    private MeshFilter _wheelMeshFilter;
    private MeshCollider _wheelMeshCollider;
    private ConfigurableJoint _suspention;
    private Rigidbody _wheelRB;
    private WheelContactRelay _relay;

    private float _currentTorque { get; set; } = 0f; // Current torque applied to the wheel
    private float _currentSteerAngle { get; set; } = 0f; // Current steering angle of the wheel
    private float _currentWheelSpeed = 0f; // Current speed of the wheel

    // --- Public API ---
    public void ApplyTorque(float torque)
    {
        _currentTorque = torque;
    }

    public void ApplySteering(float angle)
    {
        _currentSteerAngle = angle;
    }

    // --- Lifecycle ---
    private void Reset()
    {
        if (CarRigidbody == null)
            CarRigidbody = GetComponentInParent<Rigidbody>();

        EnsureSetup(createIfMissing: true);
    }

    private void Awake()
    {
        if (CarRigidbody == null)
            CarRigidbody = GetComponentInParent<Rigidbody>();

        EnsureSetup(createIfMissing: true);
        IgnoreCarSelfCollision();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            EnsureSetup(createIfMissing: true);
    }

    // --- Physics / update ---
    private void FixedUpdate()
    {
        if (_colliderGO == null || _wheelRB == null)
        {
            Debug.LogWarning("Wheel Collider is not properly set up! Please check the component.", this);
            return;
        }

        _colliderGO.transform.rotation = Quaternion.Euler(0, _currentSteerAngle, 0);

        int count = CollisionPoints.Count;

        if (count == 0)
        {
            // No collisions, skip force application
            return;
        }
        else
        {
            CarRigidbody.AddForceAtPosition(_colliderGO.transform.forward * _currentTorque, _colliderGO.transform.position, ForceMode.Force);
        }

        _currentTorque = 0f; // Reset torque after applying
        _currentWheelSpeed = _wheelRB.linearVelocity.magnitude;

        if (WheelVisualSteer != null)
        {
            WheelVisualSteer.position = _colliderGO.transform.position;
            WheelVisualSteer.rotation = _colliderGO.transform.rotation;
        }

        if (WheelVisualRotation != null)
        {
            WheelVisualRotation.Rotate(Vector3.right, (_currentWheelSpeed / (2 * Mathf.PI * Radius)) * 360 * Time.deltaTime);
        }

        CollisionPoints.Clear(); // Clear collision points after processing
    }

    // --- Internals ---
    private void EnsureSetup(bool createIfMissing)
    {
        // Ensure child
        _colliderGO = transform.Find("WheelColliderObject")?.gameObject;
        if (_colliderGO == null && createIfMissing)
        {
            EnsureCreation();
        }

        _colliderGO = transform.Find("WheelColliderObject").gameObject;

        if (_colliderGO == null)
        {
            Debug.LogWarning("WheelColliderObject not found! Please reset the component.", this);
            return;
        }

        // Ensure components
        _wheelMeshFilter = _colliderGO.GetComponent<MeshFilter>();
        _wheelMeshCollider = _colliderGO.GetComponent<MeshCollider>();
        _suspention = _colliderGO.GetComponent<ConfigurableJoint>();
        _wheelRB = _colliderGO.GetComponent<Rigidbody>();
        _relay = _colliderGO.GetComponent<WheelContactRelay>();

        if (_wheelRB == null)
        {
            Debug.LogWarning("Wheel Rigidbody is not assigned!", this);
        }

        if (WheelVisualSteer == null || WheelVisualRotation == null)
        {
            Debug.LogWarning("Wheel Visual Transforms are not assigned!", this);
        }

        if (_suspention == null)
        {
            Debug.LogWarning("Suspension Configurable Joint is not assigned!", this);
        }

        if (CarRigidbody == null)
        {
            Debug.LogWarning("Car Rigidbody is not assigned!", this);
        }

        if (_wheelMeshFilter == null)
        {
            Debug.LogWarning("Wheel Mesh Filter is not assigned!", this);
        }

        if (_wheelMeshCollider == null)
        {
            Debug.LogWarning("Wheel Mesh Collider is not assigned!", this);
        }

        // Property updates
        if (_suspention != null && CarRigidbody != null)
        {
            _suspention.connectedBody = CarRigidbody;
            _suspention.axis = Vector3.right;
            _suspention.secondaryAxis = Vector3.up;
            _suspention.linearLimit = new SoftJointLimit { limit = SuspensionDistance / 2 };
            _suspention.linearLimitSpring = new SoftJointLimitSpring
            {
                spring = Spring,
                damper = Damper
            };
        }

        _colliderGO.transform.localPosition = new Vector3(0, -SuspensionDistance, 0);
        _colliderGO.transform.localScale = new Vector3(Width, Radius, Radius);
        _colliderGO.GetComponent<Rigidbody>().mass = Mass;

        // Ensure relay is set up
        _relay.CollisionObservers.Add(OnMeshCollisionStay);
    }

    private void EnsureCreation()
    {
        // Ensure CarRigidbody is assigned
        if (CarRigidbody == null)
            CarRigidbody = GetComponentInParent<Rigidbody>();
        if (CarRigidbody == null)
            Debug.LogWarning("Car Rigidbody is not assigned! Please assign it in the inspector or ensure it is present in the parent object.", this);

        // Creation
        _colliderGO = new GameObject("WheelColliderObject");

        // Transform setup
        _colliderGO.transform.SetParent(transform, false);
        _colliderGO.transform.localPosition = new Vector3(0, 0, 0);
        _colliderGO.transform.localScale = new Vector3(Width, Radius, Radius);

        // Components setup
        _wheelRB = _colliderGO.AddComponent<Rigidbody>();
        _suspention = _colliderGO.AddComponent<ConfigurableJoint>();
        _wheelMeshFilter = _colliderGO.AddComponent<MeshFilter>();
        _wheelMeshCollider = _colliderGO.AddComponent<MeshCollider>();
        _relay = _colliderGO.AddComponent<WheelContactRelay>();

        // Rigidbody setup
        _wheelRB.mass = Mass;
        _wheelRB.useGravity = true;
        _wheelRB.interpolation = RigidbodyInterpolation.Interpolate;
        _wheelRB.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Suspension setup
        _suspention.connectedBody = CarRigidbody;
        _suspention.autoConfigureConnectedAnchor = true;

        _suspention.anchor = Vector3.zero;

        _suspention.axis = Vector3.up;

        _suspention.xMotion = _suspention.zMotion = ConfigurableJointMotion.Locked;
        _suspention.yMotion = ConfigurableJointMotion.Limited;

        _suspention.angularXMotion = _suspention.angularYMotion = _suspention.angularZMotion = ConfigurableJointMotion.Locked;

        _suspention.linearLimitSpring = new SoftJointLimitSpring { spring = Spring, damper = Damper };
        _suspention.linearLimit = new SoftJointLimit { limit = SuspensionDistance / 2 };

        // Mesh setup
        var originalMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var rotatedMesh = Instantiate(originalMesh);

        Quaternion rot = Quaternion.Euler(90f, 90f, 0f);
        var verts = rotatedMesh.vertices;
        var norms = rotatedMesh.normals;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = rot * verts[i];

        for (int i = 0; i < norms.Length; i++)
            norms[i] = rot * norms[i];

        rotatedMesh.normals = norms;
        rotatedMesh.vertices = verts;

        rotatedMesh.RecalculateBounds();

        _wheelMeshFilter.sharedMesh = rotatedMesh;
        _wheelMeshCollider.sharedMesh = rotatedMesh;
        _wheelMeshCollider.convex = true;
    }

    private void OnMeshCollisionStay(ContactPoint[] contacts)
    {
        // Collect for this physics step (will be consumed next FixedUpdate)
        for (int i = 0; i < contacts.Length; i++)
        {
            CollisionPoints.Add(contacts[i].point);
            Debug.DrawRay(contacts[i].point, contacts[i].normal * 0.2f, Color.red);
        }
    }

    private void IgnoreCarSelfCollision()
    {
        if (CarRigidbody == null || _wheelMeshCollider == null) return;
        var carCol = CarRigidbody.GetComponent<Collider>();
        if (carCol != null) Physics.IgnoreCollision(carCol, _wheelMeshCollider, true);
    }

    private Vector3 RotateAroundAxis(Vector3 v, Vector3 axis, float degrees)
    {
        return Quaternion.AngleAxis(degrees, axis.normalized) * v;
    }
}