using UnityEngine;

/// <summary>
/// Floating replay camera that follows a target from above using static and target-relative offsets.
/// </summary>
/// <remarks>
/// @ingroup replay_system
/// @brief Follows a replay target transform with smoothed position and rotation.
///
/// The camera position is calculated from:
/// - The target position.
/// - A static world-space <see cref="offset"/>.
/// - A target-relative <see cref="relativeOffset"/>.
///
/// The camera can optionally flatten the target direction so pitch and roll do not affect the
/// relative offset. This is useful when the camera should behave more like a top/floating replay camera
/// than a fully target-rotated chase camera.
///
/// Threading:
/// - Unity main thread only.
/// - Camera motion is updated in <see cref="LateUpdate"/>.
/// </remarks>
public class FloatCamera : MonoBehaviour
{
	#region Inspector

	[Header("Target")]
	/// <summary>
	/// Transform followed by this camera.
	/// </summary>
	/// <remarks>
	/// If this reference is null, the camera does not update.
	/// </remarks>
	[Tooltip("Transform followed by this camera. If empty, camera movement is skipped.")]
	public Transform target;

	[Header("Offset")]
	/// <summary>
	/// Static world-space offset added to the target position.
	/// </summary>
	/// <remarks>
	/// This offset does not rotate with the target.
	/// </remarks>
	[Tooltip("Static world-space offset added to the target position.")]
	public Vector3 offset = Vector3.zero;

	/// <summary>
	/// Offset relative to the target orientation.
	/// </summary>
	/// <remarks>
	/// X controls forward/backward distance, Y controls height, and Z controls side offset.
	/// Negative X places the camera behind the target when using the target forward direction.
	/// </remarks>
	[Tooltip("Target-relative offset. X is forward/backward, Y is height, and Z is side offset.")]
	public Vector3 relativeOffset = new Vector3(-10f, 20f, 0f);

	/// <summary>
	/// Determines whether the relative offset uses only the target yaw direction.
	/// </summary>
	/// <remarks>
	/// When enabled, target pitch and roll are ignored while calculating the relative offset.
	/// </remarks>
	[Tooltip("If enabled, relative offset follows only target yaw and ignores pitch/roll.")]
	public bool useFlatTargetDirection = true;

	[Header("Smoothing")]
	/// <summary>
	/// Smooth damp time used for camera position movement.
	/// </summary>
	/// <remarks>
	/// Smaller values make the camera react faster to target movement.
	/// </remarks>
	[Tooltip("SmoothDamp time for camera position. Smaller values make the camera more responsive.")]
	public float positionSmoothTime = 0.2f;

	/// <summary>
	/// Rotation smoothing speed used when rotating toward the desired camera orientation.
	/// </summary>
	/// <remarks>
	/// Higher values make the camera rotate faster.
	/// </remarks>
	[Tooltip("Rotation smoothing speed. Higher values rotate the camera faster.")]
	public float rotationSpeed = 10f;

	[Header("Orientation")]
	/// <summary>
	/// Determines whether the camera's up direction follows the target forward direction.
	/// </summary>
	/// <remarks>
	/// When enabled, the top of the screen follows the target's forward direction.
	/// When disabled, world forward is used as the screen-up reference.
	/// </remarks>
	[Tooltip("If enabled, the top of the screen follows the target's forward direction.")]
	public bool followTargetRotation = true;

	#endregion

	#region State

	/// <summary>
	/// Velocity accumulator used by <see cref="Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float)"/>.
	/// </summary>
	private Vector3 positionVelocity;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Updates the camera position and rotation after the target has moved for the frame.
	/// </summary>
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
	/// </summary>
	/// <remarks>
	/// Useful after respawn, teleport, or scene loading because it removes smoothing delay.
	/// </remarks>
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
	/// Assigns a new target and immediately synchronizes the camera to it.
	/// </summary>
	/// <param name="newTarget">New transform that should be followed by the camera.</param>
	public void SetTarget(Transform newTarget)
	{
		target = newTarget;
		SyncCamera();
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Calculates the desired world position of the camera.
	/// </summary>
	/// <returns>Target position plus world-space and target-relative offsets.</returns>
	private Vector3 GetDesiredPosition()
	{
		return target.position + offset + GetRelativeWorldOffset();
	}

	/// <summary>
	/// Calculates the target-relative offset in world space.
	/// </summary>
	/// <returns>World-space offset derived from <see cref="relativeOffset"/> and target orientation.</returns>
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

	/// <summary>
	/// Calculates the desired camera rotation looking toward the target.
	/// </summary>
	/// <returns>Desired camera rotation, or the current rotation if the look direction is too small.</returns>
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