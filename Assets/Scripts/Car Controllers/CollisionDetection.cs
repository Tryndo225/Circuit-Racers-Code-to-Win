using UnityEngine;

/// <summary>
/// Plays crash sound effects based on collision impact speed.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Converts collision impact severity into crash SFX volume and pitch.
///
/// The component evaluates the collision's relative velocity against contact normals to estimate
/// how strong the impact was. Valid impacts are mapped to a normalized severity value, which is then
/// used to shape crash volume and pitch.
///
/// Behaviour:
/// - Ignores collisions on configured layers.
/// - Requires a minimum impact severity before playing sound.
/// - Uses a cooldown to avoid rapid repeated crash sounds.
/// - Randomly selects a crash clip from <see cref="crashClips"/>.
/// - Plays the sound through <see cref="SoundManager"/>.
///
/// Requirements:
/// - A <see cref="Rigidbody"/> must be present on the same object.
/// - <see cref="SoundManager.Instance"/> must exist for crash audio to be played.
///
/// Threading:
/// - Unity main thread only.
/// - Uses Unity physics collision callbacks.
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
public class CollisionDetection : MonoBehaviour
{
	#region Inspector: Clips

	/// <summary>
	/// Crash audio clips available for random selection.
	/// </summary>
	[Tooltip("Crash audio clips available for random selection.")]
	[Header("Clips")]
	[SerializeField] private AudioClip[] crashClips;

	/// <summary>
	/// Lower volume bound used for valid crash impacts.
	/// </summary>
	[Tooltip("Minimum crash volume so valid impacts remain audible.")]
	[SerializeField, Range(0f, 1f)] private float minimumVolume = 0.08f;

	#endregion

	#region Inspector: Severity from Impact Speed

	[Header("Severity from Impact Speed")]

	/// <summary>
	/// Impact speed into the collision surface that starts producing crash audio.
	/// </summary>
	/// <remarks>
	/// A value of 3 m/s is approximately 11 km/h.
	/// </remarks>
	[Tooltip("Impact speed into the collision surface that starts producing crash audio.")]
	[SerializeField] private float minImpactSpeed = 3f;

	/// <summary>
	/// Impact speed into the collision surface that maps to full crash severity.
	/// </summary>
	/// <remarks>
	/// A value of 22 m/s is approximately 79 km/h.
	/// </remarks>
	[Tooltip("Impact speed into the collision surface that maps to full crash severity.")]
	[SerializeField] private float maxImpactSpeed = 22f;

	/// <summary>
	/// Minimum normalized severity required before a crash sound is played.
	/// </summary>
	[Tooltip("Minimum normalized severity needed before a crash sound is played.")]
	[SerializeField, Range(0f, 1f)] private float minSeverityToPlay = 0.06f;

	#endregion

	#region Inspector: Sound Shaping

	[Header("Sound Shaping")]

	/// <summary>
	/// Global gain applied on top of the evaluated volume curve.
	/// </summary>
	[Tooltip("Global gain applied on top of the evaluated volume curve.")]
	[Range(0f, 1f)] public float baseVolume = 0.9f;

	/// <summary>
	/// Volume response as a function of normalized impact severity.
	/// </summary>
	[Tooltip("Volume response as a function of normalized impact severity.")]
	public AnimationCurve volumeCurve = new AnimationCurve(
		new Keyframe(0f, 0f),
		new Keyframe(1f, 1f)
	);

	/// <summary>
	/// Minimum pitch used for severity-based pitch interpolation.
	/// </summary>
	[Tooltip("Minimum pitch used for severity-based pitch interpolation.")]
	[SerializeField] private float minPitch = 0.9f;

	/// <summary>
	/// Maximum pitch used for severity-based pitch interpolation.
	/// </summary>
	[Tooltip("Maximum pitch used for severity-based pitch interpolation.")]
	[SerializeField] private float maxPitch = 1.08f;

	/// <summary>
	/// Minimum time in seconds between two crash sounds.
	/// </summary>
	[Tooltip("Minimum time in seconds between two crash sounds.")]
	[SerializeField] private float cooldown = 0.18f;

