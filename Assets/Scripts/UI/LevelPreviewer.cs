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
    [SerializeField] private int pixelsPerCell = 8;

    [SerializeField] private Color32 grass = Color.green;
    [SerializeField] private Color32 road = Color.gray;
    [SerializeField] private Color32 start = Color.lightGreen;
    [SerializeField] private Color32 finish = Color.red;
    [SerializeField] private Color32 checkPoint = Color.lightBlue;

    private void Start()
    {
        //generator = new LevelGenerator(20, 10, 1000);
        //_ = ShowGeneratedPreviewAsync();
    }

    public async Task ShowPreviewAsync(LevelMap map)
    {
        //var map = await Task.Run(() =>
        //{
        //    LevelMap map = null;
        //    try
        //    {
        //        //Debug.Log("Starting level generation task");
        //        map = generator.GenerateLevel(50, 50, true, SeedFactory.Next());
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.LogError($"Level generation failed: {ex}");
        //        return null;
        //    }
        //    return map;
        //});

        //Debug.Log("Level generation task completed");

        var tex = await Task.Run(() =>
        {
            return BuildPreviewTexture(map);
        });

        target.texture = tex;
        target.rectTransform.sizeDelta = new Vector2(tex.width, tex.height);
    }

    private Texture2D BuildPreviewTexture(LevelMap map)
    {
        int texWidth = map.Width * pixelsPerCell;
        int texHeight = map.Height * pixelsPerCell;

        var tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var buffer = new Color32[texWidth * texHeight];

        // Fill grass
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = grass;
        }

        // Paint road cells (value==1)
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.Tiles[x, y] == 1 || map.Tiles[x, y] == -2)
                {
                    FillCell(buffer, texWidth, texHeight, x, y, pixelsPerCell, road);
                }

                if (map.Tiles[x, y] == -2)
                {
                    DrawMarker(buffer, texWidth, texHeight, new Coordinates(x, y), pixelsPerCell, checkPoint);
                }
            }
        }

        // Overlay start/finish markers
        DrawMarker(buffer, texWidth, texHeight, map.StartPoint, pixelsPerCell, start);
        DrawMarker(buffer, texWidth, texHeight, map.FinishPoint, pixelsPerCell, finish);

        tex.SetPixels32(buffer);
        tex.Apply(false, false);
        return tex;
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