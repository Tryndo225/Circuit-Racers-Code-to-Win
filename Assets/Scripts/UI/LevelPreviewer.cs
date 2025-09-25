using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class LevelPreviewer : MonoBehaviour
{
    [Header("UI target")]
    [SerializeField] private RawImage target;

    [Header("Look")]
    [SerializeField] private Color32 grass = Color.green;

    [SerializeField] private Color32 road = Color.gray;
    [SerializeField] private Color32 start = Color.lightGreen;
    [SerializeField] private Color32 finish = Color.red;
    [SerializeField] private Color32 checkPoint = Color.lightBlue;

    private void Start()
    {
    }

    public void Clear()
    {
        target.texture = null;
    }

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

        // Start/Finish
        DrawMarker(buffer, texWidth, texHeight, map.StartPoint, pixelsPerCell, start);
        DrawMarker(buffer, texWidth, texHeight, map.FinishPoint, pixelsPerCell, finish);

        return (buffer, texWidth, texHeight);
    }

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