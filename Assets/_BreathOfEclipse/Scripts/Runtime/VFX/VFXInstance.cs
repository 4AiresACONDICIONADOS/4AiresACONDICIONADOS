using System.Collections.Generic;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>Custom animated effect parts (ribbons, rings, bolts, lights) restart through this interface.</summary>
    public interface IVfxPart
    {
        void Restart();
    }

    /// <summary>
    /// Root of a pooled effect. Restarts every particle system and animated part when spawned and returns
    /// itself to the pool after its lifetime.
    /// </summary>
    public sealed class VFXInstance : MonoBehaviour, IPoolable
    {
        public string Id;
        public float DefaultLifetime = 2f;

        private ParticleSystem[] _systems;
        private IVfxPart[] _parts;
        private PooledObject _pooled;

        public void Cache()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _parts = GetComponentsInChildren<IVfxPart>(true);
            _pooled = GetComponent<PooledObject>();
        }

        public void OnSpawned()
        {
            if (_systems == null) Cache();
            if (_pooled == null) _pooled = GetComponent<PooledObject>();
            if (_pooled != null && _pooled.Lifetime <= 0f) _pooled.Lifetime = DefaultLifetime;
            for (int i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                ps.Clear(false);
                ps.Play(false);
            }
            for (int i = 0; i < _parts.Length; i++) _parts[i].Restart();
        }

        public void OnReleased()
        {
            if (_systems == null) return;
            for (int i = 0; i < _systems.Length; i++) _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_pooled != null) _pooled.Lifetime = DefaultLifetime;
        }

        /// <summary>Stops emitting but lets live particles finish (used by looping auras).</summary>
        public void StopEmitting()
        {
            if (_systems == null) return;
            for (int i = 0; i < _systems.Length; i++) _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Release()
        {
            if (_pooled != null) _pooled.Release();
            else Destroy(gameObject);
        }

        public void SetLifetime(float seconds)
        {
            if (_pooled == null) _pooled = GetComponent<PooledObject>();
            if (_pooled != null) _pooled.Lifetime = seconds;
        }
    }

    /// <summary>Scales particle emission counts by graphics quality.</summary>
    public static class VFXQuality
    {
        public static float Density = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Density = 1f;

        public static void Apply(GraphicsQuality quality)
        {
            switch (quality)
            {
                case GraphicsQuality.Low: Density = 0.45f; break;
                case GraphicsQuality.Medium: Density = 0.75f; break;
                case GraphicsQuality.Ultra: Density = 1.25f; break;
                default: Density = 1f; break;
            }
        }

        public static int Count(int baseCount) => Mathf.Max(1, Mathf.RoundToInt(baseCount * Density));
    }
}
