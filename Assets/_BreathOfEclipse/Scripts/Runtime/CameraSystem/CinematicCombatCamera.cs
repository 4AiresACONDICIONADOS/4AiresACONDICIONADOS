using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.CameraSystem
{
    /// <summary>
    /// In-engine cinematic shots used by ultimates and big techniques. No pre-rendered footage: every shot is
    /// computed from the live positions of the player and the target, then blended back to gameplay.
    /// </summary>
    public sealed class CinematicCombatCamera : ICameraMode
    {
        private CinematicShot _shot;
        private float _distance = 5f;
        private float _height = 1.5f;
        private float _shotStart;
        private Transform _focusTarget;
        private CameraPose _from;
        private float _blend = 0.3f;
        private Vector3 _anchorForward;
        private Vector3 _anchorPosition;

        public float Yaw { get; private set; }
        public bool HasShot => _shot != CinematicShot.None;

        public void Enter(CameraPose current, CameraContext ctx)
        {
            _from = current;
        }

        /// <summary>Starts a shot. The player's current facing is captured as the shot's reference frame.</summary>
        public void SetShot(CinematicShot shot, float distance, float height, Transform player, Transform focus, CameraPose current, float blend = 0.28f)
        {
            _shot = shot;
            _distance = distance <= 0f ? 5f : distance;
            _height = height;
            _focusTarget = focus;
            _from = current;
            _blend = blend;
            _shotStart = Time.unscaledTime;
            _anchorForward = player != null ? Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized : Vector3.forward;
            if (_anchorForward.sqrMagnitude < 0.01f) _anchorForward = Vector3.forward;
            _anchorPosition = player != null ? player.position : Vector3.zero;
        }

        public void Clear() => _shot = CinematicShot.None;

        public CameraPose Evaluate(CameraContext ctx)
        {
            float t = Time.unscaledTime - _shotStart;
            Vector3 player = ctx.Player.position;
            float s = Mathf.Max(0.5f, ctx.PlayerScale);
            Vector3 chest = player + Vector3.up * 1.3f * s;
            Vector3 fwd = _anchorForward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 target = _focusTarget != null ? _focusTarget.position : chest + fwd * 4f;
            Vector3 pos;
            Vector3 lookAt;
            float fov = ctx.BaseFov;

            switch (_shot)
            {
                case CinematicShot.LowAngleHero:
                    pos = player + fwd * (_distance * 0.65f) + right * 1.1f + Vector3.up * 0.35f;
                    lookAt = chest + Vector3.up * 0.35f;
                    fov -= 6f;
                    break;
                case CinematicShot.OrbitSlow:
                {
                    float angle = t * 32f;
                    Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * (-fwd);
                    pos = chest + dir * _distance + Vector3.up * _height;
                    lookAt = chest;
                    break;
                }
                case CinematicShot.WideArena:
                    pos = Vector3.Lerp(player, target, 0.5f) - fwd * (_distance * 1.7f) + right * 2f + Vector3.up * (_height * 2.2f);
                    lookAt = Vector3.Lerp(chest, target, 0.5f);
                    fov += 8f;
                    break;
                case CinematicShot.CloseUpFace:
                    pos = chest + Vector3.up * 0.4f * s + fwd * 1.0f + right * 0.3f;
                    lookAt = chest + Vector3.up * 0.4f * s;
                    fov -= 14f;
                    break;
                case CinematicShot.OverShoulderTarget:
                    pos = chest - fwd * 2.2f + right * 0.9f + Vector3.up * 0.4f;
                    lookAt = target;
                    break;
                case CinematicShot.TopDown:
                    pos = Vector3.Lerp(player, target, 0.5f) + Vector3.up * (_distance * 2f) - fwd * 1.5f;
                    lookAt = Vector3.Lerp(player, target, 0.5f);
                    break;
                case CinematicShot.FollowBehind:
                    pos = chest - fwd * _distance + Vector3.up * _height;
                    lookAt = chest + fwd * 3f;
                    break;
                case CinematicShot.SideProfile:
                    pos = Vector3.Lerp(_anchorPosition, player, 0.5f) + right * _distance + Vector3.up * (_height * 0.6f);
                    lookAt = chest;
                    fov -= 4f;
                    break;
                case CinematicShot.SkyLookUp:
                    pos = player - fwd * 2.5f + right * 1.2f + Vector3.up * 0.3f;
                    lookAt = target + Vector3.up * (_height * 3f);
                    fov += 10f;
                    break;
                default:
                    pos = chest - fwd * _distance + Vector3.up * _height;
                    lookAt = chest;
                    break;
            }

            // Keep shots out of walls.
            Vector3 fromFocus = pos - chest;
            float safe = CameraCollision.SafeDistance(chest, fromFocus, fromFocus.magnitude, 0.2f, Layers.CameraObstacleMask);
            pos = chest + fromFocus.normalized * safe;

            Vector3 lookDir = lookAt - pos;
            Quaternion rot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
            var pose = new CameraPose(pos, rot, fov);
            Yaw = rot.eulerAngles.y;
            float b = _blend <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, t / _blend);
            return b >= 1f ? pose : CameraPose.Lerp(_from, pose, b);
        }
    }
}
