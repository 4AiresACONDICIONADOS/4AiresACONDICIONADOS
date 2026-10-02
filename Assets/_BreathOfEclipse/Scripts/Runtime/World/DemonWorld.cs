using System;
using System.Collections.Generic;
using BreathOfEclipse.AI;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.VFX;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;
using Object = UnityEngine.Object;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// The open-world mind of one demon (plugged into its <see cref="EnemyController"/>): it has a territory and
    /// a persistent identity, patrols at night, rests in the dark by day, stalks before it strikes (growls, a glint
    /// of eyes), gives up when dragged too far, never crosses into the village unless an attack is on, retreats from
    /// sunlight, and — badly hurt — may flee and be remembered (escaped, scarred, what breathing it saw).
    /// </summary>
    public sealed class DemonWorldAgent : MonoBehaviour, IEnemyWorldBrain
    {
        public EnemyController Enemy { get; private set; }
        public DemonRecordData Record { get; private set; }
        public DemonMode Mode { get; private set; } = DemonMode.Patrolling;
        public Vector3 Home { get; private set; }
        public string Sector { get; private set; }
        public float TerritoryRadius = 30f;
        /// <summary>Allowed inside the village fence (village attack, exceptional presence).</summary>
        public bool AllowVillage;
        /// <summary>Staged by an event (the event decides targets and outcome; the director does not despawn it).</summary>
        public string EventId;
        /// <summary>Raiders: march on this point (village attack) instead of patrolling.</summary>
        public Vector3? AssaultTarget;
        public string TargetName => Enemy == null ? "-" : Enemy.TargetOverride != null ? Enemy.TargetOverride.name : Enemy.Aware ? "Player" : "-";
        public bool Engaged => Enemy != null && Enemy.Aware && Enemy.IsAlive;
        /// <summary>Escaped this encounter and vanished.</summary>
        public bool Escaped { get; private set; }

        private WorldDemonDirector _director;
        private NpcRoute _route;
        private int _waypoint;
        private float _modeTime, _modeDuration, _sinceSight, _nextGlint, _nextGrowl;
        private bool _hitThisEncounter;
        private readonly HashSet<GameObject> _attackers = new HashSet<GameObject>();
        private float _attackersReset;

        public void Bind(WorldDemonDirector director, EnemyController enemy, DemonRecordData record, Vector3 home, string sector)
        {
            _director = director;
            Enemy = enemy;
            Record = record;
            Home = home;
            Sector = sector;
            Escaped = false;
            EventId = null;
            AllowVillage = false;
            AssaultTarget = null;
            _hitThisEncounter = false;
            _attackers.Clear();
            enemy.Home = home;
            enemy.WorldBrain = this;
            enemy.TargetOverride = null;
            enemy.TargetOverrideAlive = null;
            enemy.SetAware(false);
            // Demons that fought the player before come back sharper.
            enemy.AggressionBonus = Mathf.Min(0.3f, 0.1f * record.encounters);
            enemy.SpeedMultiplier = record.scarred ? 1.08f : 1f;
            SetMode(DemonMode.Patrolling);
            enemy.ChangeState(EnemyStateId.World);
        }

        public void SetMode(DemonMode mode)
        {
            Mode = mode;
            _modeTime = 0f;
            _route = null;
            _modeDuration = mode == DemonMode.Stalking ? UnityEngine.Random.Range(8f, 18f) : mode == DemonMode.Resting ? UnityEngine.Random.Range(10f, 25f) : 0f;
        }

        /// <summary>Hunts a villager / traveller / hunter (staged by events). The demon's attacks then hit them.</summary>
        public void Hunt(Transform target, Func<bool> alive)
        {
            if (Enemy == null || target == null) return;
            Enemy.TargetOverride = target;
            Enemy.TargetOverrideAlive = alive;
            Mode = DemonMode.HuntingNpc;
            Enemy.Alert();
        }

        /// <summary>Back to the player as the opponent (the hunted one is safe, dead or gone).</summary>
        public void ClearHunt()
        {
            if (Enemy == null) return;
            Enemy.TargetOverride = null;
            Enemy.TargetOverrideAlive = null;
            if (Mode == DemonMode.HuntingNpc) Mode = DemonMode.Patrolling;
        }

        // ------------------------------------------------------------------ IEnemyWorldBrain

        public void TickWorld(EnemyController e, float dt)
        {
            _modeTime += dt;
            var pc = PlayerController.Instance;
            Vector3 me = e.transform.position;
            float hour = _director.Hour;
            bool dayOut = WorldClock.Daylight(hour) > 0.7f && L.SectorAt(me.x, me.z) is SectorDef sd && sd.Danger < 3;

            switch (Mode)
            {
                case DemonMode.Fleeing:
                    Flee(e, pc);
                    return;
                case DemonMode.HuntingNpc:
                    if (e.TargetOverride == null || !e.PlayerAlive) ClearHunt();
                    else
                    {
                        e.Alert();
                        return;
                    }
                    break;
            }

            // Sunlight: back to the shadows.
            if (dayOut && Mode != DemonMode.Resting)
            {
                Retreat(e);
                return;
            }

            // Perception of the player (not inside the village unless allowed).
            if (pc != null && e.PlayerAlive && e.TargetIsPlayer && PlayerReachable(pc.transform.position))
            {
                float d = e.DistanceToPlayer;
                if (e.CanSeePlayer() && d < e.Data.detectRadius * 0.8f)
                {
                    e.Alert();
                    return;
                }
                if (Mode != DemonMode.Stalking && d < 42f && WorldClock.IsNight(hour) && _director.CanStalk(this))
                {
                    SetMode(DemonMode.Stalking);
                }
            }

            if (AssaultTarget.HasValue && Mode != DemonMode.Stalking)
            {
                if (_route == null || _waypoint >= _route.PointCount)
                {
                    var t = AssaultTarget.Value;
                    if (Flat(t - e.transform.position).magnitude < 4f) AssaultTarget = null;
                    else PlanTo(e, _director.Nav.Nearest(t.x, t.z), true);
                }
                Follow(e, e.Data.runSpeed * 0.8f);
                return;
            }

            switch (Mode)
            {
                case DemonMode.Stalking: Stalk(e, pc, dt); break;
                case DemonMode.Resting:
                    e.Motor.SetDesiredVelocity(Vector3.zero);
                    if (_modeTime > _modeDuration && !dayOut) SetMode(DemonMode.Patrolling);
                    break;
                default: Patrol(e); break;
            }
        }

        public void Supervise(EnemyController e, float dt)
        {
            if (Time.time >= _attackersReset)
            {
                _attackersReset = Time.time + 6f;
                _attackers.Clear();
            }
            if (!e.Aware || Mode == DemonMode.Fleeing) return;
            Mode = e.TargetIsPlayer ? DemonMode.Combat : DemonMode.HuntingNpc;
            Vector3 me = e.transform.position;

            // Territory / leash / interest.
            bool sees = e.CanSeePlayer();
            _sinceSight = sees ? 0f : _sinceSight + dt;
            float fromHome = Flat(me - Home).magnitude;
            var target = e.Player;
            bool targetInLight = target != null && InLight(target.position);
            if (DemonWorldRules.ShouldDisengage(fromHome, TerritoryRadius, _sinceSight, targetInLight) && EventId == null)
            {
                Disengage(e);
                return;
            }
            // The village ward: the fight does not follow the player inside unless the village is under attack.
            if (target != null && !AllowVillage && InVillage(target.position, -4f))
            {
                Disengage(e);
                return;
            }
            // Sunrise ends the night's hunt (except on cursed ground).
            var s = L.SectorAt(me.x, me.z);
            if (WorldClock.Daylight(_director.Hour) > 0.8f && (s == null || s.Danger < 3) && EventId == null)
            {
                StartFleeing(e);
            }
        }

        public void OnHit(EnemyController e, HitData hit, HitResult result)
        {
            if (hit.Attacker != null) _attackers.Add(hit.Attacker);
            if (!result.Landed) return;
            if (!_hitThisEncounter)
            {
                _hitThisEncounter = true;
                Record.encounters++;
            }
            Record.lastSeenHours = _director.Now;
            // What breathing did it see?
            if (hit.AttackerTeam == Team.Player && (hit.Category == DamageCategory.Skill || hit.Category == DamageCategory.Ultimate))
            {
                var pc = hit.Attacker != null ? hit.Attacker.GetComponent<PlayerController>() : null;
                string style = pc != null && pc.Breathing != null && pc.Breathing.Current != null ? pc.Breathing.Current.styleId : hit.Element.ToString().ToLowerInvariant();
                if (!string.IsNullOrEmpty(style) && !Record.observedBreathing.Contains(style)) Record.observedBreathing.Add(style);
            }
            // A hunter (or villager defending itself) drew its attention.
            if (hit.Attacker != null && hit.Attacker.GetComponent<PlayerController>() == null && e.TargetOverride == null)
            {
                e.TargetOverride = hit.Attacker.transform;
                var d = hit.Attacker.GetComponent<Damageable>();
                e.TargetOverrideAlive = d != null ? (Func<bool>)(() => d != null && d.IsAlive) : null;
            }
            if (result.Killed || !e.IsAlive) return;
            float hp = e.Damageable.Health.Normalized;
            Record.health = hp;
            if (Mode != DemonMode.Fleeing && EventId == null &&
                DemonWorldRules.ShouldFlee(hp, _attackers.Count, Record.escaped, e.IsBoss, UnityEngine.Random.value))
                StartFleeing(e);
        }

        // ------------------------------------------------------------------ behaviours

        private void Patrol(EnemyController e)
        {
            if (_route == null || _waypoint >= _route.PointCount)
            {
                if (_route != null && UnityEngine.Random.value < 0.35f)
                {
                    SetMode(DemonMode.Resting);
                    return;
                }
                PlanTo(e, _director.PatrolNode(this), false);
                if (_route == null)
                {
                    e.Motor.SetDesiredVelocity(Vector3.zero);
                    return;
                }
            }
            Follow(e, e.Data.walkSpeed * 0.75f);
        }

        private void Stalk(EnemyController e, PlayerController pc, float dt)
        {
            if (pc == null)
            {
                SetMode(DemonMode.Patrolling);
                return;
            }
            Vector3 me = e.transform.position;
            Vector3 p = pc.transform.position;
            Vector3 away = Flat(me - p);
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            float want = DemonWorldRules.StalkDistance(WorldClock.Daylight(_director.Hour), InLight(p));
            Vector3 goal = p + away.normalized * want;
            if (InVillage(goal, 6f) || Flat(goal - Home).magnitude > TerritoryRadius + 40f) goal = me; // hold the edge
            e.Motor.MoveTowards(goal, e.Data.walkSpeed * 0.9f, 1.5f);
            // Presence before the strike: a growl, a glint of eyes in the dark.
            if (Time.time >= _nextGrowl)
            {
                _nextGrowl = Time.time + UnityEngine.Random.Range(4f, 8f);
                e.PlaySfx(e.Data.voicePrefix + "_grunt", 0.5f);
            }
            if (Time.time >= _nextGlint && e.Rig != null && e.Rig.EyePoint != null)
            {
                _nextGlint = Time.time + UnityEngine.Random.Range(3f, 6f);
                VFXLibrary.Spawn("telegraph_glint", e.Rig.EyePoint.position, Quaternion.identity, 0.7f);
            }
            if (_modeTime > _modeDuration)
            {
                bool strike = !InVillage(p, 0f) && !InLight(p) && Flat(me - p).magnitude < 34f;
                if (strike) e.Alert();
                else SetMode(DemonMode.Patrolling);
            }
        }

        private void Retreat(EnemyController e)
        {
            // Walk (not run) back to the lair; vanish there when nobody sees it.
            if (_route == null) PlanTo(e, _director.LairNode(this), false);
            Follow(e, e.Data.walkSpeed);
            if (_route == null || _waypoint >= _route.PointCount)
            {
                if (!_director.VisibleToPlayer(e.transform.position)) _director.Despawn(this, false);
                else SetMode(DemonMode.Resting);
            }
        }

        private void StartFleeing(EnemyController e)
        {
            Mode = DemonMode.Fleeing;
            _modeTime = 0f;
            _route = null;
            e.SetAware(false);
            e.TargetOverride = null;
            e.ReleaseToken();
            e.PlaySfx(e.Data.voicePrefix + "_hurt", 0.9f);
            e.ChangeState(EnemyStateId.World);
        }

        private void Flee(EnemyController e, PlayerController pc)
        {
            if (_route == null)
            {
                // the lair farthest from the player
                PlanTo(e, _director.FleeNode(this, pc != null ? pc.transform.position : e.transform.position), true);
            }
            Follow(e, e.Data.runSpeed * 1.1f);
            float d = pc != null ? Flat(pc.transform.position - e.transform.position).magnitude : 999f;
            bool unseen = !_director.VisibleToPlayer(e.transform.position);
            if ((d > 32f && unseen) || d > 60f || _modeTime > 25f)
            {
                Record.escaped = true;
                Record.scarred = true;
                Record.health = e.Damageable.Health.Normalized;
                Record.lastSector = Sector;
                Record.lastSeenHours = _director.Now;
                Escaped = true;
                _director.Escaped(this);
            }
        }

        private void Disengage(EnemyController e)
        {
            e.SetAware(false);
            e.TargetOverride = null;
            e.TargetOverrideAlive = null;
            e.ReleaseToken();
            _sinceSight = 0f;
            _hitThisEncounter = false;
            SetMode(DemonMode.Patrolling);
            _route = null;
            PlanTo(e, _director.LairNode(this), false);
            e.ChangeState(EnemyStateId.World);
        }

        private void PlanTo(EnemyController e, NavGraph.Node node, bool run)
        {
            _waypoint = 0;
            _route = null;
            if (node == null) return;
            var g = _director.Nav;
            var from = g.Nearest(e.transform.position.x, e.transform.position.z);
            if (from == null) return;
            var route = NpcRoute.Plan(g, from.Index, e.transform.position.x, e.transform.position.z, node.Index, node.X, node.Z, true);
            if (route == null || route.PointCount < 2) return;
            _route = route;
            _waypoint = 1;
        }

        private void Follow(EnemyController e, float speed)
        {
            if (_route == null || _waypoint >= _route.PointCount)
            {
                e.Motor.SetDesiredVelocity(Vector3.zero);
                return;
            }
            _route.PointAt(_waypoint, out float x, out float z);
            var target = new Vector3(x, e.transform.position.y, z);
            // Demons stay out of the village (the ward) unless an attack is on.
            if (!AllowVillage && InVillage(target, 2f))
            {
                _route = null;
                e.Motor.SetDesiredVelocity(Vector3.zero);
                return;
            }
            e.Motor.MoveTowards(target, speed * e.SpeedMultiplier, 0.4f);
            if (Flat(target - e.transform.position).magnitude < 1.4f) _waypoint++;
        }

        private bool PlayerReachable(Vector3 p) => AllowVillage || !InVillage(p, -2f);

        public static bool InVillage(Vector3 p, float margin)
        {
            float dx = p.x - L.VillageX, dz = p.z - L.VillageZ;
            float r = L.VillageFenceRadius + margin;
            return dx * dx + dz * dz < r * r;
        }

        /// <summary>Near a lit lantern / campfire at night (demons dislike it).</summary>
        public static bool InLight(Vector3 p)
        {
            if (NightLight.Level < 0.3f) return true; // daylight
            foreach (var l in NightLight.All)
            {
                if (l == null || l.Enabled < 0.5f || l.Light == null) continue;
                if ((l.transform.position - p).sqrMagnitude < 6f * 6f) return true;
            }
            return false;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }

    /// <summary>
    /// Demons of the region: how many roam each loaded sector (time of day, danger, exceptional presence), where they
    /// may appear (hideouts and dark forest, never closer than 38 m, never in the player's view), who they are
    /// (persistent records: escaped demons come back), their presence cues before they show up, and pooling so the
    /// world never hitches. Also the API events use to stage attacks.
    /// </summary>
    public sealed class WorldDemonDirector : MonoBehaviour
    {
        public static WorldDemonDirector Instance { get; private set; }

        public IReadOnlyList<DemonWorldAgent> Active => _active;
        public NavGraph Nav => _world.Nav;
        public float Hour => _world.Time.Clock.Hour;
        public double Now => _world.Now;
        /// <summary>Exceptional demonic presence: more demons, stalking everywhere outside the village.</summary>
        public bool Presence { get; set; }
        /// <summary>Debug: no ambient spawns (events still stage theirs).</summary>
        public bool AmbientSpawns { get; set; } = true;
        public int LogicalCount
        {
            get
            {
                int n = 0;
                foreach (var r in _world.State.Data.demons) if (r.alive && !IsActive(r)) n++;
                return n;
            }
        }
        public int Spawned { get; private set; }
        public int Escapes { get; private set; }

        private LivingWorld _world;
        private EnemyData _nightspawn;
        private readonly List<DemonWorldAgent> _active = new List<DemonWorldAgent>();
        private readonly Stack<EnemyController> _pool = new Stack<EnemyController>();
        private readonly HashSet<EnemyController> _subscribed = new HashSet<EnemyController>();
        private float _nextCheck;
        private Transform _poolRoot;

        public static WorldDemonDirector Create(LivingWorld world)
        {
            var go = new GameObject("WorldDemonDirector");
            go.transform.SetParent(world.transform, false);
            var d = go.AddComponent<WorldDemonDirector>();
            d.Init(world);
            return d;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            WorldInteractables.DemonsNearQuery = null;
        }

        private void Init(LivingWorld world)
        {
            _world = world;
            var db = GameManager.Instance != null ? GameManager.Instance.Database : DefaultContent.Build();
            _nightspawn = db.FindEnemy("nightspawn");
            _poolRoot = new GameObject("DemonPool").transform;
            _poolRoot.SetParent(transform, false);
            WorldInteractables.DemonsNearQuery = (x, z, r) =>
            {
                foreach (var a in _active)
                    if (a != null && a.Enemy != null && a.Enemy.IsAlive && (new Vector2(a.transform.position.x - x, a.transform.position.z - z)).sqrMagnitude < r * r) return true;
                return false;
            };
            world.Time.TimeJumped += (from, to) =>
            {
                // Time passed: everyone not in a fight goes back to the logical world.
                for (int i = _active.Count - 1; i >= 0; i--)
                    if (_active[i] != null && !_active[i].Engaged && _active[i].EventId == null) Despawn(_active[i], false);
            };
        }

        /// <summary>Builds a few demons up front (behind the loading fade) so spawning later never hitches.</summary>
        public void Prewarm(int count)
        {
            if (_nightspawn == null) return;
            for (int i = 0; i < count; i++)
            {
                var e = EnemyFactory.Spawn(_nightspawn, new Vector3(0f, -400f - i * 5f, 0f), Quaternion.identity, false);
                e.gameObject.SetActive(false);
                e.transform.SetParent(_poolRoot, true);
                _pool.Push(e);
            }
        }

        private bool IsActive(DemonRecordData r)
        {
            foreach (var a in _active) if (a != null && a.Record == r) return true;
            return false;
        }

        // ------------------------------------------------------------------ population

        private bool _combatMusic;
        private float _calmSince;

        private void Update()
        {
            _active.RemoveAll(a => a == null);
            UpdatePresenceCues();
            UpdateCombatMusic();
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.75f;
            if (!AmbientSpawns || _nightspawn == null) return;
            Vector3 p = _world.PlayerPosition;

            // Despawn far / day-banished demons that are not fighting.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                if (a.EventId != null || a.Engaged) continue;
                float d = Flat(a.transform.position - p).magnitude;
                if (d > 135f && !VisibleToPlayer(a.transform.position)) Despawn(a, false);
            }

            // One spawn per check at most: the nearest under-populated loaded sector.
            float hour = Hour;
            SectorDef best = null;
            float bestD = float.MaxValue;
            foreach (var e in _world.Sectors.Entries)
            {
                var s = e.Def;
                if (s.SafeZone || e.Model.State != SectorLoadState.Loaded) continue;
                float d = s.Bounds.Distance(p.x, p.z);
                if (d > 110f) continue;
                int want = DemonWorldRules.DesiredPopulation(hour, s.Danger, Presence);
                if (CountIn(s.Id) >= want) continue;
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            if (best != null) TrySpawnIn(best, p);
        }

        private int CountIn(string sector)
        {
            int n = 0;
            foreach (var a in _active) if (a != null && a.Sector == sector && a.EventId == null) n++;
            return n;
        }

        private void TrySpawnIn(SectorDef s, Vector3 player)
        {
            // Hideouts first (caves, ruins, dark thickets), then dark forest ground.
            var candidates = new List<Vector3>();
            if (L.Hideouts.TryGetValue(s.Id, out var hides))
                foreach (var h in hides) candidates.Add(RegionTerrain.OnGround(h[0], h[1]));
            var rng = new System.Random(System.Environment.TickCount);
            for (int i = 0; i < 12; i++)
            {
                float x = Mathf.Lerp(s.Bounds.MinX + 8f, s.Bounds.MaxX - 8f, (float)rng.NextDouble());
                float z = Mathf.Lerp(s.Bounds.MinZ + 8f, s.Bounds.MaxZ - 8f, (float)rng.NextDouble());
                if (L.IsWater(x, z) || L.Slope(x, z) > 0.6f || NpcPlaces.Blocked(x, z, 3f)) continue;
                if (DemonWorldAgent.InVillage(new Vector3(x, 0f, z), 25f)) continue;
                candidates.Add(RegionTerrain.OnGround(x, z));
            }
            foreach (var c in candidates)
            {
                float d = Flat(c - player).magnitude;
                if (!DemonWorldRules.CanSpawnAt(d, VisibleToPlayer(c))) continue;
                if (DemonWorldAgent.InVillage(c, 20f)) continue;
                Spawn(c, s.Id, ReuseRecord(s.Id));
                return;
            }
        }

        /// <summary>Records of this territory that are not in the world now (escaped demons come back).</summary>
        private DemonRecordData ReuseRecord(string sector)
        {
            DemonRecordData pick = null;
            foreach (var r in _world.State.Data.demons)
            {
                if (!r.alive || IsActive(r) || r.archetype != "nightspawn") continue;
                if (r.lastSector != sector && !r.escaped) continue;
                if (pick == null || r.escaped && !pick.escaped) pick = r;
            }
            return pick;
        }

        /// <summary>Puts a demon in the world (pooled). Events pass their own spot (out of view) and id.</summary>
        public DemonWorldAgent Spawn(Vector3 position, string sector, DemonRecordData record = null, string eventId = null)
        {
            if (_nightspawn == null) return null;
            record ??= _world.State.NewDemon("nightspawn", sector, Now);
            EnemyController e;
            if (_pool.Count > 0)
            {
                e = _pool.Pop();
                e.transform.SetParent(null, true);
                var cc = e.Motor.Controller;
                if (cc != null) cc.enabled = false;
                e.transform.SetPositionAndRotation(position + Vector3.up * 0.1f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                if (cc != null) cc.enabled = true;
                e.gameObject.SetActive(true);
            }
            else e = EnemyFactory.Spawn(_nightspawn, position + Vector3.up * 0.1f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), false);

            // Wounds heal over the days since it was last seen.
            double days = Math.Max(0.0, (Now - record.lastSeenHours) / 24.0);
            float health = Mathf.Clamp01(record.health + (float)days * 0.35f);
            if (health < 0.35f) health = 0.35f;
            e.Damageable.Health.Revive(health);
            record.lastSector = sector;
            record.lastSeenHours = Now;
            record.escaped = false;

            var agent = e.GetComponent<DemonWorldAgent>() ?? e.gameObject.AddComponent<DemonWorldAgent>();
            agent.Bind(this, e, record, position, sector);
            agent.EventId = eventId;
            if (_subscribed.Add(e)) e.Damageable.Died += OnDemonDied;
            _active.Add(agent);
            Spawned++;
            return agent;
        }

        private void OnDemonDied(HitData hit)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                if (a == null || a.Enemy == null || a.Enemy.IsAlive) continue;
                a.Record.alive = false;
                a.Record.health = 0f;
                a.Record.lastSeenHours = Now;
                _world.State.AddFact("Demons_Slain", 1, Now);
                _active.RemoveAt(i);
            }
        }

        /// <summary>Back to the logical world (pooled, record kept).</summary>
        public void Despawn(DemonWorldAgent a, bool vanishFx)
        {
            if (a == null) return;
            _active.Remove(a);
            var e = a.Enemy;
            if (e == null) return;
            if (!e.IsAlive)
            {
                return; // dying demons dissolve on their own
            }
            a.Record.health = e.Damageable.Health.Normalized;
            a.Record.lastSeenHours = Now;
            if (vanishFx) VFXLibrary.Spawn("spawn_portal", e.transform.position, Quaternion.identity, 0.6f, Element.Dark);
            e.SetAware(false);
            e.TargetOverride = null;
            e.ReleaseToken();
            e.gameObject.SetActive(false);
            e.transform.SetParent(_poolRoot, true);
            _pool.Push(e);
        }

        public void Escaped(DemonWorldAgent a)
        {
            Escapes++;
            _world.State.AddFact("Demons_Escaped", 1, Now);
            Despawn(a, false);
        }

        public bool CanStalk(DemonWorldAgent a)
        {
            // Only one stalker at a time (it is about dread, not crowds) — unless the presence is exceptional.
            if (Presence) return true;
            foreach (var o in _active) if (o != null && o != a && o.Mode == DemonMode.Stalking) return false;
            return true;
        }

        // ------------------------------------------------------------------ navigation help

        public NavGraph.Node LairNode(DemonWorldAgent a)
        {
            NavGraph.Node best = null;
            float bestD = float.MaxValue;
            foreach (var n in Nav.Nodes)
            {
                if (!n.Id.StartsWith("lair_")) continue;
                float d = (n.X - a.Home.x) * (n.X - a.Home.x) + (n.Z - a.Home.z) * (n.Z - a.Home.z);
                if (d < bestD)
                {
                    bestD = d;
                    best = n;
                }
            }
            return best ?? Nav.Nearest(a.Home.x, a.Home.z);
        }

        public NavGraph.Node PatrolNode(DemonWorldAgent a)
        {
            var candidates = new List<NavGraph.Node>();
            foreach (var n in Nav.Nodes)
            {
                float d = (n.X - a.Home.x) * (n.X - a.Home.x) + (n.Z - a.Home.z) * (n.Z - a.Home.z);
                if (d > 55f * 55f || d < 6f * 6f || n.Edges.Count == 0) continue;
                if (DemonWorldAgent.InVillage(new Vector3(n.X, 0f, n.Z), 12f)) continue;
                candidates.Add(n);
            }
            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
        }

        public NavGraph.Node FleeNode(DemonWorldAgent a, Vector3 from)
        {
            NavGraph.Node best = null;
            float bestScore = float.MinValue;
            foreach (var n in Nav.Nodes)
            {
                if (!n.Id.StartsWith("lair_")) continue;
                float dHome = Mathf.Sqrt((n.X - a.Home.x) * (n.X - a.Home.x) + (n.Z - a.Home.z) * (n.Z - a.Home.z));
                if (dHome > 160f) continue;
                float dFrom = Mathf.Sqrt((n.X - from.x) * (n.X - from.x) + (n.Z - from.z) * (n.Z - from.z));
                float score = dFrom - dHome * 0.3f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }
            return best ?? LairNode(a);
        }

        /// <summary>In the camera's view and not hidden by terrain / buildings / trunks.</summary>
        public bool VisibleToPlayer(Vector3 p)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 head = p + Vector3.up * 1.2f;
            Vector3 vp = cam.WorldToViewportPoint(head);
            if (vp.z <= 0f || vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f) return false;
            // Night fog swallows distant shapes.
            if (vp.z > (NightLight.Level > 0.5f ? 55f : 110f)) return false;
            return !Physics.Linecast(cam.transform.position, head, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ presence

        /// <summary>Fights in the open world get the combat music; it fades out a few seconds after the last one.</summary>
        private void UpdateCombatMusic()
        {
            bool fighting = false;
            foreach (var a in _active)
                if (a != null && a.Engaged && a.Enemy.TargetIsPlayer) fighting = true;
            if (fighting)
            {
                _calmSince = Time.unscaledTime;
                if (!_combatMusic)
                {
                    _combatMusic = true;
                    Audio.Sfx.Music("combat", 1.2f);
                }
            }
            else if (_combatMusic && Time.unscaledTime - _calmSince > 6f)
            {
                _combatMusic = false;
                if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.StopMusic(3f);
            }
        }

        private void UpdatePresenceCues()
        {
            float threat = 0f;
            Vector3 p = _world.PlayerPosition;
            foreach (var a in _active)
            {
                if (a == null || a.Enemy == null || !a.Enemy.IsAlive) continue;
                float d = Flat(a.transform.position - p).magnitude;
                float w = Mathf.Clamp01(1f - d / 50f);
                if (a.Mode == DemonMode.Stalking) w *= 1.3f;
                threat = Mathf.Max(threat, w);
            }
            if (Presence) threat = Mathf.Max(threat, 0.45f);
            if (WorldAmbience.Instance != null) WorldAmbience.Instance.Threat = Mathf.MoveTowards(WorldAmbience.Instance.Threat, Mathf.Clamp01(threat * 0.8f), Time.deltaTime * 0.25f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
