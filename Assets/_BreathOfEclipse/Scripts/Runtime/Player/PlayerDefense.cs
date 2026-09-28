using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Player
{
    /// <summary>Anything that can be staggered by a parry.</summary>
    public interface IStaggerable
    {
        void Stagger(float duration, Vector3 fromDirection);
    }

    /// <summary>
    /// Block, parry (perfect block) and dodge i-frames with perfect dodge. Implemented as a hit interceptor
    /// on the player's Damageable, so every enemy attack goes through the same rules.
    /// </summary>
    public sealed class PlayerDefense : MonoBehaviour, IHitInterceptor
    {
        public int Priority => 100;

        public bool IsBlocking { get; private set; }
        public bool IsDodging => Time.time < _dodgeEnd;
        public bool SkillInvulnerable { get; set; }
        /// <summary>Real-time window after a perfect dodge in which Light triggers the counter attack.</summary>
        public bool InCounterWindow => Time.unscaledTime < _counterWindowEnd;
        /// <summary>Window after a parry in which Heavy triggers the riposte.</summary>
        public bool InParryWindow => Time.unscaledTime < _parryWindowEnd;
        public Transform LastThreat { get; private set; }

        private PlayerController _pc;
        private PlayerData _data;
        private float _blockPressTime = -10f;
        private float _dodgeStart = -10f;
        private float _dodgeEnd;
        private float _iFramesEnd;
        private bool _perfectUsed;
        private float _counterWindowEnd;
        private float _parryWindowEnd;

        public void Initialize(PlayerController pc, PlayerData data)
        {
            _pc = pc;
            _data = data;
        }

        public void BeginBlock()
        {
            IsBlocking = true;
        }

        /// <summary>Called when the block button is pressed (parry timing starts here).</summary>
        public void RegisterBlockPress() => _blockPressTime = Time.time;

        public void EndBlock() => IsBlocking = false;

        public void BeginDodge(float duration)
        {
            _dodgeStart = Time.time;
            _dodgeEnd = Time.time + duration;
            _iFramesEnd = Time.time + _data.dodgeInvulnerability;
            _perfectUsed = false;
        }

        public void ConsumeCounterWindow() => _counterWindowEnd = 0f;
        public void ConsumeParryWindow() => _parryWindowEnd = 0f;

        public HitOutcome Intercept(ref HitData hit, out float blockReduction)
        {
            blockReduction = 0f;
            if (hit.AttackerTeam == Team.Player) return HitOutcome.Ignored;
            if (SkillInvulnerable) return HitOutcome.Evaded;

            float now = Time.time;
            if (now < _iFramesEnd)
            {
                if (!_perfectUsed && now - _dodgeStart <= _data.perfectDodgeWindow)
                {
                    _perfectUsed = true;
                    PerfectDodge(hit);
                    return HitOutcome.PerfectEvaded;
                }
                return HitOutcome.Evaded;
            }

            if (IsBlocking && !hit.Unblockable && FacingThreat(hit))
            {
                if (hit.Parryable && now - _blockPressTime <= _data.parryWindow)
                {
                    Parry(hit);
                    return HitOutcome.Parried;
                }
                float cost = _data.blockStaminaPerHit * (hit.Category == DamageCategory.EnemyHeavy ? 1.7f : 1f);
                _pc.Stats.Stamina.Spend(cost);
                if (_pc.Stats.Stamina.IsExhausted)
                {
                    // Guard break: the hit lands and staggers.
                    hit.Reaction = HitReaction.Heavy;
                    GameEvents.Notify("GUARD BREAK");
                    return HitOutcome.Hit;
                }
                blockReduction = _data.blockReduction;
                return HitOutcome.Blocked;
            }
            return HitOutcome.Hit;
        }

        private bool FacingThreat(HitData hit)
        {
            Vector3 toAttacker = hit.Attacker != null ? hit.Attacker.transform.position - transform.position : -hit.Direction;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.01f) return true;
            return Vector3.Angle(transform.forward, toAttacker) < 115f;
        }

        private void PerfectDodge(HitData hit)
        {
            LastThreat = hit.Attacker != null ? hit.Attacker.transform : null;
            _counterWindowEnd = Time.unscaledTime + _data.counterWindow;
            if (TimeController.Instance != null)
                TimeController.Instance.SlowMotion(_data.perfectDodgeSlowScale, _data.perfectDodgeSlowDuration, "perfect_dodge", 0.15f);
            AfterimageSystem.Spawn(_pc.Rig, new Color(0.5f, 0.9f, 2.5f, 0.8f), 0.6f);
            AfterimageSystem.Spawn(_pc.Rig, new Color(0.9f, 0.6f, 2.5f, 0.5f), 0.9f, 0.05f);
            VFXLibrary.Spawn("perfect_dodge", transform.position, Quaternion.identity);
            Sfx.Play2D("perfect_dodge", 0.9f);
            CameraFX.FovPunch(-6f);
            var post = CameraFX.Post;
            if (post != null)
            {
                post.HoldSaturation("perfect_dodge", -45f, 0.45f);
                post.PulseChromatic(0.5f);
            }
            _pc.Stats.Breath.Gain(BreathSource.PerfectDodge);
            GameEvents.RaisePerfectDodge(transform.position, hit.Attacker);
            GameEvents.Notify("PERFECT DODGE — counter with [Light]");
        }

        private void Parry(HitData hit)
        {
            LastThreat = hit.Attacker != null ? hit.Attacker.transform : null;
            _parryWindowEnd = Time.unscaledTime + 1.2f;
            Vector3 sparkPoint = _pc.Animator.WeaponTip.position;
            VFXLibrary.Spawn("parry_spark", Vector3.Lerp(_pc.Animator.WeaponBase.position, sparkPoint, 0.6f), Quaternion.identity);
            Sfx.Play("parry", transform.position + Vector3.up, 1f);
            CameraFX.Shake(0.45f);
            CameraFX.Zoom(0.8f, 0.4f);
            CameraFX.FovPunch(-5f);
            if (TimeController.Instance != null)
            {
                TimeController.Instance.HitStop(0.07f);
                TimeController.Instance.SlowMotion(_data.parrySlowScale, _data.parrySlowDuration, "parry", 0.12f);
            }
            FlashFrameSystem.Trigger(sparkPoint, new Color(4f, 3f, 1.2f), 2);
            if (hit.Attacker != null)
            {
                var stagger = hit.Attacker.GetComponentInParent<IStaggerable>();
                stagger?.Stagger(1.4f, transform.forward);
            }
            _pc.Stats.Breath.Gain(BreathSource.Parry);
            _pc.Animator.PlayMotion("Parry", 0.3f, 0.02f);
            GameEvents.RaiseParry(transform.position, hit.Attacker);
            GameEvents.Notify("PARRY — riposte with [Heavy]");
        }
    }
}
