using System;
using BreathOfEclipse.AI;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Lightweight performance sampling for playtests: FPS (average / 0.5 s window minimum / maximum), frame time,
    /// spikes, active enemies, active pooled VFX, approximate live particles and managed memory.
    /// Frames during scene loading are excluded from minimum and spike statistics.
    /// </summary>
    public sealed class RuntimePerformanceMonitor
    {
        [Serializable]
        public struct Summary
        {
            public float averageFps;
            public float minimumFps;
            public float maximumFps;
            public float averageFrameMs;
            public float worstFrameMs;
            public int spikes;
            public int peakEnemies;
            public int peakVfx;
            public float averageVfx;
            public int peakParticles;
            public float memoryStartMb;
            public float memoryEndMb;
            public float memoryPeakMb;
        }

        private float _time;
        private int _frames;
        private float _windowTime;
        private int _windowFrames;
        private float _minWindowFps = float.MaxValue;
        private float _maxWindowFps;
        private float _worstFrame;
        private int _spikes;
        private float _sampleTimer;
        private float _particleTimer;
        private int _peakEnemies, _peakVfx, _peakParticles;
        private float _vfxSum;
        private int _vfxSamples;
        private float _memStart, _memPeak;
        private bool _running;

        public float CurrentFps { get; private set; }
        public int ActiveEnemies { get; private set; }
        public int ActiveVfx { get; private set; }
        public int Particles { get; private set; }

        public void Begin()
        {
            _time = 0f;
            _frames = 0;
            _windowTime = 0f;
            _windowFrames = 0;
            _minWindowFps = float.MaxValue;
            _maxWindowFps = 0f;
            _worstFrame = 0f;
            _spikes = 0;
            _peakEnemies = _peakVfx = _peakParticles = 0;
            _vfxSum = 0f;
            _vfxSamples = 0;
            _memStart = _memPeak = MemoryMb();
            _running = true;
        }

        public void Stop() => _running = false;

        private static float MemoryMb() => GC.GetTotalMemory(false) / (1024f * 1024f);

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            bool loading = SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;

            _windowTime += dt;
            _windowFrames++;
            if (_windowTime >= 0.5f)
            {
                CurrentFps = _windowFrames / _windowTime;
                if (_running && !loading)
                {
                    _minWindowFps = Mathf.Min(_minWindowFps, CurrentFps);
                    _maxWindowFps = Mathf.Max(_maxWindowFps, CurrentFps);
                }
                _windowTime = 0f;
                _windowFrames = 0;
            }
            if (!_running) return;

            _time += dt;
            _frames++;
            if (!loading)
            {
                _worstFrame = Mathf.Max(_worstFrame, dt);
                if (dt > 0.05f) _spikes++;
            }

            _sampleTimer -= dt;
            if (_sampleTimer <= 0f)
            {
                _sampleTimer = 0.5f;
                ActiveEnemies = EncounterDirector.AliveCount;
                ActiveVfx = PoolManager.Instance != null ? PoolManager.Instance.CountActive("vfx") : 0;
                _peakEnemies = Mathf.Max(_peakEnemies, ActiveEnemies);
                _peakVfx = Mathf.Max(_peakVfx, ActiveVfx);
                _vfxSum += ActiveVfx;
                _vfxSamples++;
                _memPeak = Mathf.Max(_memPeak, MemoryMb());
            }

            _particleTimer -= dt;
            if (_particleTimer <= 0f)
            {
                _particleTimer = 1f;
                int count = 0;
                foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    count += ps.particleCount;
                Particles = count;
                _peakParticles = Mathf.Max(_peakParticles, count);
            }
        }

        public Summary GetSummary()
        {
            return new Summary
            {
                averageFps = _time > 0f ? _frames / _time : 0f,
                minimumFps = _minWindowFps == float.MaxValue ? 0f : _minWindowFps,
                maximumFps = _maxWindowFps,
                averageFrameMs = _frames > 0 ? _time / _frames * 1000f : 0f,
                worstFrameMs = _worstFrame * 1000f,
                spikes = _spikes,
                peakEnemies = _peakEnemies,
                peakVfx = _peakVfx,
                averageVfx = _vfxSamples > 0 ? _vfxSum / _vfxSamples : 0f,
                peakParticles = _peakParticles,
                memoryStartMb = _memStart,
                memoryEndMb = MemoryMb(),
                memoryPeakMb = _memPeak
            };
        }
    }
}
