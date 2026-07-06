using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages race flow for a track with sequential checkpoints.
/// </summary>
/// <remarks>
/// @ingroup track_mng
/// @brief Spawns the player car, tracks lap/checkpoint progression, handles respawn/restart input, and controls checkpoint activation.
///
/// This manager coordinates the runtime race flow:
/// - Spawns and restarts the player car.
/// - Activates only the next required checkpoint.
/// - Advances checkpoint and lap progress.
/// - Handles point-to-point and circuit race completion.
/// - Respawns the car at the last claimed checkpoint.
/// - Connects race progression to <see cref="RaceTimeManager"/>.
///
/// Threading:
/// - Unity main thread only.
/// - Uses Unity lifecycle methods, input callbacks, physics state, and coroutines.
/// </remarks>
public class TrackManager : Generic.SceneSingleton<TrackManager>
{
	/// <summary>
	/// Optional explicit spawn transform for the car.
	/// </summary>
	/// <remarks>
	/// If this is not assigned, the manager object's own transform is used as the spawn point.
	/// </remarks>
	[Tooltip("Optional explicit spawn transform for the player car. If empty, this object's transform is used.")]
	public Transform CarSpawn = null;

	#region Inspector: Input Settings

	[Header("Input Settings")]
	/// <summary>
	/// Input action used to respawn at the last claimed checkpoint.
	/// </summary>
	/// <remarks>
	/// Default bindings can be created automatically in <see cref="OnValidate"/> when
	/// <see cref="defaultBindings"/> is enabled.
	/// </remarks>
	[Tooltip("Input action used to respawn at the last claimed checkpoint.")]
	[SerializeField] private InputActionProperty respawnLastCheckPoint;

	/// <summary>
	/// Input action used to restart the race.
	/// </summary>
	/// <remarks>
	/// Default bindings can be created automatically in <see cref="OnValidate"/> when
	/// <see cref="defaultBindings"/> is enabled.
	/// </remarks>
	[Tooltip("Input action used to restart the race.")]
	[SerializeField] private InputActionProperty restartRace;

	/// <summary>
	/// Whether default input bindings should be generated in editor validation.
	/// </summary>
	[Tooltip("If enabled, default respawn and restart input bindings are generated during validation.")]
	[SerializeField] private bool defaultBindings = true;

	#endregion

	#region Inspector: Car Prefab

	[Header("Car Prefab Reference")]
	/// <summary>
	/// Player car prefab spawned when the race starts or restarts.
	/// </summary>
	[Tooltip("Player car prefab spawned when the race starts or restarts.")]
	[SerializeField] private GameObject carPrefab;

	/// <summary>
	/// Vertical spawn offset applied relative to the spawn transform.
	/// </summary>
	[Tooltip("Vertical offset applied when spawning the car.")]
	[SerializeField] private float carSpawnVerticalOffset = 0.5f;

	/// <summary>
	/// Forward/backward spawn offset applied in the spawn transform's local forward direction.
	/// </summary>
	/// <remarks>
	/// Negative values place the car behind the spawn point.
	/// </remarks>
	[Tooltip("Forward/backward offset applied when spawning the car. Negative values spawn behind the point.")]
	[SerializeField] private float carSpawnHorizontalOffset = -5f;

	#endregion

	#region Inspector: Track Settings

	[Header("Track Settings")]
	/// <summary>
	/// Level map currently used by this track manager.
	/// </summary>
	[Tooltip("Level map currently used by this track manager.")]
	[SerializeField, ReadOnly] private LevelMap levelMap;

	/// <summary>
	/// Whether the race is a lap-based circuit instead of a point-to-point track.
	/// </summary>
	[Tooltip("If enabled, the race is treated as a lap-based circuit. If disabled, it is point-to-point.")]
	[SerializeField] private bool isCircuit = false;

