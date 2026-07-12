using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#region Generic Button Handler

/// <summary>
/// Generic UI button driver that delegates its click behavior to a pluggable <see cref="ButtonType"/> strategy.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Connects reusable Unity UI buttons to serialized action objects.
///
/// This component is intended to be attached to a Unity UI button object. Instead of hard-coding
/// behavior directly into the button component, the actual action is stored in <see cref="properties"/>
/// as a serialized <see cref="ButtonType"/> implementation.
///
/// Typical usage:
/// - Attach this component to a UI button object.
/// - Assign a concrete <see cref="ButtonType"/> strategy in the Inspector.
/// - Hook the Unity Button OnClick() event to <see cref="OnButtonClick"/>.
///
/// This keeps menu buttons, level buttons, popup buttons, import buttons, and scene-transition buttons
/// visually configurable while sharing the same click-handling entry point.
/// </remarks>
public class ButtonScript : MonoBehaviour
{
	/// <summary>
	/// Serialized strategy object that performs the actual action when the button is clicked.
	/// </summary>
	[Tooltip("Serialized button action executed when this UI button is clicked.")]
	[SerializeReference] private ButtonType properties;

	/// <summary>
	/// Gets or assigns the serialized button behavior strategy.
	/// </summary>
	/// <remarks>
	/// This property allows code-generated UI elements to configure the button action at runtime,
	/// while still supporting Inspector-based assignment through <see cref="properties"/>.
	/// </remarks>
	public ButtonType Properties
	{
		get => properties;
		set => properties = value;
	}

	/// <summary>
	/// Invoked by the Unity UI Button OnClick event.
	/// </summary>
	/// <remarks>
	/// The method validates that a concrete <see cref="ButtonType"/> was assigned before executing it.
	/// Missing configuration is reported as a warning rather than an exception so that incorrectly
	/// configured menu buttons do not crash the scene.
	/// </remarks>
	public void OnButtonClick()
	{
		if (properties == null)
		{
			Debug.LogWarning("Button properties are not set.");
			return;
		}
		properties.Action();
	}
}

#endregion Generic Button Handler

#region Button Types

/// <summary>
/// Abstract base class for serialized UI button actions.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Defines the strategy interface used by <see cref="ButtonScript"/>.
///
/// Concrete subclasses encapsulate individual button behaviors, such as changing scenes,
/// selecting or removing levels, generating tracks, opening popups, importing levels,
/// showing notifications, or starting replay/gameplay scenes.
///
/// Because these objects are triggered from Unity UI events, implementations should remain safe
/// to call from the Unity main thread. Long-running work should be moved to background tasks where
/// appropriate, as done by <see cref="GenerateLevelButton"/>.
/// </remarks>
[Serializable]
public abstract class ButtonType
{
	/// <summary>
	/// Executes the action represented by this button strategy.
	/// </summary>
	public abstract void Action();
}

#region Concrete Button Types

/// <summary>
/// Button strategy that changes to a configured scene through <see cref="SceneManagement"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup scene_mgmt
///
/// The target scene is provided through <see cref="_sceneToLoad"/>. If the scene helper is missing,
/// the strategy logs an error and does not attempt a transition.
/// </remarks>
[Serializable]
public class ChangeSceneButton : ButtonType
{
	/// <summary>
	/// Creates an empty scene-change strategy for Unity serialization.
	/// </summary>
	public ChangeSceneButton()
	{ }

	/// <summary>
	/// Scene helper describing the target scene to load when this button action is executed.
	/// </summary>
	[Tooltip("Scene that will be loaded when this button is clicked.")]
	[SerializeField] private SceneAssetHelper _sceneToLoad;

	/// <inheritdoc/>
	public override void Action()
	{
		if (_sceneToLoad == null)
		{
			Debug.LogError("Scene to load is not assigned.");
			return;
		}
		SceneManagement.Instance.ChangeScene(_sceneToLoad);
	}
}

