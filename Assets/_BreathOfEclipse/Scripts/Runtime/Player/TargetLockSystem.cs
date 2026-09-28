using System.Collections.Generic;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Player
{
    /// <summary>
    /// Action-RPG lock-on: acquires the enemy closest to the screen center, switches left/right, keeps the
    /// camera framed, auto-switches when the target dies and releases on distance / manual toggle.
    /// </summary>
    public sealed class TargetLockSystem : MonoBehaviour
    {
        public float AcquireDistance = 24f;
        public float BreakDistance = 32f;

        public ITargetable Current { get; private set; }
        public Transform CurrentPoint => Current != null && Current.IsTargetable ? Current.LockOnPoint : null;
        public bool IsBoss => Current != null && Current.IsBoss;

        private readonly List<ITargetable> _candidates = new List<ITargetable>();
        private float _occludedTime;

        private void OnEnable()
        {
            if (InputReader.Instance == null) return;
            InputReader.Instance.LockOnPressed += Toggle;
            InputReader.Instance.SwitchTargetRequested += Switch;
        }

        private void OnDisable()
        {
            if (InputReader.Instance == null) return;
            InputReader.Instance.LockOnPressed -= Toggle;
            InputReader.Instance.SwitchTargetRequested -= Switch;
        }

        public void Toggle()
        {
            if (Current != null)
            {
                Release();
                return;
            }
            var best = FindBest(null, 0);
            if (best != null) SetTarget(best);
            else if (CameraRig.Instance != null) CameraRig.Instance.RecenterBehindPlayer();
        }

        public void Release() => SetTarget(null);

        public void Switch(int direction)
        {
            if (Current == null) return;
            var next = FindBest(Current, direction);
            if (next != null) SetTarget(next);
        }

        private void SetTarget(ITargetable t)
        {
            if (t == Current) return;
            Current = t;
            _occludedTime = 0f;
            GameEvents.RaiseLockTargetChanged(CurrentPoint);
        }

        private Camera Cam => CameraRig.Instance != null ? CameraRig.Instance.Camera : Camera.main;

        /// <summary>direction 0 = closest to screen center; +1/-1 = nearest on that side of the current target.</summary>
        private ITargetable FindBest(ITargetable relativeTo, int direction)
        {
            var cam = Cam;
            TargetRegistry.InRadius(transform.position, AcquireDistance, _candidates);
            ITargetable best = null;
            float bestScore = float.MaxValue;
            float refX = 0.5f;
            if (relativeTo != null && cam != null && relativeTo.LockOnPoint != null)
                refX = cam.WorldToViewportPoint(relativeTo.LockOnPoint.position).x;

            foreach (var c in _candidates)
            {
                if (c == relativeTo || c.LockOnPoint == null) continue;
                Vector3 p = c.LockOnPoint.position;
                float dist = Vector3.Distance(transform.position, p);
                if (cam == null)
                {
                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        best = c;
                    }
                    continue;
                }
                Vector3 vp = cam.WorldToViewportPoint(p);
                bool onScreen = vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
                if (direction == 0)
                {
                    float centerOffset = onScreen ? Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f)) : 2f;
                    float score = centerOffset * 10f + dist * 0.25f + (c.IsBoss ? -1f : 0f);
                    if (HitQuery.Blocked(cam.transform.position, p)) score += 6f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }
                else
                {
                    if (!onScreen) continue;
                    float dx = (vp.x - refX) * direction;
                    if (dx <= 0.01f) continue;
                    float score = dx + dist * 0.01f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }
            }
            return best;
        }

        private void Update()
        {
            if (Current == null) return;
            bool invalid = !Current.IsTargetable || Current.LockOnPoint == null;
            if (!invalid)
            {
                float dist = Vector3.Distance(transform.position, Current.LockOnPoint.position);
                if (dist > BreakDistance) invalid = true;
                var cam = Cam;
                if (cam != null && HitQuery.Blocked(transform.position + Vector3.up * 1.5f, Current.LockOnPoint.position))
                {
                    _occludedTime += Time.unscaledDeltaTime;
                    if (_occludedTime > 2.5f) invalid = true;
                }
                else _occludedTime = 0f;
            }
            if (invalid)
            {
                // Auto-switch to the next closest enemy so group fights flow.
                var previous = Current;
                Current = null;
                var next = FindBest(previous, 0);
                SetTargetForced(next);
            }
        }

        private void SetTargetForced(ITargetable t)
        {
            Current = t;
            GameEvents.RaiseLockTargetChanged(CurrentPoint);
        }
    }
}
