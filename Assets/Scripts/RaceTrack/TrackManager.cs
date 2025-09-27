using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;
using System.Collections;

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
public class TrackManager : MonoBehaviour
{
    /// <summary>
    /// Ordered list of checkpoints forming the track. Index 0 is the start (and finish if circuit).
    /// </summary>
    public List<CheckPointListener> CheckPoints;

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

    /// <summary>Start time of the current lap (scaled time).</summary>
    private float _lapStartTime = 0f;

    /// <summary>Time of the most recently completed lap (seconds).</summary>
    private float _lastLapTime = 0f;

    /// <summary>Overall race start time (scaled time).</summary>
    private float _trackStartTime = 0f;

    /// <summary>Overall race end time (scaled time), set on finish.</summary>
    private float _trackEndTime = 0f;

    /// <summary>True once the race has finished.</summary>
    private bool _isRaceFinished = false;

    /// <summary>Timestamp at which the last checkpoint was taken (scaled time).</summary>
    private float _lastCheckPointTime = 0f;

    /// <summary>True when a respawn has been requested and is pending the coroutine.</summary>
    private bool _pendingRespawn = false;

    /// <summary>Checkpoint index to respawn at.</summary>
    private int _respawnCheckPoint = 0;

    /// <summary>Countdown (unscaled seconds) shown during restart/respawn.</summary>
    private float _respawnTimer = 0f;

    #endregion Private Fields

    #region Public Properties

    /// <summary>Duration of the most recently completed lap (seconds).</summary>
    public float LastLapTime => _lastLapTime;

    /// <summary>Elapsed time of the current lap (seconds).</summary>
    public float CurrentLapTime => Time.time - _lapStartTime;

    /// <summary>
    /// Total elapsed time for the track (seconds). Uses end time once finished.
    /// </summary>
    public float TotalTrackTime => _isRaceFinished ? _trackEndTime - _trackStartTime : Time.time - _trackStartTime;

    /// <summary>Current lap number (1-based once started).</summary>
    public int CurrentLap => _currentLap;

    /// <summary>Total laps configured for circuit races.</summary>
    public int TotalLaps => laps;

    /// <summary>Index of the next required checkpoint.</summary>
    public int CurrentCheckPointIndex => _currentCheckPointIndex;

    /// <summary>Total number of checkpoints on this track.</summary>
    public int TotalCheckPoints => CheckPoints.Count;

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

