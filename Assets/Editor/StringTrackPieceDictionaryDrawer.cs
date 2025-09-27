#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Custom inspector drawer for <see cref="StringTrackPieceDictionary"/> that presents
/// a reorderable, foldout-friendly key/value list (string -> <c>TrackPiece</c>).
/// </summary>
/// <remarks>
/// @ingroup editor_util
/// Features:
/// - Uses <see cref="ReorderableList"/> for drag reordering, add/remove, and custom element drawing.
/// - Keeps the hidden <c>keys</c> and <c>values</c> backing arrays strictly in sync (size &amp; order).
/// - Prevents duplicate keys at edit time with a simple dialog warning.
/// - Draws the value (<c>TrackPiece</c>) generically, including prefab, position, and rotation fields.
/// Threading: Unity Editor thread only; excluded from player builds by <c>#if UNITY_EDITOR</c>.
/// </remarks>
[CustomPropertyDrawer(typeof(StringTrackPieceDictionary))]
public class StringTrackPieceDictionaryDrawer : PropertyDrawer
{
    /// <summary>Backed <see cref="ReorderableList"/> used to render and manage the dictionary items.</summary>
    private ReorderableList list;

    /// <summary>Serialized reference to the hidden <c>keys</c> array.</summary>
    private SerializedProperty keysProp;

    /// <summary>Serialized reference to the hidden <c>values</c> array.</summary>
    private SerializedProperty valuesProp;

    // ReorderableList draws a drag handle at the far left; leave room so our foldout isn't covered

    /// <summary>
    /// Horizontal padding reserved for the built-in drag handle, so the foldout icon doesn't overlap.
    /// </summary>
    private const float HANDLE_GUTTER = 18f;

    /// <summary>Small vertical padding used in element drawing.</summary>
    private const float PAD = 2f;

    /// <summary>
    /// Returns the full height required to draw the property, delegating to the
    /// underlying <see cref="ReorderableList"/> once initialized.
    /// </summary>
    /// <param name="property">Root property for the dictionary.</param>
    /// <param name="label">GUI label.</param>
    /// <returns>Required pixel height.</returns>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        Ensure(property);
        return list != null ? list.GetHeight() : EditorGUIUtility.singleLineHeight;
    }

    /// <summary>
    /// Main GUI entry point: ensures the list is built, syncs sizes, and renders the reorderable list.
    /// Shows an error help box if the expected backing arrays cannot be found.
    /// </summary>
    /// <param name="position">Draw rect.</param>
    /// <param name="property">Root property for the dictionary.</param>
    /// <param name="label">GUI label.</param>
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

    /// <summary>
    /// Lazily initializes the <see cref="ReorderableList"/> and wires all callbacks (header, add, remove,
    /// reorder, element draw, element height). Also binds <see cref="keysProp"/> and <see cref="valuesProp"/>.
    /// </summary>
    /// <param name="property">Root property for the dictionary.</param>
    private void Ensure(SerializedProperty property)
    {
        if (list != null) return;

        keysProp = property.FindPropertyRelative("keys");
        valuesProp = property.FindPropertyRelative("values");

        if (keysProp == null || valuesProp == null) return;

        SyncSizes(keysProp, valuesProp);

        list = new ReorderableList(property.serializedObject, keysProp, true, true, true, true);

        // Header
        list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, property.displayName + "  (string -> TrackPiece)");
        };

        // Add
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

        // Remove
        list.onRemoveCallback = l =>
        {
            int i = l.index;
            if (i < 0 || i >= keysProp.arraySize) return;
            keysProp.DeleteArrayElementAtIndex(i);
            if (i < valuesProp.arraySize) valuesProp.DeleteArrayElementAtIndex(i);
            property.serializedObject.ApplyModifiedProperties();
            SyncSizes(keysProp, valuesProp);
        };

        // Reorder (keep values aligned with keys)
        list.onReorderCallbackWithDetails = (l, oldIndex, newIndex) =>
        {
            MoveArrayElementSafe(valuesProp, oldIndex, newIndex);
            property.serializedObject.ApplyModifiedProperties();
        };

        // Per-element draw
        list.drawElementCallback = (rect, index, active, focused) =>
        {
            var key = keysProp.GetArrayElementAtIndex(index);
            var val = valuesProp.GetArrayElementAtIndex(index);
            if (key == null || val == null) return;

            float line = EditorGUIUtility.singleLineHeight;
            float vsp = EditorGUIUtility.standardVerticalSpacing;
            float pad = PAD;

            // Foldout (leave space for reorderable-list handle on the far left)
            var foldRect = new Rect(rect.x + HANDLE_GUTTER, rect.y + pad, 16f, line);
            val.isExpanded = EditorGUI.Foldout(foldRect, val.isExpanded, GUIContent.none, true);

            // Key field
            var keyRect = new Rect(
                foldRect.xMax + 4f,
                rect.y + pad,
                rect.width - (foldRect.width + HANDLE_GUTTER + 6f),
                line
            );

            EditorGUI.BeginChangeCheck();
            string newKey = EditorGUI.TextField(keyRect, key.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                if (!IsDuplicateKey(keysProp, index, newKey)) key.stringValue = newKey;
                else EditorUtility.DisplayDialog("Duplicate Key", $"Key '{newKey}' already exists.", "OK");
            }

            if (!val.isExpanded) return;

            // Draw all children of TrackPiece generically (prefab/position/rotation, etc.)
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

        // Dynamic element height: key line + all children heights when expanded
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

    // ---------- Helpers ----------

    /// <summary>
    /// Ensures the <c>values</c> array matches the size of the <c>keys</c> array.
    /// </summary>
    /// <param name="keys">Serialized array of keys.</param>
    /// <param name="values">Serialized array of values.</param>
    private static void SyncSizes(SerializedProperty keys, SerializedProperty values)
    {
        if (keys == null || values == null) return;
        if (values.arraySize != keys.arraySize)
            values.arraySize = keys.arraySize; // keep paired
    }

    /// <summary>
    /// Safe wrapper around <see cref="SerializedProperty.MoveArrayElement(int, int)"/> with bounds checks.
    /// </summary>
    /// <param name="array">Array property to reorder.</param>
    /// <param name="oldIndex">Original index.</param>
    /// <param name="newIndex">Destination index.</param>
    private static void MoveArrayElementSafe(SerializedProperty array, int oldIndex, int newIndex)
    {
        if (array == null) return;
        int n = array.arraySize;
        if (oldIndex < 0 || oldIndex >= n || newIndex < 0 || newIndex >= n) return;
        array.MoveArrayElement(oldIndex, newIndex);
    }

    /// <summary>
    /// Returns true if <paramref name="candidate"/> already exists in <paramref name="keys"/>
    /// at any index other than <paramref name="selfIndex"/>.
    /// </summary>
    /// <param name="keys">Key array to scan.</param>
    /// <param name="selfIndex">Index being edited.</param>
    /// <param name="candidate">Proposed new key value.</param>
    /// <returns>True when a duplicate is found; otherwise false.</returns>
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
