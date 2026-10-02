using System;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using BreathOfEclipse.Player;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Central scheduler of the region's people. One <see cref="NpcSimulation"/> holds every NPC's routine state
    /// (on and off screen). Near the player NPCs are simulated every frame and shown (bodies built lazily, one per
    /// frame, nearest first); far away they are simulated a few times per second and not drawn. Animation runs at
    /// full rate close by, at a fraction further out, and barely when off camera. Also drives the village's visible
    /// life: doors opening and closing behind people, the shop's shutters, the forge, the campfire, the laundry.
    /// </summary>
    public sealed class NpcSystem : MonoBehaviour
    {
        public const float ShowDistance = 92f;
        public const float HideDistance = 104f;

        public static NpcSystem Instance { get; private set; }

        public NpcSimulation Sim { get; private set; }
        public IReadOnlyList<NpcAgent> Agents => _agents;
        public float Hour => _world != null ? _world.Time.Clock.Hour : 12f;
        public DayPhase Phase => WorldClock.PhaseOf(Hour);
        public bool ShopOpen { get; private set; }
        public int ShownCount { get; private set; }
        public int LogicalCount => _agents.Count - ShownCount;
        public int BuiltCount { get; private set; }
        /// <summary>An NPC fell (agent, killed) — events and consequences listen.</summary>
        public event Action<NpcAgent, bool> NpcDown;
        public IEnumerable<HunterBrain> Hunters
        {
            get
            {
                foreach (var a in _agents)
                {
                    var h = a.GetComponent<HunterBrain>();
                    if (h != null) yield return h;
                }
            }
        }

        public int StuckRecoveries
        {
            get
            {
                int n = 0;
                foreach (var a in _agents) n += a.StuckRecoveries;
                return n;
            }
        }

        private LivingWorld _world;
        private readonly List<NpcAgent> _agents = new List<NpcAgent>();
        private readonly Dictionary<NpcSimState, NpcAgent> _byState = new Dictionary<NpcSimState, NpcAgent>();
        private float _farAccum, _nextProps, _nextBark = 8f;
        private int _frame;
        private int _barkSalt;

        public static NpcSystem Create(LivingWorld world)
        {
            var go = new GameObject("NpcSystem");
            go.transform.SetParent(world.transform, false);
            var s = go.AddComponent<NpcSystem>();
            s.Init(world);
            return s;
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
        }

        private void Init(LivingWorld world)
        {
            _world = world;
            Sim = new NpcSimulation(world.Nav, L.Npcs);
            // World memory: who is gone, hurt or missing.
            foreach (var n in Sim.Npcs)
            {
                var rec = world.State.Npc(n.Def.Id);
                if (!rec.alive) Sim.Kill(n);
                else if (rec.injured) Sim.SetInjured(n, true);
            }
            Sim.PlaceAt(world.Time.Clock.Hour);
            foreach (var n in Sim.Npcs)
            {
                var a = NpcAgent.Create(this, n);
                _agents.Add(a);
                _byState[n] = a;
                if (n.Def.Role == NpcRole.Hunter) HunterBrain.Attach(a, this);
            }
            Sim.WentInside += n => OpenDoor(n, 1.6f);
            Sim.CameOut += n => OpenDoor(n, 2.2f);
            world.Time.TimeJumped += OnTimeJumped;
            world.Sectors.SectorLoaded += (def, inst) =>
            {
                WorldInteractables.Populate(def, inst, world);
                UpdateWorldProps(true);
            };
            foreach (var e in world.Sectors.Entries)
                if (e.Model.State == SectorLoadState.Loaded) WorldInteractables.Populate(e.Def, e.Instance, world);
            world.RegisterBeforeSave(WriteState);
            if (WorldAmbience.Instance != null) WorldAmbience.Instance.IsWorking = IsWorking;
        }

        /// <summary>Builds the bodies of everyone near <paramref name="focus"/> right away (scene start, behind the fade).</summary>
        public void Prewarm(Vector3 focus)
        {
            UpdateDistances(focus);
            foreach (var a in _agents)
            {
                if (!WantShown(a)) continue;
                if (a.BuildBody()) BuiltCount++;
                a.SetShown(true);
            }
            UpdateWorldProps(true);
        }

        public NpcAgent AgentOf(NpcSimState s) => s != null && _byState.TryGetValue(s, out var a) ? a : null;

        public NpcAgent Find(string id)
        {
            foreach (var a in _agents) if (a.State.Def.Id == id) return a;
            return null;
        }

        // ------------------------------------------------------------------ scheduler

        private void Update()
        {
            if (_world == null || _world.Time == null) return;
            var clock = _world.Time.Clock;
            float dt = clock.Paused ? 0f : Time.deltaTime;
            float scale = Mathf.Max(1f, clock.Multiplier);
            float hour = clock.Hour;
            Vector3 focus = _world.PlayerPosition;
            _frame++;

            UpdateDistances(focus);

            // Simulation: near NPCs every frame, the rest at ~4 Hz (lightweight off-screen life).
            _farAccum += dt;
            bool farTick = _farAccum >= 0.25f;
            foreach (var a in _agents)
            {
                bool near = a.DistanceToPlayer < HideDistance + 20f;
                if (near) Sim.Tick(a.State, hour, dt, scale);
                else if (farTick) Sim.Tick(a.State, hour, _farAccum, scale);
                a.State.Visible = a.Shown;
            }
            if (farTick) _farAccum = 0f;

            // Visuals: show / hide, build one body per frame (nearest first), animation LOD.
            NpcAgent toBuild = null;
            int shown = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                bool want = WantShown(a);
                if (want && a.Body == null && !a.BodyFailed)
                {
                    if (toBuild == null || a.DistanceToPlayer < toBuild.DistanceToPlayer) toBuild = a;
                }
                a.SetShown(want && (a.Body != null || a.BodyFailed));
                if (!a.Shown) continue;
                shown++;
                int divisor = a.DistanceToPlayer < 25f ? 1 : a.DistanceToPlayer < 50f ? 2 : 4;
                if (a.Body != null && !a.Body.IsVisible) divisor = 8;
                a.Present(dt, (_frame + i) % divisor == 0);
            }
            ShownCount = shown;
            if (toBuild != null && toBuild.BuildBody()) BuiltCount++;

            if (Time.unscaledTime >= _nextProps)
            {
                _nextProps = Time.unscaledTime + 1f;
                UpdateWorldProps(false);
            }
            UpdateBarks();
        }

        private void UpdateDistances(Vector3 focus)
        {
            foreach (var a in _agents)
            {
                float dx = a.State.X - focus.x, dz = a.State.Z - focus.z;
                a.DistanceToPlayer = Mathf.Sqrt(dx * dx + dz * dz);
            }
        }

        private bool WantShown(NpcAgent a)
        {
            var s = a.State;
            if (s.Indoors) return false;
            // The fallen stay where they fell only while the player is around.
            if (!s.Alive) return a.Shown && Time.time - a.DiedAt < 45f;
            float limit = a.Shown ? HideDistance : ShowDistance;
            if (a.DistanceToPlayer > limit) return false;
            // Only where the detailed ground exists (the far view has no colliders and coarse heights).
            return _world.Sectors.IsLoadedAt(new Vector3(s.X, 0f, s.Z));
        }

        /// <summary>Another visible NPC close by (conversations, sitting together).</summary>
        public bool HasCompany(NpcAgent me)
        {
            foreach (var a in _agents)
            {
                if (a == me || !a.Shown || a.State.Travelling) continue;
                float dx = a.State.X - me.State.X, dz = a.State.Z - me.State.Z;
                if (dx * dx + dz * dz < 4.5f * 4.5f) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ time jumps

        private void OnTimeJumped(double from, double to)
        {
            float hour = _world.Time.Clock.Hour;
            foreach (var n in Sim.Npcs)
            {
                if (n.Mode == NpcMode.Fleeing) Sim.Release(n, hour);
                if (n.Mode == NpcMode.Routine) Sim.Snap(n, hour);
            }
            foreach (var a in _agents) a.SetShown(false);
            UpdateWorldProps(true);
        }

        /// <summary>Everyone runs inside (village attack, exceptional presence).</summary>
        public void Shelter(float radiusFromVillage = 200f)
        {
            foreach (var n in Sim.Npcs)
            {
                if (n.Def.Role == NpcRole.Guard || n.Def.Role == NpcRole.Hunter) continue;
                float dx = n.X - L.VillageX, dz = n.Z - L.VillageZ;
                if (dx * dx + dz * dz > radiusFromVillage * radiusFromVillage) continue;
                Sim.Flee(n);
            }
        }

        /// <summary>Danger is over: back to routines.</summary>
        public void ReleaseAll()
        {
            float hour = Hour;
            foreach (var n in Sim.Npcs)
                if (n.Mode == NpcMode.Fleeing || n.Mode == NpcMode.Afraid) Sim.Release(n, hour);
        }

        /// <summary>The player helps an injured NPC up (world memory: saved by the player).</summary>
        public void Help(NpcAgent a)
        {
            var s = a.State;
            if (!s.Injured) return;
            Sim.SetInjured(s, false);
            var rec = _world.State.Npc(s.Def.Id);
            rec.injured = false;
            rec.savedByPlayer = true;
            _world.State.SetFact("Helped_" + s.Def.Id, 1, _world.Now);
            a.Say(NpcDialogue.Thanks(), 3f);
            Sim.Release(s, Hour);
        }

        /// <summary>An NPC was struck down (killed) or badly hurt (injured, can be helped).</summary>
        public void OnNpcDown(NpcAgent a, bool killed)
        {
            var s = a.State;
            var rec = _world.State.Npc(s.Def.Id);
            if (killed)
            {
                if (!s.Alive) return;
                Sim.Kill(s);
                a.DiedAt = Time.time;
                rec.alive = false;
                _world.State.SetFact("Npc_Died_" + s.Def.Id, 1, _world.Now);
            }
            else
            {
                if (s.Injured) return;
                Sim.SetInjured(s, true);
                rec.injured = true;
                rec.injuredAt = _world.Now;
                a.Say(NpcDialogue.Bark(s, Phase, 0), 2.5f);
            }
            NpcDown?.Invoke(a, killed);
        }

        private void WriteState()
        {
            foreach (var n in Sim.Npcs)
            {
                var rec = _world.State.Npc(n.Def.Id);
                rec.alive = n.Alive;
                rec.injured = n.Injured;
                var sector = L.SectorAt(n.X, n.Z);
                rec.lastKnownSector = sector != null ? sector.Id : rec.lastKnownSector;
            }
        }

        // ------------------------------------------------------------------ village life

        private void OpenDoor(NpcSimState n, float seconds)
        {
            var door = SlidingDoor.For(n.Place);
            if (door != null) door.OpenBriefly(seconds);
        }

        private bool IsWorking(string npcId)
        {
            var n = Sim.Find(npcId);
            if (n == null || !n.Alive || n.Indoors || n.Travelling) return false;
            return n.Activity == NpcActivity.Smith || n.Activity == NpcActivity.Chop;
        }

        private void UpdateWorldProps(bool force)
        {
            var ohara = Sim.Find("ohara");
            ShopOpen = ohara != null && ohara.Alive && !ohara.Indoors && (ohara.Activity == NpcActivity.Trade || ohara.Activity == NpcActivity.Carry);
            var village = _world.Sectors.Loaded("village");
            if (village != null)
            {
                var shutters = village.Get<ShopShutters>("shop_shutters");
                if (shutters != null) shutters.SetClosed(!ShopOpen);
                var forge = village.Get<NightLight>("forge_light");
                if (forge != null)
                {
                    var tetsuo = Sim.Find("tetsuo");
                    bool working = tetsuo != null && tetsuo.Alive && tetsuo.Activity == NpcActivity.Smith && !tetsuo.Indoors;
                    forge.AlwaysOn = working;
                    forge.Enabled = working || NightLight.Level > 0.5f ? 1f : 0f;
                }
                var laundry = village.Get<Transform>("laundry");
                if (laundry != null)
                {
                    float h = Hour;
                    bool hanging = h >= 7f && h < 17.5f && !_world.State.SectorFlag("village", "alarm");
                    if (laundry.gameObject.activeSelf != hanging) laundry.gameObject.SetActive(hanging);
                }
            }
            var road = _world.Sectors.Loaded("forest_road");
            if (road != null)
            {
                var fire = road.Get<Campfire>("campfire");
                if (fire != null)
                {
                    bool anyone = false;
                    foreach (var n in Sim.Npcs)
                        if (n.Alive && n.Place == "camp" && !n.Travelling && (n.Activity == NpcActivity.Camp || n.Activity == NpcActivity.Sit)) anyone = true;
                    float h = Hour;
                    fire.SetLit(anyone && (h >= 17.5f || h < 7.5f));
                }
            }
        }

        /// <summary>Now and then someone near the player says something (never more than one line at a time).</summary>
        private void UpdateBarks()
        {
            if (Time.unscaledTime < _nextBark) return;
            _nextBark = Time.unscaledTime + Random.Range(7f, 14f);
            if (PlayerController.Instance == null) return;
            NpcAgent pick = null;
            float best = 16f;
            foreach (var a in _agents)
            {
                if (!a.Shown || !a.State.Alive || a.State.Indoors) continue;
                if (a.DistanceToPlayer < best && Random.value < 0.6f)
                {
                    best = a.DistanceToPlayer;
                    pick = a;
                }
            }
            if (pick == null) return;
            string line = NpcDialogue.Bark(pick.State, Phase, _barkSalt++);
            if (line != null) pick.Say(line, 3f);
        }
    }
}