/// <summary>
/// Button strategy that stores a specific <see cref="LevelMap"/> as the currently selected level.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
///
/// This strategy is useful for saved-level lists, generated-level previews, and level-selection menus
/// where each UI item represents one concrete <see cref="LevelMap"/>.
/// </remarks>
[Serializable]
public class SelectLevelButton : ButtonType
{
	/// <summary>
	/// Creates an empty level-selection strategy for Unity serialization.
	/// </summary>
	public SelectLevelButton()
	{ }

	/// <summary>
	/// Creates a level-selection strategy for a concrete level.
	/// </summary>
	/// <param name="levelMap">Level that should become the currently selected level.</param>
	public SelectLevelButton(LevelMap levelMap)
	{
		_levelMap = levelMap;
	}

	/// <summary>
	/// Level that will be selected when this button action is executed.
	/// </summary>
	[Tooltip("Level that becomes the currently selected level when this button is clicked.")]
	[SerializeField, ReadOnly] private LevelMap _levelMap;

	/// <inheritdoc/>
	public override void Action()
	{
		if (_levelMap == null)
		{
			Debug.LogError("Level map is not assigned.");
			return;
		}
		GameDataManager.Instance.SelectingLevelMap(_levelMap);
	}
}

/// <summary>
/// Button strategy that removes a specific <see cref="LevelMap"/> from <see cref="GameDataManager"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
///
/// Intended for delete buttons in level-list UI entries. The level reference is usually assigned
/// when the UI row or level card is created.
/// </remarks>
[Serializable]
public class RemoveLevelButton : ButtonType
{
	/// <summary>
	/// Creates an empty level-removal strategy for Unity serialization.
	/// </summary>
	public RemoveLevelButton()
	{ }

	/// <summary>
	/// Level that will be removed when this button action is executed.
	/// </summary>
	[Tooltip("Level that will be removed from saved game data when this button is clicked.")]
	[SerializeField, ReadOnly] private LevelMap _levelMap;

	/// <inheritdoc/>
	public override void Action()
	{
		if (_levelMap == null)
		{
			Debug.LogError("Level map is not assigned.");
			return;
		}
		GameDataManager.Instance.RemoveLevel(_levelMap);
	}
}

/// <summary>
/// Button strategy that procedurally generates a level, creates checkpoints, and displays the result in a popup preview.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup level_gen
///
/// Workflow:
/// - Reads current size/circuit settings from the assigned UI controls.
/// - Generates a <see cref="LevelMap"/> using <see cref="LevelGenerator"/>.
/// - Adds checkpoint data using <see cref="LevelCheckPointMaker"/>.
/// - Displays the generated level through a <see cref="LevelPopUp"/> component on <see cref="popUp"/>.
///
/// Threading:
/// - Generation and checkpoint placement are run through <see cref="Task.Run"/> to avoid blocking the UI.
/// - Unity object access remains in the surrounding async UI action.
///
/// Requirements:
/// - <see cref="popUp"/> must reference a GameObject with a <see cref="LevelPopUp"/> component.
/// - <see cref="sizeSlider"/> and <see cref="createCircuitsToggle"/> are optional; if left unassigned,
///   the serialized default values are used.
/// </remarks>
[Serializable]
public class GenerateLevelButton : ButtonType
{
	#region References (UI)

	/// <summary>
	/// Popup container that owns the <see cref="LevelPopUp"/> used to preview the generated map.
	/// </summary>
	[Header("References")]
	[Tooltip("Popup GameObject containing the LevelPopUp component used to preview the generated level.")]
	[SerializeField] private GameObject popUp;

	/// <summary>
	/// UI slider that controls the generated level size in tiles.
	/// </summary>
	[Tooltip("Slider used to choose the generated level size in tiles.")]
	[SerializeField] private Slider sizeSlider;

	/// <summary>
	/// UI toggle that controls whether generated tracks should be circuits or point-to-point tracks.
	/// </summary>
	[Tooltip("Toggle deciding whether the generated level should be a closed circuit.")]
	[SerializeField] private Toggle createCircuitsToggle;

	#endregion References (UI)

	#region Generation Settings (defaults)

	/// <summary>
	/// Determines whether the generator should attempt to create a closed-loop circuit.
	/// </summary>
	[Header("Generation Settings")]
	[Tooltip("Whether the generator should create a closed circuit instead of a point-to-point track.")]
	[SerializeField] public bool createCircuits = true;

