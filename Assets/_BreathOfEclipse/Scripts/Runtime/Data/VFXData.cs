using UnityEngine;

namespace BreathOfEclipse.Data
{
    /// <summary>
    /// Optional override for a VFX id. When a prefab is assigned it replaces the procedural recipe with the same id,
    /// so artists can swap in hand-made Particle Systems / VFX Graphs without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/VFX Override", fileName = "VFX_")]
    public sealed class VFXData : ScriptableObject
    {
        public string vfxId = "impact_slash";
        [Tooltip("Prefab to spawn instead of the procedural recipe. Leave empty to use the recipe.")]
        public GameObject prefab;
        public float lifetime = 2f;
        public int prewarm;
        public float scale = 1f;
        [Tooltip("Tint applied to the procedural recipe (multiplies element color).")]
        public Color tint = Color.white;
    }
}
