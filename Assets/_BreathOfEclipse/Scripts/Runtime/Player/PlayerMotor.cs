using System;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.Player
{
    /// <summary>
    /// CharacterController locomotion: acceleration-based running, sprint, jumps (+ air jump), gravity,
    /// forced movement for dodges / lunges / techniques, knockback, air hang for air combos and teleports.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public CharacterController Controller { get; private set; }
        public PlayerData Data { get; private set; }

        public Vector3 Velocity => _planar + _knockback + Vector3.up * _vertical;
        public Vector3 PlanarVelocity => _planar;
        public bool Grounded { get; private set; } = true;
        public bool IsForced => Time.time < _forcedUntil;
        public float VerticalVelocity => _vertical;
        public float Gravity => Data != null ? Data.gravity : -28f;
        public bool AirHanging => Time.time < _airHangUntil;
        public float TimeSinceGrounded => Grounded ? 0f : Time.time - _lastGroundedTime;

        /// <summary>Raised on landing with the downward speed.</summary>
        public event Action<float> Landed;

        private Vector3 _desiredPlanar;
        private Vector3 _planar;
        private float _vertical;
        private int _airJumpsUsed;
        private float _lastGroundedTime;
        private Vector3 _forcedVelocity;
        private float _forcedUntil;
        private bool _forcedIgnoreGravity;
        private float _airHangUntil;
        private Vector3 _knockback;
        private Vector3 _faceDirection;
        private float _turnSpeed = 900f;
        private bool _snapFacing;
        private float _gravityScale = 1f;

        public void Initialize(PlayerData data, CharacterController controller)
        {
            Data = data;
            Controller = controller;
            _faceDirection = transform.forward;
            _lastGroundedTime = Time.time;
        }

        /// <summary>Desired planar velocity (world). Magnitude is the target speed.</summary>
        public void SetDesiredVelocity(Vector3 planarVelocity)
        {
            planarVelocity.y = 0f;
            _desiredPlanar = planarVelocity;
        }

        public void Face(Vector3 worldDirection, float degreesPerSecond = -1f, bool snap = false)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;
            _faceDirection = worldDirection.normalized;
            _turnSpeed = degreesPerSecond > 0f ? degreesPerSecond : (Data != null ? Data.turnSpeed : 900f);
            if (snap) transform.rotation = Quaternion.LookRotation(_faceDirection);
            _snapFacing = snap;
        }

        public bool CanJump => Grounded || Time.time - _lastGroundedTime < 0.12f || _airJumpsUsed < (Data != null ? Data.airJumps : 1);

        public bool TryJump(float heightMultiplier = 1f)
        {
            bool ground = Grounded || Time.time - _lastGroundedTime < 0.12f;
            if (!ground)
            {
                if (_airJumpsUsed >= Data.airJumps) return false;
                _airJumpsUsed++;
            }
            _vertical = Mathf.Sqrt(2f * Mathf.Abs(Gravity) * Data.jumpHeight * heightMultiplier);
            Grounded = false;
            _lastGroundedTime = -10f;
            _airHangUntil = 0f;
            return true;
        }

        /// <summary>Overrides planar movement (and optionally gravity) for a duration.</summary>
        public void ForceMove(Vector3 velocity, float duration, bool ignoreGravity = false, bool passThroughEnemies = false)
        {
            _forcedVelocity = velocity;
            _forcedUntil = Time.time + Mathf.Max(0f, duration);
            _forcedIgnoreGravity = ignoreGravity;
            if (ignoreGravity) _vertical = velocity.y;
            SetPassThrough(passThroughEnemies);
        }

        public void StopForced()
        {
            _forcedUntil = 0f;
            _forcedVelocity = Vector3.zero;
            SetPassThrough(false);
        }

        public void SetPassThrough(bool value)
        {
            if (Controller != null) Controller.excludeLayers = value ? (LayerMask)Layers.Mask(Layers.Enemy) : (LayerMask)0;
        }

        public void SetVerticalVelocity(float v)
        {
            _vertical = v;
            if (v > 0f)
            {
                Grounded = false;
                _lastGroundedTime = -10f;
            }
        }

        /// <summary>Launches upward to reach roughly <paramref name="height"/> meters.</summary>
        public void Launch(float height)
        {
            SetVerticalVelocity(Mathf.Sqrt(2f * Mathf.Abs(Gravity) * Mathf.Max(0.1f, height)));
        }

        /// <summary>Suspends gravity (air combos). Keeps any upward velocity from decaying abruptly.</summary>
        public void AirHang(float duration)
        {
            _airHangUntil = Mathf.Max(_airHangUntil, Time.time + duration);
            if (_vertical < 0f) _vertical = 0f;
        }

        public void CancelAirHang() => _airHangUntil = 0f;

        public void SetGravityScale(float scale) => _gravityScale = scale;

        public void AddKnockback(Vector3 impulse)
        {
            impulse.y = 0f;
            _knockback += impulse;
        }

        public void Teleport(Vector3 position, Vector3? faceDirection = null)
        {
            Controller.enabled = false;
            transform.position = position;
            Controller.enabled = true;
            if (faceDirection.HasValue) Face(faceDirection.Value, -1f, true);
            _planar = Vector3.zero;
        }

        public void ResetMotion()
        {
            _planar = Vector3.zero;
            _desiredPlanar = Vector3.zero;
            _vertical = 0f;
            _knockback = Vector3.zero;
            StopForced();
            _airHangUntil = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Controller == null || !Controller.enabled) return;

            bool forced = IsForced;
            if (forced)
            {
                _planar = new Vector3(_forcedVelocity.x, 0f, _forcedVelocity.z);
            }
            else
            {
                if (_forcedVelocity != Vector3.zero)
                {
                    // Forced motion just ended.
                    _forcedVelocity = Vector3.zero;
                    SetPassThrough(false);
                }
                float accel = Data.acceleration * (Grounded ? 1f : Data.airControl);
                _planar = Vector3.MoveTowards(_planar, _desiredPlanar, accel * dt);
            }

            // Vertical
            bool hang = Time.time < _airHangUntil;
            if (forced && _forcedIgnoreGravity)
            {
                _vertical = _forcedVelocity.y;
            }
            else if (hang)
            {
                _vertical = Mathf.MoveTowards(_vertical, 0f, 30f * dt);
            }
            else if (Grounded && _vertical <= 0f)
            {
                _vertical = -2f;
            }
            else
            {
                float g = Gravity * _gravityScale;
                // Heavier fall than rise: snappier anime jumps.
                if (_vertical < 0f) g *= 1.25f;
                _vertical = Mathf.Max(-45f, _vertical + g * dt);
            }

            _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, 22f * dt);
            Vector3 motion = (_planar + _knockback) * dt + Vector3.up * (_vertical * dt);
            float fallSpeed = _vertical;
            var flags = Controller.Move(motion);
            bool groundedNow = (flags & CollisionFlags.Below) != 0 || (Controller.isGrounded && _vertical <= 0f);
            if ((flags & CollisionFlags.Above) != 0 && _vertical > 0f) _vertical = 0f;

            if (groundedNow)
            {
                if (!Grounded) Landed?.Invoke(-fallSpeed);
                _lastGroundedTime = Time.time;
                _airJumpsUsed = 0;
                if (_vertical < 0f) _vertical = -2f;
            }
            Grounded = groundedNow;

            // Rotation
            if (!_snapFacing && _faceDirection.sqrMagnitude > 0.001f)
            {
                var target = Quaternion.LookRotation(_faceDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * dt);
            }
            _snapFacing = false;
        }
    }
}
