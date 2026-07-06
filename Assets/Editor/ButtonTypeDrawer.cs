#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for polymorphic <see cref="ButtonType"/> managed references.
/// </summary>
/// <remarks>
/// @ingroup editor_util
/// @brief Renders a type-selection popup for concrete <see cref="ButtonType"/> subclasses and draws the selected instance inline.
///
/// This drawer is editor-only and should either be placed inside an Editor folder or wrapped in a
/// <c>UNITY_EDITOR</c> guard.
///
/// Usage:
/// Add a managed-reference field such as:
/// <code>
/// [SerializeReference] private ButtonType _action;
/// </code>
/// The drawer then allows choosing among non-abstract <see cref="ButtonType"/> subclasses and editing
/// their serialized fields.
///
/// Threading:
/// - Unity Editor thread only.
/// </remarks>
[CustomPropertyDrawer(typeof(ButtonType), true)]
public class ButtonTypeDrawer : PropertyDrawer
{
	/// <summary>
	/// Cache of discovered, non-abstract, non-generic subclasses of <see cref="ButtonType"/>,
	/// sorted by name.
	/// </summary>
	/// <remarks>
	/// Populated on first draw through <see cref="EnsureTypeList"/>.
	/// </remarks>
	private List<Type> _types;

	/// <summary>
	/// Cached display names corresponding to <see cref="_types"/> for the popup.
	/// </summary>
	private string[] _typeNames;

	/// <summary>
	/// Ensures the type cache is populated with concrete <see cref="ButtonType"/> subclasses.
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
	/// Calculates the required GUI height for the drawer.
	/// </summary>
	/// <param name="property">Managed-reference property being drawn.</param>
	/// <param name="label">Inspector label.</param>
	/// <returns>Total height in pixels.</returns>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUI.GetPropertyHeight(property, label, includeChildren: true)
			 + EditorGUIUtility.singleLineHeight
			 + 4f;
	}

	/// <summary>
	/// Draws the type popup and the serialized fields of the selected <see cref="ButtonType"/> instance.
	/// </summary>
	/// <param name="position">Drawing rectangle.</param>
	/// <param name="property">Managed-reference property to draw.</param>
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
	/// Converts Unity's managed-reference type name into a runtime <see cref="Type"/>.
	/// </summary>
	/// <param name="fullTypename">Managed-reference type name string.</param>
	/// <returns>Resolved type, or <c>null</c> if the type cannot be found.</returns>
	/// <remarks>
	/// Unity stores managed-reference type names in the format:
	/// <c>AssemblyName TypeFullName</c>.
	/// </remarks>
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
	/// Replaces the managed-reference object with a new instance of the selected type.
	/// </summary>
	/// <param name="property">Managed-reference property being edited.</param>
	/// <param name="newType">Concrete type to instantiate, or <c>null</c> to clear the reference.</param>
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
#endif