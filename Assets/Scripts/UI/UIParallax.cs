using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public struct UIParallaxLayer
{
    public RectTransform RTransform;
    public Vector2 Strength;
    public float TiltZ;
    public bool InvertedTilt;

    public static implicit operator UIParallaxLayer(RectTransform rt) => new UIParallaxLayer { RTransform = rt, Strength = Vector2.zero, TiltZ = 0f, InvertedTilt = false };
}

public class UIParallax : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private List<UIParallaxLayer> layers = new();

    [Header("Motion")]
    [SerializeField] private float maxOffset = 40f;
    [SerializeField] private float damping = 8f;

    private Vector2[] initialAnchored;
    private Quaternion[] initialRot;

    void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        int n = layers.Count;
        initialAnchored = new Vector2[n];
        initialRot = new Quaternion[n];

        for (int i = 0; i < n; i++)
        {
            if (!layers[i].RTransform) continue;
            initialAnchored[i] = layers[i].RTransform.anchoredPosition;
            initialRot[i] = layers[i].RTransform.localRotation;
        }
    }

    void Update()
    {
        if (!canvas || layers.Count == 0) return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 local;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, cam, out local);

        Vector2 half = canvasRect.rect.size * 0.5f;
        float nx = Mathf.Clamp(half.x > 0 ? local.x / half.x : 0f, -1f, 1f);
        float ny = Mathf.Clamp(half.y > 0 ? local.y / half.y : 0f, -1f, 1f);

        float scale = canvas.scaleFactor;
        float px = (maxOffset / Mathf.Max(scale, 0.0001f));

        float t = 1f - Mathf.Exp(-damping * Time.unscaledDeltaTime);

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (!layer.RTransform) continue;

            Vector2 target = initialAnchored[i] + new Vector2(
                nx * layer.Strength.x * px,
                ny * layer.Strength.y * px
            );

            layer.RTransform.anchoredPosition = Vector2.Lerp(layer.RTransform.anchoredPosition, target, t);

            if (layer.TiltZ != 0f)
            {
                Quaternion targetRot = Quaternion.Euler(0f, 0f, nx * layer.TiltZ * (layer.InvertedTilt ? -1f : 1f)) * initialRot[i];
                layer.RTransform.localRotation = Quaternion.Slerp(layer.RTransform.localRotation, targetRot, t);
            }
        }
    }
}
