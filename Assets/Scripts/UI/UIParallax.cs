using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-layer configuration for a UI parallax effect.
/// </summary>
/// <remarks>
/// @ingroup ui
/// Each layer tracks a <see cref="RectTransform"/>, a 2D parallax strength, and an optional Z-tilt.
/// A larger absolute value in <see cref="Strength"/> moves the layer more as the pointer moves;
/// <see cref="TiltZ"/> applies a subtle roll in degrees around Z. Set <see cref="InvertedTilt"/> to
/// flip the tilt direction relative to pointer X.
/// </remarks>
[Serializable]
public struct UIParallaxLayer
{
	/// <summary>
	/// The RectTransform to move and optionally tilt.
	/// </summary>
	[Tooltip("RectTransform moved and optionally tilted by this parallax layer.")]
	public RectTransform RTransform;

	/// <summary>
	/// Normalized parallax influence per axis. (1,1) = full movement; (0,0) = frozen.
	/// </summary>
	[Tooltip("Parallax influence per axis. Larger values move this layer more as the pointer moves.")]
	public Vector2 Strength;

	/// <summary>
	/// Z-axis tilt in degrees applied based on normalized pointer X position.
	/// </summary>
	[Tooltip("Z-axis tilt in degrees applied based on pointer X position.")]
	public float TiltZ;

	/// <summary>
	/// If true, flips the sign of the Z-tilt response.
	/// </summary>
	[Tooltip("If enabled, flips the Z-tilt direction.")]
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
/// @brief Applies smooth anchored-position offsets and optional Z-tilt to a set of RectTransforms
///        based on the pointer position within a reference <see cref="Canvas"/>.
///
/// Coordinate space:
/// - Works in the canvas local space.
/// - Supports ScreenSpaceOverlay and camera-based canvas render modes.
///
/// Motion model:
/// - The raw pointer position is converted into normalized canvas coordinates in the range [-1,1].
/// - The normalized pointer value is smoothed with <see cref="pointerSmoothTime"/> and limited by
///   <see cref="maxPointerSpeed"/> to avoid sudden jumps when the mouse moves quickly.
/// - Each layer scales the smoothed pointer by its <see cref="UIParallaxLayer.Strength"/> and by
///   <see cref="maxOffset"/>.
/// - Layer transforms are eased toward their target positions each frame using exponential smoothing
///   controlled by <see cref="damping"/>.
/// - Optional Z-tilt is based on the smoothed horizontal pointer value.
///
/// Threading:
/// - Unity main thread only. This component reads <see cref="Input.mousePosition"/> and modifies UI transforms.
///
/// Usage:
/// - Assign a reference canvas, or leave it empty to auto-find a parent canvas in <see cref="Awake"/>.
/// - Populate <see cref="layers"/> with RectTransforms and per-layer movement settings.
/// - Tune <see cref="maxOffset"/> for movement distance.
/// - Tune <see cref="pointerSmoothTime"/> and <see cref="maxPointerSpeed"/> to reduce fast mouse jumps.
/// - Tune <see cref="damping"/> to control how quickly the UI layers follow the smoothed pointer target.
/// </remarks>
public class UIParallax : MonoBehaviour
{
	[Header("Setup")]

	/// <summary>
	/// Reference canvas defining the parallax area and UI scale.
	/// </summary>
	/// <remarks>
	/// If not assigned manually, the component searches for a parent <see cref="Canvas"/> during <see cref="Awake"/>.
	/// </remarks>
	[Tooltip("Reference canvas defining the parallax area and scale. Auto-found from parents if left empty.")]
	[SerializeField] private Canvas canvas;

	/// <summary>
	/// Ordered list of parallax layers to animate.
	/// </summary>
	/// <remarks>
	/// The order does not affect the calculation, but it can be used to organize layers from background to foreground.
	/// </remarks>
	[Tooltip("List of RectTransform layers animated by the parallax effect.")]
	[SerializeField] private List<UIParallaxLayer> layers = new();

	[Header("Motion")]

	/// <summary>
	/// Maximum pixel offset applied at full pointer movement when layer strength is 1.
	/// </summary>
	/// <remarks>
	/// The value is divided by <see cref="Canvas.scaleFactor"/> so the effect remains consistent across UI scales.
	/// </remarks>
	[Tooltip("Maximum pixel offset applied at full pointer movement before canvas scale correction.")]
	[SerializeField] private float maxOffset = 40f;

