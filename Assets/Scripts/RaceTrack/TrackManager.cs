using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages race flow for a track with sequential checkpoints: spawns the player car,
/// tracks laps and times, handles respawn/restart inputs, and advances checkpoint state.
/// </summary>
/// <remarks>
/// @ingroup track_mgr
/// @invariant CheckPoints are ordered as the race path; index 0 is the start/finish for circuits.
/// @invariant If carPrefab is assigned, the spawned instance must include a Rigidbody.
/// @invariant When circuit mode is enabled (isCircuit), laps >= 1.
/// @thread Unity main thread (lifecycle, physics callbacks, coroutines).
/// </remarks>
public class TrackManager : Generic.SceneSingleton<TrackManager>
{


	/// <summary>
	/// Optional explicit spawn transform for the car; if null, this object's transform is used.
	/// </summary>
	public Transform CarSpawn = null;

	#region Inspector: Input Settings

	[Header("Input Settings")]
	/// <summary>
	/// Input action to respawn at the last checkpoint (or restart if none).
	/// Default bindings can be auto-created in OnValidate.
	/// </summary>
	[SerializeField] private InputActionProperty respawnLastCheckPoint;

	/// <summary>
	/// Input action to restart the race. Default bindings can be auto-created.
	/// </summary>
	[SerializeField] private InputActionProperty restartRace;

	/// <summary>
	/// When true, OnValidate replaces actions with generated defaults.
	/// </summary>
	[SerializeField] private bool defaultBindings = true;

	#endregion

	#region Inspector: Car Prefab

	[Header("Car Prefab Reference")]
	/// <summary>
	/// Player car prefab to spawn at race start/restart.
	/// </summary>
	[SerializeField] private GameObject carPrefab;

	/// <summary>
	/// Vertical offset applied on spawn relative to the spawn transform (meters).
	/// </summary>
	[SerializeField] private float carSpawnVerticalOffset = 0.5f;

	/// <summary>
	/// Forward/backward offset applied on spawn in local forward (meters). Negative spawns behind the point.
	/// </summary>
	[SerializeField] private float carSpawnHorizontalOffset = -5f;

	#endregion

	#region Inspector: Track Settings

	[Header("Track Settings")]
	[SerializeField, ReadOnly] private LevelMap levelMap;
	/// <summary>
	/// True for lap-based circuit; false for point-to-point (finish at last checkpoint).
	/// </summary>
	[SerializeField] private bool isCircuit = false;

	/// <summary>
	/// Number of laps to complete (circuit mode only).
	/// </summary>
	[SerializeField, ShowIf(nameof(isCircuit))] private int laps = 3;


	#endregion

	#region Inspector: Respawn

	[Header("Respawn Delay")]
	/// <summary>
	/// Unscaled delay (seconds) shown while pausing during respawn.
	/// </summary>
	[SerializeField, Range(0f, 5f)] private float respawnDelay = 3f;

	#endregion

	#region Private Fields

	/// <summary>Runtime instance of the spawned car.</summary>
	private GameObject _carInstance;

	/// <summary>Current lap index (1-based during race; 0 before the first pass).</summary>
	private int _currentLap = 0;

	/// <summary>Index of the next required checkpoint.</summary>
	private int _currentCheckPointIndex = 0;

	/// <summary>True once the race has finished.</summary>
	private bool _isRaceFinished = false;

	/// <summary>True when a respawn has been requested and is pending the coroutine.</summary>
	private bool _pendingRespawn = false;

	/// <summary>Checkpoint index to respawn at.</summary>
	private int _respawnCheckPoint = 0;

	/// <summary>Countdown (unscaled seconds) shown during restart/respawn.</summary>
	private float _respawnTimer = 0f;

	private Coroutine _restartCoroutine;

	#endregion Private Fields

	#region Public Properties

	/// <summary>Current lap number (1-based once started).</summary>
	public int CurrentLap => _currentLap;

	/// <summary>Total laps configured for circuit races.</summary>
	public int TotalLaps => laps;

