using System;
using System.Threading.Tasks;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Data;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// World soundscape: looping layers per zone (village, fields, forest, river, waterfall, cursed ground, cave) and
    /// time of day (birds and insects by day, crickets, frogs and owls by night), cross-faded smoothly from where the
    /// player stands; plus positioned one-shots (smithy hammer, wood chopping, birds, crows at dusk, distant growls at
    /// night) and the bells used by events. Loops come from the audio library when present (id "amb_world_" +
    /// layer, lower case), otherwise they are synthesised on a worker thread.
    /// </summary>
    public sealed class WorldAmbience : MonoBehaviour
    {
        private static readonly int LayerCount = Enum.GetValues(typeof(AmbienceLayer)).Length;
        private static readonly float[] BaseVolume = { 0.32f, 0.5f, 0.42f, 0.32f, 0.3f, 0.36f, 0.42f, 0.5f, 0.8f, 0.6f, 0.55f };

        public static WorldAmbience Instance { get; private set; }

        /// <summary>Optional: the NPC system tells whether a worker is at a work spot ("tetsuo" at the forge…).</summary>
        public Func<string, bool> IsWorking;
        /// <summary>Extra threat (demons nearby, village attack): pushes the danger layer and quiets wildlife.</summary>
        public float Threat { get; set; }

        private LivingWorld _world;
        private AudioSource[] _sources;
        private float[] _volume;
        private Task<float[]>[] _tasks;
        private float _nextWeights, _nextOneShot = 3f, _nextWork = 2f;
        private readonly float[] _target = new float[16];
        private System.Random _rng = new System.Random(4242);

        public static WorldAmbience Create(LivingWorld world)
        {
            var go = new GameObject("WorldAmbience");
            go.transform.SetParent(world.transform, false);
            var a = go.AddComponent<WorldAmbience>();
            a._world = world;
            return a;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _sources = new AudioSource[LayerCount];
            _volume = new float[LayerCount];
            _tasks = new Task<float[]>[LayerCount];
            var lib = AudioManager.Instance != null ? AudioManager.Instance.Library : null;
            for (int i = 0; i < LayerCount; i++)
            {
                var go = new GameObject("Amb_" + (AmbienceLayer)i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
                s.dopplerLevel = 0f;
                _sources[i] = s;
                var entry = lib != null ? lib.Find("amb_world_" + ((AmbienceLayer)i).ToString().ToLowerInvariant()) : null;
                if (entry != null && entry.clips != null && entry.clips.Length > 0 && entry.clips[0] != null) StartLoop(i, entry.clips[0]);
                else
                {
                    var layer = (AmbienceLayer)i;
                    _tasks[i] = Task.Run(() => WorldAmbienceSynth.Render(layer));
                }
            }
        }

        private void Start()
        {
            // The world owns the ambience: stop the generic scene bed (menu forest loop).
            if (AudioManager.Instance != null) AudioManager.Instance.StopAmbient();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void StartLoop(int i, AudioClip clip)
        {
            var s = _sources[i];
            s.clip = clip;
            s.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, clip.length - 0.1f));
            s.Play();
        }

        private void Update()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                var task = _tasks[i];
                if (task == null || !task.IsCompleted) continue;
                _tasks[i] = null;
                if (task.IsFaulted || task.Result == null)
                {
                    Debug.LogWarning($"[WorldAmbience] Layer {(AmbienceLayer)i} could not be synthesised.");
                    continue;
                }
                var data = task.Result;
                var clip = AudioClip.Create("BoE_amb_" + (AmbienceLayer)i, data.Length, 1, WorldAmbienceSynth.SampleRate, false);
                clip.SetData(data, 0);
                StartLoop(i, clip);
            }

            if (Time.unscaledTime >= _nextWeights)
            {
                _nextWeights = Time.unscaledTime + 0.2f;
                ComputeTargets();
            }
            float category = AudioManager.Instance != null ? AudioManager.Instance.CategoryVolume(AudioCategory.Ambient) : 1f;
            for (int i = 0; i < LayerCount; i++)
            {
                _volume[i] = Mathf.MoveTowards(_volume[i], _target[i], Time.unscaledDeltaTime * 0.35f);
                var s = _sources[i];
                s.volume = _volume[i] * BaseVolume[i] * category;
                if (s.clip != null && !s.isPlaying && s.volume > 0.001f) s.Play();
            }
            UpdateOneShots();
        }

        // ------------------------------------------------------------------ zones

        private Vector3 Listener
        {
            get
            {
                var cam = Camera.main;
                return cam != null ? cam.transform.position : _world != null ? _world.PlayerPosition : Vector3.zero;
            }
        }

        private float ZoneWeight(AmbienceZone zone, Vector3 p, float fade)
        {
            float w = 0f;
            foreach (var s in L.Sectors)
            {
                if (s.Ambience != zone) continue;
                w = Mathf.Max(w, 1f - Mathf.Clamp01(s.Bounds.Distance(p.x, p.z) / fade));
            }
            return w;
        }

        public float Night
        {
            get
            {
                var t = _world != null ? _world.Time : null;
                return t != null ? 1f - Mathf.SmoothStep(0.15f, 0.6f, t.Daylight) : 1f;
            }
        }

        private void ComputeTargets()
        {
            Vector3 p = Listener;
            float night = Night;
            float day = 1f - night;
            float village = Mathf.Max(ZoneWeight(AmbienceZone.Village, p, 30f), 1f - Mathf.Clamp01((new Vector2(p.x - L.VillageX, p.z - L.VillageZ).magnitude - 45f) / 40f));
            float fields = ZoneWeight(AmbienceZone.Fields, p, 30f) * (1f - village * 0.5f);
            float forest = ZoneWeight(AmbienceZone.Forest, p, 35f) * (1f - village);
            float danger = Mathf.Max(ZoneWeight(AmbienceZone.Danger, p, 45f), Threat);
            float river = p.x > L.WaterfallX - 15f ? 1f - Mathf.SmoothStep(0f, 1f, (L.RiverDistance(p.x, p.z) - 6f) / 40f) : 0f;
            var wf = L.Place("waterfall");
            float waterfall = wf != null ? 1f - Mathf.SmoothStep(0f, 1f, (new Vector2(p.x - wf.X, p.z - wf.Z).magnitude - 8f) / 70f) : 0f;
            var cave = L.Place("cave");
            float inCave = cave != null ? 1f - Mathf.SmoothStep(0f, 1f, (new Vector2(p.x - cave.X, p.z - cave.Z).magnitude - 5f) / 10f) : 0f;
            float outside = 1f - inCave * 0.85f;
            float wildlife = 1f - Mathf.Clamp01(danger * 1.2f); // creatures fall silent around demons

            _target[(int)AmbienceLayer.Wind] = (0.45f + 0.4f * Mathf.Clamp01((p.y - 8f) / 20f)) * outside;
            _target[(int)AmbienceLayer.ForestDay] = forest * day * wildlife * outside;
            _target[(int)AmbienceLayer.ForestNight] = forest * night * wildlife * outside;
            _target[(int)AmbienceLayer.VillageDay] = village * day * Mathf.Clamp01(1f - Threat * 2f) * outside;
            _target[(int)AmbienceLayer.VillageNight] = village * night * wildlife * outside;
            _target[(int)AmbienceLayer.FieldsDay] = fields * day * wildlife * outside;
            _target[(int)AmbienceLayer.FieldsNight] = fields * night * wildlife * outside;
            _target[(int)AmbienceLayer.River] = river * outside;
            _target[(int)AmbienceLayer.Waterfall] = waterfall * outside;
            _target[(int)AmbienceLayer.Danger] = Mathf.Clamp01(danger * (0.6f + 0.4f * night));
            _target[(int)AmbienceLayer.Cave] = inCave;
        }

        // ------------------------------------------------------------------ one-shots

        private void UpdateOneShots()
        {
            var am = AudioManager.Instance;
            var time = _world != null ? _world.Time : null;
            if (am == null || time == null) return;
            float now = Time.unscaledTime;
            float hour = time.Clock.Hour;

            // Work sounds from fixed places while the workers are there.
            if (now >= _nextWork)
            {
                _nextWork = now + 0.9f + (float)_rng.NextDouble() * 1.4f;
                var listener = Listener;
                if (Working("tetsuo", hour, 8f, 17f))
                {
                    var forge = L.Place("forge");
                    var pos = RegionTerrain.OnGround(forge.X, forge.Z) + Vector3.up;
                    if ((pos - listener).sqrMagnitude < 70f * 70f) am.Play("hammer_clink", pos, 0.55f, 1f, AudioCategory.Ambient, 1f);
                }
                if (Working("kanta", hour, 8f, 16f) && _rng.NextDouble() < 0.5)
                {
                    var yard = L.Place("lumber_yard");
                    var pos = RegionTerrain.OnGround(yard.X, yard.Z) + Vector3.up;
                    if ((pos - listener).sqrMagnitude < 80f * 80f) am.Play("wood_chop", pos, 0.6f, 1f, AudioCategory.Ambient, 1f);
                }
            }

            if (now < _nextOneShot) return;
            _nextOneShot = now + 2.5f + (float)_rng.NextDouble() * 6f;
            Vector3 p = Listener;
            float night = Night;
            float danger = Mathf.Max(ZoneWeight(AmbienceZone.Danger, p, 45f), Threat);
            float forest = ZoneWeight(AmbienceZone.Forest, p, 35f);
            float fields = ZoneWeight(AmbienceZone.Fields, p, 30f);
            var cave = L.Place("cave");
            bool inCave = cave != null && new Vector2(p.x - cave.X, p.z - cave.Z).magnitude < 12f;
            float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
            float r = 18f + (float)_rng.NextDouble() * 25f;
            Vector3 at = p + new Vector3(Mathf.Cos(a) * r, 4f + (float)_rng.NextDouble() * 6f, Mathf.Sin(a) * r);

            if (inCave)
            {
                am.Play("amb_drip", p + new Vector3(Mathf.Cos(a) * 4f, 2f, Mathf.Sin(a) * 4f), 0.5f, 1f, AudioCategory.Ambient, 0.8f);
                return;
            }
            if (danger > 0.3f || night > 0.6f && _rng.NextDouble() < 0.12 + danger * 0.5)
            {
                // Before anything appears, the dark makes itself heard.
                if (_rng.NextDouble() < 0.35 + danger * 0.4) am.Play("amb_growl_far", p + new Vector3(Mathf.Cos(a) * 45f, 0f, Mathf.Sin(a) * 45f), 0.45f + danger * 0.3f, 0.85f + (float)_rng.NextDouble() * 0.3f, AudioCategory.Ambient, 0.9f);
                return;
            }
            bool dusk = hour >= 16.5f && hour < 19.5f;
            if (night > 0.6f)
            {
                if (forest > 0.4f && _rng.NextDouble() < 0.5) am.Play("amb_owl", at, 0.4f, 0.95f + (float)_rng.NextDouble() * 0.1f, AudioCategory.Ambient, 0.9f);
                else if (fields > 0.3f) am.Play("amb_frog", at - Vector3.up * 4f, 0.4f, 0.85f + (float)_rng.NextDouble() * 0.3f, AudioCategory.Ambient, 0.9f);
            }
            else if (dusk && _rng.NextDouble() < 0.5) am.Play("amb_crow", at + Vector3.up * 6f, 0.4f, 0.9f + (float)_rng.NextDouble() * 0.2f, AudioCategory.Ambient, 0.9f);
            else if (forest + fields > 0.2f || _rng.NextDouble() < 0.4) am.Play("amb_bird", at, 0.35f, 0.85f + (float)_rng.NextDouble() * 0.35f, AudioCategory.Ambient, 0.9f);
        }

        private bool Working(string npc, float hour, float from, float to)
        {
            if (IsWorking != null) return IsWorking(npc);
            return hour >= from && hour < to && Threat < 0.3f;
        }

        // ------------------------------------------------------------------ events

        /// <summary>The watchtower alarm bell: <paramref name="strikes"/> hard strikes (village attack, exceptional presence).</summary>
        public void AlarmBell(int strikes)
        {
            var tower = L.Place("watchtower");
            Vector3 pos = tower != null ? RegionTerrain.OnGround(tower.X, tower.Z) + Vector3.up * 9f : Listener;
            StartCoroutine(Strikes(pos, strikes));
        }

        private System.Collections.IEnumerator Strikes(Vector3 pos, int strikes)
        {
            for (int i = 0; i < strikes; i++)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.Play("bell_alarm", pos, 1f, 1f, AudioCategory.SFX, 0.55f);
                yield return new WaitForSeconds(0.55f);
            }
        }

        /// <summary>The shrine bell (dawn / dusk, rest).</summary>
        public void ShrineBell()
        {
            var shrine = L.Place("village_shrine");
            Vector3 pos = shrine != null ? RegionTerrain.OnGround(shrine.X, shrine.Z) + Vector3.up * 3f : Listener;
            if (AudioManager.Instance != null) AudioManager.Instance.Play("bell", pos, 0.8f, 1f, AudioCategory.Ambient, 0.5f);
        }
    }
}
