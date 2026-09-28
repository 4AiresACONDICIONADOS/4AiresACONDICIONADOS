using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Hit stop presets. Freezes gameplay (not UI) for a few frames so impacts read clearly.
    /// Values follow the combat design doc: light 0.025, heavy 0.045, skill 0.06, critical 0.08, ultimate 0.1.
    /// </summary>
    public static class HitStopManager
    {
        public const float Light = 0.025f;
        public const float Heavy = 0.045f;
        public const float Skill = 0.06f;
        public const float Critical = 0.08f;
        public const float Ultimate = 0.1f;

        /// <summary>Global multiplier (debug / accessibility).</summary>
        public static float Scale = 1f;

        private static float _lastApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Scale = 1f;
            _lastApplied = 0f;
        }

        public static float ForCategory(DamageCategory category)
        {
            switch (category)
            {
                case DamageCategory.Light:
                case DamageCategory.EnemyLight:
                case DamageCategory.Projectile:
                    return Light;
                case DamageCategory.Heavy:
                case DamageCategory.EnemyHeavy:
                case DamageCategory.Counter:
                    return Heavy;
                case DamageCategory.Skill:
                    return Skill;
                case DamageCategory.Finisher:
                    return Critical;
                case DamageCategory.Ultimate:
                    return Ultimate;
                default:
                    return Light;
            }
        }

        /// <summary>Applies hit stop. Uses the explicit duration when positive, else the category preset.</summary>
        public static void Apply(DamageCategory category, bool critical, float explicitDuration = -1f)
        {
            float d = explicitDuration > 0f ? explicitDuration : ForCategory(category);
            if (critical) d = Mathf.Max(d, Critical);
            d *= Scale;
            // Multi-target sweeps call this several times in one frame: only the longest counts.
            if (Time.unscaledTime - _lastApplied < 0.01f && d <= 0f) return;
            _lastApplied = Time.unscaledTime;
            if (TimeController.Instance != null) TimeController.Instance.HitStop(d);
        }
    }
}
