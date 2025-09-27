using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-serializable wrapper around <see cref="Dictionary{TKey, TValue}"/> that round-trips
/// keys and values via parallel serialized lists.
/// </summary>
/// <remarks>
/// @ingroup core_utils
/// @details
/// Unity cannot serialize generic dictionaries directly. This class mirrors the runtime
/// dictionary into two parallel lists during serialization and reconstructs it after load.
/// <para>
/// Notes:
/// - Keys must be unique. Duplicate keys found on load are ignored with a warning.
/// - Keys and values must be Unity-serializable types (or custom serializable types).
/// - Order is not guaranteed (this behaves like a normal <c>Dictionary</c>).
/// </para>
/// @invariant <c>keys.Count</c> and <c>values.Count</c> are kept in sync during serialization.
/// @thread Unity main thread for Unity methods; standard collection access otherwise.
/// </remarks>
[Serializable]
public class SerializableDictionary<TKey, TValue> :
    ISerializationCallbackReceiver,
    IDictionary<TKey, TValue>
{
    #region Serialized Backing (Unity Inspector)

    [SerializeField]
    private List<TKey> keys = new List<TKey>();

    [SerializeField]
    private List<TValue> values = new List<TValue>();

    #endregion

    #region Runtime Storage

    /// <summary>
    /// The actual dictionary used at runtime. All IDictionary operations
    /// forward to this instance.
    /// </summary>
    private readonly Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

    #endregion

    #region Unity Methods (Serialization)

    /// <summary>
    /// Unity method: called before serialization. Copies runtime contents
    /// into the parallel key/value lists.
    /// </summary>
    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();

        foreach (var kvp in dictionary)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }
    }

    /// <summary>
    /// Unity method: called after deserialization. Rebuilds the runtime dictionary
    /// from the parallel key/value lists, guarding against length mismatches and duplicates.
    /// </summary>
    public void OnAfterDeserialize()
    {
        dictionary.Clear();

        if (keys.Count != values.Count)
        {
            Debug.LogWarning(
                $"[SerializableDictionary] Keys ({keys.Count}) and Values ({values.Count}) count mismatch; " +
                $"extra entries will be ignored."
            );
        }

        var comparer = EqualityComparer<TKey>.Default;
        var seen = new HashSet<TKey>(comparer);
        int count = Mathf.Min(keys.Count, values.Count);

        for (int i = 0; i < count; i++)
        {
            var key = keys[i];

            // Deduplicate keys to mirror Dictionary behavior and avoid exceptions.
            if (!seen.Add(key))
            {
                Debug.LogWarning($"[SerializableDictionary] Duplicate key ignored during load: {key}");
                continue;
            }

            dictionary[key] = values[i];
        }
    }

    #endregion

    #region IDictionary<TKey, TValue> (Core API)

    /// <inheritdoc/>
    public TValue this[TKey key]
    {
        get => dictionary[key];
        set => dictionary[key] = value;
    }

    /// <inheritdoc/>
    public ICollection<TKey> Keys => dictionary.Keys;

    /// <inheritdoc/>
    public ICollection<TValue> Values => dictionary.Values;

    /// <inheritdoc/>
    public int Count => dictionary.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(TKey key, TValue value) => dictionary.Add(key, value);

    /// <inheritdoc/>
    public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);

    /// <inheritdoc/>
    public bool Remove(TKey key) => dictionary.Remove(key);

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);

    /// <inheritdoc/>
    public void Add(KeyValuePair<TKey, TValue> item) => dictionary.Add(item.Key, item.Value);

    /// <inheritdoc/>
    public void Clear() => dictionary.Clear();

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        if (!dictionary.TryGetValue(item.Key, out var v)) return false;
        return EqualityComparer<TValue>.Default.Equals(v, item.Value);
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        => ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (Contains(item))
            return dictionary.Remove(item.Key);
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dictionary.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => dictionary.GetEnumerator();

    #endregion

    #region Utility (Optional Helpers)

    /// <summary>
    /// Gets the value for <paramref name="key"/> or adds <paramref name="factory"/> result
    /// if the key is missing, returning the stored value.
    /// </summary>
    /// <param name="key">Lookup key.</param>
    /// <param name="factory">Factory to create a value when missing.</param>
    /// <returns>Existing or newly created value.</returns>
    public TValue GetOrAdd(TKey key, Func<TValue> factory)
    {
        if (dictionary.TryGetValue(key, out var existing))
            return existing;

        var created = factory != null ? factory() : default;
        dictionary[key] = created;
        return created;
    }

    /// <summary>
    /// Sets <paramref name="value"/> for <paramref name="key"/> and returns the dictionary (fluent).
    /// </summary>
    public SerializableDictionary<TKey, TValue> Set(TKey key, TValue value)
    {
        dictionary[key] = value;
        return this;
    }

    #endregion
}
