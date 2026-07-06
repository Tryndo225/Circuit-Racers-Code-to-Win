using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Procedural engine audio controller using four RPM bands.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Blends idle, low, mid, and high RPM engine clips with on-throttle and off-throttle layers.
///
/// The component smooths RPM and throttle input, crossfades between four RPM bands, maps RPM to pitch,
/// applies a short shift flare, and uses a soft limiter near redline.
///
/// Audio layout:
/// - Four on-throttle layers: idle, low, mid, high.
/// - Four off-throttle layers: idle, low, mid, high.
/// - Each layer is played by its own looping <see cref="AudioSource"/>.
///
/// Threading:
/// - Unity main thread only.
/// - Updated from <see cref="Update"/>.
/// </remarks>
[DisallowMultipleComponent]
public class EngineSound : MonoBehaviour
{
	#region Inspector: Inputs

	[Header("Inputs")]
	/// <summary>
	/// Current engine speed in RPM.
	/// </summary>
	/// <remarks>
	/// This value is clamped between <see cref="minRPM"/> and <see cref="maxRPM"/> during updates.
	/// </remarks>
	[Tooltip("Current engine speed in RPM.")]
	public float RPM;

	/// <summary>
	/// Current driver demand or engine load.
	/// </summary>
	[Range(0f, 1f)]
	[Tooltip("Driver demand or engine load.")]
	public float throttle;

	#endregion

	#region Inspector: RPM Range

	[Header("RPM Range")]
	/// <summary>
	/// Minimum RPM used for normalization.
	/// </summary>
	[Tooltip("Minimum RPM used for normalization.")]
	public float minRPM;

	/// <summary>
	/// Maximum RPM used for normalization.
	/// </summary>
	[Tooltip("Maximum RPM used for normalization.")]
	public float maxRPM;

	#endregion

	#region Inspector: Main Output

	[Header("Main Output")]
	/// <summary>
	/// Audio mixer group used by all engine audio sources.
	/// </summary>
	[Tooltip("Audio mixer group used by all engine audio sources.")]
	public AudioMixerGroup outputGroup;

	/// <summary>
	/// Master volume applied after band mixing.
	/// </summary>
	[Tooltip("Master volume applied after band mixing.")]
	[Range(0f, 1f)] public float masterVolume;

	/// <summary>
	/// Spatial blend applied to engine audio sources.
	/// </summary>
	/// <remarks>
	/// A value of 0 means fully 2D audio. A value of 1 means fully 3D audio.
	/// </remarks>
	[Tooltip("0 = 2D, 1 = fully 3D spatialized.")]
	[Range(0f, 1f)] public float spatialBlend;

	/// <summary>
	/// Doppler effect intensity.
	/// </summary>
	[Tooltip("Doppler effect intensity.")]
	[Range(0f, 5f)] public float dopplerLevel;

	#endregion

	#region Inspector: Clips (On/Off Throttle)

	[Header("Clips (On-Throttle)")]
	/// <summary>
	/// On-throttle idle RPM band clip.
	/// </summary>
	[Tooltip("On-throttle idle RPM band clip.")]
	public AudioClip on_Idle;

	/// <summary>
	/// On-throttle low RPM band clip.
	/// </summary>
	[Tooltip("On-throttle low RPM band clip.")]
	public AudioClip on_Low;

	/// <summary>
	/// On-throttle mid RPM band clip.
	/// </summary>
	[Tooltip("On-throttle mid RPM band clip.")]
	public AudioClip on_Mid;

	/// <summary>
	/// On-throttle high RPM band clip.
	/// </summary>
	[Tooltip("On-throttle high RPM band clip.")]
	public AudioClip on_High;

	[Header("Clips (Off-Throttle)")]
	/// <summary>
	/// Off-throttle idle RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle idle RPM band clip.")]
	public AudioClip off_Idle;

	/// <summary>
	/// Off-throttle low RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle low RPM band clip.")]
	public AudioClip off_Low;

	/// <summary>
	/// Off-throttle mid RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle mid RPM band clip.")]
	public AudioClip off_Mid;

	/// <summary>
	/// Off-throttle high RPM band clip.
	/// </summary>
	[Tooltip("Off-throttle high RPM band clip.")]
	public AudioClip off_High;

	#endregion

	#region Inspector: Smoothing

	[Header("Smoothing")]
	/// <summary>
	/// RPM smoothing speed constant.
	/// </summary>
	/// <remarks>
	/// Higher values make RPM audio respond faster.
	/// </remarks>
	[Tooltip("RPM smoothing speed. Higher values make RPM audio respond faster.")]
	public float rpmLerpSpeed;

	/// <summary>
	/// Throttle smoothing speed constant.
	/// </summary>
	[Tooltip("Throttle smoothing speed. Higher values make throttle audio respond faster.")]
	public float throttleLerpSpeed;

	#endregion

	#region Inspector: Pitch Mapping

	[Header("Pitch Mapping")]
	/// <summary>
	/// Maps normalized RPM to pitch multiplier.
	/// </summary>
	[Tooltip("AnimationCurve maps normalized RPM from 0 to 1 to pitch multiplier.")]
	public AnimationCurve pitchVsRpm;

	#endregion

	#region Inspector: Band Crossfade

