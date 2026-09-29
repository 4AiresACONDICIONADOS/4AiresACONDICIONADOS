using UnityEngine;

namespace BreathOfEclipse.CameraSystem
{
    /// <summary>
    /// First person: eyes of the character. Uses a stabilized anchor (not the animated head bone directly) so
    /// fast sword animations never shake the view; arms, sword and technique VFX stay visible.
    /// </summary>
    public sealed class FirstPersonCameraMode : ICameraMode
    {
        public float EyeHeight = 1.64f;
        public float ForwardOffset = 0.14f;
        public float FovBonus = 8f;
        public Transform HeadBone;

        private float _yaw;
        private float _pitch;
        private Vector3 _smoothed;
        private bool _init;
        private float _roll;
        private float _lastYaw;

        public float Yaw => _yaw;

        public void Enter(CameraPose current, CameraContext ctx)
        {
            Vector3 e = current.Rotation.eulerAngles;
            _yaw = ctx.Player != null ? ctx.Player.eulerAngles.y : e.y;
            _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -60f, 60f);
            _init = false;
            _lastYaw = _yaw;
            _roll = 0f;
        }

        public CameraPose Evaluate(CameraContext ctx)
        {
            float dt = ctx.DeltaTime;
            float scale = Mathf.Max(0.5f, ctx.PlayerScale);
            if (ctx.LockTarget != null)
            {
                Vector3 to = ctx.LockTarget.position - (ctx.Player.position + Vector3.up * EyeHeight * scale);
                if (to.sqrMagnitude > 0.25f)
                {
                    var look = Quaternion.LookRotation(to).eulerAngles;
                    _yaw = Mathf.LerpAngle(_yaw, look.y, 1f - Mathf.Exp(-10f * dt));
                    _pitch = Mathf.LerpAngle(_pitch, Mathf.Clamp(Mathf.DeltaAngle(0f, look.x), -60f, 60f), 1f - Mathf.Exp(-6f * dt));
                }
            }
            else
            {
                _yaw += ctx.LookDelta.x;
                _pitch = Mathf.Clamp(_pitch - ctx.LookDelta.y, -75f, 80f);
            }

            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 anchor = ctx.Player.position + Vector3.up * (EyeHeight * scale) + yawRot * Vector3.forward * (ForwardOffset * scale);
            if (HeadBone != null)
            {
                // A small amount of real head motion keeps it alive without motion sickness.
                Vector3 head = HeadBone.position + Vector3.up * 0.12f * scale;
                anchor = Vector3.Lerp(anchor, new Vector3(head.x, Mathf.Min(head.y, anchor.y + 0.05f), head.z), 0.2f);
            }
            if (!_init)
            {
                _smoothed = anchor;
                _init = true;
            }
            _smoothed.x = Mathf.Lerp(_smoothed.x, anchor.x, 1f - Mathf.Exp(-30f * dt));
            _smoothed.z = Mathf.Lerp(_smoothed.z, anchor.z, 1f - Mathf.Exp(-30f * dt));
            _smoothed.y = Mathf.Lerp(_smoothed.y, anchor.y, 1f - Mathf.Exp(-12f * dt));

            // Subtle bank into fast turns (no oscillating wobble: it causes motion sickness); capped at 2 degrees.
            float turnRate = dt > 0f ? Mathf.DeltaAngle(_lastYaw, _yaw) / dt : 0f;
            _lastYaw = _yaw;
            float rollTarget = Mathf.Clamp(-turnRate * 0.01f, -2f, 2f) * (ctx.PlayerAttacking ? 1f : 0.5f);
            _roll = Mathf.Lerp(_roll, rollTarget, 1f - Mathf.Exp(-6f * dt));

            return new CameraPose(_smoothed, Quaternion.Euler(_pitch, _yaw, _roll), ctx.BaseFov + FovBonus);
        }
    }
}
