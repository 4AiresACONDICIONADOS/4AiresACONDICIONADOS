using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.Breathing
{
    /// <summary>
    /// The swordsman announces his techniques: "Respiración del Agua… Séptima Postura… ¡Serpiente Ascendente!".
    /// Style call → form call → technique name, spoken back to back from the moment the technique starts (the
    /// gameplay never waits for the voice). The style is only named again after a pause in combat. Forms,
    /// techniques and ultimates speak; normal attacks never do. Interrupted before its first strike, the line is
    /// cut; after it, the current phrase finishes and the rest is dropped. Also drives subtitles, the small title
    /// card and a slight music / ambience duck. Clips come from <see cref="VoiceLibrary"/>; missing clips keep the
    /// subtitles and play the generic callout sound.
    /// </summary>
    public sealed class BreathingVoiceSystem : MonoBehaviour
    {
        private const float StyleCallCooldown = 12f;
        /// <summary>The technique name may land this long after the first strike (the effect is still on screen).</summary>
        private const float NameLinger = 0.9f;
        private const float DuckLevel = 0.55f;

        private BreathingStyleSystem _breathing;
        private VoiceChannel _voice;
        private readonly List<VoiceChannel.Line> _lines = new List<VoiceChannel.Line>();
        private readonly List<string> _spoken = new List<string>();
        private string _lastStyleId;
        private float _lastStyleCallTime = -100f;
        private float _announceTime = -100f;
        private float _firstStrikeDelay;
        private Color _color = Color.white;

        public static BreathingVoiceSystem Attach(BreathingStyleSystem breathing)
        {
            var v = breathing.gameObject.AddComponent<BreathingVoiceSystem>();
            v._breathing = breathing;
            v._voice = VoiceChannel.Create(breathing.transform, false);
            v._voice.LineStarted += v.OnLineStarted;
            v._voice.Finished += v.OnFinished;
            breathing.TechniqueAnnounced += v.OnTechnique;
            breathing.Executor.Interrupted += v.OnInterrupted;
            return v;
        }

        private void OnDestroy()
        {
            if (_breathing == null) return;
            _breathing.TechniqueAnnounced -= OnTechnique;
            if (_breathing.Executor != null) _breathing.Executor.Interrupted -= OnInterrupted;
        }

        private void OnTechnique(BreathingStyleData style, BreathingForm form, SkillData skill)
        {
            var settings = SaveSystem.Settings;
            _color = Color.Lerp(Color.white, style.baseColor, 0.35f);
            ShowTitle(style, form, skill, settings);

            bool ultimate = skill.tier == SkillTier.Ultimate;
            string formCall = !string.IsNullOrEmpty(skill.voiceFormCall) ? skill.voiceFormCall : form != null ? Roman.SpanishFormCall(form.formNumber) : "";
            string nameCall = skill.voiceTechniqueCall;
            bool callStyle = ultimate || style.styleId != _lastStyleId || Time.unscaledTime - _lastStyleCallTime > StyleCallCooldown;
            if (string.IsNullOrEmpty(style.styleCall)) callStyle = false;

            string lang = settings.voiceLanguage;
            float strike = FirstStrikeDelay(skill);
            float formAnchor = CueStart(skill, VoiceCue.Form);
            float nameAnchor = CueStart(skill, VoiceCue.Name);
            // Longest phrasing whose name still lands while the technique is on screen; the attack never waits.
            // Full → "Respiración del Agua… ¡…!" (a style call is only due on first use / style switch, where naming the
            // style matters most) → "Séptima Postura… ¡Serpiente Ascendente!" → just the name.
            bool[][] options = { new[] { true, true }, new[] { true, false }, new[] { false, true }, new[] { false, false } };
            float deadline = strike + (ultimate ? 2.5f : NameLinger);
            foreach (var o in options)
            {
                bool withStyle = o[0] && callStyle, withForm = o[1] && !string.IsNullOrEmpty(formCall);
                if (o[0] && !callStyle) continue;
                BuildLines(style.styleCall, withStyle, formCall, withForm, nameCall, lang, formAnchor, nameAnchor);
                if (string.IsNullOrEmpty(nameCall) || NameStart() <= deadline) break;
            }
            if (_lines.Count == 0) return;
            callStyle = !string.IsNullOrEmpty(style.styleCall) && _lines[0].Text.StartsWith(style.styleCall.Trim());

            if (callStyle)
            {
                _lastStyleId = style.styleId;
                _lastStyleCallTime = Time.unscaledTime;
            }
            _announceTime = Time.unscaledTime;
            _firstStrikeDelay = FirstStrikeDelay(skill);
            _spoken.Clear();

            bool voiced = settings.techniqueVoice;
            bool anyClip = false;
            if (voiced)
                foreach (var l in _lines) anyClip |= l.Clip != null;
            if (!voiced || !anyClip)
            {
                // No voice clips (or voice off): the generic callout keeps the moment audible.
                Sfx.Play2D("callout", 0.5f, 1f, AudioCategory.Voice);
                if (!voiced) for (int i = 0; i < _lines.Count; i++) _lines[i] = WithoutClip(_lines[i]);
            }
            float total = 0f;
            foreach (var l in _lines) total += l.Gap + (l.Clip != null ? l.Clip.length : VoiceChannel.EstimateSeconds(l.Text));
            if (voiced && anyClip && AudioManager.Instance != null) AudioManager.Instance.Duck(DuckLevel, total + 0.2f);
            _voice.Speak(_lines);
        }

        private void BuildLines(string styleCall, bool withStyle, string formCall, bool withForm, string nameCall, string lang, float formAnchor, float nameAnchor)
        {
            _lines.Clear();
            if (withStyle) Add(styleCall, lang, 0f, 0f, "…");
            if (withForm) Add(formCall, lang, _lines.Count > 0 ? 0.06f : 0f, formAnchor, string.IsNullOrEmpty(nameCall) ? "" : "…");
            if (!string.IsNullOrEmpty(nameCall)) Add(nameCall, lang, _lines.Count > 0 ? 0.03f : 0f, nameAnchor, "!", "¡");
        }

        /// <summary>When the last line (the technique name) would start with the current line list.</summary>
        private float NameStart()
        {
            float t = 0f, start = 0f;
            foreach (var l in _lines)
            {
                start = Mathf.Max(t + l.Gap, l.NotBefore);
                t = start + (l.Clip != null ? l.Clip.length : VoiceChannel.EstimateSeconds(l.Text));
            }
            return start;
        }

        private void Add(string text, string lang, float gap, float notBefore, string suffix, string prefix = "")
        {
            string display = text.Trim();
            if (!string.IsNullOrEmpty(prefix) && !display.StartsWith(prefix)) display = prefix + display;
            if (!string.IsNullOrEmpty(suffix) && !display.EndsWith(suffix)) display += suffix;
            _lines.Add(new VoiceChannel.Line { Text = display, Clip = VoiceLibrary.Get(lang, text), Gap = gap, NotBefore = notBefore });
        }

        /// <summary>Start time of the first phase tagged with <paramref name="cue"/> (0 when untagged).</summary>
        private static float CueStart(SkillData skill, VoiceCue cue)
        {
            float t = 0f;
            foreach (var p in skill.phases)
            {
                if (p.voiceCue == cue) return t;
                t += Mathf.Max(0f, p.duration);
            }
            return 0f;
        }

        private static VoiceChannel.Line WithoutClip(VoiceChannel.Line l)
        {
            l.Clip = null;
            return l;
        }

        private void OnLineStarted(int index, VoiceChannel.Line line)
        {
            _spoken.Add(line.Text);
            if (!SaveSystem.Settings.techniqueSubtitles) return;
            float remaining = 0f;
            for (int i = index; i < _lines.Count; i++)
                remaining += (i > index ? _lines[i].Gap : 0f) + (_lines[i].Clip != null ? _lines[i].Clip.length : VoiceChannel.EstimateSeconds(_lines[i].Text));
            GameEvents.RaiseTechniqueSubtitle(string.Join(" ", _spoken), remaining + 0.6f, _color);
        }

        private void OnFinished()
        {
            // Subtitles fade by themselves after their duration.
        }

        private void OnInterrupted(SkillData skill)
        {
            if (!_voice.IsSpeaking) return;
            if (Time.unscaledTime - _announceTime < _firstStrikeDelay)
            {
                // The technique never happened: cut the call.
                _voice.Stop(0.08f);
                GameEvents.RaiseTechniqueSubtitle("", 0f, _color);
            }
            else
            {
                _voice.DropQueued();
            }
        }

        private void ShowTitle(BreathingStyleData style, BreathingForm form, SkillData skill, GameSettings settings)
        {
            if (!settings.techniqueTitles) return;
            bool ultimate = skill.tier == SkillTier.Ultimate;
            string formLabel = ultimate ? "FINAL FORM" : form != null ? $"{Roman.Of(form.formNumber)} FORM" : skill.formName.ToUpperInvariant();
            GameEvents.RaiseTechniqueTitle(style.displayName, formLabel, skill.displayName.ToUpperInvariant(), _color, ultimate ? 2f : 1.2f);
        }

        /// <summary>Seconds from the start of a technique to its first hit (the moment it "happened").</summary>
        private static float FirstStrikeDelay(SkillData skill)
        {
            float t = 0f;
            foreach (var p in skill.phases)
            {
                if (p.hits != null && p.hits.Count > 0)
                {
                    float first = float.MaxValue;
                    foreach (var h in p.hits) first = Mathf.Min(first, h.delay);
                    return t + (first < float.MaxValue ? first : 0f);
                }
                t += Mathf.Max(0f, p.duration);
            }
            return t;
        }
    }
}
