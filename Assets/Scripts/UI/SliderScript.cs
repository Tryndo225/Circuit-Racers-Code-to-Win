using UnityEngine;
using TMPro;

public class SliderScript : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;

    public void OnChange(float newValue)
    {
        if (_label != null)
        {
            _label.text = Mathf.RoundToInt(newValue).ToString("00");
        }

    }
}
