using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a small top-down preview image of a <see cref="LevelMap"/> into a UI <see cref="RawImage"/>.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Lightweight level thumbnail generator for menus and saved-level lists.
///
/// The preview draws:
/// - grass/background cells,
/// - road cells from <see cref="LevelMap.LevelTileTypes.Track"/>,
/// - checkpoint cells from <see cref="LevelMap.LevelTileTypes.CP"/>,
/// - start and finish markers.
///
/// Responsibilities:
/// - Choose a pixels-per-cell value based on the target rect width and device max texture size.
/// - Convert the tile grid into a CPU color buffer on a worker thread.
/// - Upload the buffer into a point-filtered <see cref="Texture2D"/> and assign it to the UI.
///
/// Threading:
/// - CPU buffer generation is done off the main thread through <c>Task.Run</c>.
/// - <see cref="Texture2D"/> creation and UI assignment must occur on the Unity main thread after the await.
///
/// Performance notes:
/// - Uses <see cref="TextureFormat.RGBA32"/> and <see cref="FilterMode.Point"/> for crisp pixel art.
/// - Avoid calling <see cref="ShowPreviewAsync(LevelMap)"/> every frame; call it only when data or size changes.
/// </remarks>
public class LevelPreviewer : MonoBehaviour
{
	#region Inspector : UI target

	[Header("UI target")]
	/// <summary>
	/// Destination UI image that displays the generated preview texture.
	/// </summary>
	[Tooltip("RawImage that displays the generated level preview texture.")]
	[SerializeField] private RawImage target;

	#endregion

	#region Inspector : Colors

	[Header("Look")]
	/// <summary>
	/// Background color for grass and other non-road cells.
	/// </summary>
	[Tooltip("Color used for grass/background tiles in the level preview.")]
	[SerializeField] private Color32 grass = Color.green;

	/// <summary>
	/// Base color for track and checkpoint cells before marker overlays are drawn.
	/// </summary>
	[Tooltip("Base color used for track and checkpoint tiles before marker overlays are drawn.")]
	[SerializeField] private Color32 road = Color.gray;

	/// <summary>
	/// Marker color for the level start cell.
	/// </summary>
	[Tooltip("Marker color used for the level start position.")]
	[SerializeField] private Color32 start = Color.lightGreen;

	/// <summary>
	/// Marker color for the level finish cell.
	/// </summary>
	[Tooltip("Marker color used for the level finish position.")]
	[SerializeField] private Color32 finish = Color.red;

	/// <summary>
	/// Marker color for checkpoint cells.
	/// </summary>
	[Tooltip("Marker color used for checkpoint tiles.")]
	[SerializeField] private Color32 checkPoint = Color.lightBlue;

	#endregion

	/// <summary>
	/// MonoBehaviour Start hook reserved for future initialization.
	/// </summary>
	private void Start()
	{
	}

	/// <summary>
	/// Removes any existing preview texture from <see cref="target"/>.
	/// </summary>
	public void Clear()
	{
		target.texture = null;
	}

