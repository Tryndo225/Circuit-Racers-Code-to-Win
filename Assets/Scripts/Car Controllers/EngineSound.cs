using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class EngineSound : MonoBehaviour
{
    [Header("Inputs")]
    [Tooltip("Current engine speed in RPM.")]
    public float RPM;

    [Range(0f, 1f), Tooltip("Driver demand/load")]
    public float throttle;

    [Header("RPM Range")]
    public float minRPM;

    public float maxRPM;

    [Header("Main Output")]
    public AudioMixerGroup outputGroup;

    [Range(0f, 1f)] public float masterVolume;
    [Range(0f, 1f)] public float spatialBlend;
    [Range(0f, 5f)] public float dopplerLevel;

    [Header("Clips (On-Throttle)")]
    public AudioClip on_Idle;

    public AudioClip on_Low;
    public AudioClip on_Mid;
    public AudioClip on_High;

    [Header("Clips (Off-Throttle)")]
    public AudioClip off_Idle;

    public AudioClip off_Low;
    public AudioClip off_Mid;
    public AudioClip off_High;

    [Header("Smoothing")]
    [Tooltip("Higher = Faster response")]
    public float rpmLerpSpeed;

    public float throttleLerpSpeed;

    [Header("Pitch Mapping")]
    [Tooltip("AnimationCurve maps normalized RPM [0..1] to pitch multiplier.")]
    public AnimationCurve pitchVsRpm;

    [Header("Band Crossfade")]
    [Tooltip("Center points of the four bands over normalized RPM")]
    public Vector4 bandCenters;

    [Tooltip("How sharp the crossfade between bands is (bigger = narrower band).")]
    public float bandSharpness;

    [Header("On/Off Balance")]
    [Tooltip("Exponent shaping for throttle : On-throttle weight (1=linear, > 1 favors off at mid throttle).")]
    public float throttleShape;

    [Tooltip("Extra volume on-throttle compared to off-throttle.")]
    public float onThrottleBoost;

    [Header("Shift & Limiter")]
    public bool enableShiftFlare;

    public float shiftFlareAmount;
    public float shiftFlareTime;

    public bool enableSoftLimiter;
    public float limiterStart;
    public float limiterDepth;

    // --- INTERNAL ---
    private float _rpmSmoothed;

    private float _thrSmoothed;

    private float _shiftFlareT;
    private const int _bands = 4;

    private AudioSource[] _onSrc = new AudioSource[_bands];
    private AudioSource[] _offSrc = new AudioSource[_bands];

    private float[] _bandWeights = new float[_bands];

    private void Awake()
    {
        CreateOrReuseSources();
        _rpmSmoothed = Mathf.Clamp(RPM, minRPM, maxRPM);
        _thrSmoothed = Mathf.Clamp01(throttle);
    }

    private void Update()
    {
        // Smooth inputs
        RPM = Mathf.Clamp(RPM, minRPM, maxRPM);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

        // Exponential smoothing (frame-rate independent):  x += (target - x) * (1 - e^{-k*dt})
        _rpmSmoothed = Mathf.Lerp(_rpmSmoothed, RPM, 1f - Mathf.Exp(-rpmLerpSpeed * dt));
        _thrSmoothed = Mathf.Lerp(_thrSmoothed, Mathf.Clamp01(throttle), 1f - Mathf.Exp(-throttleLerpSpeed * dt));

        float n = Mathf.InverseLerp(minRPM, maxRPM, _rpmSmoothed);
        float onWeight = Mathf.Pow(_thrSmoothed, throttleShape);
        float offWeight = 1f - onWeight;

        // Limiter gating near redline
        float limiterGate = 1f;
        if (enableSoftLimiter && n > limiterStart)
        {
            float t = Mathf.InverseLerp(limiterStart, 1f, n);
            float blink = Mathf.PingPong(Time.time * 18f, 1f);
            limiterGate = Mathf.Lerp(1f, 1f - limiterDepth, blink * t);
        }

        BandWeights(n, ref _bandWeights);

        // Pitch base
        float pitchBase = pitchVsRpm.Evaluate(n);

        // Shift flare
        float flare = 1f;
        if (enableShiftFlare && _shiftFlareT > 0f)
        {
            float u = 1f - (_shiftFlareT / shiftFlareTime);
            flare = 1f + shiftFlareAmount * Mathf.SmoothStep(1f, 0f, u);
            _shiftFlareT -= Time.deltaTime;
        }

        for (int i = 0; i < _bands; i++)
        {
            float bandVol = _bandWeights[i] * limiterGate * masterVolume;

            // ON
            if (_onSrc[i] != null)
            {
                _onSrc[i].volume = bandVol * onWeight * (1f + onThrottleBoost * 0.5f);
                _onSrc[i].pitch = pitchBase * flare;
            }

            // OFF
            if (_offSrc[i] != null)
            {
                _offSrc[i].volume = bandVol * offWeight;
                _offSrc[i].pitch = pitchBase;
            }
        }
    }

    public void OnShift()
    {
        if (!enableShiftFlare) return;
        _shiftFlareT = shiftFlareTime;
    }

    // --- helpers ---
    private void BandWeights(float n, ref float[] bandWeights)
    {
        float s = Mathf.Max(1e-3f, bandSharpness);
        for (int i = 0; i < _bands; i++)
        {
            float c = (i == 0) ? bandCenters.x : (i == 1) ? bandCenters.y : (i == 2) ? bandCenters.z : bandCenters.w;
            float d = Mathf.Abs(n - c);

            bandWeights[i] = Mathf.Exp(-d * d * s);
        }
        // Normalize so total = 1 (avoid volume jumps)
        float sum = bandWeights[0] + bandWeights[1] + bandWeights[2] + bandWeights[3];
        if (sum > 1e-5f)
        {
            float inv = 1f / sum;            // Compute reciprocal once.
            bandWeights[0] *= inv; bandWeights[1] *= inv; bandWeights[2] *= inv; bandWeights[3] *= inv;
        }
    }

    private void CreateOrReuseSources()
    {
        // Create 8 engine sources: 4 on + 4 off
        SetupBandSource(0, on_Idle, true); // On/Idle.
        SetupBandSource(1, on_Low, true); // On/Low.
        SetupBandSource(2, on_Mid, true); // On/Mid.
        SetupBandSource(3, on_High, true); // On/High.

        SetupBandSource(0, off_Idle, false); // Off/Idle.
        SetupBandSource(1, off_Low, false); // Off/Low.
        SetupBandSource(2, off_Mid, false); // Off/Mid.
        SetupBandSource(3, off_High, false); // Off/High.
    }

    private void SetupBandSource(int band, AudioClip clip, bool onThrottle)
    {
        if (!clip)
            return;

        var src = MakeSrc((onThrottle ? "On" : "Off") + "_Band" + band, clip, 0f, true);

        if (onThrottle)
            _onSrc[band] = src;
        else _offSrc[band] = src;
    }

    private AudioSource MakeSrc(string name, AudioClip clip, float vol, bool loop)
    {
        var go = new GameObject("Audio_" + name);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = loop;
        a.playOnAwake = true;
        a.spatialBlend = spatialBlend;
        a.dopplerLevel = dopplerLevel;
        a.outputAudioMixerGroup = outputGroup;
        a.minDistance = 3f;
        a.maxDistance = 85f;
        a.volume = vol;
        a.pitch = 1f;
        a.rolloffMode = AudioRolloffMode.Custom;
        a.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
            new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2f),
                new Keyframe(0.4f, 0.6f),
                new Keyframe(1f, 0.0f)
            ));
        a.Play();
        return a;
    }

    public void SetUp()
    {
        spatialBlend = Mathf.Clamp01(spatialBlend);
        dopplerLevel = Mathf.Clamp(dopplerLevel, 0f, 5f);
        masterVolume = Mathf.Clamp01(masterVolume);
        if (_onSrc != null)
        {
            for (int i = 0; i < _onSrc.Length; i++)
            {
                if (_onSrc[i]) { _onSrc[i].spatialBlend = spatialBlend; _onSrc[i].dopplerLevel = dopplerLevel; }
                if (_offSrc[i]) { _offSrc[i].spatialBlend = spatialBlend; _offSrc[i].dopplerLevel = dopplerLevel; }
            }
        }
    }
}