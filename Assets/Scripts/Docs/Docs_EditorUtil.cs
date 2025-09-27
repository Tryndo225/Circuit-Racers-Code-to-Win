/**
 * @file Docs_EditorUtil.cs
 * @brief Documentation entry for the Editor Utilities (property drawers & inspectors).
 *
 * @defgroup editor_util Editor Utilities
 * @ingroup tools
 * @brief Small, focused Unity Editor helpers for compact inspectors, conditional fields, and custom collections.
 *
 * @details
 * The Editor Utilities group contains custom PropertyDrawers and related editor helpers that improve authoring
 * productivity and clarity in the Inspector. These scripts are compiled only in the Unity Editor and excluded from
 * player builds via `#if UNITY_EDITOR`.
 *
 * Contents:
 * - see editor_util_overview
 * - see editor_util_assets
 * - see editor_util_drawers
 * - see editor_util_usage
 * - see editor_util_troubleshooting
 *
 * ----------------------------------------------------------------------
 * @section editor_util_overview Overview
 *
 * Responsibilities:
 * - Provide dense, one-line layouts for compound structs (e.g., WheelSpec).
 * - Show/hide properties based on boolean conditions (ShowIf).
 * - Render read-only fields in the Inspector (ReadOnly).
 * - Offer a type-switching UI for managed-reference polymorphic fields (ButtonType).
 * - Edit key/value pairs stored in a SerializableDictionary using a ReorderableList UI
 *   (StringTrackPieceDictionary).
 *
 * Scope:
 * - Editor-only quality-of-life. No runtime behavior is affected in builds.
 *
 * Threading:
 * - Unity Editor thread only.
 *
 * ----------------------------------------------------------------------
 * @section editor_util_assets Contained Assets
 *
 * Classes (Editor-only):
 * - ::ButtonTypeDrawer
 *   - Custom drawer for managed-reference fields of base type ButtonType.
 *   - Presents a "Type" popup populated via TypeCache and renders the selected concrete type inline.
 *
 * - ::ReadOnlyDrawer
 *   - Honors a [ReadOnly] attribute to render any field as disabled (non-editable) in the Inspector.
 *
 * - ::ShowIfDrawer
 *   - Honors a [ShowIf] attribute to conditionally render a field if a named boolean property equals a target state.
 *
 * - ::StringTrackPieceDictionaryDrawer
 *   - ReorderableList-based drawer for StringTrackPieceDictionary (string -> TrackPiece).
 *   - Supports add/remove/reorder, foldout per entry, duplicate-key guard, and paired key/value synchronization.
 *
 * - ::WheelSpecDrawer
 *   - Compact 1-row drawer for WheelSpec with four equal columns:
 *     Collider | Visual | Powered | Steering.
 *
 * ----------------------------------------------------------------------
 * @section editor_util_drawers Drawer Behaviors
 *
 * ButtonTypeDrawer:
 * - Uses UnityEditor.TypeCache to find all non-abstract ButtonType subclasses.
 * - Renders a popup to switch the underlying managed reference and then displays its serialized fields.
 * - Keeps existing values when changing to the same type; creates a fresh instance when the type changes.
 *
 * ReadOnlyDrawer:
 * - Wraps the field UI in EditorGUI.DisabledScope(true); preserves default height and child drawing.
 * - Useful for displaying runtime-cached references or diagnostic values in edit mode.
 *
 * ShowIfDrawer:
 * - Looks up a boolean sibling/parent property by name specified in ShowIfAttribute.
 * - If not found or not a boolean, defaults to visible to avoid hiding authoring controls accidentally.
 *
 * StringTrackPieceDictionaryDrawer:
 * - Backed by two parallel arrays (keys/values) inside SerializableDictionary.
 * - On add: inserts empty key and default TrackPiece (null prefab, zeroed transform).
 * - On remove/reorder: keeps value array synchronized with keys.
 * - Draws a foldout + inline TextField for the key and all serialized children of TrackPiece for the value.
 * - Prevents duplicate keys via a simple linear check (with Editor dialog on collision).
 *
 * WheelSpecDrawer:
 * - Single row layout with minimal padding for fast bulk editing (ideal in arrays/lists).
 * - Omits labels to save space; relies on column order and parent field tooltip/label for context.
 *
 * ----------------------------------------------------------------------
 * @section editor_util_usage Usage Examples
 *
 * ReadOnly field:
 * @code{.cs}
 * public class CarInfo : MonoBehaviour
 * {
 *     [SerializeField, ReadOnly] private Rigidbody rb;
 *     void Reset() => rb = GetComponent<Rigidbody>();
 * }
 * @endcode
 *
 * Conditional field (ShowIf):
 * @code{.cs}
 * public class Spawner : MonoBehaviour
 * {
 *     public bool useOverride;
 *     [ShowIf(nameof(useOverride), true)]
 *     public int overrideCount;
 * }
 * @endcode
 *
 * Polymorphic button action (ButtonTypeDrawer):
 * @code{.cs}
 * public class UIButton : MonoBehaviour
 * {
 *     [SerializeReference] private ButtonType onClick; // select concrete type in Inspector
 * }
 * @endcode
 *
 * String -> TrackPiece dictionary (StringTrackPieceDictionaryDrawer):
 * @code{.cs}
 * public class TrackLegend : MonoBehaviour
 * {
 *     [SerializeField] private StringTrackPieceDictionary legend;
 * }
 * @endcode
 *
 * WheelSpec array with compact drawer (WheelSpecDrawer):
 * @code{.cs}
 * public class AxleSetup : MonoBehaviour
 * {
 *     [Tooltip("Collider | Visual | Powered | Steering")]
 *     public WheelSpec[] wheels = new WheelSpec[4];
 * }
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section editor_util_troubleshooting Troubleshooting
 *
 * - Drawer not applied:
 *   - Ensure the script is inside an Editor folder or wrapped with `#if UNITY_EDITOR`.
 *   - Confirm the field type/attribute matches the drawer's [CustomPropertyDrawer] target.
 *
 * - Missing keys/values in dictionary drawer:
 *   - The SerializableDictionary must expose "keys" and "values" arrays; verify field names.
 *
 * - ShowIf does nothing:
 *   - The controlling property name must be correct and of type bool.
 *   - For nested/array contexts, the drawer attempts both sibling and root lookups; prefer sibling fields where possible.
 *
 * - ButtonType popup empty:
 *   - Ensure concrete subclasses of ButtonType are compiled in the Editor assembly (not excluded by ASMDEF).
 *   - Types must be non-abstract and non-generic to appear in the list.
 */
