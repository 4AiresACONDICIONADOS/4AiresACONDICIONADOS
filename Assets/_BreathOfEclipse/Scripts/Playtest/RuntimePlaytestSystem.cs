using System.Collections;
using System.IO;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.Scenes;
using BreathOfEclipse.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Development-only playtest hub (Editor / Development builds; this assembly is not compiled into release
    /// builds). Dormant until used: adds DEVELOPER PLAYTEST to the main menu, captures screenshots (F8) and runs
    /// sessions — automated tests (Full / Auto / AI / Boss) or interactive labs (Manual / VFX / Camera) — with the
    /// runtime logger, exception capture, performance monitor and a report in PlaytestReports/.
    ///
    /// Keys during a session: F8 screenshot · F9 pause test · F10 continue · F11 skip test · F12 abort / exit lab.
    /// Settings changed during a session go to a temporary copy; the real settings file is never modified.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class RuntimePlaytestSystem : MonoBehaviour
    {
        public static RuntimePlaytestSystem Instance { get; private set; }

        public bool Running { get; private set; }
        public PlaytestMode Mode { get; private set; }
        public PlaytestSpeed Speed { get; private set; }
        public string Status { get; private set; } = "IDLE";
        public string LastReportPath { get; private set; }

        private readonly PlaytestLogger _logger = new PlaytestLogger();
        private readonly TelemetryRecorder _telemetry = new TelemetryRecorder();
        private readonly PlaytestScreenshots _screens = new PlaytestScreenshots();
        private readonly RuntimePerformanceMonitor _perf = new RuntimePerformanceMonitor();
        private readonly SettingsSandbox _sandbox = new SettingsSandbox();
        private readonly FrameSampler _sampler = new FrameSampler();
        private PlaytestContext _ctx;
        private PlaytestRunner _runner;
        private PlaytestLab _lab;
        private AutoPlaytestDriver _driver;
        private bool _exitLab;
        private bool _showSummary;
        private float _summaryUntil;
        private string _endReason;
        private bool _prevGod, _prevBreath, _prevStamina;

        private MainMenuController _menu;
        private bool _menuOpen;
        private PlaytestSpeed _menuSpeed = PlaytestSpeed.Normal;
        private Texture2D _bg;
        private GUIStyle _title, _small, _line;

        // ------------------------------------------------------------------ bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => MainMenuController.MenuBuilt += OnMenuBuilt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAfterLoad() => Ensure();

        public static RuntimePlaytestSystem Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[BreathOfEclipse Playtest]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RuntimePlaytestSystem>();
            return Instance;
        }

        private static void OnMenuBuilt(MainMenuController menu)
        {
            menu.AddMenuButton("DeveloperPlaytest", "DEVELOPER PLAYTEST", () => Ensure().OpenDeveloperMenu(menu));
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _telemetry.Detach();
            _logger.StopCapture();
        }

        private void OnApplicationQuit()
        {
            if (Running) Finish("INTERRUPTED (application quit / play mode stopped)");
        }

        // ------------------------------------------------------------------ API

        /// <summary>Starts a session. Automated modes run by themselves; labs run until F12 / EXIT.</summary>
        public void Begin(PlaytestMode mode, PlaytestSpeed speed)
        {
            if (Running)
            {
                GameEvents.Notify("A playtest session is already running (F12 to stop it)");
                return;
            }
            StartCoroutine(Session(mode, speed));
        }

        public void OpenDeveloperMenu(MainMenuController menu)
        {
            _menu = menu;
            _menuOpen = true;
            if (_menu != null) _menu.SetMenuVisible(false);
        }

        private void CloseDeveloperMenu()
        {
            _menuOpen = false;
            if (_menu != null) _menu.SetMenuVisible(true);
        }

        // ------------------------------------------------------------------ session

        private IEnumerator Session(PlaytestMode mode, PlaytestSpeed speed)
        {
            Running = true;
            Mode = mode;
            Speed = speed;
            Status = "STARTING";
            _showSummary = false;
            _exitLab = false;
            _menuOpen = false;

            _logger.Begin();
            _perf.Begin();
            _screens.BeginSession();
            _telemetry.Clear();
            _telemetry.Attach();
            _sampler.Reset();
            _sandbox.Enter();
            _prevGod = DebugMenu.GodMode;
            _prevBreath = DebugMenu.InfiniteBreath;
            _prevStamina = DebugMenu.InfiniteStamina;

            var input = InputReader.Instance;
            if (input == null || GameManager.Instance == null)
            {
                _logger.Add(PlaytestCategories.Scenes, "Session start", TestStatus.Fail, "GameManager / InputReader missing");
                Finish("ABORTED: core services missing");
                yield break;
            }
            _driver = new AutoPlaytestDriver(input);
            _ctx = new PlaytestContext
            {
                Mode = mode,
                Speed = speed,
                Driver = _driver,
                Logger = _logger,
                Telemetry = _telemetry,
                Screens = _screens,
                Perf = _perf,
                Sampler = _sampler
            };
            Debug.Log($"[Playtest] Session started: {mode} / {speed}");

            if (PlaytestPlans.IsAutomated(mode))
            {
                _runner = new PlaytestRunner(PlaytestPlans.Build(mode));
                _ctx.Runner = _runner;
                Status = "RUNNING";
                yield return _runner.Run(_ctx);
                Finish(_runner.AbortRequested ? "ABORTED: " + _runner.AbortReason : "COMPLETED");
                yield break;
            }

            // Interactive labs.
            Status = "LOADING";
            var w = new WaitResult();
            yield return _ctx.EnsureCombatTest(w);
            if (!w.Success)
            {
                _logger.Add(PlaytestCategories.Scenes, "03_CombatTest ready", TestStatus.Fail, "scene could not be loaded");
                Finish("ABORTED: 03_CombatTest could not be loaded");
                yield break;
            }
            SceneSanityChecker.Report(_ctx, PlaytestCategories.Scenes, "03_CombatTest", true);
            _lab = CreateLab(mode);
            _lab.Begin(_ctx);
            Status = "RUNNING";
            float missingPlayer = 0f;
            while (!_exitLab && !_lab.ExitRequested)
            {
                missingPlayer = PlayerController.Instance == null ? missingPlayer + Time.unscaledDeltaTime : 0f;
                if (missingPlayer > 2f)
                {
                    _endReason = "scene changed";
                    break;
                }
                yield return null;
            }
            _lab.End();
            Destroy(_lab);
            _lab = null;
            Finish(missingPlayer > 2f ? "LAB CLOSED (scene changed)" : "LAB CLOSED");
        }

        private PlaytestLab CreateLab(PlaytestMode mode)
        {
            switch (mode)
            {
                case PlaytestMode.CameraLab:
                    return gameObject.AddComponent<CameraLab>();
                case PlaytestMode.Manual:
                    return gameObject.AddComponent<ManualChecklistLab>();
                case PlaytestMode.RisingSerpentVisual:
                {
                    var lab = gameObject.AddComponent<VfxLab>();
                    lab.PresetStyle = "tidal";
                    lab.PresetSkillId = "tidal_rising_serpent";
                    lab.PresetInterval = 3f;
                    return lab;
                }
                default:
                    return gameObject.AddComponent<VfxLab>();
            }
        }

        private void Finish(string endReason)
        {
            _endReason = endReason;
            if (_runner != null) _runner.Paused = false;
            _driver?.Dispose();
            _driver = null;
            var time = TimeController.Instance;
            if (time != null)
            {
                time.DevFrozen = false;
                time.DebugScale = 1f;
            }
            DebugMenu.GodMode = _prevGod;
            DebugMenu.InfiniteBreath = _prevBreath;
            DebugMenu.InfiniteStamina = _prevStamina;
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                pc.Stats.GodMode = _prevGod;
                pc.Stats.InfiniteBreath = _prevBreath;
                pc.Stats.InfiniteStamina = _prevStamina;
            }
            _sandbox.Exit();
            _telemetry.Detach();
            _perf.Stop();
            LastReportPath = _logger.WriteReport(Mode, Speed, endReason, _perf.GetSummary());
            _logger.StopCapture();
            Status = endReason.StartsWith("COMPLETED") ? "FINISHED" : endReason.StartsWith("LAB") ? "LAB CLOSED" : "ABORTED";
            Running = false;
            _runner = null;
            _showSummary = true;
            _summaryUntil = Time.unscaledTime + 60f;
            if (pc != null && !PauseMenu.IsOpen && !DebugMenu.IsOpen) CursorManager.SetGameplay(true);
            Debug.Log($"[Playtest] Session finished: {endReason}. Report: {LastReportPath}");
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            _perf.Tick();
            if (Running)
            {
                _logger.Update();
                _sampler.Tick();
            }
            HandleKeys();
            if (_showSummary && Time.unscaledTime > _summaryUntil) _showSummary = false;
        }

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f8Key.wasPressedThisFrame)
            {
                string label = _runner != null && _runner.Current != null ? _runner.Current.Name : _lab != null ? _lab.Title : "Manual";
                _screens.Capture(label);
                GameEvents.Notify("Screenshot saved to PlaytestCaptures/");
            }
            if (!Running)
            {
                if (_showSummary && kb.f12Key.wasPressedThisFrame) _showSummary = false;
                return;
            }
            if (_runner != null)
            {
                if (kb.f9Key.wasPressedThisFrame && !_runner.Paused)
                {
                    _runner.Paused = true;
                    _driver?.Stop();
                    if (TimeController.Instance != null) TimeController.Instance.DevFrozen = true;
                    Status = "PAUSED";
                }
                if (kb.f10Key.wasPressedThisFrame && _runner.Paused)
                {
                    _runner.Paused = false;
                    if (TimeController.Instance != null) TimeController.Instance.DevFrozen = false;
                    Status = "RUNNING";
                }
                if (kb.f11Key.wasPressedThisFrame) _runner.SkipRequested = true;
                if (kb.f12Key.wasPressedThisFrame)
                {
                    if (_runner.Paused)
                    {
                        _runner.Paused = false;
                        if (TimeController.Instance != null) TimeController.Instance.DevFrozen = false;
                    }
                    _runner.Abort("aborted by tester (F12)");
                }
            }
            else if (kb.f12Key.wasPressedThisFrame)
            {
                _exitLab = true;
            }
        }

        // ------------------------------------------------------------------ GUI

        private void EnsureStyles()
        {
            if (_title != null) return;
            _bg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _bg.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.06f, 0.78f));
            _bg.Apply();
            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.62f, 0.85f, 1f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
            _line = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
        }

        private void OnGUI()
        {
            if (!Running && !_showSummary && !_menuOpen) return;
            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float x = DebugMenu.IsOpen ? 410f : 12f;

            if (_menuOpen && !Running) DrawDeveloperMenu(Screen.width / scale, Screen.height / scale);
            else if (Running && _lab != null) DrawLab(new Rect(x, 12f, 380f, 640f));
            else if (Running) DrawRunner(new Rect(x, 12f, 380f, 250f));
            else if (_showSummary) DrawSummary(new Rect(x, 12f, 400f, 210f));

            GUI.matrix = old;
        }

        private void Panel(Rect r) => GUI.DrawTexture(r, _bg);

        private string Counts() =>
            $"PASS {_logger.Count(TestStatus.Pass)}  FAIL {_logger.Count(TestStatus.Fail)}  WARN {_logger.Count(TestStatus.Warning)}  N/T {_logger.Count(TestStatus.NotTested)}";

        private static string StatusColor(TestStatus s)
        {
            switch (s)
            {
                case TestStatus.Pass: return "#7CFF9A";
                case TestStatus.Fail: return "#FF6B6B";
                case TestStatus.Warning: return "#FFD166";
                default: return "#A0A4B8";
            }
        }

        private void DrawRunner(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height - 12f));
            GUILayout.Label("BREATH OF ECLIPSE", _title);
            GUILayout.Label($"RUNTIME VALIDATION — {Mode.ToString().ToUpperInvariant()} ({Speed.ToString().ToUpperInvariant()})", _small);
            var step = _runner != null ? _runner.Current : null;
            int index = _runner != null ? Mathf.Clamp(_runner.Index + 1, 1, _runner.Steps.Count) : 0;
            GUILayout.Label($"TEST {index:00} / {(_runner != null ? _runner.Steps.Count : 0):00}   {(step != null ? step.Category : "")}", _line);
            GUILayout.Label($"Testing: <b>{(step != null ? step.Name : "-")}</b>", _line);
            string statusColor = Status == "PAUSED" ? "#FFD166" : "#7CFF9A";
            GUILayout.Label($"STATUS: <color={statusColor}>{Status}</color>    FPS: {_perf.CurrentFps:0}    ERRORS: {_logger.TotalRuntimeProblems}", _line);
            GUILayout.Label(Counts(), _small);
            var results = _logger.Results;
            for (int i = Mathf.Max(0, results.Count - 4); i < results.Count; i++)
            {
                var res = results[i];
                string name = res.name.Length > 40 ? res.name.Substring(0, 40) + "…" : res.name;
                GUILayout.Label($"<color={StatusColor(res.Status)}>{res.status}</color>  {name}", _line);
            }
            GUILayout.Label("F8 shot · F9 pause · F10 continue · F11 skip · F12 abort", _small);
            GUILayout.EndArea();
        }

        private void DrawLab(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height - 12f));
            GUILayout.Label("BREATH OF ECLIPSE — " + _lab.Title, _title);
            GUILayout.Label($"FPS: {_perf.CurrentFps:0}    ERRORS: {_logger.TotalRuntimeProblems}    VFX: {_perf.ActiveVfx}    Enemies: {_perf.ActiveEnemies}", _line);
            GUILayout.EndArea();
            _lab.DrawPanel(new Rect(r.x + 10f, r.y + 50f, r.width - 20f, r.height - 58f));
        }

        private void DrawSummary(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height - 12f));
            GUILayout.Label("BREATH OF ECLIPSE — PLAYTEST " + Status, _title);
            GUILayout.Label($"{Mode} / {Speed}: {_endReason}", _small);
            GUILayout.Label(Counts(), _line);
            GUILayout.Label($"Console exceptions: {_logger.ExceptionCount}   errors: {_logger.ErrorCount}", _line);
            var p = _perf.GetSummary();
            GUILayout.Label($"FPS avg {p.averageFps:0} · min {p.minimumFps:0} · max {p.maximumFps:0}", _line);
            GUILayout.Label("Report: PlaytestReports/" + (LastReportPath != null ? Path.GetFileName(LastReportPath) : "(not written)"), _small);
            GUILayout.Label($"Screenshots this session: {_screens.SessionCaptures.Count} (PlaytestCaptures/)", _small);
            if (GUILayout.Button("Close (F12)")) _showSummary = false;
            GUILayout.EndArea();
        }

        private void DrawDeveloperMenu(float width, float height)
        {
            var r = new Rect(width - 560f, height * 0.5f - 370f, 520f, 740f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 12f, r.width - 32f, r.height - 24f));
            GUILayout.Label("DEVELOPER PLAYTEST", _title);
            GUILayout.Label($"v{GameManager.Version} · Editor / Development builds only · reports → PlaytestReports/", _small);
            GUILayout.Space(6);
            GUILayout.Label("Speed for automated tests", _small);
            GUILayout.BeginHorizontal();
            foreach (PlaytestSpeed s in new[] { PlaytestSpeed.Normal, PlaytestSpeed.Fast, PlaytestSpeed.Visual })
                if (GUILayout.Toggle(_menuSpeed == s, s.ToString().ToUpperInvariant(), GUI.skin.button, GUILayout.Height(28))) _menuSpeed = s;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            Option("MANUAL TEST", "Play CombatTest normally; a checklist fills itself. F12 = report.", PlaytestMode.Manual);
            Option("AUTO TEST", "Automated CombatTest suite: movement, combat, techniques (all 20 forms), cameras, UI, audio, save.", PlaytestMode.Auto);
            Option("VFX TEST", "VFX lab: any of the 25 techniques, auto repeat, 0.25x–1.5x, freeze, next frame.", PlaytestMode.VfxLab);
            Option("RISING SERPENT VISUAL", "VFX lab preset: TIDAL BREATH — RISING SERPENT every 3 s.", PlaytestMode.RisingSerpentVisual);
            Option("CAMERA TEST", "Camera lab: 1/2/3 camera modes, diagnostics, target-lost fallback.", PlaytestMode.CameraLab);
            Option("AI TEST", "Nightspawn: detection, chase, attack, reactions, death.", PlaytestMode.AiTest);
            Option("BOSS TEST", "Hollow Oni: phase 1, phase 2 at 50 %, Eclipse Cleave, ultimate, defeat.", PlaytestMode.BossTest);
            Option("FULL TEST", "Boot → Menu → CombatTest → everything → Moonlit Forest → Living World (~5–8 min).", PlaytestMode.Full);
            Option("FULL WORLD TEST", "v0.5 Living World: village morning, day/night, routines, streaming, demons, events, presence, save.", PlaytestMode.WorldTest);
            GUILayout.Space(6);
            if (GUILayout.Button("BACK", GUILayout.Height(30))) CloseDeveloperMenu();
            GUILayout.EndArea();
        }

        private void Option(string label, string description, PlaytestMode mode)
        {
            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                CloseDeveloperMenu();
                Begin(mode, _menuSpeed);
            }
            GUILayout.Label(description, _small);
        }
    }
}
