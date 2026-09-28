using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.AI;
using BreathOfEclipse.Core;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.Scenes;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>Boot → Main Menu → TRAINING, and Main Menu PLAY → Moonlit Forest.</summary>
    public static class SceneFlowSuite
    {
        public static IEnumerable<PlaytestStep> BootAndMenu()
        {
            yield return new PlaytestStep(C.Boot, "Boot scene → Main Menu", BootToMenu, 45f, false);
            yield return new PlaytestStep(C.MainMenu, "Main menu buttons", MenuButtons, 20f, false);
            yield return new PlaytestStep(C.MainMenu, "SETTINGS opens and closes", MenuSettings, 15f, false);
            yield return new PlaytestStep(C.MainMenu, "TRAINING loads 03_CombatTest", MenuTraining, 45f, false);
        }

        public static IEnumerable<PlaytestStep> Forest()
        {
            yield return new PlaytestStep(C.World, "PLAY loads 02_MoonlitForest", MenuPlay, 60f, false);
            yield return new PlaytestStep(C.World, "Moonlit Forest spawn & ground", ForestSpawn, 20f);
            yield return new PlaytestStep(C.World, "Moonlit Forest content", ForestContent, 10f);
        }

        private static MainMenuController Menu => Object.FindAnyObjectByType<MainMenuController>();

        private static IEnumerator BootToMenu(PlaytestContext ctx)
        {
            var w = new WaitResult();
            yield return ctx.LoadScene(SceneNames.Boot, w, 20f);
            if (!w.Success)
            {
                ctx.Fail("00_Boot did not load");
                ctx.Runner.Abort("scene cannot load: 00_Boot");
                yield break;
            }
            yield return ctx.Frames(3);
            SceneSanityChecker.Report(ctx, C.Scenes, "00_Boot", false);

            float start = Time.realtimeSinceStartup;
            yield return ctx.WaitUntil(() => PlaytestContext.ActiveScene == SceneNames.MainMenu && Menu != null &&
                                             (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading), 30f, w);
            if (!w.Success)
            {
                ctx.Fail($"Boot did not continue to 01_MainMenu within 30 s (active scene: {PlaytestContext.ActiveScene})");
                ctx.Runner.Abort("scene cannot load: 01_MainMenu");
                yield break;
            }
            ctx.Logger.NoteScene(SceneNames.MainMenu);
            ctx.Pass($"splash + VFX warm-up, then Main Menu after {Time.realtimeSinceStartup - start:0.0} s");
        }

        private static IEnumerator MenuButtons(PlaytestContext ctx)
        {
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => Menu != null, 5f, w);
            var menu = Menu;
            if (menu == null)
            {
                ctx.Fail("MainMenuController not found");
                yield break;
            }
            yield return ctx.Frames(5);
            SceneSanityChecker.Report(ctx, C.Scenes, "01_MainMenu", false);
            var missing = new List<string>();
            foreach (var id in new[] { "Play", "Training", "Settings", "Quit" })
            {
                if (!menu.Buttons.TryGetValue(id, out var b) || b == null || !b.isActiveAndEnabled || !b.interactable) missing.Add(id);
            }
            if (missing.Count == 0) ctx.Pass("PLAY, TRAINING, SETTINGS, QUIT present and interactable (QUIT not clicked)");
            else ctx.Fail("missing / disabled: " + string.Join(", ", missing));
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator MenuSettings(PlaytestContext ctx)
        {
            var menu = Menu;
            if (menu == null || !menu.Buttons.TryGetValue("Settings", out var settings))
            {
                ctx.Fail("menu or SETTINGS button not found");
                yield break;
            }
            settings.onClick.Invoke();
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => menu.SettingsOpen, 2f, w);
            bool opened = w.Success;
            yield return ctx.Observe(1.5f);
            menu.CloseSettingsPanel();
            yield return ctx.WaitUntil(() => !menu.SettingsOpen, 2f, w);
            if (opened && w.Success) ctx.Pass("settings panel opened from the menu button and closed");
            else ctx.Fail(opened ? "settings panel did not close" : "settings panel did not open");
        }

        private static IEnumerator MenuTraining(PlaytestContext ctx)
        {
            var menu = Menu;
            if (menu == null || !menu.Buttons.TryGetValue("Training", out var training))
            {
                ctx.Fail("menu or TRAINING button not found");
                ctx.Runner.Abort("scene cannot load: TRAINING unavailable");
                yield break;
            }
            float start = Time.realtimeSinceStartup;
            training.onClick.Invoke();
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => PlaytestContext.ActiveScene == SceneNames.CombatTest && ctx.Player != null &&
                                             (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading), 35f, w);
            if (!w.Success)
            {
                ctx.Fail("TRAINING did not load 03_CombatTest with a player within 35 s");
                ctx.Runner.Abort("scene cannot load: 03_CombatTest");
                yield break;
            }
            ctx.Logger.NoteScene(SceneNames.CombatTest);
            yield return ctx.Frames(10);
            SceneSanityChecker.Report(ctx, C.Scenes, "03_CombatTest", true);
            ctx.Pass($"loaded in {Time.realtimeSinceStartup - start:0.0} s");
        }

        private static IEnumerator MenuPlay(PlaytestContext ctx)
        {
            var w = new WaitResult();
            yield return ctx.LoadScene(SceneNames.MainMenu, w, 25f);
            yield return ctx.WaitUntil(() => Menu != null, 5f, w);
            var menu = Menu;
            if (menu == null || !menu.Buttons.TryGetValue("Play", out var play))
            {
                ctx.Fail("could not reach the main menu PLAY button");
                yield break;
            }
            yield return ctx.Frames(5);
            float start = Time.realtimeSinceStartup;
            play.onClick.Invoke();
            yield return ctx.WaitUntil(() => PlaytestContext.ActiveScene == SceneNames.MoonlitForest && ctx.Player != null &&
                                             (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading), 50f, w);
            if (!w.Success)
            {
                ctx.Fail("PLAY did not load 02_MoonlitForest with a player within 50 s");
                yield break;
            }
            ctx.Logger.NoteScene(SceneNames.MoonlitForest);
            yield return ctx.Frames(10);
            SceneSanityChecker.Report(ctx, C.Scenes, "02_MoonlitForest", true);
            ctx.Pass($"forest built and loaded in {Time.realtimeSinceStartup - start:0.0} s");
        }

        private static IEnumerator ForestSpawn(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => pc != null && pc.Motor.Grounded, 4f, w);
            ctx.Check("Grounded after spawn", w.Success, pc != null ? $"position {pc.transform.position}" : "no player");
            float y0 = pc.transform.position.y;
            yield return ctx.WaitGame(1.5f);
            float drop = y0 - pc.transform.position.y;
            ctx.Check("Not falling through the ground", drop < 1f && pc.transform.position.y > -10f, $"vertical change {-drop:0.00} m in 1.5 s");
            ctx.Screens.CaptureAuto("MoonlitForest");

            // Walk the path for a moment: terrain collision and movement on slopes.
            Vector3 start = pc.transform.position;
            ctx.Driver.MoveWorld(pc.transform.forward);
            yield return ctx.WaitGame(2f);
            ctx.Driver.Stop();
            float moved = PlaytestContext.Flat(start, pc.transform.position);
            ctx.Check("Walks on the forest path", moved > 3f && pc.Motor.Grounded, $"moved {moved:0.0} m, grounded {pc.Motor.Grounded}");
            ctx.Pass($"spawn at {start}, walked {moved:0.0} m");
            yield return ctx.Observe(2f);
        }

        private static IEnumerator ForestContent(PlaytestContext ctx)
        {
            int renderers = 0;
            bool water = false, sky = false;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                renderers++;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null) continue;
                if (m.shader.name == ShaderIds.ToonWater) water = true;
                if (m.shader.name == ShaderIds.SkyDome) sky = true;
            }
            var boss = Object.FindAnyObjectByType<BossController>();
            int enemies = EncounterDirector.AliveCount;
            ctx.Check("World geometry", renderers > 300, $"{renderers} mesh renderers (trees, rocks, temple, props)", true);
            ctx.Check("Stream (ToonWater)", water, water ? "water surface present" : "no ToonWater renderer", true);
            ctx.Check("Sky dome + moon", sky && RenderSettings.sun != null, $"sky {(sky ? "present" : "missing")}, moonlight {(RenderSettings.sun != null ? "set" : "missing")}", true);
            ctx.Check("Fog", RenderSettings.fog, $"density {RenderSettings.fogDensity:0.000}", true);
            ctx.Check("Enemies placed", enemies >= 5, $"{enemies} Nightspawn / bosses alive", true);
            ctx.Check("Hollow Oni waits in the temple", boss != null && !boss.EncounterStarted, boss != null ? "boss present, encounter not started" : "no boss", true);
            ctx.Check("Checkpoints", Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length >= 2, "lantern checkpoints", true);
            ctx.Pass("content present (visual quality needs human review)");
            yield break;
        }
    }
}
