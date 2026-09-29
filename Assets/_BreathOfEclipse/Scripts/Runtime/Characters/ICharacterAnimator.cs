using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>Per-frame locomotion inputs for a character animator.</summary>
    public struct LocomotionState
    {
        public Vector3 Velocity;
        public bool Grounded;
        public bool Sprinting;
        public bool Blocking;
        public bool CombatStance;
        public bool Exhausted;
    }

    /// <summary>
    /// Everything gameplay code needs from an animation backend. The prototype uses <see cref="ProceduralAnimator"/>;
    /// <see cref="MecanimCharacterAnimator"/> maps the same calls onto an Animator Controller for real models.
    /// </summary>
    public interface ICharacterAnimator
    {
        Transform WeaponBase { get; }
        Transform WeaponTip { get; }
        bool IsActionPlaying { get; }
        /// <summary>Diagnostics: the character is shown lying down.</summary>
        bool KnockdownPoseActive { get; }

        void SetLocomotion(LocomotionState state);
        /// <summary>Plays an attack: windup (anticipation), active (swing), recovery (follow-through).</summary>
        void PlayAttack(string motionId, float windup, float active, float recovery);
        /// <summary>Plays a pose/clip for <paramref name="duration"/> seconds and holds the last pose until stopped or replaced.</summary>
        void PlayMotion(string motionId, float duration, float blendIn);
        void StopAction(float blendOut);
        void PlayHit(HitReaction reaction, Vector3 worldDirection);
        void SetDodge(Vector3 worldDirection, float duration);
        void SetKnockedDown(bool value, float getUpDuration = 0.4f);
        void SetDead(bool value);
        void SetLookTarget(Transform target);
    }
}
