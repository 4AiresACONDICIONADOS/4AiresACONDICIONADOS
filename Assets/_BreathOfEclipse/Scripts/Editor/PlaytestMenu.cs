using System.IO;
using BreathOfEclipse.Playtest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BreathOfEclipse.EditorTools
{
    /// <summary>
    /// One-click playtests: opens 03_CombatTest, enters Play Mode and starts the requested session.
    /// The request survives the domain reload through SessionState and is delivered on EnteredPlayMode.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaytestMenu
    {
        private const string RequestKey = "BoE.Playtest.Request";
        private const string CombatTestScene = "Assets/_BreathOfEclipse/Scenes/03_CombatTest.unity";

        static PlaytestMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Breath of Eclipse/Playtest/Full Visual Test", priority = 1)]
        private static void FullVisual() => Launch(PlaytestMode.Full, PlaytestSpeed.Visual);

        [MenuItem("Breath of Eclipse/Playtest/Rising Serpent Visual Test", priority = 2)]
        private static void RisingSerpent() => Launch(PlaytestMode.RisingSerpentVisual, PlaytestSpeed.Visual);

        [MenuItem("Breath of Eclipse/Playtest/Full Test (Normal)", priority = 20)]
        private static void FullNormal() => Launch(PlaytestMode.Full, PlaytestSpeed.Normal);

        [MenuItem("Breath of Eclipse/Playtest/Full Test (Fast)", priority = 21)]
        private static void FullFast() => Launch(PlaytestMode.Full, PlaytestSpeed.Fast);

        [MenuItem("Breath of Eclipse/Playtest/Auto Test (CombatTest suite)", priority = 22)]
        private static void Auto() => Launch(PlaytestMode.Auto, PlaytestSpeed.Normal);

        [MenuItem("Breath of Eclipse/Playtest/AI Test", priority = 23)]
        private static void Ai() => Launch(PlaytestMode.AiTest, PlaytestSpeed.Normal);

        [MenuItem("Breath of Eclipse/Playtest/Boss Test", priority = 24)]
        private static void Boss() => Launch(PlaytestMode.BossTest, PlaytestSpeed.Normal);

        [MenuItem("Breath of Eclipse/Playtest/VFX Lab", priority = 40)]
        private static void VfxLab() => Launch(PlaytestMode.VfxLab, PlaytestSpeed.Normal);

        [MenuItem("Breath of Eclipse/Playtest/Camera Lab", priority = 41)]
        private static void CameraLab() => Launch(PlaytestMode.CameraLab, PlaytestSpeed.Normal);

        [MenuItem("Breath of Eclipse/Playtest/Manual Test (checklist)", priority = 42)]
        private static void Manual() => Launch(PlaytestMode.Manual, PlaytestSpeed.Normal);

        [MenuItem("Breath of Eclipse/Playtest/Open Reports Folder", priority = 60)]
        private static void OpenReports() => Reveal(PlaytestPaths.Reports);

        [MenuItem("Breath of Eclipse/Playtest/Open Screenshots Folder", priority = 61)]
        private static void OpenCaptures() => Reveal(PlaytestPaths.Captures);

        private static void Reveal(string path)
        {
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static void Launch(PlaytestMode mode, PlaytestSpeed speed)
        {
            if (EditorApplication.isPlaying)
            {
                RuntimePlaytestSystem.Ensure().Begin(mode, speed);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatTestScene) == null)
            {
                EditorUtility.DisplayDialog("Playtest", "Missing scene " + CombatTestScene, "OK");
                return;
            }
            EditorSceneManager.OpenScene(CombatTestScene);
            SessionState.SetString(RequestKey, $"{(int)mode}|{(int)speed}");
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.EraseString(RequestKey);
                return;
            }
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            string request = SessionState.GetString(RequestKey, string.Empty);
            if (string.IsNullOrEmpty(request)) return;
            SessionState.EraseString(RequestKey);
            var parts = request.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int mode) || !int.TryParse(parts[1], out int speed)) return;
            RuntimePlaytestSystem.Ensure().Begin((PlaytestMode)mode, (PlaytestSpeed)speed);
            Debug.Log($"[Playtest] Started from the Editor menu: {(PlaytestMode)mode} / {(PlaytestSpeed)speed}");
        }
    }
}
