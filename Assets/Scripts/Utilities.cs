using UnityEngine;
using System;
using System.Runtime.Serialization;
using Unity.VisualScripting;

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

namespace Generic
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;

        public static T Instance
        {
            get => _instance;
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}