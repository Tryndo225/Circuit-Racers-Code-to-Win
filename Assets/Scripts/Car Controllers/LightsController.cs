using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls vehicle lights (front, day, rear, reverse, brake) with optional fading,
/// color/intensity presets, and runtime toggles. Reverse and brake lights can share bulbs
/// with rear lights and will restore rear light settings when deactivated.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @thread Unity main thread (coroutines run on the main thread).
/// @invariant Lists may be empty but can contain nulls; null entries are skipped.
/// </remarks>
public class LightsController : MonoBehaviour
{
    #region Inspector: Lights Configuration

    [Header("Lights Configuration")]
    /// <summary>Intensity of front lights when turned on.</summary>
    [Tooltip("Intensity of the front lights when turned on.")]
    public float frontLightsIntensity;

    /// <summary>Color of front lights when turned on.</summary>
    [Tooltip("Color of the front lights when turned on.")]
    public Color frontLightsColor;

    /// <summary>List of front light components.</summary>
    [Tooltip("List of front lights")]
    public List<Light> frontLights;

    /// <summary>Intensity of day lights when turned on.</summary>
    [Tooltip("Intensity of the day lights when turned on.")]
    public float dayLightsIntensity;

    /// <summary>Color of day lights when turned on.</summary>
    [Tooltip("Color of the day lights when turned on.")]
    public Color dayLightsColor;

    [Header("List of daylights")]
    /// <summary>List of daylight components.</summary>
    public List<Light> dayLights;

    /// <summary>Intensity of rear lights when turned on.</summary>
    [Tooltip("Intensity of the rear lights when turned on.")]
    public float rearLightsIntensity;

    /// <summary>Color of rear lights when turned on.</summary>
    [Tooltip("Color of the rear lights when turned on.")]
    public Color rearLightsColor;

    /// <summary>List of rear light components.</summary>
    [Tooltip("List of rear lights")]
    public List<Light> rearLights;

    /// <summary>Intensity of reverse lights when engaged.</summary>
    [Tooltip("Intensity of the reverse lights when turned on.")]
    public float reverseLightsIntensity;

    /// <summary>Color of reverse lights when engaged.</summary>
    [Tooltip("Color of the reverse lights when turned on.")]
    public Color reverseLightsColor;

    /// <summary>List of reverse light components.</summary>
    [Tooltip("List of reverse lights")]
    public List<Light> reverseLights;

    /// <summary>Intensity of brake lights when braking.</summary>
    [Tooltip("Intensity of the brake lights when turned on.")]
    public float brakeLightsIntensity;

    /// <summary>Color of brake lights when braking.</summary>
    [Tooltip("Color of the brake lights when turned on.")]
    public Color brakeLightsColor;

    /// <summary>List of brake light components.</summary>
    [Tooltip("List of brake lights")]
    public List<Light> brakeLights;

    [Header("Fade Settings")]
    /// <summary>Duration for fading lights on and off (seconds).</summary>
    [Tooltip("Duration for fading lights on and off.")]
    public float fadeDuration;

    [Header("Initial State")]
    /// <summary>If true, front/day/rear lights are enabled on Start.</summary>
    public bool startLightsOn;

    #endregion

    #region State

    /// <summary>Internal toggle for front/day/rear lights.</summary>
    private bool _lightsToggle;

    /// <summary>Internal state for brake lights.</summary>
    private bool _isBreaking;

    /// <summary>Internal state for reverse lights.</summary>
    private bool _isReversing;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Initializes lights to the configured initial state.
    /// </summary>
    private void Start()
    {
        SetLights(startLightsOn);
        _lightsToggle = startLightsOn;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Toggles front/day/rear light sets as a group.
    /// </summary>
    public void ToggleLights()
    {
        _lightsToggle = !_lightsToggle;
        SetLights(_lightsToggle);
    }

    /// <summary>
    /// Enables or disables front, day, and rear lights using fade.
    /// </summary>
    /// <param name="active">True to enable, false to disable.</param>
    public void SetLights(bool active)
    {
        SetFrontLights(active);
        SetDayLights(active);
        SetRearLights(active);
    }

    /// <summary>
    /// Enables or disables day lights using fade and applies configured color.
    /// </summary>
    public void SetDayLights(bool active)
    {
        foreach (var light in dayLights)
        {
            if (light != null)
            {
                light.color = dayLightsColor;
                StartCoroutine(FadeLight(light, active ? dayLightsIntensity : 0f));
            }
        }
    }

    /// <summary>
    /// Enables or disables front lights using fade and applies configured color.
    /// </summary>
    public void SetFrontLights(bool active)
    {
        foreach (var light in frontLights)
        {
            if (light != null)
            {
                light.color = frontLightsColor;
                StartCoroutine(FadeLight(light, active ? frontLightsIntensity : 0f));
            }
        }
    }

    /// <summary>
    /// Enables or disables rear lights using fade and applies configured color.
    /// </summary>
    public void SetRearLights(bool active)
    {
        foreach (var light in rearLights)
        {
            if (light != null)
            {
                light.color = rearLightsColor;
                StartCoroutine(FadeLight(light, active ? rearLightsIntensity : 0f));
            }
        }
    }

    /// <summary>
    /// Enables or disables reverse lights instantly. If a lamp is shared with rear lights,
    /// it restores the rear light look when reverse is turned off and main lights are on.
    /// </summary>
    /// <param name="active">True when reversing.</param>
    public void SetReverseLights(bool active)
    {
        if (_isReversing == active) return;

        _isReversing = active;

        foreach (var light in reverseLights)
        {
            if (light != null)
            {
                if (rearLights.Contains(light) && _lightsToggle && !active)
                {
                    light.color = rearLightsColor;
                    light.intensity = rearLightsIntensity;
                }
                else
                {
                    light.color = reverseLightsColor;
                    light.intensity = active ? reverseLightsIntensity : 0f;
                }
            }
        }

        if (!_isReversing)
            SetLights(_lightsToggle);
    }

    /// <summary>
    /// Enables or disables brake lights instantly. If a lamp is shared with rear lights,
    /// it restores the rear light look when braking stops and main lights are on.
    /// </summary>
    /// <param name="active">True when braking.</param>
    public void SetBrakeLights(bool active)
    {
        if (_isBreaking == active) return;

        _isBreaking = active;

        foreach (var light in brakeLights)
        {
            if (light != null)
            {
                if (rearLights.Contains(light) && _lightsToggle && !active)
                {
                    light.color = rearLightsColor;
                    light.intensity = rearLightsIntensity;
                }
                else
                {
                    light.color = brakeLightsColor;
                    light.intensity = active ? brakeLightsIntensity : 0f;
                }
            }
        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Fades a light's intensity from its current value to targetIntensity over fadeDuration seconds.
    /// </summary>
    /// <param name="light">Light to fade.</param>
    /// <param name="targetIntensity">Target intensity.</param>
    /// <returns>Coroutine enumerator.</returns>
    private IEnumerator FadeLight(Light light, float targetIntensity)
    {
        float startIntensity = light.intensity;
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            light.intensity = Mathf.Lerp(startIntensity, targetIntensity, elapsedTime / fadeDuration);
            yield return null;
        }

        light.intensity = targetIntensity;
    }

    #endregion
}
