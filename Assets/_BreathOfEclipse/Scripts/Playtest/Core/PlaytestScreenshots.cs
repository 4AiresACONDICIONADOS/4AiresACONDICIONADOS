using System;
using System.Collections.Generic;
using System.IO;
using BreathOfEclipse.CameraSystem;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Screenshots to PlaytestCaptures/ named YYYYMMDD_HHMMSS_Camera_Label.png (F8 anytime in Editor /
    /// Development builds; automatic key moments during tests, capped per session).
    /// </summary>
    public sealed class PlaytestScreenshots
    {
        public const int AutoLimit = 15;

        private readonly HashSet<string> _usedNames = new HashSet<string>();
        public int AutoCount { get; private set; }
        public int TotalCount { get; private set; }
        public string LastPath { get; private set; }
        public readonly List<string> SessionCaptures = new List<string>();

        public void BeginSession()
        {
            AutoCount = 0;
            SessionCaptures.Clear();
        }

        public static string CameraLabel()
        {
            var rig = CameraRig.Instance;
            if (rig == null) return "NoCamera";
            if (rig.CinematicActive) return "Cinematic";
            return rig.Mode.ToString();
        }

        /// <summary>Automatic capture during tests (ignored after <see cref="AutoLimit"/>).</summary>
        public string CaptureAuto(string label)
        {
            if (AutoCount >= AutoLimit) return null;
            AutoCount++;
            return Capture(label);
        }

        public string Capture(string label)
        {
            try
            {
                string dir = PlaytestPaths.EnsureDirectory(PlaytestPaths.Captures);
                string baseName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{PlaytestPaths.Sanitize(CameraLabel())}_{PlaytestPaths.Sanitize(label)}";
                string name = baseName;
                int n = 2;
                while (_usedNames.Contains(name) || File.Exists(Path.Combine(dir, name + ".png"))) name = baseName + "_" + n++;
                _usedNames.Add(name);
                string path = Path.Combine(dir, name + ".png");
                ScreenCapture.CaptureScreenshot(path);
                LastPath = path;
                TotalCount++;
                SessionCaptures.Add(path);
                Debug.Log($"[Playtest] Screenshot: {path}");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Playtest] Screenshot failed: {e.Message}");
                return null;
            }
        }
    }
}
