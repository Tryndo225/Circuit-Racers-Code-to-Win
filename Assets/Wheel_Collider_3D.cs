using System.Collections.Generic;
using UnityEngine;

public class Wheel_Collider_3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody CarRigidbody;
    [SerializeField] private Transform WheelVisual;

    [Header("Wheel Collider Settings")]
    [SerializeField] private float Mass = 20f; // Mass of the wheel collider
    [SerializeField] private float Radius = 0.5f; // Radius of the wheel collider
    [SerializeField] private float Width = 0.2f; // Width of the wheel collider
    [SerializeField] private float SuspensionDistance = 0.2f; // Distance of the suspension travel

    [Header("Suspension Settings")]
    [SerializeField] private float Spring = 35000f; // Spring force for the suspension
    [SerializeField] private float Damper = 4500f; // Damping force for the suspension

    [Header("Collision Points")]
    [ReadOnly, SerializeField] private readonly HashSet<Vector3> Collisions = new HashSet<Vector3>(); // Array to store collision points

    private MeshFilter WheelMeshFilter;
    private MeshCollider WheelMeshCollider;

    private GameObject Mesh;

    private ConfigurableJoint Suspention;

    private Rigidbody WheelRigidBody;

    private float CurrentTorque { get; set; } = 0f; // Current torque applied to the wheel
    private float CurrentSteerAngle { get; set; } = 0f; // Current steering angle of the wheel
    private float CurrentWheelSpeed = 0f;

    private void Reset()
    {
        if (Mesh != null)
        {
            DestroyImmediate(Mesh);
        }

        Mesh = new GameObject("Mesh");
        Mesh.transform.SetParent(transform, false);

        CarRigidbody = GetComponentInParent<Rigidbody>();

        Mesh.transform.localPosition = new Vector3(0, 0, 0);
        Mesh.transform.localScale = new Vector3(Width, Radius, Radius);

        WheelRigidBody = Mesh.AddComponent<Rigidbody>();
        WheelRigidBody.mass = Mass;
        WheelRigidBody.useGravity = false;
        WheelRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        WheelRigidBody.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotationY;

        Suspention = Mesh.AddComponent<ConfigurableJoint>();
        Suspention.connectedBody = CarRigidbody;
        Suspention.autoConfigureConnectedAnchor = true;
        Suspention.anchor = Vector3.zero;
        Suspention.axis = Vector3.up;
        Suspention.secondaryAxis = Vector3.up;
        Suspention.xMotion = Suspention.zMotion = ConfigurableJointMotion.Locked;
        Suspention.yMotion = ConfigurableJointMotion.Limited;
        Suspention.angularXMotion = Suspention.angularYMotion = Suspention.angularZMotion = ConfigurableJointMotion.Locked;
        Suspention.linearLimitSpring = new SoftJointLimitSpring { spring = Spring, damper = Damper };
        Suspention.linearLimit = new SoftJointLimit { limit = SuspensionDistance };

        WheelMeshFilter = Mesh.AddComponent<MeshFilter>();
        WheelMeshCollider = Mesh.AddComponent<MeshCollider>();

        var Original = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var RotatedMesh = Instantiate(Original);

        Quaternion rot = Quaternion.Euler(90f, 90f, 0f);
        var verts = RotatedMesh.vertices;
        var norms = RotatedMesh.normals;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = rot * verts[i];

        for (int i = 0; i < norms.Length; i++)
            norms[i] = rot * norms[i];

        RotatedMesh.normals = norms;
        RotatedMesh.vertices = verts;

        RotatedMesh.RecalculateBounds();

        WheelMeshFilter.sharedMesh = RotatedMesh;
        WheelMeshCollider.sharedMesh = RotatedMesh;
        WheelMeshCollider.convex = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        AddContacts(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        AddContacts(collision);
    }

    // Optional: clear points at the end of the frame if you only
    // want “this frame’s” contacts
    void LateUpdate()
    {
        Collisions.Clear();
    }

    private void AddContacts(Collision collision)
    {
        // ContactPoint[] contacts = collision.contacts; // same as below
        for (int i = 0; i < collision.contactCount; i++)
        {
            var cp = collision.GetContact(i);
            Collisions.Add(cp.point);
            // cp.normal is the collision normal if you need it
            Debug.DrawRay(cp.point, cp.normal * 0.2f, Color.red, 0.1f);
        }
    }

    private void OnValidate()
    {
        Mesh = GameObject.Find("Mesh");

        if (Mesh == null)
        {
            Debug.LogWarning("Mesh GameObject not found! Please reset the component.", this);
            return;
        }

        WheelMeshFilter = Mesh.GetComponent<MeshFilter>();
        WheelMeshCollider = Mesh.GetComponent<MeshCollider>();
        Suspention = Mesh.GetComponent<ConfigurableJoint>();
        WheelRigidBody = Mesh.GetComponent<Rigidbody>();

        if (WheelRigidBody == null)
        {
            Debug.LogWarning("Wheel Rigidbody is not assigned!", this);
        }

        if (WheelVisual == null)
        {
            Debug.LogWarning("Wheel Visual Transform is not assigned!", this);
        }

        if (Suspention == null)
        {
            Debug.LogWarning("Suspension Configurable Joint is not assigned!", this);
        }

        if (CarRigidbody == null)
        {
            Debug.LogWarning("Car Rigidbody is not assigned!", this);
        }

        if (WheelMeshFilter == null)
        {
            Debug.LogWarning("Wheel Mesh Filter is not assigned!", this);
        }

        if (WheelMeshCollider == null)
        {
            Debug.LogWarning("Wheel Mesh Collider is not assigned!", this);
        }

        if (Suspention != null && CarRigidbody != null)
        {
            Suspention.connectedBody = CarRigidbody;
            Suspention.axis = Vector3.right;
            Suspention.secondaryAxis = Vector3.up;
            Suspention.linearLimit = new SoftJointLimit { limit = SuspensionDistance };
            Suspention.linearLimitSpring = new SoftJointLimitSpring
            {
                spring = Spring,
                damper = Damper
            };
        }

        Mesh.transform.localPosition = new Vector3(0, -SuspensionDistance, 0);
        Mesh.transform.localScale = new Vector3(Width, Radius, Radius);
        Mesh.GetComponent<Rigidbody>().mass = Mass;
    }

    private void Awake()
    {
        Collider CarCollider = CarRigidbody.gameObject.GetComponent<Collider>();
        Physics.IgnoreCollision(CarCollider, WheelMeshCollider);
    }

    private void FixedUpdate()
    {
        



    }
}
