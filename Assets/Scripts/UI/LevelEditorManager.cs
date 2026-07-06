using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages the level editor UI and applies user edits to an editable <see cref="LevelMap"/> copy.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup level_gen
/// @ingroup game_data
/// @brief Handles tile painting, start/finish placement, level metadata editing, checkpoint generation, and saving.
///
/// This component edits a temporary copy of the currently selected level. The edited copy is shown through
/// <see cref="LevelPreviewer"/> and can later be saved as a new level or used to replace the original level.
///
/// Main responsibilities:
/// - Initialize editor UI values from the selected <see cref="LevelMap"/>.
/// - Convert pointer clicks on the preview grid into tile coordinates.
/// - Paint grass, track, and checkpoint tiles.
/// - Move the start and finish positions.
/// - Update circuit, day/night, lap count, and level name values.
/// - Validate and save the edited level through <see cref="GameDataManager"/>.
/// </remarks>
public class LevelEditorManager : MonoBehaviour
{
	/// <summary>
	/// Preview renderer used to display the currently edited level.
	/// </summary>
	[Tooltip("Preview renderer used to display the level currently being edited.")]
	[SerializeField] private LevelPreviewer levelPreviewer;

	/// <summary>
	/// RectTransform of the clickable level preview grid.
	/// </summary>
	[Tooltip("RectTransform of the clickable level grid used to convert pointer positions into tile coordinates.")]
	[SerializeField] private RectTransform levelGridRect;

	/// <summary>
	/// Input field used to edit the level name.
	/// </summary>
	[Tooltip("Input field used to edit the level name.")]
	[SerializeField] private TMP_InputField levelNameField;

	/// <summary>
	/// Toggle that controls whether the edited level is a closed circuit.
	/// </summary>
	[Tooltip("Toggle that marks the edited level as a closed circuit.")]
	[SerializeField] private Toggle circuitToggle;

	/// <summary>
	/// Toggle that controls whether the edited level should use the night scene.
	/// </summary>
	[Tooltip("Toggle that marks the edited level as a night track.")]
	[SerializeField] private Toggle nightToggle;

	/// <summary>
	/// Slider used to choose the number of laps for circuit tracks.
	/// </summary>
	[Tooltip("Slider used to choose the number of laps for circuit tracks.")]
	[SerializeField] private Slider lapSlider;

	/// <summary>
	/// Parent GameObject containing the lap slider UI.
	/// </summary>
	[Tooltip("Parent object for the lap slider UI. It is shown only when the edited level is a circuit.")]
	[SerializeField] private GameObject lapSliderParent;


	/// <summary>
	/// Editable working copy of the currently selected level.
	/// </summary>
	private LevelMap editedLevel_;

	/// <summary>
	/// Tile type currently selected for painting on the grid.
	/// </summary>
	private LevelMap.LevelTileTypes currentFill_;

	/// <summary>
	/// Whether the next grid click should place the level start point.
	/// </summary>
	private bool startSelected_ = false;

	/// <summary>
	/// Whether the next grid click should place the level finish point.
	/// </summary>
	private bool finishSelected_ = false;

	/// <summary>
	/// Unity lifecycle method that initializes the editor UI and editable level copy.
	/// </summary>
	void Start()
	{
		SetUp();
	}

	/// <summary>
	/// Creates a fresh editable copy of the selected level and synchronizes the UI controls with its values.
	/// </summary>
	/// <remarks>
	/// The preview is delayed by one frame through <see cref="ShowPreviewAfterLayout"/> so Unity UI layout
	/// can finish before the level preview is rendered.
	/// </remarks>
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

	/// <summary>
	/// Handles clicks on the level grid and applies the currently selected edit operation.
	/// </summary>
	/// <param name="eventData">Pointer event data provided by the Unity EventSystem.</param>
	/// <remarks>
	/// The pointer position is converted from screen space into local coordinates of <see cref="levelGridRect"/>.
	/// The local point is then mapped to tile coordinates in <see cref="editedLevel_"/>.
	///
	/// Depending on the current editor mode, the click either:
	/// - Moves the start point.
	/// - Moves the finish point.
	/// - Paints the clicked tile using <see cref="currentFill_"/>.
	/// </remarks>
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

