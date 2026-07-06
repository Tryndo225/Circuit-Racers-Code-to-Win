using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls vehicle light groups and state-dependent light effects.
/// </summary>
/// <remarks>
/// @ingroup car_ctrl
/// @brief Manages front, day, rear, reverse, and brake lights with optional fading and shared-bulb restoration.
///
/// The controller supports:
/// - Toggling front, day, and rear lights as a group.
/// - Fading normal light groups on and off.
/// - Instantly enabling brake and reverse lights.
/// - Reusing rear light bulbs for brake or reverse lights.
/// - Restoring rear light color and intensity when shared brake/reverse lights are deactivated.
///
/// Lists may be empty and may contain null entries. Null lights are skipped.
///
/// Threading:
/// - Unity main thread only.
/// - Uses Unity coroutines for fading.
/// </remarks>
public class LightsController : MonoBehaviour
{
	#region Inspector: Lights Configuration

	[Header("Lights Configuration")]
	/// <summary>
	/// Intensity of front lights when enabled.
	/// </summary>
	[Tooltip("Intensity of the front lights when turned on.")]
	public float frontLightsIntensity;

	/// <summary>
	/// Color of front lights when enabled.
	/// </summary>
	[Tooltip("Color of the front lights when turned on.")]
	public Color frontLightsColor;

	/// <summary>
	/// Front light components controlled by this script.
	/// </summary>
	[Tooltip("List of front lights.")]
	public List<Light> frontLights;

	/// <summary>
	/// Intensity of day lights when enabled.
	/// </summary>
	[Tooltip("Intensity of the day lights when turned on.")]
	public float dayLightsIntensity;

	/// <summary>
	/// Color of day lights when enabled.
	/// </summary>
	[Tooltip("Color of the day lights when turned on.")]
	public Color dayLightsColor;

	[Header("List of daylights")]
	/// <summary>
	/// Daylight components controlled by this script.
	/// </summary>
	[Tooltip("List of day lights.")]
	public List<Light> dayLights;

	/// <summary>
	/// Intensity of rear lights when enabled.
	/// </summary>
	[Tooltip("Intensity of the rear lights when turned on.")]
	public float rearLightsIntensity;

	/// <summary>
	/// Color of rear lights when enabled.
	/// </summary>
	[Tooltip("Color of the rear lights when turned on.")]
	public Color rearLightsColor;

	/// <summary>
	/// Rear light components controlled by this script.
	/// </summary>
	[Tooltip("List of rear lights.")]
	public List<Light> rearLights;

	/// <summary>
	/// Intensity of reverse lights when reversing.
	/// </summary>
	[Tooltip("Intensity of the reverse lights when turned on.")]
	public float reverseLightsIntensity;

	/// <summary>
	/// Color of reverse lights when reversing.
	/// </summary>
	[Tooltip("Color of the reverse lights when turned on.")]
	public Color reverseLightsColor;

	/// <summary>
	/// Reverse light components controlled by this script.
	/// </summary>
	[Tooltip("List of reverse lights.")]
	public List<Light> reverseLights;

	/// <summary>
	/// Intensity of brake lights when braking.
	/// </summary>
	[Tooltip("Intensity of the brake lights when turned on.")]
	public float brakeLightsIntensity;

	/// <summary>
	/// Color of brake lights when braking.
	/// </summary>
	[Tooltip("Color of the brake lights when turned on.")]
	public Color brakeLightsColor;

	/// <summary>
	/// Brake light components controlled by this script.
	/// </summary>
	[Tooltip("List of brake lights.")]
	public List<Light> brakeLights;

	[Header("Fade Settings")]
	/// <summary>
	/// Duration in seconds used when fading normal light groups.
	/// </summary>
	[Tooltip("Duration in seconds for fading lights on and off.")]
	public float fadeDuration;

	[Header("Initial State")]
	/// <summary>
	/// Whether front, day, and rear lights should be enabled on start.
	/// </summary>
	[Tooltip("If enabled, front, day, and rear lights are turned on at Start.")]
	public bool startLightsOn;

	#endregion

	#region State

	/// <summary>
	/// Current toggle state for front, day, and rear lights.
	/// </summary>
	private bool _lightsToggle;

	/// <summary>
	/// Current internal brake-light state.
	/// </summary>
	private bool _isBraking;

	/// <summary>
	/// Current internal reverse-light state.
	/// </summary>
	private bool _isReversing;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Initializes normal lights to the configured initial state.
	/// </summary>
	private void Start()
	{
		SetLights(startLightsOn);
		_lightsToggle = startLightsOn;
	}

	#endregion

	#region Public API

	/// <summary>
	/// Toggles front, day, and rear light groups.
	/// </summary>
	public void ToggleLights()
	{
		_lightsToggle = !_lightsToggle;
		SetLights(_lightsToggle);
	}

	/// <summary>
	/// Enables or disables front, day, and rear lights.
	/// </summary>
	/// <param name="active">Whether the normal light groups should be enabled.</param>
	public void SetLights(bool active)
	{
		SetFrontLights(active);
		SetDayLights(active);
		SetRearLights(active);
	}

	/// <summary>
	/// Enables or disables day lights using fade and the configured day-light color.
	/// </summary>
	/// <param name="active">Whether day lights should be enabled.</param>
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
	/// Enables or disables front lights using fade and the configured front-light color.
	/// </summary>
	/// <param name="active">Whether front lights should be enabled.</param>
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
	/// Enables or disables rear lights using fade and the configured rear-light color.
	/// </summary>
	/// <param name="active">Whether rear lights should be enabled.</param>
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
	/// Enables or disables reverse lights instantly.
	/// </summary>
	/// <param name="active">Whether reverse lights should be enabled.</param>
	/// <remarks>
	/// If a reverse light is also listed as a rear light, turning reverse off restores the rear-light
	/// color and intensity when the normal lights are currently enabled.
	/// </remarks>
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
	/// Enables or disables brake lights instantly.
	/// </summary>
	/// <param name="active">Whether brake lights should be enabled.</param>
	/// <remarks>
	/// If a brake light is also listed as a rear light, turning braking off restores the rear-light
	/// color and intensity when the normal lights are currently enabled.
	/// </remarks>
	public void SetBrakeLights(bool active)
	{
		if (_isBraking == active) return;

		_isBraking = active;

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
	/// Fades a light from its current intensity to a target intensity.
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