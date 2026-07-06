#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector drawer that renders fields marked with <see cref="ReadOnlyAttribute"/> as disabled.
/// </summary>
/// <remarks>
/// @ingroup editor_util
/// @brief Displays serialized fields in the Unity Inspector without allowing edits.
///
/// Usage:
/// <code>
/// [SerializeField, ReadOnly] private int debugValue;
/// </code>
///
/// The drawer preserves Unity's native property rendering, including child fields for complex types,
/// arrays, and object references. It only disables user input while drawing the property.
///
/// Threading:
/// - Unity Editor thread only.
/// - Editor-only script, excluded from player builds by the <c>UNITY_EDITOR</c> guard.
/// </remarks>
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
	/// <summary>
	/// Returns the standard property height.
	/// </summary>
	/// <param name="property">Serialized property being drawn.</param>
	/// <param name="label">GUI label content.</param>
	/// <returns>Height in pixels required to render the property, including children.</returns>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUI.GetPropertyHeight(property, label, true);
	}

	/// <summary>
	/// Draws the property in a disabled read-only scope.
	/// </summary>
	/// <param name="position">On-screen rectangle to draw within.</param>
	/// <param name="property">Serialized property being drawn.</param>
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