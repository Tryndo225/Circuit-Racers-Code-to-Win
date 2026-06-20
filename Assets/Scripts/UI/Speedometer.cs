using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Speedometer : MonoBehaviour
{
	[SerializeField] private Slider speedometerGauge;
	[SerializeField] private TextMeshProUGUI speedText;

	private DriveTrainController controller_;
	private float maxSpeed = 250f;

	private void SetUp()
	{
		maxSpeed = controller_.GetMaxSpeed();
		speedometerGauge.maxValue = maxSpeed;
		speedometerGauge.minValue = 0;
	}

	private void Awake()
	{
		speedometerGauge.interactable = false;
		speedometerGauge.value = 0;
	}


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

		float currentSpeed = controller_.GetSpeed();

		if (currentSpeed > maxSpeed)
		{
			currentSpeed = maxSpeed;
		}

		speedometerGauge.value = currentSpeed;
		speedText.text = currentSpeed.ToString("000");
	}
}
