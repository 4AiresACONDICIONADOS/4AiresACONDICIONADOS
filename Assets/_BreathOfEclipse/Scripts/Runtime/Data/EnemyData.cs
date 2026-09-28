using System;
using System.Collections.Generic;
using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Data
{
    public enum EnemyArchetype
    {
        Nightspawn = 0,
        HollowOni = 1,
        TrainingDummy = 2
    }

    public enum EnemyAttackMovement
    {
        None = 0,
        Lunge = 1,
        Leap = 2,
        Charge = 3
    }

    /// <summary>One enemy attack: telegraph (windup), hit frames, recovery. Supports multi-hit strings.</summary>
    [Serializable]
    public sealed class EnemyAttackData
    {
        public string attackId = "claw";
        public string displayName = "Claw";
        public string motionId = "EnemySwipe";
        public float minRange;
        public float maxRange = 2.4f;
        public float weight = 1f;
        public float cooldown = 1.5f;

        [Header("Timing")]
        public float windup = 0.55f;
        public float active = 0.14f;
        public float recovery = 0.6f;
        public int comboHits = 1;
        public float comboInterval = 0.45f;

        [Header("Damage")]
        public float damageMultiplier = 1f;
        public HitReaction reaction = HitReaction.Light;
        public float knockback = 2.5f;
        public float launchHeight;
        public float hitRadius = 0.9f;
        public float reach = 1.9f;
        [Range(0f, 360f)] public float angle = 120f;
        public float aoeRadius;
        public bool unblockable;
        public bool parryable = true;

        [Header("Movement")]
        public EnemyAttackMovement movement = EnemyAttackMovement.Lunge;
        public float moveDistance = 1.2f;
        public float leapHeight = 2.5f;

        [Header("Presentation")]
        [Tooltip("Strong visual warning (red glint / ground marker) before the hit.")] public bool telegraphed;
        public string telegraphVfx = "telegraph_glint";
        public string swingVfx = "enemy_swipe";
        public string impactVfx = "impact_dark";
        public string sfx = "enemy_swipe";
        public bool groundSlam;

        [Header("Projectile (optional)")]
        public ProjectileSpec projectile = new ProjectileSpec();

        public float TotalDuration => windup + (active + comboInterval) * Mathf.Max(1, comboHits) + recovery;
    }

    [CreateAssetMenu(menuName = "Breath of Eclipse/Enemy", fileName = "Enemy_")]
    public sealed class EnemyData : ScriptableObject
    {
        public string enemyId = "nightspawn";
        public string displayName = "Nightspawn";
        public EnemyArchetype archetype = EnemyArchetype.Nightspawn;
        public bool isBoss;

        [Header("Stats")]
        public float maxHealth = 140f;
        public float poise = 30f;
        public float defense;
        public Element weakness = Element.None;
        public Element resistance = Element.None;
        public float baseDamage = 11f;
        [Range(0f, 1f)] public float knockbackResistance;
        public bool canBeLaunched = true;
        public float breathOnKill = 6f;

        [Header("Movement")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 5.2f;
        public float turnSpeed = 540f;
        public float scale = 1f;

        [Header("Perception")]
        public float detectRadius = 15f;
        public float loseRadius = 28f;
        [Range(0f, 360f)] public float viewAngle = 220f;

        [Header("Behaviour")]
        public float preferredDistance = 3.2f;
        [Range(0f, 1f)] public float aggression = 0.6f;
        [Range(0f, 1f)] public float dodgeChance = 0.15f;
        [Range(0f, 1f)] public float blockChance = 0.15f;
        public float reactionTime = 0.25f;
        public float staggerDuration = 0.7f;
        public float patrolRadius = 6f;

        [Header("Attacks")]
        public List<EnemyAttackData> attacks = new List<EnemyAttackData>();

        [Header("Look")]
        public Color bodyColor = new Color(0.08f, 0.06f, 0.1f);
        public Color accentColor = new Color(0.35f, 0.05f, 0.12f);
        [ColorUsage(true, true)] public Color eyeColor = new Color(3f, 0.3f, 0.2f);

        [Header("Boss phase 2")]
        [Range(0f, 1f)] public float phase2Threshold = 0.5f;
        public float phase2SpeedMultiplier = 1.35f;
        public float phase2AggressionBonus = 0.3f;
        public List<EnemyAttackData> phase2Attacks = new List<EnemyAttackData>();
        [ColorUsage(true, true)] public Color phase2EyeColor = new Color(4f, 0.1f, 0.6f);
        public string phase2Music = "boss_phase2";
        public string phase1Music = "boss_phase1";
    }
}
