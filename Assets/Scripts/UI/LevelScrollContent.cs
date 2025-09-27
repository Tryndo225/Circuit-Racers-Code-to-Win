using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Dynamic grid layout and population of level preview items inside a scrollable content RectTransform.
/// Subscribes to <see cref="GameDataManager"/> changes, instantiates preview prefabs, and lays them out responsively.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Read-only UI that mirrors the current set of saved levels.
/// 
/// Responsibilities:
/// - Listen for game-data changes and (re)build the preview list.
/// - Instantiate a preview prefab per level (under <see cref="contentRect"/>).
/// - Compute a responsive, column-based grid using viewport-relative spacing/padding ratios.
/// - Adjust content height so a parent <c>ScrollRect</c> can scroll the full grid.
/// 
/// Threading:
/// - Unity main thread only.
/// 
/// Usage:
/// - Place this on a GameObject under a <c>ScrollRect</c>. Assign <see cref="contentRect"/> to the content transform,
///   and set <see cref="levelPreviewerPrefab"/> to a prefab that contains a <see cref="LevelEntry"/> component.
/// - The grid reflows on dimension changes and one frame after (to catch late layout updates).
/// </remarks>
public class LevelScrollContent : MonoBehaviour
{
    #region Inspector : References

    [Header("References")]
    /// <summary>
    /// Scroll content container that receives instantiated preview items and whose height is adjusted.
    /// Typically the <c>ScrollRect.content</c>.
    /// </summary>
    [SerializeField] private RectTransform contentRect;

    #endregion

    #region Inspector : Grid

    [Header("Grid")]
    /// <summary>
    /// Number of columns in the grid. Clamped to [1..10].
    /// </summary>
    [SerializeField, Range(1, 10)] private int itemsInLine = 3;

    #endregion

    #region Inspector : Spacing & Padding (ratios)

    [Header("Spacing (ratios of viewport width)")]
    /// <summary>
    /// Horizontal gap between items, expressed as a fraction of the viewport width.
    /// </summary>
    [SerializeField, Range(0f, 0.2f)] private float horizontalSpacingRatio = 0.02f;

    /// <summary>
    /// Vertical gap between rows, expressed as a fraction of the viewport width.
    /// </summary>
    [SerializeField, Range(0f, 0.2f)] private float verticalSpacingRatio = 0.02f;

    /// <summary>
    /// Left/right padding, expressed as a fraction of the viewport width.
    /// </summary>
    [SerializeField, Range(0f, 0.2f)] private float horizontalPaddingRatio = 0.04f;

    /// <summary>
    /// Top padding, expressed as a fraction of the viewport width.
    /// </summary>
    [SerializeField, Range(0f, 0.2f)] private float topPaddingRatio = 0.02f;

    /// <summary>
    /// Bottom padding, expressed as a fraction of the viewport width.
    /// </summary>
    [SerializeField, Range(0f, 0.2f)] private float bottomPaddingRatio = 0.02f;

    #endregion

    #region Inspector : Prefab

    [Header("Prefabs")]
    /// <summary>
    /// Prefab instantiated once per level entry. Must include a <see cref="LevelEntry"/> component.
    /// </summary>
    [SerializeField] private GameObject levelPreviewerPrefab;

    #endregion

    /// <summary>
    /// Backing list of instantiated preview GameObjects (children of <see cref="contentRect"/>).
    /// </summary>
    private readonly List<GameObject> levelPreviewers = new();

    #region Unity Methods

    /// <summary>
    /// Subscribes to <see cref="GameDataManager"/> change notifications and creates the initial previews.
    /// </summary>
    private void Start()
    {
        GameDataManager.Instance.AddListener(CreateLevelPreviews);
        CreateLevelPreviews();
    }

    /// <summary>
    /// Unsubscribes from change notifications on destruction to avoid leaks.
    /// </summary>
    private void OnDestroy()
    {
        if (GameDataManager.Instance != null)
            GameDataManager.Instance.RemoveListener(CreateLevelPreviews);
    }

    /// <summary>
    /// Recomputes layout whenever this RectTransform (or its parents) changes size.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        UpdateLayout();
    }

    #endregion

    #region Build & Layout

    /// <summary>
    /// Destroys old preview items, instantiates one per saved level, wires the data, and updates the layout.
    /// </summary>
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

    /// <summary>
    /// Waits one frame to allow layout system to settle (e.g., after content changes) and then updates layout again.
    /// </summary>
    private IEnumerator DelayedLayout()
    {
        yield return new WaitForEndOfFrame();
        UpdateLayout();
    }

    /// <summary>
    /// Computes a responsive, column-based grid for all preview items and adjusts the content height to fit.
    /// Item size is square and derived from viewport width, column count, and spacing/padding ratios.
    /// </summary>
    private void UpdateLayout()
    {
        if (contentRect == null || levelPreviewers.Count == 0) return;

        var viewport = contentRect.parent as RectTransform;
        if (viewport == null) return;

        float fieldWidth = viewport.rect.width;
        if (fieldWidth <= 0f) return;

        // Pin content to the top, stretch horizontally
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
        float itemHeight = itemWidth; // square tiles

        float vGap = verticalSpacingRatio * fieldWidth;

        // Position and size each child
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

        // Compute total content height for scrolling
        int totalRows = Mathf.CeilToInt((float)levelPreviewers.Count / cols);
        float totalHeight =
            topPad +
            (totalRows > 0 ? (totalRows * itemHeight + (totalRows - 1) * vGap) : 0f) +
            botPad;

        contentRect.sizeDelta = new Vector2(0f, totalHeight);
    }

    #endregion
}
