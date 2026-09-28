using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    /// <summary>
    /// Enemy locomotion on a CharacterController: steering with obstacle feelers, gravity, knockback,
    /// launches and air juggles (air hang), pulls from vortex techniques.
    /// </summary>
    public sealed class EnemyMotor : MonoBehaviour
    {
        public CharacterController Controller { get; private set; }
        public bool Grounded { get; private set; } = true;
        public Vector3 Velocity => _planar + _external + Vector3.up * _vertical;
        public float VerticalVelocity => _vertical;
        public bool Airborne => !Grounded;

        public float Gravity = -26f;
        public float TurnSpeed = 540f;
        public float KnockbackResistance;

        private Vector3 _desired;
        private Vector3 _planar;
        private Vector3 _external;
        private float _vertical;
        private float _airHangUntil;
        private Vector3 _faceDir;
        private Vector3 _forced;
        private float _forcedUntil;
        private static readonly RaycastHit[] Feelers = new RaycastHit[4];

        public void Initialize(CharacterController controller)
        {
            Controller = controller;
            _faceDir = transform.forward;
        }

        public void SetDesiredVelocity(Vector3 v)
        {
            v.y = 0f;
            _desired = v;
        }

        /// <summary>Moves toward a point with simple obstacle avoidance.</summary>
        public void MoveTowards(Vector3 point, float speed, float stopDistance = 0.1f)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= stopDistance)
            {
                SetDesiredVelocity(Vector3.zero);
                return;
            }
            Vector3 dir = to / dist;
            dir = Avoid(dir);
            SetDesiredVelocity(dir * speed);
            Face(dir);
        }

        private Vector3 Avoid(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f * transform.localScale.y;
            float r = Controller != null ? Controller.radius * 0.9f : 0.3f;
            if (Physics.SphereCastNonAlloc(origin, r, dir, Feelers, 1.6f, Layers.EnvironmentMask | Layers.Mask(Layers.Destructible), QueryTriggerInteraction.Ignore) == 0)
                return dir;
            // Try steering left/right.
            for (int i = 1; i <= 3; i++)
            {
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector3 alt = Quaternion.Euler(0f, s * i * 35f, 0f) * dir;
                    if (Physics.SphereCastNonAlloc(origin, r, alt, Feelers, 1.6f, Layers.EnvironmentMask | Layers.Mask(Layers.Destructible), QueryTriggerInteraction.Ignore) == 0)
                        return alt;
                }
            }
            return dir;
        }

        public void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) _faceDir = dir.normalized;
        }

        public void FaceInstant(Vector3 dir)
        {
            Face(dir);
            if (_faceDir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(_faceDir);
        }

        public void Knockback(Vector3 impulse)
        {
            impulse.y = 0f;
            _external += impulse * (1f - KnockbackResistance);
        }

        public void Launch(float height, float airHang)
        {
            _vertical = Mathf.Sqrt(2f * Mathf.Abs(Gravity) * Mathf.Max(0.2f, height));
            Grounded = false;
            if (airHang > 0f) _airHangUntil = Time.time + airHang + 0.35f;
        }

        /// <summary>Keeps a juggled enemy floating while it keeps getting hit.</summary>
        public void AirHang(float duration)
        {
            if (Grounded) return;
            _airHangUntil = Mathf.Max(_airHangUntil, Time.time + duration);
            if (_vertical < 0f) _vertical = 0f;
        }

        public void ForceMove(Vector3 velocity, float duration)
        {
            _forced = velocity;
            _forcedUntil = Time.time + duration;
        }

        public void Jump(float height) => Launch(height, 0f);

        public void Teleport(Vector3 position)
        {
            Controller.enabled = false;
            transform.position = position;
            Controller.enabled = true;
        }

        public void Stop()
        {
            _desired = Vector3.zero;
            _planar = Vector3.zero;
            _forcedUntil = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Controller == null || !Controller.enabled) return;

            bool forced = Time.time < _forcedUntil;
            _planar = forced ? new Vector3(_forced.x, 0f, _forced.z) : Vector3.MoveTowards(_planar, Grounded ? _desired : _desired * 0.3f, 30f * dt);
            _external = Vector3.MoveTowards(_external, Vector3.zero, (Grounded ? 18f : 6f) * dt);

            if (Time.time < _airHangUntil && !Grounded) _vertical = Mathf.MoveTowards(_vertical, 0f, 25f * dt);
            else if (Grounded && _vertical <= 0f) _vertical = -2f;
            else _vertical = Mathf.Max(-40f, _vertical + Gravity * (_vertical < 0f ? 1.2f : 1f) * dt);

            var flags = Controller.Move((_planar + _external) * dt + Vector3.up * (_vertical * dt));
            Grounded = (flags & CollisionFlags.Below) != 0 || (Controller.isGrounded && _vertical <= 0f);
            if (Grounded && _vertical < 0f) _vertical = -2f;

            if (_faceDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_faceDir), TurnSpeed * dt);
        }
    }
}
