using UnityEngine;

/// <summary>
/// Plays crash SFX based on collision impulse, with cooldown, volume curve, pitch jitter,
/// and layer filtering.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @invariant A <see cref="Rigidbody"/> component is present on the same GameObject.
/// @thread Runs on Unity main thread (physics callback).
/// @req SoundManager.Instance.PlaySFXClip is available and initialized.
/// @req LayerMask.Contains(int layer) exists (helper/extension) or replace with a bitmask check.
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
public class CollisionDetection : MonoBehaviour
{
    #region Inspector: Clips

    /// <summary>Set of crash audio clips to pick from at runtime.</summary>
    [Header("Clips")]
    [SerializeField] private AudioClip[] crashClips;

    /// <summary>Lower bound for volume so very small impacts are still audible.</summary>
    [SerializeField, Range(0f, 1f)] private float minimumVolume = 0.1f;

    #endregion

    #region Inspector: Severity from Impulse

    [Header("Severity from Impulse")]
    /// <summary>Impulse (N*s) that just starts to be audible (maps to 0 on the curve).</summary>
    [Tooltip("Impulse (N*s) that just starts to be audible.")]
    [SerializeField] private float minImpulse = 200f;

    /// <summary>Impulse that maps to full volume (1 on the curve).</summary>
    [Tooltip("Impulse that maps to full volume.")]
    [SerializeField] private float maxImpulse = 3000f;

    #endregion

    #region Inspector: Sound Shaping

    [Header("Sound Shaping")]
    /// <summary>Global gain applied on top of the evaluated volume curve.</summary>
    [Range(0f, 1f)] public float baseVolume = 1f;

    /// <summary>Volume response as a function of normalized severity in [0,1].</summary>
    public AnimationCurve volumeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    /// <summary>Minimum pitch used for pitch interpolation by severity.</summary>
    [SerializeField] private float minPitch = 0.95f;

    /// <summary>Maximum pitch used for pitch interpolation by severity.</summary>
    [SerializeField] private float maxPitch = 1.15f;

    /// <summary>Minimum time (s) between two crash sounds.</summary>
    [SerializeField] private float cooldown = 0.15f;

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
    /// Physics callback: computes collision severity from impulse, shapes volume and pitch,
    /// and plays a crash clip if above threshold and not on cooldown.
    /// </summary>
    /// <param name="c">Collision data provided by Unity.</param>
    /// <remarks>
    /// Skips if: cooldown active, other object is on an ignored layer,
    /// no clips configured, or computed severity <= 0.
    /// </remarks>
    private void OnCollisionEnter(Collision c)
    {
        if (Time.time < nextAllowedTime) return;

        if (ignoreLayers.Contains(c.gameObject.layer)) return;

        if (crashClips == null || crashClips.Length == 0) return;

        float severity = GetSeverity01(c);
        if (severity <= 0f) return;

        var clip = crashClips[Random.Range(0, crashClips.Length)];

        // Volume shaping: curve(severity) * base, clamped by minimumVolume.
        float volume = volumeCurve.Evaluate(severity) * baseVolume;
        volume = Mathf.Max(volume, minimumVolume);

        // Pitch shaping: lerp by severity with slight randomization.
        float pitch = Mathf.Lerp(minPitch, maxPitch, severity) * Random.Range(0.98f, 1.02f);

        SoundManager.Instance.PlaySFXClip(clip, transform, volume, pitch);

        nextAllowedTime = Time.time + cooldown;
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Maps the collision impulse magnitude (N*s) into a normalized severity in [0,1].
    /// </summary>
    /// <param name="c">Collision data.</param>
    /// <returns>
    /// Normalized severity where 0 <= result <= 1; returns 0 if impulse is negligible.
    /// </returns>
    /// <complexity>O(1)</complexity>
    private float GetSeverity01(Collision c)
    {
        float j = c.impulse.magnitude;
        Debug.Log("Collision impulse: " + j + " N*s");
        if (j > 0.01f)
        {
            return Mathf.InverseLerp(minImpulse, maxImpulse, j);
        }
        return 0f;
    }

    #endregion
}
