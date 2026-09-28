using System;
using System.Collections.Generic;
using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Data
{
    public enum SkillTier
    {
        Normal = 0,
        Advanced = 1,
        Ultimate = 2
    }

    public enum SkillMoveMode
    {
        None = 0,
        /// <summary>Moves forward along facing.</summary>
        DashForward = 1,
        /// <summary>Dashes to the target, stopping just in front of it.</summary>
        DashToTarget = 2,
        /// <summary>Flash-step through the target and reappear behind it.</summary>
        TeleportBehindTarget = 3,
        /// <summary>Jumps forward and up.</summary>
        Leap = 4,
        /// <summary>Rises vertically (launcher follow-up).</summary>
        Rise = 5,
        /// <summary>Stays in place, gravity suspended.</summary>
        Hover = 6,
        /// <summary>Drops quickly to the ground.</summary>
        Plunge = 7,
        /// <summary>Hops backwards.</summary>
        Retreat = 8,
        /// <summary>Weaving S-shaped dash forward.</summary>
        Weave = 9
    }

    public enum HitShape
    {
        None = 0,
        SphereAroundSelf = 1,
        SphereInFront = 2,
        CapsuleForward = 3,
        Cone = 4,
        AtTarget = 5,
        /// <summary>Everything along the path travelled during this phase.</summary>
        AlongPath = 6,
        /// <summary>Arcs of lightning jumping between nearby enemies.</summary>
        ChainLightning = 7,
        /// <summary>Several strikes at random enemies/points around the target.</summary>
        ScatterAroundTarget = 8
    }

    public enum VFXAnchor
    {
        Self = 0,
        Sword = 1,
        SwordTip = 2,
        Ground = 3,
        Target = 4,
        TargetGround = 5,
        InFront = 6,
        /// <summary>Midpoint of the path travelled in this phase (flash-step lines).</summary>
        Path = 7,
        SkyAboveTarget = 8
    }

    public enum TrailMode
    {
        Keep = 0,
        Off = 1,
        Sword = 2,
        SwordAndElement = 3
    }

    public enum CinematicShot
    {
        None = 0,
        LowAngleHero = 1,
        OrbitSlow = 2,
        WideArena = 3,
        CloseUpFace = 4,
        OverShoulderTarget = 5,
        TopDown = 6,
        FollowBehind = 7,
        SideProfile = 8,
        SkyLookUp = 9
    }

    [Serializable]
    public sealed class VFXCue
    {
        public string vfxId;
        public VFXAnchor anchor;
        public float delay;
        [Tooltip("Offset in the character's facing space (x right, y up, z forward).")]
        public Vector3 offset;
        public Vector3 rotation;
        public float scale = 1f;
        [Tooltip("Parent the effect to the anchor so it follows it.")]
        public bool follow;
        [Tooltip("0 = recipe default lifetime.")]
        public float lifetime;

        public VFXCue() { }

        public VFXCue(string id, VFXAnchor anchor, float delay = 0f, float scale = 1f, bool follow = false)
        {
            vfxId = id;
            this.anchor = anchor;
            this.delay = delay;
            this.scale = scale;
            this.follow = follow;
        }
    }

    [Serializable]
    public sealed class SfxCue
    {
        public string sfxId;
        public float delay;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0.3f, 2f)] public float pitch = 1f;

        public SfxCue() { }

        public SfxCue(string id, float delay = 0f, float volume = 1f, float pitch = 1f)
        {
            sfxId = id;
            this.delay = delay;
            this.volume = volume;
            this.pitch = pitch;
        }
    }

    [Serializable]
    public sealed class HitSpec
    {
        public HitShape shape = HitShape.SphereInFront;
        [Tooltip("Seconds after the phase starts.")] public float delay;
        public float radius = 2f;
        [Tooltip("Forward distance (SphereInFront offset, capsule length, cone range).")] public float range = 2.5f;
        [Range(0f, 360f)] public float angle = 90f;
        public float damageMultiplier = 1.5f;
        public int hitCount = 1;
        public float hitInterval = 0.08f;
        public int maxTargets;
        public HitReaction reaction = HitReaction.Heavy;
        public float knockback = 3f;
        public float launchHeight;
        public float airHang;
        public float poiseDamage = 25f;
        public bool forceCritical;
        public float statusDuration;
        [Tooltip("Pulls targets toward the attacker's front (vortex techniques).")] public float pullStrength;
        public bool canBreakObjects = true;
        public bool isFinisher;
        public bool flashFrame;
        public float hitStop = 0.06f;
        public float cameraShake = 0.3f;
        public string impactVfx;
        public string hitSfx = "hit_heavy";
        [Tooltip("Spawns an independent hit zone at phase start: the hits keep happening (after 'delay') even if the technique ends or is cancelled. Lingering crescents, tornados.")]
        public bool detached;
    }

    [Serializable]
    public sealed class ProjectileSpec
    {
        public bool enabled;
        public string vfxId;
        public float delay;
        public float speed = 20f;
        public float lifetime = 1.2f;
        public float radius = 0.9f;
        public int count = 1;
        [Range(0f, 180f)] public float spreadAngle;
        public bool pierce = true;
        public bool homing;
        public float heightOffset = 1.1f;
        public float damageMultiplier = 1.2f;
        public HitReaction reaction = HitReaction.Heavy;
        public float knockback = 2f;
        public float launchHeight;
        public float poiseDamage = 15f;
        public string impactVfx;
        public string hitSfx = "hit";
    }

    [Serializable]
    public sealed class CameraCue
    {
        public float shake;
        public float fovPunch;
        [Tooltip("Camera distance multiplier during the phase (0 or 1 = unchanged, <1 closer, >1 wider).")]
        public float zoom = 1f;
        public float slowMoScale = 1f;
        public float slowMoDuration;
        [Tooltip("Saturation offset (-100..0) held during the phase.")]
        public float saturation;
        public float chromatic;
        public float lensDistortion;
        public float bloomBoost;
        public bool speedLines;
        public bool radialBlur;
        public bool flashFrame;
        public CinematicShot shot;
        public float shotDistance = 5f;
        public float shotHeight = 1.5f;
    }

    [Serializable]
    public sealed class BuffSpec
    {
        public bool enabled;
        public StatType stat;
        public float magnitude = 0.25f;
        public float duration = 5f;
    }

    /// <summary>One step of a technique timeline: anticipation, dash, strike, impact, recovery...</summary>
    [Serializable]
    public sealed class SkillPhase
    {
        public string name = "Phase";
        public float duration = 0.2f;

        [Header("Animation")]
        public string motionId;
        [Tooltip("Blend time into this phase's pose.")] public float blendIn = 0.08f;
        [Tooltip("Length of the motion clip. 0 = this phase's duration. Longer values let one clip span the next phases.")]
        public float motionDuration;

        [Header("Movement")]
        public SkillMoveMode movement;
        public float moveDistance;
        public float moveHeight;
        public bool invulnerable;
        public bool suspendGravity;
        public bool hideCharacter;
        public bool afterimages;
        public TrailMode trail;

        [Header("Presentation")]
        public List<VFXCue> vfx = new List<VFXCue>();
        public List<SfxCue> sfx = new List<SfxCue>();
        public CameraCue camera = new CameraCue();

        [Header("Gameplay")]
        public List<HitSpec> hits = new List<HitSpec>();
        public ProjectileSpec projectile = new ProjectileSpec();
        public BuffSpec buff = new BuffSpec();
        [Tooltip("Id of a registered ISkillBehaviour for logic the timeline cannot express.")]
        public string customBehaviour;
        [Tooltip("The player may cancel into attacks / dodge during this phase (recovery phases).")]
        public bool allowCancel;
    }

    /// <summary>
    /// A breathing technique. The whole technique is data: a list of phases with motion, movement, hits,
    /// VFX, audio and camera cues. Adding a technique = new SkillData asset (+ optional motion / VFX recipe).
    /// </summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Skill (Technique)", fileName = "Skill_")]
    public sealed class SkillData : ScriptableObject
    {
        public string skillId = "skill";
        [Tooltip("Short name, e.g. Rising Serpent")] public string displayName = "Technique";
        [Tooltip("e.g. First Form")] public string formName = "First Form";
        [Tooltip("Big anime callout, e.g. TIDAL BREATH — RISING SERPENT")] public string callout = "";
        [TextArea] public string description = "";
        public SkillTier tier = SkillTier.Normal;

        [Header("Voice (Spanish) — spoken by BreathingVoiceSystem")]
        [Tooltip("e.g. 'Séptima Postura' / 'Forma Final'. Empty = built from the form number.")] public string voiceFormCall = "";
        [Tooltip("e.g. 'Serpiente Ascendente'. Empty = the name is not spoken.")] public string voiceTechniqueCall = "";
        [Tooltip("None = inherit the breathing style element.")] public Element element = Element.None;

        [Header("Cost")]
        public float staminaCost = 12f;
        public float breathCost;
        public float cooldown = 4f;

        [Header("Rules")]
        public bool usableInAir = true;
        [Tooltip("Blocks player input for the duration (ultimates).")] public bool lockInput;
        [Tooltip("Uses the CinematicCombatCamera (can be skipped from settings).")] public bool cinematic;
        public float autoTargetRange = 14f;
        [Tooltip("The technique ends with the player airborne next to the launched enemy (air combo follow-up).")]
        public bool endsAirborne;
        public float damageScale = 1f;

        [Header("Timeline")]
        public List<SkillPhase> phases = new List<SkillPhase>();

        public float TotalDuration
        {
            get
            {
                float t = 0f;
                foreach (var p in phases) t += Mathf.Max(0f, p.duration);
                return t;
            }
        }
    }
}
