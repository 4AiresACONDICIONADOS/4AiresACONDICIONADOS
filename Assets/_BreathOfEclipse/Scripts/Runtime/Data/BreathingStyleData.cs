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

    /// <summary>
    /// One form (posture) of a breathing style. The technique itself (motion, movement, hits, VFX, audio, camera,
    /// costs, cooldown, voice lines) lives in <see cref="skill"/>; this adds what belongs to the style's form list.
    /// </summary>
    [Serializable]
    public sealed class BreathingForm
    {
        [Tooltip("1-based form number shown as I, II … XI.")] public int formNumber = 1;
        public SkillData skill;
        public bool unlocked = true;
        [Tooltip("Free text until progression exists, e.g. 'Defeat The Hollow Oni'.")] public string unlockRequirement = "";
        public Sprite icon;
        [Tooltip("Demons that learn breathing techniques may use this form.")] public bool aiUsable;
    }

    /// <summary>A breathing style: element, palette, any number of forms, ultimate and passive buffs.</summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Breathing Style", fileName = "Style_")]
    public sealed class BreathingStyleData : ScriptableObject
    {
        /// <summary>Keys 1-4 are quick slots; every form is reachable from the form wheel.</summary>
        public const int QuickSlotCount = 4;
        /// <summary>Slot index used for the ultimate (R).</summary>
        public const int UltimateSlot = 4;

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

        [Header("Voice (Spanish)")]
        [Tooltip("Spoken before a form, e.g. 'Respiración del Agua'.")] public string styleCall = "";

        [Header("Forms (any number, I … XI), quick slots 1-4 and ultimate (R)")]
        public List<BreathingForm> forms = new List<BreathingForm>();
        [Tooltip("Form index (0-based) for quick slots 1-4; -1 leaves the slot empty.")]
        public int[] quickSlots = { 0, 1, 2, 3 };
        public SkillData ultimate;

        [Header("Passives")]
        public List<StylePassive> passives = new List<StylePassive>();
        public float staminaCostMultiplier = 1f;
        public float cooldownMultiplier = 1f;

        // v0.1 assets stored three techniques + one advanced form. Read only when 'forms' is empty.
        [HideInInspector] public List<SkillData> techniques = new List<SkillData>();
        [HideInInspector] public SkillData advanced;
        [NonSerialized] private List<BreathingForm> _legacyForms;

        public IReadOnlyList<BreathingForm> Forms
        {
            get
            {
                if (forms != null && forms.Count > 0) return forms;
                if (_legacyForms == null)
                {
                    _legacyForms = new List<BreathingForm>();
                    if (techniques != null)
                        foreach (var t in techniques)
                            if (t != null) _legacyForms.Add(new BreathingForm { formNumber = _legacyForms.Count + 1, skill = t });
                    if (advanced != null) _legacyForms.Add(new BreathingForm { formNumber = _legacyForms.Count + 1, skill = advanced });
                }
                return _legacyForms;
            }
        }

        public int FormCount => Forms.Count;

        public BreathingForm GetForm(int index)
        {
            var list = Forms;
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        /// <summary>Form index assigned to a quick slot (0-3), or -1.</summary>
        public int QuickSlotForm(int slot)
        {
            if (slot < 0 || slot >= QuickSlotCount) return -1;
            if (quickSlots != null && slot < quickSlots.Length) return quickSlots[slot];
            return slot < FormCount ? slot : -1;
        }

        /// <summary>Slots 0-3 = quick slots (keys 1-4), <see cref="UltimateSlot"/> = ultimate.</summary>
        public SkillData GetSkill(int slot)
        {
            if (slot == UltimateSlot) return ultimate;
            var form = GetForm(QuickSlotForm(slot));
            return form != null ? form.skill : null;
        }

        /// <summary>Every form followed by the ultimate: iterate with <see cref="GetTechnique"/>.</summary>
        public int TechniqueCount => FormCount + (ultimate != null ? 1 : 0);

        public SkillData GetTechnique(int index)
        {
            if (index >= 0 && index < FormCount) return GetForm(index)?.skill;
            return index == FormCount ? ultimate : null;
        }

        /// <summary>The form that holds <paramref name="skill"/>, or null (ultimate / unknown).</summary>
        public BreathingForm FindForm(SkillData skill)
        {
            if (skill == null) return null;
            foreach (var f in Forms) if (f != null && f.skill == skill) return f;
            return null;
        }
    }
}