	/// <summary>
	/// Width and height of the generated square grid, measured in tiles.
	/// </summary>
	[Tooltip("Width and height of the generated square level grid, measured in tiles.")]
	[SerializeField] public int size = 50;

	/// <summary>
	/// Number of generation steps used by the level generator.
	/// </summary>
	[Tooltip("Number of carving/generation steps derived from the selected level size.")]
	[SerializeField, ReadOnly] private int stepCount = 20;

	/// <summary>
	/// Size of each generation step, derived from <see cref="size"/>.
	/// </summary>
	[Tooltip("Step size used by the generator, derived from the selected level size.")]
	[SerializeField, ReadOnly] private int stepSize;

	/// <summary>
	/// Maximum number of attempts used by the generator when searching for a valid step target.
	/// </summary>
	[Tooltip("Maximum number of attempts allowed when the generator searches for a valid target.")]
	[SerializeField] private int maxAttempts = 1000;

	#endregion Generation Settings (defaults)

	/// <summary>
	/// Creates an empty level-generation strategy for Unity serialization.
	/// </summary>
	public GenerateLevelButton()
	{ }

	/// <summary>
	/// Copies current values from the assigned UI widgets into the serialized generation fields.
	/// </summary>
	/// <remarks>
	/// Missing UI references are ignored so the strategy can still run with the serialized default values.
	/// </remarks>
	private void UpdateValues()
	{
		if (sizeSlider != null)
			size = Mathf.RoundToInt(sizeSlider.value);
		if (createCircuitsToggle != null)
			createCircuits = createCircuitsToggle.isOn;
	}

	/// <summary>
	/// Generates a level, generates its checkpoints, and opens the preview popup.
	/// </summary>
	/// <remarks>
	/// This is an async UI entry point. It catches and logs generation exceptions so errors do not
	/// propagate through Unity's UI event system.
	/// </remarks>
	public override async void Action()  // ok for top-level UI handlers
	{
		try
		{
			if (popUp == null)
			{
				Debug.LogError("Pop-up is not assigned.");
				return;
			}

			UpdateValues();

			stepCount = size;
			stepSize = Mathf.Max(1, size / 5);

			Debug.Log("Starting level generation...");
			var map = await Task.Run(() =>
			{
				return LevelGenerator.GenerateLevel(size, size, createCircuits, stepCount, stepSize, maxAttempts, SeedFactory.Next());
			});

			Debug.Log("Level generated, generating checkpoints...");

			await Task.Run(() =>
			{
				LevelCheckPointMaker.GenerateCheckPoints(map);
			});

			Debug.Log("Checkpoints generated, showing map...");

			if (map != null)
			{
				popUp.GetComponent<LevelPopUp>().ShowMap(map);
			}
		}
		catch (Exception ex)
		{
			NotificationManager.Instance.Show($"Level generation failed: {ex}");
		}
	}
}

/// <summary>
/// Button strategy that clears all saved levels from <see cref="GameDataManager"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
///
/// Primarily useful for reset buttons, development utilities, or testing menus.
/// </remarks>
[Serializable]
public class ClearLevelsButton : ButtonType
{
	/// <summary>
	/// Creates an empty clear-levels strategy for Unity serialization.
	/// </summary>
	public ClearLevelsButton()
	{ }

	/// <inheritdoc/>
	public override void Action()
	{
		GameDataManager.Instance.ClearLevels();
	}
}

/// <summary>
/// Button strategy that starts gameplay using the level currently selected in <see cref="GameDataManager"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
/// @ingroup scene_mgmt
///
/// The actual selection validation and transition behavior are delegated to
/// <see cref="GameDataManager.GoToSelectedLevel"/>.
/// </remarks>
[Serializable]
public class GoToSelectedLevel : ButtonType
{
	/// <summary>
	/// Creates an empty selected-level navigation strategy for Unity serialization.
	/// </summary>
	public GoToSelectedLevel()
	{ }

	/// <inheritdoc/>
	public override void Action()
	{
		GameDataManager.Instance.GoToSelectedLevel();
	}
}

