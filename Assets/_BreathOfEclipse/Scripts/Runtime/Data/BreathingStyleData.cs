using System;
using System.Collections.Generic;
using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Data
{
    /// <summary>Visual family of the element trail that follows the sword.</summary>
    public enum ElementTrailStyle
    {
        Liquid = 0,
        Flame = 1,
        Lightning = 2,
        Wind = 3,
        Lunar = 4
    }

    [Serializable]
    public struct StylePassive
    {
        public StatType stat;
        public float magnitude;
    }

    /// <summary>A breathing style: element, palette, techniques, ultimate and passive buffs.</summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Breathing Style", fileName = "Style_")]
    public sealed class BreathingStyleData : ScriptableObject
    {
        public string styleId = "tidal";
        public string displayName = "TIDAL BREATH";
        [TextArea] public string description;
        public Element element = Element.Water;

        [Header("Palette")]
        public Color baseColor = new Color(0.2f, 0.6f, 1f);
        public Color secondaryColor = new Color(0.9f, 0.97f, 1f);
        [ColorUsage(true, true)] public Color glowColor = new Color(0.4f, 1.2f, 3f);
        public Gradient elementTrailGradient = new Gradient();
        public ElementTrailStyle trailStyle = ElementTrailStyle.Liquid;

        [Header("Audio / VFX ids")]
        public string equipSfx = "water";
        public string swordAuraVfx = "aura_water";
        public string lightHitVfx = "spark_water";

        [Header("Techniques (keys 1-3), advanced (4) and ultimate (R)")]
        public List<SkillData> techniques = new List<SkillData>();
        public SkillData advanced;
        public SkillData ultimate;

        [Header("Passives")]
        public List<StylePassive> passives = new List<StylePassive>();
        public float staminaCostMultiplier = 1f;
        public float cooldownMultiplier = 1f;

        /// <summary>Index 0-2 = techniques, 3 = advanced, 4 = ultimate.</summary>
        public SkillData GetSkill(int slot)
        {
            if (slot >= 0 && slot < 3) return slot < techniques.Count ? techniques[slot] : null;
            if (slot == 3) return advanced;
            if (slot == 4) return ultimate;
            return null;
        }
    }
}
