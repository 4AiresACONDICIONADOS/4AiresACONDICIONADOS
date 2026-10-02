using System;
using System.Collections.Generic;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// A person an event brings into the world for a while (a caravan merchant, a porter, a mother and her son on
    /// the run). Light NPC body, can be hurt by demons (never by the player), cowers, runs to safety along the nav
    /// graph, falls injured (no gore: the hurt are helped up, the worst outcome is "missing"), can be helped.
    /// </summary>
    public sealed class EventActor : MonoBehaviour, IInteractable, IHitReactor
    {
        public enum Act { Cower, Run, Injured, Safe, Gone }

        public string DisplayName;
        public Act Mode { get; private set; } = Act.Cower;
        public NpcBody Body { get; private set; }
        public Damageable Damageable { get; private set; }
        public bool Helped { get; private set; }
        public bool Alive => Damageable != null && Damageable.IsAlive;
        public bool Standing => Alive && Mode != Act.Injured && Mode != Act.Gone;

        private NpcRoute _route;
        private float _travelled, _speed = 3.6f;
        private Vector3 _threat;
        private string _destination;

        public static EventActor Create(Transform parent, string id, string name, bool female, bool child, int seed, Vector3 pos, float yaw, VillagerOutfit outfit)
        {
            var go = new GameObject("EventActor_" + id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.layer = Layers.Npc;
            var a = go.AddComponent<EventActor>();
            a.DisplayName = name;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            float h = child ? 1.25f : female ? 1.62f : 1.74f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = child ? 0.22f : 0.3f;
            col.height = h;
            col.center = Vector3.up * h * 0.5f;
            var lockOn = new GameObject("LockOn").transform;
            lockOn.SetParent(go.transform, false);
            lockOn.localPosition = Vector3.up * h * 0.72f;
            a.Damageable = go.AddComponent<Damageable>();
            a.Damageable.Configure(Team.Player, child ? 45f : 70f, lockOn, 1f);
            a.Damageable.AddReactor(a);
            var def = new NpcDef { Id = id, Name = name, Role = NpcRole.Traveler, Female = female, Child = child, LookSeed = seed };
            a.Body = NpcBody.Build(go.transform, def, outfit, h);
            if (a.Body != null) a.Body.SetPose(NpcPose.Cower, 0.01f);
            return a;
        }

        public void CowerFrom(Vector3 threat)
        {
            _threat = threat;
            if (Mode == Act.Run || Mode == Act.Injured || Mode == Act.Gone) return;
            Mode = Act.Cower;
        }

        /// <summary>Runs to a place (village gate…) along the nav graph.</summary>
        public void RunTo(string place, float speed = 3.6f)
        {
            if (!Alive || Mode == Act.Injured) return;
            var g = LivingWorld.Instance != null ? LivingWorld.Instance.Nav : null;
            var node = g?.Get(place);
            var from = g?.Nearest(transform.position.x, transform.position.z);
            if (node == null || from == null) return;
            _route = NpcRoute.Plan(g, from.Index, transform.position.x, transform.position.z, node.Index, node.X, node.Z);
            _travelled = 0f;
            _speed = speed;
            _destination = place;
            Mode = _route != null ? Act.Run : Act.Cower;
        }

        public void Vanish()
        {
            Mode = Act.Gone;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Mode == Act.Gone) return;
            if (!Alive || Mode == Act.Injured)
            {
                Body?.SetPose(NpcPose.Lie, 0.2f);
                Tick(dt);
                return;
            }
            switch (Mode)
            {
                case Act.Run:
                    if (_route == null)
                    {
                        Mode = Act.Safe;
                        break;
                    }
                    _travelled += _speed * dt;
                    _route.Sample(_travelled, out float x, out float z, out float heading);
                    transform.SetPositionAndRotation(new Vector3(x, RegionTerrain.WalkHeight(x, z), z), Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, heading, 0f), 540f * dt));
                    Body?.SetMoveSpeed(_speed);
                    Body?.SetPose(_speed > 2.5f ? NpcPose.Sprint : NpcPose.Walk, 0.2f);
                    if (_route.Done) Mode = Act.Safe;
                    break;
                case Act.Cower:
                    var d = _threat - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 240f * dt);
                    Body?.SetPose(NpcPose.Cower, 0.25f);
                    break;
                case Act.Safe:
                    Body?.SetMoveSpeed(0f);
                    Body?.SetPose(NpcPose.Idle, 0.3f);
                    break;
            }
            Tick(dt);
        }

        private void Tick(float dt)
        {
            if (Body == null) return;
            var pc = PlayerController.Instance;
            float dist = pc != null ? Vector3.Distance(pc.transform.position, transform.position) : 0f;
            Body.Tick(dt, dist < 40f || Time.frameCount % 4 == 0);
        }

        public string ReachedPlace => Mode == Act.Safe ? _destination : null;

        void IHitReactor.OnHitResolved(HitData hit, HitResult result)
        {
            if (!result.Landed) return;
            Body?.SetPose(NpcPose.Hit, 0.05f);
            // Nobody dies on screen in these events: badly hurt people go down and wait for help.
            if (Damageable.Health.Normalized < 0.35f || result.Killed)
            {
                if (result.Killed) Damageable.Health.Revive(0.05f);
                Mode = Act.Injured;
            }
        }

        public string Prompt => Mode == Act.Injured ? $"Help — {DisplayName}" : $"Talk — {DisplayName}";
        public Vector3 Position => transform.position + Vector3.up * 0.9f;
        public bool CanInteract => Mode != Act.Gone && gameObject.activeInHierarchy;

        public void Interact(PlayerController player)
        {
            var hud = LivingWorld.Instance != null ? LivingWorld.Instance.Hud : null;
            if (Mode == Act.Injured)
            {
                Helped = true;
                Damageable.Health.Revive(0.6f);
                Mode = Act.Cower;
                hud?.ShowLine(DisplayName, NpcDialogue.Thanks(), 3f);
                RunTo("gate_n", 2.2f);
                return;
            }
            hud?.ShowLine(DisplayName, Mode == Act.Safe ? "Gracias... de verdad, gracias." : "¡Por favor, ayúdanos!", 3f);
        }

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);
    }

    /// <summary>
    /// Runs the dynamic events of the region (<see cref="FrontierEvents"/>). Every 15 in-game minutes eligible events
    /// may start; an active event is staged physically when its spot is loaded and the player is around (actors,
    /// demons out of view, the alarm bell…) and resolved by what happens there; otherwise it resolves on its own at
    /// the soft deadline. At the hard deadline the scene is cleared. Rest / time skips simulate the skipped hours.
    /// Consequences go to the world memory and the sectors refresh (wrecks, broken fences, repairs).
    /// </summary>
    public sealed class WorldEventDirector : MonoBehaviour
    {
        public static WorldEventDirector Instance { get; private set; }

        public sealed class Staged
        {
            public WorldEventDef Def;
            public WorldEventRecord Record;
            public EventSpot Spot;
            public Transform Root;
            public readonly List<EventActor> Actors = new List<EventActor>();
            public readonly List<DemonWorldAgent> Demons = new List<DemonWorldAgent>();
            public NpcAgent Npc;
            public bool PlayerInvolved;
            public bool Resolved;
            public float StagedAt;
            public string Note = "";
            public readonly List<Mesh> Meshes = new List<Mesh>();
        }

        public IReadOnlyList<WorldEventDef> Defs => FrontierEvents.Defs;
        public IReadOnlyDictionary<string, Staged> StagedEvents => _staged;
        public readonly List<string> Log = new List<string>();
        public bool PresenceActive { get; private set; }

        private LivingWorld _world;
        private readonly Dictionary<string, Staged> _staged = new Dictionary<string, Staged>();
        private double _nextCheck;
        private EnemyData _rareData;

        public static WorldEventDirector Create(LivingWorld world)
        {
            var go = new GameObject("WorldEventDirector");
            go.transform.SetParent(world.transform, false);
            var d = go.AddComponent<WorldEventDirector>();
            d._world = world;
            d.Init();
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
            if (_rareData != null) Destroy(_rareData);
        }

        private void Init()
        {
            _nextCheck = _world.Now + 0.25;
            _world.Time.TimeJumped += OnTimeJumped;
            var npcs = _world.Npcs;
            npcs.EventInteraction = OnNpcInteract;
            npcs.EventLine = OnNpcLine;
            npcs.Helped += a =>
            {
                foreach (var s in _staged.Values)
                    if (s.Npc == a && s.Def.Id == FrontierEvents.WoundedHunter && !s.Resolved)
                    {
                        s.PlayerInvolved = true;
                        Resolve(s, true);
                    }
            };
            // Events still active from the save resume where they were (staged when the player comes near).
            foreach (var def in Defs)
            {
                var r = _world.State.Event(def.Id);
                if (r.IsActive && def.Id == FrontierEvents.ExceptionalPresence) ApplyPresence(true);
            }
        }

        private void Note(string line)
        {
            Log.Add($"[Day {_world.Time.Clock.Day} {WorldClock.Format(_world.Time.Clock.Hour)}] {line}");
            if (Log.Count > 40) Log.RemoveAt(0);
            Debug.Log("[WorldEvents] " + line);
        }

        private int HuntersOnDuty()
        {
            int n = 0;
            foreach (var h in _world.Npcs.Hunters)
                if (h.Agent.State.Alive && !h.Agent.State.Injured && !h.Agent.State.Indoors) n++;
            return n;
        }

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            if (_world == null || _world.Time == null) return;
            double now = _world.Now;
            int day = _world.Time.Clock.Day;
            float hour = _world.Time.Clock.Hour;

            if (now >= _nextCheck)
            {
                _nextCheck = now + WorldEventSimulator.CheckInterval;
                int check = (int)(now / WorldEventSimulator.CheckInterval);
                foreach (var def in Defs)
                {
                    var r = _world.State.Event(def.Id);
                    WorldEventRules.UpdateEligibility(def, r, day, hour, now);
                    if (r.state != WorldEventState.Eligible) continue;
                    if (WorldEventRules.Roll(def.Id + ":start", check, r.triggerCount) >= def.ChancePerCheck) continue;
                    StartEvent(def, check);
                }
            }

            foreach (var def in Defs)
            {
                var r = _world.State.Event(def.Id);
                if (_staged.TryGetValue(def.Id, out var s))
                {
                    if (!s.Resolved) TickStaged(s, now);
                    if (now >= r.hardDeadline && !PlayerNear(s, 35f))
                    {
                        if (!s.Resolved) Resolve(s, FrontierEvents.SuccessChance(def, hour, HuntersOnDuty()) > Random.value);
                        WorldEventRules.Expire(def, r, now);
                        Cleanup(s);
                    }
                    continue;
                }
                if (r.IsActive && ShouldStage(def, r))
                {
                    Stage(def, r);
                    continue;
                }
                bool present = def.Id == FrontierEvents.VillageAttack && DemonWorldAgent.InVillage(_world.PlayerPosition, 40f);
                var step = WorldEventRules.Advance(def, r, now, present, FrontierEvents.SuccessChance(def, hour, HuntersOnDuty()));
                if (step == WorldEventRules.Step.ResolvedOffscreen) Outcome(def, r, r.state == WorldEventState.ResolvedSuccess, false);
                if (def.Id == FrontierEvents.ExceptionalPresence && PresenceActive && !r.IsActive) ApplyPresence(false);
            }
        }

        private void StartEvent(WorldEventDef def, int variant)
        {
            var r = _world.State.Event(def.Id);
            WorldEventRules.Start(def, r, _world.Now, variant);
            Note($"{def.Title} started{(FrontierEvents.Spot(def.Id, r.variant) is EventSpot sp ? $" near ({sp.X:0}, {sp.Z:0})" : "")}");
            if (def.Id == FrontierEvents.ExceptionalPresence) ApplyPresence(true);
        }

        private bool ShouldStage(WorldEventDef def, WorldEventRecord r)
        {
            if (def.Id == FrontierEvents.ExceptionalPresence) return false;
            Vector3 at = SpotPosition(def, r);
            if (!_world.Sectors.IsLoadedAt(at)) return false;
            float d = Flat(at - _world.PlayerPosition).magnitude;
            return d < (def.Id == FrontierEvents.RareDemon ? 160f : 130f);
        }

        private Vector3 SpotPosition(WorldEventDef def, WorldEventRecord r)
        {
            if (def.Id == FrontierEvents.Discovery)
            {
                var c = FrontierEvents.DiscoveryCaches[r.variant % FrontierEvents.DiscoveryCaches.Length];
                return RegionTerrain.OnGround(c[0], c[1]);
            }
            var spot = FrontierEvents.Spot(def.Id, r.variant);
            return spot != null ? RegionTerrain.OnGround(spot.X, spot.Z) : _world.PlayerPosition;
        }

        private bool PlayerNear(Staged s, float r)
        {
            Vector3 at = s.Spot != null ? new Vector3(s.Spot.X, 0f, s.Spot.Z) : s.Root != null ? s.Root.position : _world.PlayerPosition;
            return Flat(at - _world.PlayerPosition).magnitude < r;
        }

        // ------------------------------------------------------------------ staging

        private void Stage(WorldEventDef def, WorldEventRecord r)
        {
            var s = new Staged { Def = def, Record = r, Spot = FrontierEvents.Spot(def.Id, r.variant), StagedAt = Time.time };
            Vector3 at = SpotPosition(def, r);
            var sector = _world.Sectors.Loaded(L.SectorAt(at.x, at.z)?.Id);
            s.Root = new GameObject("Event_" + def.Id).transform;
            s.Root.SetParent(sector != null ? sector.DynamicRoot : transform, false);
            _staged[def.Id] = s;
            var demons = WorldDemonDirector.Instance;
            string sectorId = L.SectorAt(at.x, at.z)?.Id ?? def.Sector;

            switch (def.Id)
            {
                case FrontierEvents.CaravanAttack:
                {
                    BuildCart(s, at + Vector3.right * 2.5f, 20f, false);
                    var merchant = EventActor.Create(s.Root, "caravan_merchant", "Caravan merchant", false, false, 501, at + Vector3.left * 1.2f, 180f, VillagerOutfit.Traveler);
                    var porter = EventActor.Create(s.Root, "caravan_porter", "Porter", true, false, 502, at + Vector3.forward * 1.5f, 160f, VillagerOutfit.WorkJacket);
                    s.Actors.Add(merchant);
                    s.Actors.Add(porter);
                    SpawnHunters(s, at, 2, sectorId);
                    s.Note = "travellers cornered by their cart";
                    break;
                }
                case FrontierEvents.FamilyPursued:
                {
                    var mother = EventActor.Create(s.Root, "family_mother", "Fleeing mother", true, false, 511, at, 90f, VillagerOutfit.Apron);
                    var son = EventActor.Create(s.Root, "family_son", "Her son", false, true, 512, at + Vector3.right, 90f, VillagerOutfit.Child);
                    s.Actors.Add(mother);
                    s.Actors.Add(son);
                    mother.RunTo(s.Spot.Destination ?? "gate_n", 3.3f);
                    son.RunTo(s.Spot.Destination ?? "gate_n", 3.1f);
                    var demon = SpawnHunters(s, at, 1, sectorId);
                    foreach (var d in s.Demons) d.Enemy.SpeedMultiplier = 0.7f; // close, never quite catching them
                    s.Note = "a mother and her son running for the village";
                    break;
                }
                case FrontierEvents.WoundedHunter:
                {
                    NpcAgent pick = null;
                    foreach (var h in _world.Npcs.Hunters)
                    {
                        var a = h.Agent;
                        if (!a.State.Alive || a.State.Injured || h.Fighting || a.Shown) continue;
                        pick = a;
                        break;
                    }
                    if (pick != null)
                    {
                        var st = pick.State;
                        st.Indoors = false;
                        st.Travelling = false;
                        st.Route = null;
                        st.X = at.x;
                        st.Z = at.z;
                        st.Heading = Random.Range(0f, 360f);
                        var near = _world.Nav.Nearest(at.x, at.z);
                        st.AnchorNode = near != null ? near.Index : -1;
                        _world.Npcs.Sim.SetInjured(st, true);
                        st.EventId = def.Id;
                        s.Npc = pick;
                        s.Note = $"{st.Def.Name} lies hurt on the trail";
                    }
                    else s.Note = "no hunter available (resolves on its own)";
                    break;
                }
                case FrontierEvents.HunterDuel:
                {
                    HunterBrain hunter = null;
                    foreach (var h in _world.Npcs.Hunters)
                        if (h.Agent.State.Alive && !h.Agent.State.Injured && !h.Fighting && !h.Agent.Shown) { hunter = h; break; }
                    var spot = demons != null ? demons.HiddenSpot(at, 12f, 30f) : null;
                    if (hunter != null && spot.HasValue)
                    {
                        var st = hunter.Agent.State;
                        st.Indoors = false;
                        st.X = at.x;
                        st.Z = at.z;
                        st.Travelling = false;
                        hunter.OnCall = true;
                        s.Npc = hunter.Agent;
                        var d = demons.Spawn(spot.Value, sectorId, null, def.Id);
                        if (d != null)
                        {
                            s.Demons.Add(d);
                            d.AssaultTarget = at;
                        }
                        s.Note = $"{st.Def.Name} hunts a demon";
                    }
                    else s.Note = "nobody to duel (resolves on its own)";
                    break;
                }
                case FrontierEvents.VillageAttack:
                {
                    if (WorldAmbience.Instance != null) WorldAmbience.Instance.AlarmBell(10);
                    _world.Npcs.Shelter();
                    foreach (var h in _world.Npcs.Hunters) h.OnCall = true;
                    _world.State.SetSectorFlag("village", "alarm", true);
                    _world.ExtraDangerMood = 0.35f;
                    var plaza = L.Place("plaza");
                    Vector3 target = new Vector3(plaza.X, 0f, plaza.Z);
                    for (int i = 0; i < 3 && demons != null; i++)
                    {
                        var spot = demons.HiddenSpot(at, 40f, 75f, true);
                        if (!spot.HasValue) continue;
                        var d = demons.Spawn(spot.Value, sectorId, null, def.Id);
                        if (d == null) continue;
                        d.AllowVillage = true;
                        d.AssaultTarget = target;
                        s.Demons.Add(d);
                    }
                    _world.Hud?.ShowLine(null, "¡La campana! Algo se acerca a la aldea.", 4f);
                    s.Note = $"{s.Demons.Count} demons coming for the village";
                    break;
                }
                case FrontierEvents.RareDemon:
                {
                    if (demons != null && demons.NightspawnData != null)
                    {
                        if (_rareData == null)
                        {
                            _rareData = Instantiate(demons.NightspawnData);
                            _rareData.name = "RareNightspawn";
                            _rareData.displayName = "Pale-Eyed Nightspawn";
                            _rareData.scale *= 1.3f;
                            _rareData.maxHealth *= 2.4f;
                            _rareData.eyeColor = new Color(0.8f, 2.2f, 3.2f);
                        }
                        var spot = demons.HiddenSpot(at, 0f, 12f) ?? demons.HiddenSpot(at, 12f, 35f);
                        if (spot.HasValue)
                        {
                            var d = demons.Spawn(spot.Value, sectorId, null, def.Id, _rareData);
                            if (d != null)
                            {
                                d.SetMode(DemonMode.Resting);
                                s.Demons.Add(d);
                            }
                        }
                    }
                    s.Note = "something pale-eyed watches from the dark";
                    break;
                }
                case FrontierEvents.Discovery:
                {
                    BuildCart(s, at, Random.Range(0f, 360f), true);
                    InspectPoint.Create(s.Root, at + Vector3.up, "Abandoned bundle", null, () =>
                    {
                        if (!s.Resolved)
                        {
                            s.PlayerInvolved = true;
                            Resolve(s, true);
                            var pc = PlayerController.Instance;
                            if (pc != null) pc.Damageable.Health.Heal(pc.Damageable.Health.Max * 0.3f);
                        }
                        return "Un fardo abandonado: hierbas secas, arroz y una nota: «Si alguien encuentra esto, que le sirva.»";
                    });
                    s.Note = "a bundle left by the road";
                    break;
                }
                case FrontierEvents.LostChild:
                {
                    var kid = PickChild();
                    if (kid != null)
                    {
                        var st = kid.State;
                        st.Mode = NpcMode.Event;
                        st.EventId = def.Id;
                        st.Indoors = false;
                        st.Travelling = false;
                        st.Route = null;
                        st.X = at.x;
                        st.Z = at.z;
                        st.Activity = NpcActivity.Sit;
                        var near = _world.Nav.Nearest(at.x, at.z);
                        st.AnchorNode = near != null ? near.Index : -1;
                        s.Npc = kid;
                        s.Note = $"{st.Def.Name} wandered off";
                    }
                    else s.Note = "both children are home (resolves on its own)";
                    break;
                }
            }
            Note($"{def.Title} staged: {s.Note}");
        }

        /// <summary>Demons for an event, out of view near the spot, hunting its actors (or the spot).</summary>
        private DemonWorldAgent SpawnHunters(Staged s, Vector3 at, int count, string sector)
        {
            var demons = WorldDemonDirector.Instance;
            if (demons == null) return null;
            DemonWorldAgent first = null;
            for (int i = 0; i < count; i++)
            {
                var spot = demons.HiddenSpot(at, 20f, 45f);
                if (!spot.HasValue) continue;
                var d = demons.Spawn(spot.Value, sector, null, s.Def.Id);
                if (d == null) continue;
                s.Demons.Add(d);
                first ??= d;
                if (s.Actors.Count > 0)
                {
                    var target = s.Actors[i % s.Actors.Count];
                    d.Hunt(target.transform, () => target != null && target.Standing);
                }
            }
            foreach (var a in s.Actors) if (first != null) a.CowerFrom(first.transform.position);
            return first;
        }

        private void BuildCart(Staged s, Vector3 pos, float yaw, bool broken)
        {
            var batch = new MeshBatcher { Sink = s.Meshes };
            var c = new PropContext(batch, s.Root);
            RegionProps.Cart(c, pos, yaw, broken, 77);
            batch.Build(s.Root, "EventCart");
        }

        private NpcAgent PickChild()
        {
            foreach (var id in new[] { "mio", "kei" })
            {
                var a = _world.Npcs.Find(id);
                if (a != null && a.State.Alive && !a.Shown && a.State.Mode == NpcMode.Routine) return a;
            }
            return null;
        }

        // ------------------------------------------------------------------ live events

        private void TickStaged(Staged s, double now)
        {
            if (PlayerNear(s, 28f)) s.PlayerInvolved = true;
            s.Demons.RemoveAll(d => d == null);
            bool demonsDown = s.Demons.TrueForAll(d => d.Enemy == null || !d.Enemy.IsAlive || d.Escaped || !d.gameObject.activeInHierarchy);
            float hour = _world.Time.Clock.Hour;

            switch (s.Def.Id)
            {
                case FrontierEvents.CaravanAttack:
                {
                    bool anyStanding = s.Actors.Exists(a => a != null && a.Standing);
                    if (s.Demons.Count > 0 && demonsDown)
                    {
                        foreach (var a in s.Actors) if (a != null && a.Standing) a.RunTo("gate_n", 1.6f);
                        Resolve(s, true);
                    }
                    else if (!anyStanding) Resolve(s, false);
                    break;
                }
                case FrontierEvents.FamilyPursued:
                {
                    bool reached = s.Actors.Exists(a => a != null && a.Standing && a.ReachedPlace != null);
                    bool allDown = !s.Actors.Exists(a => a != null && a.Standing);
                    if (reached || (s.Demons.Count > 0 && demonsDown)) Resolve(s, true);
                    else if (allDown && !s.Actors.Exists(a => a.Helped)) Resolve(s, false);
                    break;
                }
                case FrontierEvents.HunterDuel:
                {
                    if (s.Demons.Count > 0 && demonsDown) Resolve(s, true);
                    else if (s.Npc != null && s.Npc.State.Injured) Resolve(s, false);
                    break;
                }
                case FrontierEvents.VillageAttack:
                    if (s.Demons.Count == 0 || demonsDown) Resolve(s, true);
                    break;
                case FrontierEvents.RareDemon:
                    if (s.Demons.Count > 0 && s.Demons.TrueForAll(d => d.Enemy == null || !d.Enemy.IsAlive)) Resolve(s, true);
                    else if (s.Demons.Count > 0 && WorldDemonDirector.Instance != null && WorldDemonDirector.Instance.VisibleToPlayer(s.Demons[0].transform.position))
                        _world.State.SetFact("Rare_Demon_Seen", 1, now);
                    break;
                case FrontierEvents.LostChild:
                {
                    var kid = s.Npc;
                    if (kid == null) break;
                    var home = L.Place(kid.State.Def.Home);
                    if (home != null && kid.Controller is ChildFollow && Flat(kid.transform.position - new Vector3(home.X, 0f, home.Z)).magnitude < 14f)
                    {
                        s.PlayerInvolved = true;
                        Resolve(s, true);
                    }
                    break;
                }
            }
            // Unattended past the soft deadline: the world does not wait.
            if (!s.Resolved && now >= s.Record.softDeadline && !PlayerNear(s, 60f))
                Resolve(s, Random.value < FrontierEvents.SuccessChance(s.Def, hour, HuntersOnDuty()));
        }

        private void Resolve(Staged s, bool success)
        {
            if (s.Resolved) return;
            s.Resolved = true;
            WorldEventRules.Resolve(s.Record, success, _world.Now, !s.PlayerInvolved);
            Outcome(s.Def, s.Record, success, s.PlayerInvolved);
            // Aftermath: survivors go on, demons leave, the village calms down.
            foreach (var d in s.Demons)
                if (d != null && d.Enemy != null && d.Enemy.IsAlive && !d.Engaged) { d.ClearHunt(); d.EventId = null; d.AllowVillage = false; d.AssaultTarget = null; }
            if (s.Def.Id == FrontierEvents.VillageAttack) EndAlarm();
            if (s.Def.Id == FrontierEvents.LostChild && s.Npc != null) ReturnChild(s.Npc, success);
            if (s.Def.Id == FrontierEvents.WoundedHunter && s.Npc != null && !success && s.Npc.State.Injured)
            {
                _world.Npcs.Sim.SetInjured(s.Npc.State, false);
                s.Npc.State.EventId = null;
                _world.Npcs.Sim.Release(s.Npc.State, _world.Time.Clock.Hour);
            }
            if (s.Def.Id == FrontierEvents.HunterDuel && s.Npc != null)
            {
                var hb = s.Npc.GetComponent<HunterBrain>();
                if (hb != null) hb.OnCall = false;
            }
        }

        private void Outcome(WorldEventDef def, WorldEventRecord r, bool success, bool playerInvolved)
        {
            r.playerInvolved = playerInvolved;
            string summary = FrontierEvents.ApplyOutcome(_world.State, def, r, success, playerInvolved, _world.Now);
            Note($"{def.Title}: {(success ? "SUCCESS" : "FAILURE")}{(playerInvolved ? " (player)" : " (offscreen)")} — {summary}");
            _world.Sectors.RefreshStates();
            if (playerInvolved)
            {
                _world.Hud?.ShowLine(null, success ? "Lo lograste. La región lo recordará." : "No llegaste a tiempo...", 3.5f);
                _world.SaveWorld("event " + def.Id, true);
            }
            if (def.Id == FrontierEvents.ExceptionalPresence) ApplyPresence(false);
            if (def.Id == FrontierEvents.VillageAttack && !_staged.ContainsKey(def.Id)) EndAlarm();
        }

        private void Cleanup(Staged s)
        {
            _staged.Remove(s.Def.Id);
            foreach (var d in s.Demons)
                if (d != null && d.Enemy != null && d.Enemy.IsAlive && !d.Engaged && WorldDemonDirector.Instance != null) WorldDemonDirector.Instance.Despawn(d, false);
            foreach (var m in s.Meshes) if (m != null) Destroy(m);
            if (s.Root != null) Destroy(s.Root.gameObject);
            if (s.Npc != null && s.Npc.State.EventId == s.Def.Id && !s.Npc.State.Injured) s.Npc.State.EventId = null;
            Note($"{s.Def.Title} cleared");
        }

        private void EndAlarm()
        {
            _world.State.SetSectorFlag("village", "alarm", false);
            _world.ExtraDangerMood = PresenceActive ? 0.5f : 0f;
            if (!PresenceActive)
            {
                _world.Npcs.ReleaseAll();
                foreach (var h in _world.Npcs.Hunters) h.OnCall = false;
            }
        }

        // ------------------------------------------------------------------ lost child

        /// <summary>The found child follows the player home.</summary>
        private sealed class ChildFollow : MonoBehaviour
        {
            public NpcAgent Agent;

            private void Update()
            {
                var pc = PlayerController.Instance;
                if (pc == null || Agent == null) return;
                Vector3 to = pc.transform.position - transform.position;
                to.y = 0f;
                float d = to.magnitude;
                var body = Agent.Body;
                if (d > 2.4f)
                {
                    float speed = d > 7f ? 3.4f : 1.8f;
                    Vector3 p = transform.position + to / d * Mathf.Min(d - 2f, speed * Time.deltaTime);
                    if (NpcPlaces.Standable(p.x, p.z))
                    {
                        p.y = RegionTerrain.WalkHeight(p.x, p.z);
                        transform.position = p;
                    }
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
                    if (body != null)
                    {
                        body.SetMoveSpeed(speed);
                        body.SetPose(speed > 2.5f ? NpcPose.Jog : NpcPose.Walk, 0.2f);
                    }
                }
                else if (body != null)
                {
                    body.SetMoveSpeed(0f);
                    body.SetPose(NpcPose.Idle, 0.3f);
                }
                if (d > 60f) Agent.Say("¡Espérame!", 2f);
            }
        }

        private bool OnNpcInteract(NpcAgent a)
        {
            if (!_staged.TryGetValue(FrontierEvents.LostChild, out var s) || s.Npc != a || s.Resolved) return false;
            if (a.Controller is ChildFollow) return false;
            var follow = a.gameObject.AddComponent<ChildFollow>();
            follow.Agent = a;
            a.TakeControl(follow);
            a.Say("¿Me... me llevas a casa? Me perdí siguiendo una mariposa.", 4f);
            return true;
        }

        private string OnNpcLine(NpcAgent a)
        {
            if (!_staged.TryGetValue(FrontierEvents.LostChild, out var s) || s.Npc == null || s.Resolved) return null;
            var child = s.Npc.State.Def;
            if (a == s.Npc) return null;
            bool parent = a.State.Def.Home == child.Home && a.State.Def.Role == NpcRole.Parent || a.State.Def.Id == "mitsu" && child.Id == "mio";
            if (parent) return $"¿Has visto a {child.Name}? Salió a jugar hacia el bosque y no ha vuelto...";
            if (a.State.Def.Role == NpcRole.Guard) return $"Estamos buscando a {child.Name}. Si lo ves por el bosque del oeste, tráelo.";
            return null;
        }

        private void ReturnChild(NpcAgent kid, bool foundByPlayer)
        {
            var follow = kid.GetComponent<ChildFollow>();
            if (follow != null) Destroy(follow);
            kid.State.EventId = null;
            if (kid.Controller != null) kid.ReleaseControl(_world.Npcs.Sim, _world.Time.Clock.Hour);
            else
            {
                kid.State.Mode = NpcMode.Routine;
                if (!foundByPlayer) _world.Npcs.Sim.Snap(kid.State, _world.Time.Clock.Hour);
            }
            if (foundByPlayer) kid.Say("¡Gracias! ¡Ya estoy en casa!", 3f);
        }

        // ------------------------------------------------------------------ exceptional presence

        /// <summary>
        /// The small version of an exceptional demonic presence: the bell, villagers retreat, hunters mobilise, shops
        /// close, the air thickens, demons roam in numbers outside the village, the region remembers it.
        /// </summary>
        public void TriggerExceptionalPresence()
        {
            var def = FrontierEvents.Def(FrontierEvents.ExceptionalPresence);
            var r = _world.State.Event(def.Id);
            if (r.IsActive) return;
            WorldEventRules.Start(def, r, _world.Now, 0);
            Note("Exceptional demonic presence");
            ApplyPresence(true);
        }

        private void ApplyPresence(bool on)
        {
            if (PresenceActive == on) return;
            PresenceActive = on;
            var demons = WorldDemonDirector.Instance;
            if (demons != null) demons.Presence = on;
            if (on)
            {
                if (WorldAmbience.Instance != null) WorldAmbience.Instance.AlarmBell(14);
                _world.Npcs.Shelter(260f);
                foreach (var h in _world.Npcs.Hunters) h.OnCall = true;
                _world.ExtraDangerMood = 0.5f;
                foreach (var sec in L.Sectors) _world.State.SetSectorFlag(sec.Id, "presence", true);
                _world.Hud?.ShowLine(null, "El aire se vuelve pesado. Algo enorme se mueve en la región.", 5f);
                // A first pair at the village edge: the encounters start soon.
                for (int i = 0; i < 2 && demons != null; i++)
                {
                    var p = demons.HiddenSpot(new Vector3(L.VillageX, 0f, L.VillageZ), 70f, 110f);
                    if (!p.HasValue) continue;
                    var sec = L.SectorAt(p.Value.x, p.Value.z);
                    demons.Spawn(p.Value, sec != null ? sec.Id : "village");
                }
            }
            else
            {
                foreach (var sec in L.Sectors) _world.State.SetSectorFlag(sec.Id, "presence", false);
                _world.ExtraDangerMood = 0f;
                _world.Npcs.ReleaseAll();
                foreach (var h in _world.Npcs.Hunters) h.OnCall = false;
                _world.Hud?.ShowLine(null, "La presencia se desvanece. Por ahora.", 4f);
            }
        }

        // ------------------------------------------------------------------ time jumps / debug

        private void OnTimeJumped(double from, double to)
        {
            // Staged scenes resolve as if the player had walked away; then the skipped hours are simulated.
            foreach (var s in new List<Staged>(_staged.Values))
            {
                if (!s.Resolved) Resolve(s, Random.value < FrontierEvents.SuccessChance(s.Def, _world.Time.Clock.Hour, HuntersOnDuty()));
                Cleanup(s);
            }
            var report = WorldEventSimulator.Simulate(Defs, _world.State, from, to,
                d => FrontierEvents.SuccessChance(d, 0f, HuntersOnDuty()),
                (d, r) => Note($"{d.Title} started (while you were away)"),
                (d, r) => Outcome(d, r, r.state == WorldEventState.ResolvedSuccess, false));
            _nextCheck = to + WorldEventSimulator.CheckInterval;
            if (report.Started + report.Resolved > 0) Note($"Time skip: {report.Started} started, {report.Resolved} resolved, {report.Expired} cleared");
            _world.Sectors.RefreshStates();
        }

        /// <summary>World Lab: start an event now at the spot nearest the player (staged if the area is loaded).</summary>
        public void ForceStart(string id)
        {
            if (id == FrontierEvents.ExceptionalPresence)
            {
                TriggerExceptionalPresence();
                return;
            }
            var def = FrontierEvents.Def(id);
            if (def == null) return;
            var r = _world.State.Event(id);
            if (_staged.TryGetValue(id, out var old)) Cleanup(old);
            int best = 0;
            float bestD = float.MaxValue;
            for (int v = 0; v < Math.Max(1, def.Variants); v++)
            {
                var pos = id == FrontierEvents.Discovery
                    ? new Vector3(FrontierEvents.DiscoveryCaches[v % FrontierEvents.DiscoveryCaches.Length][0], 0f, FrontierEvents.DiscoveryCaches[v % FrontierEvents.DiscoveryCaches.Length][1])
                    : FrontierEvents.Spot(id, v) is EventSpot sp ? new Vector3(sp.X, 0f, sp.Z) : Vector3.zero;
                float d = Flat(pos - _world.PlayerPosition).magnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = v;
                }
            }
            if (r.IsActive || r.IsResolved) WorldEventRules.Expire(def, r, _world.Now);
            r.state = WorldEventState.Eligible;
            r.cooldownUntil = 0;
            StartEvent(def, best);
        }

        /// <summary>World Lab: every event back to dormant, staged scenes cleared, alarms off.</summary>
        public void ResetAll()
        {
            foreach (var s in new List<Staged>(_staged.Values)) Cleanup(s);
            foreach (var def in Defs)
            {
                var r = _world.State.Event(def.Id);
                r.state = WorldEventState.Dormant;
                r.cooldownUntil = 0;
                r.startedAt = r.softDeadline = r.hardDeadline = r.resolvedAt = -1;
            }
            ApplyPresence(false);
            EndAlarm();
            Note("All world events reset");
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
