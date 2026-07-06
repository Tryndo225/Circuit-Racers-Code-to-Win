using UnityEngine;

/// <summary>
/// Deprecated wheel specification container (collider, visual, drive/steer flags).
/// </summary>
/// <remarks>
/// @ingroup car_ctrl_deprecated
/// @deprecated This was part of an experimental custom wheel controller and is no longer used.
/// Prefer the current drivetrain/wheel setup managed by the active controllers in the project.
/// </remarks>
[System.Serializable]
public struct WheelSpec
{
	/// <summary>
	/// Physics wheel collider representing suspension and tire contact.
	/// </summary>
	public WheelCollider collider;

	/// <summary>
	/// Visual mesh Transform that mirrors the collider's pose for rendering.
	/// </summary>
	public Transform visual;

	/// <summary>
	/// True if this wheel receives motor torque (driven).
	/// </summary>
	public bool powered;

	/// <summary>
	/// True if this wheel steers (has steer angle applied).
	/// </summary>
	public bool steering;

	/// <summary>
	/// Value equality: all fields must match (collider, visual, powered, steering).
	/// </summary>
	public static bool operator ==(WheelSpec lhs, WheelSpec rhs)
	{
		return lhs.collider == rhs.collider && lhs.visual == rhs.visual && lhs.powered == rhs.powered && lhs.steering == rhs.steering;
	}

	/// <summary>
	/// Inequality: negation of <see cref="operator=="/>.
	/// </summary>
	public static bool operator !=(WheelSpec lhs, WheelSpec rhs)
	{
		return !(lhs == rhs);
	}

	/// <inheritdoc/>
	public override bool Equals(object obj)
	{
		if (obj is WheelSpec other)
		{
			return this == other;
		}
		return false;
	}

	/// <summary>
	/// Hash based on collider, visual, and flags; consistent with <see cref="Equals(object)"/>.
	/// </summary>
	public override int GetHashCode()
	{
		return (collider, visual, powered, steering).GetHashCode();
	}
}
