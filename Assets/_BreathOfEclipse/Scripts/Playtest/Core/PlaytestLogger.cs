using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BreathOfEclipse.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Collects test results and runtime errors (exceptions, asserts, errors via Application.logMessageReceived)
    /// and writes a readable TXT + a JSON report to PlaytestReports/. Normal log messages are ignored.
    /// </summary>
    public sealed class PlaytestLogger
    {
        [Serializable]
        public sealed class LoggedError
        {
            public string type;
            public string message;
            public string stack;
            public int count;
            public float firstTime;
            public string scene;
        }

        [Serializable]
        private sealed class JsonReport
        {
            public string game = "BREATH OF ECLIPSE";
            public string version;
            public string unity;
            public string platform;
            public string date;
            public string mode;
            public string speed;
            public string endReason;
            public float durationSeconds;
            public List<string> scenes = new List<string>();
            public int passed, failed, warnings, notTested;
            public int exceptions, errors, asserts;
            public RuntimePerformanceMonitor.Summary performance;
            public List<TestResult> results = new List<TestResult>();
            public List<LoggedError> runtimeErrors = new List<LoggedError>();
        }

        private readonly List<TestResult> _results = new List<TestResult>();
        private readonly Dictionary<string, LoggedError> _errors = new Dictionary<string, LoggedError>();
        private readonly List<string> _scenes = new List<string>();
        private readonly object _lock = new object();
        private readonly Queue<float> _recentErrorTimes = new Queue<float>();
        private float _startTime;
        private bool _capturing;
        private volatile bool _loopDetected;

        public IReadOnlyList<TestResult> Results => _results;
        public int ExceptionCount { get; private set; }
        public int ErrorCount { get; private set; }
        public int AssertCount { get; private set; }
        public int TotalRuntimeProblems => ExceptionCount + ErrorCount + AssertCount;
        /// <summary>More than 60 errors in 5 seconds: the session should abort.</summary>
        public bool ExceptionLoopDetected => _loopDetected;
        public string LastReportPath { get; private set; }
        public event Action<TestResult> ResultAdded;

        public void Begin()
        {
            _results.Clear();
            lock (_lock)
            {
                _errors.Clear();
                _recentErrorTimes.Clear();
                ExceptionCount = ErrorCount = AssertCount = 0;
            }
            _scenes.Clear();
            _loopDetected = false;
            _startTime = Time.realtimeSinceStartup;
            LastReportPath = null;
            NoteScene(SceneManager.GetActiveScene().name);
            if (!_capturing)
            {
                Application.logMessageReceivedThreaded += OnLog;
                _capturing = true;
            }
        }

        public void StopCapture()
        {
            if (!_capturing) return;
            Application.logMessageReceivedThreaded -= OnLog;
            _capturing = false;
        }

        public void NoteScene(string scene)
        {
            if (!string.IsNullOrEmpty(scene) && (_scenes.Count == 0 || _scenes[_scenes.Count - 1] != scene)) _scenes.Add(scene);
        }

        public TestResult Add(string category, string name, TestStatus status, string details)
        {
            var r = new TestResult
            {
                category = category,
                name = name,
                Status = status,
                status = StatusLabel(status),
                details = details ?? string.Empty,
                time = Time.realtimeSinceStartup - _startTime
            };
            _results.Add(r);
            Debug.Log($"[Playtest] {category} / {name}: {r.status}{(string.IsNullOrEmpty(details) ? "" : " — " + details)}");
            ResultAdded?.Invoke(r);
            return r;
        }

        public int Count(TestStatus status)
        {
            int n = 0;
            foreach (var r in _results) if (r.Status == status) n++;
            return n;
        }

        public static string StatusLabel(TestStatus s)
        {
            switch (s)
            {
                case TestStatus.Pass: return "PASS";
                case TestStatus.Fail: return "FAIL";
                case TestStatus.Warning: return "WARNING";
                default: return "NOT TESTED";
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            float now = (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            lock (_lock)
            {
                if (type == LogType.Exception) ExceptionCount++;
                else if (type == LogType.Assert) AssertCount++;
                else ErrorCount++;

                string key = type + "|" + condition;
                if (_errors.TryGetValue(key, out var existing))
                {
                    existing.count++;
                }
                else if (_errors.Count < 200)
                {
                    _errors[key] = new LoggedError
                    {
                        type = type.ToString(),
                        message = condition,
                        stack = TrimStack(stackTrace),
                        count = 1,
                        firstTime = -1f,
                        scene = string.Empty
                    };
                }

                _recentErrorTimes.Enqueue(now);
                while (_recentErrorTimes.Count > 0 && now - _recentErrorTimes.Peek() > 5f) _recentErrorTimes.Dequeue();
                if (_recentErrorTimes.Count > 60) _loopDetected = true;
            }
        }

        private static string TrimStack(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return string.Empty;
            var lines = stack.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length && i < 6; i++) sb.AppendLine("      " + lines[i].TrimEnd());
            return sb.ToString();
        }

        /// <summary>Main-thread pass that stamps time/scene on errors captured from other threads.</summary>
        public void Update()
        {
            string scene = SceneManager.GetActiveScene().name;
            float t = Time.realtimeSinceStartup - _startTime;
            lock (_lock)
            {
                foreach (var e in _errors.Values)
                {
                    if (e.firstTime >= 0f) continue;
                    e.firstTime = t;
                    e.scene = scene;
                }
            }
        }

        public List<LoggedError> ErrorsSnapshot()
        {
            lock (_lock) return new List<LoggedError>(_errors.Values);
        }

        // ------------------------------------------------------------------ report

        public string WriteReport(PlaytestMode mode, PlaytestSpeed speed, string endReason, RuntimePerformanceMonitor.Summary perf)
        {
            Update();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dir = PlaytestPaths.EnsureDirectory(PlaytestPaths.Reports);
            string baseName = $"BoE_Playtest_{stamp}_{mode}";
            string txtPath = Path.Combine(dir, baseName + ".txt");
            string jsonPath = Path.Combine(dir, baseName + ".json");
            float duration = Time.realtimeSinceStartup - _startTime;
            var errors = ErrorsSnapshot();

            var sb = new StringBuilder();
            sb.AppendLine("BREATH OF ECLIPSE");
            sb.AppendLine("PLAYTEST REPORT");
            sb.AppendLine(new string('=', 60));
            Line(sb, "Version", "v" + GameManager.Version);
            Line(sb, "Unity", Application.unityVersion);
            Line(sb, "Platform", Application.platform + (Application.isEditor ? " (Editor)" : Debug.isDebugBuild ? " (Development build)" : ""));
            Line(sb, "Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Line(sb, "Mode", mode + " / " + speed);
            Line(sb, "Result", endReason);
            Line(sb, "Duration", $"{duration:0.0} s");
            Line(sb, "Scene", SceneManager.GetActiveScene().name);
            Line(sb, "Scenes visited", string.Join(" -> ", _scenes));
            sb.AppendLine();
            sb.AppendLine("PERFORMANCE");
            Line(sb, "Average FPS", $"{perf.averageFps:0.0}");
            Line(sb, "Minimum FPS", $"{perf.minimumFps:0.0}  (0.5 s windows; worst frame {perf.worstFrameMs:0.0} ms)");
            Line(sb, "Maximum FPS", $"{perf.maximumFps:0.0}");
            Line(sb, "Frame time avg", $"{perf.averageFrameMs:0.00} ms");
            Line(sb, "Frame spikes", $"{perf.spikes} frames > 50 ms (scene loads excluded)");
            Line(sb, "Active enemies", $"peak {perf.peakEnemies}");
            Line(sb, "Active VFX", $"peak {perf.peakVfx}, average {perf.averageVfx:0.0}");
            Line(sb, "Particles (approx.)", $"peak {perf.peakParticles}");
            Line(sb, "Managed memory", $"start {perf.memoryStartMb:0.0} MB, end {perf.memoryEndMb:0.0} MB, peak {perf.memoryPeakMb:0.0} MB");
            sb.AppendLine();
            sb.AppendLine("SUMMARY");
            Line(sb, "Console Exceptions", ExceptionCount.ToString());
            Line(sb, "Console Errors", ErrorCount.ToString());
            Line(sb, "Console Asserts", AssertCount.ToString());
            Line(sb, "Tests Passed", Count(TestStatus.Pass).ToString());
            Line(sb, "Tests Failed", Count(TestStatus.Fail).ToString());
            Line(sb, "Warnings", Count(TestStatus.Warning).ToString());
            Line(sb, "Not Tested", Count(TestStatus.NotTested).ToString());
            sb.AppendLine();

            var categories = new List<string>(PlaytestCategories.Order);
            foreach (var r in _results)
                if (!categories.Contains(r.category)) categories.Add(r.category);
            foreach (var cat in categories)
            {
                bool any = false;
                foreach (var r in _results)
                {
                    if (r.category != cat) continue;
                    if (!any)
                    {
                        sb.AppendLine(cat);
                        sb.AppendLine(new string('-', 60));
                        any = true;
                    }
                    string label = r.name.Length > 44 ? r.name.Substring(0, 44) : r.name;
                    sb.Append("  ").Append(label).Append(' ').Append(new string('.', Mathf.Max(2, 46 - label.Length))).Append(' ').AppendLine(r.status);
                    if (!string.IsNullOrEmpty(r.details)) sb.Append("      ").AppendLine(r.details);
                }
                if (any) sb.AppendLine();
            }

            sb.AppendLine("RUNTIME ERRORS (Exception / Error / Assert)");
            sb.AppendLine(new string('-', 60));
            if (errors.Count == 0) sb.AppendLine("  none");
            foreach (var e in errors)
            {
                sb.AppendLine($"  [{e.type}] x{e.count}  (first at {e.firstTime:0.0} s, scene {e.scene})");
                sb.AppendLine("    " + e.message.Replace("\n", "\n    "));
                if (!string.IsNullOrEmpty(e.stack)) sb.Append(e.stack);
            }
            sb.AppendLine();
            sb.AppendLine("Visual quality, game feel and audio quality require human review:");
            sb.AppendLine("this report only proves which systems ran and what they produced.");

            var json = new JsonReport
            {
                version = GameManager.Version,
                unity = Application.unityVersion,
                platform = Application.platform.ToString(),
                date = DateTime.Now.ToString("s"),
                mode = mode.ToString(),
                speed = speed.ToString(),
                endReason = endReason,
                durationSeconds = duration,
                scenes = new List<string>(_scenes),
                passed = Count(TestStatus.Pass),
                failed = Count(TestStatus.Fail),
                warnings = Count(TestStatus.Warning),
                notTested = Count(TestStatus.NotTested),
                exceptions = ExceptionCount,
                errors = ErrorCount,
                asserts = AssertCount,
                performance = perf,
                results = new List<TestResult>(_results),
                runtimeErrors = errors
            };

            try
            {
                File.WriteAllText(txtPath, sb.ToString());
                File.WriteAllText(jsonPath, JsonUtility.ToJson(json, true));
                LastReportPath = txtPath;
                Debug.Log($"[Playtest] Report written: {txtPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Playtest] Could not write report: {e.Message}");
            }
            return LastReportPath;
        }

        private static void Line(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append(':').Append(new string(' ', Mathf.Max(1, 20 - key.Length))).AppendLine(value);
        }
    }
}
