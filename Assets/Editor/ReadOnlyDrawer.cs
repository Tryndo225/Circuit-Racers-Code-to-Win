#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector drawer that renders any field marked with <see cref="ReadOnlyAttribute"/> as disabled
/// (grayed out) so it cannot be edited in the Unity Inspector.
/// </summary>
/// <remarks>
/// @ingroup editor_util
/// @thread Unity Editor thread only. Editor-only script; excluded from player builds by the <c>UNITY_EDITOR</c> guard.
/// @usage Annotate a serialized field with <c>[ReadOnly]</c> to display its value without allowing edits:
/// <code>
/// [SerializeField, ReadOnly] private int debugValue;
/// </code>
/// The drawer preserves native rendering, including children for complex/array types; it only disables input.
/// </remarks>
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
	/// <summary>
	/// Returns the standard property height so complex/array/object fields still lay out correctly.
	/// </summary>
	/// <param name="property">The serialized property being drawn.</param>
	/// <param name="label">GUI label content.</param>
	/// <returns>Height in pixels required to render the property (including children).</returns>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUI.GetPropertyHeight(property, label, true);
	}

	/// <summary>
	/// Draws the property in a disabled (read-only) scope so the user cannot modify its value.
	/// </summary>
	/// <param name="position">On-screen rectangle to draw within.</param>
	/// <param name="property">The serialized property being drawn.</param>
	/// <param name="label">GUI label content.</param>
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		using (new EditorGUI.DisabledScope(true))
		{
			EditorGUI.PropertyField(position, property, label, true);
		}
	}
}
#endif
