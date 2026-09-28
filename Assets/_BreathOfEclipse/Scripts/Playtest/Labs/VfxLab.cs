using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BreathOfEclipse.AI;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.UI;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// VFX LAB: cast any of the 25 existing techniques on the training dummy, repeat automatically (2 / 3 / 5 s),
    /// change playback speed (0.25x–1.5x), freeze time and advance frame by frame. Casting goes through the same
    /// simulated Skill keys as the auto test. Lab-only staging: before each cast the player can be put back on
    /// the stage mark so every repetition is framed the same way.
    /// </summary>
    public sealed class VfxLab : PlaytestLab
    {
        private static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 1.5f };
        private static readonly float[] Intervals = { 0f, 2f, 3f, 5f };
        private static readonly BufferedAction[] SuppressedButtons =
        {
            BufferedAction.LightAttack, BufferedAction.HeavyAttack, BufferedAction.Skill1, BufferedAction.Skill2,
            BufferedAction.Skill3, BufferedAction.Skill4, BufferedAction.Ultimate
        };

        public override string Title => "VFX LAB";

        /// <summary>Preset applied on begin: style id + slot + repeat interval (Rising Serpent visual test).</summary>
        public string PresetStyle = "tidal";
        public int PresetSlot = -1;
        public float PresetInterval = 0f;

        private int _style;
        private int _slot;
        private float _interval;
        private bool _resetStage = true;
        private bool _casting;
        private float _lastCastStart = -100f;
        private Vector3 _mark;
        private TrainingDummy _dummy;
        private bool _staged;
        private string _status = "Staging...";
        private readonly List<string> _lastPhases = new List<string>();
        private string _lastVfx = "";
        private string _lastHits = "";
        private float _lastDuration;
        private int _casts;
        private readonly Dictionary<string, (int casts, int ok)> _stats = new Dictionary<string, (int, int)>();
        private Coroutine _castRoutine;
        private Vector2 _scroll;

        protected override void OnBegin()
        {
            var pc = Ctx.Player;
            var styles = pc.Breathing.Styles.ToList();
            _style = Mathf.Max(0, styles.FindIndex(s => s.styleId == PresetStyle));
            _slot = PresetSlot >= 0 ? PresetSlot : 0;
            _interval = PresetInterval;
            var input = InputReader.Instance;
            if (input != null)
            {
                input.PhysicalLookSuppressed = true;
                foreach (var b in SuppressedButtons) input.SetPhysicalPressSuppressed(b, true);
            }
            CursorManager.SetGameplay(false);
            StartCoroutine(Stage());
        }

        public override void End()
        {
            if (_castRoutine != null) StopCoroutine(_castRoutine);
            var input = InputReader.Instance;
            if (input != null)
            {
                input.PhysicalLookSuppressed = false;
                foreach (var b in SuppressedButtons) input.SetPhysicalPressSuppressed(b, false);
            }
            var time = TimeController.Instance;
            if (time != null)
            {
                time.DevFrozen = false;
                time.DebugScale = 1f;
            }
            CursorManager.SetGameplay(true);
            foreach (var kv in _stats)
                Ctx.Report(PlaytestCategories.Vfx, "Lab: " + kv.Key, kv.Value.ok == kv.Value.casts ? TestStatus.Pass : TestStatus.Warning,
                    $"{kv.Value.casts} cast(s), {kv.Value.ok} with VFX + completion");
            if (_stats.Count == 0) Ctx.Report(PlaytestCategories.Vfx, "VFX lab", TestStatus.NotTested, "no technique cast");
        }

        private IEnumerator Stage()
        {
            var pc = Ctx.Player;
            _dummy = Ctx.EnsureDummy();
            _mark = _dummy.transform.position + _dummy.transform.forward * 5f;
            _mark = Ctx.GroundPoint(_mark);
            var w = new WaitResult();
            yield return Ctx.Driver.MoveTo(Ctx, _mark, 0.8f, 8f, false, w);
            yield return Ctx.LockOnto(_dummy, w);
            _staged = true;
            _status = "Ready";
            if (PresetSlot >= 0) Cast();
        }

        private void Update()
        {
            // The lab panel needs a free cursor (closing the pause menu re-locks it).
            if (!PauseMenu.IsOpen && !DebugMenu.IsOpen && Cursor.lockState != CursorLockMode.None) CursorManager.SetGameplay(false);
            if (Ctx == null || !_staged) return;
            var time = TimeController.Instance;
            if (time != null && time.DevFrozen) return;
            if (_interval > 0f && !_casting && Time.realtimeSinceStartup - _lastCastStart >= _interval) Cast();
        }

        private BreathingStyleData CurrentStyle
        {
            get
            {
                var styles = Ctx.Player.Breathing.Styles;
                return styles.Count > 0 ? styles[Mathf.Clamp(_style, 0, styles.Count - 1)] : null;
            }
        }

        public void Cast()
        {
            if (_castRoutine != null) StopCoroutine(_castRoutine);
            _castRoutine = StartCoroutine(CastRoutine());
        }

        private IEnumerator CastRoutine()
        {
            var pc = Ctx.Player;
            var style = CurrentStyle;
            var skill = style != null ? style.GetSkill(_slot) : null;
            if (pc == null || skill == null) yield break;
            _casting = true;
            _lastCastStart = Time.realtimeSinceStartup;
            _status = $"Casting {skill.displayName}";

            if (pc.Breathing.IsExecuting) pc.Breathing.Cancel();
            if (pc.Breathing.Current != style) pc.Breathing.EquipById(style.styleId);
            Ctx.RefillPlayer();
            if (_resetStage && _dummy != null)
            {
                Vector3 face = _dummy.transform.position - _mark;
                face.y = 0f;
                pc.Motor.ResetMotion();
                pc.Motor.Teleport(_mark, face);
            }
            yield return new WaitForSecondsRealtime(0.15f);
            if (_dummy != null && !Ctx.IsLockedOn(_dummy))
            {
                var w = new WaitResult();
                yield return Ctx.LockOnto(_dummy, w);
            }

            float mark = Ctx.Telemetry.Mark();
            _lastPhases.Clear();
            Ctx.Driver.Press(_slot switch
            {
                0 => InputCommand.Skill1,
                1 => InputCommand.Skill2,
                2 => InputCommand.Skill3,
                3 => InputCommand.Skill4,
                _ => InputCommand.Ultimate
            });

            float start = Time.realtimeSinceStartup;
            bool started = false;
            string last = null;
            float frozenTime = 0f;
            while (Time.realtimeSinceStartup - start - frozenTime < 60f)
            {
                if (TimeController.Instance != null && TimeController.Instance.DevFrozen)
                {
                    frozenTime += Time.unscaledDeltaTime;
                    yield return null;
                    continue;
                }
                var exec = pc.Breathing.Executor;
                if (exec.Running)
                {
                    started = true;
                    if (exec.Phase != null && exec.Phase.name != last)
                    {
                        last = exec.Phase.name;
                        _lastPhases.Add(last);
                    }
                }
                else if (started || Time.realtimeSinceStartup - start > 1.5f) break;
                yield return null;
            }
            _lastDuration = Time.realtimeSinceStartup - start - frozenTime;
            var vfx = Ctx.Telemetry.VfxIdsSince(mark);
            var hits = Ctx.Telemetry.DamageSince(mark, d => d.Hit.SourceId == skill.skillId);
            _lastVfx = string.Join(", ", vfx.Take(10));
            _lastHits = hits.Count > 0 ? $"{hits.Count} hit(s), {hits.Sum(h => h.Result.Damage)} dmg" : "no hits";
            _casts++;
            string key = $"{style.displayName} — {skill.displayName}";
            _stats.TryGetValue(key, out var s);
            _stats[key] = (s.casts + 1, s.ok + (started && vfx.Count > 0 ? 1 : 0));
            _status = started ? $"Done: {skill.displayName}" : $"Did not start: {skill.displayName} (see HUD message)";
            _casting = false;
        }

        public override float DrawPanel(Rect area)
        {
            var pc = Ctx != null ? Ctx.Player : null;
            if (pc == null) return 0f;
            var time = TimeController.Instance;
            var styles = pc.Breathing.Styles;
            GUILayout.BeginArea(area);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label($"Status: {_status}   Casts: {_casts}");

            if (GUILayout.Button("REPEAT RISING SERPENT", GUILayout.Height(34)))
            {
                _style = Mathf.Max(0, styles.ToList().FindIndex(s => s.styleId == "tidal"));
                _slot = 0;
                if (_interval <= 0f) _interval = 3f;
                Cast();
            }

            GUILayout.Label("Breathing style");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < styles.Count; i++)
            {
                string name = styles[i].displayName.Replace(" BREATH", "");
                if (GUILayout.Toggle(_style == i, name, GUI.skin.button)) _style = i;
            }
            GUILayout.EndHorizontal();

            var style = CurrentStyle;
            GUILayout.Label("Technique (click to cast)");
            string[] slotNames = { "Skill 1", "Skill 2", "Skill 3", "Advanced", "Ultimate" };
            for (int slot = 0; slot < 5; slot++)
            {
                var skill = style != null ? style.GetSkill(slot) : null;
                string label = $"{(_slot == slot ? "> " : "")}{slotNames[slot]}: {(skill != null ? skill.displayName : "-")}";
                if (GUILayout.Button(label, GUILayout.Height(24)))
                {
                    _slot = slot;
                    Cast();
                }
            }

            GUILayout.Label("Auto repeat");
            GUILayout.BeginHorizontal();
            foreach (var iv in Intervals)
                if (GUILayout.Toggle(Mathf.Approximately(_interval, iv), iv <= 0f ? "Off" : $"{iv:0} s", GUI.skin.button)) _interval = iv;
            GUILayout.EndHorizontal();

            if (time != null)
            {
                GUILayout.Label("Playback speed");
                GUILayout.BeginHorizontal();
                foreach (var sp in Speeds)
                    if (GUILayout.Toggle(Mathf.Approximately(time.DebugScale, sp), $"{sp:0.##}x", GUI.skin.button)) time.DebugScale = sp;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                bool frozen = GUILayout.Toggle(time.DevFrozen, time.DevFrozen ? "RESUME" : "FREEZE VFX", GUI.skin.button, GUILayout.Height(28));
                if (frozen != time.DevFrozen) time.DevFrozen = frozen;
                GUI.enabled = time.DevFrozen;
                if (GUILayout.Button("NEXT FRAME", GUILayout.Height(28))) time.StepFrame();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            _resetStage = GUILayout.Toggle(_resetStage, " Reset to the stage mark before each cast");

            GUILayout.Space(4);
            GUILayout.Label("Last cast: " + _lastDuration.ToString("0.00") + " s");
            GUILayout.Label("Phases: " + (_lastPhases.Count > 0 ? string.Join(" → ", _lastPhases) : "-"));
            GUILayout.Label("VFX: " + _lastVfx);
            GUILayout.Label("Damage: " + _lastHits);
            GUILayout.Label("F8 screenshot · F12 exit lab");
            if (GUILayout.Button("EXIT LAB")) ExitRequested = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return area.height;
        }
    }
}
