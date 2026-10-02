using System.Collections.Generic;
using BreathOfEclipse.Player;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// F1 WORLD LAB (opens with the F1 debug menu in the living world): time, sector, streaming, people, demons,
    /// events, FPS and position; time controls, event spawns, sector load / unload, teleports, streaming bounds,
    /// NPC and demon inspectors, world event reset. Development tool — nothing in the game depends on it.
    /// </summary>
    public sealed class WorldLab : MonoBehaviour
    {
        private LivingWorld _world;
        private Rect _window = new Rect(430f, 20f, 470f, 700f);
        private Vector2 _scroll;
        private int _tab;
        private float _fps, _fpsAccum;
        private int _fpsFrames;
        private GUIStyle _header, _small;
        private static readonly string[] Tabs = { "WORLD", "NPCS", "DEMONS", "EVENTS" };

        public static WorldLab Create(LivingWorld world)
        {
            var go = new GameObject("WorldLab");
            go.transform.SetParent(world.transform, false);
            var lab = go.AddComponent<WorldLab>();
            lab._world = world;
            return lab;
        }

        private void Update()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum >= 0.5f)
            {
                _fps = _fpsFrames / _fpsAccum;
                _fpsAccum = 0f;
                _fpsFrames = 0;
            }
        }

        /// <summary>Frames per second measured in the running build (for the lab / playtest; never estimated).</summary>
        public float Fps => _fps;

        private void OnGUI()
        {
            if (!Core.DebugMenu.IsOpen || _world == null) return;
            if (_header == null)
            {
                _header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
                _header.normal.textColor = new Color(1f, 0.8f, 0.45f);
                _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            }
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUI.Window(0x0B0F, _window, Draw, "F1 WORLD LAB — v0.5 Living World");
            GUI.matrix = old;
        }

        private void Draw(int id)
        {
            var time = _world.Time;
            var clock = time.Clock;
            var sectors = _world.Sectors;
            var npcs = _world.Npcs;
            var demons = _world.Demons;
            var events = _world.Events;
            Vector3 p = _world.PlayerPosition;

            GUILayout.Label($"{time.Describe()}  |  FPS {(_fps > 0f ? _fps.ToString("0") : "-")}", _header);
            GUILayout.Label($"Sector: {(sectors.Current != null ? sectors.Current.Name : "-")}  |  Loaded {sectors.LoadedCount}/{sectors.Entries.Count}  |  Pos ({p.x:0}, {p.y:0}, {p.z:0})");
            GUILayout.Label($"NPCs active {npcs.ShownCount} / logical {npcs.LogicalCount}  |  Demons active {demons.Active.Count} / logical {demons.LogicalCount}  |  Events live {events.StagedEvents.Count}");
            _tab = GUILayout.Toolbar(_tab, Tabs);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawWorld(); break;
                case 1: DrawNpcs(); break;
                case 2: DrawDemons(); break;
                default: DrawEvents(); break;
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawWorld()
        {
            var time = _world.Time;
            GUILayout.Label("TIME", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("06:00")) time.SetHour(6f);
            if (GUILayout.Button("Morning 08:00")) time.SetHour(8f);
            if (GUILayout.Button("Noon 12:00")) time.SetHour(12f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sunset 17:30")) time.SetHour(17.5f);
            if (GUILayout.Button("Night 20:00")) time.SetHour(20f);
            if (GUILayout.Button("00:00")) time.SetHour(0f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(time.Clock.Paused ? "Resume" : "Pause")) time.SetPaused(!time.Clock.Paused);
            if (GUILayout.Button("1x")) time.SetMultiplier(1f);
            if (GUILayout.Button("5x")) time.SetMultiplier(5f);
            if (GUILayout.Button("20x")) time.SetMultiplier(20f);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Rest until morning (time skip)") && _world.Rest != null) _world.Rest.RestUntil(6f);

            GUILayout.Label("TELEPORT", _header);
            GUILayout.BeginHorizontal();
            Teleport("Village", -19f, -160f);
            Teleport("Forest", 0f, 60f);
            Teleport("River", -24f, -66f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Teleport("Ruins", 160f, 40f);
            Teleport("DangerZone", 168f, 118f);
            Teleport("Waterfall", -196f, -36f);
            GUILayout.EndHorizontal();

            GUILayout.Label("STREAMING", _header);
            var s = _world.Sectors;
            s.ShowBounds = GUILayout.Toggle(s.ShowBounds, " Show streaming bounds (green loaded / yellow loading / red unloaded)");
            foreach (var e in s.Entries)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{e.Def.Name} [{e.Model.State}]{(e.Model.Pinned ? " pinned" : "")}", _small, GUILayout.Width(250f));
                if (GUILayout.Button("Load", GUILayout.Width(55f))) s.Pin(e.Def.Id, true);
                if (GUILayout.Button("Unload", GUILayout.Width(60f))) s.Suppress(e.Def.Id);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("WEATHER / SAVE", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear")) _world.Time.SetWeather(WeatherState.Clear);
            if (GUILayout.Button("Light fog")) _world.Time.SetWeather(WeatherState.LightFog);
            if (GUILayout.Button("Save world")) _world.SaveWorld("lab", true);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Discovered: {string.Join(", ", _world.State.Data.discoveredAreas)}", _small);
        }

        private void Teleport(string label, float x, float z)
        {
            if (!GUILayout.Button(label)) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;
            var target = new Vector3(x, 0f, z);
            _world.Sectors.LoadAround(target, false);
            target.y = RegionTerrain.SampleHeight(x, z) + 0.3f;
            pc.Motor.Teleport(target);
        }

        private void DrawNpcs()
        {
            GUILayout.Label("NPC — routine / task / destination / sector / alive / event", _header);
            foreach (var a in _world.Npcs.Agents)
            {
                var s = a.State;
                var sector = L.SectorAt(s.X, s.Z);
                string task = s.Indoors ? "inside" : s.Travelling ? $"walking ({s.Route?.Travelled:0}/{s.Route?.Length:0} m)" : s.Activity.ToString();
                var hunter = a.GetComponent<HunterBrain>();
                GUILayout.Label($"{s.Def.Name} [{s.Def.Role}] — {s.Entry.Activity}@{s.Entry.Start:0.0}h · {task} → {s.Place ?? "-"} · {(sector != null ? sector.Id : "-")} · " +
                                $"{(s.Alive ? s.Injured ? "INJURED" : "alive" : "DEAD")} · {s.Mode}{(s.EventId != null ? " · event " + s.EventId : "")}" +
                                $"{(a.Shown ? $" · shown {a.DistanceToPlayer:0} m" : "")}{(hunter != null ? $" · hunter: {hunter.Status}" : "")}", _small);
            }
            GUILayout.Label($"Body builds {_world.Npcs.BuiltCount} · route plans {_world.Npcs.Sim.Replans} · snaps {_world.Npcs.Sim.Snaps} · stuck recoveries {_world.Npcs.StuckRecoveries}", _small);
        }

        private void DrawDemons()
        {
            var d = _world.Demons;
            GUILayout.BeginHorizontal();
            d.AmbientSpawns = GUILayout.Toggle(d.AmbientSpawns, " ambient spawns");
            if (GUILayout.Button("Spawn near (out of view)"))
            {
                var spot = d.HiddenSpot(_world.PlayerPosition, 40f, 60f);
                if (spot.HasValue) d.Spawn(spot.Value, L.SectorAt(spot.Value.x, spot.Value.z)?.Id ?? "forest_road");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Spawned {d.Spawned} · escapes {d.Escapes} · presence {(d.Presence ? "ON" : "off")}", _small);
            GUILayout.Label("ACTIVE — state / target / territory / health / escaped / encounters", _header);
            foreach (var a in d.Active)
            {
                if (a == null || a.Enemy == null) continue;
                var e = a.Enemy;
                float dist = Vector3.Distance(a.transform.position, _world.PlayerPosition);
                GUILayout.Label($"{a.Record.persistentId}: {a.Mode}/{e.StateId} · target {a.TargetName} · home {a.Sector} ({Vector3.Distance(a.transform.position, a.Home):0} m) · " +
                                $"HP {e.Damageable.Health.Normalized * 100f:0}% · escaped {a.Record.escaped} · enc {a.Record.encounters} · {dist:0} m" +
                                $"{(a.EventId != null ? " · event " + a.EventId : "")}", _small);
            }
            GUILayout.Label("REMEMBERED", _header);
            int shown = 0;
            foreach (var r in _world.State.Data.demons)
            {
                if (shown++ > 20) break;
                GUILayout.Label($"{r.persistentId}: {(r.alive ? "alive" : "slain")} · {r.lastSector} · HP {r.health * 100f:0}% · escaped {r.escaped} · scar {r.scarred} · enc {r.encounters} · saw [{string.Join(",", r.observedBreathing)}]", _small);
            }
        }

        private void DrawEvents()
        {
            var ev = _world.Events;
            GUILayout.Label("SPAWN EVENT (nearest spot to the player)", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Caravan Attack")) ev.ForceStart(FrontierEvents.CaravanAttack);
            if (GUILayout.Button("Family Pursuit")) ev.ForceStart(FrontierEvents.FamilyPursued);
            if (GUILayout.Button("Hunter Duel")) ev.ForceStart(FrontierEvents.HunterDuel);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Village Attack")) ev.ForceStart(FrontierEvents.VillageAttack);
            if (GUILayout.Button("Rare Demon")) ev.ForceStart(FrontierEvents.RareDemon);
            if (GUILayout.Button("Wounded Hunter")) ev.ForceStart(FrontierEvents.WoundedHunter);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Lost Child")) ev.ForceStart(FrontierEvents.LostChild);
            if (GUILayout.Button("Discovery")) ev.ForceStart(FrontierEvents.Discovery);
            if (GUILayout.Button("Trigger Exceptional Presence")) ev.TriggerExceptionalPresence();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset World Events")) ev.ResetAll();

            GUILayout.Label("STATE", _header);
            double now = _world.Now;
            foreach (var def in ev.Defs)
            {
                var r = _world.State.Event(def.Id);
                string dl = r.IsActive ? $" · soft in {(r.softDeadline - now) * 60:0} min · hard in {(r.hardDeadline - now) * 60:0} min" : r.state == WorldEventState.Expired ? $" · cooldown {(r.cooldownUntil - now):0.0} h" : "";
                bool staged = ev.StagedEvents.ContainsKey(def.Id);
                GUILayout.Label($"{def.Title}: {r.state}{(staged ? " (staged)" : "")} · ran {r.triggerCount} · ok {r.successes} / failed {r.failures}{dl}", _small);
            }
            GUILayout.Label("LOG", _header);
            for (int i = ev.Log.Count - 1; i >= 0 && i >= ev.Log.Count - 14; i--) GUILayout.Label(ev.Log[i], _small);
            GUILayout.Label("FACTS", _header);
            var facts = new List<string>();
            foreach (var f in _world.State.Data.facts) if (!f.key.StartsWith("told_") && !f.key.StartsWith("herb_")) facts.Add($"{f.key}={f.value}");
            GUILayout.Label(string.Join("  ", facts), _small);
        }
    }

    /// <summary>
    /// Simple world map (M): the region drawn from the terrain colours, only the areas already discovered are
    /// revealed (the rest stays in fog), discovered places are named, the player is a marker. Secret places never
    /// show until visited, and nothing marks events.
    /// </summary>
    public sealed class WorldMap : MonoBehaviour
    {
        public bool Open { get; private set; }
        private LivingWorld _world;
        private Texture2D _fog;
        private float _nextFog;
        private GUIStyle _label, _title;

        public static WorldMap Create(LivingWorld world)
        {
            var go = new GameObject("WorldMap");
            go.transform.SetParent(world.transform, false);
            var m = go.AddComponent<WorldMap>();
            m._world = world;
            return m;
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame && !Core.DebugMenu.IsOpen && !UI.PauseMenu.IsOpen) Open = !Open;
            if (Open && kb != null && kb.escapeKey.wasPressedThisFrame) Open = false;
        }

        private void RebuildFog()
        {
            const int n = 64;
            if (_fog == null) _fog = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "BoE_MapFog" };
            var px = new Color32[n * n];
            var known = new List<SectorDef>();
            foreach (var s in L.Sectors) if (_world.State.IsDiscovered("sector:" + s.Id)) known.Add(s);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = -L.HalfSize + (i + 0.5f) / n * 2f * L.HalfSize;
                float z = -L.HalfSize + (j + 0.5f) / n * 2f * L.HalfSize;
                float fog = 1f;
                foreach (var s in known) fog = Mathf.Min(fog, Mathf.Clamp01(s.Bounds.Distance(x, z) / 25f));
                px[j * n + i] = new Color32(14, 14, 24, (byte)(fog * 235f));
            }
            _fog.SetPixels32(px);
            _fog.Apply(false);
        }

        private void OnGUI()
        {
            if (!Open || _world == null || RegionTerrain.ColorMap == null) return;
            if (Time.unscaledTime >= _nextFog || _fog == null)
            {
                _nextFog = Time.unscaledTime + 1f;
                RebuildFog();
            }
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _label.normal.textColor = new Color(1f, 0.96f, 0.86f);
                _title = new GUIStyle(_label) { fontSize = 22 };
            }
            float size = Mathf.Min(Screen.width, Screen.height) * 0.82f;
            var rect = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // The colour map has +Z at v=1: GUI y grows down, so draw flipped.
            GUI.DrawTextureWithTexCoords(rect, RegionTerrain.ColorMap, new Rect(0f, 1f, 1f, -1f));
            GUI.DrawTextureWithTexCoords(rect, _fog, new Rect(0f, 1f, 1f, -1f));
            Vector2 ToScreen(float x, float z) => new Vector2(rect.x + (x + L.HalfSize) / (2f * L.HalfSize) * rect.width, rect.y + (1f - (z + L.HalfSize) / (2f * L.HalfSize)) * rect.height);

            foreach (var s in L.Sectors)
            {
                if (!_world.State.IsDiscovered("sector:" + s.Id)) continue;
                var c = ToScreen((s.Bounds.MinX + s.Bounds.MaxX) * 0.5f, (s.Bounds.MinZ + s.Bounds.MaxZ) * 0.5f);
                GUI.Label(new Rect(c.x - 90f, c.y - 10f, 180f, 20f), s.Name, _label);
            }
            foreach (var d in L.Discoveries)
            {
                if (!_world.State.IsDiscovered(d.Id)) continue;
                var c = ToScreen(d.X, d.Z);
                GUI.Label(new Rect(c.x - 80f, c.y + 6f, 160f, 18f), "◆ " + d.Name, _label);
            }
            var p = _world.PlayerPosition;
            var pp = ToScreen(p.x, p.z);
            GUI.color = new Color(1f, 0.85f, 0.3f);
            GUI.DrawTexture(new Rect(pp.x - 6f, pp.y - 6f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x, rect.y - 34f, rect.width, 30f), $"{L.RegionName} — {_world.Time.Describe()}  (M to close)", _title);
        }
    }
}
