using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.Audio
{
    /// <summary>
    /// Voice clips by spoken text: Resources/BreathOfEclipse/Voice/&lt;language&gt;/&lt;variant&gt;/&lt;key&gt; (a style's own
    /// delivery, e.g. tidal/primera_postura) falling back to Resources/BreathOfEclipse/Voice/&lt;language&gt;/&lt;key&gt;. The
    /// key is the text lower-cased without accents or punctuation ("¡Serpiente Ascendente!" → serpiente_ascendente).
    /// Replace a placeholder by dropping a recorded clip with the same name into that folder.
    /// </summary>
    public static class VoiceLibrary
    {
        public const string Root = "BreathOfEclipse/Voice/";
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        public static AudioClip Get(string language, string text) => Get(language, text, null);

        /// <summary>The <paramref name="variant"/> delivery (a style id) when it exists, else the shared clip.</summary>
        public static AudioClip Get(string language, string text, string variant)
        {
            string key = Key(text);
            if (string.IsNullOrEmpty(key)) return null;
            if (!string.IsNullOrEmpty(variant))
            {
                var own = Load(Root + language + "/" + variant + "/" + key);
                if (own != null) return own;
            }
            return Load(Root + language + "/" + key);
        }

        private static AudioClip Load(string path)
        {
            if (Cache.TryGetValue(path, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(path);
            Cache[path] = clip;
            return clip;
        }

        public static string Key(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var sb = new StringBuilder(text.Length);
            bool underscore = false;
            foreach (char c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    underscore = false;
                }
                else if (sb.Length > 0 && !underscore)
                {
                    sb.Append('_');
                    underscore = true;
                }
            }
            if (underscore) sb.Length--;
            return sb.ToString();
        }
    }

    /// <summary>
    /// One voice per character: plays a short sequence of lines (e.g. style → form → technique) back to back,
    /// interrupting whatever that character was saying. 2D for the local player, 3D for enemies.
    /// </summary>
    public sealed class VoiceChannel : MonoBehaviour
    {
        public struct Line
        {
            public string Text;
            public AudioClip Clip;
            /// <summary>Silence before this line (seconds).</summary>
            public float Gap;
            /// <summary>Earliest start, in seconds after <see cref="VoiceChannel.Speak"/> (sync with a technique phase).</summary>
            public float NotBefore;
            /// <summary>Loudness of this line (0 = 1): calls build up towards the technique name.</summary>
            public float Volume;
        }

        /// <summary>(line index, line) when a line starts.</summary>
        public event Action<int, Line> LineStarted;
        /// <summary>The last line finished (not raised after <see cref="Stop"/>).</summary>
        public event Action Finished;

        public bool IsSpeaking => _index < _lines.Count || _busy;

        private readonly float[] _levelBuffer = new float[256];

        /// <summary>Loudness of the line being spoken (RMS of the output, ~0..0.5): drives the character's mouth.</summary>
        public float Level
        {
            get
            {
                if (_source == null || !_source.isPlaying) return 0f;
                _source.GetOutputData(_levelBuffer, 0);
                float sum = 0f;
                for (int i = 0; i < _levelBuffer.Length; i++) sum += _levelBuffer[i] * _levelBuffer[i];
                return Mathf.Sqrt(sum / _levelBuffer.Length);
            }
        }

        private AudioSource _source;
        /// <summary>Carries the previous line while it fades out when a new call interrupts it (no clicks).</summary>
        private AudioSource _tail;
        private float _tailFrom, _tailStart;
        private const float TailFade = 0.08f;
        private readonly List<Line> _lines = new List<Line>();
        private int _index;
        private float _nextAt;
        private float _speakStart;
        private bool _busy;
        private float _busyUntil;
        private float _fadeFrom, _fadeStart, _fadeDuration;
        private bool _fading;
        private float _baseVolume = 1f;
        private float _lineVolume = 1f;

        public static VoiceChannel Create(Transform owner, bool positional, float volume = 1f)
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var ch = go.AddComponent<VoiceChannel>();
            ch._source = NewSource(go, positional);
            ch._tail = NewSource(go, positional);
            ch._baseVolume = volume;
            return ch;
        }

        private static AudioSource NewSource(GameObject go, bool positional)
        {
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = positional ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 3f;
            src.maxDistance = 45f;
            src.priority = 16;
            return src;
        }

        /// <summary>
        /// Speaks <paramref name="lines"/> in order; lines without a clip are skipped (their text still raises
        /// LineStarted). One call per character: a line still playing fades out quickly instead of overlapping.
        /// </summary>
        public void Speak(IEnumerable<Line> lines)
        {
            if (_source.isPlaying)
            {
                var old = _source;
                _source = _tail;
                _tail = old;
                _tailFrom = _tail.volume;
                _tailStart = Time.unscaledTime;
            }
            _source.Stop();
            _fading = false;
            _lines.Clear();
            _lines.AddRange(lines);
            _index = 0;
            _busy = false;
            _speakStart = Time.unscaledTime;
            _nextAt = _speakStart + (_lines.Count > 0 ? Mathf.Max(_lines[0].Gap, _lines[0].NotBefore) : 0f);
        }

        /// <summary>Stops now (fade in seconds) and drops the queued lines.</summary>
        public void Stop(float fade = 0.08f)
        {
            _lines.Clear();
            _index = 0;
            _busy = false;
            if (!_source.isPlaying) return;
            _fading = true;
            _fadeFrom = _source.volume;
            _fadeStart = Time.unscaledTime;
            _fadeDuration = Mathf.Max(0.01f, fade);
        }

        /// <summary>Lets the current line finish but drops the ones after it.</summary>
        public void DropQueued()
        {
            if (_index < _lines.Count) _lines.RemoveRange(_index, _lines.Count - _index);
        }

        private void Update()
        {
            if (_tail.isPlaying)
            {
                float k = 1f - Mathf.Clamp01((Time.unscaledTime - _tailStart) / TailFade);
                _tail.volume = _tailFrom * k;
                if (k <= 0f) _tail.Stop();
            }
            float volume = _baseVolume * (AudioManager.Instance != null ? AudioManager.Instance.CategoryVolume(AudioCategory.Voice) : 1f);
            if (_fading)
            {
                float k = 1f - Mathf.Clamp01((Time.unscaledTime - _fadeStart) / _fadeDuration);
                _source.volume = _fadeFrom * k;
                if (k <= 0f)
                {
                    _source.Stop();
                    _fading = false;
                }
                return;
            }
            _source.volume = volume * _lineVolume;

            float now = Time.unscaledTime;
            if (_busy)
            {
                // A line is playing (or, without a clip, being "read" for its estimated length).
                if (_source.isPlaying || now < _busyUntil) return;
                _busy = false;
                if (_index >= _lines.Count)
                {
                    Finished?.Invoke();
                    return;
                }
                _nextAt = Mathf.Max(now + _lines[_index].Gap, _speakStart + _lines[_index].NotBefore);
            }
            if (_index >= _lines.Count || now < _nextAt) return;

            var line = _lines[_index];
            _index++;
            _busy = true;
            _lineVolume = line.Volume > 0f ? Mathf.Clamp01(line.Volume) : 1f;
            if (line.Clip != null)
            {
                _source.volume = volume * _lineVolume;
                _source.clip = line.Clip;
                _source.Play();
                _busyUntil = now;
            }
            else
            {
                _busyUntil = now + EstimateSeconds(line.Text);
            }
            LineStarted?.Invoke(_index - 1, line);
        }

        public static float EstimateSeconds(string text) => string.IsNullOrEmpty(text) ? 0f : 0.25f + text.Length * 0.055f;
    }
}
