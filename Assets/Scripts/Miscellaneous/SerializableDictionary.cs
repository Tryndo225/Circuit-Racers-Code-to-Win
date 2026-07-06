using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-serializable wrapper around <see cref="Dictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// @ingroup core_utils
/// @brief Stores dictionary data through parallel serialized key and value lists.
///
/// Unity cannot serialize generic dictionaries directly. This class mirrors the runtime
/// dictionary into two parallel lists before serialization and reconstructs it after loading.
///
/// Notes:
/// - Keys must be unique.
/// - Duplicate keys found during deserialization are ignored with a warning.
/// - Keys and values must be Unity-serializable types or custom serializable types.
/// - Iteration order is not guaranteed, matching normal <see cref="Dictionary{TKey, TValue}"/> behavior.
///
/// Serialization:
/// - <c>keys</c> and <c>values</c> are kept in sync during serialization.
/// - Runtime dictionary operations are forwarded to the internal dictionary.
/// </remarks>
[Serializable]
public class SerializableDictionary<TKey, TValue> :
	ISerializationCallbackReceiver,
	IDictionary<TKey, TValue>
{
	#region Serialized Backing (Unity Inspector)

	/// <summary>
	/// Serialized dictionary keys.
	/// </summary>
	/// <remarks>
	/// This list is paired with <see cref="values"/> during Unity serialization.
	/// </remarks>
	[Tooltip("Serialized dictionary keys. Kept in sync with the values list.")]
	[SerializeField]
	private List<TKey> keys = new List<TKey>();

	/// <summary>
	/// Serialized dictionary values.
	/// </summary>
	/// <remarks>
	/// Each entry corresponds to the key at the same index in <see cref="keys"/>.
	/// </remarks>
	[Tooltip("Serialized dictionary values. Each value corresponds to the key at the same index.")]
	[SerializeField]
	private List<TValue> values = new List<TValue>();

	#endregion

	#region Runtime Storage

	/// <summary>
	/// Runtime dictionary used by all <see cref="IDictionary{TKey, TValue}"/> operations.
	/// </summary>
	private readonly Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

	#endregion

	#region Unity Methods (Serialization)

	/// <summary>
	/// Copies runtime dictionary contents into serialized key and value lists before Unity serialization.
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
	/// Rebuilds the runtime dictionary from serialized key and value lists after Unity deserialization.
	/// </summary>
	/// <remarks>
	/// If the serialized lists have different lengths, extra entries are ignored. Duplicate keys are also
	/// ignored so reconstruction does not throw an exception.
	/// </remarks>
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
	/// Gets the value for <paramref name="key"/>, or creates and stores a new value when the key is missing.
	/// </summary>
	/// <param name="key">Lookup key.</param>
	/// <param name="factory">Factory used to create a value when the key is missing.</param>
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
	/// Sets a value for a key and returns this dictionary for fluent usage.
	/// </summary>
	/// <param name="key">Key to set.</param>
	/// <param name="value">Value to store.</param>
	/// <returns>This dictionary instance.</returns>
	public SerializableDictionary<TKey, TValue> Set(TKey key, TValue value)
	{
		dictionary[key] = value;
		return this;
	}

	#endregion
}