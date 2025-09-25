using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class LevelScrollContent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform contentRect;

    [Header("Grid")]
    [SerializeField, Range(1, 10)] private int itemsInLine = 3;

    [Header("Spacing (ratios of viewport width)")]
    [SerializeField, Range(0f, 0.2f)] private float horizontalSpacingRatio = 0.02f;

    [SerializeField, Range(0f, 0.2f)] private float verticalSpacingRatio = 0.02f;
    [SerializeField, Range(0f, 0.2f)] private float horizontalPaddingRatio = 0.04f;
    [SerializeField, Range(0f, 0.2f)] private float topPaddingRatio = 0.02f;
    [SerializeField, Range(0f, 0.2f)] private float bottomPaddingRatio = 0.02f;

    [Header("Prefabs")]
    [SerializeField] private GameObject levelPreviewerPrefab;

    private readonly List<GameObject> levelPreviewers = new();

    private void Start()
    {
        GameDataManager.Instance.AddListener(CreateLevelPreviews);
        CreateLevelPreviews();
    }

    private void OnDestroy()
    {
        if (GameDataManager.Instance != null)
            GameDataManager.Instance.RemoveListener(CreateLevelPreviews);
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdateLayout();
    }

    private void CreateLevelPreviews()
    {
        foreach (var go in levelPreviewers) Destroy(go);
        levelPreviewers.Clear();

        var levels = GameDataManager.Instance.CurrentGameData.Levels;
        foreach (var levelMap in levels)
        {
            var previewerObj = Instantiate(levelPreviewerPrefab, contentRect);
            var previewer = previewerObj.GetComponent<LevelEntry>();
            if (previewer != null) previewer.SetUp(levelMap);
            levelPreviewers.Add(previewerObj);
        }

        UpdateLayout();
        StartCoroutine(DelayedLayout());
    }

    private IEnumerator DelayedLayout()
    {
        yield return new WaitForEndOfFrame();
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        if (contentRect == null || levelPreviewers.Count == 0) return;

        var viewport = contentRect.parent as RectTransform;
        if (viewport == null) return;

        float fieldWidth = viewport.rect.width;
        if (fieldWidth <= 0f) return;

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(0f, contentRect.offsetMin.y);
        contentRect.offsetMax = new Vector2(0f, contentRect.offsetMax.y);

        int cols = Mathf.Max(1, itemsInLine);

        float hPad = horizontalPaddingRatio * fieldWidth;
        float hGap = horizontalSpacingRatio * fieldWidth;
        float topPad = topPaddingRatio * fieldWidth;
        float botPad = bottomPaddingRatio * fieldWidth;

        float totalGutters = (cols - 1) * hGap;
        float usableWidth = Mathf.Max(0f, fieldWidth - 2f * hPad - totalGutters);
        float itemWidth = usableWidth / cols;
        float itemHeight = itemWidth;

        float vGap = verticalSpacingRatio * fieldWidth;

        for (int i = 0; i < levelPreviewers.Count; i++)
        {
            var rt = levelPreviewers[i].GetComponent<RectTransform>();
            if (!rt) continue;

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(itemWidth, itemHeight);

            int row = i / cols;
            int col = i % cols;

            float x = hPad + col * (itemWidth + hGap);
            float y = -topPad - row * (itemHeight + vGap);

            rt.anchoredPosition = new Vector2(x, y);
        }

        int totalRows = Mathf.CeilToInt((float)levelPreviewers.Count / cols);
        float totalHeight =
            topPad +
            (totalRows > 0 ? (totalRows * itemHeight + (totalRows - 1) * vGap) : 0f) +
            botPad;

        contentRect.sizeDelta = new Vector2(0f, totalHeight);
    }
}