	/// <summary>
	/// Number of laps required to finish a circuit race.
	/// </summary>
	[Tooltip("Number of laps required to finish a circuit race.")]
	[SerializeField, ShowIf(nameof(isCircuit))] private int laps = 3;


	#endregion

	#region Inspector: Respawn

	[Header("Respawn Delay")]
	/// <summary>
	/// Unscaled delay in seconds used during respawn and restart countdowns.
	/// </summary>
	[Tooltip("Unscaled delay in seconds used during respawn.")]
	[SerializeField, Range(0f, 5f)] private float respawnDelay = 3f;

	#endregion

	#region Private Fields

	/// <summary>
	/// Runtime instance of the spawned player car.
	/// </summary>
	private GameObject _carInstance;

	/// <summary>
	/// Current lap index.
	/// </summary>
	/// <remarks>
	/// The value is one-based once a race has started and zero before the first start/finish pass.
	/// </remarks>
	private int _currentLap = 0;

	/// <summary>
	/// Index of the next checkpoint that must be claimed.
	/// </summary>
	private int _currentCheckPointIndex = 0;

	/// <summary>
	/// Whether the race has finished.
	/// </summary>
	private bool _isRaceFinished = false;

	/// <summary>
	/// Whether a respawn has been requested and is waiting to run.
	/// </summary>
	private bool _pendingRespawn = false;

	/// <summary>
	/// Checkpoint index used as the next respawn target.
	/// </summary>
	private int _respawnCheckPoint = 0;

	/// <summary>
	/// Countdown value shown during restart or respawn.
	/// </summary>
	private float _respawnTimer = 0f;

	/// <summary>
	/// Currently running restart coroutine, if any.
	/// </summary>
	private Coroutine _restartCoroutine;

	#endregion Private Fields

	#region Public Properties

	/// <summary>
	/// Gets the current lap number.
	/// </summary>
	public int CurrentLap => _currentLap;

	/// <summary>
	/// Gets the total number of laps configured for circuit races.
	/// </summary>
	public int TotalLaps => laps;

	/// <summary>
	/// Gets the index of the next required checkpoint.
	/// </summary>
	public int CurrentCheckPointIndex => _currentCheckPointIndex;

	/// <summary>
	/// Gets whether the race has finished.
	/// </summary>
	public bool IsRaceFinished => _isRaceFinished;

	/// <summary>
	/// Gets the current visible respawn/restart countdown value.
	/// </summary>
	public float RespawnTimer => _respawnTimer;

	#endregion Public Properties

	#region Unity Methods

	/// <summary>
	/// Unity editor validation hook that optionally assigns default input bindings.
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
	/// Enables input actions and subscribes to respawn/restart callbacks.
	/// </summary>
	private void OnEnable()
	{
		respawnLastCheckPoint.action.Enable();
		restartRace.action.Enable();

		respawnLastCheckPoint.action.performed += OnRespawnPerformed;
		restartRace.action.performed += OnRestartPerformed;
	}

	/// <summary>
	/// Unsubscribes input callbacks and disables input actions.
	/// </summary>
	private void OnDisable()
	{
		respawnLastCheckPoint.action.performed -= OnRespawnPerformed;
		restartRace.action.performed -= OnRestartPerformed;

		respawnLastCheckPoint.action.Disable();
		restartRace.action.Disable();
	}

	/// <summary>
	/// Handles the respawn input action.
	/// </summary>
	/// <param name="context">Input callback context.</param>
	private void OnRespawnPerformed(InputAction.CallbackContext context)
	{
		Respawn();
	}

	/// <summary>
	/// Handles the restart input action.
	/// </summary>
	/// <param name="context">Input callback context.</param>
	private void OnRestartPerformed(InputAction.CallbackContext context)
	{
		StartRestartCountdown();
	}