	/// <summary>
	/// Smoothing speed used when easing each layer toward its current target.
	/// </summary>
	/// <remarks>
	/// Higher values make layers follow the smoothed pointer target faster.
	/// Lower values create a slower, floatier movement.
	/// </remarks>
	[Tooltip("Smoothing speed for layer movement. Higher values make layers follow the pointer faster.")]
	[SerializeField] private float damping = 8f;

	/// <summary>
	/// Time used to smooth the normalized pointer position.
	/// </summary>
	/// <remarks>
	/// This smooths the input target before it is applied to the layers.
	/// Smaller values make the pointer target react faster.
	/// Larger values reduce sudden jumps more strongly, but can feel more delayed.
	/// </remarks>
	[Tooltip("Time used to smooth mouse input before applying parallax. Smaller is faster; larger reduces jumps more.")]
	[SerializeField] private float pointerSmoothTime = 0.08f;

	/// <summary>
	/// Maximum speed of the smoothed normalized pointer movement.
	/// </summary>
	/// <remarks>
	/// This limits how quickly the internal pointer target can move across the normalized [-1,1] canvas range.
	/// It helps prevent sudden target jumps when the mouse moves very quickly between frames.
	/// </remarks>
	[Tooltip("Maximum speed of the smoothed pointer target. Lower values reduce fast mouse jumps more strongly.")]
	[SerializeField] private float maxPointerSpeed = 8f;

	/// <summary>
	/// Current smoothed normalized pointer position.
	/// </summary>
	/// <remarks>
	/// Stored in normalized canvas coordinates, where (-1,-1) is one corner and (1,1) is the opposite corner.
	/// </remarks>
	private Vector2 smoothedPointer;

	/// <summary>
	/// Velocity reference used by <see cref="Vector2.SmoothDamp(Vector2, Vector2, ref Vector2, float, float, float)"/>.
	/// </summary>
	private Vector2 pointerVelocity;

	/// <summary>
	/// Tracks whether <see cref="smoothedPointer"/> has been initialized from the first raw pointer sample.
	/// </summary>
	private bool pointerInitialized;

	/// <summary>
	/// Cached initial anchored positions per layer.
	/// </summary>
	/// <remarks>
	/// These values are captured in <see cref="Awake"/> and used as the neutral position for each layer.
	/// </remarks>
	private Vector2[] initialAnchored;

	/// <summary>
	/// Cached initial local rotations per layer.
	/// </summary>
	/// <remarks>
	/// These values are captured in <see cref="Awake"/> and used as the neutral rotation for optional Z-tilt.
	/// </remarks>
	private Quaternion[] initialRot;

	/// <summary>
	/// Unity Awake callback.
	/// </summary>
	/// <remarks>
	/// Resolves the reference canvas if needed and caches the initial position and rotation of each layer.
	/// </remarks>
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
	/// Unity Update callback.
	/// </summary>
	/// <remarks>
	/// Reads the mouse position, converts it into normalized canvas space, smooths the pointer target,
	/// and then moves and optionally tilts each configured parallax layer toward its calculated target.
	/// </remarks>
	void Update()
	{
		if (!canvas || layers.Count == 0) return;

		RectTransform canvasRect = canvas.transform as RectTransform;
		if (!canvasRect) return;

		Vector2 local;
		Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, cam, out local);

		Vector2 half = canvasRect.rect.size * 0.5f;
		float nx = Mathf.Clamp(half.x > 0 ? local.x / half.x : 0f, -1f, 1f);
		float ny = Mathf.Clamp(half.y > 0 ? local.y / half.y : 0f, -1f, 1f);

		Vector2 rawPointer = new Vector2(nx, ny);

		if (!pointerInitialized)
		{
			smoothedPointer = rawPointer;
			pointerInitialized = true;
		}
		else
		{
			smoothedPointer = Vector2.SmoothDamp(
				smoothedPointer,
				rawPointer,
				ref pointerVelocity,
				pointerSmoothTime,
				maxPointerSpeed,
				Time.unscaledDeltaTime
			);
		}

		nx = smoothedPointer.x;
		ny = smoothedPointer.y;

		float scale = canvas.scaleFactor;
		float px = maxOffset / Mathf.Max(scale, 0.0001f);

		// Exponential smoothing toward the target each frame using unscaled time for UI responsiveness.
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