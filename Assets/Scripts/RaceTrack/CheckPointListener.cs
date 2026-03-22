using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger-based checkpoint sensor that captures the player's pose and velocities on entry
/// and notifies registered listeners. Intended for lap/track progression and respawn points.
/// </summary>
/// <remarks>
/// @ingroup track_mng
/// @invariant A <see cref="Collider"/> (set to trigger) and a <see cref="Renderer"/> exist on the same GameObject
///            (enforced by attributes; validated in <see cref="GetReferences"/>).
/// @thread Unity main thread only (standard MonoBehaviour lifecycle).
/// @usage
/// - Call <see cref="SetActive(bool)"/> to enable/disable the checkpoint (toggles collider &amp; renderer).
/// - Register callbacks with <see cref="AddListener(Action)"/> to react when the player enters the trigger.
/// - After a claim, read the captured respawn data via <see cref="CPClaimedPosition"/>, <see cref="CPClaimedRotation"/>,
///   <see cref="CPClaimedRbLinearVelocity"/>, and <see cref="CPClaimedRbAngularVelocity"/>.
/// </remarks>
[Serializable]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class CheckPointListener : MonoBehaviour
{
	[Header("Checkpoint Settings")]
	[field: SerializeField] public int CheckpointOrder { get; private set; }

	[Header("Check Point Reference")]
	/// <summary>
	/// Trigger collider used to detect the player crossing the checkpoint.
	/// Auto-cached in <see cref="GetReferences"/>.
	/// </summary>
	[SerializeField, ReadOnly] private Collider checkPointCollider;

	/// <summary>
	/// Visual marker for the checkpoint. Toggled alongside the collider in <see cref="SetActive(bool)"/>.
	/// Auto-cached in <see cref="GetReferences"/>.
	/// </summary>
	[SerializeField, ReadOnly] private Renderer checkPointRenderer;

	/// <summary>
	/// True while the checkpoint is accepting triggers and visible (see <see cref="SetActive(bool)"/>).
	/// </summary>
	private bool _isActive = false;

	/// <summary>
	/// Observer list invoked when the player claims this checkpoint.
	/// </summary>
	private HashSet<Action> _listeners = new HashSet<Action>();

	/// <summary>Captured player position at the moment of trigger entry.</summary>
	public Vector3 CPClaimedPosition { get; private set; }
	/// <summary>Captured player rotation at the moment of trigger entry.</summary>
	public Quaternion CPClaimedRotation { get; private set; }
	/// <summary>Captured player rigidbody linear velocity at the moment of trigger entry.</summary>
	public Vector3 CPClaimedRbLinearVelocity { get; private set; }
	/// <summary>Captured player rigidbody angular velocity at the moment of trigger entry.</summary>
	public Vector3 CPClaimedRbAngularVelocity { get; private set; }

	#region Unity Methods

	/// <summary>
	/// Editor validation hook: caches required references and warns on misconfiguration.
	/// </summary>
	private void OnValidate()
	{
		GetReferences();
	}

	/// <summary>
	/// Unity Awake: caches component references.
	/// </summary>
	private void Awake()
	{
		GetReferences();
	}

	/// <summary>
	/// Unity Start: ensures references are cached.
	/// </summary>
	private void Start()
	{
		GetReferences();
	}
	#endregion Unity Methods

	#region Setup Methods
	/// <summary>
	/// Caches the <see cref="Collider"/> and <see cref="Renderer"/> components and validates
	/// that the collider is set as a trigger.
	/// </summary>
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
	/// Physics callback: when active and a collider tagged <c>Player</c> enters,
	/// captures position, rotation, and velocities, then notifies listeners.
	/// </summary>
	/// <param name="other">Incoming collider.</param>
	private void OnTriggerEnter(Collider other)
	{
		if (_isActive && other.CompareTag("Player"))
		{
			other.GetComponent<Rigidbody>();

			CPClaimedPosition = other.transform.position;
			CPClaimedRotation = other.transform.rotation;

			var playerRigidbody = other.GetComponent<Rigidbody>();

			CPClaimedRbLinearVelocity = playerRigidbody.linearVelocity;
			CPClaimedRbAngularVelocity = playerRigidbody.angularVelocity;

			if (_listeners != null)
			{
				foreach (var listener in _listeners)
				{
					listener?.Invoke();
				}
			}
		}
	}

	/// <summary>
	/// Enables/disables the checkpoint. When disabled, the collider is turned off and the
	/// renderer is hidden; the checkpoint will not claim or notify.
	/// </summary>
	/// <param name="isActive">True to enable; false to disable.</param>
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
	/// Registers a callback to be invoked when this checkpoint is claimed by the player.
	/// </summary>
	/// <param name="listener">Action to invoke on claim.</param>
	public void AddListener(Action listener)
	{
		_listeners.Add(listener);
	}

	/// <summary>
	/// Unregisters a previously added listener.
	/// </summary>
	/// <param name="listener">Action to remove.</param>
	/// <returns>True if the listener was removed; otherwise false.</returns>
	public bool RemoveListener(Action listener)
	{
		return _listeners.Remove(listener);
	}
	#endregion Observer Pattern Methods
}
