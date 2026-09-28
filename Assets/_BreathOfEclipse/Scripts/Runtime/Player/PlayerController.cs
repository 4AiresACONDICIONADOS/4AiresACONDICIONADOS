using System.Collections;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Breathing;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Player
{
    public enum PlayerState
    {
        Locomotion = 0,
        Attack = 1,
        Skill = 2,
        Dodge = 3,
        Block = 4,
        HitStun = 5,
        Knockdown = 6,
        Dead = 7
    }

    /// <summary>Something the player can interact with (E).</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        Vector3 Position { get; }
        bool CanInteract { get; }
        void Interact(PlayerController player);
    }

    /// <summary>
    /// Player composition root and action state machine. Decides what the player is doing each frame using
    /// buffered input, cancel windows and priorities (Ultimate &gt; Techniques &gt; Dodge &gt; Attacks &gt; Jump &gt; Block).
    /// Movement, combat, defense, lock-on and breathing live in their own components.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerController : MonoBehaviour, IHitReactor
    {
        public static PlayerController Instance { get; private set; }

        public PlayerState State { get; private set; } = PlayerState.Locomotion;
        public PlayerData Data { get; private set; }
        public WeaponData Weapon { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerStats Stats { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public PlayerDefense Defense { get; private set; }
        public TargetLockSystem LockOn { get; private set; }
        public BreathingStyleSystem Breathing { get; private set; }
        public ProceduralAnimator Animator { get; private set; }
        public CharacterRig Rig { get; private set; }
        public Damageable Damageable { get; private set; }

        /// <summary>Camera-relative movement input in world space (magnitude 0..1).</summary>
        public Vector3 WorldMoveInput { get; private set; }
        public bool IsSprinting { get; private set; }
        public Vector3 SpawnPoint { get; set; }
        public Quaternion SpawnRotation { get; set; } = Quaternion.identity;
        public IInteractable NearbyInteractable { get; private set; }

        private SwordTrail _swordTrail;
        private SwordTrail _elementTrail;
        private float _stateTimer;
        private float _lastCombatTime = -100f;
        private float _dodgeEndTime;
        private bool _airDodgeUsed;
        private float _respawnInvulnerableUntil;
        private float _footstepTimer;

        private static readonly BufferedAction[] AttackButtons = { BufferedAction.LightAttack, BufferedAction.HeavyAttack };
        private static readonly CancelFlags[] AttackCancelFlags = { CancelFlags.Attack, CancelFlags.Dodge, CancelFlags.Skill, CancelFlags.Block, CancelFlags.Ultimate, CancelFlags.Jump };

        // ------------------------------------------------------------------ setup

        public void Initialize(PlayerData data, WeaponData weapon, ComboData combos, GameDatabase db, CharacterRig rig, ProceduralAnimator animator,
            PlayerMotor motor, PlayerStats stats, PlayerCombat combat, PlayerDefense defense, TargetLockSystem lockOn,
            BreathingStyleSystem breathing, Damageable damageable)
        {
            Instance = this;
            Data = data;
            Weapon = weapon;
            Rig = rig;
            Animator = animator;
            Motor = motor;
            Stats = stats;
            Combat = combat;
            Defense = defense;
            LockOn = lockOn;
            Breathing = breathing;
            Damageable = damageable;
            SpawnPoint = transform.position;
            SpawnRotation = transform.rotation;

            damageable.AddReactor(this);
            damageable.Died += OnDied;
            motor.Landed += OnLanded;

            _swordTrail = SwordTrail.Create("SwordTrail", transform, animator.WeaponBase, animator.WeaponTip,
                MaterialFactory.SwordTrail("sword_default", weapon.trailGradient, VfxBlend.Additive));
            _swordTrail.Lifetime = weapon.trailLifetime;
            _elementTrail = SwordTrail.Create("ElementTrail", transform, animator.WeaponBase, animator.WeaponTip,
                MaterialFactory.SwordTrail("element_default", weapon.trailGradient, VfxBlend.Additive));
            _elementTrail.Lifetime = 0.3f;
            _elementTrail.StartFraction = 0.35f;
            _elementTrail.LengthScale = 1.75f;
            _elementTrail.WidthOverAge = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1.1f), new Keyframe(1f, 0.4f));

            breathing.Initialize(this, db.styles, SaveSystem.Settings.lastEquippedStyle);
            breathing.Executor.Finished += OnSkillFinished;
        }

        private void OnEnable()
        {
            var input = InputReader.Instance;
            if (input == null) return;
            input.BlockPressed += OnBlockPressed;
            input.InteractPressed += OnInteract;
            input.NextStylePressed += OnNextStyle;
            input.PrevStylePressed += OnPrevStyle;
        }

        private void OnDisable()
        {
            var input = InputReader.Instance;
            if (input == null) return;
            input.BlockPressed -= OnBlockPressed;
            input.InteractPressed -= OnInteract;
            input.NextStylePressed -= OnNextStyle;
            input.PrevStylePressed -= OnPrevStyle;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnBlockPressed() => Defense?.RegisterBlockPress();
        private void OnNextStyle() => Breathing?.Cycle(1);
        private void OnPrevStyle() => Breathing?.Cycle(-1);

        /// <summary>Applies the equipped style colors to both trails.</summary>
        public void ApplyStyleTrails(BreathingStyleData style)
        {
            if (_swordTrail == null) return;
            var swordGradient = new Gradient();
            swordGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(style.secondaryColor, 0.3f), new GradientColorKey(style.baseColor, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            _swordTrail.SetMaterial(MaterialFactory.SwordTrail("sword_" + style.styleId, swordGradient, VfxBlend.Additive));
            var blend = style.element == Element.Water ? VfxBlend.AlphaBlend : VfxBlend.Additive;
            _elementTrail.SetMaterial(MaterialFactory.SwordTrail("element_" + style.styleId, style.elementTrailGradient, blend));
        }

        public void SetTrails(bool sword, bool element, float width)
        {
            if (_swordTrail == null) return;
            _swordTrail.Emitting = sword;
            _elementTrail.Emitting = element;
            _elementTrail.LengthScale = 1.45f + 0.3f * width;
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var input = InputReader.Instance;
            ReadMoveInput(input);
            UpdateInteractable();

            switch (State)
            {
                case PlayerState.Locomotion:
                    Locomotion(input, dt, 1f);
                    TryActions(input, CancelFlags.All);
                    break;
                case PlayerState.Attack:
                    Combat.Tick(dt);
                    Motor.SetDesiredVelocity(Vector3.zero);
                    if (!Combat.IsAttacking) EnterLocomotion();
                    else
                    {
                        CancelFlags allowed = CancelFlags.None;
                        foreach (var f in AttackCancelFlags)
                            if (Combat.CanCancel(f)) allowed |= f;
                        if (allowed != CancelFlags.None) TryActions(input, allowed);
                        if (State == PlayerState.Attack && Combat.CanCancel(CancelFlags.Move) && WorldMoveInput.sqrMagnitude > 0.2f)
                        {
                            Combat.Cancel();
                            EnterLocomotion();
                        }
                    }
                    break;
                case PlayerState.Skill:
                    Breathing.Tick(dt);
                    if (!Breathing.IsExecuting) EnterLocomotion();
                    else if (Breathing.Executor.CanCancel) TryActions(input, CancelFlags.Attack | CancelFlags.Dodge | CancelFlags.Jump | CancelFlags.Skill);
                    break;
                case PlayerState.Dodge:
                    Motor.SetDesiredVelocity(Vector3.zero);
                    if (Time.time >= _dodgeEndTime)
                    {
                        EnterLocomotion();
                    }
                    else if (Time.time >= _dodgeEndTime - Data.dodgeDuration * 0.45f)
                    {
                        TryActions(input, CancelFlags.Attack | CancelFlags.Skill | CancelFlags.Ultimate);
                    }
                    break;
                case PlayerState.Block:
                    if (input == null || !input.BlockHeld)
                    {
                        Defense.EndBlock();
                        EnterLocomotion();
                        break;
                    }
                    Locomotion(input, dt, Data.blockMoveSpeed / Data.runSpeed);
                    if (Stats.Stamina.IsExhausted)
                    {
                        Defense.EndBlock();
                        EnterLocomotion();
                        break;
                    }
                    TryActions(input, CancelFlags.Attack | CancelFlags.Dodge | CancelFlags.Skill | CancelFlags.Ultimate);
                    break;
                case PlayerState.HitStun:
                    Motor.SetDesiredVelocity(Vector3.zero);
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f) EnterLocomotion();
                    break;
                case PlayerState.Knockdown:
                    Motor.SetDesiredVelocity(Vector3.zero);
                    _stateTimer -= dt;
                    if (_stateTimer <= 0.35f) Animator.SetKnockedDown(false);
                    if (_stateTimer <= 0f) EnterLocomotion();
                    else if (_stateTimer < Data.knockdownDuration - 0.4f) TryActions(input, CancelFlags.Dodge);
                    break;
                case PlayerState.Dead:
                    Motor.SetDesiredVelocity(Vector3.zero);
                    break;
            }

            if (Damageable != null) Damageable.Invulnerable = Time.time < _respawnInvulnerableUntil;
            UpdateAnimator();
        }

        private void ReadMoveInput(InputReader input)
        {
            Vector2 move = input != null ? input.Move : Vector2.zero;
            var rig = CameraRig.Instance;
            Vector3 fwd = rig != null ? rig.PlanarForward : Vector3.forward;
            Vector3 right = rig != null ? rig.PlanarRight : Vector3.right;
            WorldMoveInput = Vector3.ClampMagnitude(fwd * move.y + right * move.x, 1f);
        }

        private void Locomotion(InputReader input, float dt, float speedScale)
        {
            Vector3 dir = WorldMoveInput;
            float mag = dir.magnitude;
            bool locked = LockOn.CurrentPoint != null;
            bool wantsSprint = input != null && input.SprintHeld && mag > 0.5f && State == PlayerState.Locomotion;
            IsSprinting = wantsSprint && !Stats.Stamina.IsExhausted && Motor.Grounded;
            if (IsSprinting) Stats.Stamina.Drain(Data.sprintStaminaPerSecond, dt);

            float speed = mag < 0.55f ? Mathf.Lerp(0f, Data.walkSpeed, mag / 0.55f) : Data.runSpeed;
            if (IsSprinting) speed = Data.sprintSpeed;
            if (locked && !IsSprinting) speed *= 0.85f;
            speed *= speedScale * Stats.Buffs.GetMultiplier(StatType.MoveSpeed);
            Motor.SetDesiredVelocity(mag > 0.01f ? dir.normalized * speed : Vector3.zero);

            var cam = CameraRig.Instance;
            bool firstPerson = cam != null && cam.Mode == CameraMode.FirstPerson && !cam.CinematicActive;
            if (firstPerson)
            {
                Motor.Face(cam.PlanarForward, 1440f);
            }
            else if (locked && !IsSprinting)
            {
                Vector3 to = LockOn.CurrentPoint.position - transform.position;
                Motor.Face(to, 720f);
            }
            else if (mag > 0.05f)
            {
                Motor.Face(dir, Data.turnSpeed);
            }

            // Footstep sounds.
            if (Motor.Grounded && Motor.PlanarVelocity.magnitude > 1f)
            {
                _footstepTimer -= dt * Motor.PlanarVelocity.magnitude;
                if (_footstepTimer <= 0f)
                {
                    _footstepTimer = 3.2f;
                    Sfx.Play("footstep", transform.position, 0.25f);
                }
            }
        }

        private void UpdateAnimator()
        {
            Animator.SetLocomotion(new LocomotionState
            {
                Velocity = Motor.Velocity,
                Grounded = Motor.Grounded,
                Sprinting = IsSprinting,
                Blocking = State == PlayerState.Block,
                CombatStance = LockOn.CurrentPoint != null || Time.time - _lastCombatTime < 5f || State == PlayerState.Block,
                Exhausted = Stats.Stamina.IsExhausted
            });
            Animator.SetLookTarget(LockOn.CurrentPoint);
        }

        // ------------------------------------------------------------------ actions

        private void TryActions(InputReader input, CancelFlags allowed)
        {
            if (input == null) return;
            var buffer = input.Buffer;
            float now = Time.unscaledTime;
            bool locked = LockOn.CurrentPoint != null;

            if ((allowed & CancelFlags.Ultimate) != 0 && buffer.Has(BufferedAction.Ultimate, now))
            {
                buffer.Consume(BufferedAction.Ultimate, now);
                if (StartSkill(BreathingStyleSystem.UltimateSlot)) return;
            }

            if ((allowed & CancelFlags.Skill) != 0)
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    var action = (BufferedAction)((int)BufferedAction.Skill1 + slot);
                    if (!buffer.Has(action, now)) continue;
                    buffer.Consume(action, now);
                    if (StartSkill(slot)) return;
                }
            }

            if ((allowed & CancelFlags.Dodge) != 0)
            {
                bool dodge = buffer.Has(BufferedAction.Dodge, now);
                // Optional "direction + Space" dodge (configurable).
                if (!dodge && buffer.Has(BufferedAction.Jump, now, 0.12f) && WorldMoveInput.sqrMagnitude > 0.25f && Motor.Grounded)
                {
                    var mode = SaveSystem.Settings.DodgeMode;
                    if (mode == DodgeInputMode.DirectionalJumpAlways || (mode == DodgeInputMode.DirectionalJumpWhenLockedOn && locked))
                    {
                        dodge = true;
                        buffer.Consume(BufferedAction.Jump, now);
                    }
                }
                if (dodge)
                {
                    buffer.Consume(BufferedAction.Dodge, now);
                    if (StartDodge()) return;
                }
            }

            if ((allowed & CancelFlags.Attack) != 0 && buffer.TryGetLatest(AttackButtons, now, out var attackButton))
            {
                var ctx = new ComboContext
                {
                    Airborne = !Motor.Grounded,
                    Dashing = (IsSprinting && Motor.PlanarVelocity.magnitude > 6.5f) || (State == PlayerState.Dodge && WorldMoveInput.sqrMagnitude > 0.2f),
                    PerfectDodgeWindow = Defense.InCounterWindow,
                    ParryWindow = Defense.InParryWindow,
                    AllowRestart = true
                };
                var comboInput = attackButton == BufferedAction.LightAttack ? ComboInput.Light : ComboInput.Heavy;
                if (StartAttack(comboInput, ctx))
                {
                    buffer.Consume(attackButton, now);
                    if (ctx.PerfectDodgeWindow && comboInput == ComboInput.Light) Defense.ConsumeCounterWindow();
                    if (ctx.ParryWindow && comboInput == ComboInput.Heavy) Defense.ConsumeParryWindow();
                    return;
                }
            }

            if ((allowed & CancelFlags.Jump) != 0 && buffer.Has(BufferedAction.Jump, now))
            {
                if (Motor.CanJump)
                {
                    buffer.Consume(BufferedAction.Jump, now);
                    if (State != PlayerState.Locomotion) CancelCurrentAction();
                    bool airJump = !Motor.Grounded && Motor.TimeSinceGrounded > 0.12f;
                    if (Motor.TryJump())
                    {
                        State = PlayerState.Locomotion;
                        Sfx.Play("jump", transform.position, 0.5f);
                        if (airJump)
                        {
                            VFXLibrary.Spawn("dodge_wind", transform.position, transform.rotation, 0.7f);
                            AfterimageSystem.Spawn(Rig, new Color(0.6f, 0.8f, 1.5f, 0.4f), 0.3f);
                        }
                        return;
                    }
                }
            }

            if ((allowed & CancelFlags.Block) != 0 && input.BlockHeld && State != PlayerState.Block && Motor.Grounded)
            {
                CancelCurrentAction();
                State = PlayerState.Block;
                Defense.BeginBlock();
                _lastCombatTime = Time.time;
            }
        }

        private void CancelCurrentAction()
        {
            if (Combat.IsAttacking) Combat.Cancel();
            if (Breathing.IsExecuting) Breathing.Cancel();
            if (State == PlayerState.Block) Defense.EndBlock();
        }

        private bool StartAttack(ComboInput input, ComboContext ctx)
        {
            if (State == PlayerState.Skill) Breathing.Cancel();
            if (State == PlayerState.Block) Defense.EndBlock();
            if (!Combat.TryAttack(input, ctx)) return false;
            State = PlayerState.Attack;
            _lastCombatTime = Time.time;
            Motor.SetDesiredVelocity(Vector3.zero);
            return true;
        }

        private bool StartSkill(int slot)
        {
            if (!Breathing.CanUse(slot, out var reason))
            {
                if (!string.IsNullOrEmpty(reason)) GameEvents.Notify(reason);
                return false;
            }
            CancelCurrentAction();
            if (!Breathing.TryUse(slot, LockOn.CurrentPoint)) return false;
            State = PlayerState.Skill;
            _lastCombatTime = Time.time;
            return true;
        }

        private bool StartDodge()
        {
            if (!Motor.Grounded && _airDodgeUsed) return false;
            if (!Stats.Stamina.TrySpend(Data.dodgeStaminaCost))
            {
                GameEvents.Notify("Not enough stamina");
                return false;
            }
            CancelCurrentAction();
            Vector3 dir = WorldMoveInput.sqrMagnitude > 0.04f ? WorldMoveInput.normalized : -transform.forward;
            bool airborne = !Motor.Grounded;
            if (airborne) _airDodgeUsed = true;
            Motor.ForceMove(dir * (Data.dodgeDistance / Data.dodgeDuration), Data.dodgeDuration, airborne, true);
            if (LockOn.CurrentPoint == null && WorldMoveInput.sqrMagnitude > 0.04f) Motor.Face(dir, 2000f);
            Defense.BeginDodge(Data.dodgeDuration);
            Animator.SetDodge(dir, Data.dodgeDuration);
            VFXLibrary.Spawn("dodge_wind", transform.position, Quaternion.LookRotation(dir), 1f, Breathing.CurrentElement);
            AfterimageSystem.Spawn(Rig, new Color(0.4f, 0.6f, 1.2f, 0.35f), 0.25f);
            Sfx.Play("dash", transform.position, 0.6f);
            CameraFX.FovPunch(3f);
            _dodgeEndTime = Time.time + Data.dodgeDuration;
            State = PlayerState.Dodge;
            return true;
        }

        private void EnterLocomotion()
        {
            if (State == PlayerState.Block) Defense.EndBlock();
            State = PlayerState.Locomotion;
        }

        private void OnSkillFinished(SkillData skill)
        {
            if (State == PlayerState.Skill) EnterLocomotion();
        }

        private void OnLanded(float fallSpeed)
        {
            _airDodgeUsed = false;
            if (fallSpeed > 9f)
            {
                VFXLibrary.Spawn("dust_puff", transform.position, Quaternion.identity);
                Sfx.Play("land", transform.position, 0.6f);
            }
        }

        // ------------------------------------------------------------------ interaction

        private void UpdateInteractable()
        {
            NearbyInteractable = null;
            float best = 2.6f * 2.6f;
            foreach (var i in InteractableRegistry.All)
            {
                if (i == null || !i.CanInteract) continue;
                float d = (i.Position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    NearbyInteractable = i;
                }
            }
        }

        private void OnInteract()
        {
            if (State == PlayerState.Dead || NearbyInteractable == null) return;
            NearbyInteractable.Interact(this);
            Sfx.Play2D("ui_confirm", 0.6f);
        }

        // ------------------------------------------------------------------ hits

        /// <summary>A hit dealt by the player landed (attacks, techniques, projectiles).</summary>
        public void NotifyHitLanded(HitData hit, HitResult result, float breathMultiplier = 1f)
        {
            _lastCombatTime = Time.time;
            if (result.Target != null && result.Target.GetComponent<Destructible>() != null) return;
            Stats.OnHitLanded(result, breathMultiplier);
        }

        /// <summary>The player received a hit (called by its Damageable).</summary>
        void IHitReactor.OnHitResolved(HitData hit, HitResult result)
        {
            if (State == PlayerState.Dead) return;
            _lastCombatTime = Time.time;
            CombatFeedback.PlayerDamaged(hit, result);
            if (result.Outcome == HitOutcome.Blocked)
            {
                Motor.AddKnockback(hit.Direction * 3f);
                Animator.PlayHit(HitReaction.Light, hit.Direction);
                return;
            }
            if (!result.Landed) return;
            Stats.OnDamageTaken(result.Damage);
            if (result.Killed) return;

            CancelCurrentAction();
            Animator.PlayHit(hit.Reaction, hit.Direction);
            switch (hit.Reaction)
            {
                case HitReaction.Launch:
                case HitReaction.Knockdown:
                    Motor.StopForced();
                    Motor.AddKnockback(hit.Direction * Mathf.Max(4f, hit.Knockback));
                    Motor.Launch(Mathf.Max(0.6f, hit.LaunchHeight * 0.5f));
                    Animator.SetKnockedDown(true);
                    State = PlayerState.Knockdown;
                    _stateTimer = Data.knockdownDuration;
                    break;
                case HitReaction.Knockback:
                case HitReaction.Heavy:
                    Motor.AddKnockback(hit.Direction * Mathf.Max(3f, hit.Knockback));
                    State = PlayerState.HitStun;
                    _stateTimer = Data.hitStunHeavy;
                    break;
                default:
                    Motor.AddKnockback(hit.Direction * Mathf.Max(1.5f, hit.Knockback * 0.6f));
                    State = PlayerState.HitStun;
                    _stateTimer = Data.hitStunLight;
                    break;
            }
        }

        private void OnDied(HitData hit)
        {
            CancelCurrentAction();
            State = PlayerState.Dead;
            Motor.ResetMotion();
            Animator.SetDead(true);
            LockOn.Release();
            if (TimeController.Instance != null) TimeController.Instance.SlowMotion(0.3f, 1.2f, "player_death", 0.4f);
            CameraFX.Zoom(0.7f, 2f);
            GameEvents.RaisePlayerDied();
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSecondsRealtime(Data.respawnDelay);
            if (SceneLoader.Instance != null) yield return SceneLoader.Instance.FadeOutIn(0.4f, Respawn);
            else Respawn();
        }

        /// <summary>Restores the player at the current checkpoint.</summary>
        public void Respawn()
        {
            Motor.ResetMotion();
            Motor.Teleport(SpawnPoint, SpawnRotation * Vector3.forward);
            Damageable.Health.Revive(1f);
            Stats.Stamina.Refill();
            Animator.SetDead(false);
            Animator.SetKnockedDown(false);
            State = PlayerState.Locomotion;
            _respawnInvulnerableUntil = Time.time + 2f;
            Breathing.Cooldowns.ResetAll();
            GameEvents.RaisePlayerRespawned();
        }

        public void SetCheckpoint(Vector3 position, Quaternion rotation)
        {
            SpawnPoint = position;
            SpawnRotation = rotation;
        }
    }

    /// <summary>Registry of interactables near the player.</summary>
    public static class InteractableRegistry
    {
        private static readonly System.Collections.Generic.List<IInteractable> Items = new System.Collections.Generic.List<IInteractable>();
        public static System.Collections.Generic.IReadOnlyList<IInteractable> All => Items;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Items.Clear();

        public static void Register(IInteractable i)
        {
            if (i != null && !Items.Contains(i)) Items.Add(i);
        }

        public static void Unregister(IInteractable i) => Items.Remove(i);
    }
}