	/// <summary>Index of the next required checkpoint.</summary>
	public int CurrentCheckPointIndex => _currentCheckPointIndex;

	/// <summary>True after the race has finished.</summary>
	public bool IsRaceFinished => _isRaceFinished;

	/// <summary>Current visible countdown (unscaled seconds) used by respawn/restart flows.</summary>
	public float RespawnTimer => _respawnTimer;

	#endregion Public Properties

	#region Unity Methods

	/// <summary>
	/// Editor-time validation: optionally assigns default input bindings for respawn and restart.
	/// </summary>
	private void OnValidate()
	{
		if (defaultBindings)
		{
			respawnLastCheckPoint = new InputActionProperty(CreateDefaultRespawnBind());
			restartRace = new InputActionProperty(CreateDefaultRestartBind());
		}
	}

	/// <summary>
	/// Enables input actions and subscribes to performed callbacks for respawn and restart.
	/// </summary>
	private void OnEnable()
	{
		respawnLastCheckPoint.action.Enable();
		restartRace.action.Enable();

		respawnLastCheckPoint.action.performed += OnRespawnPerformed;
		restartRace.action.performed += OnRestartPerformed;
	}

	/// <summary>
	/// Unsubscribes input callbacks and disables actions to avoid leaks when disabled.
	/// </summary>
	private void OnDisable()
	{
		respawnLastCheckPoint.action.performed -= OnRespawnPerformed;
		restartRace.action.performed -= OnRestartPerformed;

		respawnLastCheckPoint.action.Disable();
		restartRace.action.Disable();
	}

	private void OnRespawnPerformed(InputAction.CallbackContext context)
	{
		Respawn();
	}

	private void OnRestartPerformed(InputAction.CallbackContext context)
	{
		StartRestartCountdown();
	}

	/// <summary>
	/// Starts the race automatically if at least one checkpoint is assigned.
	/// </summary>
	private void Start()
	{
		if (FindFirstObjectByType<RaceTrackPlacer>() != null)
		{
			return;
		}

		CheckPointManager.Instance.ClearCheckPoints();
		CheckPointManager.Instance.AutoAddCheckpoints();

		StartRace(null);
	}

	/// <summary>
	/// Physics loop: triggers the respawn coroutine when a respawn is pending.
	/// </summary>
	private void FixedUpdate()
	{
		if (_pendingRespawn)
		{
			StartCoroutine(RespawnDelayCoroutine(respawnDelay));
		}
	}

	#endregion Unity Methods

	#region Default Input Bindings

	/// <summary>
	/// Creates a default respawn action with keyboard and gamepad bindings.
	/// </summary>
	private InputAction CreateDefaultRespawnBind()
	{
		var respawn = new InputAction("Respawn", InputActionType.Button, expectedControlType: "Button");
		respawn.AddBinding("<Keyboard>/backspace");
		respawn.AddBinding("<DualShockGamepad>/triangle");
		respawn.AddBinding("<Gamepad>/buttonNorth");
		return respawn;
	}

	/// <summary>
	/// Creates a default restart action with keyboard and gamepad bindings.
	/// </summary>
	private InputAction CreateDefaultRestartBind()
	{
		var restart = new InputAction("Restart", InputActionType.Button, expectedControlType: "Button");
		restart.AddBinding("<Keyboard>/delete");
		restart.AddBinding("<DualShockGamepad>/start");
		restart.AddBinding("<Gamepad>/start");
		return restart;
	}

	#endregion Default Input Bindings

	#region Coroutines

	/// <summary>
	/// Restarts the race with a short unscaled countdown, pausing gameplay during the delay.
	/// </summary>
	private IEnumerator RestartCoroutine()
	{
		Restart();
		Time.timeScale = 0f;

		float startTime = Time.unscaledTime;
		while (Time.unscaledTime - startTime < 3f)
		{
			_respawnTimer = 3f - (Time.unscaledTime - startTime);
			Debug.Log($"[TrackManager] Restarting in {_respawnTimer:0.0} seconds...");
			yield return new WaitForSecondsRealtime(0.5f);
		}

		_respawnTimer = 0f;
		Time.timeScale = 1f;
	}

