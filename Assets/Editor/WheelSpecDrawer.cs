#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(WheelSpec))]
public class WheelSpecDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight + 4f;
    }

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