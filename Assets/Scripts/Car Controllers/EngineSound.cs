using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Procedural engine audio using four RPM bands (idle/low/mid/high) with on/off-throttle layers.
/// Smooths RPM and throttle, crossfades bands, applies pitch vs. RPM, shift flares, and a soft limiter.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @invariant Exactly 4 bands are used internally (idle, low, mid, high).
/// @invariant minRPM <= RPM <= maxRPM during Update (values are clamped).
/// @thread Unity main thread (Update).
/// </remarks>
[DisallowMultipleComponent]
public class EngineSound : MonoBehaviour
{
    #region Inspector: Inputs

    [Header("Inputs")]
    /// <summary>Current engine speed in RPM (will be clamped to [minRPM, maxRPM]).</summary>
    [Tooltip("Current engine speed in RPM.")]
    public float RPM;

    /// <summary>Driver demand/load in [0,1].</summary>
    [Range(0f, 1f), Tooltip("Driver demand/load")]
    public float throttle;

    #endregion

    #region Inspector: RPM Range

    [Header("RPM Range")]
    /// <summary>Minimum RPM for normalization.</summary>
    public float minRPM;

    /// <summary>Maximum RPM for normalization.</summary>
    public float maxRPM;

    #endregion

    #region Inspector: Main Output

    [Header("Main Output")]
    /// <summary>Audio mixer group for all engine sources.</summary>
    public AudioMixerGroup outputGroup;

    /// <summary>Master volume applied after band mixing.</summary>
    [Range(0f, 1f)] public float masterVolume;

    /// <summary>0 = 2D, 1 = fully 3D spatialized.</summary>
    [Range(0f, 1f)] public float spatialBlend;

    /// <summary>Doppler effect intensity (0..5).</summary>
    [Range(0f, 5f)] public float dopplerLevel;

    #endregion

    #region Inspector: Clips (On/Off Throttle)

    [Header("Clips (On-Throttle)")]
    /// <summary>On-throttle idle clip.</summary>
    public AudioClip on_Idle;
    /// <summary>On-throttle low band clip.</summary>
    public AudioClip on_Low;
    /// <summary>On-throttle mid band clip.</summary>
    public AudioClip on_Mid;
    /// <summary>On-throttle high band clip.</summary>
    public AudioClip on_High;

    [Header("Clips (Off-Throttle)")]
    /// <summary>Off-throttle idle clip.</summary>
    public AudioClip off_Idle;
    /// <summary>Off-throttle low band clip.</summary>
    public AudioClip off_Low;
    /// <summary>Off-throttle mid band clip.</summary>
    public AudioClip off_Mid;
    /// <summary>Off-throttle high band clip.</summary>
    public AudioClip off_High;

    #endregion

    #region Inspector: Smoothing

    [Header("Smoothing")]
    /// <summary>RPM smoothing speed constant (higher = faster response).</summary>
    [Tooltip("Higher = Faster response")]
    public float rpmLerpSpeed;

    /// <summary>Throttle smoothing speed constant.</summary>
    public float throttleLerpSpeed;

    #endregion

    #region Inspector: Pitch Mapping

    [Header("Pitch Mapping")]
    /// <summary>Maps normalized RPM [0..1] to pitch multiplier.</summary>
    [Tooltip("AnimationCurve maps normalized RPM [0..1] to pitch multiplier.")]
    public AnimationCurve pitchVsRpm;

    #endregion

    #region Inspector: Band Crossfade

    [Header("Band Crossfade")]
    /// <summary>Center points of the four bands over normalized RPM.</summary>
    [Tooltip("Center points of the four bands over normalized RPM")]
    public Vector4 bandCenters;

    /// <summary>Crossfade sharpness between bands (bigger = narrower band).</summary>
    [Tooltip("How sharp the crossfade between bands is (bigger = narrower band).")]
    public float bandSharpness;

    #endregion

    #region Inspector: On/Off Balance

    [Header("On/Off Balance")]
    /// <summary>Exponent shaping for throttle to on-throttle weight (1=linear, >1 favors off around mid throttle).</summary>
    [Tooltip("Exponent shaping for throttle : On-throttle weight (1=linear, > 1 favors off at mid throttle).")]
    public float throttleShape;

    /// <summary>Additional gain for on-throttle layer relative to off-throttle.</summary>
    [Tooltip("Extra volume on-throttle compared to off-throttle.")]
    public float onThrottleBoost;

    #endregion

    #region Inspector: Shift & Limiter

    [Header("Shift & Limiter")]
    /// <summary>Pitch flare amplitude during gear shifts.</summary>
    public float shiftFlareAmount;

    /// <summary>Duration of shift flare in seconds.</summary>
    public float shiftFlareTime;

    /// <summary>Limiter activation threshold in normalized RPM [0..1].</summary>
    public float limiterStart;

    /// <summary>Limiter depth (how much level is reduced near redline).</summary>
    public float limiterDepth;

    #endregion

    #region State

    // Internal state
    private float _rpmSmoothed;
    private float _thrSmoothed;
    private float _shiftFlareT;

    private const int _bands = 4;

    private AudioSource[] _onSrc = new AudioSource[_bands];
    private AudioSource[] _offSrc = new AudioSource[_bands];

    private float[] _bandWeights = new float[_bands];

    #endregion

