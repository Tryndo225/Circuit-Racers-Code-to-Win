using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LightsController : MonoBehaviour
{
    [Header("Lights Configuration")]
    [Tooltip("Intensity of the front lights when turned on.")]
    public float frontLightsIntensity;

    [Tooltip("Color of the front lights when turned on.")]
    public Color frontLightsColor;

    [Tooltip("List of front lights")]
    public List<Light> frontLights;

    [Tooltip("Intensity of the day lights when turned on.")]
    public float dayLightsIntensity;

    [Tooltip("Color of the day lights when turned on.")]
    public Color dayLightsColor;

    [Header("List of daylights")]
    public List<Light> dayLights;

    [Tooltip("Intensity of the rear lights when turned on.")]
    public float rearLightsIntensity;

    [Tooltip("Color of the rear lights when turned on.")]
    public Color rearLightsColor;

    [Tooltip("List of rear lights")]
    public List<Light> rearLights;

    [Tooltip("Intensity of the reverse lights when turned on.")]
    public float reverseLightsIntensity;

    [Tooltip("Color of the reverse lights when turned on.")]
    public Color reverseLightsColor;

    [Tooltip("List of reverse lights")]
    public List<Light> reverseLights;

    [Tooltip("Intensity of the brake lights when turned on.")]
    public float brakeLightsIntensity;

    [Tooltip("Color of the brake lights when turned on.")]
    public Color brakeLightsColor;

    [Tooltip("List of brake lights")]
    public List<Light> brakeLights;

    [Header("Fade Settings")]
    [Tooltip("Duration for fading lights on and off.")]
    public float fadeDuration;

    [Header("Initial State")]
    public bool startLightsOn;

    private bool _lightsToggle;
    private bool _isBreaking;
    private bool _isReversing;

    private void Start()
    {
        SetLights(startLightsOn);
        _lightsToggle = startLightsOn;
    }

    public void ToggleLights()
    {
        _lightsToggle = !_lightsToggle;
        SetLights(_lightsToggle);
    }

    public void SetLights(bool active)
    {
        SetFrontLights(active);
        SetDayLights(active);
        SetRearLights(active);
    }

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
}