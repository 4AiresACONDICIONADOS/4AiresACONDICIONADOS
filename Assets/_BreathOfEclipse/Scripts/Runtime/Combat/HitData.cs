using UnityEngine;

namespace BreathOfEclipse.Combat
{
    public enum Team
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2
    }

    /// <summary>Broad category of a hit; drives hit stop, camera and flash-frame presets.</summary>
    public enum DamageCategory
    {
        Light = 0,
        Heavy = 1,
        Skill = 2,
        Ultimate = 3,
        Finisher = 4,
        Counter = 5,
        Projectile = 6,
        Environment = 7,
        EnemyLight = 8,
        EnemyHeavy = 9
    }

    /// <summary>
    /// Everything an attacker knows about a hit before the defender resolves it
    /// (the defender adds its defense, weaknesses, block/parry state).
    /// </summary>
    public struct HitData
    {
        public GameObject Attacker;
        public Team AttackerTeam;
        public string SourceId;
        public int AttackInstanceId;
        public DamageCategory Category;

        public float BaseDamage;
        public float Multiplier;
        public float BonusPercent;
        public float CritChance;
        public float CritMultiplier;
        public bool ForceCritical;
        public Element Element;

        public HitReaction Reaction;
        public Vector3 HitPoint;
        /// <summary>World direction the hit travels (attacker -> target), flattened for ground hits.</summary>
        public Vector3 Direction;
        public float Knockback;
        public float LaunchHeight;
        public float PoiseDamage;
        public float HitStop;
        public float CameraShake;

        public bool Unblockable;
        public bool Parryable;
        public bool CanBreakObjects;
        public bool IsFinisher;
        public bool FlashFrame;
        /// <summary>Seconds of elemental status (burn / stun) applied on hit.</summary>
        public float StatusDuration;
        /// <summary>Pull toward this point (vortex techniques). Zero = none.</summary>
        public Vector3 PullTarget;
        public float PullStrength;
        /// <summary>Keeps airborne targets floating (air combos).</summary>
        public float AirHang;
    }

    /// <summary>The defender's answer to a <see cref="HitData"/>.</summary>
    public struct HitResult
    {
        public HitOutcome Outcome;
        public int Damage;
        public bool IsCritical;
        public bool Weakness;
        public GameObject Target;
        public Vector3 HitPoint;
        public bool Killed;

        public bool Landed => Outcome == HitOutcome.Hit || Outcome == HitOutcome.Killed || Outcome == HitOutcome.Blocked;

        public static HitResult Ignored => new HitResult { Outcome = HitOutcome.Ignored };
    }

    /// <summary>Anything that can receive hits: characters, dummies, destructibles.</summary>
    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        Transform Transform { get; }
        /// <summary>Point effects should aim at (chest height).</summary>
        Vector3 CenterPoint { get; }
        HitResult ReceiveHit(HitData hit);
    }

    /// <summary>
    /// Gets a chance to modify or cancel a hit before damage is applied (block, parry, dodge i-frames).
    /// Returning an outcome other than <see cref="HitOutcome.Hit"/> short-circuits the damage pipeline,
    /// except <see cref="HitOutcome.Blocked"/> which still applies reduced damage.
    /// </summary>
    public interface IHitInterceptor
    {
        int Priority { get; }
        HitOutcome Intercept(ref HitData hit, out float blockReduction);
    }

    /// <summary>Reacts to resolved hits (animation, knockback, AI state changes).</summary>
    public interface IHitReactor
    {
        void OnHitResolved(HitData hit, HitResult result);
    }
}
