using UnityEngine;

/// <summary>
/// Inspector-only attribute that marks a field as read-only in the Unity Editor.
/// </summary>
/// <remarks>
/// @ingroup editor_attrs
/// @brief Marks serialized fields that should be displayed but not edited in the Inspector.
///
/// Purpose:
/// - Display a value without allowing edits in the Inspector.
/// - Useful for runtime diagnostics, cached references, and generated state.
///
/// Usage:
/// <code>
/// [ReadOnly]
/// [SerializeField] private float currentSpeed;
/// </code>
///
/// Limitations:
/// - Has no effect at runtime.
/// - Requires a matching custom <c>PropertyDrawer</c> to enforce the read-only Inspector UI.
/// </remarks>
public class ReadOnlyAttribute : PropertyAttribute
{ }

/// <summary>
/// Conditional display attribute for Inspector fields.
/// </summary>
/// <remarks>
/// @ingroup editor_attrs
/// @brief Shows or hides a property based on the boolean value of another field on the same object.
///
/// This attribute is used to create more compact inspectors by revealing fields only when they are relevant.
///
/// Show when true:
/// <code>
/// [SerializeField] private bool useAdvanced;
///
/// [ShowIf(nameof(useAdvanced))]
/// public float advancedGain;
/// </code>
///
/// Show when false:
/// <code>
/// [SerializeField] private bool useDefaults = true;
///
/// [ShowIf(nameof(useDefaults), false)]
/// public float customValue;
/// </code>
///
/// Limitations:
/// - The referenced field must be a <c>bool</c> member on the same component or object.
/// - Requires a custom <c>PropertyDrawer</c> for <see cref="ShowIfAttribute"/> to affect the Inspector.
/// - Has no runtime effect.
/// </remarks>
public class ShowIfAttribute : PropertyAttribute
{
	/// <summary>
	/// Name of the boolean field whose value controls the visibility of the decorated property.
	/// </summary>
	public readonly string Field;

	/// <summary>
	/// Required state of <see cref="Field"/> for the decorated property to be shown.
	/// </summary>
	/// <remarks>
	/// If the referenced field equals this value, the decorated property is visible.
	/// Otherwise, it is hidden by the matching property drawer.
	/// </remarks>
	public readonly bool RequiredState;

	/// <summary>
	/// Creates an attribute that shows the property when the referenced boolean field is <c>true</c>.
	/// </summary>
	/// <param name="boolField">Name of a <c>bool</c> field on the same object.</param>
	public ShowIfAttribute(string boolField)
	{
		Field = boolField;
		RequiredState = true;
	}

	/// <summary>
	/// Creates an attribute that shows the property when the referenced boolean field equals a required value.
	/// </summary>
	/// <param name="boolField">Name of a <c>bool</c> field on the same object.</param>
	/// <param name="mustBeTrue">
	/// Required value of the referenced field for the property to be visible.
	/// Use <c>true</c> to show when true, or <c>false</c> to show when false.
	/// </param>
	public ShowIfAttribute(string boolField, bool mustBeTrue)
	{
		Field = boolField;
		RequiredState = mustBeTrue;
	}
}