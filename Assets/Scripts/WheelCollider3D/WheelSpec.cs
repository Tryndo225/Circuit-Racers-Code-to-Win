using UnityEngine;

[System.Serializable]
public struct WheelSpec
{
    public WheelCollider collider;
    public Transform visual;
    public bool powered;
    public bool steering;

    public static bool operator ==(WheelSpec lhs, WheelSpec rhs)
    {
        return lhs.collider == rhs.collider && lhs.visual == rhs.visual && lhs.powered == rhs.powered && lhs.steering == rhs.steering;
    }

    public static bool operator !=(WheelSpec lhs, WheelSpec rhs)
    {
        return !(lhs == rhs);
    }

    public override bool Equals(object obj)
    {
        if (obj is WheelSpec other)
        {
            return this == other;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return (collider, visual, powered, steering).GetHashCode();
    }
}