/// <summary>
/// Button strategy that closes a configured popup GameObject.
/// </summary>
/// <remarks>
/// @ingroup ui
///
/// The popup field is protected so derived strategies can reuse the same closing behavior,
/// for example after a successful manual level import.
/// </remarks>
[Serializable]
public class ClosePopUpButton : ButtonType
{
	/// <summary>
	/// Creates an empty popup-closing strategy for Unity serialization.
	/// </summary>
	public ClosePopUpButton()
	{ }

	/// <summary>
	/// Popup GameObject that will be deactivated when this action is executed.
	/// </summary>
	[Tooltip("Popup GameObject that will be closed when this button is clicked.")]
	[SerializeField] protected GameObject popUp;

	/// <inheritdoc/>
	public override void Action()
	{
		if (popUp == null)
		{
			Debug.LogError("Pop-up is not assigned.");
			return;
		}
		popUp.SetActive(false);
	}
}

/// <summary>
/// Button strategy that opens a configured popup GameObject.
/// </summary>
/// <remarks>
/// @ingroup ui
///
/// The popup field is protected so derived strategies can reuse the same opening behavior,
/// for example when clipboard import fails and the manual import popup should be shown.
/// </remarks>
[Serializable]
public class OpenPopUpButton : ButtonType
{
	/// <summary>
	/// Creates an empty popup-opening strategy for Unity serialization.
	/// </summary>
	public OpenPopUpButton()
	{ }

	/// <summary>
	/// Popup GameObject that will be activated when this action is executed.
	/// </summary>
	[Tooltip("Popup GameObject that will be opened when this button is clicked.")]
	[SerializeField] protected GameObject popUp;

	/// <inheritdoc/>
	public override void Action()
	{
		if (popUp == null)
		{
			Debug.LogError("Pop-up is not assigned.");
			return;
		}
		popUp.SetActive(true);
	}
}

/// <summary>
/// Button strategy that attempts to import a level directly from the system clipboard.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
///
/// If clipboard import succeeds, the imported <see cref="LevelMap"/> is added to
/// <see cref="GameDataManager"/> and a confirmation notification is shown.
///
/// If clipboard import fails, the inherited <see cref="OpenPopUpButton.Action"/> behavior is used,
/// usually opening a manual import popup where the user can paste or edit the level text.
/// </remarks>
[Serializable]
public class ImportLevelFromClipboardButton : OpenPopUpButton
{
	/// <summary>
	/// Creates an empty clipboard-import strategy for Unity serialization.
	/// </summary>
	public ImportLevelFromClipboardButton() { }


	/// <inheritdoc/>
	public override void Action()
	{
		if (ImportExportManager.TryImportLevelFromClipboard(out var levelMap))
		{
			GameDataManager.Instance.AddLevel(levelMap);
			NotificationManager.Instance.Show("Level Imported From ClipBoard.", Color.lightBlue);
		}
		else
		{
			base.Action();
		}
	}
}

/// <summary>
/// Button strategy that imports a level from text entered into a TMP input field.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
///
/// The input text is passed to <see cref="ImportExportManager.TryImportLevelFromString"/>.
/// If the import succeeds, the popup is closed through <see cref="ClosePopUpButton.Action"/>.
/// If the import fails, an error is logged and the popup remains open.
/// </remarks>
[Serializable]
public class ImportLevelButton : ClosePopUpButton
{
	/// <summary>
	/// Input field containing the serialized level text to import.
	/// </summary>
	[Tooltip("Input field containing the serialized level text that should be imported.")]
	[SerializeField] private TMP_InputField textField;

	/// <summary>
	/// Creates an empty text-import strategy for Unity serialization.
	/// </summary>
	public ImportLevelButton()
	{ }

	/// <inheritdoc/>
	public override void Action()
	{
		if (!ImportExportManager.TryImportLevelFromString(textField.text))
			NotificationManager.Instance.Show("Failed to import level: invalid format.");
		else
		{
			base.Action();
		}
	}
}

