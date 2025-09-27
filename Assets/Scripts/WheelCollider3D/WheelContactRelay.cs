using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Deprecated collision relay that forwards <see cref="Collision.contacts"/> arrays
/// to registered observers on <see cref="OnCollisionStay(Collision)"/>.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl_deprecated
/// @deprecated Part of an old custom wheel/contact experiment; not used by the current drivetrain.
/// Prefer the active wheel/physics integration in the project.
/// </remarks>
public class WheelContactRelay : MonoBehaviour
{
    /// <summary>
    /// Subscribers that will be invoked with the contact points from the current collision.
    /// </summary>
    public List<Action<ContactPoint[]>> CollisionObservers = new List<Action<ContactPoint[]>>();

    /// <summary>
    /// Unity physics callback: invoked every fixed frame a collider/rigidbody is touching this object.
    /// Relays the <see cref="Collision.contacts"/> array to all non-null observers.
    /// </summary>
    /// <param name="collision">Collision data from Unity, including contact points.</param>
    private void OnCollisionStay(Collision collision)
    {
        foreach (var item in CollisionObservers)
        {
            if (item != null)
            {
                item.Invoke(collision.contacts);
                Debug.Log($"Information Relayed to {item}");
            }
        }
    }
}
