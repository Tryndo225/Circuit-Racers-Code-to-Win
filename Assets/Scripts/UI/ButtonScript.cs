using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#region Generic Button Handler

/// <summary>
/// Generic UI button driver that delegates its click behavior to a pluggable <see cref="ButtonType"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Keeps scene/UI logic decoupled by holding a serialized strategy object that implements <see cref="ButtonType.Action"/>.
///
/// Usage:
/// - Assign a <see cref="ButtonType"/> (e.g., <see cref="ChangeSceneButton"/>, <see cref="GenerateLevelButton"/>) in the Inspector.
/// - Hook the Unity Button's OnClick() to <see cref="OnButtonClick"/> (or call it from a UnityEvent).
///
/// Threading:
/// - Unity main thread for <see cref="OnButtonClick"/>; specific strategies may spawn background work (see <see cref="GenerateLevelButton"/>).
/// </remarks>
public class ButtonScript : MonoBehaviour
{
	/// <summary>
	/// Serialized behavior object that performs the action when the button is clicked.
	/// </summary>
	[SerializeReference] private ButtonType properties;

	/// <summary>
	/// Read/write accessor for the underlying button behavior.
	/// </summary>
	public ButtonType Properties
	{
		get => properties;
		set => properties = value;
	}

	/// <summary>
	/// Invoked by the UI Button. Validates configuration and executes <see cref="ButtonType.Action"/>.
	/// </summary>
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
/// Abstract strategy for a button action. Concrete implementations encapsulate the behavior
/// (scene change, level selection, generation, etc.) without coupling UI to systems.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Implementations must remain safe to call from the main thread. Long-running work should be
/// pushed to background tasks and marshalled back to main (see <see cref="GenerateLevelButton"/>).
/// </remarks>
[Serializable]
public abstract class ButtonType
{
	/// <summary>
	/// Execute the action represented by this strategy.
	/// </summary>
	public abstract void Action();
}

#region Concrete Button Types

/// <summary>
/// Strategy: change to a specific scene via <see cref="SceneManagement"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Requires a valid <see cref="_sceneToLoad"/> reference. Logs an error if unassigned.
/// </remarks>
[Serializable]
public class ChangeSceneButton : ButtonType
{
	/// <summary>Parameterless ctor for serialization.</summary>
	public ChangeSceneButton()
	{ }

	/// <summary>
	/// Target scene (helper) to load when executed.
	/// </summary>
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
/// Strategy: set the currently selected <see cref="LevelMap"/> in <see cref="GameDataManager"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Use when a UI item represents a particular level slot.
/// </remarks>
[Serializable]
public class SelectLevelButton : ButtonType
{
	/// <summary>Parameterless ctor for serialization.</summary>
	public SelectLevelButton()
	{ }

	/// <summary>
	/// Construct with a concrete level reference.
	/// </summary>
	/// <param name="levelMap">Level to select.</param>
	public SelectLevelButton(LevelMap levelMap)
	{
		_levelMap = levelMap;
	}

	/// <summary>Level to select when executed.</summary>
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
/// Strategy: remove a level from <see cref="GameDataManager"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Intended for list items with a "delete" action.
/// </remarks>
[Serializable]
public class RemoveLevelButton : ButtonType
{
	/// <summary>Parameterless ctor for serialization.</summary>
	public RemoveLevelButton()
	{ }

	/// <summary>Level to remove when executed.</summary>
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
/// Strategy: procedurally generate a level (optionally circuit), add checkpoints,
/// and display it in a popup preview.
/// </summary>
/// <remarks>
/// @ingroup ui @ingroup level_gen
/// Workflow:
/// - Reads UI sliders/toggles to update generation parameters.
/// - Runs generation and checkpoint placement on background threads via <see cref="Task.Run"/>.
/// - On success, shows the result in <see cref="LevelPopUp"/> attached to <see cref="popUp"/>.
///
/// Threading:
/// - Kicks off CPU work on background threads; UI interactions remain on main.
///
/// Invariants:
/// - <see cref="popUp"/> must reference a GameObject with a <see cref="LevelPopUp"/> component.
/// </remarks>
[Serializable]
public class GenerateLevelButton : ButtonType
{
	#region References (UI)

	/// <summary>Popup container that owns a <see cref="LevelPopUp"/> for previewing the generated map.</summary>
	[Header("References")]
	[SerializeField] private GameObject popUp;

	/// <summary>Slider for size in tiles.</summary>
	[SerializeField] private Slider sizeSlider;

	/// <summary>Toggle to generate circuits (loop) instead of point-to-point tracks.</summary>
	[SerializeField] private Toggle createCircuitsToggle;

	#endregion References (UI)

	#region Generation Settings (defaults)

