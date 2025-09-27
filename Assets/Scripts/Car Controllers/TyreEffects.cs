using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Generates tire effects (smoke particles, skid trail, and audio) when wheel slip exceeds a threshold.
/// Aligns smoke to ground normal and projects forward along vehicle velocity.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @invariant Requires a <see cref="WheelCollider"/> on the same GameObject.
/// @thread Unity main thread (Awake/Start) and physics thread (FixedUpdate).
/// </remarks>
[RequireComponent(typeof(WheelCollider))]
public class TyreEffects : MonoBehaviour
{
    #region Inspector: References

    [Header("References")]
    /// <summary>Particle system prefab used for tire smoke (instantiated at runtime).</summary>
    [SerializeField] private ParticleSystem smokePrefab;

    /// <summary>Trail renderer prefab used for skid marks (instantiated at runtime).</summary>
    [SerializeField] private TrailRenderer skidTrailPrefab;

    /// <summary>Skid loop audio clip.</summary>
    [SerializeField] private AudioClip skidAudioClip;

    /// <summary>Audio mixer group for the skid audio source.</summary>
    [SerializeField] private AudioMixerGroup skidAudioMixerGroup;

    #endregion

    #region Inspector: Tuning

    [Header("Tuning")]
    /// <summary>Slip amount above which smoke/trail/audio start.</summary>
    [Tooltip("Slip amount above which smoke starts.")]
    [SerializeField, Range(0f, 5f)] private float slipThreshold = 0.25f;

    /// <summary>Maximum particles per second emitted at peak slip.</summary>
    [Tooltip("Particles per second at max slip.")]
    [SerializeField] private float maxEmissionRatePerSecond = 60f;

    /// <summary>Slip value at which emission is considered “max”.</summary>
    [SerializeField] private float maxEmissionRateAtSlip = 4f;

    /// <summary>Offsets smoke slightly above ground to avoid z-fighting.</summary>
    [Tooltip("Offsets smoke slightly above the ground to avoid z-fighting.")]
    [SerializeField] private float groundOffset = 0.02f;

    #endregion

    #region State

    /// <summary>WheelCollider reference on this GameObject.</summary>
    private WheelCollider wheel;

    /// <summary>Vehicle rigidbody (searched in parents).</summary>
    private Rigidbody carRb;

    /// <summary>Runtime instance of the smoke particle system.</summary>
    private ParticleSystem smokeInstance;

    /// <summary>Runtime instance of the skid trail.</summary>
    private TrailRenderer trailInstance;

    /// <summary>Audio source used for skid sound.</summary>
    private AudioSource skidAudio;

    /// <summary>Accumulator used to convert fractional emission into whole particles.</summary>
    private float particlesLeftOver;

    #endregion

    #region Unity Methods

    /// <summary>Cache component references and create the skid audio source.</summary>
    private void Awake()
    {
        wheel = GetComponent<WheelCollider>();
        carRb = GetComponentInParent<Rigidbody>();

        skidAudio = gameObject.AddComponent<AudioSource>();
        skidAudio.outputAudioMixerGroup = skidAudioMixerGroup;
        skidAudio.clip = skidAudioClip;
    }

    /// <summary>Instantiate optional smoke and trail prefabs and configure audio.</summary>
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
    /// Physics tick: evaluates wheel slip and updates smoke emission, trail state, and skid audio.
    /// Stops effects when wheel is not grounded or slip is below threshold.
    /// </summary>
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

    /// <summary>Disables trail emission and stops skid audio playback.</summary>
    private void StopEffects()
    {
        if (trailInstance != null)
            trailInstance.emitting = false;

        if (skidAudio != null && skidAudio.isPlaying)
            skidAudio.Stop();
    }

    #endregion
}
