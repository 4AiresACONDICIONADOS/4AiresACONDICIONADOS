using System;
using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// The persistent root of the open world: owns the world memory (<see cref="WorldStateDatabase"/>), the clock,
    /// sector streaming and the world systems, and saves. It lives for the whole session; sectors come and go under
    /// it, so nothing persistent is ever duplicated.
    /// </summary>
    public sealed class LivingWorld : MonoBehaviour
    {
        public static LivingWorld Instance { get; private set; }

        public WorldStateDatabase State { get; private set; }
        public WorldTimeSystem Time { get; private set; }
        public WorldSectorSystem Sectors { get; private set; }
        public NavGraph Nav { get; private set; }
        public WorldHud Hud { get; private set; }
        public WorldAmbience Ambience { get; private set; }
        public PlayerController Player { get; private set; }
        /// <summary>True when this session continued a saved world.</summary>
        public bool Continued { get; private set; }
        /// <summary>Absolute world time (hours since day 1, 00:00).</summary>
        public double Now => Time != null ? Time.Clock.TotalHours : 0;
        public Vector3 PlayerPosition => Player != null ? Player.transform.position : _fallbackFocus;

        /// <summary>A place was discovered (id, display name, secret).</summary>
        public event Action<DiscoverySpot> Discovered;
        public event Action<string> Saved;

        private Vector3 _fallbackFocus;
        private float _nextDiscovery;
        private float _lastSave = -999f;
        private readonly List<Action> _beforeSave = new List<Action>();

        public static LivingWorld Create(Transform parent, WorldStateData data, bool continued, Vector3 focus)
        {
            var go = new GameObject("LivingWorld");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<LivingWorld>();
            w.Init(data, continued, focus);
            return w;
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

        private void Init(WorldStateData data, bool continued, Vector3 focus)
        {
            Continued = continued;
            _fallbackFocus = focus;
            State = new WorldStateDatabase(data);
            Nav = L.BuildNavGraph();
            Time = WorldTimeSystem.Create(transform, State.Data.day, State.Data.hour);
            Sectors = WorldSectorSystem.Create(transform, () => PlayerPosition, () => State, () => Now);
            Sectors.SectorChanged += OnSectorChanged;
            Sectors.LoadAround(focus, true);
            Hud = WorldHud.Create(this);
            Ambience = WorldAmbience.Create(this);
            // The shrine bell marks dawn and dusk.
            Time.PhaseChanged += (from, to) =>
            {
                if (to == DayPhase.Dawn || to == DayPhase.Sunset) Ambience.ShrineBell();
            };
        }

        public void BindPlayer(PlayerController player)
        {
            Player = player;
            var s = Sectors.Current;
            if (s != null) Visit(s);
        }

        /// <summary>Lets a system write its state into <see cref="State"/> right before saving.</summary>
        public void RegisterBeforeSave(Action a)
        {
            if (a != null && !_beforeSave.Contains(a)) _beforeSave.Add(a);
        }

        // ------------------------------------------------------------------ save

        /// <summary>
        /// Writes the world. Autosaves (rest, an important event resolved, entering a safe sector) are throttled;
        /// <paramref name="force"/> bypasses the throttle.
        /// </summary>
        public bool SaveWorld(string reason, bool force = false)
        {
            if (!force && UnityEngine.Time.unscaledTime - _lastSave < 20f) return false;
            _lastSave = UnityEngine.Time.unscaledTime;
            foreach (var a in _beforeSave)
            {
                try { a(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            var d = State.Data;
            d.day = Time.Clock.Day;
            d.hour = Time.Clock.Hour;
            if (Player != null && Player.Damageable != null && Player.Damageable.IsAlive)
            {
                var p = Player.transform.position;
                d.playerX = p.x;
                d.playerY = p.y;
                d.playerZ = p.z;
                d.playerYaw = Player.transform.eulerAngles.y;
            }
            d.lastSector = Sectors.Current != null ? Sectors.Current.Id : d.lastSector;
            bool ok = WorldSave.Save(d);
            if (ok)
            {
                Saved?.Invoke(reason);
                if (Hud != null) Hud.ShowSaved();
                Debug.Log($"[LivingWorld] Saved ({reason}) — {Time.Describe()}");
            }
            return ok;
        }

        // ------------------------------------------------------------------ sectors / discovery

        private void OnSectorChanged(SectorDef prev, SectorDef next)
        {
            if (next == null) return;
            Visit(next);
            // Safe sector transition: autosave when the player comes back to safety.
            if (prev != null && !prev.SafeZone && next.SafeZone) SaveWorld("safe sector");
        }

        private void Visit(SectorDef s)
        {
            State.Sector(s.Id).visits++;
            bool first = State.Discover("sector:" + s.Id);
            if (Hud != null) Hud.ShowArea(s, first);
        }

        private void Update()
        {
            // Ash Hollow: the air itself warns the player (no invisible walls).
            if (Time != null)
            {
                Vector3 p = PlayerPosition;
                float danger = 0f;
                foreach (var s in L.Sectors)
                {
                    if (s.Danger < 3) continue;
                    float d = s.Bounds.Distance(p.x, p.z);
                    danger = Mathf.Max(danger, 0.55f * (1f - Mathf.Clamp01(d / 45f)));
                }
                Time.DangerMood = Mathf.Max(danger, ExtraDangerMood);
            }
            if (UnityEngine.Time.unscaledTime >= _nextDiscovery)
            {
                _nextDiscovery = UnityEngine.Time.unscaledTime + 0.5f;
                CheckDiscoveries();
            }
        }

        /// <summary>Additional danger mood set by events (exceptional presence, village attack).</summary>
        public float ExtraDangerMood { get; set; }

        private void CheckDiscoveries()
        {
            Vector3 p = PlayerPosition;
            foreach (var d in L.Discoveries)
            {
                if (State.IsDiscovered(d.Id)) continue;
                float dx = p.x - d.X, dz = p.z - d.Z;
                if (dx * dx + dz * dz > d.Radius * d.Radius) continue;
                State.Discover(d.Id);
                State.SetFact("Discovered_" + d.Id, 1, Now);
                Discovered?.Invoke(d);
                if (Hud != null) Hud.ShowDiscovery(d);
            }
        }
    }
}
