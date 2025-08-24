using UnityEngine;
using System;
using System.Runtime.Serialization;

[Serializable]
public abstract class SerializableRunnable : ISerializationCallbackReceiver
{
    public abstract void Run();

    public virtual void OnBeforeSerialize()
    { }

    public virtual void OnAfterDeserialize()
    { }
}

public static class LayerMaskUtils
{
    public static bool Contains(this LayerMask mask, int layer)
        => (mask.value & (1 << layer)) != 0;
}