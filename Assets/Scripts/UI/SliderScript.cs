using TMPro;
using UnityEngine;

/// <summary>
/// Updates a TMP label from a slider value.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Formats an incoming float value as a zero-padded integer and writes it into a <see cref="TMP_Text"/>.
///
/// Behavior:
/// - Rounds the incoming slider value to the nearest integer.
/// - Formats the rounded value using the <c>00</c> format string.
/// - Writes the result to the assigned label if the label reference exists.
///
/// Dependencies:
/// - TextMeshPro label component through <see cref="TMP_Text"/>.
///
/// Usage:
/// - Assign <see cref="_label"/> in the Inspector.
/// - Wire a slider's OnValueChanged(float) event to <see cref="OnChange(float)"/>.
/// </remarks>
public class SliderScript : MonoBehaviour
{
	/// <summary>
	/// Target label that displays the formatted slider value.
	/// </summary>
	[Tooltip("Text label that displays the rounded slider value.")]
	[SerializeField] private TMP_Text _label;

	/// <summary>
	/// Updates <see cref="_label"/> with the rounded value formatted as two digits.
	/// </summary>
	/// <param name="newValue">Raw float value received from the UI event.</param>
	public void OnChange(float newValue)
	{
		if (_label != null)
		{
			_label.text = Mathf.RoundToInt(newValue).ToString("00");
		}

	}
}