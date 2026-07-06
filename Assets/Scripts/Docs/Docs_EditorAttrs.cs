/**
 * @file Docs_EditorAttrs.cs
 * @brief Documentation entry for runtime attributes used by custom Unity editor drawers.
 *
 * @defgroup editor_attrs Editor Attributes
 * @ingroup tools
 * @brief Runtime-safe Inspector attributes consumed by editor-only custom property drawers.
 *
 * @details
 * The Editor Attributes group contains lightweight PropertyAttribute types that can be used in runtime
 * scripts without depending on UnityEditor. The actual Inspector behaviour is implemented separately
 * by editor-only drawers in the ::editor_util group.
 *
 * Main attributes:
 * - ::ReadOnlyAttribute marks a serialized field as visible but non-editable in the Inspector.
 * - ::ShowIfAttribute conditionally shows a serialized field based on another boolean field.
 *
 * Contents:
 * - @ref editor_attrs_overview
 * - @ref editor_attrs_readonly
 * - @ref editor_attrs_showif
 * - @ref editor_attrs_usage
 * - @ref editor_attrs_integration
 * - @ref editor_attrs_troubleshooting
 * - @ref editor_attrs_versions
 *
 * ----------------------------------------------------------------------
 * @section editor_attrs_overview Overview
 *
 * Responsibilities:
 * - Provide runtime-safe attribute markers for custom Inspector behaviour.
 * - Keep UnityEditor references out of runtime scripts.
 * - Allow serialized fields to be shown as read-only.
 * - Allow serialized fields to be shown or hidden based on boolean conditions.
 *
 * Scope:
 * - These attributes do not implement drawing behaviour by themselves.
 * - They are metadata consumed by matching custom PropertyDrawer classes.
 * - They have no direct runtime gameplay effect.
 *
 * Dependencies:
 * - UnityEngine.PropertyAttribute.
 *
 * Related editor utilities:
 * - ::ReadOnlyDrawer in the ::editor_util group.
 * - ::ShowIfDrawer in the ::editor_util group.
 *
 * ----------------------------------------------------------------------
 * @section editor_attrs_readonly ReadOnlyAttribute
 *
 * ::ReadOnlyAttribute marks a field as displayed but not editable in the Unity Inspector.
 *
 * Purpose:
 * - Show runtime diagnostics.
 * - Show cached references.
 * - Show generated or derived state.
 * - Prevent accidental manual editing of values that should be controlled by code.
 *
 * Behaviour:
 * - Without ::ReadOnlyDrawer, the attribute is only metadata.
 * - With ::ReadOnlyDrawer, the field is drawn in a disabled GUI scope.
 * - The value can still be changed by code at runtime or edit time.
 * - The value can still be serialized by Unity.
 *
 * Limitations:
 * - Has no direct runtime effect.
 * - Does not make a field immutable.
 * - Requires the matching custom drawer for Inspector enforcement.
 *
 * ----------------------------------------------------------------------
 * @section editor_attrs_showif ShowIfAttribute
 *
 * ::ShowIfAttribute marks a field as conditionally visible in the Unity Inspector.
 *
 * Data:
 * - Field:
 *   Name of the boolean field controlling visibility.
 *
 * - RequiredState:
 *   Boolean value required for the decorated property to be shown.
 *
 * Constructors:
 * - ShowIfAttribute(string boolField):
 *   Shows the decorated property when boolField is true.
 *
 * - ShowIfAttribute(string boolField, bool mustBeTrue):
 *   Shows the decorated property when boolField equals mustBeTrue.
 *
 * Behaviour:
 * - The referenced field should be a bool on the same component or serialized object.
 * - The matching ::ShowIfDrawer evaluates the condition in the Inspector.
 * - If the condition matches RequiredState, the decorated field is visible.
 * - If the condition does not match, the decorated field is hidden.
 *
 * Limitations:
 * - Has no direct runtime effect.
 * - Requires the matching custom drawer for Inspector visibility changes.
 * - The referenced field must be a bool for intended behaviour.
 *
 * ----------------------------------------------------------------------
 * @section editor_attrs_usage Usage Examples
 *
 * Read-only diagnostic field:
 * @code{.cs}
 * public class VehicleDebugInfo : MonoBehaviour
 * {
 *     [SerializeField, ReadOnly] private float currentSpeed;
 *
 *     private void Update()
 *     {
 *         currentSpeed = GetComponent<Rigidbody>().linearVelocity.magnitude;
 *     }
 * }
 * @endcode
 *
 * Read-only cached reference:
 * @code{.cs}
 * public class CachedReferenceExample : MonoBehaviour
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
 * Show a field when a toggle is true:
 * @code{.cs}
 * public class Spawner : MonoBehaviour
 * {
 *     [SerializeField] private bool useAdvanced;
 *
 *     [SerializeField, ShowIf(nameof(useAdvanced))]
 *     private float advancedGain;
 * }
 * @endcode
 *
 * Show a field when a toggle is false:
 * @code{.cs}
 * public class Spawner : MonoBehaviour
 * {
 *     [SerializeField] private bool useDefaults = true;
 *
 *     [SerializeField, ShowIf(nameof(useDefaults), false)]
 *     private float customValue;
 * }
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section editor_attrs_integration Integration Notes
 *
 * Runtime scripts:
 * - Attributes can live in runtime assemblies because they depend only on UnityEngine.
 * - Runtime scripts can safely use [ReadOnly] and [ShowIf] without UnityEditor references.
 *
 * Editor scripts:
 * - ::ReadOnlyDrawer should be inside an Editor folder or guarded with UNITY_EDITOR.
 * - ::ShowIfDrawer should be inside an Editor folder or guarded with UNITY_EDITOR.
 *
 * Documentation grouping:
 * - Attributes belong to ::editor_attrs.
 * - Drawers belong to ::editor_util.
 *
 * Assembly definitions:
 * - If using asmdefs, the runtime assembly should contain these attributes.
 * - The editor assembly should reference the runtime assembly so drawers can target the attributes.
 *
 * ----------------------------------------------------------------------
 * @section editor_attrs_troubleshooting Troubleshooting
 *
 * ReadOnly field is still editable:
 * - Check that ::ReadOnlyDrawer exists.
 * - Check that the drawer is compiled in the editor.
 * - Check that the field is marked with [ReadOnly].
 * - Check whether another drawer overrides the same field type.
 *
 * ShowIf field is always visible:
 * - Check that ::ShowIfDrawer exists.
 * - Check that the controlling field name is spelled correctly.
 * - Check that the controlling field is a bool.
 * - Check whether the drawer intentionally fails open when it cannot resolve the condition.
 *
 * ShowIf field never appears:
 * - Check RequiredState.
 * - Use [ShowIf(nameof(field), false)] when the field should show on false.
 * - Check that the controlling field is serialized or discoverable by the drawer.
 *
 * Build fails with UnityEditor error:
 * - The attributes should not reference UnityEditor.
 * - Move only the drawer scripts into an Editor folder or UNITY_EDITOR guard.
 * - Keep these attribute classes runtime-safe.
 *
 * Attribute has no runtime effect:
 * - This is expected.
 * - These are Inspector metadata attributes, not gameplay logic.
 *
 * ----------------------------------------------------------------------
 * @section editor_attrs_versions Version History
 *
 * - v1.1: Added ShowIfAttribute with configurable required state.
 * - v1.0: Added ReadOnlyAttribute for Inspector-only read-only display.
 */