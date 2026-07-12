using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI entry representing a single saved level row.
/// </summary>
/// <remarks>
/// @ingroup ui_levels
/// @ingroup game_data
/// @brief Displays saved level metadata and exposes UI actions for selecting, renaming, exporting, and deleting the level.
///
/// This component is used by saved-level lists. Each entry receives a <see cref="GameDataManager.LevelData"/>
/// object through <see cref="SetUp(GameDataManager.LevelData)"/> and uses it to populate the level name,
/// best time, day/night icon, and optional preview.
///
/// Unity lifecycle note:
/// - MonoBehaviour constructors are not used for normal Unity initialization.
/// - Runtime setup should be done through <see cref="SetUp(GameDataManager.LevelData)"/>.
/// </remarks>
public class LevelEntry : MonoBehaviour
{
	#region Inspector

	/// <summary>
	/// Text label that shows the best time for this level or <c>No Time</c> when no valid time exists.
	/// </summary>
	[Tooltip("Text label used to display the best saved time for this level, or 'No Time' if none exists.")]
	[SerializeField] private TMP_Text levelTime;

	/// <summary>
	/// Input field used to display and edit the level name.
	/// </summary>
	[Tooltip("Input field used to display and edit the saved level name.")]
	[SerializeField] private TMP_InputField levelName;

	/// <summary>
	/// UI image that displays either the day or night icon for the level.
	/// </summary>
	[Tooltip("Raw image used to display whether this level is a day or night track.")]
	[SerializeField] private RawImage dayNightIconImage;

	/// <summary>
	/// Texture displayed when the level is marked as a day track.
	/// </summary>
	[Tooltip("Icon texture displayed for day tracks.")]
	[SerializeField] private Texture dayIcon;

	/// <summary>
	/// Texture displayed when the level is marked as a night track.
	/// </summary>
	[Tooltip("Icon texture displayed for night tracks.")]
	[SerializeField] private Texture nightIcon;

	#endregion

	#region Runtime State

	/// <summary>
	/// Backing data for this entry, including the level map and its saved best time.
	/// </summary>
	/// <remarks>
	/// This value may be null until <see cref="SetUp(GameDataManager.LevelData)"/> is called.
	/// </remarks>
	public GameDataManager.LevelData levelData;

	#endregion

	#region UI Callbacks

	/// <summary>
	/// Selects this entry's level as the current level in <see cref="GameDataManager"/>.
	/// </summary>
	/// <remarks>
	/// Intended to be called from a UI button or clickable level-entry area.
	/// </remarks>
	public void LevelSelected()
	{
		//Debug.Log($"Selecting level from game data.");
		GameDataManager.Instance.SelectingLevelMap(levelData.LevelMap);
	}

	/// <summary>
	/// Exports this entry's level data to the system clipboard.
	/// </summary>
	/// <remarks>
	/// Delegates serialization and clipboard handling to <see cref="ImportExportManager"/>.
	/// </remarks>
	public void ExportLevelButton()
	{
		ImportExportManager.ExportLevelToClipboard(levelData.LevelMap);
	}

	/// <summary>
	/// Removes this entry's level from the saved level list.
	/// </summary>
	/// <remarks>
	/// Delegates removal to <see cref="GameDataManager"/>.
	/// </remarks>
	public void RemoveLevel()
	{
		// Debug.Log($"Removing level from game data. Level map: \n {levelData.levelMap.Tithisles}");
		GameDataManager.Instance.RemoveLevel(levelData.LevelMap);
	}

	/// <summary>
	/// Updates the stored name of this entry's level.
	/// </summary>
	/// <param name="name">New level name entered in the UI.</param>
	/// <remarks>
	/// Intended to be connected to the level-name input field value-change event.
	/// </remarks>
	public void OnLevelNameChange(string name)
	{
		levelData.LevelMap.Name = name;
	}

	#endregion

	#region Initialization

	/// <summary>
	/// Initializes this UI entry from saved level data and refreshes all displayed values.
	/// </summary>
	/// <param name="levelData">Level data containing the level map and best saved time to display.</param>
	/// <remarks>
	/// The method updates:
	/// - The optional level preview, if a <see cref="LevelPreviewer"/> component is attached.
	/// - The best-time label.
	/// - The editable level-name field.
	/// - The day/night icon.
	/// </remarks>
	public void SetUp(GameDataManager.LevelData levelData)
	{
		this.levelData = levelData;

		// Attempt to show a visual preview, if a LevelPreviewer component is present.
		_ = GetComponent<LevelPreviewer>()?.ShowPreviewAsync(levelData.LevelMap);

		// Update displayed time.
		if (levelData.Time < float.MaxValue)
		{
			TimeSpan timeSpan = TimeSpan.FromSeconds(levelData.Time);
			levelTime.text = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
		}
		else
		{
			levelTime.text = "No Time";
		}

		levelName.text = levelData.LevelMap.Name;

		if (levelData.LevelMap.IsDayTrack)
			dayNightIconImage.texture = dayIcon;
		else
			dayNightIconImage.texture = nightIcon;
	}

	#endregion
}