	[Header("Band Crossfade")]
	/// <summary>
	/// Center points of the four RPM bands over normalized RPM.
	/// </summary>
	/// <remarks>
	/// Values correspond to idle, low, mid, and high bands.
	/// </remarks>
	[Tooltip("Center points of the idle, low, mid, and high RPM bands over normalized RPM.")]
	public Vector4 bandCenters;

	/// <summary>
	/// Crossfade sharpness between RPM bands.
	/// </summary>
	/// <remarks>
	/// Larger values create narrower band ranges.
	/// </remarks>
	[Tooltip("Crossfade sharpness between bands. Higher values create narrower bands.")]
	public float bandSharpness;

	#endregion

	#region Inspector: On/Off Balance

	[Header("On/Off Balance")]
	/// <summary>
	/// Exponent shaping from throttle value to on-throttle weight.
	/// </summary>
	/// <remarks>
	/// A value of 1 is linear. Values greater than 1 keep more off-throttle character around mid throttle.
	/// </remarks>
	[Tooltip("Exponent shaping for throttle to on-throttle weight. 1 = linear.")]
	public float throttleShape;

	/// <summary>
	/// Additional gain for the on-throttle layer.
	/// </summary>
	[Tooltip("Extra volume for on-throttle audio compared to off-throttle audio.")]
	public float onThrottleBoost;

	#endregion

	#region Inspector: Shift & Limiter

	[Header("Shift & Limiter")]
	/// <summary>
	/// Pitch flare amplitude during gear shifts.
	/// </summary>
	[Tooltip("Pitch flare amplitude during gear shifts.")]
	public float shiftFlareAmount;

	/// <summary>
	/// Duration of shift flare in seconds.
	/// </summary>
	[Tooltip("Duration of shift flare in seconds.")]
	public float shiftFlareTime;

	/// <summary>
	/// Limiter activation threshold in normalized RPM.
	/// </summary>
	[Tooltip("Limiter activation threshold in normalized RPM.")]
	public float limiterStart;

	/// <summary>
	/// Limiter depth near redline.
	/// </summary>
	[Tooltip("Limiter depth, controlling how much volume is reduced near redline.")]
	public float limiterDepth;

	#endregion

	#region State

	/// <summary>
	/// Smoothed RPM value used for audio blending.
	/// </summary>
	private float _rpmSmoothed;

	/// <summary>
	/// Smoothed throttle value used for on/off-throttle blending.
	/// </summary>
	private float _thrSmoothed;

	/// <summary>
	/// Remaining shift flare time.
	/// </summary>
	private float _shiftFlareT;

	/// <summary>
	/// Number of RPM bands used internally.
	/// </summary>
	private const int _bands = 4;

	/// <summary>
	/// Audio sources for on-throttle RPM bands.
	/// </summary>
	private AudioSource[] _onSrc = new AudioSource[_bands];

	/// <summary>
	/// Audio sources for off-throttle RPM bands.
	/// </summary>
	private AudioSource[] _offSrc = new AudioSource[_bands];

	/// <summary>
	/// Current normalized weights for the four RPM bands.
	/// </summary>
	private float[] _bandWeights = new float[_bands];

	#endregion

	#region Unity Methods

	/// <summary>
	/// Initializes audio sources and seeds smoothed input values.
	/// </summary>
	private void Awake()
	{
		CreateOrReuseSources();
		_rpmSmoothed = Mathf.Clamp(RPM, minRPM, maxRPM);
		_thrSmoothed = Mathf.Clamp01(throttle);
	}

	/// <summary>
	/// Updates smoothed RPM/throttle, computes band weights, applies limiter, and updates all engine sources.
	/// </summary>
	private void Update()
	{
		//Debug.Log("[EngineSound] RPM: " + RPM);
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

	/// <summary>
	/// Triggers a short pitch flare.
	/// </summary>
	/// <remarks>
	/// Intended to be called on gear shifts.
	/// </remarks>
	public void OnShift()
	{
		_shiftFlareT = shiftFlareTime;
	}

	/// <summary>
	/// Clamps user-facing audio properties and applies them to created sources.
	/// </summary>
	/// <remarks>
	/// Call after <see cref="Awake"/> if inspector values are modified at runtime.
	/// </remarks>
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
	/// Computes normalized weights for the four RPM bands.
	/// </summary>
	/// <param name="n">Normalized RPM in the range 0 to 1.</param>
	/// <param name="bandWeights">Output array containing four band weights.</param>
	/// <remarks>
	/// Weights are calculated around <see cref="bandCenters"/> and then normalized so the total is 1.
	/// </remarks>
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
	/// Creates or reuses audio sources for all on-throttle and off-throttle RPM bands.
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
	/// Creates a band source if a clip exists and stores it in the on/off source arrays.
	/// </summary>
	/// <param name="band">Band index from 0 to 3.</param>
	/// <param name="clip">Audio clip for this band.</param>
	/// <param name="onThrottle">Whether the source belongs to the on-throttle layer.</param>
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
	/// Instantiates an engine audio source child and starts playback.
	/// </summary>
	/// <param name="name">Child object name suffix.</param>
	/// <param name="clip">Audio clip assigned to the source.</param>
	/// <param name="vol">Initial source volume.</param>
	/// <param name="loop">Whether the source loops playback.</param>
	/// <returns>The created audio source.</returns>
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