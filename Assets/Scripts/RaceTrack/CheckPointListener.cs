using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger-based checkpoint sensor that records the player's pose and velocities when entered.
/// </summary>
/// <remarks>
/// @ingroup track_mng
/// @brief Detects when the player crosses this checkpoint and notifies registered listeners.
///
/// This component represents one checkpoint trigger in the race track. When active, it waits for a
/// collider attached to a rigidbody tagged <c>Player</c>. On entry, it stores the player's position,
/// rotation, linear velocity, and angular velocity, then invokes all registered listeners.
///
/// Requirements:
/// - A <see cref="Collider"/> must be present on the same GameObject.
/// - The collider should be configured as a trigger.
/// - A <see cref="Renderer"/> must be present on the same GameObject.
/// - The player rigidbody must be tagged <c>Player</c>.
///
/// Usage:
/// - Call <see cref="SetActive(bool)"/> to enable or disable this checkpoint.
/// - Register callbacks with <see cref="AddListener(Action)"/>.
/// - Read the captured values from <see cref="CPClaimedPosition"/>, <see cref="CPClaimedRotation"/>,
///   <see cref="CPClaimedRbLinearVelocity"/>, and <see cref="CPClaimedRbAngularVelocity"/> after the checkpoint is claimed.
///
/// Threading:
/// - Unity main thread only.
/// </remarks>
[Serializable]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class CheckPointListener : MonoBehaviour
{
	[Header("Checkpoint Settings")]
	/// <summary>
	/// Order of this checkpoint within the track progression.
	/// </summary>
	[field: Tooltip("Order of this checkpoint within the track progression.")]
	[field: SerializeField] public int CheckpointOrder { get; set; }

	[Header("Checkpoint Reference")]
	/// <summary>
	/// Trigger collider used to detect the player crossing the checkpoint.
	/// </summary>
	/// <remarks>
	/// This reference is cached by <see cref="GetReferences"/>.
	/// </remarks>
	[Tooltip("Trigger collider used to detect the player crossing this checkpoint.")]
	[SerializeField, ReadOnly] private Collider checkPointCollider;

	/// <summary>
	/// Renderer used as the visual marker for this checkpoint.
	/// </summary>
	/// <remarks>
	/// This reference is cached by <see cref="GetReferences"/> and toggled together with
	/// <see cref="checkPointCollider"/> in <see cref="SetActive(bool)"/>.
	/// </remarks>
	[Tooltip("Renderer used as the visible checkpoint marker.")]
	[SerializeField, ReadOnly] private Renderer checkPointRenderer;

	/// <summary>
	/// Whether this checkpoint currently accepts trigger events and displays its renderer.
	/// </summary>
	private bool _isActive = false;

	/// <summary>
	/// Registered callbacks invoked when this checkpoint is claimed by the player.
	/// </summary>
	private HashSet<Action> _listeners = new HashSet<Action>();

	/// <summary>
	/// Captured player position at the moment of trigger entry.
	/// </summary>
	public Vector3 CPClaimedPosition { get; private set; }

	/// <summary>
	/// Captured player rotation at the moment of trigger entry.
	/// </summary>
	public Quaternion CPClaimedRotation { get; private set; }

	/// <summary>
	/// Captured player rigidbody linear velocity at the moment of trigger entry.
	/// </summary>
	public Vector3 CPClaimedRbLinearVelocity { get; private set; }

	/// <summary>
	/// Captured player rigidbody angular velocity at the moment of trigger entry.
	/// </summary>
	public Vector3 CPClaimedRbAngularVelocity { get; private set; }

	/// <summary>
	/// Temporary listener array used to invoke callbacks safely even if the listener set changes during invocation.
	/// </summary>
	private Action[] _listenerBuffer;

	#region Unity Methods

	/// <summary>
	/// Unity editor validation hook that caches required references and warns about missing or misconfigured components.
	/// </summary>
	private void OnValidate()
	{
		GetReferences();
	}

	/// <summary>
	/// Unity initialization hook that caches required component references.
	/// </summary>
	private void Awake()
	{
		GetReferences();
	}

	/// <summary>
	/// Unity start hook that ensures required component references are cached.
	/// </summary>
	private void Start()
	{
		GetReferences();
	}
	#endregion Unity Methods

	#region Setup Methods
	/// <summary>
	/// Caches the <see cref="Collider"/> and <see cref="Renderer"/> components and validates trigger setup.
	/// </summary>
	/// <remarks>
	/// Missing required components are reported as errors. A non-trigger collider is reported as a warning
	/// because checkpoint detection depends on trigger entry events.
	/// </remarks>
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

	/// <summary>
	/// Handles trigger entry and claims the checkpoint when the entering rigidbody is tagged as the player.
	/// </summary>
	/// <param name="other">Incoming collider that entered this checkpoint trigger.</param>
	/// <remarks>
	/// When the checkpoint is inactive, the trigger is ignored. When the checkpoint is active and the entering
	/// rigidbody is tagged <c>Player</c>, the player's pose and velocities are captured and listeners are notified.
	/// </remarks>
	private void OnTriggerEnter(Collider other)
	{
		if (!_isActive)
		{
			return;
		}

		Rigidbody playerRigidbody = other.attachedRigidbody;

		if (playerRigidbody == null || !playerRigidbody.CompareTag("Player"))
		{
			return;
		}

		Transform playerTransform = playerRigidbody.transform;

		CPClaimedPosition = playerTransform.position;
		CPClaimedRotation = playerTransform.rotation;
		CPClaimedRbLinearVelocity = playerRigidbody.linearVelocity;
		CPClaimedRbAngularVelocity = playerRigidbody.angularVelocity;

		NotifyListeners();
	}

	/// <summary>
	/// Invokes all registered checkpoint listeners.
	/// </summary>
	/// <remarks>
	/// A copied listener buffer is used so callbacks may add or remove listeners without modifying the
	/// collection while it is being enumerated.
	/// </remarks>
	private void NotifyListeners()
	{
		if (_listeners == null || _listeners.Count == 0)
		{
			return;
		}

		if (_listenerBuffer == null || _listenerBuffer.Length < _listeners.Count)
		{
			_listenerBuffer = new Action[_listeners.Count];
		}

		_listeners.CopyTo(_listenerBuffer);

		int listenerCount = _listeners.Count;

		for (int i = 0; i < listenerCount; i++)
		{
			_listenerBuffer[i]?.Invoke();
			_listenerBuffer[i] = null;
		}
	}

	/// <summary>
	/// Enables or disables this checkpoint.
	/// </summary>
	/// <param name="isActive">Whether the checkpoint should accept trigger events and be visible.</param>
	/// <remarks>
	/// The checkpoint collider and renderer are toggled together. Disabled checkpoints do not claim the player.
	/// </remarks>
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
	/// <summary>
	/// Registers a callback to invoke when this checkpoint is claimed by the player.
	/// </summary>
	/// <param name="listener">Callback invoked on checkpoint claim.</param>
	public void AddListener(Action listener)
	{
		_listeners.Add(listener);
	}

	/// <summary>
	/// Unregisters a previously added checkpoint callback.
	/// </summary>
	/// <param name="listener">Callback to remove.</param>
	/// <returns><c>true</c> if the callback was removed; otherwise <c>false</c>.</returns>
	public bool RemoveListener(Action listener)
	{
		return _listeners.Remove(listener);
	}
	#endregion Observer Pattern Methods
}