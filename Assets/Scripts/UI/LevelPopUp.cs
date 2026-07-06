using UnityEngine;

/// <summary>
/// Simple popup controller that previews a generated <see cref="LevelMap"/> and lets the user
/// accept (add to game data) or dismiss it.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Shows/hides a preview panel and forwards the map to <see cref="GameDataManager"/> when accepted.
///
/// Responsibilities:
/// - Toggle a visual container on/off for the preview.
/// - Drive a co-located <see cref="LevelPreviewer"/> to render the given <see cref="LevelMap"/>.
/// - Add the accepted map to the persistent list via <see cref="GameDataManager.AddLevel(LevelMap)"/>.
///
/// Threading:
/// - Unity main thread only (standard MonoBehaviour lifecycle).
///
/// Usage:
/// - Call <see cref="ShowMap(LevelMap)"/> with a generated map to display the popup preview.
/// - Call <see cref="KeepMap()"/> to persist the currently previewed map; <see cref="HideMap()"/> to close.
/// </remarks>
public class LevelPopUp : MonoBehaviour
{
	/// <summary>
	/// Visual container hosting the preview UI and a <see cref="LevelPreviewer"/> component.
	/// </summary>
	[Tooltip("Visual container that holds the popup UI and the LevelPreviewer used to show the generated level.")]
	[SerializeField] private GameObject visuals;

	/// <summary>
	/// The map currently previewed by this popup (null when hidden).
	/// </summary>
	private LevelMap _levelMap;

	/// <summary>
	/// Unity Start: ensure popup is hidden initially.
	/// </summary>
	private void Start()
	{
		visuals.SetActive(false);
	}

	/// <summary>
	/// Displays the popup and renders a preview of <paramref name="map"/>.
	/// </summary>
	/// <param name="map">Level to preview.</param>
	/// <remarks>
	/// Attempts to find a <see cref="LevelPreviewer"/> on <see cref="visuals"/> and uses
	/// <see cref="LevelPreviewer.ShowPreviewAsync(LevelMap)"/> to generate the thumbnail.
	/// </remarks>
	public void ShowMap(LevelMap map)
	{
		_levelMap = map;
		visuals.SetActive(true);
		var levelViewer = visuals.GetComponent<LevelPreviewer>();
		if (levelViewer != null)
		{
			_ = levelViewer.ShowPreviewAsync(map);
		}
	}

	/// <summary>
	/// Hides the popup and clears any previously rendered preview.
	/// </summary>
	public void HideMap()
	{
		visuals.GetComponent<LevelPreviewer>().Clear();
		_levelMap = null;
		visuals.SetActive(false);
	}

	/// <summary>
	/// Accepts the currently previewed level and adds it to the game's saved list,
	/// then hides the popup.
	/// </summary>
	/// <remarks>
	/// No-op if there is no map currently previewed.
	/// </remarks>
	public void KeepMap()
	{
		if (_levelMap != null)
		{
			GameDataManager.Instance.AddLevel(_levelMap);
		}
		HideMap();
	}
}