	/// <summary>
	/// Refreshes the visual preview for the currently edited level.
	/// </summary>
	private void UpdateLevelPreview()
	{
		_ = levelPreviewer.ShowPreviewAsync(editedLevel_);
	}

	/// <summary>
	/// Waits one frame for UI layout, forces canvas updates, and then renders the first level preview.
	/// </summary>
	/// <returns>Coroutine enumerator used by Unity.</returns>
	private IEnumerator ShowPreviewAfterLayout()
	{
		yield return null;

		Canvas.ForceUpdateCanvases();

		_ = levelPreviewer.ShowPreviewAsync(editedLevel_);
	}

	/// <summary>
	/// Selects grass as the current tile painting mode.
	/// </summary>
	public void OnGrassSelected()
	{
		currentFill_ = LevelMap.LevelTileTypes.Grass;
		startSelected_ = false;
		finishSelected_ = false;
	}

	/// <summary>
	/// Selects track as the current tile painting mode.
	/// </summary>
	public void OnTrackSelected()
	{
		currentFill_ = LevelMap.LevelTileTypes.Track;
		startSelected_ = false;
		finishSelected_ = false;
	}

	/// <summary>
	/// Selects checkpoint as the current tile painting mode.
	/// </summary>
	public void OnCheckPointSelected()
	{
		currentFill_ = LevelMap.LevelTileTypes.CP;
		startSelected_ = false;
		finishSelected_ = false;
	}

	/// <summary>
	/// Selects start-point placement mode for the next grid click.
	/// </summary>
	public void OnStartSelected()
	{
		startSelected_ = true;
		finishSelected_ = false;
	}

	/// <summary>
	/// Selects finish-point placement mode for the next grid click.
	/// </summary>
	public void OnFinishSelected()
	{
		startSelected_ = false;
		finishSelected_ = true;
	}

	/// <summary>
	/// Sets whether the edited level is a circuit and updates the lap slider visibility.
	/// </summary>
	/// <param name="isCircuit">Whether the level should be marked as a circuit.</param>
	public void SetCircuit(bool isCircuit)
	{
		editedLevel_.Circuit = isCircuit;
		lapSliderParent.SetActive(isCircuit);

	}

	/// <summary>
	/// Sets whether the edited level should be treated as a night track.
	/// </summary>
	/// <param name="isNight">Whether the level should use the night scene.</param>
	public void SetDayNight(bool isNight)
	{
		editedLevel_.IsDayTrack = !isNight;
	}

	/// <summary>
	/// Sets the lap count of the edited level from a slider value.
	/// </summary>
	/// <param name="laps">Slider value representing the desired number of laps.</param>
	public void SetLaps(float laps)
	{
		editedLevel_.Laps = Mathf.RoundToInt(laps);
	}

	/// <summary>
	/// Updates the edited level name.
	/// </summary>
	/// <param name="name">New level name entered in the UI.</param>
	public void OnLevelNameChange(string name)
	{
		editedLevel_.Name = name;
		Debug.Log($"Name is: {name}");
	}

	/// <summary>
	/// Automatically regenerates checkpoints for the edited level and refreshes the preview.
	/// </summary>
	public void AutomaticCPGeneration()
	{
		LevelCheckPointMaker.GenerateCheckPoints(editedLevel_);
		UpdateLevelPreview();
	}

	/// <summary>
	/// Validates the edited level and saves it as a new level entry.
	/// </summary>
	/// <remarks>
	/// If validation fails, the level is not saved and a notification is shown. On success, the edited
	/// level is added to <see cref="GameDataManager"/>, selected as the current level, and the editor is reset
	/// to edit a fresh copy.
	/// </remarks>
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

	/// <summary>
	/// Validates the edited level and replaces the currently selected level with it.
	/// </summary>
	/// <remarks>
	/// If validation fails, the original level is kept unchanged and a notification is shown. On success,
	/// <see cref="GameDataManager.ReplaceLevel"/> replaces the original selected level, the edited level becomes
	/// the current selection, and the editor is reset to edit a fresh copy.
	/// </remarks>
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