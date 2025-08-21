using UnityEngine;

public static class LayerMaskUtils
{
    public static bool Contains(this LayerMask mask, int layer)
        => (mask.value & (1 << layer)) != 0;
}