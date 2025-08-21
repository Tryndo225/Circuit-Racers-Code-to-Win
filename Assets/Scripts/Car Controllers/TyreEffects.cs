using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(WheelCollider))]
public class TyreEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem smokePrefab;

    [SerializeField] private AudioClip skidAudioClip;
    [SerializeField] private AudioMixerGroup skidAudioMixerGroup;

    [Header("Tuning")]
    [Tooltip("Slip amount above which smoke starts.")]
    [SerializeField][Range(0f, 5f)] private float slipThreshold = 0.25f;

    [Tooltip("Particles per second at max slip.")]
    [SerializeField] private float maxEmissionRate = 60f;

    [Tooltip("Don’t smoke when basically stationary (m/s).")]
    [SerializeField] private float minSpeedForSmoke = 0f;

    [Tooltip("Offsets smoke slightly above the ground to avoid z-fighting.")]
    [SerializeField] private float groundOffset = 0.02f;

    [Header("Skid Trail Settings")]
    [SerializeField] private Material trailMaterial;

    [SerializeField] private float trailTime = 10f;
    [SerializeField] private float trailWidth = 0.08f;
    [SerializeField] private float trailMinVertexDistance = 0.03f;

    private WheelCollider wheel;
    private TrailRenderer skidTrail;
    private Rigidbody carRb;
    private ParticleSystem smokeInstance;
    private ParticleSystem.Particle[] singleParticleBuffer = new ParticleSystem.Particle[1];

    private AudioSource skidAudio;

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
            var emission = smokeInstance.emission;
            emission.rateOverTime = 0f;
        }

        if (skidTrail == null)
        {
            var go = new GameObject("SkidTrail");
            go.transform.SetParent(transform, false);
            skidTrail = go.AddComponent<TrailRenderer>();
        }
        ConfigureSkidTrail();

        if (skidAudio != null)
        {
            skidAudio.loop = true;
            skidAudio.playOnAwake = false;
        }
    }

    private void ConfigureSkidTrail()
    {
        skidTrail.time = trailTime;
        skidTrail.minVertexDistance = trailMinVertexDistance;
        skidTrail.widthCurve = AnimationCurve.Constant(0f, 1f, trailWidth);
        skidTrail.textureMode = LineTextureMode.Tile;
        skidTrail.alignment = LineAlignment.View;
        skidTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        skidTrail.receiveShadows = false;
        skidTrail.sortingOrder = 10;

        if (trailMaterial != null) skidTrail.material = trailMaterial;

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        skidTrail.colorGradient = grad;

        skidTrail.emitting = false;
    }

    private void FixedUpdate()
    {
        if (smokeInstance == null && skidTrail == null && skidAudio == null) return;

        bool grounded = wheel.isGrounded;
        float speed = carRb ? carRb.linearVelocity.magnitude : 0f;

        if (!grounded || speed < minSpeedForSmoke)
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

                    float t = Mathf.InverseLerp(slipThreshold, 1f, Mathf.Clamp01(slipMag));
                    int emitCount = Mathf.CeilToInt(t * maxEmissionRate * Time.fixedDeltaTime);
                    if (emitCount > 0) smokeInstance.Emit(emitCount);
                }

                if (skidTrail != null)
                {
                    skidTrail.transform.position = at;
                    skidTrail.emitting = true;
                }

                if (skidAudio != null)
                {
                    if (!skidAudio.isPlaying) skidAudio.Play();
                    skidAudio.volume = Mathf.Clamp01(slipMag);
                }

                return;
            }
        }

        StopEffects();
    }

    private void StopEffects()
    {
        if (skidTrail != null) skidTrail.emitting = false;
        if (skidAudio != null && skidAudio.isPlaying) skidAudio.Stop();
    }
}