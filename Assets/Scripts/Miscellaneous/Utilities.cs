using System;
using UnityEngine;

#region SerializableRunnable

/// <summary>
/// Base class for serializable commands that expose a callable entry point through <see cref="Run"/>.
/// </summary>
/// <remarks>
/// @ingroup core_utils
/// @brief Provides a serializable command base with optional Unity serialization callbacks.
///
/// Typical use: derive a type that stores parameters in serializable fields and implement
/// <see cref="Run"/> to execute an action, for example loading a scene.
/// </remarks>
[Serializable]
public abstract class SerializableRunnable : ISerializationCallbackReceiver
{
	/// <summary>
	/// Entry point for the runnable command. Implement in derived classes.
	/// </summary>
	public abstract void Run();

	/// <summary>
	/// Unity callback invoked before serialization.
	/// </summary>
	/// <remarks>
	/// Override this to normalize or precompute data before Unity serializes the object.
	/// </remarks>
	public virtual void OnBeforeSerialize() { }

	/// <summary>
	/// Unity callback invoked after deserialization.
	/// </summary>
	/// <remarks>
	/// Override this to rebuild transient state after Unity deserializes the object.
	/// </remarks>
	public virtual void OnAfterDeserialize() { }
}

#endregion SerializableRunnable

#region LayerMaskUtils

/// <summary>
/// Utilities for working with <see cref="LayerMask"/> bitfields.
/// </summary>
/// <remarks>
/// @ingroup core_utils
/// @brief Provides helper methods for checking layer-mask contents.
/// </remarks>
public static class LayerMaskUtils
{
	/// <summary>
	/// Checks whether <paramref name="mask"/> contains the bit corresponding to <paramref name="layer"/>.
	/// </summary>
	/// <param name="mask">Mask to test.</param>
	/// <param name="layer">Layer index in the range 0 to 31.</param>
	/// <returns><c>true</c> if the layer is included in the mask; otherwise <c>false</c>.</returns>
	public static bool Contains(this LayerMask mask, int layer)
		=> (mask.value & (1 << layer)) != 0;
}

#endregion LayerMaskUtils

#region IEnumerableExtensions

namespace IEnumerableExtensions
{
	using System.Collections.Generic;

#nullable enable

	/// <summary>
	/// Extensions for <see cref="IEnumerable{T}"/> sequences.
	/// </summary>
	/// <remarks>
	/// @ingroup core_utils
	/// @brief Provides content-based helper methods for enumerable sequences.
	/// </remarks>
	public static class IEnumerableExtensions
	{
		/// <summary>
		/// Computes an order-sensitive hash value based on the contents of the sequence.
		/// </summary>
		/// <typeparam name="T">Element type.</typeparam>
		/// <param name="source">Sequence to hash. If null, returns zero.</param>
		/// <param name="comparer">
		/// Optional equality comparer used when hashing elements. If null,
		/// <see cref="EqualityComparer{T}.Default"/> is used.
		/// </param>
		/// <returns>Hash code reflecting sequence elements and order.</returns>
		/// <remarks>
		/// Uses <see cref="System.HashCode"/> aggregation. Suitable for cache keys within a session,
		/// but not intended for persistent identities.
		/// </remarks>
		public static int GetContentHash<T>(this IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
		{
			if (source is null)
				return 0;

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
	/// Minimal persistent singleton base for Unity components.
	/// </summary>
	/// <typeparam name="T">Concrete type deriving from <see cref="Singleton{T}"/>.</typeparam>
	/// <remarks>
	/// @ingroup core_utils
	/// @brief Ensures a single live instance of <typeparamref name="T"/> and keeps it across scene loads.
	///
	/// Behaviour:
	/// - The first instance becomes <see cref="Instance"/>.
	/// - Later duplicate instances destroy their own GameObject during <see cref="Awake"/>.
	/// - The surviving instance is marked with <see cref="UnityEngine.Object.DontDestroyOnLoad(UnityEngine.Object)"/>.
	///
	/// Threading:
	/// - Unity main thread only, because this class uses Unity lifecycle methods and GameObject destruction.
	/// </remarks>
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-100)]
	public class Singleton<T> : MonoBehaviour where T : Singleton<T>
	{
		/// <summary>
		/// Backing field for <see cref="Instance"/>.
		/// </summary>
		protected static T _instance;

		/// <summary>
		/// Gets the current singleton instance, or null if no instance has awoken yet.
		/// </summary>
		public static T Instance => _instance;

		#region Unity Methods

		/// <summary>
		/// Enforces the singleton rule and applies persistence to the surviving instance.
		/// </summary>
		protected virtual void Awake()
		{
			if (!Coalesce())
				return;

			Persist();
		}

		/// <summary>
		/// Attempts to make this object the active singleton instance.
		/// </summary>
		/// <returns>
		/// <c>true</c> if this object is the active instance; <c>false</c> if it was a duplicate and was destroyed.
		/// </returns>
		/// <remarks>
		/// If another instance already exists, this object's GameObject is destroyed to preserve the singleton rule.
		/// </remarks>
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

		/// <summary>
		/// Applies persistence behavior to the active singleton instance.
		/// </summary>
		/// <remarks>
		/// The base implementation keeps the GameObject alive across scene loads.
		/// Override this in derived singleton types that should remain scene-local.
		/// </remarks>
		protected virtual void Persist()
		{
			DontDestroyOnLoad(gameObject);
		}

		/// <summary>
		/// Clears the static instance reference if this object currently owns it.
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

	/// <summary>
	/// Scene-local singleton base for Unity components.
	/// </summary>
	/// <typeparam name="T">Concrete type deriving from <see cref="SceneSingleton{T}"/>.</typeparam>
	/// <remarks>
	/// @ingroup core_utils
	/// @brief Ensures a single instance in the active scene without preserving it across scene loads.
	///
	/// This class keeps the singleton coalescing behaviour from <see cref="Singleton{T}"/>,
	/// but disables <see cref="UnityEngine.Object.DontDestroyOnLoad(UnityEngine.Object)"/> persistence.
	/// </remarks>
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-50)]
	public class SceneSingleton<T> : Singleton<T> where T : SceneSingleton<T>
	{
		/// <summary>
		/// Leaves the object scene-local instead of marking it as persistent.
		/// </summary>
		/// <remarks>
		/// Intentionally empty. Scene singleton objects should be destroyed normally on scene load.
		/// </remarks>
		protected override void Persist()
		{ }
	}

	#endregion SceneSingleton
}

#endregion Generic