	/// <summary>
	/// Initializes scene checkpoints and starts a race when no runtime track placer is present.
	/// </summary>
	/// <remarks>
	/// If a <see cref="RaceTrackPlacer"/> exists, track setup is handled by the generated-track flow
	/// and this method returns early. Otherwise, the currently selected level map is cleared,
	/// existing scene checkpoints are collected automatically, and a race is started from the
	/// scene-defined checkpoint layout.
	/// </remarks>
	private void Start()
	{
		if (FindFirstObjectByType<RaceTrackPlacer>() != null)
		{
			return;
		}

		GameDataManager.Instance.UnselectLevelMap();

		CheckPointManager.Instance.ClearCheckPoints();
		CheckPointManager.Instance.AutoAddCheckpoints();

		StartRace(null);
	}

	/// <summary>
	/// Starts the respawn coroutine when a respawn request is pending.
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
	/// Creates the default respawn input action.
	/// </summary>
	/// <returns>Input action with keyboard and gamepad respawn bindings.</returns>
	private InputAction CreateDefaultRespawnBind()
	{
		var respawn = new InputAction("Respawn", InputActionType.Button, expectedControlType: "Button");
		respawn.AddBinding("<Keyboard>/backspace");
		respawn.AddBinding("<DualShockGamepad>/triangle");
		respawn.AddBinding("<Gamepad>/buttonNorth");
		return respawn;
	}

	/// <summary>
	/// Creates the default restart input action.
	/// </summary>
	/// <returns>Input action with keyboard and gamepad restart bindings.</returns>
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
	/// Restarts the race with a short unscaled countdown.
	/// </summary>
	/// <returns>Coroutine enumerator.</returns>
	/// <remarks>
	/// Gameplay time is paused while the restart countdown is active.
	/// </remarks>
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
	/// Respawns the car after an unscaled countdown.
	/// </summary>
	/// <param name="delay">Unscaled delay in seconds.</param>
	/// <returns>Coroutine enumerator.</returns>
	/// <remarks>
	/// Gameplay time is paused while the respawn countdown is active. The car is first moved to the
	/// saved checkpoint pose and then gameplay resumes after the delay.
	/// </remarks>
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

	/// <summary>
	/// Starts or restarts the restart countdown coroutine.
	/// </summary>
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
	/// Restarts the race state by replacing the car, resetting progress, starting timing, and resetting checkpoints.
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
	/// Requests a respawn at the previous checkpoint or starts a full restart when no checkpoint can be used.
	/// </summary>
	/// <remarks>
	/// Finished races restart instead of respawning. In circuit mode, when the current checkpoint index wraps to zero,
	/// the previous checkpoint is the final checkpoint of the lap.
	/// </remarks>
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
	/// Warps the car to the saved pose and velocities captured by a checkpoint.
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
	/// Starts race setup from a provided level map or from existing scene checkpoint data.
	/// </summary>
	/// <param name="lvlMap">Optional level map used to configure laps and circuit mode.</param>
	/// <remarks>
	/// If a level map is provided, its lap count and circuit flag are copied into this manager.
	/// The method requires at least one checkpoint in <see cref="CheckPointManager"/> and registers
	/// <see cref="CheckPointTaken"/> as a checkpoint listener before starting the restart countdown.
	/// </remarks>
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

		StartRestartCountdown();
	}

	/// <summary>
	/// Deactivates all checkpoints and activates the first checkpoint.
	/// </summary>
	private void ResetCheckPoints()
	{
		CheckPointManager.Instance.DeactivateAllCheckpoints();
		CheckPointManager.Instance.ActivateCheckpoint(0);
	}

	/// <summary>
	/// Handles the currently active checkpoint being claimed.
	/// </summary>
	/// <remarks>
	/// The method advances checkpoint progression, handles lap transitions in circuit mode,
	/// finishes the race when the required final condition is met, and activates the next required checkpoint.
	/// </remarks>
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

	/// <summary>
	/// Gets the follow camera component from the main camera.
	/// </summary>
	/// <returns>The <see cref="FollowCamera"/> on the main camera, or <c>null</c> if unavailable.</returns>
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