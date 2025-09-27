using UnityEngine;
using TMPro;

/// <summary>
/// Simple UI slider label updater: formats the incoming float value as a zero-padded
/// two-digit integer and writes it into a <see cref="TMP_Text"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Lightweight helper intended for Options/Settings sliders.
///
/// Behavior:
/// - Rounds <paramref name="newValue"/> to the nearest integer, clamps to an int range implicitly,
///   and formats as "00", "01", … "99", "100", etc. via <see cref="int.ToString(string)"/> with "00".
///
/// Dependencies:
/// - TextMeshProUGUI (<see cref="TMP_Text"/>).
///
/// Threading:
/// - Unity main thread only (driven by UI events).
///
/// Usage:
/// - Wire the slider's OnValueChanged(float) event to <see cref="OnChange(float)"/>.
/// - Assign <see cref="_label"/> in the Inspector to the target TMP text element.
/// </remarks>
public class SliderScript : MonoBehaviour
{
    /// <summary>
    /// Target label that displays the formatted slider value.
    /// </summary>
    [SerializeField] private TMP_Text _label;

    /// <summary>
    /// UI callback: updates <see cref="_label"/> with the rounded value formatted as two digits.
    /// </summary>
    /// <param name="newValue">Raw slider value (float) from the UI event.</param>
    public void OnChange(float newValue)
    {
        if (_label != null)
        {
            _label.text = Mathf.RoundToInt(newValue).ToString("00");
        }

    }
}
