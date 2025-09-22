using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(ButtonType), true)]
public class ButtonTypeDrawer : PropertyDrawer
{
    private List<Type> _types;
    private string[] _typeNames;

    private void EnsureTypeList()
    {
        if (_types != null) return;

        _types = TypeCache.GetTypesDerivedFrom<ButtonType>()
                          .Where(t => !t.IsAbstract && !t.IsGenericType)
                          .OrderBy(t => t.Name)
                          .ToList();

        _typeNames = _types.Select(t => t.Name).ToArray();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, includeChildren: true) + EditorGUIUtility.singleLineHeight + 4f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureTypeList();

        var popupRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

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

        var bodyRect = new Rect(position.x, popupRect.yMax + 4f, position.width,
                                position.height - EditorGUIUtility.singleLineHeight - 4f);
        EditorGUI.PropertyField(bodyRect, property, new GUIContent(label.text), includeChildren: true);

        EditorGUI.EndProperty();
    }

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