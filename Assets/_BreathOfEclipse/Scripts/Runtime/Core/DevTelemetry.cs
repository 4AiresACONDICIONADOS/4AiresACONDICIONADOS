using System;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Observation points for development tools (runtime playtest, diagnostics). Systems report what they
    /// did; nothing in gameplay listens to these, so they never change behaviour. Invoking an event without
    /// listeners costs a null check.
    /// </summary>
    public static class DevTelemetry
    {
        /// <summary>Hit stop requested (real seconds).</summary>
        public static event Action<float> HitStopRequested;
        /// <summary>Camera effect requested: kind is "shake", "impulse", "fov", "zoom" or "cinematic".</summary>
        public static event Action<string, float> CameraCue;
        /// <summary>A pooled effect was spawned (id, position).</summary>
        public static event Action<string, Vector3> VfxSpawned;
        /// <summary>An enemy ground telegraph was shown (position, radius, duration).</summary>
        public static event Action<Vector3, float, float> TelegraphShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HitStopRequested = null;
            CameraCue = null;
            VfxSpawned = null;
            TelegraphShown = null;
        }

        public static void ReportHitStop(float duration) => HitStopRequested?.Invoke(duration);
        public static void ReportCameraCue(string kind, float amount) => CameraCue?.Invoke(kind, amount);
        public static void ReportVfx(string id, Vector3 position) => VfxSpawned?.Invoke(id, position);
        public static void ReportTelegraph(Vector3 position, float radius, float duration) => TelegraphShown?.Invoke(position, radius, duration);
    }
}
