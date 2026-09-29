using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BreathOfEclipse.AI;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// F1 debug additions for the real 3D characters (v0.4): player / demon visual mode (3D model or procedural
    /// mannequin), animation tests on the player, demon tests, and the Animation Lab window (select a clip, play /
    /// loop, 0.5x / 1x / 1.5x, freeze, restart, reset pose; preview procedural motions and face expressions on the
    /// player or the nearest demon; skeleton report).
    /// </summary>
    public sealed class AnimationLab : MonoBehaviour
    {
        public static bool Open { get; set; }

        private Rect _window = new Rect(420f, 20f, 440f, 660f);
        private Vector2 _clipScroll, _motionScroll;
        private bool _targetDemon;
        private string _filter = "";
        private List<string> _clips;
        private List<string> _motions;

        // ------------------------------------------------------------------ F1 section

        /// <summary>Drawn inside the F1 window.</summary>
        public static void DrawDebugSection(GUIStyle header, PlayerController pc, System.Action reloadScene)
        {
            var settings = SaveSystem.Settings;
            GUILayout.Space(4f);
            GUILayout.Label("3D MODEL (v0.4)", header);
            var driver = pc != null ? pc.GetComponent<HumanoidVisualDriver>() : null;
            GUILayout.Label(driver != null
                ? $"Player: real 3D model · clips {(driver.ClipsActive ? HumanoidClipLibrary.Count.ToString() : "none (procedural retarget)")}"
                : pc != null ? "Player: procedural mannequin" : "No player");
            if (settings != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("PLAYER VISUAL", GUILayout.Width(110f));
                if (GUILayout.Toggle(settings.playerVisualMode == 0, "Humanoid 3D", GUI.skin.button) && settings.playerVisualMode != 0) SetMode(true, 0, reloadScene);
                if (GUILayout.Toggle(settings.playerVisualMode == 1, "Procedural", GUI.skin.button) && settings.playerVisualMode != 1) SetMode(true, 1, reloadScene);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("DEMON VISUAL", GUILayout.Width(110f));
                if (GUILayout.Toggle(settings.demonVisualMode == 0, "Humanoid 3D", GUI.skin.button) && settings.demonVisualMode != 0) SetMode(false, 0, reloadScene);
                if (GUILayout.Toggle(settings.demonVisualMode == 1, "Procedural", GUI.skin.button) && settings.demonVisualMode != 1) SetMode(false, 1, reloadScene);
                GUILayout.EndHorizontal();
            }
            Open = GUILayout.Toggle(Open, " Animation Lab window");

            GUILayout.Label("ANIMATION TESTS (player)", header);
            GUI.enabled = pc != null;
            var anim = pc != null ? pc.Animator : null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle") && anim != null) anim.StopAction(0.1f);
            if (GUILayout.Button("Combo") && pc != null) pc.StartCoroutine(Combo(anim));
            if (GUILayout.Button("Heavy") && anim != null) anim.PlayAttack("H1", 0.3f, 0.14f, 0.45f);
            if (GUILayout.Button("Dodge") && anim != null) anim.SetDodge(pc.transform.forward, 0.32f);
            if (GUILayout.Button("Parry") && anim != null) anim.PlayMotion("Parry", 0.3f, 0.02f);
            if (GUILayout.Button("Inhale") && anim != null) anim.PlayMotion("SkillInhale", 0.5f, 0.08f);
            GUILayout.EndHorizontal();
            DrawForms(pc, "tidal", "Water");
            DrawForms(pc, "thunder", "Thunder");
            GUI.enabled = true;

            GUILayout.Label("DEMON TESTS", header);
            GUI.enabled = pc != null;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nightspawn", GUILayout.Width(80f));
            if (GUILayout.Button("Spawn")) DebugSpawn("nightspawn", 6f);
            if (GUILayout.Button("Attack")) Force(EnemyArchetype.Nightspawn, "claw_combo");
            if (GUILayout.Button("Heavy")) Force(EnemyArchetype.Nightspawn, "rending_cross");
            if (GUILayout.Button("Hit")) HitNearest(EnemyArchetype.Nightspawn);
            if (GUILayout.Button("Death")) KillNearest(EnemyArchetype.Nightspawn);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hollow Oni", GUILayout.Width(80f));
            if (GUILayout.Button("Spawn")) DebugSpawn("hollow_oni", 10f);
            if (GUILayout.Button("Phase 2")) PhaseTwo();
            if (GUILayout.Button("Eclipse Cleave")) Force(EnemyArchetype.HollowOni, "eclipse_cleave");
            if (GUILayout.Button("Death")) KillNearest(EnemyArchetype.HollowOni);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private static void SetMode(bool player, int mode, System.Action reloadScene)
        {
            if (player) SaveSystem.Settings.playerVisualMode = mode;
            else SaveSystem.Settings.demonVisualMode = mode;
            SaveSystem.SaveSettings();
            reloadScene?.Invoke();
        }

        private static IEnumerator Combo(ICharacterAnimator anim)
        {
            if (anim == null) yield break;
            string[] ids = { "L1", "L2", "L3", "L4" };
            foreach (var id in ids)
            {
                anim.PlayAttack(id, 0.12f, 0.1f, 0.3f);
                yield return new WaitForSeconds(0.36f);
            }
        }

        private static void DrawForms(PlayerController pc, string styleId, string label)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60f));
            string[] roman = { "I", "II", "III", "IV", "V", "VI", "VII" };
            for (int i = 0; i < roman.Length; i++)
            {
                if (!GUILayout.Button(roman[i]) || pc == null) continue;
                var b = pc.Breathing;
                if (b.Current == null || b.Current.styleId != styleId) b.EquipById(styleId);
                b.Cancel();
                pc.Stats.Breath.SetValue(pc.Stats.Breath.Max);
                b.Cooldowns.ResetAll();
                if (i < b.FormCount) b.TryUseForm(i, pc.LockOn.CurrentPoint);
            }
            GUILayout.EndHorizontal();
        }

        private static EnemyController Nearest(EnemyArchetype archetype)
        {
            var pc = PlayerController.Instance;
            EnemyController best = null;
            float bestD = float.MaxValue;
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (e == null || !e.IsAlive || e.Data == null || e.Data.archetype != archetype) continue;
                float d = pc != null ? (e.transform.position - pc.transform.position).sqrMagnitude : 0f;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        private static void DebugSpawn(string id, float distance)
        {
            var pc = PlayerController.Instance;
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            var data = db != null ? db.FindEnemy(id) : null;
            if (pc == null || data == null) return;
            Vector3 forward = pc.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            HitQuery.GroundPoint(pc.transform.position + forward * distance + Vector3.up * 4f, out var ground, out _, 10f);
            var enemy = EnemyFactory.Spawn(data, ground + Vector3.up * 0.05f, Quaternion.LookRotation(-forward));
            var boss = enemy.GetComponent<BossController>();
            if (boss != null) boss.StartEncounter();
            else enemy.Alert();
        }

        private static void Force(EnemyArchetype archetype, string attackId)
        {
            var e = Nearest(archetype);
            if (e == null) return;
            e.ForcedNextAttackId = attackId;
            e.Alert();
        }

        private static void HitNearest(EnemyArchetype archetype)
        {
            var e = Nearest(archetype);
            var pc = PlayerController.Instance;
            if (e == null || pc == null) return;
            var hit = new HitData
            {
                AttackerTeam = Team.Player,
                SourceId = "debug_hit",
                AttackInstanceId = Random.Range(1_000_000, int.MaxValue),
                Category = DamageCategory.Light,
                BaseDamage = 4f,
                Multiplier = 1f,
                Reaction = HitReaction.Light,
                HitPoint = e.Damageable.CenterPoint,
                Direction = (e.transform.position - pc.transform.position).normalized,
                Knockback = 1f
            };
            e.Damageable.ReceiveHit(hit);
        }

        private static void KillNearest(EnemyArchetype archetype)
        {
            var e = Nearest(archetype);
            if (e != null) e.Damageable.Kill(PlayerController.Instance != null ? PlayerController.Instance.gameObject : null);
        }

        private static void PhaseTwo()
        {
            var e = Nearest(EnemyArchetype.HollowOni);
            if (e == null || e.Phase >= 2) return;
            var h = e.Damageable.Health;
            float target = h.Max * e.Data.phase2Threshold - 1f;
            if (h.Current > target) h.ApplyDamage(h.Current - target);
        }

        // ------------------------------------------------------------------ Animation Lab window

        private void OnGUI()
        {
            if (!Open || !DebugMenu.IsOpen) return;
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUI.Window(0x0B0F, _window, DrawLab, "ANIMATION LAB");
            GUI.matrix = old;
        }

        private GameObject Target()
        {
            if (_targetDemon)
            {
                var pc = PlayerController.Instance;
                EnemyController best = null;
                float bestD = float.MaxValue;
                foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (e == null || !e.IsAlive) continue;
                    float d = pc != null ? (e.transform.position - pc.transform.position).sqrMagnitude : 0f;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = e;
                    }
                }
                return best != null ? best.gameObject : null;
            }
            return PlayerController.Instance != null ? PlayerController.Instance.gameObject : null;
        }

        private void DrawLab(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_targetDemon, "Player", GUI.skin.button)) _targetDemon = false;
            if (GUILayout.Toggle(_targetDemon, "Nearest demon", GUI.skin.button)) _targetDemon = true;
            GUILayout.EndHorizontal();
            var target = Target();
            var driver = target != null ? target.GetComponent<HumanoidVisualDriver>() : null;
            var source = target != null ? target.GetComponent<ProceduralAnimator>() : null;
            if (driver == null)
            {
                GUILayout.Label(target == null ? "No target." : "Target uses the procedural mannequin (no 3D model).");
                if (source != null) DrawMotions(source);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }
            var sk = driver.Skeleton;
            GUILayout.Label($"Skeleton: {(sk != null ? sk.Report.Split('\n')[0] : "-")}");
            GUILayout.Label($"Clips: {(driver.ClipsActive ? HumanoidClipLibrary.Count + " (Humanoid)" : "none — procedural retarget only")}  ·  Set: {driver.Set}");

            GUILayout.BeginHorizontal();
            bool loop = GUILayout.Toggle(driver.LabLoop, " Loop");
            if (loop != driver.LabLoop) driver.LabLoop = loop;
            foreach (float sp in new[] { 0.5f, 1f, 1.5f })
                if (GUILayout.Toggle(Mathf.Approximately(driver.LabSpeed, sp), $"{sp:0.#}x", GUI.skin.button)) driver.LabSpeed = sp;
            bool freeze = GUILayout.Toggle(driver.LabFrozen, "Freeze", GUI.skin.button);
            if (freeze != driver.LabFrozen) driver.LabFrozen = freeze;
            if (GUILayout.Button("Restart")) driver.LabRestart();
            if (GUILayout.Button("Reset Pose")) driver.SetLabClip(null);
            GUILayout.EndHorizontal();
            bool sword = GUILayout.Toggle(driver.LabHoldsSword, " Keep katana grip (IK) while previewing");
            if (sword != driver.LabHoldsSword) driver.LabHoldsSword = sword;
            GUILayout.Label(driver.LabClip != null ? $"Playing {driver.LabClip}  {driver.LabTime:0.00} / {driver.LabLength:0.00} s" : "Normal animation (no lab clip).");

            if (_clips == null) _clips = driver.AvailableClips().OrderBy(c => c).ToList();
            _filter = GUILayout.TextField(_filter ?? "");
            _clipScroll = GUILayout.BeginScrollView(_clipScroll, GUILayout.Height(210f));
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (var c in _clips)
            {
                if (!string.IsNullOrEmpty(_filter) && c.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (GUILayout.Toggle(driver.LabClip == c, c, GUI.skin.button, GUILayout.Width(195f)) && driver.LabClip != c) driver.SetLabClip(c);
                if (++col % 2 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            if (source != null) DrawMotions(source, driver);
            var face = target.GetComponentInChildren<AnimeFace>();
            if (face != null)
            {
                GUILayout.Label($"FACE: {face.Current}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(!face.Forced.HasValue, "Auto", GUI.skin.button)) face.Forced = null;
                foreach (AnimeFace.Expression e in System.Enum.GetValues(typeof(AnimeFace.Expression)))
                    if (GUILayout.Toggle(face.Forced == e, e.ToString(), GUI.skin.button)) face.Forced = e;
                GUILayout.EndHorizontal();
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        /// <summary>Procedural motions (attacks and technique poses) played on the character through the gameplay animator.</summary>
        private void DrawMotions(ProceduralAnimator source, HumanoidVisualDriver driver = null)
        {
            GUILayout.Label("PROCEDURAL / COMBAT MOTIONS (retargeted)");
            if (_motions == null) _motions = MotionLibrary.AllIds.OrderBy(m => m).ToList();
            _motionScroll = GUILayout.BeginScrollView(_motionScroll, GUILayout.Height(150f));
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (var m in _motions)
            {
                if (GUILayout.Button(m, GUILayout.Width(128f)))
                {
                    if (driver != null) driver.SetLabClip(null);
                    bool attack = m.Length <= 4 || m.StartsWith("Enemy") || m.StartsWith("Oni") || m == "Riposte" || m == "PDCounter" || m == "DashL";
                    if (attack && !m.StartsWith("Skill") && m != "OniTransform" && m != "OniRoar" && m != "EnemyRoar" && m != "EnemyStagger" && m != "EnemyBlock")
                        source.PlayAttack(m, 0.25f, 0.14f, 0.4f);
                    else source.PlayMotion(m, 1.2f, 0.1f);
                }
                if (++col % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }
    }
}
