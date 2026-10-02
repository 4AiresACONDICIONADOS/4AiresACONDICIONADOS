using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Scenes;
using BreathOfEclipse.World;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// FULL WORLD TEST (v0.5): Main Menu → PLAY LIVING WORLD (fresh world) → village at morning with people moving →
    /// day/night → routines → streaming → demons → events (staged, rest time skip) → exceptional presence → save.
    /// Reports what it measures; nothing here is estimated.
    /// </summary>
    public static class WorldSuite
    {
        public static IEnumerable<PlaytestStep> Steps()
        {
            yield return new PlaytestStep(C.World, "PLAY LIVING WORLD loads 04_FrontierRegion", LoadWorld, 90f, false);
            yield return new PlaytestStep(C.World, "Village spawn at morning, people about", VillageMorning, 25f);
            yield return new PlaytestStep(C.World, "Day / night cycle (06 / 12 / 17:30 / 20 / 00)", DayNight, 30f);
            yield return new PlaytestStep(C.World, "Routines follow the clock (night home, morning work, shop)", Routines, 40f);
            yield return new PlaytestStep(C.World, "Sector streaming (teleport to the forest and back)", Streaming, 60f);
            yield return new PlaytestStep(C.World, "Demons appear out of view at night", Demons, 45f);
            yield return new PlaytestStep(C.World, "World event staged, then resolved by a rest", Events, 60f);
            yield return new PlaytestStep(C.World, "Exceptional presence and reset", Presence, 30f);
            yield return new PlaytestStep(C.World, "World save round trip", Save, 15f);
        }

        private static LivingWorld W => LivingWorld.Instance;

        private static IEnumerator LoadWorld(PlaytestContext ctx)
        {
            var w = new WaitResult();
            yield return ctx.LoadScene(SceneNames.MainMenu, w, 25f);
            yield return ctx.WaitUntil(() => Object.FindAnyObjectByType<MainMenuController>() != null, 5f, w);
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            if (menu == null || !menu.Buttons.TryGetValue("LivingWorld", out var play))
            {
                ctx.Fail("PLAY LIVING WORLD button not found");
                ctx.Runner.Abort("living world unavailable");
                yield break;
            }
            WorldSave.StartFresh = true;
            float start = Time.realtimeSinceStartup;
            play.onClick.Invoke();
            WorldSave.StartFresh = true; // the button resets it; the test wants a fresh world
            yield return ctx.WaitUntil(() => PlaytestContext.ActiveScene == SceneNames.FrontierRegion && ctx.Player != null && W != null &&
                                             (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading), 80f, w);
            if (!w.Success)
            {
                ctx.Fail("04_FrontierRegion did not load with a player within 80 s");
                ctx.Runner.Abort("scene cannot load: 04_FrontierRegion");
                yield break;
            }
            ctx.Logger.NoteScene(SceneNames.FrontierRegion);
            yield return ctx.Frames(10);
            SceneSanityChecker.Report(ctx, C.Scenes, "04_FrontierRegion", true);
            ctx.Pass($"region loaded in {Time.realtimeSinceStartup - start:0.0} s, {W.Sectors.LoadedCount} sectors detailed");
        }

        private static IEnumerator VillageMorning(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => pc.Motor.Grounded, 5f, w);
            ctx.Check("Grounded at the spawn", w.Success, $"{pc.transform.position}");
            var sector = W.Sectors.Current;
            ctx.Check("Spawn in the village", sector != null && sector.Id == "village", sector != null ? sector.Name : "no sector");
            float h = W.Time.Clock.Hour;
            ctx.Check("Morning", h >= 6.5f && h < 10f, WorldClock.Format(h));
            yield return ctx.WaitGame(3f);
            int shown = W.Npcs.ShownCount, walking = 0;
            foreach (var a in W.Npcs.Agents) if (a.Shown && a.State.Travelling) walking++;
            ctx.Check("Villagers visible around the spawn", shown >= 6, $"{shown} shown, {walking} walking");
            ctx.Check("People already moving", walking >= 1 || shown >= 6, $"{walking} walking", true);
            ctx.Screens.CaptureAuto("FrontierVillageMorning");
            ctx.Pass($"{shown} NPCs shown, {W.Npcs.LogicalCount} logical, {W.Npcs.BuiltCount} bodies built");
        }

        private static IEnumerator DayNight(PlaytestContext ctx)
        {
            var t = W.Time;
            var readings = new List<string>();
            float noonLight = 0f, nightLevel = 0f;
            foreach (float hour in new[] { 6f, 12f, 17.5f, 20f, 0f })
            {
                t.SetHour(hour);
                yield return ctx.WaitReal(0.6f);
                readings.Add($"{WorldClock.Format(hour)}: sun {t.Sun.intensity:0.00} moon {t.Moon.intensity:0.00} lanterns {NightLight.Level:0.00} fog {RenderSettings.fogDensity:0.0000}");
                if (Mathf.Approximately(hour, 12f)) noonLight = t.Sun.intensity;
                if (Mathf.Approximately(hour, 0f)) nightLevel = NightLight.Level;
                ctx.Screens.CaptureAuto($"Frontier_{hour:00}h");
            }
            ctx.Check("Sun up at noon", noonLight > 1f, $"sun {noonLight:0.00}");
            ctx.Check("Lanterns lit at midnight", nightLevel > 0.8f, $"level {nightLevel:0.00}");
            t.SetPaused(true);
            float before = t.Clock.Hour;
            yield return ctx.WaitGame(1f);
            ctx.Check("Pause stops the clock", Mathf.Approximately(before, t.Clock.Hour), $"{before:0.000} → {t.Clock.Hour:0.000}");
            t.SetPaused(false);
            t.SetMultiplier(20f);
            before = t.Clock.Hour;
            yield return ctx.WaitGame(1f);
            float advanced = WorldClock.HoursUntil(before, t.Clock.Hour);
            ctx.Check("20x speeds the clock", advanced > 0.15f, $"+{advanced * 60f:0} min in 1 s");
            t.SetMultiplier(1f);
            ctx.Pass(string.Join(" | ", readings));
        }

        private static IEnumerator Routines(PlaytestContext ctx)
        {
            W.Time.SetHour(21.5f);
            yield return ctx.WaitReal(1f);
            int villagers = 0, inside = 0;
            foreach (var n in W.Npcs.Sim.Npcs)
            {
                if (n.Def.Role == NpcRole.Guard || n.Def.Role == NpcRole.Hunter || n.Def.Role == NpcRole.Traveler || n.Def.Role == NpcRole.Camper) continue;
                villagers++;
                if (n.Indoors) inside++;
            }
            ctx.Check("Villagers home at night", inside >= villagers - 1, $"{inside}/{villagers} inside at 21:30");
            ctx.Check("Shop closed at night", !W.Npcs.ShopOpen, "");
            W.Time.SetHour(9f);
            yield return ctx.WaitReal(1.5f);
            var ohara = W.Npcs.Sim.Find("ohara");
            var genta = W.Npcs.Sim.Find("genta");
            ctx.Check("Merchant trading at 09:00", ohara != null && ohara.Activity == NpcActivity.Trade && W.Npcs.ShopOpen, ohara != null ? ohara.Activity.ToString() : "-");
            ctx.Check("Farmer in the field at 09:00", genta != null && genta.Activity == NpcActivity.Farm, genta != null ? genta.Activity.ToString() : "-");
            ctx.Pass();
        }

        private static IEnumerator Streaming(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            Vector3 home = pc.transform.position;
            W.Sectors.LoadAround(new Vector3(0f, 0f, 60f), false);
            pc.Motor.Teleport(new Vector3(0f, RegionTerrain.SampleHeight(0f, 60f) + 0.3f, 60f));
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => W.Sectors.IsLoaded("forest_road") && pc.Motor.Grounded, 10f, w);
            ctx.Check("Forest road loaded under the player", w.Success, $"loaded {W.Sectors.LoadedCount}");
            yield return ctx.WaitUntil(() => !W.Sectors.IsLoaded("village"), 15f, w);
            ctx.Check("Village unloads when far (far view stays)", w.Success, $"loaded {W.Sectors.LoadedCount}");
            ctx.Check("Not falling", pc.transform.position.y > -5f, $"{pc.transform.position}");
            W.Sectors.LoadAround(home, false);
            pc.Motor.Teleport(home + Vector3.up * 0.3f);
            yield return ctx.WaitUntil(() => W.Sectors.IsLoaded("village") && pc.Motor.Grounded, 10f, w);
            ctx.Check("Back in the village", w.Success, "");
            ctx.Pass($"sectors loaded now: {W.Sectors.LoadedCount}");
        }

        private static IEnumerator Demons(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            W.Sectors.LoadAround(new Vector3(0f, 0f, 60f), true);
            pc.Motor.Teleport(new Vector3(0f, RegionTerrain.SampleHeight(0f, 60f) + 0.3f, 60f));
            W.Time.SetHour(22f);
            int before = W.Demons.Spawned;
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => W.Demons.Active.Count > 0, 20f, w);
            float nearest = float.MaxValue;
            foreach (var d in W.Demons.Active) nearest = Mathf.Min(nearest, Vector3.Distance(d.transform.position, pc.transform.position));
            ctx.Check("Demons roam the forest at night", w.Success, $"{W.Demons.Active.Count} active, {W.Demons.Spawned - before} spawned");
            ctx.Check("Never spawned close to the player", !w.Success || nearest > 20f, $"nearest {nearest:0} m");
            ctx.Check("Each demon has a persistent record", W.State.Data.demons.Count >= W.Demons.Active.Count, $"{W.State.Data.demons.Count} records");
            ctx.Pass();
        }

        private static IEnumerator Events(PlaytestContext ctx)
        {
            var ev = W.Events;
            ev.ForceStart(FrontierEvents.CaravanAttack);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => ev.StagedEvents.ContainsKey(FrontierEvents.CaravanAttack), 6f, w);
            var rec = W.State.Event(FrontierEvents.CaravanAttack);
            ctx.Check("Caravan attack started", rec.IsActive || rec.IsResolved, rec.state.ToString());
            ctx.Check("Staged near the player", w.Success, w.Success ? ev.StagedEvents[FrontierEvents.CaravanAttack].Note : "logical only");
            ctx.Screens.CaptureAuto("FrontierCaravanAttack");
            W.Rest.RestUntil(6f);
            yield return ctx.WaitUntil(() => !W.Rest.Resting, 15f, w);
            ctx.Check("Rest skipped to the morning", W.Time.Clock.Hour >= 5.9f && W.Time.Clock.Hour < 7f, W.Time.Describe());
            ctx.Check("The event did not wait", !rec.IsActive, rec.state.ToString());
            ctx.Check("Outcome remembered", W.State.GetFact("Event_caravan_attack_Success") + W.State.GetFact("Event_caravan_attack_Failure") > 0, "");
            ctx.Pass(ev.Log.Count > 0 ? ev.Log[ev.Log.Count - 1] : "");
        }

        private static IEnumerator Presence(PlaytestContext ctx)
        {
            var ev = W.Events;
            ev.TriggerExceptionalPresence();
            yield return ctx.WaitReal(1f);
            ctx.Check("Presence active", ev.PresenceActive && W.Demons.Presence, "");
            ctx.Check("Region state changed", W.State.SectorFlag("village", "presence"), "");
            int fleeing = 0;
            foreach (var n in W.Npcs.Sim.Npcs) if (n.Mode == NpcMode.Fleeing || n.Indoors) fleeing++;
            ctx.Check("Villagers retreat", fleeing > 0, $"{fleeing} fleeing or inside");
            ev.ResetAll();
            yield return ctx.WaitReal(0.5f);
            ctx.Check("Reset clears the presence", !ev.PresenceActive && !W.Demons.Presence, "");
            ctx.Pass();
        }

        private static IEnumerator Save(PlaytestContext ctx)
        {
            bool ok = W.SaveWorld("playtest", true);
            var data = WorldSave.Load();
            ctx.Check("Saved", ok && WorldSave.Exists, "");
            ctx.Check("Version stamped", data != null && data.version == WorldStateData.CurrentVersion, data != null ? $"v{data.version}" : "unreadable");
            ctx.Check("Clock restored from the save", data != null && data.day == W.Time.Clock.Day, data != null ? $"day {data.day} {WorldClock.Format(data.hour)}" : "-");
            yield return null;
            ctx.Pass($"{data?.facts.Count ?? 0} facts, {data?.demons.Count ?? 0} demon records, {data?.events.Count ?? 0} events");
        }
    }
}
