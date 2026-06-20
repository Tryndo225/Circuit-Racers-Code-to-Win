using UnityEngine;

/// <summary>
/// Third-person follow camera for a racing game that tracks a target using a configurable
/// offset, smooth positional damping, smooth yaw alignment, a fixed pitch angle,
/// and movement-direction-based positioning. When reversing, both camera rotation
/// and position flip to the opposite side of the car.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @thread Unity main thread (LateUpdate).
/// </remarks>
public class FollowCamera : MonoBehaviour
{
	#region Inspector

	[Header("Target")]
	/// <summary>Transform to follow. If null, camera update is skipped.</summary>
	public Transform target;

	[Header("Physics")]
	/// <summary>
	/// Optional Rigidbody used to determine actual movement direction.
	/// If assigned, the camera can flip properly when the vehicle reverses.
	/// </summary>
	public Rigidbody targetRb;

	[Header("Offset")]
	/// <summary>
	/// Camera offset values. X is side offset, Y is height, and Z controls follow distance.
	/// The sign of Z is ignored for movement-direction-based positioning.
	/// </summary>
	public Vector3 offset = new Vector3(0f, 6f, -12f);

	[Header("Position Smoothing")]
	/// <summary>
	/// Time used by <see cref="Vector3.SmoothDamp"/> for positional follow smoothing.
	/// Smaller values make the camera more responsive, larger values make it smoother.
	/// </summary>
	public float positionSmoothTime = 0.25f;

	[Header("Rotation Smoothing")]
	/// <summary>
	/// Time used by <see cref="Mathf.SmoothDampAngle"/> for yaw smoothing.
	/// Smaller values make yaw react faster, larger values make it turn more gently.
	/// </summary>
	public float rotationSmoothTime = 0.2f;

	[Header("Pitch")]
	/// <summary>Fixed pitch angle in degrees applied to the camera rotation.</summary>
	public float fixedPitchAngle = 15f;

	[Header("Look Ahead")]
	/// <summary>
	/// Distance applied along the chosen movement heading to make the camera anticipate motion.
	/// </summary>
	public float lookAheadDistance = 4f;

	[Header("Movement Direction")]
	/// <summary>
	/// Minimum planar speed required before Rigidbody velocity is used as the camera heading.
	/// Below this threshold, target forward is used instead.
	/// </summary>
	public float minVelocityForDirection = 0.5f;

	#endregion

	#region State

	/// <summary>Velocity accumulator used internally by <see cref="Vector3.SmoothDamp"/>.</summary>
	private Vector3 positionVelocity;

	/// <summary>Velocity accumulator used internally by <see cref="Mathf.SmoothDampAngle"/>.</summary>
	private float yawVelocity;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Updates the camera after all regular frame movement using smooth positional damping toward
	/// a movement-direction-based follow position plus look-ahead, and smooth yaw damping toward
	/// the chosen heading while enforcing a fixed pitch angle.
	/// </summary>
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
	/// Instantly snaps the camera to the target using the configured movement-based offset,
	/// look-ahead, and fixed pitch angle. Useful after teleports, respawns, or scene loads.
	/// </summary>
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

	public void SetTarget(Transform newTarget)
	{
		target = newTarget;
		newTarget.TryGetComponent<Rigidbody>(out targetRb);
		SyncCamera();
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Returns the flattened heading the camera should use. Prefers Rigidbody planar velocity
	/// when the target is moving fast enough, otherwise falls back to target forward.
	/// This allows both camera rotation and camera position to flip when reversing.
	/// </summary>
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