/**
 * @file Docs_EditorUtil.cs
 * @brief Documentation entry for custom Unity Editor utilities.
 *
 * @defgroup editor_util Editor Utilities
 * @ingroup tools
 * @brief Custom property drawers and inspector helpers for compact, safer authoring workflows.
 *
 * @details
 * The Editor Utilities group contains custom PropertyDrawers that improve the Unity Inspector
 * experience for project-specific runtime data. These scripts are editor-only and should either
 * be placed inside an Editor folder or wrapped in a UNITY_EDITOR guard so they are excluded from
 * player builds.
 *
 * The runtime attributes used by some drawers, such as ::ReadOnlyAttribute and ::ShowIfAttribute,
 * are documented separately in the editor_attrs group. This group documents the editor-side
 * drawers that interpret those attributes.
 *
 * Contents:
 * - @ref editor_util_overview
 * - @ref editor_util_assets
 * - @ref editor_util_drawers
 * - @ref editor_util_usage
 * - @ref editor_util_integration
 * - @ref editor_util_troubleshooting
 * - @ref editor_util_versions
 *
 * ----------------------------------------------------------------------
 * @section editor_util_overview Overview
 *
 * Responsibilities:
 * - Provide compact one-line layouts for compound structs such as ::WheelSpec.
 * - Show or hide Inspector fields based on boolean conditions through ::ShowIfDrawer.
 * - Render selected fields as disabled/read-only through ::ReadOnlyDrawer.
 * - Provide a type-selection UI for managed-reference polymorphic fields through ::ButtonTypeDrawer.
 * - Edit ::StringTrackPieceDictionary data with a reorderable key/value list.
 *
 * Scope:
 * - Editor-only quality-of-life.
 * - No runtime gameplay behavior is changed by these drawers.
 * - Runtime builds should not include UnityEditor-dependent code.
 *
 * Dependencies:
 * - UnityEditor.
 * - UnityEditorInternal.ReorderableList for the track-piece dictionary drawer.
 * - UnityEditor.TypeCache for discovering concrete ::ButtonType subclasses.
 * - Runtime data types such as ::ButtonType, ::WheelSpec, ::StringTrackPieceDictionary,
 *   ::ReadOnlyAttribute, and ::ShowIfAttribute.
 *
 * Threading:
 * - Unity Editor thread only.
 *
 * ----------------------------------------------------------------------
 * @section editor_util_assets Contained Assets
 *
 * Editor-only classes:
 *
 * - ::ButtonTypeDrawer
 *   Custom drawer for managed-reference fields whose base type is ::ButtonType.
 *   It shows a Type popup, discovers concrete subclasses with TypeCache, creates selected
 *   instances through reflection, and draws the selected strategy object's serialized fields inline.
 *
 * - ::ReadOnlyDrawer
 *   Drawer for fields marked with ::ReadOnlyAttribute. It preserves Unity's normal property drawing,
 *   including child fields, but wraps the GUI in a disabled scope so the value cannot be edited.
 *
 * - ::ShowIfDrawer
 *   Drawer for fields marked with ::ShowIfAttribute. It looks up a boolean condition field and only
 *   draws the decorated property when the condition matches the required state.
 *
 * - ::StringTrackPieceDictionaryDrawer
 *   ReorderableList-based drawer for ::StringTrackPieceDictionary, which stores string keys mapped
 *   to ::TrackPiece values. It keeps the hidden serialized key and value arrays synchronized.
 *
 * - ::WheelSpecDrawer
 *   Compact one-row drawer for ::WheelSpec. It displays the fields in four equal columns:
 *   collider, visual, powered, and steering.
 *
 * ----------------------------------------------------------------------
 * @section editor_util_drawers Drawer Behaviors
 *
 * ButtonTypeDrawer:
 * - Targets ::ButtonType managed references.
 * - Uses TypeCache.GetTypesDerivedFrom<ButtonType>() to find non-abstract, non-generic subclasses.
 * - Sorts discovered types by name and displays them in a Type popup.
 * - Reads the current managed-reference type from SerializedProperty.managedReferenceFullTypename.
 * - Creates a fresh instance with Activator.CreateInstance when the selected type changes.
 * - Draws the selected ButtonType instance's serialized fields below the popup.
 *
 * ReadOnlyDrawer:
 * - Targets ::ReadOnlyAttribute.
 * - Uses EditorGUI.DisabledScope(true).
 * - Uses EditorGUI.PropertyField(..., includeChildren: true), so complex values and child fields
 *   still render normally.
 * - Preserves Unity's default property height.
 *
 * ShowIfDrawer:
 * - Targets ::ShowIfAttribute.
 * - First attempts to find the condition as a sibling or parent-relative property.
 * - Falls back to a root-level property lookup.
 * - If the condition is missing or is not a boolean, the drawer fails open and shows the field.
 * - If the condition is valid, the property is shown only when its value equals
 *   ShowIfAttribute.RequiredState.
 * - Hidden fields return height 0, so no empty spacing remains in the Inspector.
 *
 * StringTrackPieceDictionaryDrawer:
 * - Targets ::StringTrackPieceDictionary.
 * - Uses ReorderableList for add, remove, reorder, header drawing, element drawing, and element height.
 * - Reads the hidden serialized backing arrays named keys and values.
 * - Keeps keys and values synchronized by size and order.
 * - Shows an error help box if the expected backing arrays cannot be found.
 * - Draws each entry as a foldout with an editable key and the serialized TrackPiece value.
 * - Prevents duplicate keys at edit time.
 *
 * WheelSpecDrawer:
 * - Targets ::WheelSpec.
 * - Draws one row with four equal-width columns:
 *   - collider,
 *   - visual,
 *   - powered,
 *   - steering.
 * - Omits per-field labels to keep wheel arrays compact.
 * - Intended mainly for VehicleController wheel setup arrays.
 *
 * ----------------------------------------------------------------------
 * @section editor_util_usage Usage Examples
 *
 * Read-only field:
 * @code{.cs}
 * public class CarInfo : MonoBehaviour
 * {
 *     [SerializeField, ReadOnly] private Rigidbody rb;
 *
 *     private void Reset()
 *     {
 *         rb = GetComponent<Rigidbody>();
 *     }
 * }
 * @endcode
 *
 * Conditional field:
 * @code{.cs}
 * public class Spawner : MonoBehaviour
 * {
 *     [SerializeField] private bool useOverride;
 *
 *     [SerializeField, ShowIf(nameof(useOverride))]
 *     private int overrideCount;
 * }
 * @endcode
 *
 * Conditional field shown when false:
 * @code{.cs}
 * public class Spawner : MonoBehaviour
 * {
 *     [SerializeField] private bool useDefaults = true;
 *
 *     [SerializeField, ShowIf(nameof(useDefaults), false)]
 *     private int customValue;
 * }
 * @endcode
 *
 * Polymorphic button action:
 * @code{.cs}
 * public class UIButton : MonoBehaviour
 * {
 *     [SerializeReference] private ButtonType onClick;
 * }
 * @endcode
 *
 * String-to-track-piece dictionary:
 * @code{.cs}
 * public class TrackLegend : MonoBehaviour
 * {
 *     [SerializeField] private StringTrackPieceDictionary legend;
 * }
 * @endcode
 *
 * Compact wheel setup:
 * @code{.cs}
 * public class AxleSetup : MonoBehaviour
 * {
 *     [Tooltip("Collider | Visual | Powered | Steering")]
 *     [SerializeField] private WheelSpec[] wheels = new WheelSpec[4];
 * }
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section editor_util_integration Integration Notes
 *
 * Attribute drawers:
 * - ::ReadOnlyDrawer depends on ::ReadOnlyAttribute.
 * - ::ShowIfDrawer depends on ::ShowIfAttribute.
 * - Keep the attributes in a runtime assembly if runtime scripts need to compile with them.
 * - Keep the drawers themselves in an Editor folder or behind UNITY_EDITOR.
 *
 * Managed references:
 * - ::ButtonTypeDrawer requires fields to use [SerializeReference].
 * - Concrete ::ButtonType subclasses must be non-abstract and non-generic to appear in the popup.
 * - If assembly definitions are used, the editor assembly containing the drawer must be able to reference
 *   the runtime assembly containing ButtonType and its subclasses.
 *
 * Serializable dictionaries:
 * - ::StringTrackPieceDictionaryDrawer expects the backing field names from ::SerializableDictionary:
 *   keys and values.
 * - Renaming those backing fields requires updating the drawer.
 *
 * Vehicle setup:
 * - ::WheelSpecDrawer assumes the field names collider, visual, powered, and steering.
 * - Renaming fields in ::WheelSpec requires updating the drawer.
 *
 * ----------------------------------------------------------------------
 * @section editor_util_troubleshooting Troubleshooting
 *
 * Drawer is not applied:
 * - Ensure the script is inside an Editor folder or wrapped with UNITY_EDITOR.
 * - Confirm the [CustomPropertyDrawer] target matches the field type or attribute.
 * - Confirm there are no compile errors in editor scripts.
 *
 * Build fails with UnityEditor namespace errors:
 * - Move the drawer script into an Editor folder.
 * - Or wrap the whole script in #if UNITY_EDITOR / #endif.
 *
 * ShowIf does nothing:
 * - Check that the controlling field name is correct.
 * - Check that the controlling field is a bool.
 * - Prefer sibling fields for predictable lookup.
 * - Remember that the drawer fails open when the condition cannot be resolved.
 *
 * ReadOnly does nothing:
 * - Confirm the field has [ReadOnly].
 * - Confirm ::ReadOnlyDrawer is compiled in the editor.
 * - Confirm no other custom drawer is overriding the same field type.
 *
 * ButtonType popup is empty:
 * - Ensure concrete ButtonType subclasses are compiled in an assembly visible to the editor drawer.
 * - Ensure subclasses are not abstract.
 * - Ensure subclasses are not generic.
 * - Ensure the field is marked with [SerializeReference].
 *
 * ButtonType loses values when changing type:
 * - Changing to a different concrete type creates a fresh instance.
 * - This is expected because different strategies have different serialized fields.
 *
 * Dictionary drawer shows backing-field error:
 * - Confirm ::SerializableDictionary still uses serialized fields named keys and values.
 * - Confirm the field type is exactly ::StringTrackPieceDictionary.
 *
 * Duplicate dictionary keys:
 * - Use unique pattern keys.
 * - The drawer prevents duplicate keys in the editor, but existing serialized duplicates may still need manual cleanup.
 *
 * WheelSpec columns are unclear:
 * - Add a tooltip or header on the parent wheel array, for example:
 *   "Collider | Visual | Powered | Steering".
 *
 * ----------------------------------------------------------------------
 * @section editor_util_versions Version History
 *
 * - v1.2: Added managed-reference ButtonType drawer and compact WheelSpec drawer.
 * - v1.1: Added ShowIf and ReadOnly drawers.
 * - v1.0: Added StringTrackPieceDictionary ReorderableList drawer.
 */