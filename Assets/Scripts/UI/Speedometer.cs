using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI speedometer that displays the current speed as both a gauge value and a numeric text label.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Reads speed from the selected source and mirrors it into a slider-based gauge.
///
/// The component supports reading speed from either a live <see cref="DriveTrainController"/> or from
/// <see cref="ReplayPreviewer"/> playback, depending on <see cref="source"/>.
///
/// The slider is used as a visual gauge only and is made non-interactable in <see cref="Awake"/>.
/// </remarks>
public class Speedometer : MonoBehaviour
{
	/// <summary>
	/// Slider used as the visual speed gauge.
	/// </summary>
	[Tooltip("Slider used as the visual speedometer gauge.")]
	[SerializeField] private Slider speedometerGauge;

	/// <summary>
	/// Text label that displays the numeric speed value.
	/// </summary>
	[Tooltip("Text label that displays the current speed as a three-digit value.")]
	[SerializeField] private TextMeshProUGUI speedText;

	/// <summary>
	/// Source from which the speedometer reads the current speed.
	/// </summary>
	[Tooltip("Source used to read the current speed: live drivetrain or replay playback.")]
	[SerializeField] private SpeedSource source;

	/// <summary>
	/// Available sources for speedometer data.
	/// </summary>
	public enum SpeedSource
	{
		/// <summary>
		/// Read speed from the active <see cref="DriveTrainController"/>.
		/// </summary>
		DriveTrain,

		/// <summary>
		/// Read speed from the active <see cref="ReplayPreviewer"/>.
		/// </summary>
		Replay
	}

	/// <summary>
	/// Cached drivetrain controller used when <see cref="source"/> is <see cref="SpeedSource.DriveTrain"/>.
	/// </summary>
	private DriveTrainController controller_;

	/// <summary>
	/// Maximum speed shown by the gauge.
	/// </summary>
	private float maxSpeed = 250f;

	/// <summary>
	/// Configures the gauge range from the current drivetrain controller.
	/// </summary>
	private void SetUp()
	{
		maxSpeed = controller_.GetMaxSpeed();
		speedometerGauge.maxValue = maxSpeed;
		speedometerGauge.minValue = 0;
	}

	/// <summary>
	/// Initializes the gauge as a read-only display with zero speed.
	/// </summary>
	private void Awake()
	{
		speedometerGauge.interactable = false;
		speedometerGauge.value = 0;
	}


	/// <summary>
	/// Updates the speedometer from the selected speed source once per frame.
	/// </summary>
	/// <remarks>
	/// The drivetrain controller is resolved lazily because it may not be available when this component awakens.
	/// Displayed speed is clamped to <see cref="maxSpeed"/> and formatted as a three-digit value.
	/// </remarks>
	void Update()
	{
		if (controller_ == null)
		{
			controller_ = FindFirstObjectByType<DriveTrainController>();

			if (controller_ != null)
			{
				SetUp();
			}
			else
			{
				return;
			}
		}

		float currentSpeed = 0;
		if (source == SpeedSource.DriveTrain)
			currentSpeed = controller_.GetSpeed();
		else if (source == SpeedSource.Replay)
			currentSpeed = ReplayPreviewer.Instance.GetCurrentSpeed();

		if (currentSpeed > maxSpeed)
		{
			currentSpeed = maxSpeed;
		}

		speedometerGauge.value = currentSpeed;
		speedText.text = currentSpeed.ToString("000");
	}
}