	/// <summary>Whether to attempt creating closed-loop tracks.</summary>
	[Header("Generation Settings")]
	[SerializeField] public bool createCircuits = true;

	/// <summary>Grid size in tiles.</summary>
	[SerializeField] public int size = 50;

	/// <summary>Total carving steps.</summary>
	[SerializeField, ReadOnly] private int stepCount = 20;

	/// <summary>Computed step size (derived from area and count).</summary>
	[SerializeField, ReadOnly] private int stepSize;

	/// <summary>Max attempts per step to find a valid target.</summary>
	[SerializeField] private int maxAttemps = 1000;

	#endregion Generation Settings (defaults)

	/// <summary>Parameterless ctor for serialization.</summary>
	public GenerateLevelButton()
	{ }

	/// <summary>
	/// Pulls latest values from the bound UI widgets into the generation fields.
	/// </summary>
	private void UpdateValues()
	{
		if (sizeSlider != null)
			size = Mathf.RoundToInt(sizeSlider.value);
		if (createCircuitsToggle != null)
			createCircuits = createCircuitsToggle.isOn;
	}

	/// <summary>
	/// Execute: generate a level, add checkpoints, and show the preview popup.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="Task.Run(Func{Task})"/> to keep the main thread responsive.
	/// Logs/guards against missing references and exceptions.
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
				return LevelGenerator.GenerateLevel(size, size, createCircuits, stepCount, stepSize, maxAttemps, SeedFactory.Next());
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
			Debug.LogError($"Level generation failed: {ex}");
		}
	}
}

/// <summary>
/// Strategy: clear all levels from <see cref="GameDataManager"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Useful for dev/testing or a "reset progression" button.
/// </remarks>
[Serializable]
public class ClearLevelsButton : ButtonType
{
	/// <summary>Parameterless ctor for serialization.</summary>
	public ClearLevelsButton()
	{ }

	/// <inheritdoc/>
	public override void Action()
	{
		GameDataManager.Instance.ClearLevels();
	}
}

/// <summary>
/// Strategy: request a scene change to the gameplay level currently selected in <see cref="GameDataManager"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Calls <see cref="GameDataManager.GoToSelectedLevel"/> which validates selection and performs the transition.
/// </remarks>
[Serializable]
public class GoToSelectedLevel : ButtonType
{
	/// <summary>Parameterless ctor for serialization.</summary>
	public GoToSelectedLevel()
	{ }

	/// <inheritdoc/>
	public override void Action()
	{
		GameDataManager.Instance.GoToSelectedLevel();
	}
}

[Serializable]
public class ClosePopUpButton : ButtonType
{
	/// <summary>Parameterless ctor for serialization.</summary>
	public ClosePopUpButton()
	{ }
	/// <summary>Reference to the popup GameObject to close.</summary>
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

[Serializable]
public class OpenPopUpButton : ButtonType
{
	/// <summary>Parameterless ctor for serialization.</summary>
	public OpenPopUpButton()
	{ }
	/// <summary>Reference to the popup GameObject to open.</summary>
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

[Serializable]
public class ImportLevelFromClipboardButton : OpenPopUpButton
{
	/// <summary>Parameterless ctor for serialization.</summary>
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

[Serializable]
public class ImportLevelButton : ClosePopUpButton
{
	[SerializeField] private TMP_InputField textField;
	/// <summary>Parameterless ctor for serialization.</summary>
	public ImportLevelButton()
	{ }
	/// <inheritdoc/>
	public override void Action()
	{
		if (!ImportExportManager.TryImportLevelFromString(textField.text))
			Debug.LogError("Failed to import level: invalid format.");
		else
		{
			base.Action();
		}
	}
}

[Serializable]
public class ReplayButton : ChangeSceneButton
{
	public override void Action()
	{
		if (GameDataManager.Instance.CurrentGameData.GetBestReplay(GameDataManager.Instance.CurrentLevelMap) != null)
			base.Action();
		else
			NotificationManager.Instance.Show("No available replay.", Color.red);
	}
}


[Serializable]
public class EditButton : ChangeSceneButton
{
	public override void Action()
	{
		if (GameDataManager.Instance.CurrentLevelMap != null)
			base.Action();
		else
			NotificationManager.Instance.Show("No level selected.", Color.red);
	}
}

[Serializable]
public class NotificationButton : ButtonType
{
	[SerializeField] private Color notificationColor;
	[SerializeField] private string notificationText;

	public override void Action()
	{
		NotificationManager.Instance.Show(notificationText, notificationColor);
	}
}

[Serializable]
public class GoPlayButtom : ButtonType
{
	[SerializeField] private SceneAssetHelper _sceneToLoadDay;
	[SerializeField] private SceneAssetHelper _sceneToLoadNight;

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