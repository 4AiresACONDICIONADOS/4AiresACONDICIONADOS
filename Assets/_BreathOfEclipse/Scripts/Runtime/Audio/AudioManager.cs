using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.Audio
{
    /// <summary>
    /// Central audio: pooled SFX sources, music with crossfades, ambient loops and category volumes
    /// (Master, Music, SFX, Voice, Ambient) driven by <see cref="GameSettings"/>.
    /// Real clips come from <see cref="AudioLibraryData"/>; missing ids use procedural placeholders.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public AudioLibraryData Library;

        private const int SourceCount = 28;
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly Dictionary<AudioSource, AudioCategory> _sourceCategory = new Dictionary<AudioSource, AudioCategory>();
        private readonly Dictionary<AudioSource, float> _sourceBaseVolume = new Dictionary<AudioSource, float>();
        private readonly Dictionary<string, AudioClip> _generated = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip> _music = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, Task<float[]>> _musicTasks = new Dictionary<string, Task<float[]>>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private int _next;

        private AudioSource _musicA, _musicB, _ambient;
        private bool _musicAActive = true;
        private string _currentMusic;
        private string _pendingMusic;
        private float _musicFade = 1.5f;
        private Coroutine _musicRoutine;

        private float _master = 1f, _musicVol = 1f, _sfx = 1f, _voice = 1f, _ambientVol = 1f;

        private static readonly string[] Prewarm =
        {
            "slash", "slash_heavy", "whoosh", "dash", "dodge", "hit", "hit_heavy", "hit_crit", "block", "parry", "perfect_dodge",
            "jump", "land", "water", "water_big", "fire", "fire_big", "explosion", "thunder", "thunder_big", "wind", "wind_big",
            "moon", "moon_big", "charge", "ultimate", "callout", "enemy_swipe", "enemy_roar", "boss_roar", "enemy_death",
            "telegraph", "crate_break", "breath_full", "ui_click", "ui_hover", "ui_confirm"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Services.Register(this);
            for (int i = 0; i < SourceCount; i++)
            {
                // One GameObject per source: 3D sounds need their own position.
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0.6f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = 60f;
                s.dopplerLevel = 0f;
                _sources.Add(s);
            }
            _musicA = CreateLoop("MusicA");
            _musicB = CreateLoop("MusicB");
            _ambient = CreateLoop("Ambient");
            SaveSystem.SettingsChanged += ApplySettings;
            ApplySettings(SaveSystem.Settings);
        }

        private AudioSource CreateLoop(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        private void Start() => StartCoroutine(PrewarmRoutine());

        private IEnumerator PrewarmRoutine()
        {
            // Spread synthesis over frames so boot never hitches.
            foreach (var id in Prewarm)
            {
                GetClip(id);
                yield return null;
            }
        }

        private void OnDestroy()
        {
            SaveSystem.SettingsChanged -= ApplySettings;
            if (Instance != this) return;
            Instance = null;
            Services.Unregister(this);
        }

        private void ApplySettings(GameSettings s)
        {
            _master = s.masterVolume;
            _musicVol = s.musicVolume;
            _sfx = s.sfxVolume;
            _voice = s.voiceVolume;
            _ambientVol = s.ambientVolume;
            AudioListener.volume = 1f;
            foreach (var src in _sources)
            {
                if (!src.isPlaying) continue;
                if (_sourceCategory.TryGetValue(src, out var cat) && _sourceBaseVolume.TryGetValue(src, out var baseVol))
                    src.volume = baseVol * CategoryVolume(cat);
            }
            if (_ambient.isPlaying) _ambient.volume = 0.6f * CategoryVolume(AudioCategory.Ambient);
        }

        public float CategoryVolume(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Music: return _master * _musicVol;
                case AudioCategory.SFX: return _master * _sfx;
                case AudioCategory.Voice: return _master * _voice;
                case AudioCategory.Ambient: return _master * _ambientVol;
                default: return _master;
            }
        }

        private AudioClip GetClip(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var entry = Library != null ? Library.Find(id) : null;
            if (entry != null) return entry.clips[Random.Range(0, entry.clips.Length)];
            if (_generated.TryGetValue(id, out var clip)) return clip;
            clip = ProceduralAudio.Generate(id);
            _generated[id] = clip;
            return clip;
        }

        /// <summary>Plays a sound at a world position.</summary>
        public void Play(string id, Vector3 position, float volume = 1f, float pitch = 1f, AudioCategory category = AudioCategory.SFX, float spatial = 0.6f)
        {
            var clip = GetClip(id);
            if (clip == null) return;
            // The same sound many times in one frame (multi-target hits) only plays once.
            if (_lastPlayed.TryGetValue(id, out var last) && Time.unscaledTime - last < 0.03f) return;
            _lastPlayed[id] = Time.unscaledTime;

            var src = NextSource();
            src.transform.position = position;
            src.spatialBlend = spatial;
            src.clip = clip;
            float baseVol = volume * (Library != null && Library.Find(id) is AudioLibraryData.Entry e ? e.volume : 1f);
            _sourceCategory[src] = category;
            _sourceBaseVolume[src] = baseVol;
            src.volume = baseVol * CategoryVolume(category);
            src.pitch = pitch * Random.Range(0.95f, 1.05f);
            src.Play();
        }

        public void Play2D(string id, float volume = 1f, float pitch = 1f, AudioCategory category = AudioCategory.SFX)
        {
            var cam = Camera.main;
            Play(id, cam != null ? cam.transform.position : Vector3.zero, volume, pitch, category, 0f);
        }

        private AudioSource NextSource()
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                int idx = (_next + i) % _sources.Count;
                if (!_sources[idx].isPlaying)
                {
                    _next = idx + 1;
                    return _sources[idx];
                }
            }
            var s = _sources[_next % _sources.Count];
            _next++;
            s.Stop();
            return s;
        }

        // ------------------------------------------------------------------ music

        public string CurrentMusic => _currentMusic;

        /// <summary>Diagnostics: a music source is playing.</summary>
        public bool MusicPlaying => (_musicA != null && _musicA.isPlaying) || (_musicB != null && _musicB.isPlaying);
        /// <summary>Diagnostics: volume of the active music source.</summary>
        public float MusicSourceVolume => _musicAActive ? (_musicA != null ? _musicA.volume : 0f) : (_musicB != null ? _musicB.volume : 0f);
        /// <summary>Diagnostics: number of pooled SFX sources currently playing.</summary>
        public int PlayingSfxCount
        {
            get
            {
                int n = 0;
                foreach (var s in _sources) if (s != null && s.isPlaying) n++;
                return n;
            }
        }

        public void PlayMusic(string id, float fade = 1.5f)
        {
            if (string.IsNullOrEmpty(id) || id == _currentMusic || id == _pendingMusic) return;
            _musicFade = fade;
            var libEntry = Library != null ? Library.Find(id) : null;
            if (libEntry != null)
            {
                _music[id] = libEntry.clips[0];
            }
            if (_music.ContainsKey(id))
            {
                StartCrossfade(id);
                return;
            }
            _pendingMusic = id;
            if (!_musicTasks.ContainsKey(id))
            {
                string captured = id;
                _musicTasks[id] = Task.Run(() => MusicSynth.Generate(captured));
            }
        }

        public void StopMusic(float fade = 1.5f)
        {
            _pendingMusic = null;
            _currentMusic = null;
            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(FadeOutBoth(fade));
        }

        private IEnumerator FadeOutBoth(float fade)
        {
            float a0 = _musicA.volume, b0 = _musicB.volume;
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - t / fade;
                _musicA.volume = a0 * k;
                _musicB.volume = b0 * k;
                yield return null;
            }
            _musicA.Stop();
            _musicB.Stop();
        }

        private void Update()
        {
            if (_pendingMusic != null && _musicTasks.TryGetValue(_pendingMusic, out var task) && task.IsCompleted)
            {
                string id = _pendingMusic;
                _pendingMusic = null;
                if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
                {
                    var clip = AudioClip.Create("BoE_Music_" + id, task.Result.Length, 1, MusicSynth.SampleRate, false);
                    clip.SetData(task.Result, 0);
                    _music[id] = clip;
                    StartCrossfade(id);
                }
            }

            // Keep music volume in sync with the settings slider.
            if (_musicRoutine == null)
            {
                var active = _musicAActive ? _musicA : _musicB;
                if (active.isPlaying) active.volume = 0.8f * CategoryVolume(AudioCategory.Music);
            }
        }

        private void StartCrossfade(string id)
        {
            _currentMusic = id;
            var from = _musicAActive ? _musicA : _musicB;
            var to = _musicAActive ? _musicB : _musicA;
            _musicAActive = !_musicAActive;
            to.clip = _music[id];
            to.volume = 0f;
            to.Play();
            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(Crossfade(from, to, _musicFade));
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to, float duration)
        {
            float t = 0f;
            float fromStart = from.volume;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float target = 0.8f * CategoryVolume(AudioCategory.Music);
                to.volume = target * k;
                from.volume = fromStart * (1f - k);
                yield return null;
            }
            from.Stop();
            _musicRoutine = null;
        }

        // ------------------------------------------------------------------ ambient

        /// <summary>Plays a looping ambience clip from the library (e.g. "ambient_forest"). Placeholder: soft noise bed.</summary>
        public void PlayAmbient(string id)
        {
            var entry = Library != null ? Library.Find(id) : null;
            AudioClip clip = entry != null ? entry.clips[0] : GetAmbientPlaceholder(id);
            if (clip == null) return;
            _ambient.clip = clip;
            _ambient.volume = 0.6f * CategoryVolume(AudioCategory.Ambient);
            _ambient.Play();
        }

        public void StopAmbient() => _ambient.Stop();

        private AudioClip GetAmbientPlaceholder(string id)
        {
            string key = "amb_" + id;
            if (_generated.TryGetValue(key, out var c)) return c;
            const int sr = 22050;
            int n = sr * 8;
            var data = new float[n];
            var rng = new System.Random(id.GetHashCode());
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.02f * (w - lp);
                float wind = lp * (0.6f + 0.4f * Mathf.Sin(t * 0.7f));
                // Crickets: short high chirps in groups.
                float chirpPhase = (t * 2.3f) % 1f;
                float chirp = chirpPhase < 0.12f ? Mathf.Sin(2f * Mathf.PI * 4300f * t) * Mathf.Sin(chirpPhase / 0.12f * Mathf.PI) * 0.08f : 0f;
                data[i] = wind * 1.5f + chirp;
            }
            // Loop crossfade.
            int xf = sr / 2;
            for (int i = 0; i < xf; i++)
            {
                float k = i / (float)xf;
                data[i] = data[i] * k + data[n - xf + i] * (1f - k);
            }
            var trimmed = new float[n - xf];
            System.Array.Copy(data, trimmed, trimmed.Length);
            c = AudioClip.Create("BoE_" + key, trimmed.Length, 1, sr, false);
            c.SetData(trimmed, 0);
            _generated[key] = c;
            return c;
        }
    }

    /// <summary>Static shortcuts for gameplay code.</summary>
    public static class Sfx
    {
        public static void Play(string id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (AudioManager.Instance != null && !string.IsNullOrEmpty(id)) AudioManager.Instance.Play(id, position, volume, pitch);
        }

        public static void Play2D(string id, float volume = 1f, float pitch = 1f, AudioCategory category = AudioCategory.SFX)
        {
            if (AudioManager.Instance != null && !string.IsNullOrEmpty(id)) AudioManager.Instance.Play2D(id, volume, pitch, category);
        }

        public static void Music(string id, float fade = 1.5f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(id, fade);
        }
    }
}
