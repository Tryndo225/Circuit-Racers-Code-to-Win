/**
 * @file Docs_CoreUtils.cs
 * @brief Documentation entry for shared runtime utility types.
 *
 * @defgroup core_utils Core Utilities
 * @ingroup systems
 * @brief Small shared helpers for commands, layer masks, content hashing, singletons, and serializable dictionaries.
 *
 * @details
 * The Core Utilities group contains reusable runtime helpers used across multiple systems.
 * These types are intentionally small and independent so they can support scene management,
 * game data, track management, UI actions, and editor-facing serialized data without creating
 * large dependencies.
 *
 * Main utilities:
 * - ::SerializableRunnable provides a serializable command base with a Run entry point.
 * - ::LayerMaskUtils adds LayerMask helper methods.
 * - IEnumerableExtensions::IEnumerableExtensions provides content hashing for sequences.
 * - Generic::Singleton<T> provides persistent singleton behaviour.
 * - Generic::SceneSingleton<T> provides scene-local singleton behaviour.
 * - ::SerializableDictionary<TKey, TValue> provides a Unity-serializable dictionary wrapper.
 *
 * Contents:
 * - @ref core_utils_overview
 * - @ref core_utils_runnable
 * - @ref core_utils_layer_masks
 * - @ref core_utils_hashing
 * - @ref core_utils_singletons
 * - @ref core_utils_dictionary
 * - @ref core_utils_api
 * - @ref core_utils_integration
 * - @ref core_utils_troubleshooting
 * - @ref core_utils_versions
 *
 * ----------------------------------------------------------------------
 * @section core_utils_overview Overview
 *
 * Responsibilities:
 * - Provide common runtime patterns used by multiple subsystems.
 * - Keep command-like serialized actions simple and callable.
 * - Provide readable LayerMask checks.
 * - Provide an order-sensitive content hash for enumerable sequences.
 * - Provide singleton bases for persistent and scene-local managers.
 * - Provide a serializable dictionary implementation compatible with Unity serialization.
 *
 * Threading:
 * - Singleton and scene singleton lifecycle methods are Unity main thread only.
 * - SerializableRunnable callbacks are Unity serialization callbacks.
 * - SerializableDictionary serialization callbacks are Unity serialization callbacks.
 * - IEnumerableExtensions::GetContentHash is pure C# code but should still be used with stable,
 *   non-mutating sequences during the call.
 *
 * ----------------------------------------------------------------------
 * @section core_utils_runnable SerializableRunnable
 *
 * ::SerializableRunnable is an abstract base class for serializable command objects.
 *
 * Responsibilities:
 * - Expose a required Run() method.
 * - Provide overridable Unity serialization callbacks.
 * - Allow derived command objects to be stored in serialized fields and executed later.
 *
 * Typical use:
 * - Scene actions.
 * - Button strategies.
 * - Small command objects configured in the Inspector.
 *
 * Behaviour:
 * - Derived classes must implement Run().
 * - OnBeforeSerialize() is virtual and empty by default.
 * - OnAfterDeserialize() is virtual and empty by default.
 *
 * ----------------------------------------------------------------------
 * @section core_utils_layer_masks LayerMask Utilities
 *
 * ::LayerMaskUtils contains extension helpers for Unity LayerMask values.
 *
 * Contains:
 * - Checks whether a LayerMask includes a given layer index.
 * - Uses bitwise mask checking.
 *
 * Example:
 * @code{.cs}
 * if (ignoreLayers.Contains(collision.gameObject.layer))
 * {
 *     return;
 * }
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section core_utils_hashing Enumerable Content Hashing
 *
 * IEnumerableExtensions::IEnumerableExtensions provides GetContentHash.
 *
 * Purpose:
 * - Compute an order-sensitive hash from sequence contents.
 * - Support change detection for collections such as saved level lists.
 *
 * Behaviour:
 * - Returns zero for null sequences.
 * - Uses EqualityComparer<T>.Default when no comparer is supplied.
 * - Uses System.HashCode aggregation.
 * - Includes element order in the resulting hash.
 *
 * Limitations:
 * - Intended for runtime/session change detection.
 * - Not intended as a persistent identity.
 * - Hash values may differ across runtime versions or platforms.
 *
 * ----------------------------------------------------------------------
 * @section core_utils_singletons Singletons
 *
 * Generic::Singleton<T>:
 * - Provides a minimal persistent singleton base for MonoBehaviours.
 * - The first instance becomes Instance.
 * - Later duplicates destroy their own GameObject.
 * - The surviving instance is marked with DontDestroyOnLoad.
 * - OnDestroy clears Instance when the current object owns it.
 *
 * Generic::SceneSingleton<T>:
 * - Inherits from Generic::Singleton<T>.
 * - Keeps the coalescing behaviour.
 * - Overrides persistence so the object remains scene-local.
 * - Useful for managers that should reset with the scene.
 *
 * Execution order:
 * - Singleton<T> uses DefaultExecutionOrder(-100).
 * - SceneSingleton<T> uses DefaultExecutionOrder(-50).
 *
 * ----------------------------------------------------------------------
 * @section core_utils_dictionary SerializableDictionary
 *
 * ::SerializableDictionary<TKey, TValue> wraps a Dictionary<TKey, TValue> and exposes IDictionary<TKey, TValue>.
 *
 * Purpose:
 * - Unity does not serialize generic Dictionary<TKey, TValue> directly.
 * - This class mirrors dictionary contents into parallel serialized lists.
 * - Runtime access goes through the internal Dictionary<TKey, TValue>.
 *
 * Serialized backing:
 * - keys: serialized list of keys.
 * - values: serialized list of values.
 *
 * Serialization:
 * - OnBeforeSerialize clears the serialized lists and fills them from the runtime dictionary.
 * - OnAfterDeserialize rebuilds the runtime dictionary from the serialized lists.
 * - If keys and values have different counts, extra entries are ignored.
 * - Duplicate keys are ignored during deserialization with a warning.
 *
 * Runtime API:
 * - Implements IDictionary<TKey, TValue>.
 * - Supports normal dictionary operations such as Add, Remove, ContainsKey, TryGetValue, indexer,
 *   Keys, Values, Count, Clear, and enumeration.
 *
 * Optional helpers:
 * - GetOrAdd creates a value with a factory when the key is missing.
 * - Set assigns a value and returns the dictionary instance for fluent use.
 *
 * ----------------------------------------------------------------------
 * @section core_utils_api Public API Reference
 *
 * ::SerializableRunnable:
 * - void Run()
 *   Abstract command entry point implemented by derived classes.
 *
 * - void OnBeforeSerialize()
 *   Virtual Unity serialization callback.
 *
 * - void OnAfterDeserialize()
 *   Virtual Unity deserialization callback.
 *
 * ::LayerMaskUtils:
 * - bool Contains(this LayerMask mask, int layer)
 *   Returns true when the mask contains the provided layer index.
 *
 * IEnumerableExtensions::IEnumerableExtensions:
 * - int GetContentHash<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
 *   Computes an order-sensitive hash code for a sequence.
 *
 * Generic::Singleton<T>:
 * - static T Instance
 *   Gets the current singleton instance, or null if none has awoken.
 *
 * - bool Coalesce()
 *   Attempts to make the object the active singleton instance.
 *
 * - void Persist()
 *   Marks the surviving instance as DontDestroyOnLoad in the base implementation.
 *
 * Generic::SceneSingleton<T>:
 * - void Persist()
 *   Overrides persistence with an intentionally empty implementation.
 *
 * ::SerializableDictionary<TKey, TValue>:
 * - TValue this[TKey key]
 *   Gets or sets a dictionary value.
 *
 * - ICollection<TKey> Keys
 *   Gets dictionary keys.
 *
 * - ICollection<TValue> Values
 *   Gets dictionary values.
 *
 * - int Count
 *   Gets the number of entries.
 *
 * - bool IsReadOnly
 *   Always false.
 *
 * - void Add(TKey key, TValue value)
 *   Adds a key/value pair.
 *
 * - bool ContainsKey(TKey key)
 *   Checks whether a key exists.
 *
 * - bool Remove(TKey key)
 *   Removes a key.
 *
 * - bool TryGetValue(TKey key, out TValue value)
 *   Attempts to read a value by key.
 *
 * - void Clear()
 *   Removes all entries.
 *
 * - IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
 *   Enumerates dictionary entries.
 *
 * - TValue GetOrAdd(TKey key, Func<TValue> factory)
 *   Returns an existing value or creates, stores, and returns a new value.
 *
 * - SerializableDictionary<TKey, TValue> Set(TKey key, TValue value)
 *   Sets a value and returns this dictionary.
 *
 * ----------------------------------------------------------------------
 * @section core_utils_integration Integration Notes
 *
 * Scene management:
 * - ::SceneAssetHelper derives from ::SerializableRunnable so scene actions can be run through a shared command shape.
 *
 * UI:
 * - Button strategies can follow the same command-like pattern as SerializableRunnable.
 *
 * Game data:
 * - ::GameDataManager uses IEnumerableExtensions::GetContentHash for level-list change detection.
 *
 * Audio, replay, track, and scene managers:
 * - Manager components can derive from Generic::Singleton<T> or Generic::SceneSingleton<T> depending on
 *   whether they should persist across scene loads.
 *
 * Level generation and track placement:
 * - Serialized dictionaries can be used for editor-friendly mappings such as track-piece legends.
 *
 * Editor utilities:
 * - Custom drawers may depend on SerializableDictionary backing field names keys and values.
 *
 * ----------------------------------------------------------------------
 * @section core_utils_troubleshooting Troubleshooting
 *
 * Singleton Instance is null:
 * - Check that the singleton component exists in the scene.
 * - Check script execution order if another object accesses it too early.
 * - Check whether a duplicate destroyed itself before use.
 *
 * Scene singleton persists unexpectedly:
 * - Use Generic::SceneSingleton<T>, not Generic::Singleton<T>.
 * - Verify the derived class does not override Persist with DontDestroyOnLoad.
 *
 * Duplicate singleton warnings or missing manager:
 * - Keep only one intended instance in bootstrap/menu scenes.
 * - Avoid placing persistent singletons in every scene unless duplicate destruction is expected.
 *
 * SerializableDictionary loses entries:
 * - Check for duplicate keys in serialized data.
 * - Check whether keys and values lists have different lengths.
 * - Check that key and value types are Unity-serializable.
 *
 * Dictionary drawer breaks:
 * - Confirm the serialized backing fields are still named keys and values.
 *
 * Content hash does not change:
 * - Ensure the compared objects implement meaningful Equals and GetHashCode.
 * - Remember that hash values are only a change signal, not a stable identity.
 *
 * LayerMask check fails:
 * - Pass a layer index, not a layer mask value.
 * - Use gameObject.layer as the layer argument.
 *
 * ----------------------------------------------------------------------
 * @section core_utils_versions Version History
 *
 * - v1.4: Added SerializableRunnable and improved singleton documentation.
 * - v1.3: Added SerializableDictionary helper methods and duplicate-key protection.
 * - v1.2: Added enumerable content hashing.
 * - v1.1: Added scene-local singleton support.
 * - v1.0: Added basic singleton and utility helpers.
 */