using System.Collections.Generic;
using BreathOfEclipse.AI;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.VFX;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// A demon hunter of the frontier (Kaede, Rokuro, Sora): patrols at night, notices demons, draws the katana
    /// and fights them for real — closes in, cuts in short combos, rolls away from telegraphed strikes or blocks,
    /// protects villagers that are being hunted, gets hurt, wins, loses (falls injured, can be helped) or falls back
    /// when the fight turns. Sora, rarely, shows a single moonlit technique. Uses the light NPC body and the same
    /// hit pipeline as everyone else (team rules keep the player and hunters from hurting each other).
    /// </summary>
    public sealed class HunterBrain : MonoBehaviour
    {
        public const float MaxHealth = 150f;

        public NpcAgent Agent { get; private set; }
        public bool Fighting { get; private set; }
        public DemonWorldAgent Target { get; private set; }
        public string Status { get; private set; } = "patrol";
        /// <summary>Forced on duty (alarm, events) regardless of the hour.</summary>
        public bool OnCall { get; set; }
        public int Wins { get; private set; }
        public bool TeaserStyle => !string.IsNullOrEmpty(Agent.State.Def.BreathingTeaser);

        private NpcSystem _system;
        private float _nextScan, _nextSwing, _swingStart, _swingEnd, _hitAt, _rollUntil, _blockUntil, _fleeUntil;
        private bool _swinging, _hitDone, _decidedWindup, _teaserDone, _teaserSwing;
        private int _combo;
        private Vector3 _rollDir;
        private NpcPose _swingPose;
        private readonly List<IDamageable> _targets = new List<IDamageable>();
        private int _attackInstance = 1;

        public static HunterBrain Attach(NpcAgent agent, NpcSystem system)
        {
            var b = agent.gameObject.AddComponent<HunterBrain>();
            b.Agent = agent;
            b._system = system;
            agent.EnsureDamageable(MaxHealth);
            return b;
        }

        private bool OnDuty
        {
            get
            {
                if (OnCall) return true;
                var s = Agent.State;
                if (s.Indoors || !s.Alive || s.Injured) return false;
                float h = _system.Hour;
                return h >= 17.5f || h < 5f || s.Activity == NpcActivity.Patrol;
            }
        }

        private void Update()
        {
            var s = Agent.State;
            if (!s.Alive || s.Injured)
            {
                if (Fighting) EndFight("down");
                return;
            }
            if (!Fighting)
            {
                if (Time.time < _nextScan) return;
                _nextScan = Time.time + 0.5f;
                if (!Agent.Shown || !OnDuty || Agent.Controller != null) return;
                var demon = FindDemon(30f);
                if (demon != null) BeginFight(demon);
                return;
            }
            if (!Agent.Shown)
            {
                EndFight("out of sight");
                return;
            }
            FightTick(Time.deltaTime);
        }

        /// <summary>Nearest living demon in range; demons hunting villagers count from farther away.</summary>
        private DemonWorldAgent FindDemon(float range)
        {
            var dir = WorldDemonDirector.Instance;
            if (dir == null) return null;
            DemonWorldAgent best = null;
            float bestD = float.MaxValue;
            Vector3 me = transform.position;
            foreach (var a in dir.Active)
            {
                if (a == null || a.Enemy == null || !a.Enemy.IsAlive || a.Mode == DemonMode.Fleeing) continue;
                float d = Vector3.Distance(me, a.transform.position);
                float limit = a.Mode == DemonMode.HuntingNpc ? range + 15f : range;
                if (d > limit) continue;
                if (d < bestD)
                {
                    bestD = d;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>Sends this hunter at a demon (events: hunter vs demon, defending villagers).</summary>
        public void Engage(DemonWorldAgent demon)
        {
            if (demon == null || Fighting || !Agent.State.Alive || Agent.State.Injured) return;
            BeginFight(demon);
        }

        private void BeginFight(DemonWorldAgent demon)
        {
            Fighting = true;
            Target = demon;
            Status = "fight";
            _combo = 0;
            _teaserDone = false;
            Agent.TakeControl(this);
            Agent.EnsureDamageable(MaxHealth);
            if (Agent.Body != null)
            {
                Agent.Body.SetProp(NpcProp.Katana);
                Agent.Body.SetPose(NpcPose.SwordIdle, 0.15f);
            }
            // The demon turns on the hunter unless it is already fighting the player.
            if (demon.Enemy != null && !(demon.Enemy.Aware && demon.Enemy.TargetIsPlayer))
            {
                var d = Agent.Damageable;
                demon.Hunt(transform, () => d != null && d.IsAlive && Agent.State.Alive && !Agent.State.Injured);
            }
            Agent.Say(Random.value < 0.5f ? "¡Atrás, demonio!" : "¡Quédense detrás de mí!", 2.2f);
            _nextSwing = Time.time + 0.4f;
        }

        private void EndFight(string why)
        {
            if (!Fighting) return;
            Fighting = false;
            Status = why;
            _swinging = false;
            if (Agent.Damageable != null) Agent.Damageable.IncomingDamageMultiplier = 1f;
            if (Target != null && Target.Enemy != null && Target.Enemy.TargetOverride == transform) Target.ClearHunt();
            Target = null;
            if (Agent.Body != null) Agent.Body.SetProp(NpcProp.None);
            Agent.ReleaseControl(_system.Sim, _system.Hour);
        }

        // ------------------------------------------------------------------ fight

        private void FightTick(float dt)
        {
            var t = Target;
            if (t == null || t.Enemy == null || !t.Enemy.IsAlive || t.Escaped || !t.gameObject.activeInHierarchy)
            {
                if (t != null && t.Enemy != null && !t.Enemy.IsAlive)
                {
                    Wins++;
                    LivingWorld.Instance?.State.AddFact("Hunter_Kills_" + Agent.State.Def.Id, 1, LivingWorld.Instance.Now);
                    Agent.Say(Random.value < 0.5f ? "Se acabó." : "Uno menos...", 2.5f);
                }
                // another one close by?
                var next = FindDemon(18f);
                if (next != null && next != t) Target = next;
                else EndFight("won");
                return;
            }
            var body = Agent.Body;
            var me = transform.position;
            var demonPos = t.transform.position;
            Vector3 to = demonPos - me;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
            float hp = Agent.Damageable != null ? Agent.Damageable.Health.Normalized : 1f;

            // Falling back when the fight turns.
            if (hp < 0.25f && _fleeUntil <= 0f)
            {
                _fleeUntil = Time.time + 6f;
                Status = "fall back";
                Agent.Say("¡Retírense! ¡Es demasiado fuerte!", 2.5f);
                if (t.Enemy.TargetOverride == transform) t.ClearHunt();
            }
            if (_fleeUntil > 0f)
            {
                if (Time.time > _fleeUntil)
                {
                    _fleeUntil = 0f;
                    EndFight("fell back");
                    return;
                }
                var post = L.Place("hunter_post");
                Vector3 goal = post != null ? new Vector3(post.X, 0f, post.Z) : me - dir * 20f;
                MoveTowards(goal, 4.5f, dt);
                if (body != null) body.SetPose(NpcPose.Sprint, 0.15f);
                return;
            }

            // Rolling out of a strike.
            if (Time.time < _rollUntil)
            {
                Step(_rollDir * 5.5f * dt);
                Face(dir);
                return;
            }
            if (Time.time >= _blockUntil && Agent.Damageable != null) Agent.Damageable.IncomingDamageMultiplier = 1f;

            // Read the demon's windup: roll (60%) or block.
            var attack = t.Enemy.StateId == EnemyStateId.Attack ? t.Enemy.GetState<AttackState>(EnemyStateId.Attack) : null;
            bool windup = attack != null && attack.InWindup && t.Enemy.TargetOverride == transform;
            if (!windup) _decidedWindup = false;
            if (windup && !_decidedWindup && dist < 3.4f && !_swinging)
            {
                _decidedWindup = true;
                if (Random.value < 0.6f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * (Random.value < 0.5f ? 1f : -1f);
                    _rollDir = (side - dir * 0.6f).normalized;
                    _rollUntil = Time.time + 0.45f;
                    if (body != null) body.Restart(NpcPose.Roll);
                    Status = "dodge";
                    return;
                }
                _blockUntil = Time.time + 0.9f;
                if (Agent.Damageable != null) Agent.Damageable.IncomingDamageMultiplier = 0.25f;
                if (body != null) body.Restart(NpcPose.SwordBlock);
                Status = "block";
            }

            Face(dir);
            if (_swinging)
            {
                if (!_hitDone && Time.time >= _hitAt)
                {
                    _hitDone = true;
                    DealHit(dir);
                }
                if (Time.time >= _swingEnd) _swinging = false;
                else return;
            }
            if (Time.time < _blockUntil) return;

            if (dist > 2.1f)
            {
                MoveTowards(demonPos, dist > 6f ? 4f : 2.6f, dt);
                if (body != null) body.SetPose(dist > 6f ? NpcPose.Jog : NpcPose.Walk, 0.15f);
                Status = "close in";
                return;
            }
            if (Time.time >= _nextSwing) StartSwing();
            else if (body != null && !_swinging) body.SetPose(NpcPose.SwordIdle, 0.2f);
        }

        private void StartSwing()
        {
            var body = Agent.Body;
            _teaserSwing = TeaserStyle && !_teaserDone && _combo >= 2 && Random.value < 0.5f;
            _swingPose = _combo % 3 == 0 ? NpcPose.SlashA : _combo % 3 == 1 ? NpcPose.SlashB : NpcPose.SlashC;
            float len = body != null ? Mathf.Clamp(body.PoseLength(_swingPose), 0.45f, 1.1f) : 0.6f;
            _swingStart = Time.time;
            _swingEnd = Time.time + len * 0.8f;
            _hitAt = Time.time + len * 0.42f;
            _hitDone = false;
            _swinging = true;
            if (body != null) body.Restart(_swingPose);
            Sfx.Play("slash", transform.position + Vector3.up, 0.5f, 1.05f);
            _combo++;
            bool comboEnd = _combo % 3 == 0;
            _nextSwing = _swingEnd + (comboEnd ? Random.Range(0.8f, 1.4f) : 0.05f);
            Status = _teaserSwing ? "technique" : "attack";
            if (_teaserSwing)
            {
                _teaserDone = true;
                Agent.Say("Respiración de la luna...", 2f);
            }
        }

        private void DealHit(Vector3 dir)
        {
            Vector3 center = transform.position + Vector3.up * 1.05f + dir * 1.15f;
            float radius = _teaserSwing ? 2.4f : 1.15f;
            HitQuery.Sphere(center, radius, Team.Player, _targets);
            if (_teaserSwing)
            {
                VFXLibrary.Spawn("moon_crescent", transform.position + dir * 0.6f, Quaternion.LookRotation(dir), 1f, Element.Moon);
                Sfx.Play("moon", transform.position + Vector3.up, 0.7f);
            }
            _attackInstance++;
            foreach (var target in _targets)
            {
                if (target == null || target.Team != Team.Enemy) continue;
                var hit = new HitData
                {
                    Attacker = gameObject,
                    AttackerTeam = Team.Player,
                    SourceId = _teaserSwing ? "hunter_moonlight" : "hunter_slash",
                    AttackInstanceId = GetInstanceID() * 1000 + _attackInstance,
                    Category = _teaserSwing ? DamageCategory.Skill : DamageCategory.Light,
                    BaseDamage = 13f,
                    Multiplier = _teaserSwing ? 3f : _swingPose == NpcPose.SlashC ? 1.4f : 1f,
                    CritChance = 0.08f,
                    CritMultiplier = 1.5f,
                    Element = _teaserSwing ? Element.Moon : Element.None,
                    Reaction = _teaserSwing || _swingPose == NpcPose.SlashC ? HitReaction.Heavy : HitReaction.Light,
                    HitPoint = target.CenterPoint,
                    Direction = dir,
                    Knockback = _teaserSwing ? 5f : 2f,
                    PoiseDamage = _teaserSwing ? 40f : 12f,
                    HitStop = 0f,
                    CameraShake = 0f,
                    CanBreakObjects = false
                };
                var result = target.ReceiveHit(hit);
                if (result.Landed) Sfx.Play("hit", hit.HitPoint, 0.45f);
            }
        }

        private void MoveTowards(Vector3 goal, float speed, float dt)
        {
            Vector3 d = goal - transform.position;
            d.y = 0f;
            float m = d.magnitude;
            if (m < 0.05f) return;
            Step(d / m * Mathf.Min(m, speed * dt));
            Face(d / m);
        }

        private void Step(Vector3 delta)
        {
            Vector3 p = transform.position + delta;
            if (!NpcPlaces.Standable(p.x, p.z)) return;
            p.y = RegionTerrain.WalkHeight(p.x, p.z);
            transform.position = p;
        }

        private void Face(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * Time.deltaTime);
        }
    }
}
