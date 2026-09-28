using BreathOfEclipse.AI;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Player;
using BreathOfEclipse.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// F1 developer menu (IMGUI so it works in any scene without prefabs). Cheats persist across scene reloads
    /// and are re-applied to each newly spawned player.
    /// </summary>
    public sealed class DebugMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        public static bool GodMode;
        public static bool InfiniteBreath;
        public static bool InfiniteStamina;

        private static readonly float[] SlowMoPresets = { 1f, 0.5f, 0.25f, 0.1f };

        private Rect _window = new Rect(20f, 20f, 380f, 640f);
        private Vector2 _scroll;
        private PlayerController _appliedTo;
        private HitboxDebugRenderer _hitboxRenderer;
        private GUIStyle _header;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
            GodMode = InfiniteBreath = InfiniteStamina = false;
        }

        private void Start()
        {
            if (InputReader.Instance != null) InputReader.Instance.DebugMenuPressed += Toggle;
        }

        private void OnDestroy()
        {
            if (InputReader.Instance != null) InputReader.Instance.DebugMenuPressed -= Toggle;
            IsOpen = false;
        }

        public void Toggle()
        {
            if (PauseMenu.IsOpen) return;
            IsOpen = !IsOpen;
            bool inGameplay = PlayerController.Instance != null;
            if (InputReader.Instance != null && inGameplay) InputReader.Instance.SetGameplayEnabled(!IsOpen);
            CursorManager.SetGameplay(inGameplay && !IsOpen);
        }

        private void Update()
        {
            var pc = PlayerController.Instance;
            if (pc != null && pc != _appliedTo && pc.Stats != null)
            {
                _appliedTo = pc;
                ApplyCheats(pc);
            }
        }

        /// <summary>Hits the player from the front with a chosen reaction (tests hit reactions, knockdown and get up).</summary>
        private static void HitPlayer(PlayerController pc, HitReaction reaction, float launchHeight)
        {
            if (pc == null) return;
            bool heavy = reaction != HitReaction.Light;
            var hit = new HitData
            {
                AttackerTeam = Team.Enemy,
                SourceId = "debug_hit",
                AttackInstanceId = Random.Range(1_000_000, int.MaxValue),
                Category = heavy ? DamageCategory.EnemyHeavy : DamageCategory.EnemyLight,
                BaseDamage = heavy ? 12f : 5f,
                Multiplier = 1f,
                Reaction = reaction,
                HitPoint = pc.Damageable.CenterPoint,
                Direction = -pc.transform.forward,
                Knockback = heavy ? 4f : 1f,
                LaunchHeight = launchHeight
            };
            pc.Damageable.ReceiveHit(hit);
        }

        private static void ApplyCheats(PlayerController pc)
        {
            pc.Stats.GodMode = GodMode;
            pc.Stats.InfiniteBreath = InfiniteBreath;
            pc.Stats.InfiniteStamina = InfiniteStamina;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            if (_header == null)
            {
                _header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
                _header.normal.textColor = new Color(0.55f, 0.85f, 1f);
            }
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUI.Window(0x0B0E, _window, DrawWindow, "BREATH OF ECLIPSE — DEBUG (F1)");
            GUI.matrix = old;
        }

        private void DrawWindow(int id)
        {
            var pc = PlayerController.Instance;
            var time = TimeController.Instance;
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"v{GameManager.Version}  |  Scene: {SceneManager.GetActiveScene().name}");
            GUILayout.Label($"Enemies alive: {EncounterDirector.AliveCount}  |  timeScale: {Time.timeScale:0.00}");
            if (pc != null)
            {
                GUILayout.Label($"Player: {pc.State}{(pc.GettingUp ? " (getting up)" : "")}  HP {pc.Damageable.Health.Current:0}/{pc.Damageable.Health.Max:0}  " +
                                $"ST {pc.Stats.Stamina.Current:0}  BR {pc.Stats.Breath.Current:0}");
                var cam = CameraRig.Instance;
                string technique = pc.Breathing.IsExecuting && pc.Breathing.Executor.Skill != null ? pc.Breathing.Executor.Skill.displayName : "-";
                GUILayout.Label($"Grounded {pc.Motor.Grounded}  CanAttack/Technique {pc.CanAct}  Down pose {pc.Animator.KnockdownPoseActive}");
                GUILayout.Label($"Camera {(cam != null ? cam.Mode.ToString() : "-")}  Technique {technique}");
            }

            GUILayout.Space(4f);
            GUILayout.Label("CHEATS", _header);
            bool god = GUILayout.Toggle(GodMode, " God Mode");
            bool breath = GUILayout.Toggle(InfiniteBreath, " Infinite Breath");
            bool stamina = GUILayout.Toggle(InfiniteStamina, " Infinite Stamina");
            if (god != GodMode || breath != InfiniteBreath || stamina != InfiniteStamina)
            {
                GodMode = god;
                InfiniteBreath = breath;
                InfiniteStamina = stamina;
                if (pc != null) ApplyCheats(pc);
            }
            if (pc != null && GUILayout.Button("Refill HP / Stamina / Breath + reset cooldowns"))
            {
                pc.Damageable.Health.Revive(1f);
                pc.Stats.Stamina.Refill();
                pc.Stats.Breath.SetValue(pc.Stats.Breath.Max);
                pc.Breathing.Cooldowns.ResetAll();
            }

            GUILayout.Space(4f);
            GUILayout.Label("PLAYER HIT TEST", _header);
            GUI.enabled = pc != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Light Hit")) HitPlayer(pc, HitReaction.Light, 0f);
            if (GUILayout.Button("Heavy Hit")) HitPlayer(pc, HitReaction.Heavy, 0f);
            if (GUILayout.Button("Knockdown")) HitPlayer(pc, HitReaction.Knockdown, 0f);
            if (GUILayout.Button("Launch")) HitPlayer(pc, HitReaction.Launch, 3f);
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(4f);
            GUILayout.Label("ENEMIES", _header);
            GUILayout.BeginHorizontal();
            GUI.enabled = pc != null;
            if (GUILayout.Button("Spawn Nightspawn")) SpawnEnemy("nightspawn", 6f);
            if (GUILayout.Button("Spawn Hollow Oni")) SpawnEnemy("hollow_oni", 10f);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Kill All Enemies")) EncounterDirector.KillAll();

            GUILayout.Space(4f);
            GUILayout.Label("TIME", _header);
            GUILayout.BeginHorizontal();
            foreach (float preset in SlowMoPresets)
            {
                bool active = time != null && Mathf.Approximately(time.DebugScale, preset);
                if (GUILayout.Toggle(active, preset >= 1f ? "Normal" : $"x{preset:0.##}", GUI.skin.button) && !active && time != null)
                    time.DebugScale = preset;
            }
            GUILayout.EndHorizontal();
            if (time != null)
            {
                GUILayout.BeginHorizontal();
                bool frozen = GUILayout.Toggle(time.DevFrozen, time.DevFrozen ? "FROZEN (click to resume)" : "Freeze / Pause", GUI.skin.button);
                if (frozen != time.DevFrozen) time.DevFrozen = frozen;
                GUI.enabled = time.DevFrozen;
                if (GUILayout.Button("Next Frame")) time.StepFrame();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            GUILayout.Label("VIEW", _header);
            bool hitboxes = GUILayout.Toggle(HitboxDebug.Enabled, " Show Hitboxes");
            if (hitboxes != HitboxDebug.Enabled) SetHitboxes(hitboxes);
            var fps = FpsCounter.Instance;
            if (fps != null)
            {
                bool showFps = GUILayout.Toggle(fps.Visible, " Show FPS (F2)");
                if (showFps != fps.Visible) fps.Toggle();
            }
            var rig = CameraRig.Instance;
            if (rig != null)
            {
                GUILayout.Label($"Camera Mode: {rig.Mode}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Third")) rig.SetMode(CameraMode.ThirdPerson);
                if (GUILayout.Button("First")) rig.SetMode(CameraMode.FirstPerson);
                if (GUILayout.Button("Second")) rig.SetMode(CameraMode.SecondPerson);
                GUILayout.EndHorizontal();
            }

            if (pc != null && pc.Breathing != null && pc.Breathing.Styles.Count > 0)
            {
                GUILayout.Space(4f);
                GUILayout.Label("BREATHING STYLE", _header);
                var styles = pc.Breathing.Styles;
                for (int i = 0; i < styles.Count; i++)
                {
                    bool current = i == pc.Breathing.CurrentIndex;
                    if (GUILayout.Toggle(current, styles[i].displayName, GUI.skin.button) && !current) pc.Breathing.Equip(i, false);
                }
            }

            GUILayout.Space(4f);
            GUILayout.Label("SCENE", _header);
            if (GUILayout.Button("Reset Scene")) ReloadScene();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Forest")) LoadScene(SceneNames.MoonlitForest);
            if (GUILayout.Button("Combat Test")) LoadScene(SceneNames.CombatTest);
            if (GUILayout.Button("Menu")) LoadScene(SceneNames.MainMenu);
            GUILayout.EndHorizontal();
            if (pc != null && GUILayout.Button("Respawn Player")) pc.Respawn();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void SetHitboxes(bool on)
        {
            HitboxDebug.Enabled = on;
            if (on && _hitboxRenderer == null) _hitboxRenderer = gameObject.AddComponent<HitboxDebugRenderer>();
            if (_hitboxRenderer != null) _hitboxRenderer.enabled = on;
        }

        private static void SpawnEnemy(string id, float distance)
        {
            var pc = PlayerController.Instance;
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            var data = db != null ? db.FindEnemy(id) : null;
            if (pc == null || data == null) return;
            Vector3 forward = pc.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 p = pc.transform.position + forward * distance;
            HitQuery.GroundPoint(p + Vector3.up * 4f, out var ground, out _, 10f);
            var enemy = EnemyFactory.Spawn(data, ground + Vector3.up * 0.05f, Quaternion.LookRotation(-forward));
            var boss = enemy.GetComponent<BossController>();
            if (boss != null) boss.StartEncounter();
            else enemy.Alert();
        }

        private void ReloadScene()
        {
            CloseSilently();
            if (SceneLoader.Instance != null) SceneLoader.Instance.ReloadCurrent();
        }

        private void LoadScene(string scene)
        {
            CloseSilently();
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(scene);
        }

        private void CloseSilently()
        {
            if (IsOpen) Toggle();
        }
    }
}
