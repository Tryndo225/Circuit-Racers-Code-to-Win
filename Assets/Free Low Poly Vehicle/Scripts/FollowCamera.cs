using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;

    [Header("Offset & Movement")]
    public Vector3 offset = new Vector3(0f, 6f, -12f); // Adjust as needed
    public float followSpeed = 5f;

    [Header("Rotation Settings")]
    public float yawSmoothness = 5f;
    public float fixedPitchAngle = 15f; // Vertical camera angle (X axis)

    private Vector3 velocity;

    void FixedUpdate()
    {
        if (target == null) return;

        // Smooth follow
        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, 1f / followSpeed);

        // Get flat forward (Y rotation only)
        Vector3 flatForward = target.forward;
        flatForward.y = 0;

        if (flatForward.sqrMagnitude > 0.001f)
        {
            Quaternion yawRotation = Quaternion.LookRotation(flatForward);
            // Apply fixed pitch (X) angle on top of yaw
            Vector3 euler = yawRotation.eulerAngles;
            euler.x = fixedPitchAngle;
            Quaternion finalRotation = Quaternion.Euler(euler);

            transform.rotation = Quaternion.Slerp(transform.rotation, finalRotation, Time.fixedDeltaTime * yawSmoothness);
        }
    }
}
