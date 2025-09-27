using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Renders a tiny top-down preview image of a <see cref="LevelMap"/> into a UI <see cref="RawImage"/>:
/// grass background, road cells, checkpoint markers, and start/finish markers.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Lightweight, allocation-friendly level thumbnail generator for menus and lists.
///
/// Responsibilities:
/// - Choose a pixels-per-cell (PPC) based on the target rect width and device max texture size.
/// - Convert the integer tile grid into a CPU color buffer (on a worker thread).
/// - Upload the buffer into a point-filtered <see cref="Texture2D"/> and assign it to the UI.
/// 
/// Threading:
/// - CPU buffer generation is done off the main thread via <see cref="Task.Run(Func{Task})"/>.
/// - Texture2D creation and assignment must occur on the Unity main thread (done after the await).
///
/// Performance notes:
/// - Uses <see cref="TextureFormat.RGBA32"/> and <see cref="FilterMode.Point"/> for crisp pixel art.
/// - Avoid calling <see cref="ShowPreviewAsync(LevelMap)"/> every frame; call when data or size changes.
/// </remarks>
public class LevelPreviewer : MonoBehaviour
{
    #region Inspector : UI target

    [Header("UI target")]
    /// <summary>
    /// Destination UI image that will display the generated preview texture.
    /// </summary>
    [SerializeField] private RawImage target;

    #endregion

    #region Inspector : Colors

    [Header("Look")]
    /// <summary>Background color for non-road cells (grass).</summary>
    [SerializeField] private Color32 grass = Color.green;

    /// <summary>Base color for road cells (tile value 1) and checkpoint cells before marker overlay.</summary>
    [SerializeField] private Color32 road = Color.gray;

    /// <summary>Marker color for the level start cell.</summary>
    [SerializeField] private Color32 start = Color.lightGreen;

    /// <summary>Marker color for the level finish cell.</summary>
    [SerializeField] private Color32 finish = Color.red;

    /// <summary>Marker color for checkpoint cells (tile value -2).</summary>
    [SerializeField] private Color32 checkPoint = Color.lightBlue;

    #endregion

    /// <summary>
    /// MonoBehaviour Start hook (unused; reserved for future initialization).
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
    /// Asynchronously builds and displays a preview texture for the given <paramref name="map"/>.
    /// </summary>
    /// <param name="map">Level definition to visualize (must have valid <c>Tiles</c>, <c>Width</c>, <c>Height</c>).</param>
    /// <remarks>
    /// The CPU color buffer is generated on a worker thread; the <see cref="Texture2D"/> is created and assigned on the main thread.
    /// PPC (pixels-per-cell) is chosen so the preview fits in the target rect and also respects <see cref="SystemInfo.maxTextureSize"/>.
    /// </remarks>
    public async Task ShowPreviewAsync(LevelMap map)
    {
        if (target == null) return;

        // Pick pixels-per-cell from the current target width
        int ppc = Mathf.Max(1, Mathf.FloorToInt(target.rectTransform.rect.width / map.Width));

        // Clamp to device max texture size
        int maxTex = SystemInfo.maxTextureSize;
        ppc = Mathf.Min(ppc, Mathf.Max(1, maxTex / Math.Max(map.Width, map.Height)));

        // Build CPU buffer off the main thread
        var result = await Task.Run(() =>
        {
            return BuildPreviewBuffer(map, ppc);
        });

        // Upload on main thread
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
    /// Constructs a CPU color buffer for the preview and returns it along with the texture dimensions.
    /// </summary>
    /// <param name="map">Level grid to render.</param>
    /// <param name="pixelsPerCell">Preview resolution in pixels per tile.</param>
    /// <returns>Tuple (buffer, width, height) ready for <see cref="Texture2D.SetPixels32(Color32[])"/>.</returns>
    private (Color32[] buffer, int texWidth, int texHeight) BuildPreviewBuffer(LevelMap map, int pixelsPerCell)
    {
        int texWidth = map.Width * pixelsPerCell;
        int texHeight = map.Height * pixelsPerCell;

        var buffer = new Color32[texWidth * texHeight];

        // Fill grass
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = grass; // OK: 'grass' is a struct value captured when called

        // Road & checkpoints
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int v = map.Tiles[x, y];
                if (v == 1 || v == -2)
                    FillCell(buffer, texWidth, texHeight, x, y, pixelsPerCell, road);

                if (v == -2)
                    DrawMarker(buffer, texWidth, texHeight, new Coordinates(x, y), pixelsPerCell, checkPoint);
            }
        }

        // Start/Finish overlays
        DrawMarker(buffer, texWidth, texHeight, map.StartPoint, pixelsPerCell, start);
        DrawMarker(buffer, texWidth, texHeight, map.FinishPoint, pixelsPerCell, finish);

        return (buffer, texWidth, texHeight);
    }

    /// <summary>
    /// Fills a single tile cell area with a solid <paramref name="color"/> in the buffer.
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
        // Flip vertically so (0,0) is bottom-left in the preview
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
    /// Draws a square marker centered within a tile (used for start, finish, and checkpoint highlights).
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
        // Flip vertically
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
