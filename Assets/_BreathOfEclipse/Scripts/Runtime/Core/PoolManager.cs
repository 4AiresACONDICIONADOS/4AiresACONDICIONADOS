using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>Implemented by components on pooled objects that must reset their state.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnReleased();
    }

    /// <summary>Marks an object as belonging to a pool and handles automatic release.</summary>
    public sealed class PooledObject : MonoBehaviour
    {
        public string PoolKey { get; internal set; }
        /// <summary>Seconds until automatic release. 0 or less = manual release.</summary>
        public float Lifetime { get; set; }
        /// <summary>When true, lifetime counts unscaled seconds (UI elements such as damage numbers).</summary>
        public bool UseUnscaledTime { get; set; }
        public bool IsActiveInstance { get; internal set; }

        private float _age;
        private IPoolable[] _poolables;

        internal void NotifySpawned()
        {
            _age = 0f;
            IsActiveInstance = true;
            if (_poolables == null) _poolables = GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < _poolables.Length; i++) _poolables[i].OnSpawned();
        }

        internal void NotifyReleased()
        {
            IsActiveInstance = false;
            if (_poolables == null) _poolables = GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < _poolables.Length; i++) _poolables[i].OnReleased();
        }

        /// <summary>Re-scan children for <see cref="IPoolable"/> (call after adding components at runtime).</summary>
        public void RefreshPoolables() => _poolables = GetComponentsInChildren<IPoolable>(true);

        private void Update()
        {
            if (Lifetime <= 0f) return;
            _age += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_age >= Lifetime) Release();
        }

        public void Release()
        {
            if (!IsActiveInstance) return;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }

    /// <summary>
    /// Keyed GameObject pools. Everything spawned often (VFX, projectiles, debris, damage numbers) goes through here
    /// so combat never Instantiates/Destroys in the hot path.
    /// </summary>
    public sealed class PoolManager : MonoBehaviour
    {
        private sealed class Pool
        {
            public readonly Stack<GameObject> Inactive = new Stack<GameObject>();
            public readonly List<GameObject> Active = new List<GameObject>();
            public Func<GameObject> Factory;
            public Transform Root;
        }

        public static PoolManager Instance { get; private set; }

        private readonly Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();

        public int PoolCount => _pools.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Services.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Services.Unregister(this);
        }

        public bool HasPool(string key) => _pools.ContainsKey(key);

        /// <summary>Registers a factory for a key (optional; <see cref="Spawn"/> can register lazily).</summary>
        public void Register(string key, Func<GameObject> factory, int prewarm = 0)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                pool = new Pool { Factory = factory };
                var rootGo = new GameObject("Pool_" + key);
                rootGo.transform.SetParent(transform, false);
                pool.Root = rootGo.transform;
                _pools[key] = pool;
            }
            else if (factory != null)
            {
                pool.Factory = factory;
            }

            for (int i = 0; i < prewarm; i++)
            {
                var go = Create(key, pool);
                if (go == null) break;
                go.SetActive(false);
                pool.Inactive.Push(go);
            }
        }

        /// <summary>Gets an instance from the pool (creating it with <paramref name="factory"/> if needed).</summary>
        public GameObject Spawn(string key, Func<GameObject> factory, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                Register(key, factory);
                pool = _pools[key];
            }
            else if (pool.Factory == null)
            {
                pool.Factory = factory;
            }

            GameObject go = null;
            while (pool.Inactive.Count > 0 && go == null) go = pool.Inactive.Pop();
            if (go == null) go = Create(key, pool);
            if (go == null) return null;

            var t = go.transform;
            t.SetParent(parent != null ? parent : pool.Root, false);
            if (parent != null)
            {
                t.SetPositionAndRotation(position, rotation);
            }
            else
            {
                t.position = position;
                t.rotation = rotation;
            }
            go.SetActive(true);
            pool.Active.Add(go);
            var pooled = go.GetComponent<PooledObject>();
            pooled.NotifySpawned();
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null || string.IsNullOrEmpty(pooled.PoolKey) || !_pools.TryGetValue(pooled.PoolKey, out var pool))
            {
                Destroy(go);
                return;
            }
            if (!pooled.IsActiveInstance) return;
            pooled.NotifyReleased();
            pool.Active.Remove(go);
            go.SetActive(false);
            go.transform.SetParent(pool.Root, false);
            pool.Inactive.Push(go);
        }

        /// <summary>Returns every active instance to its pool (scene transitions).</summary>
        public void ReleaseAll()
        {
            foreach (var pool in _pools.Values)
            {
                for (int i = pool.Active.Count - 1; i >= 0; i--)
                {
                    var go = pool.Active[i];
                    if (go == null)
                    {
                        pool.Active.RemoveAt(i);
                        continue;
                    }
                    Release(go);
                }
            }
        }

        /// <summary>Removes destroyed instances (objects parented to an unloaded scene).</summary>
        public void Prune()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Active.RemoveAll(g => g == null);
                if (pool.Inactive.Count == 0) continue;
                var alive = new List<GameObject>(pool.Inactive);
                pool.Inactive.Clear();
                for (int i = alive.Count - 1; i >= 0; i--)
                    if (alive[i] != null) pool.Inactive.Push(alive[i]);
            }
        }

        private static GameObject Create(string key, Pool pool)
        {
            if (pool.Factory == null)
            {
                Debug.LogError($"[PoolManager] No factory registered for '{key}'.");
                return null;
            }
            var go = pool.Factory();
            if (go == null) return null;
            go.transform.SetParent(pool.Root, false);
            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null) pooled = go.AddComponent<PooledObject>();
            pooled.PoolKey = key;
            return go;
        }
    }
}
