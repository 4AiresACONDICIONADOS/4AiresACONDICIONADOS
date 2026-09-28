using UnityEngine;

namespace BreathOfEclipse.CameraSystem
{
    public enum CameraMode
    {
        ThirdPerson = 0,
        FirstPerson = 1,
        SecondPerson = 2
    }

    /// <summary>Where the camera wants to be this frame.</summary>
    public struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Fov;

        public CameraPose(Vector3 position, Quaternion rotation, float fov)
        {
            Position = position;
            Rotation = rotation;
            Fov = fov;
        }

        public static CameraPose Lerp(CameraPose a, CameraPose b, float t)
        {
            return new CameraPose(Vector3.Lerp(a.Position, b.Position, t), Quaternion.Slerp(a.Rotation, b.Rotation, t), Mathf.Lerp(a.Fov, b.Fov, t));
        }
    }

    /// <summary>Data every camera mode receives each frame.</summary>
    public struct CameraContext
    {
        public Transform Player;
        public float PlayerScale;
        public Vector3 PlayerVelocity;
        public bool PlayerSprinting;
        public bool PlayerAttacking;
        public Transform LockTarget;
        public bool LockTargetIsBoss;
        public Vector2 LookDelta;
        public float LastLookTime;
        public float BaseFov;
        public float DistanceMultiplier;
        public bool AutoRecenter;
        public float DeltaTime;
    }

    /// <summary>One camera behaviour (third person, first person, second person, cinematic).</summary>
    public interface ICameraMode
    {
        void Enter(CameraPose current, CameraContext ctx);
        CameraPose Evaluate(CameraContext ctx);
        /// <summary>Yaw the player should use for camera-relative movement.</summary>
        float Yaw { get; }
    }

    /// <summary>Helpers for camera collision.</summary>
    public static class CameraCollision
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>Returns the farthest safe distance from <paramref name="pivot"/> along <paramref name="direction"/>.</summary>
        public static float SafeDistance(Vector3 pivot, Vector3 direction, float desired, float radius, int mask)
        {
            if (desired <= 0.01f) return desired;
            int n = Physics.SphereCastNonAlloc(pivot, radius, direction.normalized, Hits, desired, mask, QueryTriggerInteraction.Ignore);
            float best = desired;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].distance <= 0f) continue; // started inside: ignore (pivot inside geometry)
                if (Hits[i].distance < best) best = Hits[i].distance;
            }
            return Mathf.Max(0.25f, best - 0.05f);
        }
    }
}
