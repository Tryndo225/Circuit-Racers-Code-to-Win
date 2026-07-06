#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// Compact 1-row drawer for <see cref="WheelSpec"/> showing collider, visual, powered, and steering columns.
/// </summary>
/// <remarks>
/// @ingroup editor_util
/// Purpose:
/// - Presents a dense, horizontal layout for the four fields of <see cref="WheelSpec"/> in the Inspector.
/// - Keeps the footprint to a single line per element for faster wheel setup.
/// 
/// Behavior:
/// - Lays out four equal-width columns with a small padding: Collider | Visual | Powered | Steering.
/// - Does not draw field labels (uses <see cref="GUIContent.none"/>); rely on column order and tooltips from Unity.
/// 
/// Threading:
/// - Unity Editor thread only; compiled out of player builds with <c>#if UNITY_EDITOR</c>.
/// 
/// Notes:
/// - Intended for use inside arrays or structs on vehicle setup components.
/// - Pairs well with a header/tooltip on the parent field describing the column order.
/// </remarks>
[CustomPropertyDrawer(typeof(WheelSpec))]
public class WheelSpecDrawer : PropertyDrawer
{
	/// <summary>
	/// Returns a fixed one-line height plus a small padding to separate rows visually.
	/// </summary>
	/// <param name="property">Property being drawn.</param>
	/// <param name="label">GUI label.</param>
	/// <returns>Pixel height for this drawer.</returns>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUIUtility.singleLineHeight + 4f;
	}

	/// <summary>
	/// Renders the four <see cref="WheelSpec"/> fields in a single row: collider, visual, powered, steering.
	/// </summary>
	/// <param name="position">Target rect to draw within.</param>
	/// <param name="property">Root serialized property (a <see cref="WheelSpec"/> instance).</param>
	/// <param name="label">GUI label (unused; columns omit labels).</param>
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);
		position = EditorGUI.IndentedRect(position);

		float pad = 4f;
		int cols = 4;
		float w = (position.width - pad * (cols - 1)) / cols;

		var r0 = new Rect(position.x + (w + pad) * 0, position.y, w, EditorGUIUtility.singleLineHeight);
		var r1 = new Rect(position.x + (w + pad) * 1, position.y, w, EditorGUIUtility.singleLineHeight);
		var r2 = new Rect(position.x + (w + pad) * 2, position.y, w, EditorGUIUtility.singleLineHeight);
		var r3 = new Rect(position.x + (w + pad) * 3, position.y, w, EditorGUIUtility.singleLineHeight);

		EditorGUI.PropertyField(r0, property.FindPropertyRelative("collider"), GUIContent.none);
		EditorGUI.PropertyField(r1, property.FindPropertyRelative("visual"), GUIContent.none);
		EditorGUI.PropertyField(r2, property.FindPropertyRelative("powered"), GUIContent.none);
		EditorGUI.PropertyField(r3, property.FindPropertyRelative("steering"), GUIContent.none);

		EditorGUI.EndProperty();
	}
}

#endif
