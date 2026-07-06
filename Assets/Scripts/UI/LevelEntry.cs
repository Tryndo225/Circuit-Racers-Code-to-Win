using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI entry representing a single saved level row. Displays the best time,
/// allows selecting the level for play, and removing it from saved data.
/// </summary>
/// <remarks>
/// @ingroup ui_levels
/// @thread Unity main thread.
/// @invariant <see cref="levelTime"/> (if assigned) is used only for UI text updates.
/// @invariant <see cref="levelData"/> may be null until <see cref="SetUp(GameDataManager.LevelData)"/> is called.
/// </remarks>
public class LevelEntry : MonoBehaviour
{
	#region Inspector

	/// <summary>
	/// Text label that shows the best time for this level (formatted as HH:MM:SS) or "No Time".
	/// </summary>
	[SerializeField] private TMP_Text levelTime;
	[SerializeField] private TMP_InputField levelName;
	[SerializeField] private RawImage dayNightIconImage;
	[SerializeField] private Texture dayIcon;
	[SerializeField] private Texture nightIcon;

	#endregion

	#region Runtime State

	/// <summary>
	/// Backing data for this entry (map + best time), as provided by the game data manager.
	/// </summary>
	public GameDataManager.LevelData levelData;

	#endregion

	#region Constructors

	/// <summary>
	/// Constructor accepting a <see cref="GameDataManager.LevelData"/>. 
	/// Note: Unity does not invoke MonoBehaviour constructors at runtime; prefer calling <see cref="SetUp(GameDataManager.LevelData)"/>.
	/// </summary>
	/// <param name="data">Level data to associate with this entry.</param>
	public LevelEntry(GameDataManager.LevelData data)
	{
		levelData = data;
	}

	#endregion

	#region UI Callbacks

	/// <summary>
	/// UI callback: selects this level for play by notifying <see cref="GameDataManager"/>.
	/// </summary>
	public void LevelSelected()
	{
		//Debug.Log($"Selecting level from game data.");
		GameDataManager.Instance.SelectingLevelMap(levelData.LevelMap);
	}

	public void ExportLevelButton()
	{
		ImportExportManager.ExportLevelToClipboard(levelData.LevelMap);
	}

	/// <summary>
	/// UI callback: removes this level from the saved list via <see cref="GameDataManager"/>.
	/// </summary>
	public void RemoveLevel()
	{
		// Debug.Log($"Removing level from game data. Level map: \n {levelData.LevelMap.Tithisles}");
		GameDataManager.Instance.RemoveLevel(levelData.LevelMap);
	}

	public void OnLevelNameChange(string name)
	{
		levelData.LevelMap.Name = name;
	}

	#endregion

	#region Initialization

	/// <summary>
	/// Initializes the entry with the given <paramref name="levelData"/> and updates the preview & time label.
	/// </summary>
	/// <param name="levelData">Level data (map + best time) to display.</param>
	public void SetUp(GameDataManager.LevelData levelData)
	{
		this.levelData = levelData;

		// Attempt to show a visual preview, if a LevelPreviewer component is present.
		GetComponent<LevelPreviewer>()?.ShowPreviewAsync(levelData.LevelMap);

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
