using System.Collections.Generic;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    public enum EnemyStateId
    {
        Idle = 0,
        Patrol = 1,
        Alert = 2,
        Chase = 3,
        Reposition = 4,
        Attack = 5,
        Block = 6,
        Dodge = 7,
        Stagger = 8,
        Knockdown = 9,
        Airborne = 10,
        Dead = 11,
        Special = 12
    }

    /// <summary>Base class of the enemy finite state machine.</summary>
    public abstract class EnemyState
    {
        protected EnemyController E;
        public abstract EnemyStateId Id { get; }
        public void Bind(EnemyController e) => E = e;
        public virtual void Enter() { }
        public virtual void Exit() { }
        public abstract void Tick(float dt);
        protected EnemyData Data => E.Data;
    }

    public sealed class IdleState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Idle;
        private float _wait;

        public override void Enter()
        {
            E.Motor.SetDesiredVelocity(Vector3.zero);
            _wait = Random.Range(1.5f, 4f);
        }

        public override void Tick(float dt)
        {
            if (E.CanSeePlayer())
            {
                E.Alert();
                return;
            }
            if (E.Aware && E.PlayerAlive)
            {
                E.ChangeState(EnemyStateId.Chase);
                return;
            }
            _wait -= dt;
            if (_wait <= 0f && Data.patrolRadius > 0.5f) E.ChangeState(EnemyStateId.Patrol);
        }
    }

    public sealed class PatrolState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Patrol;
        private Vector3 _target;
        private float _timeout;

        public override void Enter()
        {
            Vector2 r = Random.insideUnitCircle * Data.patrolRadius;
            _target = E.Home + new Vector3(r.x, 0f, r.y);
            _timeout = 8f;
        }

        public override void Tick(float dt)
        {
            if (E.CanSeePlayer())
            {
                E.Alert();
                return;
            }
            _timeout -= dt;
            E.Motor.MoveTowards(_target, Data.walkSpeed * E.SpeedMultiplier, 0.6f);
            Vector3 d = _target - E.transform.position;
            d.y = 0f;
            if (d.magnitude < 0.8f || _timeout <= 0f) E.ChangeState(EnemyStateId.Idle);
        }

        public override void Exit() => E.Motor.SetDesiredVelocity(Vector3.zero);
    }

    /// <summary>Spots the player: short roar so the player notices, then the pack joins.</summary>
    public sealed class AlertState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Alert;
        private float _duration;

        public override void Enter()
        {
            E.SetAware(true);
            E.Motor.SetDesiredVelocity(Vector3.zero);
            E.FacePlayer();
            _duration = E.IsBoss ? 1.6f : 0.65f;
            E.Anim.PlayMotion(E.IsBoss ? "OniRoar" : "EnemyRoar", _duration, 0.1f);
            E.PlaySfx(E.IsBoss ? "boss_roar" : "enemy_roar", E.IsBoss ? 1f : 0.6f);
            E.SpawnVfx("telegraph_glint", E.Rig.EyePoint.position, Quaternion.identity, 0.8f);
            EncounterDirector.AlertAround(E.transform.position, 14f);
            if (E.IsBoss) CameraFX.Shake(0.4f);
        }

        public override void Tick(float dt)
        {
            E.FacePlayer();
            if (E.StateTime >= _duration)
            {
                E.Anim.StopAction(0.2f);
                E.ChangeState(EnemyStateId.Chase);
            }
        }
    }

    public sealed class ChaseState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Chase;
        private float _tokenRetry;

        public override void Enter() => _tokenRetry = 0f;

        public override void Tick(float dt)
        {
            if (!E.PlayerAlive)
            {
                E.SetAware(false);
                E.ChangeState(EnemyStateId.Patrol);
                return;
            }
            float dist = E.DistanceToPlayer;
            if (dist > Data.loseRadius)
            {
                E.SetAware(false);
                E.ChangeState(EnemyStateId.Patrol);
                return;
            }

            float range = E.MinReadyAttackRange;
            _tokenRetry -= dt;
            if (dist <= E.MaxAttackRange && _tokenRetry <= 0f)
            {
                var attack = E.ChooseAttack(dist);
                if (attack != null)
                {
                    float aggression = Mathf.Clamp01(Data.aggression + E.AggressionBonus);
                    if (Random.value < aggression + 0.25f && E.TryAcquireToken())
                    {
                        E.PendingAttack = attack;
                        E.ChangeState(EnemyStateId.Attack);
                        return;
                    }
                    _tokenRetry = 0.4f;
                    if (dist < Data.preferredDistance + 1f)
                    {
                        E.ChangeState(EnemyStateId.Reposition);
                        return;
                    }
                }
            }

            if (dist > range * 0.85f)
            {
                float speed = (dist > 6f ? Data.runSpeed : Data.walkSpeed * 1.4f) * E.SpeedMultiplier;
                E.Motor.MoveTowards(E.Player.position, speed, range * 0.7f);
            }
            else
            {
                E.Motor.SetDesiredVelocity(Vector3.zero);
                E.FacePlayer();
            }
        }

        public override void Exit() => E.Motor.SetDesiredVelocity(Vector3.zero);
    }

    /// <summary>Circles the player at a safe distance while waiting for an attack token (readable group fights).</summary>
    public sealed class RepositionState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Reposition;
        private float _duration;
        private float _side;

        public override void Enter()
        {
            _duration = Random.Range(1f, 2.4f);
            _side = Random.value < 0.5f ? -1f : 1f;
        }

        public override void Tick(float dt)
        {
            if (!E.PlayerAlive)
            {
                E.ChangeState(EnemyStateId.Idle);
                return;
            }
            Vector3 toPlayer = E.DirectionToPlayer;
            float dist = E.DistanceToPlayer;
            Vector3 tangent = Vector3.Cross(Vector3.up, toPlayer) * _side;
            float radial = (dist - Data.preferredDistance) * 0.8f;
            Vector3 v = tangent * Data.walkSpeed * 0.8f + toPlayer * Mathf.Clamp(radial, -1.5f, 1.5f);
            E.Motor.SetDesiredVelocity(v * E.SpeedMultiplier);
            E.FacePlayer();
            if (E.StateTime >= _duration) E.ChangeState(EnemyStateId.Chase);
        }

        public override void Exit() => E.Motor.SetDesiredVelocity(Vector3.zero);
    }

    /// <summary>Telegraph (windup) → hit frames → recovery, with multi-hit strings, lunges, leaps and projectiles.</summary>
    public sealed class AttackState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Attack;
        public bool InWindup => _phase == Phase.Windup;

        private enum Phase { Windup, Active, Interval, Recovery }

        private EnemyAttackData _a;
        private Phase _phase;
        private float _t;
        private int _hitIndex;
        private bool _hitDone;
        private Vector3 _lockedDir;
        private Vector3 _leapTarget;
        private bool _leaping;
        private readonly HitRegistry _registry = new HitRegistry();
        private readonly List<IDamageable> _targets = new List<IDamageable>();
        private float _speed;

        public override void Enter()
        {
            _a = E.PendingAttack;
            if (_a == null)
            {
                E.ChangeState(EnemyStateId.Chase);
                return;
            }
            _speed = Mathf.Max(0.5f, E.SpeedMultiplier);
            _hitIndex = 0;
            _leaping = false;
            E.Motor.SetDesiredVelocity(Vector3.zero);
            E.FacePlayer(true);
            _lockedDir = E.DirectionToPlayer;
            BeginSwing(true);

            if (_a.telegraphed)
            {
                E.SpawnVfx(_a.telegraphVfx, E.Anim.WeaponTip.position, Quaternion.identity, 1.2f, E.Anim.WeaponTip);
                E.SpawnVfx("telegraph_glint", E.Rig.EyePoint.position, Quaternion.identity, 1f, E.Rig.EyePoint);
                E.PlaySfx("telegraph", 0.8f);
                E.Rig.SetHitFlash(0.35f, new Color(1f, 0.15f, 0.1f));
            }
            if (_a.groundSlam && _a.aoeRadius > 0f)
            {
                Vector3 at = _a.movement == EnemyAttackMovement.Leap && E.Player != null ? E.Player.position : E.transform.position + E.transform.forward * _a.reach * 0.6f * Data.scale;
                _leapTarget = at;
                TelegraphMarker.Show(at, _a.aoeRadius, _a.windup / _speed + (_a.movement == EnemyAttackMovement.Leap ? 0.45f : 0f), new Color(1f, 0.1f, 0.15f, 0.8f));
            }
        }

        private void BeginSwing(bool first)
        {
            _phase = Phase.Windup;
            _t = 0f;
            _hitDone = false;
            _registry.Begin(1, 1f);
            float windup = (first ? _a.windup : Mathf.Max(0.12f, _a.comboInterval * 0.6f)) / _speed;
            string motion = _a.motionId;
            if (_hitIndex % 2 == 1 && motion == "EnemySwipe") motion = "EnemySwipe2";
            E.Anim.PlayAttack(motion, windup, _a.active / _speed, _a.recovery / _speed);
        }

        public override void Tick(float dt)
        {
            if (_a == null) return;
            _t += dt;
            float windup = (_hitIndex == 0 ? _a.windup : Mathf.Max(0.12f, _a.comboInterval * 0.6f)) / _speed;
            float active = _a.active / _speed;

            switch (_phase)
            {
                case Phase.Windup:
                    // Track the player during most of the windup, then commit (dodgeable timing).
                    if (_t < windup * 0.7f)
                    {
                        E.FacePlayer();
                        _lockedDir = E.DirectionToPlayer;
                    }
                    if (_a.movement == EnemyAttackMovement.Leap && !_leaping && _t >= windup * 0.55f)
                    {
                        _leaping = true;
                        Vector3 to = _leapTarget - E.transform.position;
                        to.y = 0f;
                        float airTime = windup * 0.45f + active;
                        E.Motor.Jump(_a.leapHeight);
                        E.Motor.ForceMove(to / Mathf.Max(0.1f, airTime), airTime);
                    }
                    if (_t >= windup)
                    {
                        _phase = Phase.Active;
                        _t = 0f;
                        OnActiveStart();
                    }
                    break;
                case Phase.Active:
                    if (!_hitDone || _a.movement == EnemyAttackMovement.Charge) CheckHits();
                    if (_t >= active && (_a.movement != EnemyAttackMovement.Leap || E.Motor.Grounded || _t > active + 1f))
                    {
                        if (_a.groundSlam) Slam();
                        _hitIndex++;
                        if (_hitIndex < Mathf.Max(1, _a.comboHits))
                        {
                            _phase = Phase.Interval;
                            _t = 0f;
                        }
                        else
                        {
                            _phase = Phase.Recovery;
                            _t = 0f;
                        }
                    }
                    break;
                case Phase.Interval:
                    E.FacePlayer();
                    if (_t >= 0.05f)
                    {
                        _lockedDir = E.DirectionToPlayer;
                        BeginSwing(false);
                    }
                    break;
                case Phase.Recovery:
                    if (_t >= _a.recovery / _speed) Finish();
                    break;
            }
        }

        private void OnActiveStart()
        {
            E.Rig.SetHitFlash(0f);
            E.PlaySfx(_a.sfx, 0.8f);
            if (!string.IsNullOrEmpty(_a.swingVfx))
                E.SpawnVfx(_a.swingVfx, E.transform.position, Quaternion.LookRotation(_lockedDir), 1f);
            float active = _a.active / _speed;
            switch (_a.movement)
            {
                case EnemyAttackMovement.Lunge:
                    E.Motor.ForceMove(_lockedDir * (_a.moveDistance * Data.scale / Mathf.Max(0.05f, active)), active);
                    break;
                case EnemyAttackMovement.Charge:
                    E.Motor.ForceMove(_lockedDir * (_a.moveDistance / Mathf.Max(0.05f, active)), active);
                    break;
            }
            if (_a.projectile != null && _a.projectile.enabled) FireProjectiles();
            CheckHits();
        }

        private void CheckHits()
        {
            if (_a.projectile != null && _a.projectile.enabled) return;
            if (_a.groundSlam && _a.aoeRadius > 0f) return; // slams hit on impact
            Vector3 origin = E.transform.position + Vector3.up * 1f * Data.scale;
            HitQuery.Cone(origin, _lockedDir, (_a.reach + _a.hitRadius) * Data.scale, _a.angle, Team.Enemy, _targets);
            foreach (var t in _targets)
            {
                if (t.Team != Team.Player || !_registry.CanHit(t, Time.time)) continue;
                if (Mathf.Abs(t.CenterPoint.y - origin.y) > 2.2f * Data.scale) continue;
                _registry.Register(t, Time.time);
                var hit = E.BuildHit(_a, t.CenterPoint, _lockedDir);
                t.ReceiveHit(hit);
                _hitDone = true;
            }
        }

        private void Slam()
        {
            Vector3 center = E.transform.position + (_a.movement == EnemyAttackMovement.Leap ? Vector3.zero : E.transform.forward * _a.reach * 0.6f * Data.scale);
            HitQuery.GroundPoint(center + Vector3.up, out var ground, out _);
            E.SpawnVfx("ground_impact", ground, Quaternion.LookRotation(_lockedDir), _a.aoeRadius / 3f);
            E.SpawnVfx("shockwave", ground, Quaternion.identity, _a.aoeRadius / 4f);
            if (E.Phase >= 2 && E.IsBoss)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 p = ground + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * _a.aoeRadius * 0.75f;
                    VFXLibrary.Spawn("fire_column", p, Quaternion.identity, 0.55f, Element.Dark);
                }
            }
            CameraFX.Shake(E.IsBoss ? 0.8f : 0.4f);
            E.PlaySfx("explosion", E.IsBoss ? 1f : 0.6f);
            HitQuery.Sphere(ground + Vector3.up, _a.aoeRadius, Team.Enemy, _targets);
            foreach (var t in _targets)
            {
                if (t.Team != Team.Player || !_registry.CanHit(t, Time.time)) continue;
                _registry.Register(t, Time.time);
                Vector3 dir = t.CenterPoint - ground;
                dir.y = 0f;
                t.ReceiveHit(E.BuildHit(_a, t.CenterPoint, dir.sqrMagnitude > 0.01f ? dir.normalized : _lockedDir));
            }
            DestructibleUtility.BreakInRadius(ground, _a.aoeRadius, E.gameObject);
        }

        private void FireProjectiles()
        {
            var spec = _a.projectile;
            int count = Mathf.Max(1, spec.count);
            Vector3 origin = E.Anim.WeaponTip.position;
            var target = E.PlayerController != null ? E.PlayerController.Rig.LockOnPoint : null;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-spec.spreadAngle * 0.5f, spec.spreadAngle * 0.5f, i / (float)(count - 1));
                Vector3 aim = target != null ? (target.position - origin).normalized : _lockedDir;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * aim;
                var hit = E.BuildHit(_a, origin, _lockedDir);
                hit.Multiplier = spec.damageMultiplier;
                hit.Reaction = spec.reaction;
                hit.Knockback = spec.knockback;
                Projectile.Spawn(spec.vfxId, origin, dir, spec.speed, spec.lifetime, spec.radius, spec.pierce, spec.homing ? target : null,
                    Team.Enemy, hit, null, spec.impactVfx, Element.Dark);
            }
        }

        private void Finish()
        {
            E.StartCooldown(_a);
            E.ReleaseToken();
            E.ChangeState(Random.value < 0.4f ? EnemyStateId.Reposition : EnemyStateId.Chase);
        }

        public override void Exit()
        {
            E.ReleaseToken();
            E.Rig.SetHitFlash(0f);
            if (_a != null && _phase != Phase.Recovery) E.StartCooldown(_a);
            E.Anim.StopAction(0.15f);
            _a = null;
        }
    }

    public sealed class BlockState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Block;
        private float _duration;

        public override void Enter()
        {
            E.IsBlocking = true;
            E.Motor.SetDesiredVelocity(Vector3.zero);
            _duration = Random.Range(0.8f, 1.6f);
            E.Anim.PlayMotion(E.IsBoss ? "OniRoar" : "EnemyBlock", _duration, 0.08f);
        }

        public override void Tick(float dt)
        {
            E.FacePlayer();
            if (E.StateTime < _duration) return;
            // Counter attack right after guarding.
            float dist = E.DistanceToPlayer;
            var attack = E.ChooseAttack(dist);
            if (attack != null && E.TryAcquireToken())
            {
                E.PendingAttack = attack;
                E.ChangeState(EnemyStateId.Attack);
            }
            else E.ChangeState(EnemyStateId.Reposition);
        }

        public override void Exit()
        {
            E.IsBlocking = false;
            E.Anim.StopAction(0.1f);
        }
    }

    public sealed class DodgeState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Dodge;

        public override void Enter()
        {
            Vector3 away = -E.DirectionToPlayer;
            Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? 1f : -1f);
            Vector3 dir = (away + side * 0.6f).normalized;
            E.Motor.ForceMove(dir * 11f, 0.3f);
            E.Anim.SetDodge(dir, 0.3f);
            E.Damageable.Invulnerable = true;
            E.SpawnVfx("dodge_wind", E.transform.position, Quaternion.LookRotation(dir), 0.8f);
            AfterimageSystem.Spawn(E.Rig, new Color(1.2f, 0.1f, 0.4f, 0.4f), 0.3f);
        }

        public override void Tick(float dt)
        {
            E.FacePlayer();
            if (E.StateTime >= 0.34f) E.ChangeState(EnemyStateId.Reposition);
        }

        public override void Exit() => E.Damageable.Invulnerable = false;
    }

    public sealed class StaggerState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Stagger;
        public float Duration = 0.6f;

        public override void Enter()
        {
            E.Motor.SetDesiredVelocity(Vector3.zero);
            E.ReleaseToken();
            // Long staggers (poise break, stun) read as a real loss of balance; short ones stay additive flinches.
            if (Duration >= 0.45f) E.Anim.PlayMotion("EnemyStagger", Duration, 0.05f);
            else E.Anim.StopAction(0.05f);
        }

        public override void Tick(float dt)
        {
            if (E.StateTime >= Duration) E.ChangeState(E.Aware ? EnemyStateId.Chase : EnemyStateId.Idle);
        }

        public override void Exit() => E.Anim.StopAction(0.12f);
    }

    public sealed class KnockdownState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Knockdown;
        private float _duration;

        public override void Enter()
        {
            E.Motor.SetDesiredVelocity(Vector3.zero);
            E.ReleaseToken();
            E.Anim.SetKnockedDown(true);
            _duration = E.IsBoss ? 1.2f : 1.6f;
        }

        public override void Tick(float dt)
        {
            if (E.StateTime >= _duration - 0.4f) E.Anim.SetKnockedDown(false);
            if (E.StateTime >= _duration) E.ChangeState(EnemyStateId.Chase);
        }

        public override void Exit() => E.Anim.SetKnockedDown(false);
    }

    /// <summary>Launched / juggled in the air. Lands into a short knockdown.</summary>
    public sealed class AirborneState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Airborne;

        public override void Enter()
        {
            E.ReleaseToken();
            E.Motor.SetDesiredVelocity(Vector3.zero);
            E.Anim.StopAction(0.05f);
        }

        public override void Tick(float dt)
        {
            if (E.StateTime > 0.2f && E.Motor.Grounded)
            {
                E.SpawnVfx("dust_puff", E.transform.position, Quaternion.identity);
                E.ChangeState(EnemyStateId.Knockdown);
            }
            else if (E.StateTime > 6f) E.ChangeState(EnemyStateId.Chase);
        }
    }

    /// <summary>Death: collapse, then the demon disintegrates into ash and embers.</summary>
    public sealed class DeadState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Dead;
        private bool _dissolving;
        private bool _burst;

        public override void Enter()
        {
            E.IsBlocking = false;
            E.ReleaseToken();
            E.Anim.SetDead(true);
            E.Motor.SetDesiredVelocity(Vector3.zero);
            E.PlaySfx("enemy_death", 0.8f);
            _dissolving = false;
            _burst = false;
        }

        public override void Tick(float dt)
        {
            float t = E.StateTime;
            if (!_burst && t > 0.7f)
            {
                _burst = true;
                E.SpawnVfx("demon_death", E.transform.position, Quaternion.identity);
            }
            if (t > 0.6f)
            {
                _dissolving = true;
                E.Rig.SetDissolve(Mathf.Clamp01((t - 0.6f) / 1.3f));
            }
            if (_dissolving && t > 2f)
            {
                var controller = E.Motor.Controller;
                if (controller != null) controller.enabled = false;
                Object.Destroy(E.gameObject);
            }
        }
    }

    /// <summary>Scripted moments (boss phase transition). Driven by <see cref="BossController"/>.</summary>
    public sealed class SpecialState : EnemyState
    {
        public override EnemyStateId Id => EnemyStateId.Special;
        public float Duration = 2f;
        public System.Action OnDone;

        public override void Enter()
        {
            E.Motor.SetDesiredVelocity(Vector3.zero);
            E.ReleaseToken();
        }

        public override void Tick(float dt)
        {
            E.FacePlayer();
            if (E.StateTime >= Duration)
            {
                var done = OnDone;
                OnDone = null;
                done?.Invoke();
                if (E.StateId == EnemyStateId.Special) E.ChangeState(EnemyStateId.Chase);
            }
        }
    }
}
