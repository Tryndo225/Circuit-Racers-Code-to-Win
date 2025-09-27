using UnityEngine;

/// <summary>
/// Third-person follow camera that tracks a target with a configurable local-space offset,
/// smooth positional damping, and yaw aligned to the target with a fixed pitch angle.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @thread Unity main thread (Update).
/// </remarks>
public class FollowCamera : MonoBehaviour
{
    #region Inspector

    [Header("Target Settings")]
    /// <summary>Transform to follow. If null, no update occurs.</summary>
    public Transform target;

    [Header("Offset & Movement")]
    /// <summary>Camera offset in the target's local space.</summary>
    public Vector3 offset = new Vector3(0f, 6f, -12f);

    /// <summary>Follow responsiveness (higher = snappier). Used as 1 / followSpeed in SmoothDamp.</summary>
    public float followSpeed = 5f;

    [Header("Rotation Settings")]
    /// <summary>Yaw interpolation speed (higher = faster yaw alignment).</summary>
    public float yawSmoothness = 5f;

    /// <summary>Fixed pitch angle in degrees applied on top of yaw.</summary>
    public float fixedPitchAngle = 15f;

    #endregion

    #region State

    /// <summary>Velocity accumulator used by SmoothDamp.</summary>
    private Vector3 velocity;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Updates position using SmoothDamp toward target.TransformPoint(offset) and
    /// rotates toward target yaw while enforcing a fixed pitch angle.
    /// </summary>
    private void Update()
    {
        if (target == null) return;

        // Smooth follow toward local-space offset
        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            1f / followSpeed
        );

        // Compute yaw from target forward projected onto XZ plane
        Vector3 flatForward = target.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude > 0.001f)
        {
            Quaternion yawRotation = Quaternion.LookRotation(flatForward);

            // Apply fixed pitch on top of yaw
            Vector3 euler = yawRotation.eulerAngles;
            euler.x = fixedPitchAngle;
            Quaternion finalRotation = Quaternion.Euler(euler);

            // Smooth yaw toward target orientation
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                finalRotation,
                Time.fixedDeltaTime * yawSmoothness
            );
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Instantly snaps camera position and rotation to the target with the configured offset and pitch.
    /// Useful after teleports or scene loads.
    /// </summary>
    public void SyncCamera()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = desiredPosition;

        Vector3 flatForward = target.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude > 0.001f)
        {
            Quaternion yawRotation = Quaternion.LookRotation(flatForward);
            Vector3 euler = yawRotation.eulerAngles;
            euler.x = fixedPitchAngle;
            Quaternion finalRotation = Quaternion.Euler(euler);
            transform.rotation = finalRotation;
        }
    }

    #endregion
}
