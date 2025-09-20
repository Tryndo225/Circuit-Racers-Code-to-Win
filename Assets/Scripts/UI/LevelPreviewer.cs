using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelPreviewer : MonoBehaviour
{
    [Header("UI target")]
    [SerializeField] private RawImage target;

    [Header("Look")]
    [SerializeField] private int pixelsPerCell = 8;

    [SerializeField] private Color32 grass = Color.green;
    [SerializeField] private Color32 road = Color.gray;
    [SerializeField] private Color32 start = Color.lightBlue;
    [SerializeField] private Color32 finish = Color.red;
    [SerializeField] private bool topLeftOrigin = true; // draw with (0,0) at top-left

    private LevelGenerator generator;

    private void Start()
    {
        generator = new LevelGenerator(20, 10, 1000);
        _ = ShowGeneratedPreviewAsync();
    }

    public void GenerateNewPreview()
    {
        _ = ShowGeneratedPreviewAsync();
    }

    public async Task ShowGeneratedPreviewAsync()
    {
        var map = await Task.Run(() =>
        {
            LevelMap map = null;
            try
            {
                //Debug.Log("Starting level generation task");
                map = generator.GenerateLevel(50, 50, true, SeedFactory.Next());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Level generation failed: {ex}");
                return null;
            }
            return map;
        });

        //Debug.Log("Level generation task completed");

        var tex = BuildPreviewTexture(map);
        target.texture = tex;
        target.rectTransform.sizeDelta = new Vector2(tex.width, tex.height);
    }

    public Texture2D BuildPreviewTexture(LevelMap map)
    {
        int texW = map.width * pixelsPerCell;
        int texH = map.height * pixelsPerCell;

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var buffer = new Color32[texW * texH];

        //Debug.Log("Filling texture buffer");

        // Fill grass first
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = grass;
        }

        //Debug.Log("Painting road cells");
        // Paint road cells (value==1)
        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                if (map.tiles[x, y] == 1)
                {
                    FillCell(buffer, texW, texH, x, y, pixelsPerCell, road, topLeftOrigin);
                }
            }
        }

        //Debug.Log("Overlaying start/finish markers");

        // Overlay start/finish markers
        DrawMarker(buffer, texW, texH, map.startPoint, pixelsPerCell, start, topLeftOrigin);
        DrawMarker(buffer, texW, texH, map.finishPoint, pixelsPerCell, finish, topLeftOrigin);

        tex.SetPixels32(buffer);
        tex.Apply(false, false);
        return tex;
    }

    private static void FillCell(Color32[] buf, int texW, int texH,
                                 int cellX, int cellY, int ppc, Color32 color, bool topLeft)
    {
        int pxX = cellX * ppc;
        int pxY = cellY * ppc;
        if (topLeft) pxY = texH - ppc - pxY;

        for (int dy = 0; dy < ppc; dy++)
        {
            int row = (pxY + dy) * texW;
            int idx = row + pxX;
            for (int dx = 0; dx < ppc; dx++)
            {
                buf[idx + dx] = color;
            }
        }
    }

    private static void DrawMarker(Color32[] buf, int texW, int texH,
                                   Coordinates c, int ppc, Color32 color, bool topLeft)
    {
        if (c.x < 0 || c.y < 0) return;
        // Slightly thicker marker (a 2*2 block of cells if ppc>=2, else single cell)
        int size = Math.Max(1, ppc / 2);
        int pxX = c.x * ppc + (ppc - size) / 2;
        int pxY = c.y * ppc + (ppc - size) / 2;
        if (topLeft) pxY = texH - size - pxY;

        for (int dy = 0; dy < size; dy++)
        {
            int row = (pxY + dy) * texW;
            int idx = row + pxX;
            for (int dx = 0; dx < size; dx++)
            {
                buf[idx + dx] = color;
            }
        }
    }
}