using UnityEngine;
/// <summary>
/// Third-person follow camera for a racing vehicle.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Tracks a target using configurable offset, smoothing, fixed pitch, and movement-direction-based positioning.
///
/// The camera follows the target from behind its current movement direction. When a target
/// <see cref="Rigidbody"/> is assigned and moving fast enough, the camera uses the rigidbody's planar
/// velocity as the heading. This allows the camera to flip naturally when the vehicle reverses.
///
/// Behaviour:
/// - Smoothly follows the desired position with <see cref="Vector3.SmoothDamp"/>.
/// - Smoothly aligns yaw with <see cref="Mathf.SmoothDampAngle"/>.
/// - Keeps a fixed pitch angle.
/// - Adds look-ahead in the current movement direction.
/// - Can be snapped instantly with <see cref="SyncCamera"/> after respawns or teleports.
///
/// Threading:
/// - Unity main thread only.
/// - Camera motion is updated in <see cref="LateUpdate"/>.
/// </remarks>
public class FollowCamera : MonoBehaviour
{
	#region Inspector

	[Header("Target")]
	/// <summary>
	/// Transform followed by the camera.
	/// </summary>
	/// <remarks>
	/// If this is null, camera updates are skipped.
	/// </remarks>
	[Tooltip("Transform followed by the camera. If empty, camera updates are skipped.")]
	[SerializeField] public Transform target;

	[Header("Physics")]
	/// <summary>
	/// Optional rigidbody used to determine actual movement direction.
	/// </summary>
	/// <remarks>
	/// If assigned, the camera can use vehicle velocity instead of only target forward direction.
	/// </remarks>
	[Tooltip("Optional Rigidbody used to determine actual movement direction, including reversing.")]
	public Rigidbody targetRb;

	[Header("Offset")]
	/// <summary>
	/// Camera offset from the target.
	/// </summary>
	/// <remarks>
	/// X is side offset, Y is height, and Z controls follow distance. The sign of Z is ignored
	/// because distance is calculated from its absolute value.
	/// </remarks>
	[Tooltip("Camera offset. X = side offset, Y = height, Z = follow distance.")]
	public Vector3 offset = new Vector3(0f, 6f, -12f);

	[Header("Position Smoothing")]
	/// <summary>
	/// Position smoothing time used by <see cref="Vector3.SmoothDamp"/>.
	/// </summary>
	/// <remarks>
	/// Smaller values make the camera more responsive. Larger values make it smoother.
	/// </remarks>
	[Tooltip("Position smoothing time. Smaller values are more responsive; larger values are smoother.")]
	public float positionSmoothTime = 0.25f;

	[Header("Rotation Smoothing")]
	/// <summary>
	/// Yaw smoothing time used by <see cref="Mathf.SmoothDampAngle"/>.
	/// </summary>
	/// <remarks>
	/// Smaller values make yaw react faster. Larger values make it turn more gently.
	/// </remarks>
	[Tooltip("Yaw smoothing time. Smaller values react faster; larger values turn more gently.")]
	public float rotationSmoothTime = 0.2f;

	[Header("Pitch")]
	/// <summary>
	/// Fixed pitch angle applied to the camera rotation.
	/// </summary>
	[Tooltip("Fixed pitch angle in degrees applied to the camera rotation.")]
	public float fixedPitchAngle = 15f;

	[Header("Look Ahead")]
	/// <summary>
	/// Distance added along the selected movement heading.
	/// </summary>
	/// <remarks>
	/// This makes the camera look and move slightly ahead of the target.
	/// </remarks>
	[Tooltip("Distance added along the movement heading to make the camera anticipate motion.")]
	public float lookAheadDistance = 4f;

	[Header("Movement Direction")]
	/// <summary>
	/// Minimum planar speed required before rigidbody velocity is used as the camera heading.
	/// </summary>
	/// <remarks>
	/// Below this threshold, the camera falls back to the target's forward direction.
	/// </remarks>
	[Tooltip("Minimum planar speed before Rigidbody velocity is used as the camera heading.")]
	public float minVelocityForDirection = 0.5f;

