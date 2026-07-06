using UnityEngine;

/// <summary>
/// Serializable wheel specification used by <see cref="VehicleController"/>.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Stores the collider, visual wheel transform, and drive/steer flags for one vehicle wheel.
///
/// The vehicle controller expects four entries in front-left, front-right, rear-left, rear-right order.
/// Each entry is later unpacked and passed to <see cref="DriveTrainController"/> during setup.
/// </remarks>
[System.Serializable]
public struct WheelSpec
{
	/// <summary>
	/// Physics wheel collider representing suspension, tire contact, and wheel forces.
	/// </summary>
	[Tooltip("Physics wheel collider representing suspension, tire contact, and wheel forces.")]
	public WheelCollider collider;

	/// <summary>
	/// Visual wheel transform synchronized with the collider pose.
	/// </summary>
	[Tooltip("Visual wheel transform synchronized with the collider pose.")]
	public Transform visual;

	/// <summary>
	/// Whether this wheel receives motor torque.
	/// </summary>
	[Tooltip("Whether this wheel receives motor torque.")]
	public bool powered;

	/// <summary>
	/// Whether this wheel receives steering angle.
	/// </summary>
	[Tooltip("Whether this wheel receives steering angle.")]
	public bool steering;

	/// <summary>
	/// Compares two wheel specifications for value equality.
	/// </summary>
	/// <param name="lhs">Left-hand wheel specification.</param>
	/// <param name="rhs">Right-hand wheel specification.</param>
	/// <returns><c>true</c> if collider, visual transform, powered flag, and steering flag match; otherwise <c>false</c>.</returns>
	public static bool operator ==(WheelSpec lhs, WheelSpec rhs)
	{
		return lhs.collider == rhs.collider && lhs.visual == rhs.visual && lhs.powered == rhs.powered && lhs.steering == rhs.steering;
	}

	/// <summary>
	/// Compares two wheel specifications for inequality.
	/// </summary>
	/// <param name="lhs">Left-hand wheel specification.</param>
	/// <param name="rhs">Right-hand wheel specification.</param>
	/// <returns><c>true</c> if any field differs; otherwise <c>false</c>.</returns>
	public static bool operator !=(WheelSpec lhs, WheelSpec rhs)
	{
		return !(lhs == rhs);
	}

	/// <summary>
	/// Determines whether another object represents the same wheel specification.
	/// </summary>
	/// <param name="obj">Object to compare with this value.</param>
	/// <returns><c>true</c> if the object is an equal <see cref="WheelSpec"/>; otherwise <c>false</c>.</returns>
	public override bool Equals(object obj)
	{
		if (obj is WheelSpec other)
		{
			return this == other;
		}
		return false;
	}

	/// <summary>
	/// Gets a hash code based on the collider, visual transform, and wheel flags.
	/// </summary>
	/// <returns>Hash code consistent with <see cref="Equals(object)"/>.</returns>
	public override int GetHashCode()
	{
		return (collider, visual, powered, steering).GetHashCode();
	}
}