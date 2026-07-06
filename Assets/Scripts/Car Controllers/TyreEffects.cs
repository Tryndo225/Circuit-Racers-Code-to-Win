using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Generates tire slip effects for a wheel.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Plays smoke particles, skid trails, and skid audio when wheel slip exceeds a threshold.
///
/// The component reads slip from the required <see cref="WheelCollider"/>. When the wheel is grounded
/// and the combined forward/sideways slip is high enough, it:
/// - emits smoke at the wheel contact point,
/// - aligns smoke with the ground normal,
/// - enables a skid trail,
/// - plays looping skid audio with volume based on slip.
///
/// Requirements:
/// - A <see cref="WheelCollider"/> must be present on the same object.
/// - A vehicle <see cref="Rigidbody"/> may exist in the parent hierarchy for velocity-based smoke direction.
///
/// Threading:
/// - Unity main thread only.
/// - Effects are updated in <see cref="FixedUpdate"/>.
/// </remarks>
[RequireComponent(typeof(WheelCollider))]
public class TyreEffects : MonoBehaviour
{
	#region Inspector: References

	[Header("References")]
	/// <summary>
	/// Particle system prefab used for tire smoke.
	/// </summary>
	/// <remarks>
	/// Instantiated at runtime as a child of this object.
	/// </remarks>
	[Tooltip("Particle system prefab used for tire smoke.")]
	[SerializeField] private ParticleSystem smokePrefab;

	/// <summary>
	/// Trail renderer prefab used for skid marks.
	/// </summary>
	/// <remarks>
	/// Instantiated at runtime as a child of this object.
	/// </remarks>
	[Tooltip("Trail renderer prefab used for skid marks.")]
	[SerializeField] private TrailRenderer skidTrailPrefab;

	/// <summary>
	/// Looping skid audio clip.
	/// </summary>
	[Tooltip("Looping skid audio clip.")]
	[SerializeField] private AudioClip skidAudioClip;

	/// <summary>
	/// Audio mixer group used by the skid audio source.
	/// </summary>
	[Tooltip("Audio mixer group used by the skid audio source.")]
	[SerializeField] private AudioMixerGroup skidAudioMixerGroup;

	#endregion

	#region Inspector: Tuning

	[Header("Tuning")]
	/// <summary>
	/// Combined slip amount above which smoke, trail, and audio effects start.
	/// </summary>
	[Tooltip("Combined slip amount above which smoke, trail, and audio effects start.")]
	[SerializeField, Range(0f, 5f)] private float slipThreshold = 0.25f;

	/// <summary>
	/// Maximum particles emitted per second at peak slip.
	/// </summary>
	[Tooltip("Maximum particles emitted per second at peak slip.")]
	[SerializeField] private float maxEmissionRatePerSecond = 60f;

	/// <summary>
	/// Slip value at which smoke emission reaches its maximum rate.
	/// </summary>
	[Tooltip("Slip value at which smoke emission reaches its maximum rate.")]
	[SerializeField] private float maxEmissionRateAtSlip = 4f;

	/// <summary>
	/// Vertical offset above the ground contact point used for visual effects.
	/// </summary>
	/// <remarks>
	/// Helps avoid z-fighting with the road surface.
	/// </remarks>
	[Tooltip("Offsets smoke and trail slightly above the ground to avoid z-fighting.")]
	[SerializeField] private float groundOffset = 0.02f;

	#endregion

	#region State

	/// <summary>
	/// Cached wheel collider on this object.
	/// </summary>
	private WheelCollider wheel;

	/// <summary>
	/// Cached vehicle rigidbody found in the parent hierarchy.
	/// </summary>
	private Rigidbody carRb;

	/// <summary>
	/// Runtime smoke particle-system instance.
	/// </summary>
	private ParticleSystem smokeInstance;

	/// <summary>
	/// Runtime skid-trail instance.
	/// </summary>
	private TrailRenderer trailInstance;

	/// <summary>
	/// Runtime audio source used for skid sound.
	/// </summary>
	private AudioSource skidAudio;

	/// <summary>
	/// Fractional particle accumulator used to emit whole particle counts over time.
	/// </summary>
	private float particlesLeftOver;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Caches component references and creates the skid audio source.
	/// </summary>
	private void Awake()
	{
		wheel = GetComponent<WheelCollider>();
		carRb = GetComponentInParent<Rigidbody>();

		skidAudio = gameObject.AddComponent<AudioSource>();
		skidAudio.outputAudioMixerGroup = skidAudioMixerGroup;
		skidAudio.clip = skidAudioClip;
	}

	/// <summary>
	/// Instantiates optional smoke and trail prefabs and configures skid audio playback.
	/// </summary>
	private void Start()
	{
		if (smokePrefab != null)
			smokeInstance = Instantiate(smokePrefab, transform);

		if (skidTrailPrefab != null)
		{
			trailInstance = Instantiate(skidTrailPrefab, transform);
			trailInstance.emitting = false;
		}

		if (skidAudio != null)
		{
			skidAudio.loop = true;
			skidAudio.playOnAwake = false;
		}
	}

	/// <summary>
	/// Updates tire smoke, skid trail, and skid audio for the current physics step.
	/// </summary>
	/// <remarks>
	/// Effects stop when the wheel is not grounded, when no ground hit is available, or when slip is below
	/// <see cref="slipThreshold"/>.
	/// </remarks>
	private void FixedUpdate()
	{
		if (smokeInstance == null && trailInstance == null && skidAudio == null) return;

		bool grounded = wheel.isGrounded;
		if (!grounded)
		{
			StopEffects();
			return;
		}

		if (wheel.GetGroundHit(out WheelHit hit))
		{
			float slipMag = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);

			if (slipMag > slipThreshold)
			{
				Vector3 at = hit.point + hit.normal * groundOffset;

				// Smoke: position, orientation, and emission by slip
				if (smokeInstance != null)
				{
					smokeInstance.transform.position = at;

					Vector3 forward = (carRb && carRb.linearVelocity.sqrMagnitude > 0.01f)
						? Vector3.ProjectOnPlane(carRb.linearVelocity, hit.normal).normalized
						: Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

					smokeInstance.transform.rotation = Quaternion.LookRotation(forward, hit.normal);

					float emission01 = Mathf.InverseLerp(slipThreshold, maxEmissionRateAtSlip, slipMag);
					particlesLeftOver += emission01 * maxEmissionRatePerSecond * Time.fixedDeltaTime;

					int emitCount = Mathf.FloorToInt(particlesLeftOver);
					particlesLeftOver -= emitCount;

					if (emitCount > 0)
						smokeInstance.Emit(emitCount);
				}

				// Trail: enable and position
				if (trailInstance != null)
				{
					trailInstance.transform.position = at;
					trailInstance.emitting = true;
				}

				// Audio: volume scales with slip, looped
				if (skidAudio != null)
				{
					skidAudio.volume = Mathf.Clamp01(slipMag / maxEmissionRateAtSlip);
					if (!skidAudio.isPlaying)
						skidAudio.Play();
				}

				return;
			}
		}

		// Below threshold or no hit
		StopEffects();
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Disables trail emission and stops skid audio playback.
	/// </summary>
	private void StopEffects()
	{
		if (trailInstance != null)
			trailInstance.emitting = false;

		if (skidAudio != null && skidAudio.isPlaying)
			skidAudio.Stop();
	}

	#endregion
}