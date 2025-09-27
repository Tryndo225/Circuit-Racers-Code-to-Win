using UnityEngine;

/// <summary>
/// Inspector-only attribute that marks a field as read-only in the Unity Editor.
/// </summary>
/// <remarks>
/// @ingroup editor_attrs
/// @purpose Display a value without allowing edits in the Inspector (e.g., runtime diagnostics).
/// @usage Apply to serialized fields: <c>[ReadOnly] [SerializeField] private float currentSpeed;</c>
/// @limits Has no effect at runtime; requires a matching PropertyDrawer to enforce the read-only UI.
/// </remarks>
public class ReadOnlyAttribute : PropertyAttribute
{ }

/// <summary>
/// Conditional display attribute for Inspector fields.
/// Shows (or hides) a property based on the boolean value of another field on the same object.
/// </summary>
/// <remarks>
/// @ingroup editor_attrs
/// @purpose Create compact inspectors by revealing advanced options only when relevant.
/// @usage
/// <para>Show when true:</para>
/// <code>
/// [SerializeField] private bool useAdvanced;
/// [ShowIf(nameof(useAdvanced))]
/// public float advancedGain;
/// </code>
/// <para>Show when false:</para>
/// <code>
/// [SerializeField] private bool useDefaults = true;
/// [ShowIf(nameof(useDefaults), false)]
/// public float customValue;
/// </code>
/// @limits
/// - The referenced field must be a <c>bool</c> member on the same component/object.
/// - Requires a custom <c>PropertyDrawer</c> for <see cref="ShowIfAttribute"/> to actually affect the Inspector.
/// - No runtime impact; purely an editor hint.
/// </remarks>
public class ShowIfAttribute : PropertyAttribute
{
    /// <summary>
    /// Name of the boolean field whose value controls the visibility of the decorated property.
    /// </summary>
    public readonly string Field;

    /// <summary>
    /// Required state of <see cref="Field"/> for the decorated property to be shown.
    /// If the field equals this value, the property is visible; otherwise it is hidden.
    /// </summary>
    public readonly bool RequiredState;

    /// <summary>
    /// Shows the property when the referenced boolean field is <c>true</c>.
    /// </summary>
    /// <param name="boolField">Name of a <c>bool</c> field on the same object.</param>
    public ShowIfAttribute(string boolField)
    {
        Field = boolField;
        RequiredState = true;
    }

    /// <summary>
    /// Shows the property when the referenced boolean field equals <paramref name="mustBeTrue"/>.
    /// </summary>
    /// <param name="boolField">Name of a <c>bool</c> field on the same object.</param>
    /// <param name="mustBeTrue">
    /// Desired value of the referenced field for the property to be visible.
    /// Use <c>true</c> to show-when-true, <c>false</c> to show-when-false.
    /// </param>
    public ShowIfAttribute(string boolField, bool mustBeTrue)
    {
        Field = boolField;
        RequiredState = mustBeTrue;
    }
}
