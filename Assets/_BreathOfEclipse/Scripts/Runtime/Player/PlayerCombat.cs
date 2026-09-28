using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Player
{
    /// <summary>
    /// Sword attacks and combo strings. Resolves the next attack through the <see cref="ComboGraph"/>, drives the
    /// animation, lunges toward targets, sweeps the blade during hit frames and exposes cancel windows.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        public AttackData Current { get; private set; }
        public bool IsAttacking { get; private set; }
        public float Elapsed { get; private set; }
        public float Normalized => Current == null || Current.TotalDuration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Current.TotalDuration);
        public bool InActiveFrames => IsAttacking && Elapsed >= Current.windup && Elapsed <= Current.windup + Current.active;
        public ComboGraph Graph { get; private set; }

        private PlayerController _pc;
        private ComboData _combos;
        private WeaponData _weapon;
        private readonly HitRegistry _registry = new HitRegistry();
        private readonly List<IDamageable> _targets = new List<IDamageable>();
        private readonly List<Vector3> _points = new List<Vector3>();
        private bool _activeStarted;
        private bool _activeEnded;
        private bool _plungeLanded;
        private Vector3 _prevBase, _prevTip;
        private bool _hasPrev;
        private Transform _aimTarget;

        public void Initialize(PlayerController pc, ComboData combos, WeaponData weapon)
        {
            _pc = pc;
            _combos = combos;
            _weapon = weapon;
            Graph = combos.BuildGraph();
        }

        /// <summary>Tries to start (or continue) a combo with the given button.</summary>
        public bool TryAttack(ComboInput input, ComboContext ctx)
        {
            string current = IsAttacking && Current != null ? Current.attackId : _lastFinishedId;
            if (!IsAttacking && Time.time - _lastFinishedTime > 0.35f) current = null;
            string nextId = Graph.Resolve(current, input, ctx);
            var attack = _combos.Find(nextId);
            if (attack == null) return false;
            Begin(attack);
            return true;
        }

        private string _lastFinishedId;
        private float _lastFinishedTime = -10f;

        public void Begin(AttackData attack)
        {
            Current = attack;
            IsAttacking = true;
            Elapsed = 0f;
            _activeStarted = false;
            _activeEnded = false;
            _plungeLanded = false;
            _hasPrev = false;
            _registry.Begin(attack.maxHitsPerTarget, attack.multiHitInterval);

            _pc.Animator.PlayAttack(attack.motionId, attack.windup, attack.active, attack.recovery);

            // Auto aim: lock target, else the closest enemy roughly in front.
            _aimTarget = _pc.LockOn.CurrentPoint;
            if (_aimTarget == null)
            {
                Vector3 inputDir = _pc.WorldMoveInput.sqrMagnitude > 0.01f ? _pc.WorldMoveInput : transform.forward;
                var t = TargetRegistry.Closest(transform.position, 5.5f, inputDir, 6f);
                _aimTarget = t != null ? t.LockOnPoint : null;
            }
            Vector3 dir = transform.forward;
            if (_aimTarget != null)
            {
                dir = _aimTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) _pc.Motor.Face(dir, 2400f);
            }
            else if (_pc.WorldMoveInput.sqrMagnitude > 0.01f)
            {
                dir = _pc.WorldMoveInput;
                _pc.Motor.Face(dir, 2400f);
            }
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

            // Lunge: stop in front of the target instead of pushing into it.
            float distance = attack.forwardDistance;
            if (_aimTarget != null)
            {
                float d = Vector3.Distance(Flat(_aimTarget.position), Flat(transform.position));
                distance = Mathf.Clamp(d - 1.25f, 0f, Mathf.Max(attack.forwardDistance, attack.category == DamageCategory.Light ? 2.2f : 3f));
            }
            float moveTime = Mathf.Max(0.05f, attack.windup + attack.active);
            bool airborne = !_pc.Motor.Grounded;
            if (distance > 0.01f) _pc.Motor.ForceMove(dir * (distance / moveTime), moveTime, airborne && attack.airHang, false);
            if (airborne && attack.airHang) _pc.Motor.AirHang(attack.TotalDuration);

            _pc.SetTrails(true, attack.elementTrail, attack.trailWidth);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public bool CanCancel(CancelFlags flag) => IsAttacking && Current != null && Current.CanCancel(flag, Normalized);

        public void Cancel()
        {
            if (!IsAttacking) return;
            Finish(true);
        }

        private void Finish(bool cancelled)
        {
            if (Current != null)
            {
                _lastFinishedId = Current.attackId;
                _lastFinishedTime = Time.time;
            }
            IsAttacking = false;
            if (cancelled) _pc.Animator.StopAction(0.08f);
            _pc.SetTrails(false, false, 1f);
            if (Current != null && Current.plunge) _pc.Motor.SetGravityScale(1f);
        }

        /// <summary>Called by the controller every frame while attacking.</summary>
        public void Tick(float dt)
        {
            if (!IsAttacking || Current == null) return;
            var a = Current;
            Elapsed += dt;

            if (!_activeStarted && Elapsed >= a.windup)
            {
                _activeStarted = true;
                OnActiveStart(a);
            }

            if (_activeStarted && !_activeEnded)
            {
                SweepBlade(a);
                if (a.plunge && !_plungeLanded && _pc.Motor.Grounded && Elapsed > a.windup + 0.02f)
                {
                    _plungeLanded = true;
                    PlungeImpact(a);
                }
                if (Elapsed > a.windup + a.active && (!a.plunge || _plungeLanded || Elapsed > a.windup + a.active + 0.8f))
                {
                    _activeEnded = true;
                    _pc.SetTrails(false, false, 1f);
                }
            }

            if (Elapsed >= a.TotalDuration && (!a.plunge || _plungeLanded || _pc.Motor.Grounded)) Finish(false);
        }

        private void OnActiveStart(AttackData a)
        {
            Sfx.Play(a.swingSfx, transform.position + Vector3.up, 0.9f, a.category == DamageCategory.Heavy ? 0.85f : 1f);
            if (a.verticalVelocity > 0f) _pc.Motor.SetVerticalVelocity(a.verticalVelocity);
            if (a.plunge)
            {
                _pc.Motor.CancelAirHang();
                _pc.Motor.SetVerticalVelocity(-26f);
            }
            if (a.speedLines) ScreenFX.SpeedLines(0.5f, a.active + 0.1f, _pc.Breathing.CurrentPalette.Core);
            if (a.elementTrail && _pc.Breathing.Current != null) Sfx.Play(_pc.Breathing.Current.equipSfx, transform.position, 0.35f);
            // Generous arc check on the first hit frame so wide swings feel fair.
            if (a.arcRadius > 0f)
            {
                HitQuery.Cone(transform.position + Vector3.up, transform.forward, a.arcRadius + a.reachBonus, a.arcAngle, Team.Player, _targets);
                for (int i = 0; i < _targets.Count; i++) ApplyHit(a, _targets[i], _targets[i].CenterPoint);
            }
        }

        private void SweepBlade(AttackData a)
        {
            var baseT = _pc.Animator.WeaponBase;
            var tipT = _pc.Animator.WeaponTip;
            Vector3 b = baseT.position, t = tipT.position;
            Vector3 dir = t - b;
            t += dir.normalized * a.reachBonus;
            if (!_hasPrev)
            {
                _prevBase = b;
                _prevTip = t;
                _hasPrev = true;
            }
            float radius = _weapon.hitRadius * a.hitRadiusMultiplier;
            HitQuery.BladeSweep(_prevBase, _prevTip, b, t, radius, 5, Team.Player, _targets, _points);
            for (int i = 0; i < _targets.Count; i++) ApplyHit(a, _targets[i], _points[i]);
            _prevBase = b;
            _prevTip = t;
        }

        private void PlungeImpact(AttackData a)
        {
            HitQuery.Sphere(transform.position + Vector3.up * 0.5f, 3f, Team.Player, _targets);
            for (int i = 0; i < _targets.Count; i++) ApplyHit(a, _targets[i], _targets[i].CenterPoint);
            VFXLibrary.Spawn("ground_impact", transform.position, transform.rotation, 1.2f, _pc.Breathing.CurrentElement);
            VFXLibrary.Spawn("shockwave", transform.position, Quaternion.identity, 1f, _pc.Breathing.CurrentElement);
            CameraSystem.CameraFX.Shake(0.5f);
            Sfx.Play("explosion", transform.position, 0.6f);
            DestructibleUtility.BreakInRadius(transform.position, 3f, _pc.gameObject);
        }

        private void ApplyHit(AttackData a, IDamageable target, Vector3 point)
        {
            if (target == null || !_registry.CanHit(target, Time.time)) return;
            _registry.Register(target, Time.time);
            var hit = BuildHit(a, target, point);
            var result = target.ReceiveHit(hit);
            if (result.Outcome == HitOutcome.Ignored) return;
            _pc.NotifyHitLanded(hit, result);
            var feel = new HitFeel
            {
                CameraShake = a.cameraShake,
                FovPunch = a.fovPunch,
                Zoom = a.cameraZoom,
                ZoomDuration = 0.3f,
                HitSfx = a.hitSfx,
                ImpactVfx = a.impactVfx,
                GroundImpact = a.groundImpact,
                SpeedLines = a.speedLines,
                SlowMoScale = a.slowMoScale,
                SlowMoDuration = a.slowMoDuration
            };
            CombatFeedback.PlayerHitLanded(hit, result, feel, _pc.Breathing.CurrentElement);
        }

        public HitData BuildHit(AttackData a, IDamageable target, Vector3 point)
        {
            Vector3 dir = target.CenterPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            var buffs = _pc.Stats.Buffs;
            bool counter = a.category == DamageCategory.Counter;
            return new HitData
            {
                Attacker = gameObject,
                AttackerTeam = Team.Player,
                SourceId = a.attackId,
                AttackInstanceId = _registry.AttackInstanceId,
                Category = a.category,
                BaseDamage = _weapon.baseDamage,
                Multiplier = a.damageMultiplier,
                BonusPercent = buffs.GetBonus(StatType.Damage),
                CritChance = _weapon.critChance + a.bonusCritChance + buffs.GetBonus(StatType.CritChance),
                CritMultiplier = _weapon.critMultiplier,
                ForceCritical = a.forceCritical || counter,
                Element = a.element,
                Reaction = a.reaction,
                HitPoint = point,
                Direction = dir.normalized,
                Knockback = a.knockback,
                LaunchHeight = a.launchHeight,
                PoiseDamage = a.poiseDamage,
                HitStop = a.hitStop,
                CameraShake = a.cameraShake,
                CanBreakObjects = a.canBreakObjects,
                IsFinisher = a.isFinisher,
                FlashFrame = a.flashFrame,
                AirHang = a.targetAirHang
            };
        }
    }
}
