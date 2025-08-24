#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return IsVisible(property) ? EditorGUI.GetPropertyHeight(property, label, true) : 0f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (IsVisible(property))
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    private bool IsVisible(SerializedProperty property)
    {
        var attrib = (ShowIfAttribute)attribute;

        string path = property.propertyPath;
        int lastDot = path.LastIndexOf('.');
        string pathToField = (lastDot >= 0 ? path.Substring(0, lastDot + 1) : "") + attrib.Field;

        SerializedProperty cond = property.serializedObject.FindProperty(pathToField) ?? property.serializedObject.FindProperty(attrib.Field);

        if (cond == null || cond.propertyType != SerializedPropertyType.Boolean)
        {
            return true;
        }

        return cond.boolValue == attrib.RequiredState;
    }
}

#endif