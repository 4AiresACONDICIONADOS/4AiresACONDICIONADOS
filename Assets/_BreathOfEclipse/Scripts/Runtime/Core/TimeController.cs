using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Single owner of Time.timeScale. Combines hit stop, slow motion requests, pause and debug time scale.
    /// Everything runs on unscaled time so UI and the controller itself never freeze.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class TimeController : MonoBehaviour
    {
        private struct SlowMotionRequest
        {
            public string Id;
            public float Scale;
            public float End;
            public float EaseOut;
        }

        public static TimeController Instance { get; private set; }

        private readonly List<SlowMotionRequest> _requests = new List<SlowMotionRequest>();
        private float _hitStopEnd;
        private float _baseFixedDelta = 1f / 60f;
        private bool _paused;

        /// <summary>Global multiplier from the debug menu (1 = normal).</summary>
        public float DebugScale { get; set; } = 1f;
        public bool Paused
        {
            get => _paused;
            set
            {
                _paused = value;
                Apply();
            }
        }

        public bool InHitStop => Time.unscaledTime < _hitStopEnd;
        public float CurrentScale { get; private set; } = 1f;

        /// <summary>Development freeze (VFX inspection): game time stops, rendering and UI keep running.</summary>
        public bool DevFrozen
        {
            get => _devFrozen;
            set
            {
                _devFrozen = value;
                _pendingSteps = 0;
                EndStep();
                Apply();
            }
        }

        private bool _devFrozen;
        private int _pendingSteps;
        private bool _stepping;
        private const float StepDelta = 1f / 60f;

        /// <summary>While <see cref="DevFrozen"/>, advances the game by exactly one 1/60 s frame.</summary>
        public void StepFrame()
        {
            if (!_devFrozen) return;
            _pendingSteps++;
        }

        private void EndStep()
        {
            if (!_stepping) return;
            _stepping = false;
            Time.captureDeltaTime = 0f;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _baseFixedDelta = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 1f / 60f;
            Services.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Services.Unregister(this);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
            if (_stepping) Time.captureDeltaTime = 0f;
        }

        /// <summary>Freezes gameplay for <paramref name="duration"/> real seconds. Overlapping requests keep the longest.</summary>
        public void HitStop(float duration)
        {
            if (duration <= 0f) return;
            DevTelemetry.ReportHitStop(duration);
            float end = Time.unscaledTime + duration;
            if (end > _hitStopEnd) _hitStopEnd = end;
            Apply();
        }

        /// <summary>Requests slow motion. Requests with the same id replace each other; the slowest active request wins.</summary>
        public void SlowMotion(float scale, float duration, string id = null, float easeOut = 0.12f)
        {
            if (duration <= 0f) return;
            if (!string.IsNullOrEmpty(id)) _requests.RemoveAll(r => r.Id == id);
            _requests.Add(new SlowMotionRequest
            {
                Id = id,
                Scale = Mathf.Clamp(scale, 0.02f, 1f),
                End = Time.unscaledTime + duration,
                EaseOut = Mathf.Min(easeOut, duration)
            });
            Apply();
        }

        public void CancelSlowMotion(string id)
        {
            _requests.RemoveAll(r => r.Id == id);
            Apply();
        }

        /// <summary>Clears everything (scene transitions, respawn).</summary>
        public void ResetAll()
        {
            _requests.Clear();
            _hitStopEnd = 0f;
            _paused = false;
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            float now = Time.unscaledTime;
            float scale;
            if (_devFrozen && !_paused)
            {
                // One fixed 1/60 s game frame per requested step, then frozen again.
                if (_pendingSteps > 0 && !_stepping)
                {
                    _pendingSteps--;
                    _stepping = true;
                    Time.captureDeltaTime = StepDelta;
                    scale = 1f;
                }
                else
                {
                    EndStep();
                    scale = 0f;
                }
            }
            else if (_paused)
            {
                scale = 0f;
            }
            else if (now < _hitStopEnd)
            {
                scale = 0f;
            }
            else
            {
                scale = 1f;
                for (int i = _requests.Count - 1; i >= 0; i--)
                {
                    var r = _requests[i];
                    if (now >= r.End)
                    {
                        _requests.RemoveAt(i);
                        continue;
                    }
                    float s = r.Scale;
                    float easeStart = r.End - r.EaseOut;
                    if (r.EaseOut > 0f && now > easeStart) s = Mathf.Lerp(r.Scale, 1f, (now - easeStart) / r.EaseOut);
                    if (s < scale) scale = s;
                }
                scale *= Mathf.Max(0.01f, DebugScale);
            }

            CurrentScale = scale;
            Time.timeScale = scale;
            // Keep physics stepping smooth in slow motion; never set a zero fixed step.
            Time.fixedDeltaTime = _baseFixedDelta * Mathf.Clamp(scale, 0.05f, 1f);
        }
    }
}
