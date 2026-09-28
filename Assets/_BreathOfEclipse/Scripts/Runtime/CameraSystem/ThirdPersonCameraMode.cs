using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.CameraSystem
{
    /// <summary>
    /// Main anime action-RPG camera: orbit, collision, lock-on framing, auto recentering, dynamic FOV
    /// and distance changes (wider for giant attacks, closer for parries / finishers).
    /// </summary>
    public sealed class ThirdPersonCameraMode : ICameraMode
    {
        public float Distance = 5.2f;
        public float PivotHeight = 1.55f;
        public float ShoulderOffset = 0.38f;
        public float MinPitch = -35f;
        public float MaxPitch = 70f;
        public float CollisionRadius = 0.22f;

        private float _yaw;
        private float _pitch = 14f;
        private float _currentDistance = 5.2f;
        private float _shoulder;
        private float _yawVelocity, _pitchVelocity;
        private Vector3 _pivotSmoothed;
        private bool _hasPivot;
        private float _recenterUntil;

        public float Yaw => _yaw;
        /// <summary>Diagnostics: geometry is pulling the camera closer than desired.</summary>
        public bool Obstructed { get; private set; }
        public float Pitch => _pitch;

        public void Enter(CameraPose current, CameraContext ctx)
        {
            Vector3 e = current.Rotation.eulerAngles;
            _yaw = e.y;
            _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), MinPitch, MaxPitch);
            _hasPivot = false;
        }

        /// <summary>Snaps the camera behind the player over a short ease.</summary>
        public void RequestRecenter(float duration = 0.3f) => _recenterUntil = Time.unscaledTime + duration;

        public void SetOrbit(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = pitch;
        }

        public CameraPose Evaluate(CameraContext ctx)
        {
            float dt = ctx.DeltaTime;
            float scale = Mathf.Max(0.5f, ctx.PlayerScale);
            Vector3 playerPos = ctx.Player.position;

            // Pivot follows the player with light smoothing (vertical smoothing hides jump jitter).
            Vector3 pivotTarget = playerPos + Vector3.up * (PivotHeight * scale);
            if (!_hasPivot)
            {
                _pivotSmoothed = pivotTarget;
                _hasPivot = true;
            }
            _pivotSmoothed.x = Mathf.Lerp(_pivotSmoothed.x, pivotTarget.x, 1f - Mathf.Exp(-22f * dt));
            _pivotSmoothed.z = Mathf.Lerp(_pivotSmoothed.z, pivotTarget.z, 1f - Mathf.Exp(-22f * dt));
            _pivotSmoothed.y = Mathf.Lerp(_pivotSmoothed.y, pivotTarget.y, 1f - Mathf.Exp(-9f * dt));

            bool locked = ctx.LockTarget != null;
            if (locked)
            {
                Vector3 toTarget = ctx.LockTarget.position - _pivotSmoothed;
                Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
                float flatDist = flat.magnitude;
                if (flatDist > 0.5f)
                {
                    float desiredYaw = Quaternion.LookRotation(flat).eulerAngles.y;
                    float desiredPitch = Mathf.Clamp(12f - Mathf.Atan2(toTarget.y, flatDist) * Mathf.Rad2Deg * 0.5f + (ctx.LockTargetIsBoss ? 4f : 0f), -10f, 40f);
                    _yaw = Mathf.SmoothDampAngle(_yaw, desiredYaw, ref _yawVelocity, 0.16f, 900f, dt);
                    _pitch = Mathf.SmoothDampAngle(_pitch, desiredPitch + ctx.LookDelta.y * -0.5f, ref _pitchVelocity, 0.25f, 400f, dt);
                }
            }
            else
            {
                _yaw += ctx.LookDelta.x;
                _pitch = Mathf.Clamp(_pitch - ctx.LookDelta.y, MinPitch, MaxPitch);

                float playerYaw = ctx.Player.eulerAngles.y;
                bool manualRecenter = Time.unscaledTime < _recenterUntil;
                Vector3 flatVel = new Vector3(ctx.PlayerVelocity.x, 0f, ctx.PlayerVelocity.z);
                bool idleLook = Time.unscaledTime - ctx.LastLookTime > 1.6f;
                if (manualRecenter)
                {
                    _yaw = Mathf.SmoothDampAngle(_yaw, playerYaw, ref _yawVelocity, 0.08f, 2000f, dt);
                    _pitch = Mathf.SmoothDampAngle(_pitch, 12f, ref _pitchVelocity, 0.1f, 600f, dt);
                }
                else if (ctx.AutoRecenter && idleLook && flatVel.magnitude > 2f)
                {
                    // Gentle recentering behind the direction of travel.
                    float travelYaw = Quaternion.LookRotation(flatVel).eulerAngles.y;
                    float delta = Mathf.DeltaAngle(_yaw, travelYaw);
                    if (Mathf.Abs(delta) < 150f) _yaw += delta * Mathf.Clamp01(dt * 1.1f);
                }
            }

            // Distance: wider for bosses / big attacks, tighter when requested.
            float desiredDistance = Distance * scale * Mathf.Max(0.2f, ctx.DistanceMultiplier);
            if (locked)
            {
                float targetDist = Vector3.Distance(ctx.LockTarget.position, playerPos);
                desiredDistance += Mathf.Clamp(targetDist * 0.12f, 0f, 2.2f) + (ctx.LockTargetIsBoss ? 1.3f : 0f);
            }

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            float shoulderTarget = locked ? ShoulderOffset * 0.45f : ShoulderOffset;
            _shoulder = Mathf.Lerp(_shoulder, shoulderTarget * scale, 1f - Mathf.Exp(-6f * dt));
            Vector3 pivot = _pivotSmoothed + rot * Vector3.right * _shoulder;

            float safe = CameraCollision.SafeDistance(pivot, rot * Vector3.back, desiredDistance, CollisionRadius, Layers.CameraObstacleMask);
            Obstructed = safe < desiredDistance - 0.05f;
            // Pull in instantly when blocked, ease back out when clear.
            _currentDistance = safe < _currentDistance ? safe : Mathf.Lerp(_currentDistance, safe, 1f - Mathf.Exp(-4f * dt));

            Vector3 position = pivot + rot * Vector3.back * _currentDistance;
            Quaternion look = rot;
            if (locked)
            {
                // Frame both fighters: aim between the player and the target.
                Vector3 mid = Vector3.Lerp(pivot, ctx.LockTarget.position, 0.35f);
                Vector3 dir = mid - position;
                if (dir.sqrMagnitude > 0.01f) look = Quaternion.Slerp(rot, Quaternion.LookRotation(dir), 0.65f);
            }

            float fov = ctx.BaseFov;
            if (ctx.PlayerSprinting) fov += 7f;
            return new CameraPose(position, look, fov);
        }
    }
}
