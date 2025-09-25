using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver, IDictionary<TKey, TValue>
{
    [SerializeField]
    private List<TKey> keys = new List<TKey>();

    [SerializeField]
    private List<TValue> values = new List<TValue>();

    private Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

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

    public void OnAfterDeserialize()
    {
        dictionary.Clear();

        for (int i = 0; i < keys.Count; i++)
        {
            dictionary[keys[i]] = values[i];
        }
    }

    public TValue this[TKey key]
    {
        get => dictionary[key];
        set => dictionary[key] = value;
    }

    public void Add(TKey key, TValue value) => dictionary.Add(key, value);

    public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);

    public bool Remove(TKey key) => dictionary.Remove(key);

    public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);

    public ICollection<TKey> Keys => dictionary.Keys;
    public ICollection<TValue> Values => dictionary.Values;

    public void Add(KeyValuePair<TKey, TValue> item) => dictionary.Add(item.Key, item.Value);

    public void Clear() => dictionary.Clear();

    public bool Contains(KeyValuePair<TKey, TValue> item) => dictionary.ContainsKey(item.Key) && dictionary[item.Key].Equals(item.Value);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey, TValue>)dictionary).CopyTo(array, arrayIndex);

    public bool Remove(KeyValuePair<TKey, TValue> item) => dictionary.Remove(item.Key);

    public int Count => dictionary.Count;
    public bool IsReadOnly => ((IDictionary<TKey, TValue>)dictionary).IsReadOnly;

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dictionary.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => dictionary.GetEnumerator();
}