	#endregion

	#region State

	/// <summary>
	/// Velocity accumulator used internally by <see cref="Vector3.SmoothDamp"/>.
	/// </summary>
	private Vector3 positionVelocity;

	/// <summary>
	/// Velocity accumulator used internally by <see cref="Mathf.SmoothDampAngle"/>.
	/// </summary>
	private float yawVelocity;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Updates the camera after target movement for the current frame.
	/// </summary>
	/// <remarks>
	/// The camera moves toward a movement-direction-based follow position and smoothly rotates toward
	/// the same heading while keeping <see cref="fixedPitchAngle"/>.
	/// </remarks>
	private void LateUpdate()
	{
		if (target == null)
		{
			return;
		}

		Vector3 flatDirection = GetCameraDirection();

		Vector3 desiredPosition = GetDesiredCameraPosition();
		transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);

		float targetYaw = Mathf.Atan2(flatDirection.x, flatDirection.z) * Mathf.Rad2Deg;
		float currentYaw = transform.eulerAngles.y;

		float smoothYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);

		transform.rotation = Quaternion.Euler(fixedPitchAngle, smoothYaw, 0f);
	}

	#endregion

	#region Public API

	/// <summary>
	/// Instantly snaps the camera to the target using the configured movement-based offset.
	/// </summary>
	/// <remarks>
	/// Useful after teleports, respawns, or scene loads. Also clears smoothing velocity accumulators
	/// so the next frame does not continue old smoothing momentum.
	/// </remarks>
	public void SyncCamera()
	{
		if (target == null)
		{
			return;
		}

		transform.position = GetDesiredCameraPosition();

		Vector3 flatDirection = GetCameraDirection();
		float targetYaw = Mathf.Atan2(flatDirection.x, flatDirection.z) * Mathf.Rad2Deg;
		transform.rotation = Quaternion.Euler(fixedPitchAngle, targetYaw, 0f);

		positionVelocity = Vector3.zero;
		yawVelocity = 0f;
	}

	/// <summary>
	/// Assigns a new target transform and tries to cache its rigidbody.
	/// </summary>
	/// <param name="newTarget">New transform to follow.</param>
	/// <remarks>
	/// After assigning the target, the camera is immediately synchronized to avoid a delayed smooth movement
	/// from the previous target position.
	/// </remarks>
	public void SetTarget(Transform newTarget)
	{
		target = newTarget;
		newTarget.TryGetComponent<Rigidbody>(out targetRb);
		SyncCamera();
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Gets the flattened heading used by the camera.
	/// </summary>
	/// <returns>Normalized planar direction used for camera positioning and yaw.</returns>
	/// <remarks>
	/// Rigidbody planar velocity is preferred when the target is moving faster than
	/// <see cref="minVelocityForDirection"/>. Otherwise, the method falls back to the target's forward direction.
	/// </remarks>
	private Vector3 GetCameraDirection()
	{
		if (targetRb != null)
		{
			Vector3 flatVelocity = targetRb.linearVelocity;
			flatVelocity.y = 0f;

			if (flatVelocity.sqrMagnitude > minVelocityForDirection * minVelocityForDirection)
			{
				return flatVelocity.normalized;
			}
		}

		Vector3 flatForward = target.forward;
		flatForward.y = 0f;

		if (flatForward.sqrMagnitude < 0.001f)
		{
			return Vector3.forward;
		}

		return flatForward.normalized;
	}

	/// <summary>
	/// Calculates the desired camera position from target position, movement direction, offset, and look-ahead.
	/// </summary>
	/// <returns>Desired world-space camera position.</returns>
	private Vector3 GetDesiredCameraPosition()
	{
		Vector3 flatDirection = GetCameraDirection();
		float distance = Mathf.Abs(offset.z);
		Vector3 right = Vector3.Cross(Vector3.up, flatDirection).normalized;
		Vector3 lookAhead = flatDirection * lookAheadDistance;
		return target.position - flatDirection * distance + Vector3.up * offset.y + right * offset.x + lookAhead;
	}

	#endregion
}