    #region Unity Methods

    /// <summary>Initializes audio sources and seeds smoothed inputs.</summary>
    private void Awake()
    {
        CreateOrReuseSources();
        _rpmSmoothed = Mathf.Clamp(RPM, minRPM, maxRPM);
        _thrSmoothed = Mathf.Clamp01(throttle);
    }

    /// <summary>
    /// Updates smoothed RPM and throttle, computes band weights and limiter, and applies volume/pitch
    /// to each on/off source.
    /// </summary>
    private void Update()
    {
        // Smooth inputs
        RPM = Mathf.Clamp(RPM, minRPM, maxRPM);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

        // Exponential smoothing: x += (target - x) * (1 - exp(-k*dt))
        _rpmSmoothed = Mathf.Lerp(_rpmSmoothed, RPM, 1f - Mathf.Exp(-rpmLerpSpeed * dt));
        _thrSmoothed = Mathf.Lerp(_thrSmoothed, Mathf.Clamp01(throttle), 1f - Mathf.Exp(-throttleLerpSpeed * dt));

        float n = Mathf.InverseLerp(minRPM, maxRPM, _rpmSmoothed);
        float onWeight = Mathf.Pow(_thrSmoothed, throttleShape);
        float offWeight = 1f - onWeight;

        // Limiter gating near redline
        float limiterGate = 1f;
        if (n > limiterStart)
        {
            float t = Mathf.InverseLerp(limiterStart, 1f, n);
            float blink = Mathf.PingPong(Time.time * 18f, 1f);
            limiterGate = Mathf.Lerp(1f, 1f - limiterDepth, blink * t);
        }

        BandWeights(n, ref _bandWeights);

        // Pitch
        float pitchBase = pitchVsRpm.Evaluate(n);

        // Shift flare
        float flare = 1f;
        if (_shiftFlareT > 0f)
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

    #endregion

    #region Public API

    /// <summary>Triggers a short pitch flare (e.g., on gear shift).</summary>
    public void OnShift()
    {
        _shiftFlareT = shiftFlareTime;
    }

    /// <summary>
    /// Clamps user-facing audio properties and applies them to created sources.
    /// Call after Awake if you modify inspector values at runtime.
    /// </summary>
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

    #endregion

    #region Helpers

    /// <summary>
    /// Computes normalized Gaussian-like weights for the four RPM bands around bandCenters,
    /// then normalizes weights to sum to 1.
    /// </summary>
    /// <param name="n">Normalized RPM in [0,1].</param>
    /// <param name="bandWeights">Output array of length 4.</param>
    private void BandWeights(float n, ref float[] bandWeights)
    {
        float s = Mathf.Max(1e-3f, bandSharpness);
        for (int i = 0; i < _bands; i++)
        {
            float c = (i == 0) ? bandCenters.x : (i == 1) ? bandCenters.y : (i == 2) ? bandCenters.z : bandCenters.w;
            float d = Mathf.Abs(n - c);
            bandWeights[i] = Mathf.Exp(-d * d * s);
        }
        // Normalize to sum = 1
        float sum = bandWeights[0] + bandWeights[1] + bandWeights[2] + bandWeights[3];
        if (sum > 1e-5f)
        {
            float inv = 1f / sum;
            bandWeights[0] *= inv; bandWeights[1] *= inv; bandWeights[2] *= inv; bandWeights[3] *= inv;
        }
    }

    /// <summary>
    /// Creates or reuses eight audio sources (4 on-throttle + 4 off-throttle) and starts playback.
    /// </summary>
    private void CreateOrReuseSources()
    {
        // 4 on-throttle
        SetupBandSource(0, on_Idle, true);
        SetupBandSource(1, on_Low, true);
        SetupBandSource(2, on_Mid, true);
        SetupBandSource(3, on_High, true);

        // 4 off-throttle
        SetupBandSource(0, off_Idle, false);
        SetupBandSource(1, off_Low, false);
        SetupBandSource(2, off_Mid, false);
        SetupBandSource(3, off_High, false);
    }

    /// <summary>
    /// Creates a band source if the clip exists and stores it in the on/off arrays.
    /// </summary>
    /// <param name="band">Band index [0..3].</param>
    /// <param name="clip">Audio clip for this band.</param>
    /// <param name="onThrottle">True for on-throttle layer, false for off-throttle.</param>
    private void SetupBandSource(int band, AudioClip clip, bool onThrottle)
    {
        if (!clip)
            return;

        var src = MakeSrc((onThrottle ? "On" : "Off") + "_Band" + band, clip, 0f, true);

        if (onThrottle)
            _onSrc[band] = src;
        else
            _offSrc[band] = src;
    }

    /// <summary>
    /// Instantiates an AudioSource child with standard engine settings and starts playback.
    /// </summary>
    /// <param name="name">Child object name suffix.</param>
    /// <param name="clip">Audio clip.</param>
    /// <param name="vol">Initial volume.</param>
    /// <param name="loop">Whether to loop playback.</param>
    /// <returns>The created AudioSource.</returns>
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
        a.SetCustomCurve(
            AudioSourceCurveType.CustomRolloff,
            new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2f),
                new Keyframe(0.4f, 0.6f),
                new Keyframe(1f, 0.0f)
            )
        );
        a.Play();
        return a;
    }

    #endregion
}
