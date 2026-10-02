using BreathOfEclipse.Characters;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// The scene presence of one NPC: follows its logical state (<see cref="NpcSimState"/>) when visible — ground
    /// height, smooth turning, the pose and hand prop for what it is doing, stepping aside for the player — and
    /// talks when the player presses Interact. Built lazily and hidden indoors / far away by <see cref="NpcSystem"/>.
    /// </summary>
    public sealed class NpcAgent : MonoBehaviour, IInteractable
    {
        public NpcSimState State { get; private set; }
        public NpcBody Body { get; private set; }
        public bool BodyFailed { get; private set; }
        public bool Shown { get; private set; }
        public float DistanceToPlayer { get; set; } = 999f;
        public VillagerOutfit Outfit { get; private set; }
        public float Height { get; private set; }
        /// <summary>Times the stuck watchdog had to move this NPC.</summary>
        public int StuckRecoveries { get; private set; }

        private NpcSystem _system;
        private CapsuleCollider _collider;
        private Vector3 _avoid;
        private float _blocked;
        private float _talkUntil = -1f;
        private float _speakUntil = -1f;
        private float _poseSeed;
        private Vector3 _lastPos;
        private float _speed;
        private bool _askedToPass;

        public static NpcAgent Create(NpcSystem system, NpcSimState state)
        {
            var go = new GameObject("NPC_" + state.Def.Id);
            go.transform.SetParent(system.transform, false);
            go.layer = Layers.Npc;
            var a = go.AddComponent<NpcAgent>();
            a._system = system;
            a.State = state;
            a.Outfit = OutfitFor(state.Def);
            var rng = new System.Random(state.Def.LookSeed * 31 + 7);
            float h = state.Def.Child ? 1.2f + (float)rng.NextDouble() * 0.12f : state.Def.Female ? 1.6f + (float)rng.NextDouble() * 0.07f : 1.72f + (float)rng.NextDouble() * 0.08f;
            if (state.Def.Elder) h -= 0.06f;
            a.Height = h;
            a._poseSeed = (float)rng.NextDouble() * 100f;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            a._collider = go.AddComponent<CapsuleCollider>();
            a._collider.radius = state.Def.Child ? 0.22f : 0.3f;
            a._collider.height = h;
            a._collider.center = Vector3.up * h * 0.5f;
            a._collider.enabled = false;
            a.Place(true);
            return a;
        }

        public static VillagerOutfit OutfitFor(NpcDef d)
        {
            switch (d.Role)
            {
                case NpcRole.Child: return VillagerOutfit.Child;
                case NpcRole.Priest: return VillagerOutfit.ShrineRobe;
                case NpcRole.Guard: return VillagerOutfit.Guard;
                case NpcRole.Traveler:
                case NpcRole.Camper: return VillagerOutfit.Traveler;
                case NpcRole.Hunter: return VillagerOutfit.Hunter;
                case NpcRole.Merchant: return VillagerOutfit.Apron;
                case NpcRole.Parent:
                case NpcRole.Villager: return d.Female ? VillagerOutfit.Apron : d.Elder ? VillagerOutfit.Kimono : VillagerOutfit.WorkJacket;
                default: return VillagerOutfit.WorkJacket;
            }
        }

        /// <summary>Builds the visual (the scheduler calls this at most once per frame).</summary>
        public bool BuildBody()
        {
            if (Body != null || BodyFailed) return Body != null;
            Body = NpcBody.Build(transform, State.Def, Outfit, Height);
            if (Body == null)
            {
                BodyFailed = true;
                return false;
            }
            Body.gameObject.SetActive(Shown);
            UpdatePose(true);
            return true;
        }

        public void SetShown(bool shown)
        {
            if (Shown == shown) return;
            Shown = shown;
            _collider.enabled = shown && State.Alive;
            if (Body != null) Body.SetVisible(shown);
            if (shown) Place(true);
        }

        // ------------------------------------------------------------------ presentation

        /// <summary>Per-frame presentation (only for shown NPCs). <paramref name="evaluate"/>: animation LOD tick.</summary>
        public void Present(float dt, bool evaluate)
        {
            if (!Shown) return;
            UpdateAvoidance(dt);
            Place(false);
            UpdatePose(false);
            if (Body != null)
            {
                Body.SetMoveSpeed(_speed);
                if (Body.Face != null) Body.Face.Speaking = Time.time < _speakUntil;
                Body.Tick(dt, evaluate);
            }
            if (_talkUntil > 0f && Time.time >= _talkUntil)
            {
                _talkUntil = -1f;
                State.Held = false;
            }
        }

        private void Place(bool snap)
        {
            float x = State.X + _avoid.x, z = State.Z + _avoid.z;
            var target = new Vector3(x, RegionTerrain.WalkHeight(x, z), z);
            var t = transform;
            if (snap || (t.position - target).sqrMagnitude > 9f) t.position = target;
            else t.position = Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            var flat = t.position - _lastPos;
            flat.y = 0f;
            _speed = Mathf.Lerp(_speed, snap ? 0f : flat.magnitude / dt, 1f - Mathf.Exp(-8f * dt));
            _lastPos = t.position;

            float yaw = State.Heading;
            if (_talkUntil > 0f)
            {
                var pc = PlayerController.Instance;
                if (pc != null)
                {
                    var d = pc.transform.position - t.position;
                    if (d.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
            }
            var rot = Quaternion.Euler(0f, yaw, 0f);
            t.rotation = snap ? rot : Quaternion.RotateTowards(t.rotation, rot, 360f * Time.deltaTime);
        }

        /// <summary>Walking NPCs step aside for the player; if there is no room they wait, then ask to pass.</summary>
        private void UpdateAvoidance(float dt)
        {
            var pc = PlayerController.Instance;
            Vector3 want = Vector3.zero;
            bool blocked = false;
            if (pc != null && State.Travelling && State.Mode != NpcMode.Fleeing)
            {
                Vector3 me = new Vector3(State.X, 0f, State.Z);
                Vector3 p = pc.transform.position;
                p.y = 0f;
                Vector3 fwd = Quaternion.Euler(0f, State.Heading, 0f) * Vector3.forward;
                Vector3 to = p - me;
                float d = to.magnitude;
                if (d < 2f && Vector3.Dot(fwd, to / Mathf.Max(0.01f, d)) > 0.25f)
                {
                    Vector3 right = Vector3.Cross(Vector3.up, fwd);
                    float side = Vector3.Dot(right, to) > 0f ? -1f : 1f;
                    Vector3 offset = right * side * 0.95f;
                    if (NpcPlaces.Standable(me.x + offset.x, me.z + offset.z) && !L.IsWater(me.x + offset.x, me.z + offset.z)) want = offset;
                    else if (NpcPlaces.Standable(me.x - offset.x, me.z - offset.z)) want = -offset;
                    else blocked = d < 1.1f;
                }
            }
            _avoid = Vector3.MoveTowards(_avoid, want, dt * 1.8f);
            if (blocked)
            {
                _blocked += dt;
                State.Held = true;
                if (_blocked > 2.5f && !_askedToPass)
                {
                    _askedToPass = true;
                    Say("Disculpa... ¿me dejas pasar?", 2.5f);
                }
                // Stuck watchdog: if the player keeps blocking a narrow spot, slip past once out of view.
                if (_blocked > 6f && (Body == null || !Body.IsVisible) && State.Route != null)
                {
                    State.Route.Travelled = Mathf.Min(State.Route.Length, State.Route.Travelled + 3f);
                    _blocked = 0f;
                    StuckRecoveries++;
                }
            }
            else if (_blocked > 0f)
            {
                _blocked = 0f;
                _askedToPass = false;
                if (_talkUntil < 0f) State.Held = false;
            }
        }

        private void UpdatePose(bool snap)
        {
            if (Body == null) return;
            var s = State;
            float hour = _system.Hour;
            bool night = hour >= 19f || hour < 5.5f;
            bool moving = s.Travelling && !s.Held;
            NpcPose pose;
            NpcProp prop = NpcProp.None;

            if (!s.Alive) pose = NpcPose.Dead;
            else if (s.Injured) pose = moving ? NpcPose.InjuredWalk : NpcPose.Lie;
            else if (s.Mode == NpcMode.Afraid) pose = NpcPose.Cower;
            else if (_talkUntil > 0f) pose = NpcPose.Talk;
            else if (moving)
            {
                pose = s.Mode == NpcMode.Fleeing ? NpcPose.Sprint
                    : s.Activity == NpcActivity.Carry ? NpcPose.Carry
                    : s.Activity == NpcActivity.Play ? NpcPose.Jog
                    : NpcPose.Walk;
                prop = WalkingProp(s, hour, night);
            }
            else
            {
                switch (s.Activity)
                {
                    case NpcActivity.Farm:
                        int phase = (int)((Time.time + _poseSeed) / 22f) % 3;
                        pose = phase == 0 ? NpcPose.Harvest : phase == 1 ? NpcPose.Plant : NpcPose.Water;
                        break;
                    case NpcActivity.Chop: pose = NpcPose.Chop; prop = NpcProp.Axe; break;
                    case NpcActivity.Fish: pose = s.Place == "dock" ? NpcPose.Sit : NpcPose.Idle; prop = NpcProp.Rod; break;
                    case NpcActivity.Trade: pose = ((int)((Time.time + _poseSeed) / 12f) % 2 == 0) ? NpcPose.Talk : NpcPose.FoldArms; break;
                    case NpcActivity.Talk: pose = _system.HasCompany(this) ? NpcPose.Talk : NpcPose.Idle; break;
                    case NpcActivity.Sit: pose = _system.HasCompany(this) ? NpcPose.SitTalk : NpcPose.Sit; break;
                    case NpcActivity.Eat: pose = NpcPose.Eat; break;
                    case NpcActivity.Guard:
                    case NpcActivity.Patrol:
                        pose = night ? NpcPose.Lantern : NpcPose.FoldArms;
                        prop = night ? NpcProp.Lantern : s.Def.Role == NpcRole.Guard ? NpcProp.Spear : NpcProp.None;
                        if (!night && prop == NpcProp.Spear) pose = NpcPose.Idle;
                        break;
                    case NpcActivity.Camp:
                        pose = hour >= 22f || hour < 5f ? NpcPose.Lie : NpcPose.Sit;
                        break;
                    case NpcActivity.Sleep: pose = NpcPose.Lie; break;
                    case NpcActivity.Carry: pose = NpcPose.CarryIdle; prop = NpcProp.Basket; break;
                    case NpcActivity.Smith: pose = NpcPose.Hammer; prop = NpcProp.Hammer; break;
                    case NpcActivity.Pray: pose = NpcPose.Pray; break;
                    case NpcActivity.Work: pose = NpcPose.Kneel; break;
                    case NpcActivity.Play: pose = NpcPose.Idle; break;
                    default: pose = NpcPose.Idle; break;
                }
                if (s.Def.Role == NpcRole.Traveler && pose != NpcPose.Lie && pose != NpcPose.Sit) prop = prop == NpcProp.None ? NpcProp.Staff : prop;
            }
            if (s.Activity == NpcActivity.Carry && moving) prop = NpcProp.Basket;
            Body.SetPose(pose, snap ? 0.01f : 0.3f);
            Body.SetProp(prop);
        }

        private static NpcProp WalkingProp(NpcSimState s, float hour, bool night)
        {
            if (night) return NpcProp.Lantern;
            switch (s.Def.Role)
            {
                case NpcRole.Lumberjack: return hour < 17.5f ? NpcProp.Axe : NpcProp.None;
                case NpcRole.Farmer: return hour < 12f ? NpcProp.Hoe : NpcProp.None;
                case NpcRole.Fisher: return NpcProp.Rod;
                case NpcRole.Guard: return NpcProp.Spear;
                case NpcRole.Traveler:
                case NpcRole.Camper: return NpcProp.Staff;
                default: return NpcProp.None;
            }
        }

        // ------------------------------------------------------------------ talking

        /// <summary>Shows a line from this NPC (mouth moves while it is on screen).</summary>
        public void Say(string line, float seconds = 3.5f)
        {
            if (string.IsNullOrEmpty(line)) return;
            var hud = LivingWorld.Instance != null ? LivingWorld.Instance.Hud : null;
            if (hud != null) hud.ShowLine(State.Def.Name, line, seconds);
            _speakUntil = Time.time + Mathf.Min(seconds, 0.12f * line.Length);
        }

        public string Prompt => State.Injured ? $"Help — {State.Def.Name}" : $"Talk — {State.Def.Name}";
        public Vector3 Position => transform.position + Vector3.up * 0.9f;
        public bool CanInteract => Shown && State.Alive && !State.Indoors && State.Mode != NpcMode.Fleeing && State.Mode != NpcMode.Event;

        public void Interact(PlayerController player)
        {
            if (State.Injured)
            {
                _system.Help(this);
                return;
            }
            State.Held = true;
            _talkUntil = Time.time + 4.5f;
            if (Body != null) Body.SetPose(NpcPose.Nod, 0.2f);
            Say(NpcDialogue.Talk(State, _system.Phase, LivingWorld.Instance != null ? LivingWorld.Instance.State : null, _system.ShopOpen), 4.5f);
        }

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);
    }
}
