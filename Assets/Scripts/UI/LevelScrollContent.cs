using UnityEngine;
using System.Collections.Generic;

public class LevelScrollContent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform contentRect;

    [Header("Layout")]
    [SerializeField] private float verticalSpacing;

    [SerializeField, Range(1, 10)] private int itemsInLine;

    [Header("Prefabs")]
    [SerializeField] private GameObject levelPreviewerPrefab;

    private List<GameObject> levelPreviewers;

    private void Start()
    {
        GameDataManager.Instance.AddListener(CreateLevelPreviews);
        CreateLevelPreviews();
    }

    private void CreateLevelPreviews()
    {
        GameDataManager.Instance.CurrentGameData.Levels.ForEach(levelMap =>
        {
            var previewerObj = Instantiate(levelPreviewerPrefab, contentRect);
            var previewer = previewerObj.GetComponent<LevelEntry>();
            if (previewer != null)
            {
                previewer.SetUp(levelMap);
            }

            levelPreviewers.Add(previewerObj);
        });

        UpdateLayout();
    }

    private void UpdateLayout()
    {
        if (levelPreviewers == null || levelPreviewers.Count == 0) return;
        float parentWidth = ((RectTransform)transform).rect.width;
        float itemWidth = parentWidth / itemsInLine;
        float itemHeight = itemWidth + itemWidth / 9;

        for (int i = 0; i < levelPreviewers.Count; i++)
        {
            var rt = levelPreviewers[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                int row = i / itemsInLine;
                int col = i % itemsInLine;
                float x = col * itemWidth + itemWidth / 2;
                float y = -row * (itemHeight + verticalSpacing) - itemHeight / 2;
                rt.sizeDelta = new Vector2(itemWidth, itemHeight);
                rt.anchoredPosition = new Vector2(x, y);
            }
        }
        // Adjust parent height
        int totalRows = Mathf.CeilToInt((float)levelPreviewers.Count / itemsInLine);
        float totalHeight = totalRows * itemHeight + (totalRows - 1) * verticalSpacing;
        contentRect.sizeDelta = new Vector2(parentWidth, totalHeight);
    }
}