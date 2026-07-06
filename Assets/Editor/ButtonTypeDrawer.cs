using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for polymorphic <see cref="ButtonType"/> managed references.
/// Renders a type-selection popup (for concrete subclasses) and the chosen instance's fields inline.
/// </summary>
/// <remarks>
/// @ingroup editor_util
/// @thread Unity Editor thread only. Editor-only code; excluded from player builds.
/// @usage Add a field like <c>[SerializeReference] private ButtonType _action;</c> on a component,
/// then this drawer will allow choosing among non-abstract <see cref="ButtonType"/> subclasses
/// and editing their serialized fields.
/// </remarks>
[CustomPropertyDrawer(typeof(ButtonType), true)]
public class ButtonTypeDrawer : PropertyDrawer
{
	/// <summary>
	/// Cache of discovered, non-abstract, non-generic subclasses of <see cref="ButtonType"/>,
	/// sorted by name. Populated on first draw via <see cref="EnsureTypeList"/>.
	/// </summary>
	private List<Type> _types;

	/// <summary>
	/// Cached display names corresponding to <see cref="_types"/> for the popup.
	/// </summary>
	private string[] _typeNames;

	/// <summary>
	/// Ensures <see cref="_types"/> and <see cref="_typeNames"/> are populated with
	/// concrete subclasses of <see cref="ButtonType"/> using <see cref="TypeCache"/>.
	/// </summary>
	private void EnsureTypeList()
	{
		if (_types != null) return;

		_types = TypeCache.GetTypesDerivedFrom<ButtonType>()
						  .Where(t => !t.IsAbstract && !t.IsGenericType)
						  .OrderBy(t => t.Name)
						  .ToList();

		_typeNames = _types.Select(t => t.Name).ToArray();
	}

	/// <summary>
	/// Calculates the required GUI height for the drawer: popup line + body height + padding.
	/// </summary>
	/// <param name="property">The managed reference property.</param>
	/// <param name="label">The label for the property.</param>
	/// <returns>Total height in pixels.</returns>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUI.GetPropertyHeight(property, label, includeChildren: true)
			 + EditorGUIUtility.singleLineHeight
			 + 4f;
	}

	/// <summary>
	/// Draws the type popup and, below it, the serialized fields of the selected
	/// <see cref="ButtonType"/> instance.
	/// </summary>
	/// <param name="position">Drawing rect.</param>
	/// <param name="property">Managed reference property to draw.</param>
	/// <param name="label">Inspector label.</param>
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EnsureTypeList();

		// Popup row
		var popupRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

		// Resolve current type and index for the popup
		var fullTypeName = property.managedReferenceFullTypename;
		var currentType = GetTypeFromManagedReferenceFullTypename(fullTypeName);
		var currentIndex = currentType != null ? _types.IndexOf(currentType) : -1;
		if (currentIndex < 0 && _types.Count > 0) currentIndex = 0;

		EditorGUI.BeginProperty(position, label, property);
		EditorGUI.BeginChangeCheck();
		int newIndex = EditorGUI.Popup(popupRect, "Type", currentIndex, _typeNames);
		if (EditorGUI.EndChangeCheck())
		{
			SetManagedReferenceToNewInstance(property, newIndex >= 0 && newIndex < _types.Count ? _types[newIndex] : null);
		}

		// Body (selected instance fields)
		var bodyRect = new Rect(position.x, popupRect.yMax + 4f, position.width,
								position.height - EditorGUIUtility.singleLineHeight - 4f);
		EditorGUI.PropertyField(bodyRect, property, new GUIContent(label.text), includeChildren: true);

		EditorGUI.EndProperty();
	}

	/// <summary>
	/// Converts Unity's managed reference typename (format: "AssemblyName TypeFullName")
	/// to a <see cref="Type"/> by searching loaded assemblies.
	/// </summary>
	/// <param name="fullTypename">Managed reference typename string.</param>
	/// <returns>Resolved <see cref="Type"/> or <c>null</c> if not found.</returns>
	private static Type GetTypeFromManagedReferenceFullTypename(string fullTypename)
	{
		if (string.IsNullOrEmpty(fullTypename)) return null;
		int space = fullTypename.IndexOf(' ');
		if (space < 0) return null;

		string assemblyName = fullTypename.Substring(0, space);
		string typeName = fullTypename.Substring(space + 1);

		foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (!asm.GetName().Name.Equals(assemblyName, StringComparison.Ordinal)) continue;
			var t = asm.GetType(typeName);
			if (t != null) return t;
		}
		return null;
	}

	/// <summary>
	/// Replaces the managed reference object with a new instance of <paramref name="newType"/>,
	/// or clears it when <paramref name="newType"/> is <c>null</c>. Wraps the change in
	/// SerializedObject Update/Apply.
	/// </summary>
	/// <param name="property">Managed reference property being edited.</param>
	/// <param name="newType">Concrete type to instantiate (or null to clear).</param>
	private static void SetManagedReferenceToNewInstance(SerializedProperty property, Type newType)
	{
		property.serializedObject.Update();

		if (newType == null)
		{
			property.managedReferenceValue = null;
		}
		else
		{
			var instance = Activator.CreateInstance(newType);
			property.managedReferenceValue = instance;
		}

		property.serializedObject.ApplyModifiedProperties();
	}
}
