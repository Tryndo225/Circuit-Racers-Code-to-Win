using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(WheelCollider))]
public class TyreEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem smokePrefab;

    [SerializeField] private TrailRenderer skidTrailPrefab;

    [SerializeField] private AudioClip skidAudioClip;
    [SerializeField] private AudioMixerGroup skidAudioMixerGroup;

    [Header("Tuning")]
    [Tooltip("Slip amount above which smoke starts.")]
    [SerializeField][Range(0f, 5f)] private float slipThreshold = 0.25f;

    [Tooltip("Particles per second at max slip.")]
    [SerializeField] private float maxEmissionRatePerSecond = 60f;

    [SerializeField] private float maxEmissionRateAtSlip = 4f;

    [Tooltip("Offsets smoke slightly above the ground to avoid z-fighting.")]
    [SerializeField] private float groundOffset = 0.02f;

    private WheelCollider wheel;
    private Rigidbody carRb;
    private ParticleSystem smokeInstance;
    private TrailRenderer trailInstance;
    private AudioSource skidAudio;

    private float particlesLeftOver;

    private void Awake()
    {
        wheel = GetComponent<WheelCollider>();
        carRb = GetComponentInParent<Rigidbody>();

        skidAudio = gameObject.AddComponent<AudioSource>();
        skidAudio.outputAudioMixerGroup = skidAudioMixerGroup;
        skidAudio.clip = skidAudioClip;
    }

    private void Start()
    {
        if (smokePrefab != null)
        {
            smokeInstance = Instantiate(smokePrefab, transform);
        }

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

                if (smokeInstance != null)
                {
                    smokeInstance.transform.position = at;

                    Vector3 forward = (carRb && carRb.linearVelocity.sqrMagnitude > 0.01f) ? Vector3.ProjectOnPlane(carRb.linearVelocity, hit.normal).normalized : Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

                    smokeInstance.transform.rotation = Quaternion.LookRotation(forward, hit.normal);

                    float emissionRate = Mathf.InverseLerp(slipThreshold, maxEmissionRateAtSlip, slipMag);
                    particlesLeftOver += emissionRate * maxEmissionRatePerSecond * Time.fixedDeltaTime;

                    int emitCount = Mathf.FloorToInt(particlesLeftOver);

                    particlesLeftOver -= emitCount;

                    if (emitCount > 0)
                        smokeInstance.Emit(emitCount);
                }

                if (trailInstance != null)
                {
                    trailInstance.transform.position = at;
                    trailInstance.emitting = true;
                }

                if (skidAudio != null)
                {
                    skidAudio.volume = Mathf.Clamp01(slipMag / maxEmissionRateAtSlip);

                    if (!skidAudio.isPlaying)
                        skidAudio.Play();
                }

                return;
            }
        }

        StopEffects();
    }

    private void StopEffects()
    {
        if (trailInstance != null)
            trailInstance.emitting = false;

        if (skidAudio != null && skidAudio.isPlaying)
            skidAudio.Stop();
    }
}