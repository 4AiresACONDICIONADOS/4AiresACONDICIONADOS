using UnityEngine;

namespace BreathOfEclipse.Data
{
    /// <summary>Stats and look of a weapon. The procedural katana is built from these values unless a prefab is set.</summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Weapon", fileName = "Weapon_")]
    public sealed class WeaponData : ScriptableObject
    {
        public string weaponId = "eclipse_katana";
        public string displayName = "Eclipse Katana";

        [Header("Damage")]
        public float baseDamage = 12f;
        [Range(0f, 1f)] public float critChance = 0.12f;
        public float critMultiplier = 1.8f;

        [Header("Shape (meters)")]
        public float bladeLength = 1.0f;
        public float bladeWidth = 0.036f;
        public float handleLength = 0.26f;
        /// <summary>Radius of the swept spheres along the blade used for hit detection.</summary>
        public float hitRadius = 0.2f;

        [Header("Look")]
        public Color bladeColor = new Color(0.78f, 0.82f, 0.9f);
        [ColorUsage(true, true)] public Color edgeGlowColor = new Color(0.6f, 0.8f, 1.6f);
        public Color handleColor = new Color(0.12f, 0.08f, 0.14f);
        public Color guardColor = new Color(0.72f, 0.55f, 0.2f);

        [Header("Trail")]
        public Gradient trailGradient = new Gradient();
        public float trailLifetime = 0.16f;

        [Tooltip("Optional model replacing the procedural blade. Must contain children named 'BladeBase' and 'BladeTip'.")]
        public GameObject modelPrefab;

        private void Reset() => trailGradient = DefaultTrailGradient();

        public static Gradient DefaultTrailGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 1f, 1f), 0f), new GradientColorKey(new Color(0.65f, 0.8f, 1f), 0.35f), new GradientColorKey(new Color(0.3f, 0.45f, 0.9f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.55f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
