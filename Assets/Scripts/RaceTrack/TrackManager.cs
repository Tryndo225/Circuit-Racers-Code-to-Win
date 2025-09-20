using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;
using System.Collections;

public class TrackManager : MonoBehaviour
{
    public List<CheckPointListener> checkPoints;

    [Header("Input Settings")]
    [SerializeField] private InputActionProperty respawnLastCheckPoint;

    [SerializeField] private InputActionProperty restartRace;
    [SerializeField] private bool defaultBindings = true;

    [Header("Car Prefab Reference")]
    [SerializeField] private GameObject carPrefab;

    [Header("Track Settings")]
    [SerializeField] private bool isCircuit = false;

    [SerializeField, ShowIf(nameof(isCircuit))] private int laps = 3;

    [Header("Respawn Delay")]
    [SerializeField, Range(0f, 5f)] private float respawnDelay = 3f;

    private GameObject _carInstance;

    private int _currentLap = 0;

    private int _currentCheckPointIndex = 0;

    private float _lapStartTime = 0f;

    private float _lastLapTime = 0f;

    private float _trackStartTime = 0f;

    private float _trackEndTime = 0f;

    private bool _isRaceFinished = false;

    private float _lastCheckPointTime = 0f;

    private bool _pendingRespawn = false;

    private int _respawnCheckPoint = 0;

    private float _respawnTimer = 0f;

    public float LastLapTime => _lastLapTime;
    public float CurrentLapTime => Time.time - _lapStartTime;
    public float TotalTrackTime => _isRaceFinished ? _trackEndTime - _trackStartTime : Time.time - _trackStartTime;
    public int CurrentLap => _currentLap;
    public int TotalLaps => laps;
    public int CurrentCheckPointIndex => _currentCheckPointIndex;
    public int TotalCheckPoints => checkPoints.Count;

    public bool IsRaceFinished => _isRaceFinished;
    public float RespawnTimer => _respawnTimer;

    private void OnValidate()
    {
        if (defaultBindings)
        {
            respawnLastCheckPoint = new InputActionProperty(CreateDefaultRespawnBind());
            restartRace = new InputActionProperty(CreateDefaultRestartBind());
        }
    }

    private void OnEnable()
    {
        // Make sure the actions are enabled
        respawnLastCheckPoint.action.Enable();
        restartRace.action.Enable();

        // Subscribe to events
        respawnLastCheckPoint.action.performed += ctx => Respawn();
        restartRace.action.performed += ctx => StartCoroutine(RestartCoroutine());
    }

    private void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        respawnLastCheckPoint.action.performed -= ctx => Respawn();
        restartRace.action.performed -= ctx => StartCoroutine(RestartCoroutine());

        respawnLastCheckPoint.action.Disable();
        restartRace.action.Disable();
    }

    private InputAction CreateDefaultRespawnBind()
    {
        var respawn = new InputAction("Respawn", InputActionType.Button, expectedControlType: "Button");
        respawn.AddBinding("<Keyboard>/backspace");
        respawn.AddBinding("<DualShockGamepad>/triangle");
        respawn.AddBinding("<Gamepad>/buttonNorth");
        return respawn;
    }

    private InputAction CreateDefaultRestartBind()
    {
        var restart = new InputAction("Restart", InputActionType.Button, expectedControlType: "Button");
        restart.AddBinding("<Keyboard>/delete");
        restart.AddBinding("<DualShockGamepad>/start");
        restart.AddBinding("<Gamepad>/start");
        return restart;
    }

    private void Awake()
    {
        if (checkPoints == null || checkPoints.Count == 0)
        {
            Debug.LogError("TrackManager requires at least one CheckPointListener.");
            return;
        }
        foreach (var checkPoint in checkPoints)
        {
            checkPoint.AddListener(CheckPointTaken);
        }
    }

    private void Start()
    {
        StartRace();
    }

    public void StartRace()
    {
        StartCoroutine(RestartCoroutine());
    }

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

    private void FixedUpdate()
    {
        if (_pendingRespawn)
        {
            StartCoroutine(RespawnDelayCoroutine(respawnDelay));
        }
    }

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

    private void ResetCheckPoints()
    {
        foreach (var checkPoint in checkPoints)
        {
            checkPoint.SetActive(false);
        }
        checkPoints[0].SetActive(true);
    }

    private void Restart()
    {
        if (_carInstance != null)
        {
            Destroy(_carInstance);
        }
        _carInstance = Instantiate(carPrefab, transform.position, transform.rotation);
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
                _respawnCheckPoint = checkPoints.Count - 1;
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

    private void RespawnCar(int index)
    {
        var carRb = _carInstance.GetComponent<Rigidbody>();
        if (carRb == null)
        {
            Debug.LogError("Car prefab must have a valid Rigidbody component for Respawning.");
            return;
        }

        _carInstance.transform.position = checkPoints[index].cPClaimedPosition;
        _carInstance.transform.rotation = checkPoints[index].cPClaimedRotation;
        carRb.linearVelocity = checkPoints[index].cPClaimedRbLinearVelocity;
        carRb.angularVelocity = checkPoints[index].cPClaimedRbAngularVelocity;

        _trackStartTime += Time.time - _lastCheckPointTime;
        _lapStartTime += Time.time - _lastCheckPointTime;
        _lastCheckPointTime = Time.time;
    }

    public void CheckPointTaken()
    {
        Debug.Log($"CheckPoint {_currentCheckPointIndex} taken at {Time.time - _trackStartTime} seconds.");
        if (isCircuit && _currentCheckPointIndex == 0)
        {
            if (_currentLap == laps)
            {
                _trackEndTime = Time.time;
                _isRaceFinished = true;
                _lastLapTime = Time.time - _lapStartTime;
                checkPoints[0].SetActive(false);
                Debug.Log("Track finished! Total time: " + TotalTrackTime);
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
        else if (!isCircuit && _currentCheckPointIndex == checkPoints.Count - 1)
        {
            _trackEndTime = Time.time;
            _lastLapTime = Time.time - _lapStartTime;
            _isRaceFinished = true;
            checkPoints[0].SetActive(false);
            return;
        }

        _lastCheckPointTime = Time.time;

        checkPoints[_currentCheckPointIndex].SetActive(false);

        _currentCheckPointIndex = (_currentCheckPointIndex + 1) % checkPoints.Count;
        checkPoints[_currentCheckPointIndex].SetActive(true);
    }
}