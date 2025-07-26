using UnityEngine;

public class Car_Controller : MonoBehaviour
{
    [SerializeField] private WheelCollider frontLeftCollider;
    [SerializeField] private WheelCollider frontRightCollider;
    [SerializeField] private WheelCollider rearLeftCollider;
    [SerializeField] private WheelCollider rearRightCollider;

    [SerializeField] private Transform frontLeftMesh;
    [SerializeField] private Transform frontRightMesh;
    [SerializeField] private Transform rearLeftMesh;
    [SerializeField] private Transform rearRightMesh;

    [SerializeField] private float maxMotorTorque = 1500f; // Maximum torque applied to the rear wheels
    [SerializeField] private float maxSteerAngle = 30f; // Maximum angle for steering the front wheels

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W))
        {
            ApplyMotorTorque(maxMotorTorque);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            ApplyMotorTorque(-maxMotorTorque);
        }

        if (Input.GetKey(KeyCode.A))
        {
            Steer(-maxSteerAngle);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            Steer(maxSteerAngle);
        }
        else
        {
            Steer(0f); // Reset steering angle if no input
        }

        float speed = GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f; // Convert m/s to km/h

        Debug.Log("Speed: " + speed + " km/h");
        
        UpdateWheelPoses();
    }

    // Method to apply motor torque to the rear wheels
    public void ApplyMotorTorque(float torque)
    {
        rearLeftCollider.motorTorque = torque;
        rearRightCollider.motorTorque = torque;
    }

    // Method to update the wheel transforms based on the WheelColliders
    private void UpdateWheelPoses()
    {
        UpdateWheelPose(frontLeftCollider, frontLeftMesh);
        UpdateWheelPose(frontRightCollider, frontRightMesh);
        UpdateWheelPose(rearLeftCollider, rearLeftMesh);
        UpdateWheelPose(rearRightCollider, rearRightMesh);
    }

    // Method to update the position and rotation of a wheel based on its WheelCollider
    private void UpdateWheelPose(WheelCollider collider, Transform transform)
    {
        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);
        transform.position = collider.transform.position;
        transform.rotation = rotation;
    }

    // Method to steer the front wheels
    public void Steer(float steerAngle)
    {
        frontLeftCollider.steerAngle = steerAngle;
        frontRightCollider.steerAngle = steerAngle;
    }
}