	/// <summary>
	/// Respawns the car at the chosen checkpoint after an unscaled delay, pausing during the countdown.
	/// </summary>
	/// <param name="delay">Unscaled seconds to wait.</param>
	private IEnumerator RespawnDelayCoroutine(float delay)
	{
		if (_carInstance == null)
		{
			Debug.LogError("[TrackManager] Cannot respawn; car instance is missing.");
			_pendingRespawn = false;
			yield break;
		}

		_pendingRespawn = false;

		var carRb = _carInstance.GetComponent<Rigidbody>();
		var oldInterpolation = carRb.interpolation;

		carRb.interpolation = RigidbodyInterpolation.None;

		bool oldKinematic = carRb.isKinematic;
		carRb.isKinematic = false;

		RespawnCar(_respawnCheckPoint);

		Physics.SyncTransforms();
		carRb.isKinematic = oldKinematic;
		carRb.interpolation = oldInterpolation;

		FollowCamera followCamera = GetFollowCamera();

		if (followCamera != null)
		{
			followCamera.SyncCamera();
		}

		Time.timeScale = 0f;

		float startTime = Time.unscaledTime;

		while (Time.unscaledTime - startTime < delay)
		{
			_respawnTimer = delay - (Time.unscaledTime - startTime);
			yield return new WaitForSecondsRealtime(0.5f);
		}

		_respawnTimer = 0f;

		Time.timeScale = 1f;
	}

	private void StartRestartCountdown()
	{
		if (_restartCoroutine != null)
		{
			StopCoroutine(_restartCoroutine);
		}

		_restartCoroutine = StartCoroutine(RestartCoroutine());
	}

	#endregion Coroutines

	#region Restart and Respawn Methods

	/// <summary>
	/// Destroys any existing car, spawns a new one at the spawn transform (or manager transform),
	/// resets race timing and checkpoint activation.
	/// </summary>
	private void Restart()
	{
		if (_carInstance != null)
		{
			Destroy(_carInstance);
		}
		Vector3 _carStartPosition;
		Quaternion _carStartRotation;
		if (CarSpawn != null)
		{
			_carStartPosition = CarSpawn.position;
			_carStartRotation = CarSpawn.rotation;
		}
		else
		{
			Debug.LogWarning("[TrackManager] Car spawn point not assigned, using TrackManager position.");
			_carStartPosition = transform.position;
			_carStartRotation = transform.rotation;
		}
		_carInstance = Instantiate(carPrefab, _carStartPosition + (_carStartRotation * Vector3.forward * carSpawnHorizontalOffset) + (Vector3.up * carSpawnVerticalOffset), _carStartRotation);
		_carInstance.tag = "Player";

		FollowCamera followCamera = GetFollowCamera();

		if (followCamera != null)
		{
			followCamera.SetTarget(_carInstance.transform);
		}

		_currentLap = 0;
		_currentCheckPointIndex = 0;
		_isRaceFinished = false;

		RaceTimeManager.Instance.RaceStart();

		ResetCheckPoints();
	}

	/// <summary>
	/// Requests a respawn at the previous checkpoint (or restart if before the first checkpoint),
	/// or restarts immediately if the race is finished.
	/// </summary>
	private void Respawn()
	{
		if (_isRaceFinished)
		{
			StartRestartCountdown();
			return;
		}
		if (_currentCheckPointIndex == 0)
		{
			if (isCircuit && _currentLap > 0)
			{
				_respawnCheckPoint = CheckPointManager.Instance.TotalCheckpoints - 1;
				_pendingRespawn = true;
			}
			else
			{
				StartRestartCountdown();
			}
		}
		else
		{
			_respawnCheckPoint = _currentCheckPointIndex - 1;
			_pendingRespawn = true;
		}
	}

