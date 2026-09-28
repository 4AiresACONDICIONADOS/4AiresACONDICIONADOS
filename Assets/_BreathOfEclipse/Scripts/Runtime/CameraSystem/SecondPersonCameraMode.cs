using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.CameraSystem
{
    /// <summary>
    /// Experimental second-person camera: the viewpoint of the locked-on enemy, looking at the protagonist.
    /// Corrects for walls (behind the enemy), the ground and obstacles between the enemy and the player.
    /// The player still controls the protagonist; movement stays screen-relative.
    /// </summary>
    public sealed class SecondPersonCameraMode : ICameraMode
    {
        public float BackOffset = 1.25f;
        public float SideOffset = 0.55f;
        public float UpOffset = 0.45f;
        public float MaxOrbit = 40f;

        private float _orbit;
        private Vector3 _smoothed;
        private Quaternion _smoothedRot = Quaternion.identity;
        private bool _init;
        private float _yaw;

        public float Yaw => _yaw;

        public void Enter(CameraPose current, CameraContext ctx)
        {
            _init = false;
            _orbit = 0f;
            _yaw = current.Rotation.eulerAngles.y;
        }

        public CameraPose Evaluate(CameraContext ctx)
        {
            float dt = ctx.DeltaTime;
            Transform target = ctx.LockTarget;
            float scale = Mathf.Max(0.5f, ctx.PlayerScale);
            Vector3 playerFocus = ctx.Player.position + Vector3.up * (1.3f * scale);
            if (target == null)
            {
                // Caller switches back to third person; return something sane for this frame.
                return new CameraPose(_smoothed, _smoothedRot, ctx.BaseFov);
            }

            _orbit = Mathf.Clamp(_orbit + ctx.LookDelta.x * 0.5f, -MaxOrbit, MaxOrbit);

            // "Eyes" of the rival: slightly above its lock-on point.
            Vector3 eyes = target.position + Vector3.up * UpOffset;
            Vector3 toPlayer = playerFocus - eyes;
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(0f, _orbit, 0f);
            Vector3 desired = eyes + facing * new Vector3(SideOffset, 0.25f, -BackOffset);

            // Walls behind the rival.
            Vector3 offset = desired - eyes;
            float safe = CameraCollision.SafeDistance(eyes, offset, offset.magnitude, 0.2f, Layers.CameraObstacleMask);
            desired = eyes + offset.normalized * safe;

            // Never under the ground.
            if (Physics.Raycast(desired + Vector3.up * 2f, Vector3.down, out var ground, 6f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                desired.y = Mathf.Max(desired.y, ground.point.y + 0.4f);

            // Obstacles between the camera and the protagonist: slide forward along the line of sight.
            Vector3 los = playerFocus - desired;
            float losDist = los.magnitude;
            if (losDist > 0.1f && Physics.SphereCast(desired, 0.15f, los / losDist, out var block, losDist - 0.3f, Layers.CameraObstacleMask, QueryTriggerInteraction.Ignore))
                desired = block.point + los / losDist * 0.35f;

            if (!_init)
            {
                _smoothed = desired;
                _smoothedRot = Quaternion.LookRotation(playerFocus - desired);
                _init = true;
            }
            _smoothed = Vector3.Lerp(_smoothed, desired, 1f - Mathf.Exp(-10f * dt));
            Vector3 lookDir = playerFocus - _smoothed;
            if (lookDir.sqrMagnitude > 0.01f)
                _smoothedRot = Quaternion.Slerp(_smoothedRot, Quaternion.LookRotation(lookDir), 1f - Mathf.Exp(-14f * dt));
            _yaw = _smoothedRot.eulerAngles.y;
            return new CameraPose(_smoothed, _smoothedRot, ctx.BaseFov - 4f);
        }
    }
}
