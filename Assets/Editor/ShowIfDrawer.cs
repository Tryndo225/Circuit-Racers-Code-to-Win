#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector drawer for <see cref="ShowIfAttribute"/> that conditionally shows or hides
/// a serialized field based on the value of a boolean sibling/parent property.
/// </summary>
/// <remarks>
/// @ingroup editor_util
/// @thread Unity Editor thread only. Editor-only script, excluded from player builds via <c>UNITY_EDITOR</c>.
///
/// Behavior:
/// - If the referenced condition property is found and is a <c>bool</c>, the decorated field is only drawn
///   when its value equals <see cref="ShowIfAttribute.RequiredState"/> (default: <c>true</c>).
/// - If the condition cannot be found or is not a boolean, the field is shown (fails open).
///
/// Field path resolution:
/// - First attempts a sibling/ancestor-relative lookup by replacing the tail of <c>property.propertyPath</c>
///   with the condition field name.
/// - Falls back to a root-level lookup using just <see cref="ShowIfAttribute.Field"/>.
///
/// Usage:
/// <code>
/// public class Example : MonoBehaviour
/// {
///     [SerializeField] private bool advancedMode;
///     [SerializeField, ShowIf(nameof(advancedMode))]
///     private float advancedSetting;
/// }
/// </code>
/// </remarks>
[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    /// <summary>
    /// Returns either the normal property height (when visible) or 0 when hidden,
    /// allowing the drawer to collapse layout space in the Inspector.
    /// </summary>
    /// <param name="property">Serialized property being measured.</param>
    /// <param name="label">GUI label.</param>
    /// <returns>Height in pixels for the property field.</returns>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return IsVisible(property) ? EditorGUI.GetPropertyHeight(property, label, true) : 0f;
    }

    /// <summary>
    /// Draws the property only if the visibility condition evaluates to true.
    /// </summary>
    /// <param name="position">Drawing rect.</param>
    /// <param name="property">Serialized property to draw.</param>
    /// <param name="label">GUI label.</param>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (IsVisible(property))
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    /// <summary>
    /// Evaluates the condition specified by the attached <see cref="ShowIfAttribute"/>.
    /// </summary>
    /// <param name="property">Serialized property whose visibility is being evaluated.</param>
    /// <returns>True if the field should be shown; otherwise false.</returns>
    private bool IsVisible(SerializedProperty property)
    {
        var attrib = (ShowIfAttribute)attribute;

        // Try relative path first: replace tail of the current property path with the condition field name.
        string path = property.propertyPath;
        int lastDot = path.LastIndexOf('.');
        string pathToField = (lastDot >= 0 ? path.Substring(0, lastDot + 1) : "") + attrib.Field;

        // Attempt relative lookup, then fallback to root-level lookup.
        SerializedProperty cond = property.serializedObject.FindProperty(pathToField)
                                 ?? property.serializedObject.FindProperty(attrib.Field);

        // Fail open (visible) if condition missing or not a bool.
        if (cond == null || cond.propertyType != SerializedPropertyType.Boolean)
        {
            return true;
        }

        // Visible only when the boolean matches the required state.
        return cond.boolValue == attrib.RequiredState;
    }
}
#endif
