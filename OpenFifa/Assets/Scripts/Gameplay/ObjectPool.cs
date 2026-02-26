using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Generic Unity object pool. Pre-warms instances on initialization.
    /// Get() activates an object, Return() deactivates and returns it.
    /// Zero GC during gameplay after pre-warm.
    /// </summary>
    /// <typeparam name="T">Component type to pool.</typeparam>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly T[] _instances;
        private readonly ObjectPoolLogic _logic;

        /// <summary>Number of available items in the pool.</summary>
        public int AvailableCount => _logic.AvailableCount;

        /// <summary>Number of active items.</summary>
        public int ActiveCount => _logic.ActiveCount;

        /// <summary>
        /// Create and pre-warm the pool.
        /// </summary>
        /// <param name="prefab">Prefab to instantiate.</param>
        /// <param name="capacity">Number of instances to pre-allocate.</param>
        /// <param name="parent">Parent transform for pooled objects.</param>
        public ObjectPool(T prefab, int capacity, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
            _logic = new ObjectPoolLogic(capacity);
            _instances = new T[capacity];

            // Pre-warm
            for (int i = 0; i < capacity; i++)
            {
                T instance = Object.Instantiate(_prefab, _parent);
                instance.gameObject.SetActive(false);
                _instances[i] = instance;
            }
        }

        /// <summary>
        /// Get an item from the pool. Returns null if exhausted.
        /// </summary>
        public T Get()
        {
            int index = _logic.Get();
            if (index < 0) return null;

            T instance = _instances[index];
            instance.gameObject.SetActive(true);
            return instance;
        }

        /// <summary>
        /// Return an item to the pool.
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;

            for (int i = 0; i < _instances.Length; i++)
            {
                if (_instances[i] == item)
                {
                    item.gameObject.SetActive(false);
                    _logic.Return(i);
                    return;
                }
            }
        }
    }
}
