using System;
using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Breathing
{
    /// <summary>
    /// Equips breathing styles (palette, passives, trails, sword aura) and executes their techniques:
    /// any number of forms (keys 1-4 = quick slots, the form wheel reaches all), R = ultimate.
    /// Handles stamina / breath costs and cooldowns.
    /// </summary>
    public sealed class BreathingStyleSystem : MonoBehaviour
    {
        public const int UltimateSlot = BreathingStyleData.UltimateSlot;
        private const string PassiveBuffPrefix = "style_passive_";

        public IReadOnlyList<BreathingStyleData> Styles => _styles;
        public BreathingStyleData Current { get; private set; }
        public int CurrentIndex { get; private set; }
        public Element CurrentElement => Current != null ? Current.element : Element.None;
        public ElementPalette CurrentPalette => ElementPalette.Get(CurrentElement);
        public CooldownTracker Cooldowns { get; } = new CooldownTracker();
        public SkillExecutor Executor { get; private set; }
        public bool IsExecuting => Executor != null && Executor.Running;
        public SkillData ExecutingSkill => IsExecuting ? Executor.Skill : null;

        public event Action<BreathingStyleData> StyleChanged;
        /// <summary>(skill, callout text) — HUD shows the anime technique banner.</summary>
        public event Action<SkillData, string> TechniqueStarted;
        /// <summary>(style, form or null for the ultimate, skill) — voice, subtitles and title card.</summary>
        public event Action<BreathingStyleData, BreathingForm, SkillData> TechniqueAnnounced;

        private readonly List<BreathingStyleData> _styles = new List<BreathingStyleData>();
        private PlayerController _pc;
        private VFXInstance _aura;
        private int _passiveCount;

        public void Initialize(PlayerController pc, IEnumerable<BreathingStyleData> styles, string equippedId)
        {
            _pc = pc;
            _styles.Clear();
            foreach (var s in styles) if (s != null) _styles.Add(s);
            Executor = new SkillExecutor(pc);
            int index = Mathf.Max(0, _styles.FindIndex(s => s.styleId == equippedId));
            Equip(index, true);
        }

        public void Cycle(int direction)
        {
            if (_styles.Count <= 1 || IsExecuting) return;
            int next = (CurrentIndex + direction + _styles.Count) % _styles.Count;
            Equip(next, false);
        }

        public void Equip(int index, bool silent)
        {
            if (_styles.Count == 0) return;
            index = Mathf.Clamp(index, 0, _styles.Count - 1);
            // Remove previous passives.
            for (int i = 0; i < _passiveCount; i++) _pc.Stats.Buffs.Remove(PassiveBuffPrefix + i);
            CurrentIndex = index;
            Current = _styles[index];
            _passiveCount = Current.passives.Count;
            for (int i = 0; i < Current.passives.Count; i++)
                _pc.Stats.Buffs.Apply(PassiveBuffPrefix + i, Current.passives[i].stat, Current.passives[i].magnitude, -1f);
            Cooldowns.DurationMultiplier = Current.cooldownMultiplier;

            _pc.Rig.SetAccentColor(Current.baseColor);
            _pc.Rig.SetWeaponGlow(Current.glowColor * 0.35f);
            _pc.ApplyStyleTrails(Current);

            if (_aura != null) _aura.Release();
            _aura = null;
            string auraId = CombatFeedback.AuraId(Current.element);
            if (!string.IsNullOrEmpty(auraId) && _pc.Animator.WeaponBase != null)
                _aura = VFXLibrary.Spawn(auraId, _pc.Animator.WeaponBase.position, _pc.Animator.WeaponBase.rotation, 1f, Current.element, _pc.Animator.WeaponBase);

            SaveSystem.Settings.lastEquippedStyle = Current.styleId;
            if (!silent)
            {
                SaveSystem.SaveSettings();
                Sfx.Play(Current.equipSfx, _pc.transform.position + Vector3.up, 0.7f);
                VFXLibrary.Spawn(CombatFeedback.SparkId(Current.element), _pc.Animator.WeaponTip.position, Quaternion.identity, 1.5f, Current.element);
                GameEvents.Notify(Current.displayName);
            }
            StyleChanged?.Invoke(Current);
            GameEvents.RaiseStyleChanged(Current.styleId, Current.displayName);
        }

        public void EquipById(string styleId)
        {
            int i = _styles.FindIndex(s => s.styleId == styleId);
            if (i >= 0) Equip(i, false);
        }

        public SkillData GetSkill(int slot) => Current != null ? Current.GetSkill(slot) : null;

        public float CooldownNormalized(int slot)
        {
            var s = GetSkill(slot);
            return s == null ? 0f : Cooldowns.NormalizedRemaining(s.skillId, Time.time);
        }

        public float CooldownRemaining(int slot)
        {
            var s = GetSkill(slot);
            return s == null ? 0f : Cooldowns.Remaining(s.skillId, Time.time);
        }

        public int FormCount => Current != null ? Current.FormCount : 0;
        public BreathingForm GetForm(int index) => Current != null ? Current.GetForm(index) : null;

        /// <summary>Checks whether a slot can be used now. <paramref name="reason"/> explains failures for the HUD.</summary>
        public bool CanUse(int slot, out string reason) => CanUseSkill(GetSkill(slot), out reason);

        public bool CanUseForm(int formIndex, out string reason)
        {
            var form = GetForm(formIndex);
            if (form != null && !form.unlocked)
            {
                reason = string.IsNullOrEmpty(form.unlockRequirement) ? "Form locked" : "Locked: " + form.unlockRequirement;
                return false;
            }
            return CanUseSkill(form != null ? form.skill : null, out reason);
        }

        public bool TryUse(int slot, Transform target) => TryUseSkill(GetSkill(slot), target, out _);

        public bool TryUseForm(int formIndex, Transform target)
        {
            if (!CanUseForm(formIndex, out var reason))
            {
                if (!string.IsNullOrEmpty(reason)) GameEvents.Notify(reason);
                return false;
            }
            return TryUseSkill(GetForm(formIndex).skill, target, out _);
        }

        private bool CanUseSkill(SkillData skill, out string reason)
        {
            reason = null;
            if (skill == null)
            {
                reason = "No technique";
                return false;
            }
            if (!Cooldowns.IsReady(skill.skillId, Time.time))
            {
                reason = $"{skill.displayName}: {Cooldowns.Remaining(skill.skillId, Time.time):0.0}s";
                return false;
            }
            if (!skill.usableInAir && !_pc.Motor.Grounded)
            {
                reason = "Must be grounded";
                return false;
            }
            float stamina = skill.staminaCost * Current.staminaCostMultiplier;
            if (!_pc.Stats.Stamina.CanSpend(stamina))
            {
                reason = "Not enough stamina";
                return false;
            }
            if (!_pc.Stats.Breath.CanSpend(skill.breathCost))
            {
                reason = skill.tier == SkillTier.Ultimate ? "BREATH gauge not full" : "Not enough BREATH";
                return false;
            }
            return true;
        }

        private bool TryUseSkill(SkillData skill, Transform target, out string reason)
        {
            if (!CanUseSkill(skill, out reason))
            {
                if (!string.IsNullOrEmpty(reason)) GameEvents.Notify(reason);
                return false;
            }
            _pc.Stats.Stamina.TrySpend(skill.staminaCost * Current.staminaCostMultiplier);
            _pc.Stats.Breath.TrySpend(skill.breathCost);
            Cooldowns.Start(skill.skillId, skill.cooldown, Time.time);

            string callout = string.IsNullOrEmpty(skill.callout) ? $"{Current.displayName} — {skill.displayName.ToUpperInvariant()}" : skill.callout;
            TechniqueStarted?.Invoke(skill, callout);
            TechniqueAnnounced?.Invoke(Current, Current.FindForm(skill), skill);
            var element = skill.element != Element.None ? skill.element : Current.element;
            if (skill.tier == SkillTier.Ultimate) GameEvents.RaiseUltimateActivated(skill.skillId, callout, element);
            else GameEvents.RaiseSkillUsed(skill.skillId, callout, element);

            _pc.Rig.SetWeaponGlow(Current.glowColor * 1.2f);
            Executor.Start(skill, Current, target);
            return true;
        }

        public void Tick(float dt)
        {
            if (!IsExecuting) return;
            Executor.Tick(dt);
            if (!Executor.Running && Current != null) _pc.Rig.SetWeaponGlow(Current.glowColor * 0.35f);
        }

        public void Cancel()
        {
            if (!IsExecuting) return;
            Executor.Cancel();
            if (Current != null) _pc.Rig.SetWeaponGlow(Current.glowColor * 0.35f);
        }

        private void OnDestroy()
        {
            if (_aura != null) _aura.Release();
        }
    }
}