	#endregion

	#region Inspector: Filtering

	[Header("Filtering")]

	/// <summary>
	/// Layers ignored when deciding whether to play a crash sound.
	/// </summary>
	[Tooltip("Ignore these layers when deciding to play a crash.")]
	[SerializeField] private LayerMask ignoreLayers;

	#endregion

	#region State

	/// <summary>
	/// Cached required rigidbody component.
	/// </summary>
	private new Rigidbody rigidbody;

	/// <summary>
	/// Next <see cref="Time.time"/> value at which a crash sound may be played.
	/// </summary>
	private float nextAllowedTime;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Caches the required <see cref="Rigidbody"/> dependency.
	/// </summary>
	private void Awake()
	{
		rigidbody = GetComponent<Rigidbody>();
	}

	/// <summary>
	/// Handles collision entry and plays crash audio when the impact is strong enough.
	/// </summary>
	/// <param name="c">Collision data provided by Unity.</param>
	/// <remarks>
	/// The method checks layer filtering, clip availability, impact severity, and cooldown before
	/// playing a crash sound through <see cref="SoundManager"/>.
	/// </remarks>
	private void OnCollisionEnter(Collision c)
	{
		if (IsIgnoredLayer(c.gameObject.layer)) return;

		if (crashClips == null || crashClips.Length == 0) return;

		float severity = GetSeverity01(c);

		Debug.Log($"[CollisionDetection] Collision Severity: {severity}");

		if (severity < minSeverityToPlay) return;

		if (Time.time < nextAllowedTime) return;

		AudioClip clip = crashClips[Random.Range(0, crashClips.Length)];
		if (clip == null) return;

		float volume = volumeCurve.Evaluate(severity) * baseVolume;
		volume = Mathf.Clamp01(Mathf.Max(volume, minimumVolume));

		float pitch = Mathf.Lerp(minPitch, maxPitch, severity) * Random.Range(0.98f, 1.02f);

		Debug.Log($"[CollisionDetection] Collision Volume: {volume}");

		if (SoundManager.Instance == null)
		{
			Debug.LogWarning("[CollisionDetection] SoundManager is missing; crash SFX skipped.");
			return;
		}

		SoundManager.Instance.PlaySFXClip(clip, transform, volume, pitch, false, 1.0f, true);

		nextAllowedTime = Time.time + cooldown;
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Maps collision impact speed into normalized severity.
	/// </summary>
	/// <param name="c">Collision data.</param>
	/// <returns>Normalized severity in the range 0 to 1.</returns>
	/// <remarks>
	/// Only velocity into the collision surface is used. This means scraping along a wall should produce
	/// less severe crash audio than driving directly into it.
	///
	/// The method checks all contact points and uses the strongest normal impact speed.
	/// </remarks>
	private float GetSeverity01(Collision c)
	{
		if (c.contactCount == 0)
		{
			return Mathf.InverseLerp(
				minImpactSpeed,
				maxImpactSpeed,
				c.relativeVelocity.magnitude
			);
		}

		float strongestImpactSpeed = 0f;

		for (int i = 0; i < c.contactCount; i++)
		{
			ContactPoint contact = c.GetContact(i);

			float normalImpactSpeed = Mathf.Abs(
				Vector3.Dot(c.relativeVelocity, contact.normal)
			);

			if (normalImpactSpeed > strongestImpactSpeed)
			{
				strongestImpactSpeed = normalImpactSpeed;
			}
		}

		return Mathf.InverseLerp(
			minImpactSpeed,
			maxImpactSpeed,
			strongestImpactSpeed
		);
	}

	/// <summary>
	/// Checks whether a layer is included in the ignored layer mask.
	/// </summary>
	/// <param name="layer">Layer index to check.</param>
	/// <returns><c>true</c> if the layer should be ignored; otherwise <c>false</c>.</returns>
	private bool IsIgnoredLayer(int layer)
	{
		return (ignoreLayers.value & (1 << layer)) != 0;
	}

	#endregion
}