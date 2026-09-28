using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    /// <summary>
    /// Demon enemy: owns the finite state machine and reacts to hits (flinch, stagger, knockback, launch,
    /// knockdown, death). Behaviour is data driven by <see cref="EnemyData"/>.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class EnemyController : MonoBehaviour, IHitReactor, IHitInterceptor, ITargetable, IStaggerable
    {
        public EnemyData Data { get; private set; }
        public ProceduralAnimator Anim { get; private set; }
        public CharacterRig Rig { get; private set; }
        public EnemyMotor Motor { get; private set; }
        public Damageable Damageable { get; private set; }
        public PoiseModel Poise { get; private set; }
        public Vector3 Home { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public float AggressionBonus { get; set; }
        public bool SuperArmor { get; set; }
        public bool Aware { get; private set; }
        public EnemyAttackData PendingAttack { get; set; }
        public EnemyStateId StateId => _current != null ? _current.Id : EnemyStateId.Idle;
        public bool IsBlocking { get; set; }
        public int Phase { get; set; } = 1;
        public float StateTime => _stateTime;

        /// <summary>(previous, next) — raised on every FSM transition (HUD / diagnostics / playtest).</summary>
        public event System.Action<EnemyStateId, EnemyStateId> StateChanged;

        /// <summary>
        /// Development hook: when set, the next attack choice picks this attack whenever it is usable at the
        /// current distance (deterministic playtests). Cleared once used. Gameplay never sets it.
        /// </summary>
        public string ForcedNextAttackId { get; set; }

        public Transform LockOnPoint => Rig != null ? Rig.LockOnPoint : transform;
        public bool IsTargetable => IsAlive && gameObject.activeInHierarchy;
        public bool IsBoss => Data != null && Data.isBoss;
        IDamageable ITargetable.Damageable => Damageable;
        public bool IsAlive => Damageable != null && Damageable.IsAlive;
        public int Priority => 50;
        public int TokenId => GetInstanceID();

        private readonly Dictionary<EnemyStateId, EnemyState> _states = new Dictionary<EnemyStateId, EnemyState>();
        private readonly Dictionary<string, float> _attackReady = new Dictionary<string, float>();
        private EnemyState _current;
        private float _stateTime;
        private float _lastPlayerAttackSeen;
        private int _lastPlayerAttackInstance = -1;

        public Transform Player => PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        public PlayerController PlayerController => PlayerController.Instance;

        public void Initialize(EnemyData data, CharacterRig rig, ProceduralAnimator anim, EnemyMotor motor, Damageable damageable)
        {
            Data = data;
            Rig = rig;
            Anim = anim;
            Motor = motor;
            Damageable = damageable;
            Poise = new PoiseModel(data.poise, data.poise * 0.6f, 1.6f);
            Home = transform.position;
            motor.TurnSpeed = data.turnSpeed;
            motor.KnockbackResistance = data.knockbackResistance;
            damageable.AddReactor(this);
            damageable.AddInterceptor(this);
            damageable.Died += OnDied;

            Add(new IdleState());
            Add(new PatrolState());
            Add(new AlertState());
            Add(new ChaseState());
            Add(new RepositionState());
            Add(new AttackState());
            Add(new BlockState());
            Add(new DodgeState());
            Add(new StaggerState());
            Add(new KnockdownState());
            Add(new AirborneState());
            Add(new DeadState());
            Add(new SpecialState());
            ChangeState(EnemyStateId.Idle);
        }

        private void Add(EnemyState s)
        {
            s.Bind(this);
            _states[s.Id] = s;
        }

        private void OnEnable()
        {
            TargetRegistry.Register(this);
            EncounterDirector.Register(this);
        }

        private void OnDisable()
        {
            TargetRegistry.Unregister(this);
            EncounterDirector.Unregister(this);
        }

        public void ChangeState(EnemyStateId id)
        {
            if (!_states.TryGetValue(id, out var next)) return;
            if (_current != null && _current.Id == EnemyStateId.Dead) return;
            var previous = _current != null ? _current.Id : id;
            _current?.Exit();
            _current = next;
            _stateTime = 0f;
            _current.Enter();
            StateChanged?.Invoke(previous, id);
        }

        public T GetState<T>(EnemyStateId id) where T : EnemyState => _states.TryGetValue(id, out var s) ? s as T : null;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _current == null) return;
            _stateTime += dt;
            Poise.Tick(dt);
            _current.Tick(dt);
            WatchPlayerAttacks();
            Anim.SetLocomotion(new LocomotionState
            {
                Velocity = Motor.Velocity,
                Grounded = Motor.Grounded,
                Sprinting = Motor.Velocity.magnitude > Data.walkSpeed * 1.3f,
                Blocking = IsBlocking,
                CombatStance = Aware
            });
            Anim.SetLookTarget(Aware && IsAlive ? PlayerLockPoint : null);
        }

        private Transform PlayerLockPoint => PlayerController != null && PlayerController.Rig != null ? PlayerController.Rig.LockOnPoint : Player;

        // ------------------------------------------------------------------ perception

        public float DistanceToPlayer
        {
            get
            {
                var p = Player;
                if (p == null) return float.MaxValue;
                Vector3 d = p.position - transform.position;
                d.y = 0f;
                return d.magnitude;
            }
        }

        public Vector3 DirectionToPlayer
        {
            get
            {
                var p = Player;
                if (p == null) return transform.forward;
                Vector3 d = p.position - transform.position;
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
            }
        }

        public bool PlayerAlive => PlayerController != null && PlayerController.State != PlayerState.Dead;

        public bool CanSeePlayer()
        {
            var p = Player;
            if (p == null || !PlayerAlive) return false;
            float d = DistanceToPlayer;
            if (d > Data.detectRadius * (Aware ? 1.8f : 1f)) return false;
            if (!Aware && Vector3.Angle(transform.forward, DirectionToPlayer) > Data.viewAngle * 0.5f && d > 4f) return false;
            return !HitQuery.Blocked(transform.position + Vector3.up * 1.4f * Data.scale, p.position + Vector3.up * 1.2f);
        }

        public void FacePlayer(bool instant = false)
        {
            if (instant) Motor.FaceInstant(DirectionToPlayer);
            else Motor.Face(DirectionToPlayer);
        }

        /// <summary>Becomes aware of the player (alerted by sight, damage or pack members).</summary>
        public void Alert()
        {
            if (Aware || !IsAlive) return;
            Aware = true;
            if (StateId == EnemyStateId.Idle || StateId == EnemyStateId.Patrol) ChangeState(EnemyStateId.Alert);
        }

        public void SetAware(bool aware) => Aware = aware;

        // ------------------------------------------------------------------ attacks

        public List<EnemyAttackData> CurrentAttacks => Phase >= 2 && Data.phase2Attacks.Count > 0 ? Data.phase2Attacks : Data.attacks;

        public EnemyAttackData ChooseAttack(float distance)
        {
            var list = CurrentAttacks;
            if (!string.IsNullOrEmpty(ForcedNextAttackId))
            {
                foreach (var a in list)
                {
                    if (a.attackId != ForcedNextAttackId || !AttackUsable(a, distance)) continue;
                    ForcedNextAttackId = null;
                    return a;
                }
            }
            float total = 0f;
            foreach (var a in list)
                if (AttackUsable(a, distance)) total += a.weight;
            if (total <= 0f) return null;
            float pick = Random.value * total;
            foreach (var a in list)
            {
                if (!AttackUsable(a, distance)) continue;
                pick -= a.weight;
                if (pick <= 0f) return a;
            }
            return null;
        }

        public bool AttackUsable(EnemyAttackData a, float distance)
        {
            if (distance < a.minRange || distance > a.maxRange) return false;
            return !_attackReady.TryGetValue(a.attackId, out var ready) || Time.time >= ready;
        }

        public float MaxAttackRange
        {
            get
            {
                float r = 0f;
                foreach (var a in CurrentAttacks) r = Mathf.Max(r, a.maxRange);
                return r;
            }
        }

        public float MinReadyAttackRange
        {
            get
            {
                float r = float.MaxValue;
                foreach (var a in CurrentAttacks)
                    if (!_attackReady.TryGetValue(a.attackId, out var ready) || Time.time >= ready) r = Mathf.Min(r, a.maxRange);
                return r == float.MaxValue ? 2f : r;
            }
        }

        public void StartCooldown(EnemyAttackData a) => _attackReady[a.attackId] = Time.time + a.cooldown / Mathf.Max(0.5f, SpeedMultiplier);

        public bool TryAcquireToken()
        {
            if (IsBoss)
            {
                EncounterDirector.Tokens.ForceAcquire(TokenId);
                return true;
            }
            return EncounterDirector.Tokens.TryAcquire(TokenId);
        }

        public void ReleaseToken() => EncounterDirector.Tokens.Release(TokenId);

        public HitData BuildHit(EnemyAttackData a, Vector3 point, Vector3 direction)
        {
            bool heavy = a.reaction >= HitReaction.Heavy || a.damageMultiplier >= 1.6f;
            return new HitData
            {
                Attacker = gameObject,
                AttackerTeam = Team.Enemy,
                SourceId = a.attackId,
                Category = heavy ? DamageCategory.EnemyHeavy : DamageCategory.EnemyLight,
                BaseDamage = Data.baseDamage,
                Multiplier = a.damageMultiplier,
                CritMultiplier = 1.5f,
                Element = Phase >= 2 ? Element.Dark : Element.None,
                Reaction = a.reaction,
                HitPoint = point,
                Direction = direction,
                Knockback = a.knockback,
                LaunchHeight = a.launchHeight,
                Unblockable = a.unblockable,
                Parryable = a.parryable
            };
        }

        /// <summary>Dodges when it sees a heavy player attack coming (reactive AI).</summary>
        private void WatchPlayerAttacks()
        {
            var pc = PlayerController;
            if (pc == null || !Aware || !IsAlive) return;
            if (pc.State != PlayerState.Attack || pc.Combat.Current == null) return;
            if (StateId != EnemyStateId.Chase && StateId != EnemyStateId.Reposition) return;
            int id = pc.Combat.Current.GetInstanceID();
            if (id == _lastPlayerAttackInstance || Time.time - _lastPlayerAttackSeen < 0.8f) return;
            _lastPlayerAttackInstance = id;
            _lastPlayerAttackSeen = Time.time;
            if (DistanceToPlayer > 3.6f) return;
            float r = Random.value;
            if (pc.Combat.Current.category == DamageCategory.Heavy && r < Data.dodgeChance) ChangeState(EnemyStateId.Dodge);
            else if (r < Data.blockChance) ChangeState(EnemyStateId.Block);
        }

        // ------------------------------------------------------------------ hits

        public HitOutcome Intercept(ref HitData hit, out float blockReduction)
        {
            blockReduction = 0f;
            if (!IsBlocking || hit.Unblockable) return HitOutcome.Hit;
            Vector3 toAttacker = hit.Attacker != null ? hit.Attacker.transform.position - transform.position : -hit.Direction;
            toAttacker.y = 0f;
            if (Vector3.Angle(transform.forward, toAttacker) > 100f) return HitOutcome.Hit;
            // Guard breaks against heavies and techniques.
            if (hit.Category == DamageCategory.Heavy || hit.Category == DamageCategory.Skill || hit.Category == DamageCategory.Ultimate ||
                hit.Category == DamageCategory.Finisher || hit.Category == DamageCategory.Counter)
            {
                IsBlocking = false;
                hit.Reaction = HitReaction.Heavy;
                hit.PoiseDamage += Data.poise;
                GameEvents.Notify("GUARD BREAK!");
                return HitOutcome.Hit;
            }
            blockReduction = 0.75f;
            return HitOutcome.Blocked;
        }

        public void OnHitResolved(HitData hit, HitResult result)
        {
            if (result.Killed || !IsAlive) return;
            if (result.Outcome == HitOutcome.Blocked)
            {
                Motor.Knockback(hit.Direction * 1.5f);
                return;
            }
            if (!result.Landed) return;

            if (!Aware)
            {
                Aware = true;
                EncounterDirector.AlertAround(transform.position, 12f);
            }

            if (hit.PullStrength > 0f && hit.PullTarget != Vector3.zero)
            {
                Vector3 pull = hit.PullTarget - transform.position;
                pull.y = 0f;
                Motor.Knockback(pull.normalized * Mathf.Min(hit.PullStrength, pull.magnitude * 6f));
            }

            bool poiseBroken = Poise.ApplyDamage(Mathf.Max(hit.PoiseDamage, 4f));
            bool airborne = !Motor.Grounded;

            if (hit.Reaction == HitReaction.Launch && Data.canBeLaunched && hit.LaunchHeight > 0f)
            {
                Motor.Launch(hit.LaunchHeight, Mathf.Max(0.4f, hit.AirHang));
                Motor.Knockback(hit.Direction * hit.Knockback * 0.5f);
                Anim.PlayHit(HitReaction.Heavy, hit.Direction);
                ChangeState(EnemyStateId.Airborne);
                return;
            }

            if (airborne && Data.canBeLaunched)
            {
                // Juggle: keep floating while combos continue.
                Motor.AirHang(Mathf.Max(0.35f, hit.AirHang));
                Motor.Knockback(hit.Direction * hit.Knockback * 0.35f);
                Anim.PlayHit(HitReaction.Light, hit.Direction);
                if (StateId != EnemyStateId.Airborne) ChangeState(EnemyStateId.Airborne);
                return;
            }

            if (SuperArmor && !poiseBroken)
            {
                Anim.PlayHit(HitReaction.Light, hit.Direction);
                return;
            }

            Anim.PlayHit(hit.Reaction, hit.Direction);
            switch (hit.Reaction)
            {
                case HitReaction.Knockdown:
                    Motor.Knockback(hit.Direction * Mathf.Max(5f, hit.Knockback));
                    if (Data.canBeLaunched) Motor.Launch(0.8f, 0f);
                    ChangeState(EnemyStateId.Knockdown);
                    return;
                case HitReaction.Knockback:
                    Motor.Knockback(hit.Direction * Mathf.Max(4f, hit.Knockback));
                    StaggerFor(0.5f);
                    return;
            }

            Motor.Knockback(hit.Direction * hit.Knockback);
            if (poiseBroken || hit.Reaction >= HitReaction.Heavy || hit.Reaction == HitReaction.Stun)
            {
                StaggerFor(hit.Reaction == HitReaction.Stun ? Mathf.Max(1.2f, hit.StatusDuration) : Data.staggerDuration);
            }
            else if (StateId != EnemyStateId.Attack || GetState<AttackState>(EnemyStateId.Attack).InWindup)
            {
                // Light hits interrupt windups and idle behaviour (flinch).
                StaggerFor(0.28f);
            }
        }

        private void StaggerFor(float duration)
        {
            var s = GetState<StaggerState>(EnemyStateId.Stagger);
            s.Duration = duration;
            ChangeState(EnemyStateId.Stagger);
        }

        public void Stagger(float duration, Vector3 fromDirection)
        {
            if (!IsAlive) return;
            Poise.Reset();
            Motor.Knockback(fromDirection * 2.5f);
            Anim.PlayHit(HitReaction.Heavy, fromDirection);
            StaggerFor(duration);
        }

        private void OnDied(HitData hit)
        {
            Motor.Knockback(hit.Direction * Mathf.Max(3f, hit.Knockback));
            ChangeState(EnemyStateId.Dead);
            EncounterDirector.NotifyDeath(this);
            GameEvents.RaiseEnemyKilled(gameObject, hit.Attacker);
        }

        public void PlaySfx(string id, float volume = 1f) => Sfx.Play(id, transform.position + Vector3.up, volume, IsBoss ? 0.8f : 1f);

        public void SpawnVfx(string id, Vector3 position, Quaternion rotation, float scale = 1f, Transform follow = null)
        {
            VFXLibrary.Spawn(id, position, rotation, scale * Data.scale, Phase >= 2 ? Element.Dark : Element.Dark, follow);
        }
    }
}
