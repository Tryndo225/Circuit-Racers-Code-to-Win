using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelEditorManager : MonoBehaviour
{
	[SerializeField] private LevelPreviewer levelPreviewer;
	[SerializeField] private RectTransform levelGridRect;
	[SerializeField] private TMP_InputField levelNameField;
	[SerializeField] private Toggle circuitToggle;
	[SerializeField] private Toggle nightToggle;
	[SerializeField] private Slider lapSlider;
	[SerializeField] private GameObject lapSliderParent;


	private LevelMap editedLevel_;
	private LevelMap.LevelTileTypes currentFill_;
	private bool startSelected_ = false;
	private bool finishSelected_ = false;

	void Start()
	{
		SetUp();
	}

	private void SetUp()
	{
		editedLevel_ = GameDataManager.Instance.CreateEditableCopy(GameDataManager.Instance.CurrentLevelMap);
		StartCoroutine(ShowPreviewAfterLayout());
		levelNameField.text = editedLevel_.Name;
		circuitToggle.isOn = editedLevel_.Circuit;
		lapSlider.value = editedLevel_.Laps;
		lapSliderParent.SetActive(editedLevel_.Circuit);
		nightToggle.isOn = (editedLevel_.IsDayTrack == false);
	}

	public void OnLevelGridClick(BaseEventData eventData)
	{
		if (editedLevel_ == null || editedLevel_.Tiles == null)
		{
			return;
		}

		PointerEventData pointerData = eventData as PointerEventData;

		if (pointerData == null)
		{
			return;
		}

		bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(levelGridRect, pointerData.position, pointerData.pressEventCamera, out Vector2 localPoint);

		if (!converted)
		{
			return;
		}

		Rect rect = levelGridRect.rect;

		if (!rect.Contains(localPoint))
		{
			return;
		}

		float normalizedX = (localPoint.x - rect.xMin) / rect.width;
		float normalizedY = (localPoint.y - rect.yMin) / rect.height;

		int tileX = Mathf.FloorToInt(normalizedX * editedLevel_.Width);
		int tileY = Mathf.FloorToInt((1f - normalizedY) * editedLevel_.Height);

		tileX = Mathf.Clamp(tileX, 0, editedLevel_.Width - 1);
		tileY = Mathf.Clamp(tileY, 0, editedLevel_.Height - 1);

		if (startSelected_)
		{
			editedLevel_.StartPoint = new Coordinates(tileX, tileY);
			UpdateLevelPreview();
			return;
		}

		if (finishSelected_)
		{
			editedLevel_.FinishPoint = new Coordinates(tileX, tileY);
			UpdateLevelPreview();
			return;
		}

		editedLevel_.Tiles[tileX, tileY] = (int)currentFill_;

		Debug.Log($"Changed tile [{tileX}, {tileY}] to {(int)currentFill_}");
		UpdateLevelPreview();
	}

	private void UpdateLevelPreview()
	{
		_ = levelPreviewer.ShowPreviewAsync(editedLevel_);
	}

	private IEnumerator ShowPreviewAfterLayout()
	{
		yield return null;

		Canvas.ForceUpdateCanvases();

		_ = levelPreviewer.ShowPreviewAsync(editedLevel_);
	}

	public void OnGrassSelected()
	{
		currentFill_ = LevelMap.LevelTileTypes.Grass;
		startSelected_ = false;
		finishSelected_ = false;
	}

	public void OnTrackSelected()
	{
		currentFill_ = LevelMap.LevelTileTypes.Track;
		startSelected_ = false;
		finishSelected_ = false;
	}

	public void OnCheckPointSelected()
	{
		currentFill_ = LevelMap.LevelTileTypes.CP;
		startSelected_ = false;
		finishSelected_ = false;
	}

	public void OnStartSelected()
	{
		startSelected_ = true;
		finishSelected_ = false;
	}

	public void OnFinishSelected()
	{
		startSelected_ = false;
		finishSelected_ = true;
	}

	public void SetCircuit(bool isCircuit)
	{
		editedLevel_.Circuit = isCircuit;
		lapSliderParent.SetActive(isCircuit);

	}

	public void SetDayNight(bool isNight)
	{
		editedLevel_.IsDayTrack = !isNight;
	}

	public void SetLaps(float laps)
	{
		editedLevel_.Laps = Mathf.RoundToInt(laps);
	}

	public void OnLevelNameChange(string name)
	{
		editedLevel_.Name = name;
		Debug.Log($"Name is: {name}");
	}

	public void AutomaticCPGeneration()
	{
		LevelCheckPointMaker.GenerateCheckPoints(editedLevel_);
		UpdateLevelPreview();
	}

	public void SaveEditedLevelAsNew()
	{
		if (!LevelMapValidator.Validate(editedLevel_))
		{
			Debug.LogWarning("Edited level is invalid.");
			NotificationManager.Instance.Show("Edited level is invalid.", Color.red);
			return;
		}

		GameDataManager.Instance.AddLevel(editedLevel_);
		NotificationManager.Instance.Show("New Level Saved.", Color.green);
		GameDataManager.Instance.SelectingLevelMap(editedLevel_);
		SetUp();
	}

	public void ReplaceEditedLevel()
	{
		if (!LevelMapValidator.Validate(editedLevel_))
		{
			Debug.LogWarning("Edited level is invalid.");
			NotificationManager.Instance.Show("Edited level is invalid.", Color.red);
			return;
		}

		GameDataManager.Instance.ReplaceLevel(GameDataManager.Instance.CurrentLevelMap, editedLevel_);
		NotificationManager.Instance.Show("Level Replaced.", Color.green);
		GameDataManager.Instance.SelectingLevelMap(editedLevel_);
		SetUp();
	}
}
