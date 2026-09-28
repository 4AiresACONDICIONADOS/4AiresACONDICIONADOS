using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Player;
using BreathOfEclipse.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Component-based scene validation (never by GameObject names): cameras, audio listeners, core services,
    /// input, EventSystem, player, UI canvases, lighting and render pipeline.
    /// </summary>
    public static class SceneSanityChecker
    {
        public struct Check
        {
            public string Name;
            public TestStatus Status;
            public string Details;
        }

        public static List<Check> Run(bool expectGameplay)
        {
            var checks = new List<Check>();
            void Add(string name, TestStatus status, string details) => checks.Add(new Check { Name = name, Status = status, Details = details });

            // Cameras rendering to the screen.
            int screenCameras = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (cam.enabled && cam.targetTexture == null) screenCameras++;
            Add("One gameplay camera", screenCameras == 1 ? TestStatus.Pass : TestStatus.Fail, $"{screenCameras} enabled screen camera(s)");

            int listeners = 0;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.enabled) listeners++;
            Add("AudioListener", listeners == 1 ? TestStatus.Pass : TestStatus.Fail, $"{listeners} enabled listener(s)");

            Add("GameManager", GameManager.Instance != null ? TestStatus.Pass : TestStatus.Fail, GameManager.Instance != null ? "persistent services present" : "missing");
            var input = InputReader.Instance;
            Add("Input system", input != null && input.Asset != null ? TestStatus.Pass : TestStatus.Fail,
                input == null ? "InputReader missing" : input.Asset == null ? "actions asset missing" : $"gameplay input {(input.GameplayEnabled ? "enabled" : "disabled")}");

            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bool moduleOk = systems.Length == 1 && systems[0].GetComponent<InputSystemUIInputModule>() != null;
            Add("EventSystem", moduleOk ? TestStatus.Pass : TestStatus.Fail, $"{systems.Length} EventSystem(s), Input System UI module {(moduleOk ? "ok" : "missing")}");

            int canvases = 0;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (c.isRootCanvas && c.enabled) canvases++;
            Add("UI canvas", canvases > 0 ? TestStatus.Pass : TestStatus.Fail, $"{canvases} root canvas(es)");

            var pipeline = GraphicsSettings.currentRenderPipeline;
            Add("Render pipeline", pipeline is UniversalRenderPipelineAsset ? TestStatus.Pass : TestStatus.Fail, pipeline != null ? pipeline.name : "none (built-in)");

            if (expectGameplay)
            {
                var pc = PlayerController.Instance;
                Add("Player", pc != null ? TestStatus.Pass : TestStatus.Fail, pc != null ? $"HP {pc.Damageable.Health.Current:0}/{pc.Damageable.Health.Max:0}" : "no PlayerController");
                Add("HUD", Object.FindAnyObjectByType<HUDController>() != null ? TestStatus.Pass : TestStatus.Fail, "HUDController");
                Add("Scene builder", Object.FindAnyObjectByType<GameplaySceneBuilder>() != null ? TestStatus.Pass : TestStatus.Warning, "GameplaySceneBuilder component");

                int directional = 0;
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (l.enabled && l.type == LightType.Directional) directional++;
                bool lit = RenderSettings.sun != null && directional > 0;
                Add("Lighting", lit ? TestStatus.Pass : TestStatus.Warning,
                    $"{directional} directional light(s), sun {(RenderSettings.sun != null ? "set" : "missing")}, fog {(RenderSettings.fog ? $"on ({RenderSettings.fogDensity:0.000})" : "off")}");
            }
            return checks;
        }

        /// <summary>Runs the checks and reports them as results of the given category.</summary>
        public static bool Report(PlaytestContext ctx, string category, string prefix, bool expectGameplay)
        {
            bool allOk = true;
            foreach (var c in Run(expectGameplay))
            {
                ctx.Report(category, $"{prefix}: {c.Name}", c.Status, c.Details);
                if (c.Status == TestStatus.Fail) allOk = false;
            }
            return allOk;
        }

        public static string ActiveScene => SceneManager.GetActiveScene().name;
    }
}
