using UnityEngine;
using System;
using System.Collections.Generic;

public class WheelContactRelay : MonoBehaviour
{
    public List<Action<ContactPoint[]>> CollisionObservers = new List<Action<ContactPoint[]>>();

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