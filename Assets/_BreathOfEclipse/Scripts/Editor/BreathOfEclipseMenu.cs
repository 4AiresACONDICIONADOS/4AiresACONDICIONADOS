using System.Collections.Generic;
using System.Text;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.EditorTools
{
    /// <summary>Editor shortcuts: open scenes, play from boot, register build scenes, validate the project setup.</summary>
    public static class BreathOfEclipseMenu
    {
        public const string SceneFolder = "Assets/_BreathOfEclipse/Scenes";

        private static readonly string[] SceneOrder =
        {
            SceneNames.Boot, SceneNames.MainMenu, SceneNames.MoonlitForest, SceneNames.CombatTest, SceneNames.FrontierRegion
        };

        private static string ScenePath(string name) => $"{SceneFolder}/{name}.unity";

        [MenuItem("Breath of Eclipse/Play From Boot %#&p", priority = 0)]
        private static void PlayFromBoot()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Breath of Eclipse/Scenes/00 Boot", priority = 20)]
        private static void OpenBoot() => Open(SceneNames.Boot);

        [MenuItem("Breath of Eclipse/Scenes/01 Main Menu", priority = 21)]
        private static void OpenMenu() => Open(SceneNames.MainMenu);

        [MenuItem("Breath of Eclipse/Scenes/02 Moonlit Forest", priority = 22)]
        private static void OpenForest() => Open(SceneNames.MoonlitForest);

        [MenuItem("Breath of Eclipse/Scenes/03 Combat Test", priority = 23)]
        private static void OpenCombatTest() => Open(SceneNames.CombatTest);

        [MenuItem("Breath of Eclipse/Scenes/04 Frontier Region (Living World)", priority = 24)]
        private static void OpenFrontier() => Open(SceneNames.FrontierRegion);

        private static void Open(string scene)
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath(scene));
        }

        [MenuItem("Breath of Eclipse/Setup/Register Build Scenes", priority = 40)]
        public static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var name in SceneOrder)
            {
                string path = ScenePath(name);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogError($"[Breath of Eclipse] Missing scene {path}");
                    continue;
                }
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Breath of Eclipse] Registered {scenes.Count} build scenes.");
        }

        [MenuItem("Breath of Eclipse/Setup/Validate Project", priority = 41)]
        public static void ValidateProject()
        {
            var report = new StringBuilder();
            int problems = 0;
            void Check(bool ok, string what)
            {
                report.AppendLine((ok ? "  OK   " : "  FAIL ") + what);
                if (!ok) problems++;
            }

            Check(GraphicsSettings.currentRenderPipeline != null, "URP asset active (Graphics / Quality settings)");
            foreach (var shader in new[]
                     {
                         ShaderIds.ToonLit, ShaderIds.VfxAdditive, ShaderIds.VfxAlpha, ShaderIds.ElementRibbon, ShaderIds.SwordTrail,
                         ShaderIds.Ghost, ShaderIds.SkyDome, ShaderIds.ToonWater, ShaderIds.Distortion, ShaderIds.Telegraph,
                         ShaderIds.UISpeedLines, ShaderIds.UIRadialBurst
                     })
            {
                var s = Shader.Find(shader);
                Check(s != null && s.isSupported, $"Shader {shader}{(s != null && !s.isSupported ? " (not supported on this GPU)" : "")}");
            }
            Check(Resources.Load<UnityEngine.InputSystem.InputActionAsset>("BreathOfEclipse/Input/BreathOfEclipseControls") != null,
                "Input actions asset in Resources");
            foreach (var name in SceneOrder) Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath(name)) != null, $"Scene {name}");
            var build = EditorBuildSettings.scenes;
            Check(build.Length >= SceneOrder.Length && build[0].path == ScenePath(SceneNames.Boot), "Build scenes registered (00_Boot first)");
            Check(AssetDatabase.LoadAssetAtPath<Data.GameDatabase>(ContentExporter.DatabasePath) != null,
                "GameDatabase asset (optional: code defaults are used when missing)");

            string header = problems == 0 ? "[Breath of Eclipse] Project validation passed." : $"[Breath of Eclipse] Project validation found {problems} problem(s).";
            if (problems == 0) Debug.Log(header + "\n" + report);
            else Debug.LogWarning(header + "\n" + report);
        }

        [MenuItem("Breath of Eclipse/Documentation", priority = 100)]
        private static void OpenDocs()
        {
            // Documentation lives next to Assets/ (project root) so it is versioned but not imported.
            string readme = System.IO.Path.GetFullPath("Documentation/README.md");
            if (System.IO.File.Exists(readme)) EditorUtility.OpenWithDefaultApp(readme);
            else Debug.LogWarning("[Breath of Eclipse] Documentation/README.md not found at the project root.");
        }
    }
}
