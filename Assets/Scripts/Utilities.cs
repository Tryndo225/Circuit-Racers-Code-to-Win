using System;
using UnityEngine;

#region SerializableRunnable

/// <summary>
/// Base class for serializable “commands” that expose a callable entry point via <see cref="Run"/>.
/// Provides Unity serialization callbacks you can optionally override.
/// </summary>
/// <remarks>
/// @ingroup core_utils
/// Typical use: derive a type that stores parameters in serializable fields and implement
/// <see cref="Run"/> to execute an action (for example, loading a scene).
/// </remarks>
[Serializable]
public abstract class SerializableRunnable : ISerializationCallbackReceiver
{
	/// <summary>
	/// Entry point for the runnable command. Implement in derived classes.
	/// </summary>
	public abstract void Run();

	/// <summary>
	/// Unity callback invoked before serialization. Override to normalize or precompute data.
	/// </summary>
	public virtual void OnBeforeSerialize() { }

	/// <summary>
	/// Unity callback invoked after deserialization. Override to rebuild transient state.
	/// </summary>
	public virtual void OnAfterDeserialize() { }
}

#endregion SerializableRunnable

#region LayerMaskUtils

/// <summary>
/// Utilities for working with <see cref="LayerMask"/> bitfields.
/// </summary>
/// <remarks>
/// @ingroup core_utils
/// </remarks>
public static class LayerMaskUtils
{
	/// <summary>
	/// Returns true if <paramref name="mask"/> contains the bit corresponding to <paramref name="layer"/>.
	/// </summary>
	/// <param name="mask">Mask to test.</param>
	/// <param name="layer">Layer index in [0..31].</param>
	/// <returns>True if the layer is included in the mask; otherwise false.</returns>
	public static bool Contains(this LayerMask mask, int layer)
		=> (mask.value & (1 << layer)) != 0;
}

#endregion LayerMaskUtils

#region IEnumerableExtensions

namespace IEnumerableExtention
{
	using System.Collections.Generic;

#nullable enable

	/// <summary>
	/// Extensions for <see cref="IEnumerable{T}"/> sequences.
	/// </summary>
	/// <remarks>
	/// @ingroup core_utils
	/// </remarks>
	public static class IEnumerableExtensions
	{
		/// <summary>
		/// Computes an order-sensitive hash value based on the contents of the sequence.
		/// </summary>
		/// <typeparam name="T">Element type.</typeparam>
		/// <param name="source">Sequence to hash. If null, returns 0.</param>
		/// <param name="comparer">
		/// Optional equality comparer used when hashing elements. If null,
		/// <see cref="EqualityComparer{T}.Default"/> is used.
		/// </param>
		/// <returns>Hash code reflecting sequence elements and order.</returns>
		/// <remarks>
		/// Uses <see cref="System.HashCode"/> aggregation for good distribution.
		/// Suitable for cache keys within a session; not intended for persistent identities.
		/// </remarks>
		public static int GetContentHash<T>(this IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
		{
			if (source is null) return 0;

			comparer ??= EqualityComparer<T>.Default;

			var hc = new HashCode();
			foreach (var item in source)
				hc.Add(item, comparer);

			return hc.ToHashCode();
		}
	}

#nullable disable
}

#endregion IEnumerableExtensions

#region Generic

namespace Generic
{

	#region Singleton

	/// <summary>
	/// Minimal <c>DontDestroyOnLoad</c> singleton base.
	/// Ensures a single live instance of <typeparamref name="T"/>; duplicates self-destruct in <see cref="Awake"/>.
	/// </summary>
	/// <typeparam name="T">Concrete type deriving from <see cref="Singleton{T}"/>.</typeparam>
	/// <remarks>
	/// @ingroup core_utils
	/// @invariant At most one live instance exists for a given <typeparamref name="T"/>.
	/// @thread Unity main thread (Unity lifecycle methods).
	/// </remarks>
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-100)] // Awake early so managers are available to others.
	public class Singleton<T> : MonoBehaviour where T : Singleton<T>
	{
		/// <summary>Backer for <see cref="Instance"/>.</summary>
		protected static T _instance;

		/// <summary>
		/// Global accessor to the current singleton instance (or null if none has awoken yet).
		/// </summary>
		public static T Instance => _instance;

		#region Unity Methods

		/// <summary>
		/// Unity method: enforces the singleton rule and marks the instance as persistent.
		/// </summary>
		protected virtual void Awake()
		{
			if (!Coalesce())
				return;

			Persist();
		}

		protected virtual bool Coalesce()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return false;
			}

			_instance = (T)this;
			return true;
		}

		protected virtual void Persist()
		{
			DontDestroyOnLoad(gameObject);
		}


		/// <summary>
		/// Unity method: clears the static instance reference if this object owned it.
		/// </summary>
		protected virtual void OnDestroy()
		{
			if (_instance == this)
				_instance = null;
		}

		#endregion Unity Methods
	}

	#endregion Singleton

	#region SceneSingleton

	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-50)]
	public class SceneSingleton<T> : Singleton<T> where T : SceneSingleton<T>
	{
		protected override void Persist()
		{
			// Intentionally empty.
			// SceneSingleton objects should be destroyed normally on scene load.
		}
	}

	#endregion SceneSingleton

}

#endregion Generic