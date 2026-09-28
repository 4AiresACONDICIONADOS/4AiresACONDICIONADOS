using System;
using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Breathing
{
    /// <summary>
    /// Runs a <see cref="SkillData"/> timeline: each phase plays its motion, moves the character, schedules
    /// VFX / SFX cues, camera cues, hits and projectiles. New techniques are pure data; exotic logic plugs in
    /// through <see cref="ISkillBehaviour"/>.
    /// </summary>
    public sealed class SkillExecutor
    {
        private struct Scheduled
        {
            public float Time;
            public Action Action;
        }

        public SkillData Skill { get; private set; }
        public BreathingStyleData Style { get; private set; }
        public Element Element { get; private set; }
        public ElementPalette Palette { get; private set; }
        public bool Running { get; private set; }
        public int PhaseIndex { get; private set; }
        public SkillPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public Transform Target { get; private set; }
        public PlayerController Player => _pc;
        public Vector3 PhaseStartPosition { get; private set; }

        private readonly PlayerController _pc;
        private readonly List<Scheduled> _scheduled = new List<Scheduled>();
        private readonly List<IDamageable> _buffer = new List<IDamageable>();
        private readonly List<ITargetable> _targetBuffer = new List<ITargetable>();
        private readonly Dictionary<HitSpec, HitRegistry> _registries = new Dictionary<HitSpec, HitRegistry>();
        private ISkillBehaviour _behaviour;
        private float _afterimageTimer;
        private Vector3 _weaveForward;
        private float _weaveSpeed;
        private bool _cinematicUsed;

        public event Action<SkillData> Finished;

        public SkillExecutor(PlayerController pc) => _pc = pc;

        public bool CanCancel
        {
            get
            {
                if (!Running || Phase == null || Skill.lockInput) return false;
                if (Phase.allowCancel) return true;
                return PhaseIndex == Skill.phases.Count - 1 && PhaseTime >= Phase.duration * 0.5f;
            }
        }

        public void Start(SkillData skill, BreathingStyleData style, Transform target)
        {
            Skill = skill;
            Style = style;
            Element = skill.element != Element.None ? skill.element : (style != null ? style.element : Element.None);
            Palette = ElementPalette.Get(Element);
            Target = target;
            Running = true;
            PhaseIndex = -1;
            _cinematicUsed = false;
            _registries.Clear();
            EnterNext();
        }

        public void Cancel()
        {
            if (!Running) return;
            ExitPhase();
            End();
            _pc.Animator.StopAction(0.1f);
        }

        public void Tick(float dt)
        {
            if (!Running || Phase == null) return;
            PhaseTime += dt;

            if (Phase.movement == SkillMoveMode.Weave)
            {
                float t = PhaseTime / Mathf.Max(0.01f, Phase.duration);
                Vector3 right = Vector3.Cross(Vector3.up, _weaveForward);
                Vector3 v = _weaveForward * _weaveSpeed + right * Mathf.Cos(t * Mathf.PI * 3f) * _weaveSpeed * 0.9f;
                _pc.Motor.ForceMove(v, 0.05f, false, true);
                _pc.Motor.Face(v, 1800f);
            }

            if (Phase.afterimages)
            {
                _afterimageTimer -= dt;
                if (_afterimageTimer <= 0f)
                {
                    _afterimageTimer = 0.035f;
                    AfterimageSystem.Spawn(_pc.Rig, new Color(Palette.Core.r * 0.5f, Palette.Core.g * 0.5f, Palette.Core.b * 0.5f, 0.55f), 0.3f);
                }
            }

            for (int i = 0; i < _scheduled.Count; i++)
            {
                if (_scheduled[i].Time > PhaseTime) continue;
                var action = _scheduled[i].Action;
                _scheduled.RemoveAt(i);
                i--;
                action?.Invoke();
                if (!Running) return;
            }

            _behaviour?.Tick(this, dt);
            if (Running && PhaseTime >= Phase.duration) EnterNext();
        }

        // ------------------------------------------------------------------ phases

        private void EnterNext()
        {
            ExitPhase();
            PhaseIndex++;
            if (PhaseIndex >= Skill.phases.Count)
            {
                End();
                return;
            }
            Phase = Skill.phases[PhaseIndex];
            PhaseTime = 0f;
            _scheduled.Clear();
            EnterPhase(Phase);
        }

        private void EnterPhase(SkillPhase p)
        {
            PhaseStartPosition = _pc.transform.position;
            RefreshTarget();
            FaceTarget(true);

            if (!string.IsNullOrEmpty(p.motionId)) _pc.Animator.PlayMotion(p.motionId, p.motionDuration > 0f ? p.motionDuration : p.duration, p.blendIn);
            if (p.invulnerable) _pc.Defense.SkillInvulnerable = true;
            if (p.suspendGravity) _pc.Motor.AirHang(p.duration + 0.05f);
            if (p.hideCharacter) _pc.Rig.SetVisible(false);
            _afterimageTimer = 0f;

            switch (p.trail)
            {
                case TrailMode.Off: _pc.SetTrails(false, false, 1f); break;
                case TrailMode.Sword: _pc.SetTrails(true, false, 1f); break;
                case TrailMode.SwordAndElement: _pc.SetTrails(true, true, 1.3f); break;
            }

            ApplyMovement(p);
            ApplyCamera(p.camera, p.duration);

            foreach (var cue in p.vfx)
            {
                var c = cue;
                Schedule(c.delay, () => SpawnCue(c));
            }
            foreach (var cue in p.sfx)
            {
                var c = cue;
                Schedule(c.delay, () => Sfx.Play(c.sfxId, _pc.transform.position + Vector3.up, c.volume, c.pitch));
            }

            _behaviour = SkillBehaviours.Get(p.customBehaviour);
            bool behaviourHandlesHits = _behaviour != null && _behaviour.HandlesHits;
            if (!behaviourHandlesHits)
            {
                foreach (var spec in p.hits)
                {
                    var s = spec;
                    if (s.detached)
                    {
                        SpawnHitZone(s);
                        continue;
                    }
                    var registry = new HitRegistry();
                    registry.Begin(Mathf.Max(1, s.hitCount), Mathf.Max(0.01f, s.hitInterval * 0.8f));
                    _registries[s] = registry;
                    int count = Mathf.Max(1, s.hitCount);
                    for (int k = 0; k < count; k++)
                    {
                        int index = k;
                        Schedule(s.delay + k * s.hitInterval, () => ResolveHit(s, index));
                    }
                }
            }

            if (p.projectile != null && p.projectile.enabled)
            {
                var proj = p.projectile;
                Schedule(proj.delay, () => SpawnProjectiles(proj));
            }

            if (p.buff != null && p.buff.enabled)
            {
                _pc.Stats.Buffs.Apply(Skill.skillId, p.buff.stat, p.buff.magnitude, p.buff.duration);
                GameEvents.Notify($"{p.buff.stat} +{Mathf.RoundToInt(p.buff.magnitude * 100f)}% ({p.buff.duration:0}s)");
            }

            _behaviour?.Enter(this, p);
        }

        private void ExitPhase()
        {
            if (Phase == null) return;
            _behaviour?.Exit(this);
            _behaviour = null;
            if (Phase.invulnerable) _pc.Defense.SkillInvulnerable = false;
            if (Phase.hideCharacter) _pc.Rig.SetVisible(true);
            if (Phase.movement == SkillMoveMode.Rise || Phase.movement == SkillMoveMode.Weave || Phase.movement == SkillMoveMode.DashToTarget ||
                Phase.movement == SkillMoveMode.DashForward || Phase.movement == SkillMoveMode.Leap || Phase.movement == SkillMoveMode.Retreat)
                _pc.Motor.StopForced();
        }

        private void End()
        {
            Running = false;
            Phase = null;
            _scheduled.Clear();
            _pc.Defense.SkillInvulnerable = false;
            _pc.Rig.SetVisible(true);
            _pc.SetTrails(false, false, 1f);
            if (_cinematicUsed && CameraRig.Instance != null) CameraRig.Instance.EndCinematic();
            var post = CameraFX.Post;
            if (post != null)
            {
                post.ReleaseSaturation("skill");
                post.SetCinematicDof(0f, 5f);
            }
            if (Skill != null && Skill.endsAirborne)
            {
                _pc.Motor.AirHang(0.7f);
            }
            else
            {
                _pc.Animator.StopAction(0.2f);
            }
            Finished?.Invoke(Skill);
        }

        private void Schedule(float time, Action action) => _scheduled.Add(new Scheduled { Time = Mathf.Max(0f, time), Action = action });

        // ------------------------------------------------------------------ targeting

        public void RefreshTarget()
        {
            if (Target != null)
            {
                var dmg = Target.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive) return;
            }
            Target = _pc.LockOn.CurrentPoint;
            if (Target == null)
            {
                var t = TargetRegistry.Closest(_pc.transform.position, Skill.autoTargetRange, _pc.transform.forward, 8f);
                Target = t != null ? t.LockOnPoint : null;
            }
        }

        public void FaceTarget(bool snap)
        {
            if (Target == null) return;
            Vector3 d = Target.position - _pc.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) _pc.Motor.Face(d, 2400f, snap);
        }

        public Vector3 Forward
        {
            get
            {
                Vector3 f = _pc.transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
            }
        }

        // ------------------------------------------------------------------ movement

        private void ApplyMovement(SkillPhase p)
        {
            var motor = _pc.Motor;
            float d = Mathf.Max(0.01f, p.duration);
            Vector3 fwd = Forward;
            switch (p.movement)
            {
                case SkillMoveMode.DashForward:
                    motor.ForceMove(fwd * (p.moveDistance / d), d, !motor.Grounded, true);
                    break;
                case SkillMoveMode.DashToTarget:
                {
                    float dist = p.moveDistance > 0f ? p.moveDistance : 8f;
                    if (Target != null)
                    {
                        Vector3 to = Target.position - _pc.transform.position;
                        to.y = 0f;
                        dist = Mathf.Clamp(to.magnitude - 1.4f, 0f, Mathf.Max(p.moveDistance, 12f));
                        if (to.sqrMagnitude > 0.01f) fwd = to.normalized;
                    }
                    motor.ForceMove(fwd * (dist / d), d, !motor.Grounded, false);
                    break;
                }
                case SkillMoveMode.TeleportBehindTarget:
                    FlashStep(p);
                    break;
                case SkillMoveMode.Leap:
                    motor.ForceMove(fwd * (p.moveDistance / d), d * 0.9f, false, true);
                    motor.Launch(Mathf.Max(0.5f, p.moveHeight));
                    break;
                case SkillMoveMode.Rise:
                    motor.ForceMove(fwd * (p.moveDistance / d) + Vector3.up * (p.moveHeight / d), d, true, true);
                    break;
                case SkillMoveMode.Hover:
                    motor.ForceMove(Vector3.zero, d, true, false);
                    motor.AirHang(d + 0.05f);
                    break;
                case SkillMoveMode.Plunge:
                    motor.CancelAirHang();
                    motor.SetVerticalVelocity(-30f);
                    break;
                case SkillMoveMode.Retreat:
                    motor.ForceMove(-fwd * (p.moveDistance / d), d, false, true);
                    break;
                case SkillMoveMode.Weave:
                    _weaveForward = fwd;
                    _weaveSpeed = p.moveDistance / d;
                    break;
            }
        }

        /// <summary>Flash step: instantly appears behind (or past) the target, leaving a luminous path and afterimages.</summary>
        public void FlashStep(SkillPhase p, Transform explicitTarget = null)
        {
            var target = explicitTarget != null ? explicitTarget : Target;
            Vector3 from = _pc.transform.position;
            Vector3 dir = Forward;
            Vector3 to;
            if (target != null)
            {
                Vector3 flat = target.position - from;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f) dir = flat.normalized;
                Vector3 behind = new Vector3(target.position.x, from.y, target.position.z) + dir * (1.8f + Mathf.Max(0f, p.moveDistance * 0.1f));
                to = behind;
            }
            else
            {
                to = from + dir * Mathf.Max(3f, p.moveDistance);
            }
            // Never teleport into walls.
            Vector3 up = Vector3.up * 1f;
            if (Physics.SphereCast(from + up, 0.35f, (to - from).normalized, out var wall, Vector3.Distance(from, to), Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                to = wall.point - (to - from).normalized * 0.5f - up;
            if (HitQuery.GroundPoint(to + Vector3.up * 2f, out var ground, out _)) to.y = ground.y;

            VFXLibrary.SpawnBetween("thunder_path", from, to, Element, 1f);
            AfterimageSystem.SpawnAlongPath(_pc.Rig, from, to, 6, new Color(Palette.Core.r * 0.5f, Palette.Core.g * 0.5f, Palette.Core.b * 0.5f, 0.6f), 0.5f);
            VFXLibrary.Spawn("thunder_blink", from, Quaternion.identity, 1f, Element);
            _pc.Motor.Teleport(to, target != null ? -dir : dir);
            VFXLibrary.Spawn("thunder_blink", to, Quaternion.identity, 1f, Element);
            PhaseStartPosition = from;
        }

        // ------------------------------------------------------------------ presentation

        private void ApplyCamera(CameraCue c, float duration)
        {
            if (c == null) return;
            if (c.shake > 0f) CameraFX.Shake(c.shake);
            if (c.fovPunch != 0f) CameraFX.FovPunch(c.fovPunch);
            if (c.zoom > 0f && !Mathf.Approximately(c.zoom, 1f)) CameraFX.Zoom(c.zoom, duration, 0.08f, 0.3f);
            if (c.slowMoDuration > 0f && c.slowMoScale < 1f && TimeController.Instance != null)
                TimeController.Instance.SlowMotion(c.slowMoScale, c.slowMoDuration, "skill_" + Skill.skillId, 0.1f);
            var post = CameraFX.Post;
            if (post != null)
            {
                if (c.saturation < 0f) post.HoldSaturation("skill", c.saturation, duration);
                if (c.chromatic > 0f) post.PulseChromatic(c.chromatic);
                if (c.lensDistortion != 0f) post.PulseLensDistortion(c.lensDistortion);
                if (c.bloomBoost > 0f) post.PulseBloom(c.bloomBoost);
                if (c.radialBlur) post.PulseMotionBlur(0.6f);
            }
            if (c.speedLines) ScreenFX.SpeedLines(0.85f, duration, Palette.Core);
            if (c.flashFrame)
            {
                Vector3 at = Target != null ? Target.position : _pc.transform.position + Vector3.up * 1.2f;
                FlashFrameSystem.Trigger(at, Palette.Core, Skill.tier == SkillTier.Ultimate ? 3 : 2);
            }
            if (c.shot != CinematicShot.None && CameraRig.Instance != null && (Skill.cinematic || Skill.tier == SkillTier.Ultimate))
            {
                if (CameraRig.Instance.PlayCinematicShot(c.shot, c.shotDistance, c.shotHeight, Target, duration))
                {
                    _cinematicUsed = true;
                    if (post != null) post.SetCinematicDof(0.8f, c.shotDistance);
                }
            }
        }

        private void SpawnCue(VFXCue cue)
        {
            if (string.IsNullOrEmpty(cue.vfxId)) return;
            var t = _pc.transform;
            Quaternion facing = Quaternion.LookRotation(Forward);
            Quaternion rot = facing * Quaternion.Euler(cue.rotation);
            Vector3 pos;
            Transform follow = null;
            switch (cue.anchor)
            {
                case VFXAnchor.Sword:
                {
                    var b = _pc.Animator.WeaponBase;
                    pos = b.TransformPoint(cue.offset);
                    rot = b.rotation * Quaternion.Euler(cue.rotation);
                    if (cue.follow) follow = b;
                    break;
                }
                case VFXAnchor.SwordTip:
                {
                    var tip = _pc.Animator.WeaponTip;
                    pos = tip.TransformPoint(cue.offset);
                    rot = tip.rotation * Quaternion.Euler(cue.rotation);
                    if (cue.follow) follow = tip;
                    break;
                }
                case VFXAnchor.Ground:
                    HitQuery.GroundPoint(t.position, out pos, out _);
                    pos += facing * cue.offset;
                    if (cue.follow) follow = t;
                    break;
                case VFXAnchor.Target:
                    pos = (Target != null ? Target.position : t.position + Forward * 3f + Vector3.up * 1.2f) + facing * cue.offset;
                    if (cue.follow && Target != null) follow = Target;
                    break;
                case VFXAnchor.TargetGround:
                {
                    Vector3 basePos = Target != null ? Target.position : t.position + Forward * 3f;
                    HitQuery.GroundPoint(basePos, out pos, out _);
                    pos += facing * cue.offset;
                    break;
                }
                case VFXAnchor.InFront:
                    pos = t.position + facing * cue.offset;
                    break;
                case VFXAnchor.Path:
                    VFXLibrary.SpawnBetween(cue.vfxId, PhaseStartPosition + Vector3.up * cue.offset.y, t.position + Vector3.up * cue.offset.y, Element, cue.scale, cue.lifetime);
                    return;
                case VFXAnchor.SkyAboveTarget:
                {
                    Vector3 basePos = Target != null ? Target.position : t.position + Forward * 4f;
                    HitQuery.GroundPoint(basePos, out pos, out _);
                    pos += Vector3.up * cue.offset.y + facing * new Vector3(cue.offset.x, 0f, cue.offset.z);
                    break;
                }
                default:
                    pos = t.position + facing * cue.offset;
                    if (cue.follow) follow = t;
                    break;
            }
            VFXLibrary.Spawn(cue.vfxId, pos, rot, cue.scale <= 0f ? 1f : cue.scale, Element, follow, cue.lifetime);
        }

        // ------------------------------------------------------------------ hits

        public void ResolveHit(HitSpec spec, int index)
        {
            var t = _pc.transform;
            Vector3 origin = t.position + Vector3.up * 1f;
            Vector3 fwd = Forward;
            switch (spec.shape)
            {
                case HitShape.SphereAroundSelf:
                    HitQuery.Sphere(origin, spec.radius, Team.Player, _buffer);
                    break;
                case HitShape.SphereInFront:
                    HitQuery.Sphere(origin + fwd * spec.range, spec.radius, Team.Player, _buffer);
                    break;
                case HitShape.CapsuleForward:
                    HitQuery.Capsule(origin, origin + fwd * spec.range, spec.radius, Team.Player, _buffer);
                    break;
                case HitShape.Cone:
                    HitQuery.Cone(origin, fwd, spec.range, spec.angle, Team.Player, _buffer);
                    break;
                case HitShape.AtTarget:
                    HitQuery.Sphere(Target != null ? Target.position : origin + fwd * spec.range, spec.radius, Team.Player, _buffer);
                    break;
                case HitShape.AlongPath:
                    HitQuery.Capsule(PhaseStartPosition + Vector3.up, t.position + Vector3.up, spec.radius, Team.Player, _buffer);
                    break;
                case HitShape.ChainLightning:
                    ChainLightning(spec);
                    return;
                case HitShape.ScatterAroundTarget:
                    Scatter(spec);
                    return;
                default:
                    return;
            }
            ApplyToBuffer(spec, index);
        }

        private void ApplyToBuffer(HitSpec spec, int index)
        {
            int applied = 0;
            _registries.TryGetValue(spec, out var registry);
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (spec.maxTargets > 0 && applied >= spec.maxTargets) break;
                var target = _buffer[i];
                if (registry != null)
                {
                    if (!registry.CanHit(target, Time.time)) continue;
                    registry.Register(target, Time.time);
                }
                ApplyHit(spec, target, target.CenterPoint, index);
                applied++;
            }
        }

        public HitResult ApplyHit(HitSpec spec, IDamageable target, Vector3 point, int index = 0)
        {
            var hit = BuildHit(spec, target, point);
            var result = target.ReceiveHit(hit);
            if (result.Outcome == HitOutcome.Ignored) return result;
            _pc.NotifyHitLanded(hit, result, Skill.tier == SkillTier.Ultimate ? 0.4f : 0.7f);
            var feel = new HitFeel
            {
                CameraShake = spec.cameraShake,
                HitSfx = spec.hitSfx,
                ImpactVfx = spec.impactVfx,
                Zoom = spec.isFinisher ? 0.8f : 1f,
                ZoomDuration = 0.35f
            };
            CombatFeedback.PlayerHitLanded(hit, result, feel, Element);
            if (spec.statusDuration > 0f && Element == Element.Fire && result.Landed && target.IsAlive)
                VFXLibrary.Spawn("burn_status", target.Transform.position, Quaternion.identity, 1f, Element.Fire, target.Transform, spec.statusDuration);
            return result;
        }

        public HitData BuildHit(HitSpec spec, IDamageable target, Vector3 point)
        {
            Vector3 dir = (target != null ? target.CenterPoint : point) - _pc.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Forward;
            var buffs = _pc.Stats.Buffs;
            var weapon = _pc.Weapon;
            return new HitData
            {
                Attacker = _pc.gameObject,
                AttackerTeam = Team.Player,
                SourceId = Skill.skillId,
                Category = Skill.tier == SkillTier.Ultimate ? DamageCategory.Ultimate : DamageCategory.Skill,
                BaseDamage = weapon.baseDamage,
                Multiplier = spec.damageMultiplier * Skill.damageScale,
                BonusPercent = buffs.GetBonus(StatType.Damage),
                CritChance = weapon.critChance + buffs.GetBonus(StatType.CritChance),
                CritMultiplier = weapon.critMultiplier,
                ForceCritical = spec.forceCritical,
                Element = Element,
                Reaction = spec.reaction,
                HitPoint = point,
                Direction = dir.normalized,
                Knockback = spec.knockback,
                LaunchHeight = spec.launchHeight,
                PoiseDamage = spec.poiseDamage,
                HitStop = spec.hitStop,
                CameraShake = spec.cameraShake,
                CanBreakObjects = spec.canBreakObjects,
                IsFinisher = spec.isFinisher,
                FlashFrame = spec.flashFrame,
                StatusDuration = spec.statusDuration,
                PullTarget = spec.pullStrength > 0f ? _pc.transform.position + Forward * 1.6f : Vector3.zero,
                PullStrength = spec.pullStrength,
                AirHang = spec.airHang
            };
        }

        /// <summary>Independent lingering hit zone positioned by the spec's shape at phase start.</summary>
        private void SpawnHitZone(HitSpec spec)
        {
            Vector3 origin = _pc.transform.position + Vector3.up;
            Vector3 center;
            switch (spec.shape)
            {
                case HitShape.SphereAroundSelf: center = origin; break;
                case HitShape.AtTarget: center = Target != null ? Target.position : origin + Forward * spec.range; break;
                default: center = origin + Forward * spec.range; break;
            }
            var template = BuildHit(spec, null, center);
            HitZone.Spawn(center, spec.radius, spec.delay, Mathf.Max(1, spec.hitCount), spec.hitInterval, template, spec, OnZoneHit);
        }

        private void OnZoneHit(HitData hit, HitResult result, HitSpec spec)
        {
            if (_pc == null) return;
            _pc.NotifyHitLanded(hit, result, 0.5f);
            CombatFeedback.PlayerHitLanded(hit, result, new HitFeel { CameraShake = spec.cameraShake * 0.6f, HitSfx = spec.hitSfx, ImpactVfx = spec.impactVfx }, hit.Element);
        }

        private void ChainLightning(HitSpec spec)
        {
            Vector3 from = _pc.Animator.WeaponTip.position;
            var visited = new HashSet<IDamageable>();
            IDamageable current = null;
            if (Target != null) current = Target.GetComponentInParent<IDamageable>();
            int jumps = Mathf.Max(1, spec.maxTargets > 0 ? spec.maxTargets : 4);
            for (int j = 0; j < jumps; j++)
            {
                if (current == null || visited.Contains(current))
                {
                    current = null;
                    HitQuery.Sphere(from, spec.radius, Team.Player, _buffer);
                    float best = float.MaxValue;
                    foreach (var d in _buffer)
                    {
                        if (visited.Contains(d) || d.Team != Team.Enemy) continue;
                        float dist = (d.CenterPoint - from).sqrMagnitude;
                        if (dist < best)
                        {
                            best = dist;
                            current = d;
                        }
                    }
                }
                if (current == null) break;
                visited.Add(current);
                Vector3 to = current.CenterPoint;
                VFXLibrary.SpawnBetween("thunder_link", from, to, Element, 1f);
                ApplyHit(spec, current, to, j);
                from = to;
                current = null;
            }
            if (visited.Count == 0)
            {
                // No enemy: discharge forward so the technique still reads.
                VFXLibrary.SpawnBetween("thunder_link", from, from + Forward * spec.range, Element, 1f);
            }
        }

        private void Scatter(HitSpec spec)
        {
            Vector3 center = Target != null ? Target.position : _pc.transform.position + Forward * spec.range;
            TargetRegistry.InRadius(center, spec.radius, _targetBuffer);
            int strikes = Mathf.Max(1, spec.maxTargets > 0 ? spec.maxTargets : 5);
            for (int i = 0; i < strikes; i++)
            {
                Vector3 point;
                IDamageable victim = null;
                if (i < _targetBuffer.Count && _targetBuffer[i].Damageable != null)
                {
                    victim = _targetBuffer[i].Damageable;
                    point = victim.Transform.position;
                }
                else
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * spec.radius;
                    point = center + new Vector3(r.x, 0f, r.y);
                }
                HitQuery.GroundPoint(point + Vector3.up * 2f, out var ground, out _);
                float delay = i * 0.08f;
                int strikeIndex = i;
                var capturedVictim = victim;
                var capturedGround = ground;
                Schedule(PhaseTime + delay, () =>
                {
                    if (!string.IsNullOrEmpty(spec.impactVfx)) VFXLibrary.Spawn(spec.impactVfx, capturedGround, Quaternion.identity, 1f, Element);
                    HitQuery.Sphere(capturedGround + Vector3.up, 1.8f, Team.Player, _buffer);
                    foreach (var d in _buffer)
                    {
                        if (capturedVictim != null && d != capturedVictim && d.Team == Team.Enemy) continue;
                        ApplyHit(spec, d, d.CenterPoint, strikeIndex);
                    }
                    Sfx.Play(Element == Element.Thunder ? "thunder_big" : "explosion", capturedGround, 0.6f);
                });
            }
        }

        private void SpawnProjectiles(ProjectileSpec spec)
        {
            int count = Mathf.Max(1, spec.count);
            Vector3 origin = _pc.transform.position + Vector3.up * (spec.heightOffset - 1.1f) + Forward * 0.8f;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-spec.spreadAngle * 0.5f, spec.spreadAngle * 0.5f, i / (float)(count - 1));
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Forward;
                var template = new HitData
                {
                    Attacker = _pc.gameObject,
                    AttackerTeam = Team.Player,
                    SourceId = Skill.skillId,
                    Category = DamageCategory.Projectile,
                    BaseDamage = _pc.Weapon.baseDamage,
                    Multiplier = spec.damageMultiplier * Skill.damageScale,
                    BonusPercent = _pc.Stats.Buffs.GetBonus(StatType.Damage),
                    CritChance = _pc.Weapon.critChance,
                    CritMultiplier = _pc.Weapon.critMultiplier,
                    Element = Element,
                    Reaction = spec.reaction,
                    Knockback = spec.knockback,
                    LaunchHeight = spec.launchHeight,
                    PoiseDamage = spec.poiseDamage,
                    HitStop = HitStopManager.Skill,
                    CanBreakObjects = true
                };
                Projectile.Spawn(spec.vfxId, origin, dir, spec.speed, spec.lifetime, spec.radius, spec.pierce,
                    spec.homing ? Target : null, Team.Player, template, OnProjectileHit, spec.impactVfx, Element);
            }
        }

        private void OnProjectileHit(HitData hit, HitResult result)
        {
            _pc.NotifyHitLanded(hit, result, 0.6f);
            CombatFeedback.PlayerHitLanded(hit, result, new HitFeel { CameraShake = 0.15f, HitSfx = "hit" }, Element);
        }
    }
}