	/// <summary>
	/// Asynchronously builds and displays a preview texture for the given level map.
	/// </summary>
	/// <param name="map">Level definition to visualize.</param>
	/// <remarks>
	/// The CPU color buffer is generated on a worker thread. The <see cref="Texture2D"/> is created
	/// and assigned on the Unity main thread after the asynchronous work completes.
	/// </remarks>
	public async Task ShowPreviewAsync(LevelMap map)
	{
		if (target == null) return;

		int ppc = Mathf.Max(1, Mathf.FloorToInt(target.rectTransform.rect.width / map.Width));

		int maxTex = SystemInfo.maxTextureSize;
		ppc = Mathf.Min(ppc, Mathf.Max(1, maxTex / Math.Max(map.Width, map.Height)));

		var result = await Task.Run(() =>
		{
			return BuildPreviewBuffer(map, ppc);
		});

		var tex = new Texture2D(result.texWidth, result.texHeight, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		tex.SetPixels32(result.buffer);
		tex.Apply(false, false);

		target.texture = tex;
	}

	/// <summary>
	/// Constructs a CPU color buffer for the preview.
	/// </summary>
	/// <param name="map">Level grid to render.</param>
	/// <param name="pixelsPerCell">Preview resolution in pixels per tile.</param>
	/// <returns>Color buffer and texture dimensions ready for <see cref="Texture2D.SetPixels32(Color32[])"/>.</returns>
	private (Color32[] buffer, int texWidth, int texHeight) BuildPreviewBuffer(LevelMap map, int pixelsPerCell)
	{
		const int TrackTile = (int)LevelMap.LevelTileTypes.Track;
		const int CheckpointTile = (int)LevelMap.LevelTileTypes.CP;

		int texWidth = map.Width * pixelsPerCell;
		int texHeight = map.Height * pixelsPerCell;

		var buffer = new Color32[texWidth * texHeight];

		for (int i = 0; i < buffer.Length; i++)
			buffer[i] = grass;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				int v = map.Tiles[x, y];

				if (v == TrackTile || v == CheckpointTile)
					FillCell(buffer, texWidth, texHeight, x, y, pixelsPerCell, road);

				if (v == CheckpointTile)
					DrawMarker(buffer, texWidth, texHeight, new Coordinates(x, y), pixelsPerCell, checkPoint);
			}
		}

		DrawMarker(buffer, texWidth, texHeight, map.StartPoint, pixelsPerCell, start);
		DrawMarker(buffer, texWidth, texHeight, map.FinishPoint, pixelsPerCell, finish);

		return (buffer, texWidth, texHeight);
	}

	/// <summary>
	/// Fills a single tile cell area with a solid color in the buffer.
	/// </summary>
	/// <param name="buffer">Destination color buffer.</param>
	/// <param name="texWidth">Total texture width in pixels.</param>
	/// <param name="texHeight">Total texture height in pixels.</param>
	/// <param name="cellX">Tile X coordinate.</param>
	/// <param name="cellY">Tile Y coordinate.</param>
	/// <param name="pixelsPerCell">Pixels per tile.</param>
	/// <param name="color">Fill color.</param>
	private static void FillCell(Color32[] buffer, int texWidth, int texHeight, int cellX, int cellY, int pixelsPerCell, Color32 color)
	{
		int pxX = cellX * pixelsPerCell;
		int pxY = cellY * pixelsPerCell;

		pxY = texHeight - pixelsPerCell - pxY;

		for (int dy = 0; dy < pixelsPerCell; dy++)
		{
			int row = (pxY + dy) * texWidth;
			int idx = row + pxX;
			for (int dx = 0; dx < pixelsPerCell; dx++)
			{
				buffer[idx + dx] = color;
			}
		}
	}

	/// <summary>
	/// Draws a square marker centered within a tile.
	/// </summary>
	/// <param name="buffer">Destination color buffer.</param>
	/// <param name="texWidth">Total texture width in pixels.</param>
	/// <param name="texHeight">Total texture height in pixels.</param>
	/// <param name="cell">Tile coordinate to mark.</param>
	/// <param name="pixelsPerCell">Pixels per tile.</param>
	/// <param name="color">Marker color.</param>
	private static void DrawMarker(Color32[] buffer, int texWidth, int texHeight, Coordinates cell, int pixelsPerCell, Color32 color)
	{
		if (cell.X < 0 || cell.Y < 0) return;

		int size = Math.Max(1, pixelsPerCell / 2);
		int pxX = cell.X * pixelsPerCell + (pixelsPerCell - size) / 2;
		int pxY = cell.Y * pixelsPerCell + (pixelsPerCell - size) / 2;

		pxY = texHeight - size - pxY;

		for (int dy = 0; dy < size; dy++)
		{
			int row = (pxY + dy) * texWidth;
			int idx = row + pxX;
			for (int dx = 0; dx < size; dx++)
			{
				buffer[idx + dx] = color;
			}
		}
	}
}