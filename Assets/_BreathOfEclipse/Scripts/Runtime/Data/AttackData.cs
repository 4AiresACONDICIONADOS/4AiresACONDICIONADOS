using System;
using System.Collections.Generic;
using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Data
{
    /// <summary>
    /// A time window (normalized 0..1 over the whole attack) during which the attack can be cancelled
    /// into the flagged actions. Also settable from Animation Events via <c>AnimationEventRelay</c>.
    /// </summary>
    [Serializable]
    public struct CancelWindow
    {
        public CancelFlags flags;
        [Range(0f, 1f)] public float start;
        [Range(0f, 1f)] public float end;

        public CancelWindow(CancelFlags flags, float start, float end)
        {
            this.flags = flags;
            this.start = start;
            this.end = end;
        }

        public bool Contains(float normalizedTime) => normalizedTime >= start && normalizedTime <= end;
    }

    /// <summary>One sword attack of a combo string (light, heavy, air, counter...).</summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Attack", fileName = "Attack_")]
    public sealed class AttackData : ScriptableObject
    {
        public string attackId = "L1";
        public string displayName = "Slash";
        public DamageCategory category = DamageCategory.Light;

        [Header("Timing (seconds)")]
        [Tooltip("Anticipation before the blade moves.")] public float windup = 0.1f;
        [Tooltip("Hit frames: the blade sweeps and can hit.")] public float active = 0.1f;
        [Tooltip("Follow-through and return to stance.")] public float recovery = 0.26f;

        [Header("Animation")]
        [Tooltip("Procedural motion id (MotionLibrary) or Animator state name.")]
        public string motionId = "L1";
        public bool faceTargetDuringWindup = true;

        [Header("Movement")]
        [Tooltip("Meters travelled forward during windup+active (stops at enemies).")]
        public float forwardDistance = 0.7f;
        [Tooltip("Upward velocity applied at the start of the active window (launchers, air attacks).")]
        public float verticalVelocity;
        [Tooltip("Keeps the attacker floating while attacking in the air.")]
        public bool airHang;
        [Tooltip("Plunge attacks: slam down to the ground during the active window.")]
        public bool plunge;

        [Header("Damage")]
        public float damageMultiplier = 1f;
        public float poiseDamage = 10f;
        public HitReaction reaction = HitReaction.Light;
        public float knockback = 1.2f;
        public float launchHeight;
        [Tooltip("Air hang applied to targets hit (keeps juggled enemies up).")] public float targetAirHang = 0.45f;
        public bool forceCritical;
        [Range(0f, 1f)] public float bonusCritChance;
        public bool canBreakObjects;
        public bool isFinisher;
        public bool flashFrame;
        [Tooltip("None = the equipped breathing style element is used for sparks only.")]
        public Element element = Element.None;

        [Header("Hit detection")]
        public float hitRadiusMultiplier = 1f;
        public float reachBonus;
        [Tooltip("Extra forward arc overlap (meters) so wide swings feel generous. 0 = blade sweep only.")]
        public float arcRadius = 1.4f;
        [Range(0f, 360f)] public float arcAngle = 150f;
        public int maxHitsPerTarget = 1;
        public float multiHitInterval = 0.1f;

        [Header("Feel")]
        public float hitStop = 0.025f;
        public float cameraShake = 0.12f;
        public float fovPunch;
        [Tooltip("Camera distance multiplier on hit (<1 = closer). 0 or 1 = none.")]
        public float cameraZoom = 1f;
        public float slowMoScale = 1f;
        public float slowMoDuration;
        public bool groundImpact;
        public bool speedLines;
        [Tooltip("Shows the breathing style element trail on this swing.")]
        public bool elementTrail;
        public float trailWidth = 1f;
        public string swingSfx = "slash";
        public string hitSfx = "hit";
        public string impactVfx = "impact_slash";

        [Header("Cancel windows")]
        public List<CancelWindow> cancelWindows = new List<CancelWindow>();

        public float TotalDuration => windup + active + recovery;
        public float ActiveStartNormalized => TotalDuration <= 0f ? 0f : windup / TotalDuration;
        public float ActiveEndNormalized => TotalDuration <= 0f ? 1f : (windup + active) / TotalDuration;

        public bool CanCancel(CancelFlags flag, float normalizedTime)
        {
            for (int i = 0; i < cancelWindows.Count; i++)
            {
                var w = cancelWindows[i];
                if ((w.flags & flag) != 0 && w.Contains(normalizedTime)) return true;
            }
            return false;
        }

        /// <summary>Default cancel layout used by content generation: combo continues from end of hit frames.</summary>
        public void SetStandardCancelWindows(float comboStart = -1f)
        {
            float activeEnd = ActiveEndNormalized;
            float start = comboStart < 0f ? activeEnd - 0.05f : comboStart;
            cancelWindows = new List<CancelWindow>
            {
                new CancelWindow(CancelFlags.Attack | CancelFlags.Skill | CancelFlags.Ultimate, Mathf.Clamp01(start), 1f),
                new CancelWindow(CancelFlags.Dodge | CancelFlags.Block, Mathf.Clamp01(ActiveStartNormalized * 0.6f), 1f),
                new CancelWindow(CancelFlags.Jump | CancelFlags.Move, Mathf.Clamp01(activeEnd + 0.12f), 1f)
            };
        }
    }
}