	/// <summary>
	/// Warps the car to a checkpoint's saved pose and velocities, adjusts timers to exclude time since last checkpoint.
	/// </summary>
	/// <param name="index">Checkpoint index to respawn at.</param>
	private void RespawnCar(int index)
	{
		var carRb = _carInstance.GetComponent<Rigidbody>();
		if (carRb == null)
		{
			Debug.LogError("[TrackManager] Car prefab must have a valid Rigidbody component for Respawning.");
			return;
		}

		_carInstance.transform.position = CheckPointManager.Instance.CheckPoints[index].CPClaimedPosition;
		_carInstance.transform.rotation = CheckPointManager.Instance.CheckPoints[index].CPClaimedRotation;
		carRb.linearVelocity = CheckPointManager.Instance.CheckPoints[index].CPClaimedRbLinearVelocity;
		carRb.angularVelocity = CheckPointManager.Instance.CheckPoints[index].CPClaimedRbAngularVelocity;
	}

	#endregion Restart and Respawn Methods

	/// <summary>
	/// Initializes the race: subscribes to checkpoint events and starts the restart countdown.
	/// </summary>
	public void StartRace(LevelMap lvlMap)
	{
		if (lvlMap != null)
		{
			levelMap = lvlMap;

			laps = levelMap.Laps;

			isCircuit = levelMap.Circuit;
		}

		if (CheckPointManager.Instance.TotalCheckpoints == 0)
		{
			Debug.LogError("[TrackManager] No checkpoints assigned to TrackManager.");
			return;
		}

		CheckPointManager.Instance.AddListenerToCheckpoints(CheckPointTaken);

		var checkpointParent = CheckPointManager.Instance.CheckPoints[0].GetComponentInParent<Transform>();
		StartRestartCountdown();
	}

	/// <summary>
	/// Deactivates all checkpoints and activates the first one (start).
	/// </summary>
	private void ResetCheckPoints()
	{
		CheckPointManager.Instance.DeactivateAllCheckpoints();
		CheckPointManager.Instance.ActivateCheckpoint(0);
	}

	/// <summary>
	/// Callback when the current checkpoint is taken: advances the checkpoint index,
	/// updates lap or finish logic, and records times.
	/// </summary>
	public void CheckPointTaken()
	{
		Debug.Log($"[TrackManager] CheckPoint {_currentCheckPointIndex} taken at {RaceTimeManager.Instance.GetCurrentRaceTime()} seconds.");
		if (isCircuit && _currentCheckPointIndex == 0)
		{
			if (_currentLap == laps)
			{
				_isRaceFinished = true;
				RaceTimeManager.Instance.RaceEnd();
				CheckPointManager.Instance.DeactivateAllCheckpoints();
				return;
			}
			if (_currentLap != 0)
				RaceTimeManager.Instance.LapPassed();

			_currentLap++;
		}
		else if (!isCircuit && _currentCheckPointIndex == CheckPointManager.Instance.TotalCheckpoints - 1)
		{
			RaceTimeManager.Instance.RaceEnd();
			CheckPointManager.Instance.DeactivateAllCheckpoints();
			_isRaceFinished = true;
			return;
		}

		CheckPointManager.Instance.DeactivateCheckpoint(_currentCheckPointIndex);
		_currentCheckPointIndex = (_currentCheckPointIndex + 1) % CheckPointManager.Instance.TotalCheckpoints;
		CheckPointManager.Instance.ActivateCheckpoint(_currentCheckPointIndex);
	}

	private FollowCamera GetFollowCamera()
	{
		Camera mainCamera = Camera.main;

		if (mainCamera == null)
		{
			Debug.LogError("[TrackManager] No MainCamera found.");
			return null;
		}

		if (!mainCamera.TryGetComponent(out FollowCamera followCamera))
		{
			Debug.LogError("[TrackManager] MainCamera has no FollowCamera component.");
			return null;
		}

		return followCamera;
	}
}
