using UnityEngine;

/// <summary>
/// Simple floating camera that follows a target from above using static and target-relative offsets.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @thread Unity main thread (LateUpdate).
/// </remarks>
public class FloatCamera : MonoBehaviour
{
	#region Inspector

	[Header("Target")]
	/// <summary>Transform to follow. If null, camera update is skipped.</summary>
	public Transform target;

	[Header("Offset")]
	/// <summary>
	/// Static world-space offset.
	/// This does not rotate with the target.
	/// </summary>
	public Vector3 offset = Vector3.zero;

	/// <summary>
	/// Offset relative to the target rotation.
	/// X = forward/backward, Y = height, Z = side.
	/// Negative X means behind the car.
	/// </summary>
	public Vector3 relativeOffset = new Vector3(-10f, 20f, 0f);

	/// <summary>
	/// If true, the relative offset follows only the target's yaw and ignores pitch/roll.
	/// This is usually better for a top/floating camera.
	/// </summary>
	public bool useFlatTargetDirection = true;

	[Header("Smoothing")]
	/// <summary>
	/// Time used by Vector3.SmoothDamp for positional smoothing.
	/// Smaller values make the camera more responsive.
	/// </summary>
	public float positionSmoothTime = 0.2f;

	/// <summary>
	/// Rotation smoothing speed.
	/// Higher values make the camera rotate faster.
	/// </summary>
	public float rotationSpeed = 10f;

	[Header("Orientation")]
	/// <summary>
	/// If true, the top of the screen follows the target's forward direction.
	/// If false, world forward remains the top of the screen.
	/// </summary>
	public bool followTargetRotation = true;

	#endregion

	#region State

	/// <summary>Velocity accumulator used by Vector3.SmoothDamp.</summary>
	private Vector3 positionVelocity;

	#endregion

	#region Unity Methods

	private void LateUpdate()
	{
		if (target == null)
		{
			return;
		}

		Vector3 desiredPosition = GetDesiredPosition();

		transform.position = Vector3.SmoothDamp(
			transform.position,
			desiredPosition,
			ref positionVelocity,
			positionSmoothTime);

		Quaternion desiredRotation = GetDesiredRotation();

		transform.rotation = Quaternion.Slerp(
			transform.rotation,
			desiredRotation,
			rotationSpeed * Time.deltaTime);
	}

	#endregion

	#region Public API

	/// <summary>
	/// Instantly places and rotates the camera to match the target.
	/// Useful after respawn, teleport, or scene loading.
	/// </summary>
	public void SyncCamera()
	{
		if (target == null)
		{
			return;
		}

		transform.position = GetDesiredPosition();
		transform.rotation = GetDesiredRotation();

		positionVelocity = Vector3.zero;
	}

	/// <summary>
	/// Assigns a new target and immediately syncs the camera to it.
	/// </summary>
	public void SetTarget(Transform newTarget)
	{
		target = newTarget;
		SyncCamera();
	}

	#endregion

	#region Helpers

	private Vector3 GetDesiredPosition()
	{
		return target.position + offset + GetRelativeWorldOffset();
	}

	private Vector3 GetRelativeWorldOffset()
	{
		Vector3 forward = target.forward;
		Vector3 right = target.right;
		Vector3 up = Vector3.up;

		if (useFlatTargetDirection)
		{
			forward.y = 0f;

			if (forward.sqrMagnitude < 0.001f)
			{
				forward = Vector3.forward;
			}

			forward.Normalize();
			right = Vector3.Cross(Vector3.up, forward).normalized;
		}
		else
		{
			up = target.up;
		}

		return
			forward * relativeOffset.x +
			up * relativeOffset.y +
			right * relativeOffset.z;
	}

	private Quaternion GetDesiredRotation()
	{
		Vector3 lookDirection = target.position - transform.position;

		if (lookDirection.sqrMagnitude < 0.001f)
		{
			return transform.rotation;
		}

		Vector3 upDirection = followTargetRotation ? target.forward : Vector3.forward;
		upDirection.y = 0f;

		if (upDirection.sqrMagnitude < 0.001f)
		{
			upDirection = Vector3.forward;
		}

		return Quaternion.LookRotation(lookDirection.normalized, upDirection.normalized);
	}

	#endregion
}