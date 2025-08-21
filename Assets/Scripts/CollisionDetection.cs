using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CollisionDetection : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip[] crashClips;

    [SerializeField, Range(0f, 1f)] private float minimumVolume = 0.1f;

    [Header("Severity from Impulse")]
    [Tooltip("Impulse (N·s) that just starts to be audible.")]
    [SerializeField] private float minImpulse = 200f;

    [Tooltip("Impulse that maps to full volume.")]
    [SerializeField] private float maxImpulse = 3000f;

    [Header("Sound Shaping")]
    [Range(0f, 1f)] public float baseVolume = 1f;

    public AnimationCurve volumeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.15f;
    [SerializeField] private float cooldown = 0.15f;

    [Header("Filtering")]
    [Tooltip("Ignore these layers when deciding to play a crash.")]
    [SerializeField] private LayerMask ignoreLayers;

    private new Rigidbody rigidbody;
    private float nextAllowedTime;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision c)
    {
        if (Time.time < nextAllowedTime) return;

        if (ignoreLayers.Contains(c.gameObject.layer)) return;

        if (crashClips == null || crashClips.Length == 0) return;

        float severity = GetSeverity01(c);

        if (severity <= 0f) return;

        var clip = crashClips[Random.Range(0, crashClips.Length)];
        float volume = volumeCurve.Evaluate(severity) * baseVolume;
        volume = Mathf.Max(volume, minimumVolume);
        float pitch = Mathf.Lerp(minPitch, maxPitch, severity) * Random.Range(0.98f, 1.02f);

        SoundManager.instance.PlaySFXClip(clip, transform, volume, pitch);

        nextAllowedTime = Time.time + cooldown;
    }

    private float GetSeverity01(Collision c)
    {
        float j = c.impulse.magnitude;
        Debug.Log($"Collision impulse: {j} N·s");
        if (j > 0.01f)
        {
            return Mathf.InverseLerp(minImpulse, maxImpulse, j);
        }
        return 0f;
    }
}