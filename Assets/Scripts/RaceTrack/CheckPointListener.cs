using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class CheckPointListener : MonoBehaviour
{
    [Header("Check Point Reference")]
    [SerializeField, ReadOnly] private Collider checkPointCollider;

    [SerializeField, ReadOnly] private Renderer checkPointRenderer;

    private bool _isActive = false;
    private List<Action> _listeners = new List<Action>();

    public Vector3 cPClaimedPosition { get; private set; }
    public Quaternion cPClaimedRotation { get; private set; }
    public Vector3 cPClaimedRbLinearVelocity { get; private set; }
    public Vector3 cPClaimedRbAngularVelocity { get; private set; }

    #region Unity Methods
    private void OnValidate()
    {
        GetReferences();
    }

    private void Awake()
    {
        GetReferences();
    }

    private void Start()
    {
        GetReferences();
    }
    #endregion Unity Methods

    #region Setup Methods
    private void GetReferences()
    {
        checkPointCollider = GetComponent<Collider>();
        if (checkPointCollider == null)
        {
            Debug.LogError("CheckPointListener requires a Collider component.");
        }

        if (!checkPointCollider.isTrigger)
        {
            Debug.LogWarning("CheckPointListener collider should be set as a trigger.");
        }

        checkPointRenderer = GetComponent<Renderer>();
        if (checkPointRenderer == null)
        {
            Debug.LogError("CheckPointListener requires a Renderer component.");
        }
    }
    #endregion Setup Methods

    private void OnTriggerEnter(Collider other)
    {
        if (_isActive && other.CompareTag("Player"))
        {
            other.GetComponent<Rigidbody>();

            cPClaimedPosition = other.transform.position;
            cPClaimedRotation = other.transform.rotation;

            var playerRigidbody = other.GetComponent<Rigidbody>();

            cPClaimedRbLinearVelocity = playerRigidbody.linearVelocity;
            cPClaimedRbAngularVelocity = playerRigidbody.angularVelocity;

            if (_listeners != null)
            {
                foreach (var listener in _listeners)
                {
                    listener?.Invoke();
                }
            }
        }
    }

    public void SetActive(bool isActive)
    {
        if (checkPointCollider == null || checkPointRenderer == null)
        {
            GetReferences();
        }

        _isActive = isActive;
        checkPointCollider.enabled = isActive;
        checkPointRenderer.enabled = isActive;
    }

    #region Observer Pattern Methods
    public void AddListener(Action listener)
    {
        _listeners.Add(listener);
    }

    public bool RemoveListener(Action listener)
    {
        return _listeners.Remove(listener);
    }
    #endregion Observer Pattern Methods
}