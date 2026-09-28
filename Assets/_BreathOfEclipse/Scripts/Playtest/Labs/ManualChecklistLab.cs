using System.Collections.Generic;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.UI;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// MANUAL TEST: normal play in CombatTest while a checklist fills itself from what actually happens
    /// (movement, attacks, dodge, parry, techniques, cameras, menus, kills...). Exceptions and performance are
    /// recorded; F12 ends the test and writes the report (unticked items are reported as NOT TESTED).
    /// </summary>
    public sealed class ManualChecklistLab : PlaytestLab
    {
        private sealed class Item
        {
            public string Name;
            public bool Done;
            public string Details;
        }

        public override string Title => "MANUAL TEST";

        private readonly List<Item> _items = new List<Item>();
        private readonly Dictionary<string, Item> _byName = new Dictionary<string, Item>();
        private Vector3 _lastPos;
        private float _distance;
        private float _since;
        private readonly AttackWatcher _watcher = new AttackWatcher();
        private int _maxCombo;

        private static readonly string[] Names =
        {
            "Move (WASD)", "Sprint (Shift)", "Jump (Space)", "Light attack (LMB)", "Heavy attack (RMB)", "Combo 4+ hits",
            "Dodge (Alt)", "Block (Q)", "Parry", "Perfect dodge", "Lock-on (Tab / MMB)", "Technique 1", "Technique 2",
            "Technique 3", "Technique 4 (advanced)", "Ultimate (R)", "Change breathing style (X/Z)", "First person",
            "Second person", "Third person", "Pause menu (Esc)", "Debug menu (F1)", "Defeat an enemy", "Boss phase 2"
        };

        protected override void OnBegin()
        {
            foreach (var n in Names)
            {
                var item = new Item { Name = n };
                _items.Add(item);
                _byName[n] = item;
            }
            _since = Ctx.Telemetry.Mark();
            if (Ctx.Player != null) _lastPos = Ctx.Player.transform.position;
            _watcher.Reset();
            GameEvents.StyleChanged += OnStyle;
            GameEvents.Notify("MANUAL TEST — play normally; the checklist fills itself. F12 = finish + report");
        }

        public override void End()
        {
            GameEvents.StyleChanged -= OnStyle;
            foreach (var i in _items)
                Ctx.Report(PlaytestCategories.Manual, i.Name, i.Done ? TestStatus.Pass : TestStatus.NotTested, i.Done ? (i.Details ?? "observed") : "not done during the session");
        }

        private void OnStyle(string id, string name) => Tick("Change breathing style (X/Z)", name);

        private void Tick(string name, string details = null)
        {
            if (_byName.TryGetValue(name, out var i) && !i.Done)
            {
                i.Done = true;
                i.Details = details;
            }
        }

        private void Update()
        {
            var pc = Ctx != null ? Ctx.Player : null;
            if (pc == null) return;
            _distance += Vector3.Distance(_lastPos, pc.transform.position);
            _lastPos = pc.transform.position;
            if (_distance > 4f) Tick("Move (WASD)", $"{_distance:0} m");
            if (pc.IsSprinting) Tick("Sprint (Shift)");
            if (!pc.Motor.Grounded && pc.Motor.VerticalVelocity > 3f) Tick("Jump (Space)");
            _watcher.Poll();
            foreach (var a in _watcher.Started)
            {
                if (a == "L1") Tick("Light attack (LMB)");
                if (a == "H1") Tick("Heavy attack (RMB)");
            }
            _watcher.Started.Clear();
            if (pc.State == PlayerState.Dodge) Tick("Dodge (Alt)");
            if (pc.State == PlayerState.Block) Tick("Block (Q)");
            if (pc.LockOn.Current != null) Tick("Lock-on (Tab / MMB)");

            var t = Ctx.Telemetry;
            _maxCombo = Mathf.Max(_maxCombo, (int)TelemetryRecorder.MaxSince(t.Combo, _since));
            if (_maxCombo >= 4) Tick("Combo 4+ hits", $"{_maxCombo} hits");
            if (TelemetryRecorder.CountSince(t.Parries, _since) > 0) Tick("Parry");
            if (TelemetryRecorder.CountSince(t.PerfectDodges, _since) > 0) Tick("Perfect dodge");
            if (TelemetryRecorder.CountSince(t.Ultimates, _since) > 0) Tick("Ultimate (R)");
            if (TelemetryRecorder.CountSince(t.Kills, _since) > 0) Tick("Defeat an enemy");
            foreach (var p in t.BossPhases)
                if (p.Time >= _since && p.Value >= 2f) Tick("Boss phase 2");
            foreach (var s in t.Skills)
            {
                if (s.Time < _since) continue;
                var style = pc.Breathing.Current;
                if (style == null) continue;
                for (int slot = 0; slot < 4; slot++)
                {
                    var skill = style.GetSkill(slot);
                    if (skill != null && skill.skillId == s.Id) Tick(slot == 3 ? "Technique 4 (advanced)" : $"Technique {slot + 1}", skill.displayName);
                }
            }

            var rig = Ctx.Rig;
            if (rig != null && !rig.CinematicActive)
            {
                if (rig.Mode == CameraMode.FirstPerson) Tick("First person");
                if (rig.Mode == CameraMode.SecondPerson) Tick("Second person");
                if (rig.Mode == CameraMode.ThirdPerson) Tick("Third person");
            }
            if (PauseMenu.IsOpen) Tick("Pause menu (Esc)");
            if (DebugMenu.IsOpen) Tick("Debug menu (F1)");
        }

        public override float DrawPanel(Rect area)
        {
            GUILayout.BeginArea(area);
            int done = 0;
            foreach (var i in _items) if (i.Done) done++;
            GUILayout.Label($"Checklist {done}/{_items.Count} — play normally");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            GUILayout.BeginHorizontal();
            for (int col = 0; col < 2; col++)
            {
                GUILayout.BeginVertical();
                for (int i = col; i < _items.Count; i += 2)
                {
                    var item = _items[i];
                    style.normal.textColor = item.Done ? new Color(0.5f, 1f, 0.6f) : new Color(0.8f, 0.8f, 0.85f);
                    GUILayout.Label((item.Done ? "[x] " : "[ ] ") + item.Name, style);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("F8 screenshot · F12 finish + report");
            GUILayout.EndArea();
            return area.height;
        }
    }
}
