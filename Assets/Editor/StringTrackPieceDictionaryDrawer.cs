#if UNITY_EDITOR

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(StringTrackPieceDictionary))]
public class StringTrackPieceDictionaryDrawer : PropertyDrawer
{
    private ReorderableList list;
    private SerializedProperty keysProp, valuesProp;

    // ReorderableList draws a drag handle at the far left; leave room so our foldout isn't covered
    private const float HANDLE_GUTTER = 18f; // space for the built-in drag handle

    private const float PAD = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        Ensure(property);
        return list != null ? list.GetHeight() : EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Ensure(property);
        if (list == null)
        {
            EditorGUI.HelpBox(position, "Backing fields 'keys'/'values' not found.", MessageType.Error);
            return;
        }

        SyncSizes(keysProp, valuesProp);
        list.DoList(position);
    }

    private void Ensure(SerializedProperty property)
    {
        if (list != null) return;

        keysProp = property.FindPropertyRelative("keys");
        valuesProp = property.FindPropertyRelative("values");

        if (keysProp == null || valuesProp == null) return;

        SyncSizes(keysProp, valuesProp);

        list = new ReorderableList(property.serializedObject, keysProp, true, true, true, true);

        list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, property.displayName + "  (string -> TrackPiece)");
        };

        list.onAddCallback = l =>
        {
            int i = keysProp.arraySize;
            keysProp.arraySize++;
            valuesProp.arraySize++;

            keysProp.GetArrayElementAtIndex(i).stringValue = "";

            var v = valuesProp.GetArrayElementAtIndex(i);
            if (v != null)
            {
                var prefabSP = v.FindPropertyRelative("prefab");
                var posSP = v.FindPropertyRelative("position");
                var rotSP = v.FindPropertyRelative("rotation");
                if (prefabSP != null) prefabSP.objectReferenceValue = null;
                if (posSP != null) posSP.vector3Value = Vector3.zero;
                if (rotSP != null) rotSP.quaternionValue = Quaternion.identity;
            }

            property.serializedObject.ApplyModifiedProperties();
        };

        list.onRemoveCallback = l =>
        {
            int i = l.index;
            if (i < 0 || i >= keysProp.arraySize) return;
            keysProp.DeleteArrayElementAtIndex(i);
            if (i < valuesProp.arraySize) valuesProp.DeleteArrayElementAtIndex(i);
            property.serializedObject.ApplyModifiedProperties();
            SyncSizes(keysProp, valuesProp);
        };

        // keep value list in the same order as keys
        list.onReorderCallbackWithDetails = (l, oldIndex, newIndex) =>
        {
            MoveArrayElementSafe(valuesProp, oldIndex, newIndex);
            property.serializedObject.ApplyModifiedProperties();
        };

        list.drawElementCallback = (rect, index, active, focused) =>
        {
            var key = keysProp.GetArrayElementAtIndex(index);
            var val = valuesProp.GetArrayElementAtIndex(index);
            if (key == null || val == null) return;

            float line = EditorGUIUtility.singleLineHeight;
            float vsp = EditorGUIUtility.standardVerticalSpacing;
            float pad = 2f;

            // foldout (leave space for reorderable-list handle on the far left)
            var foldRect = new Rect(rect.x + HANDLE_GUTTER, rect.y + pad, 16f, line);
            val.isExpanded = EditorGUI.Foldout(foldRect, val.isExpanded, GUIContent.none, true);

            // key field
            var keyRect = new Rect(foldRect.xMax + 4f, rect.y + pad, rect.width - (foldRect.width + HANDLE_GUTTER + 6f), line);
            EditorGUI.BeginChangeCheck();
            string newKey = EditorGUI.TextField(keyRect, key.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                if (!IsDuplicateKey(keysProp, index, newKey)) key.stringValue = newKey;
                else EditorUtility.DisplayDialog("Duplicate Key", $"Key '{newKey}' already exists.", "OK");
            }

            if (!val.isExpanded) return;

            // draw all children of TrackPiece generically (prefab/position/rotation, etc.)
            float x = rect.x + HANDLE_GUTTER + 14f;
            float w = rect.width - (HANDLE_GUTTER + 14f);
            float y = keyRect.yMax + vsp;

            var child = val.Copy();
            var end = val.GetEndProperty();
            bool enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false; // only descend once from the value
                float h = EditorGUI.GetPropertyHeight(child, true);
                var r = new Rect(x, y, w, h);
                EditorGUI.PropertyField(r, child, true);
                y += h + vsp;
            }
        };

        // Height: key line + all children heights when expanded
        list.elementHeightCallback = index =>
        {
            var val = valuesProp.GetArrayElementAtIndex(index);
            float line = EditorGUIUtility.singleLineHeight;
            float vsp = EditorGUIUtility.standardVerticalSpacing;

            float h = line + 6f; // key row
            if (val != null && val.isExpanded)
            {
                var child = val.Copy();
                var end = val.GetEndProperty();
                bool enterChildren = true;
                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;
                    h += EditorGUI.GetPropertyHeight(child, true) + vsp;
                }
            }
            return h;
        };
    }

    // ---------- helpers ----------
    private static void SyncSizes(SerializedProperty keys, SerializedProperty values)
    {
        if (keys == null || values == null) return;
        if (values.arraySize != keys.arraySize)
            values.arraySize = keys.arraySize; // keep paired
    }

    private static void MoveArrayElementSafe(SerializedProperty array, int oldIndex, int newIndex)
    {
        if (array == null) return;
        int n = array.arraySize;
        if (oldIndex < 0 || oldIndex >= n || newIndex < 0 || newIndex >= n) return;
        array.MoveArrayElement(oldIndex, newIndex);
    }

    private static bool IsDuplicateKey(SerializedProperty keys, int selfIndex, string candidate)
    {
        for (int i = 0; i < keys.arraySize; i++)
        {
            if (i == selfIndex) continue;
            if (keys.GetArrayElementAtIndex(i).stringValue == candidate)
                return true;
        }
        return false;
    }
}

#endif