        respawnLastCheckPoint.action.performed += ctx => Respawn();
        restartRace.action.performed += ctx => StartCoroutine(RestartCoroutine());
    }

    /// <summary>
    /// Unsubscribes input callbacks and disables actions to avoid leaks when disabled.
    /// </summary>
    private void OnDisable()
    {
        respawnLastCheckPoint.action.performed -= ctx => Respawn();
        restartRace.action.performed -= ctx => StartCoroutine(RestartCoroutine());

        respawnLastCheckPoint.action.Disable();
        restartRace.action.Disable();
    }

    /// <summary>Initialization hook (unused).</summary>
    private void Awake() { }

    /// <summary>
    /// Starts the race automatically if at least one checkpoint is assigned.
    /// </summary>
    private void Start()
    {
        if (CheckPoints.Count != 0)
        {
            StartRace();
        }
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
            Debug.Log($"Restarting in {_respawnTimer:0.0} seconds...");
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
        Camera.main.GetComponent<FollowCamera>().SyncCamera();

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
            Debug.LogWarning("Car spawn point not assigned, using TrackManager position.");
            _carStartPosition = transform.position;
            _carStartRotation = transform.rotation;
        }
        _carInstance = Instantiate(
            carPrefab,
            _carStartPosition + (_carStartRotation * Vector3.forward * carSpawnHorizontalOffset) + (Vector3.up * carSpawnVerticalOffset),
            _carStartRotation);
        _carInstance.tag = "Player";
        Camera.main.GetComponent<FollowCamera>().target = _carInstance.transform;

        Camera.main.GetComponent<FollowCamera>().SyncCamera();

        _trackStartTime = Time.time;
        _currentLap = 0;
        _currentCheckPointIndex = 0;
        _lapStartTime = Time.time;
        _lastLapTime = 0f;
        _lastCheckPointTime = 0f;
        _isRaceFinished = false;

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
            StartCoroutine(RestartCoroutine());
            return;
        }
        if (_currentCheckPointIndex == 0)
        {
            if (isCircuit && _currentLap > 0)
            {
                _respawnCheckPoint = CheckPoints.Count - 1;
                _pendingRespawn = true;
            }
            else
            {
                StartCoroutine(RestartCoroutine());
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
            Debug.LogError("Car prefab must have a valid Rigidbody component for Respawning.");
            return;
        }

        _carInstance.transform.position = CheckPoints[index].cPClaimedPosition;
        _carInstance.transform.rotation = CheckPoints[index].cPClaimedRotation;
        carRb.linearVelocity = CheckPoints[index].cPClaimedRbLinearVelocity;
        carRb.angularVelocity = CheckPoints[index].cPClaimedRbAngularVelocity;

        _trackStartTime += Time.time - _lastCheckPointTime;
        _lapStartTime += Time.time - _lastCheckPointTime;
        _lastCheckPointTime = Time.time;
    }

    #endregion Restart and Respawn Methods

    /// <summary>
    /// Initializes the race: subscribes to checkpoint events and starts the restart countdown.
    /// </summary>
    /// <param name="carPosition">Optional explicit car position (unused in current flow).</param>
    public void StartRace(Vector3? carPosition = null)
    {
        if (CheckPoints.Count == 0)
        {
            Debug.LogError("No checkpoints assigned to TrackManager.");
            return;
        }

        foreach (var checkPoint in CheckPoints)
        {
            checkPoint.RemoveListener(CheckPointTaken);
            checkPoint.AddListener(CheckPointTaken);
        }

        var checkpointParent = CheckPoints[0].GetComponentInParent<Transform>();
        StartCoroutine(RestartCoroutine());
    }

    /// <summary>
    /// Deactivates all checkpoints and activates the first one (start).
    /// </summary>
    private void ResetCheckPoints()
    {
        foreach (var checkPoint in CheckPoints)
        {
            checkPoint.SetActive(false);
        }
        CheckPoints[0].SetActive(true);
    }

    /// <summary>
    /// Callback when the current checkpoint is taken: advances the checkpoint index,
    /// updates lap or finish logic, and records times.
    /// </summary>
    public void CheckPointTaken()
    {
        Debug.Log($"CheckPoint {_currentCheckPointIndex} taken at {Time.time - _trackStartTime} seconds. At position {CheckPoints[_currentCheckPointIndex].cPClaimedPosition}");
        if (isCircuit && _currentCheckPointIndex == 0)
        {
            if (_currentLap == laps)
            {
                _trackEndTime = Time.time;
                _isRaceFinished = true;
                _lastLapTime = Time.time - _lapStartTime;
                CheckPoints[0].SetActive(false);
                Debug.Log("Track finished! Total time: " + TotalTrackTime);
                GameDataManager.Instance.CompleteLevel(TotalTrackTime);
                return;
            }

            if (_currentLap > 0)
            {
                _lastLapTime = Time.time - _lapStartTime;
                Debug.Log($"Lap {_currentLap} completed in {_lastLapTime} seconds.");
            }

            _lapStartTime = Time.time;
            _currentLap++;
        }
        else if (!isCircuit && _currentCheckPointIndex == CheckPoints.Count - 1)
        {
            _trackEndTime = Time.time;
            _lastLapTime = Time.time - _lapStartTime;
            _isRaceFinished = true;
            CheckPoints[0].SetActive(false);
            return;
        }

        _lastCheckPointTime = Time.time;

        CheckPoints[_currentCheckPointIndex].SetActive(false);

        _currentCheckPointIndex = (_currentCheckPointIndex + 1) % CheckPoints.Count;
        CheckPoints[_currentCheckPointIndex].SetActive(true);
    }
}
