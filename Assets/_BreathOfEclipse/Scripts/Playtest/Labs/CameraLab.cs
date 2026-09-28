using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.AI;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// CAMERA TEST lab: normal play next to a training dummy and a Nightspawn. Keys 1 / 2 / 3 select first /
    /// second / third person (the skill keys 1–3 are disabled while the lab runs), C cycles as usual. Shows live
    /// diagnostics and flags "SECOND PERSON TARGET LOST", checking the automatic return to third person.
    /// The player has God Mode in the lab (restored on exit).
    /// </summary>
    public sealed class CameraLab : PlaytestLab
    {
        private static readonly BufferedAction[] SuppressedButtons = { BufferedAction.Skill1, BufferedAction.Skill2, BufferedAction.Skill3 };

        public override string Title => "CAMERA TEST";

        private EnemyController _enemy;
        private bool _prevGod;
        private float _lostBannerUntil;
        private bool _lostChecked = true;
        private float _lostTime;
        private int _lostEvents, _lostFallbackOk;
        private bool _collisionSeen;
        private readonly HashSet<CameraMode> _modesSeen = new HashSet<CameraMode>();
        private float _respawnAt = -1f;
        private CameraRig _rig;

        protected override void OnBegin()
        {
            var input = InputReader.Instance;
            if (input != null) foreach (var b in SuppressedButtons) input.SetPhysicalPressSuppressed(b, true);
            _prevGod = DebugMenu.GodMode;
            DebugMenu.GodMode = true;
            Ctx.Player.Stats.GodMode = true;
            _rig = Ctx.Rig;
            if (_rig != null) _rig.SecondPersonTargetLost += OnTargetLost;
            StartCoroutine(Stage());
        }

        public override void End()
        {
            var input = InputReader.Instance;
            if (input != null) foreach (var b in SuppressedButtons) input.SetPhysicalPressSuppressed(b, false);
            DebugMenu.GodMode = _prevGod;
            if (Ctx.Player != null) Ctx.Player.Stats.GodMode = _prevGod;
            if (_rig != null) _rig.SecondPersonTargetLost -= OnTargetLost;
            const string cat = PlaytestCategories.Cameras;
            foreach (CameraMode m in new[] { CameraMode.ThirdPerson, CameraMode.FirstPerson, CameraMode.SecondPerson })
                Ctx.Report(cat, "Lab: " + m, _modesSeen.Contains(m) ? TestStatus.Pass : TestStatus.NotTested, _modesSeen.Contains(m) ? "used by the tester" : "not used");
            Ctx.Report(cat, "Lab: second person target lost", _lostEvents == 0 ? TestStatus.NotTested : _lostFallbackOk == _lostEvents ? TestStatus.Pass : TestStatus.Fail,
                $"{_lostEvents} event(s), {_lostFallbackOk} returned to third person");
            Ctx.Report(cat, "Lab: collision", _collisionSeen ? TestStatus.Pass : TestStatus.NotTested, _collisionSeen ? "camera pulled in by geometry at least once" : "no obstruction happened");
        }

        private IEnumerator Stage()
        {
            var pc = Ctx.Player;
            var dummy = Ctx.EnsureDummy();
            yield return new WaitForSeconds(0.2f);
            SpawnEnemy(dummy.transform.position + dummy.transform.right * 4f);
            GameEvents.Notify("CAMERA TEST — 1 First · 2 Second · 3 Third · C cycles · F12 exit");
        }

        private void SpawnEnemy(Vector3 near)
        {
            var pc = Ctx.Player;
            _enemy = Ctx.SpawnEnemy("nightspawn", near, pc != null ? pc.transform.position : Vector3.zero, true);
            if (_enemy != null) _enemy.Alert();
        }

        private void OnTargetLost()
        {
            _lostEvents++;
            _lostBannerUntil = Time.unscaledTime + 3f;
            _lostChecked = false;
            _lostTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (Ctx == null || _rig == null) return;
            var pc = Ctx.Player;
            var kb = Keyboard.current;
            if (kb != null && pc != null && !Core.DebugMenu.IsOpen)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) _rig.SetMode(CameraMode.FirstPerson);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) _rig.SetMode(CameraMode.ThirdPerson);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                {
                    if (pc.LockOn.Current == null) pc.LockOn.Toggle();
                    if (pc.LockOn.Current != null) _rig.SetMode(CameraMode.SecondPerson);
                    else GameEvents.Notify("Second person needs a target: none in range");
                }
            }
            _modesSeen.Add(_rig.Mode);
            _collisionSeen |= _rig.CollisionActive;

            if (!_lostChecked && Time.unscaledTime - _lostTime > 0.3f)
            {
                _lostChecked = true;
                if (_rig.Mode == CameraMode.ThirdPerson) _lostFallbackOk++;
            }

            // Keep a live Nightspawn around.
            if (_enemy == null || !_enemy.IsAlive)
            {
                if (_respawnAt < 0f) _respawnAt = Time.time + 3f;
                else if (Time.time >= _respawnAt)
                {
                    _respawnAt = -1f;
                    var dummy = Ctx.ClosestDummy();
                    SpawnEnemy(dummy != null ? dummy.transform.position + dummy.transform.right * 4f : pc.transform.position + pc.transform.forward * 8f);
                }
            }
        }

        public override float DrawPanel(Rect area)
        {
            if (_rig == null) return 0f;
            var pc = Ctx.Player;
            var big = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            big.normal.textColor = new Color(0.6f, 0.9f, 1f);
            GUILayout.BeginArea(area);
            string mode = _rig.CinematicActive ? "CINEMATIC" : _rig.Mode == CameraMode.FirstPerson ? "FIRST PERSON" : _rig.Mode == CameraMode.SecondPerson ? "SECOND PERSON" : "THIRD PERSON";
            GUILayout.Label("CURRENT CAMERA:", GUILayout.Height(18));
            GUILayout.Label(mode, big);
            var target = _rig.LockTarget;
            GUILayout.Label($"Target: {(target != null ? target.root.name : "none")}");
            GUILayout.Label($"Distance to player: {_rig.DistanceToPlayer:0.00} m");
            GUILayout.Label($"FOV: {_rig.Camera.fieldOfView:0.0}°");
            GUILayout.Label($"Collision: {(_rig.CollisionActive ? "PULLED IN (obstructed)" : "clear")}");
            GUILayout.Label($"Lock-on: {(pc != null && pc.LockOn.Current != null ? "ON" : "off")}   Player: {(pc != null ? pc.State.ToString() : "-")}");
            GUILayout.Label($"Target lost events: {_lostEvents} (returned to 3rd: {_lostFallbackOk})");
            if (Time.unscaledTime < _lostBannerUntil)
            {
                var warn = new GUIStyle(GUI.skin.box) { fontSize = 16, fontStyle = FontStyle.Bold };
                warn.normal.textColor = new Color(1f, 0.4f, 0.35f);
                GUILayout.Box("SECOND PERSON TARGET LOST" + (_lostChecked ? (_rig.Mode == CameraMode.ThirdPerson ? " → THIRD PERSON" : " → FALLBACK FAILED") : ""), warn);
            }
            GUILayout.Label("1 First · 2 Second (auto lock) · 3 Third · C cycle");
            GUILayout.Label("Tab lock-on · F8 screenshot · F12 exit");
            GUILayout.EndArea();
            return area.height;
        }
    }
}