/// <summary>
/// Button strategy that opens the replay scene only when the selected level has a saved best replay.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup replay_system
/// @ingroup scene_mgmt
///
/// This class extends <see cref="ChangeSceneButton"/> with replay availability validation.
/// If no best replay exists for the current level, the scene transition is blocked and a notification is shown.
/// </remarks>
[Serializable]
public class ReplayButton : ChangeSceneButton
{
	/// <inheritdoc/>
	public override void Action()
	{
		if (GameDataManager.Instance.CurrentGameData.GetBestReplay(GameDataManager.Instance.CurrentLevelMap) != null)
			base.Action();
		else
			NotificationManager.Instance.Show("No available replay.", Color.red);
	}
}


/// <summary>
/// Button strategy that opens the editor scene only when a level is currently selected.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
/// @ingroup scene_mgmt
///
/// This class extends <see cref="ChangeSceneButton"/> with selected-level validation.
/// If no level is selected, the scene transition is blocked and a notification is shown.
/// </remarks>
[Serializable]
public class EditButton : ChangeSceneButton
{
	/// <inheritdoc/>
	public override void Action()
	{
		if (GameDataManager.Instance.CurrentLevelMap != null)
			base.Action();
		else
			NotificationManager.Instance.Show("No level selected.", Color.red);
	}
}

/// <summary>
/// Button strategy that displays a configured notification message.
/// </summary>
/// <remarks>
/// @ingroup ui
///
/// The notification text and color are configured in the Inspector and passed to
/// <see cref="NotificationManager"/> when the button is executed.
/// </remarks>
[Serializable]
public class NotificationButton : ButtonType
{
	/// <summary>
	/// Color used when displaying the notification.
	/// </summary>
	[Tooltip("Color used for the notification shown by this button.")]
	[SerializeField] private Color notificationColor;

	/// <summary>
	/// Text displayed in the notification.
	/// </summary>
	[Tooltip("Text displayed in the notification shown by this button.")]
	[SerializeField] private string notificationText;

	/// <inheritdoc/>
	public override void Action()
	{
		NotificationManager.Instance.Show(notificationText, notificationColor);
	}
}

/// <summary>
/// Button strategy that starts gameplay by selecting either the day or night scene for the current level.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @ingroup game_data
/// @ingroup scene_mgmt
///
/// The class name intentionally keeps the existing spelling <c>GoPlayButton</c> to preserve Unity
/// serialized references.
///
/// The action checks <see cref="GameDataManager.CurrentLevelMap"/> and chooses a scene based on
/// <see cref="LevelMap.IsDayTrack"/>:
/// - Day tracks load <see cref="_sceneToLoadDay"/>.
/// - Night tracks load <see cref="_sceneToLoadNight"/>.
///
/// If no level is selected, or if the required scene helper is missing, the method reports the problem
/// and does not attempt a scene transition.
/// </remarks>
[Serializable]
public class GoPlayButton : ButtonType
{
	/// <summary>
	/// Scene helper used when the selected level is marked as a day track.
	/// </summary>
	[Tooltip("Scene loaded when the selected level is marked as a day track.")]
	[SerializeField] private SceneAssetHelper _sceneToLoadDay;

	/// <summary>
	/// Scene helper used when the selected level is marked as a night track.
	/// </summary>
	[Tooltip("Scene loaded when the selected level is marked as a night track.")]
	[SerializeField] private SceneAssetHelper _sceneToLoadNight;

	/// <inheritdoc/>
	public override void Action()
	{
		LevelMap levelMap = GameDataManager.Instance.CurrentLevelMap;
		if (levelMap == null)
		{
			NotificationManager.Instance.Show("No level selected.");
		}
		else if (levelMap.IsDayTrack)
		{
			if (_sceneToLoadDay == null)
			{
				Debug.LogError("Day scene to load is not assigned.");
				return;
			}
			SceneManagement.Instance.ChangeScene(_sceneToLoadDay);
		}
		else if (levelMap.IsDayTrack == false)
		{
			if (_sceneToLoadNight == null)
			{
				Debug.LogError("Night scene to load is not assigned.");
				return;
			}
			SceneManagement.Instance.ChangeScene(_sceneToLoadNight);
		}
	}
}

#endregion Concrete Button Types

#endregion Button Types