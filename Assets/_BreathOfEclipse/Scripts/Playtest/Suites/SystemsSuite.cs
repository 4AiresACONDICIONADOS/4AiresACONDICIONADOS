using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.UI;
using BreathOfEclipse.VFX;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>UI, audio, save system, VFX library and the performance summary.</summary>
    public static class SystemsSuite
    {
        public static IEnumerable<PlaytestStep> Ui()
        {
            yield return new PlaytestStep(C.UI, "HUD", Hud, 5f);
            yield return new PlaytestStep(C.UI, "Pause menu (Esc)", PauseMenuTest, 8f);
            yield return new PlaytestStep(C.UI, "Debug menu (F1)", DebugMenuTest, 8f);
            yield return new PlaytestStep(C.UI, "Damage numbers", DamageNumbersTest, 5f);
        }

        public static IEnumerable<PlaytestStep> AudioSteps()
        {
            yield return new PlaytestStep(C.Audio, "Audio system", AudioTest, 20f);
        }

        public static IEnumerable<PlaytestStep> SaveSteps()
        {
            yield return new PlaytestStep(C.Save, "Settings save / reload (temporary storage)", SaveTest, 5f, false);
        }

        public static IEnumerable<PlaytestStep> VfxLibrary()
        {
            yield return new PlaytestStep(C.Vfx, "VFX library: every recipe spawns", VfxSmoke, 20f, false);
        }

        public static IEnumerable<PlaytestStep> PerformanceSteps()
        {
            yield return new PlaytestStep(C.Performance, "Performance summary", Performance, 5f, false);
        }

        // ------------------------------------------------------------------ UI

        private static IEnumerator Hud(PlaytestContext ctx)
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<HUDController>();
            var canvas = hud != null ? hud.GetComponent<Canvas>() : null;
            if (hud != null && canvas != null && canvas.enabled) ctx.Pass("HUD canvas active (HP, stamina, BREATH, skills, combo, boss bar, lock-on)");
            else ctx.Fail("HUDController / canvas missing");
            float combo = TelemetryRecorder.MaxSince(ctx.Telemetry.Combo, 0f);
            ctx.Sub("Combo counter events", combo >= 2f ? TestStatus.Pass : TestStatus.NotTested, $"highest combo this session {combo:0}");
            yield break;
        }

        private static IEnumerator PauseMenuTest(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            ctx.Driver.Press(InputCommand.Pause);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => PauseMenu.IsOpen && TimeController.Instance != null && TimeController.Instance.Paused, 1.5f, w);
            bool opened = w.Success;
            bool timeStopped = Time.timeScale == 0f;
            yield return ctx.WaitReal(ctx.Speed == PlaytestSpeed.Visual ? 1.5f : 0.3f);
            ctx.Driver.Press(InputCommand.Pause);
            yield return ctx.WaitUntil(() => !PauseMenu.IsOpen, 1.5f, w);
            bool closed = w.Success;
            yield return ctx.WaitReal(0.2f);
            bool resumed = Time.timeScale > 0f;
            if (opened && timeStopped && closed && resumed) ctx.Pass("Esc opens (game paused) and closes (game resumed)");
            else ctx.Fail($"opened {opened}, paused {timeStopped}, closed {closed}, resumed {resumed}");
        }

        private static IEnumerator DebugMenuTest(PlaytestContext ctx)
        {
            ctx.Driver.Press(InputCommand.DebugMenu);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => DebugMenu.IsOpen, 1.5f, w);
            bool opened = w.Success;
            yield return ctx.WaitReal(ctx.Speed == PlaytestSpeed.Visual ? 1.5f : 0.3f);
            ctx.Driver.Press(InputCommand.DebugMenu);
            yield return ctx.WaitUntil(() => !DebugMenu.IsOpen, 1.5f, w);
            if (opened && w.Success) ctx.Pass("F1 opens and closes the debug menu");
            else ctx.Fail($"opened {opened}, closed {w.Success}");
        }

        private static IEnumerator DamageNumbersTest(PlaytestContext ctx)
        {
            bool exists = UnityEngine.Object.FindAnyObjectByType<DamageNumbers>() != null;
            bool enabled = SaveSystem.Settings.showDamageNumbers;
            if (exists && enabled) ctx.Pass("DamageNumbers present and enabled in settings");
            else if (exists) ctx.Warn("present but disabled in settings");
            else ctx.Fail("DamageNumbers component missing");
            yield break;
        }

        // ------------------------------------------------------------------ audio

        private static IEnumerator AudioTest(PlaytestContext ctx)
        {
            var audio = AudioManager.Instance;
            int errorsBefore = ctx.Logger.TotalRuntimeProblems;
            if (audio == null)
            {
                ctx.Fail("AudioManager missing");
                yield break;
            }
            int listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.enabled);
            ctx.Check("AudioListener", listeners == 1, $"{listeners} enabled listener(s)");

            Sfx.Play2D("ui_click", 0.6f);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => audio.PlayingSfxCount > 0, 1f, w);
            ctx.Check("SFX source activates", w.Success, $"{audio.PlayingSfxCount} SFX source(s) playing");

            yield return ctx.WaitUntil(() => audio.MusicPlaying, 12f, w);
            ctx.Check("Music source activates", w.Success, $"music '{audio.CurrentMusic}' {(w.Success ? "playing" : "not playing (synthesis may still be running)")}");

            // Volume categories (inside the settings sandbox; restored right after).
            var s = SaveSystem.Settings;
            float music0 = s.musicVolume;
            float cat0 = audio.CategoryVolume(AudioCategory.Music);
            s.musicVolume = Mathf.Max(0.05f, music0 * 0.25f);
            SaveSystem.NotifyChanged();
            yield return ctx.Frames(3);
            float cat1 = audio.CategoryVolume(AudioCategory.Music);
            float sfx = audio.CategoryVolume(AudioCategory.SFX);
            s.musicVolume = music0;
            SaveSystem.NotifyChanged();
            ctx.Check("Volume categories respond", cat1 < cat0 && sfx > 0f, $"music category {cat0:0.00} → {cat1:0.00} (SFX unchanged {sfx:0.00})");

            int errors = ctx.Logger.TotalRuntimeProblems - errorsBefore;
            ctx.Check("No audio exceptions", errors == 0, $"{errors} new error(s) during the audio test");
            ctx.Pass("audio systems respond (sound quality needs human review)");
        }

        // ------------------------------------------------------------------ save

        private static IEnumerator SaveTest(PlaytestContext ctx)
        {
            // Nested temporary storage: the session sandbox (and the real settings file) are never touched.
            var sandboxStorage = SaveSystem.Storage;
            var sandboxSettings = SaveSystem.Settings;
            try
            {
                SaveSystem.Storage = new MemorySaveStorage();
                var s = sandboxSettings.Clone();
                SaveSystem.OverrideSettings(s);
                s.masterVolume = 0.37f;
                s.mouseSensitivity = 2.25f;
                s.lastCameraMode = 1;
                s.lastEquippedStyle = "gale";
                s.fieldOfView = 72f;
                SaveSystem.SaveSettings();

                SaveSystem.OverrideSettings(null);
                var loaded = SaveSystem.LoadSettings();
                var mismatches = new List<string>();
                if (!Mathf.Approximately(loaded.masterVolume, 0.37f)) mismatches.Add($"master volume {loaded.masterVolume}");
                if (!Mathf.Approximately(loaded.mouseSensitivity, 2.25f)) mismatches.Add($"sensitivity {loaded.mouseSensitivity}");
                if (loaded.lastCameraMode != 1) mismatches.Add($"camera {loaded.lastCameraMode}");
                if (loaded.lastEquippedStyle != "gale") mismatches.Add($"style {loaded.lastEquippedStyle}");
                if (!Mathf.Approximately(loaded.fieldOfView, 72f)) mismatches.Add($"FOV {loaded.fieldOfView}");
                if (mismatches.Count == 0) ctx.Pass("master volume, sensitivity, camera preference, breathing style and FOV round-trip");
                else ctx.Fail("mismatch: " + string.Join(", ", mismatches));
            }
            catch (Exception e)
            {
                ctx.Fail("exception: " + e.Message);
            }
            finally
            {
                SaveSystem.Storage = sandboxStorage;
                SaveSystem.OverrideSettings(sandboxSettings);
            }
            yield break;
        }

        // ------------------------------------------------------------------ VFX

        private static IEnumerator VfxSmoke(PlaytestContext ctx)
        {
            var ids = VFXLibrary.Ids.ToList();
            var failed = new List<string>();
            int errorsBefore = ctx.Logger.TotalRuntimeProblems;
            Vector3 hidden = new Vector3(0f, -300f, 0f);
            foreach (var id in ids)
            {
                VFXInstance inst = null;
                try
                {
                    inst = VFXLibrary.Spawn(id, hidden, Quaternion.identity, 1f, Element.None, null, 0.1f);
                }
                catch (Exception e)
                {
                    failed.Add($"{id} ({e.GetType().Name})");
                    continue;
                }
                if (inst == null) failed.Add(id);
            }
            yield return ctx.Frames(3);
            int errors = ctx.Logger.TotalRuntimeProblems - errorsBefore;
            if (failed.Count == 0 && errors == 0) ctx.Pass($"{ids.Count} recipes built and spawned from the pool");
            else ctx.Fail($"{failed.Count} failed ({string.Join(", ", failed.Take(10))}), {errors} console error(s)");
        }

        // ------------------------------------------------------------------ performance

        private static IEnumerator Performance(PlaytestContext ctx)
        {
            var p = ctx.Perf.GetSummary();
            string editor = Application.isEditor ? " (Editor: expect lower numbers than a build)" : "";
            TestStatus fps = p.averageFps >= 50f ? TestStatus.Pass : p.averageFps >= 25f ? TestStatus.Warning : TestStatus.Fail;
            ctx.Report(C.Performance, "Average FPS", fps, $"{p.averageFps:0.0} avg, {p.minimumFps:0.0} min (0.5 s), {p.maximumFps:0.0} max{editor}");
            ctx.Report(C.Performance, "Frame time", p.averageFrameMs < 25f ? TestStatus.Pass : TestStatus.Warning, $"{p.averageFrameMs:0.00} ms average, worst frame {p.worstFrameMs:0.0} ms");
            ctx.Report(C.Performance, "Frame spikes", p.spikes <= 8 ? TestStatus.Pass : TestStatus.Warning, $"{p.spikes} frame(s) over 50 ms outside scene loads");
            ctx.Report(C.Performance, "VFX / particles", TestStatus.Pass, $"peak {p.peakVfx} pooled effects, ~{p.peakParticles} particles, peak {p.peakEnemies} enemies");
            float growth = p.memoryEndMb - p.memoryStartMb;
            ctx.Report(C.Performance, "Managed memory", growth < 300f ? TestStatus.Pass : TestStatus.Warning, $"{p.memoryStartMb:0} → {p.memoryEndMb:0} MB (peak {p.memoryPeakMb:0} MB)");
            int problems = ctx.Logger.TotalRuntimeProblems;
            ctx.Report(C.Performance, "Console exceptions / errors", problems == 0 ? TestStatus.Pass : TestStatus.Fail,
                $"{ctx.Logger.ExceptionCount} exception(s), {ctx.Logger.ErrorCount} error(s), {ctx.Logger.AssertCount} assert(s)");
            ctx.Pass("see lines above");
            yield break;
        }
    }
}
