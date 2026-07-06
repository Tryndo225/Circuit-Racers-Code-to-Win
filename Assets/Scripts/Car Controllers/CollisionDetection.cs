using UnityEngine;

/// <summary>
/// Plays crash SFX based on collision impact speed, with cooldown, volume curve,
/// pitch jitter, and layer filtering.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @invariant A <see cref="Rigidbody"/> component is present on the same GameObject.
/// @thread Runs on Unity main thread (physics callback).
/// @req SoundManager.Instance.PlaySFXClip is available and initialized.
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
public class CollisionDetection : MonoBehaviour
{
	#region Inspector: Clips

	/// <summary>Set of crash audio clips to pick from at runtime.</summary>
	[Header("Clips")]
	[SerializeField] private AudioClip[] crashClips;

	/// <summary>Lower bound for volume so valid impacts are still audible.</summary>
	[SerializeField, Range(0f, 1f)] private float minimumVolume = 0.08f;

	#endregion

	#region Inspector: Severity from Impact Speed

	[Header("Severity from Impact Speed")]

	/// <summary>
	/// Impact speed into the collision surface that starts producing crash audio.
	/// 3 m/s is about 11 km/h.
	/// </summary>
	[Tooltip("Impact speed into the collision surface that starts producing crash audio.")]
	[SerializeField] private float minImpactSpeed = 3f;

	/// <summary>
	/// Impact speed into the collision surface that maps to full crash severity.
	/// 22 m/s is about 79 km/h.
	/// </summary>
	[Tooltip("Impact speed into the collision surface that maps to full crash severity.")]
	[SerializeField] private float maxImpactSpeed = 22f;

	/// <summary>Minimum normalized severity needed before a crash sound is played.</summary>
	[Tooltip("Minimum normalized severity needed before a crash sound is played.")]
	[SerializeField, Range(0f, 1f)] private float minSeverityToPlay = 0.06f;

	#endregion

	#region Inspector: Sound Shaping

	[Header("Sound Shaping")]

	/// <summary>Global gain applied on top of the evaluated volume curve.</summary>
	[Range(0f, 1f)] public float baseVolume = 0.9f;

	/// <summary>Volume response as a function of normalized severity in [0,1].</summary>
	public AnimationCurve volumeCurve = new AnimationCurve(
		new Keyframe(0f, 0f),
		new Keyframe(1f, 1f)
	);

	/// <summary>Minimum pitch used for pitch interpolation by severity.</summary>
	[SerializeField] private float minPitch = 0.9f;

	/// <summary>Maximum pitch used for pitch interpolation by severity.</summary>
	[SerializeField] private float maxPitch = 1.08f;

	/// <summary>Minimum time (s) between two crash sounds.</summary>
	[SerializeField] private float cooldown = 0.18f;

	#endregion

	#region Inspector: Filtering

	[Header("Filtering")]

	/// <summary>Layers to ignore when deciding whether to play a crash.</summary>
	[Tooltip("Ignore these layers when deciding to play a crash.")]
	[SerializeField] private LayerMask ignoreLayers;

	#endregion

	#region State

	/// <summary>Cached rigidbody reference (required).</summary>
	private new Rigidbody rigidbody;

	/// <summary>Next allowed Time.time when a sound may be played.</summary>
	private float nextAllowedTime;

	#endregion

	#region Unity Methods

	/// <summary>Caches the Rigidbody dependency.</summary>
	private void Awake()
	{
		rigidbody = GetComponent<Rigidbody>();
	}

	/// <summary>
	/// Physics callback: computes collision severity from impact speed, shapes volume and pitch,
	/// and plays a crash clip if above threshold and not on cooldown.
	/// </summary>
	/// <param name="c">Collision data provided by Unity.</param>
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

		Debug.Log($"[CollisionDetection] Collision Valume: {volume}");

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
	/// Maps collision impact speed into normalized severity in [0,1].
	/// Only velocity into the collision surface is used, so scraping a wall sideways
	/// should not produce a full crash sound.
	/// </summary>
	/// <param name="c">Collision data.</param>
	/// <returns>Normalized severity where 0 <= result <= 1.</returns>
	/// <complexity>O(contactCount)</complexity>
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
	private bool IsIgnoredLayer(int layer)
	{
		return (ignoreLayers.value & (1 << layer)) != 0;
	}

	#endregion
}