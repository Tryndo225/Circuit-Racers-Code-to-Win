using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Per-layer configuration for a UI parallax effect.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Each layer tracks a <see cref="RectTransform"/>, a 2D parallax strength, and an optional Z-tilt.
/// A larger absolute value in <see cref="Strength"/> moves the layer more as the pointer moves; 
/// <see cref="TiltZ"/> applies a subtle roll (in degrees) around Z. Set <see cref="InvertedTilt"/> to
/// flip the tilt direction relative to pointer X.
/// </remarks>
[Serializable]
public struct UIParallaxLayer
{
    /// <summary>
    /// The RectTransform to move and optionally tilt.
    /// </summary>
    public RectTransform RTransform;

    /// <summary>
    /// Normalized parallax influence per axis. (1,1) = full movement; (0,0) = frozen.
    /// </summary>
    public Vector2 Strength;

    /// <summary>
    /// Z-axis tilt (in degrees) applied based on normalized pointer X position.
    /// </summary>
    public float TiltZ;

    /// <summary>
    /// If true, flips the sign of the Z-tilt response.
    /// </summary>
    public bool InvertedTilt;

    /// <summary>
    /// Convenience cast: treat a RectTransform as a layer with zero strength and no tilt.
    /// </summary>
    /// <param name="rt">Target RectTransform.</param>
    /// <returns>A layer pointing at <paramref name="rt"/> with default settings.</returns>
    public static implicit operator UIParallaxLayer(RectTransform rt) => new UIParallaxLayer { RTransform = rt, Strength = Vector2.zero, TiltZ = 0f, InvertedTilt = false };
}

/// <summary>
/// Pointer-driven UI parallax controller for layered RectTransforms.
/// </summary>
/// <remarks>
/// @ingroup ui
/// @brief Applies smooth anchored-position offsets (and optional Z-tilt) to a set of RectTransforms
///        based on the pointer position within a reference <see cref="Canvas"/>.
/// 
/// Coordinate space:
/// - Works in the canvas local space; supports ScreenSpaceOverlay and world/screen camera modes.
/// 
/// Motion model:
/// - Pointer position is normalized to [-1,1] over the canvas rect. Each layer uses its
///   <see cref="UIParallaxLayer.Strength"/> to scale a maximum pixel offset derived from <see cref="maxOffset"/>
///   and the canvas <see cref="Canvas.scaleFactor"/>. Motion is eased toward the target each frame using
///   exponential smoothing controlled by <see cref="damping"/>.
/// 
/// Threading:
/// - Unity main thread only (reads <see cref="Input.mousePosition"/> and manipulates transforms).
/// 
/// Usage:
/// - Assign a reference canvas (or leave null to auto-find in parents on <see cref="Awake"/>).
/// - Populate <see cref="layers"/> with RectTransforms and per-layer parameters.
/// - Tune <see cref="maxOffset"/> and <see cref="damping"/> for the desired feel.
/// </remarks>
public class UIParallax : MonoBehaviour
{
    [Header("Setup")]
    /// <summary>
    /// Reference canvas defining the parallax area and scale. Auto-resolved from parents on <see cref="Awake"/> if not set.
    /// </summary>
    [SerializeField] private Canvas canvas;

    /// <summary>
    /// Ordered list of parallax layers to animate (near-to-far or vice versa as you prefer).
    /// </summary>
    [SerializeField] private List<UIParallaxLayer> layers = new();

    [Header("Motion")]
    /// <summary>
    /// Maximum pixel offset (at Strength = 1) applied at the canvas edges (normalized ±1),
    /// before division by <see cref="Canvas.scaleFactor"/>.
    /// </summary>
    [SerializeField] private float maxOffset = 40f;

    /// <summary>
    /// Exponential smoothing factor (units: 1/seconds). Higher values converge faster.
    /// </summary>
    [SerializeField] private float damping = 8f;

    /// <summary>
    /// Cached initial anchored positions per layer (set in <see cref="Awake"/>).
    /// </summary>
    private Vector2[] initialAnchored;

    /// <summary>
    /// Cached initial local rotations per layer (set in <see cref="Awake"/>).
    /// </summary>
    private Quaternion[] initialRot;

    /// <summary>
    /// Unity Awake: cache the reference canvas (if missing) and per-layer initial transforms.
    /// </summary>
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

    /// <summary>
    /// Unity Update: compute normalized pointer position in canvas space and ease layers toward their targets.
    /// </summary>
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

        // Exponential smoothing toward the target each frame (unscaled time for UI responsiveness).
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
