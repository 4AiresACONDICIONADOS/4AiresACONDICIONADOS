using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BreathOfEclipse.AI;
using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.UI;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// The Hollow Oni: encounter + boss bar, phase 1 attacks, phase 2 at ~49 % HP (transition, aura, speed, music,
    /// new attacks), Eclipse Cleave (telegraph → attack → damage), the player's ultimate and the defeat.
    /// God Mode is enabled for the player during this suite so the run cannot end early; hits and damage events
    /// still happen normally. The previous cheat state is restored at the end.
    /// </summary>
    public static class BossSuite
    {
        private const string BossKey = "boss";
        private const string AttacksKey = "bossAttacks";
        private const string GodKey = "bossPrevGod";

        public static IEnumerable<PlaytestStep> Steps()
        {
            yield return new PlaytestStep(C.Boss, "Hollow Oni encounter + boss bar", Encounter, 12f);
            yield return new PlaytestStep(C.Boss, "Phase 1 attacks", Phase1, 25f);
            yield return new PlaytestStep(C.Boss, "Phase 2 at 50 % HP", Phase2, 15f);
            yield return new PlaytestStep(C.Boss, "Eclipse Cleave (telegraph → attack → damage)", EclipseCleave, 30f);
            yield return new PlaytestStep(C.Boss, "Player Ultimate", Ultimate, 20f);
            yield return new PlaytestStep(C.Boss, "Boss defeated", Defeat, 12f);
        }

        private static EnemyController Boss(PlaytestContext ctx) => ctx.Shared.TryGetValue(BossKey, out var b) ? b as EnemyController : null;

        private static List<string> Attacks(PlaytestContext ctx)
        {
            if (!ctx.Shared.TryGetValue(AttacksKey, out var a))
            {
                a = new List<string>();
                ctx.Shared[AttacksKey] = a;
            }
            return (List<string>)a;
        }

        private static HUDController Hud => Object.FindAnyObjectByType<HUDController>();

        private static IEnumerator Encounter(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            ctx.ReleaseLockOn();
            foreach (var e in EncounterDirector.All.ToList())
                if (e != null && e.IsAlive) e.Damageable.Kill();
            ctx.Shared[GodKey] = DebugMenu.GodMode;
            DebugMenu.GodMode = true;
            ctx.Player.Stats.GodMode = true;
            ctx.RefillPlayer();
            yield return ctx.WaitGame(0.5f);

            var pc = ctx.Player;
            float mark = ctx.Telemetry.Mark();
            var boss = ctx.SpawnEnemy("hollow_oni", pc.transform.position + pc.transform.forward * 12f, pc.transform.position, true);
            if (boss == null)
            {
                ctx.Fail("could not spawn 'hollow_oni'");
                yield break;
            }
            ctx.Shared[BossKey] = boss;
            var attacks = Attacks(ctx);
            attacks.Clear();
            boss.StateChanged += (from, to) =>
            {
                if (to == EnemyStateId.Attack && boss.PendingAttack != null) attacks.Add(boss.PendingAttack.attackId);
            };
            var controller = boss.GetComponent<BossController>();
            controller.StartEncounter();
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => TelemetryRecorder.CountSince(ctx.Telemetry.BossEncounters, mark) > 0 && Hud != null && Hud.BossBarVisible, 3f, w);
            ctx.Screens.CaptureAuto("HollowOniPhase1");
            if (w.Success) ctx.Pass($"BossEncounter event, boss bar visible, music '{(AudioManager.Instance != null ? AudioManager.Instance.CurrentMusic : "?")}'");
            else ctx.Fail($"encounter event {TelemetryRecorder.CountSince(ctx.Telemetry.BossEncounters, mark)}, boss bar {(Hud != null && Hud.BossBarVisible)}");
            yield return ctx.Observe(2f);
        }

        private static IEnumerator Phase1(PlaytestContext ctx)
        {
            var boss = Boss(ctx);
            if (boss == null || !boss.IsAlive)
            {
                ctx.Result(TestStatus.NotTested, "no boss");
                yield break;
            }
            var pc = ctx.Player;
            float mark = ctx.Telemetry.Mark();
            float observe = ctx.Speed == PlaytestSpeed.Fast ? 7f : ctx.Speed == PlaytestSpeed.Visual ? 14f : 10f;
            var w = new WaitResult();
            // Stand close enough to be attacked (the boss must actually fight), keep facing it.
            float t = 0f;
            while (t < observe && !ctx.ShouldStop)
            {
                float d = PlaytestContext.Flat(pc.transform.position, boss.transform.position);
                if (d > 5f) ctx.Driver.MoveWorld(boss.transform.position - pc.transform.position);
                else ctx.Driver.Stop();
                if (pc.Damageable.Health.Normalized < 0.4f) ctx.RefillPlayer(false);
                t += Time.deltaTime;
                yield return null;
            }
            ctx.Driver.Stop();
            var hitsOnPlayer = ctx.Telemetry.DamageSince(mark, d => d.Result.Target == pc.gameObject && d.Hit.Attacker == boss.gameObject);
            var attacks = Attacks(ctx);
            bool stillPhase1 = boss.Phase == 1;
            ctx.Check("Stays in phase 1 above 50 %", stillPhase1, $"HP {boss.Damageable.Health.Normalized * 100f:0} %");
            ctx.Check("Boss damages the player", hitsOnPlayer.Count > 0, $"{hitsOnPlayer.Count} hit(s)");
            if (attacks.Count > 0) ctx.Pass($"attacks: {string.Join(", ", attacks)}");
            else ctx.Fail($"no attack in {observe:0} s");
        }

        private static IEnumerator Phase2(PlaytestContext ctx)
        {
            var boss = Boss(ctx);
            if (boss == null || !boss.IsAlive)
            {
                ctx.Result(TestStatus.NotTested, "no boss");
                yield break;
            }
            var hp = boss.Damageable.Health;
            float mark = ctx.Telemetry.Mark();
            float speedBefore = boss.SpeedMultiplier;
            // Test setup: set HP to ~49 % through the health model (raises the same Damaged event gameplay uses).
            if (hp.Normalized > 0.49f) hp.ApplyDamage(hp.Current - hp.Max * 0.49f);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => boss.Phase >= 2 && TelemetryRecorder.CountSince(ctx.Telemetry.BossPhases, mark) > 0, 2f, w);
            bool phase2 = w.Success;
            yield return ctx.WaitUntil(() => !boss.Damageable.Invulnerable, 6f, w);
            bool transitionEnded = w.Success;
            yield return ctx.WaitReal(0.3f);
            var audio = AudioManager.Instance;
            yield return ctx.WaitUntil(() => audio != null && audio.CurrentMusic == boss.Data.phase2Music, 4f, w);
            bool music = w.Success;
            bool aura = ctx.Telemetry.VfxIdsSince(mark).Contains("boss_aura");
            ctx.Screens.CaptureAuto("HollowOniPhase2");

            ctx.Check("Transition (roar, invulnerable, then vulnerable)", transitionEnded, transitionEnded ? "transition finished" : "still invulnerable after 6 s");
            ctx.Check("Aggression / speed up", boss.SpeedMultiplier > speedBefore, $"speed x{speedBefore:0.00} → x{boss.SpeedMultiplier:0.00}, aggression +{boss.AggressionBonus:0.00}");
            ctx.Check("New VFX (dark aura)", aura, aura ? "boss_aura spawned" : "no aura effect");
            ctx.Check("Music change", music, $"music '{(audio != null ? audio.CurrentMusic : "?")}'");
            ctx.Check("New attack set", boss.CurrentAttacks == boss.Data.phase2Attacks, string.Join(", ", boss.CurrentAttacks.Select(a => a.attackId)));
            ctx.Check("Not killed by the test", boss.IsAlive, $"HP {hp.Normalized * 100f:0} %");
            if (phase2) ctx.Pass($"phase 2 at {hp.Normalized * 100f:0} % HP");
            else ctx.Fail("phase 2 did not start");
            yield return ctx.Observe(2.5f);
        }

        private static IEnumerator EclipseCleave(PlaytestContext ctx)
        {
            var boss = Boss(ctx);
            if (boss == null || !boss.IsAlive)
            {
                ctx.Result(TestStatus.NotTested, "no boss");
                yield break;
            }
            var pc = ctx.Player;
            ctx.RefillPlayer(false);
            var w = new WaitResult();
            // Eclipse Cleave is a gap-closer (3–16 m): back off to ~8 m and wait for it.
            Vector3 away = pc.transform.position - boss.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -boss.transform.forward;
            yield return ctx.Driver.MoveTo(ctx, boss.transform.position + away * 8f, 1f, 5f, false, w);
            boss.ForcedNextAttackId = "eclipse_cleave";
            float mark = ctx.Telemetry.Mark();
            var attacks = Attacks(ctx);
            int before = attacks.Count;
            yield return ctx.WaitUntil(() => attacks.Skip(before).Contains("eclipse_cleave"), 20f, w);
            bool started = w.Success;
            yield return ctx.WaitUntil(() => TelemetryRecorder.CountSince(ctx.Telemetry.Telegraphs, mark) > 0, 3f, w);
            bool telegraph = w.Success;
            yield return ctx.WaitUntil(() => ctx.Telemetry.DamageSince(mark, d => d.Hit.SourceId == "eclipse_cleave").Count > 0, 5f, w);
            bool damage = w.Success;
            boss.ForcedNextAttackId = null;
            ctx.Check("Telegraph", telegraph, telegraph ? $"ground warning radius {TelemetryRecorder.MaxSince(ctx.Telemetry.Telegraphs, mark):0.0} m" : "no telegraph");
            ctx.Check("Attack executed", started, started ? "eclipse_cleave chosen and executed" : "boss never used it in 20 s");
            ctx.Check("Damage event", damage, damage ? "the cleave hit the player" : "no damage event from the cleave", true);
            if (started && telegraph) ctx.Pass("Eclipse Cleave telegraphed and executed");
            else ctx.Fail($"started {started}, telegraph {telegraph}");
            yield return ctx.Observe(2f);
        }

        private static IEnumerator Ultimate(PlaytestContext ctx)
        {
            var boss = Boss(ctx);
            if (boss == null || !boss.IsAlive)
            {
                ctx.Result(TestStatus.NotTested, "no boss");
                yield break;
            }
            var pc = ctx.Player;
            yield return ctx.Settle();
            pc.Breathing.EquipById("tidal");
            ctx.RefillPlayer();
            var w = new WaitResult();
            yield return ctx.Driver.MoveTo(ctx, boss.transform.position, 5f, 5f, false, w);
            yield return ctx.LockOnto(boss, w);
            float mark = ctx.Telemetry.Mark();
            var ult = pc.Breathing.GetSkill(4);
            ctx.Driver.Press(InputCommand.Ultimate);
            yield return ctx.WaitUntil(() => TelemetryRecorder.CountSince(ctx.Telemetry.Ultimates, mark) > 0, 1.5f, w);
            bool started = w.Success;
            bool shot = false;
            float start = Time.realtimeSinceStartup;
            if (started)
            {
                yield return ctx.WaitUntil(() =>
                {
                    if (!shot && Time.realtimeSinceStartup - start > 2f)
                    {
                        shot = true;
                        ctx.Screens.CaptureAuto("Ultimate");
                    }
                    return !pc.Breathing.IsExecuting;
                }, 14f, w);
            }
            float duration = Time.realtimeSinceStartup - start;
            var hits = ctx.Telemetry.DamageSince(mark, d => ult != null && d.Hit.SourceId == ult.skillId);
            bool cinematic = ctx.Telemetry.CameraCueKindsSince(mark).Contains("cinematic");
            ctx.Check("Cinematic camera", cinematic || SaveSystem.Settings.skipUltimateCinematics,
                SaveSystem.Settings.skipUltimateCinematics ? "skipped by settings" : cinematic ? "CinematicCombatCamera shots played" : "no cinematic shot");
            ctx.Check("Duration", duration > 3f && duration < 12f, $"{duration:0.0} s (design 5–8 s at normal speed)", true);
            ctx.Check("Damages the boss", hits.Count > 0, $"{hits.Count} hit(s), {hits.Sum(h => h.Result.Damage)} damage");
            if (started) ctx.Pass($"{(ult != null ? ult.displayName : "ultimate")} activated with R");
            else ctx.Fail("ultimate did not start (BREATH full, grounded, locked on)");
            yield return ctx.Observe(2f);
        }

        private static IEnumerator Defeat(PlaytestContext ctx)
        {
            var boss = Boss(ctx);
            float mark = ctx.Telemetry.Mark();
            if (boss != null && boss.IsAlive) boss.Damageable.Kill(ctx.Player.gameObject);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => TelemetryRecorder.CountSince(ctx.Telemetry.BossDefeats, 0f) > 0, 3f, w);
            bool defeated = w.Success;
            yield return ctx.WaitUntil(() => Hud == null || !Hud.BossBarVisible, 6f, w);
            ctx.Check("Boss bar hidden", w.Success, "HUD boss bar closed after the defeat");
            ctx.Check("Victory music", AudioManager.Instance != null && AudioManager.Instance.CurrentMusic == "victory", $"music '{(AudioManager.Instance != null ? AudioManager.Instance.CurrentMusic : "?")}'", true);
            if (defeated) ctx.Pass("BossDefeated event (finale slow motion, flash frame)");
            else ctx.Fail("no BossDefeated event");

            // Restore the player's cheat state.
            bool prevGod = ctx.Shared.TryGetValue(GodKey, out var g) && (bool)g;
            DebugMenu.GodMode = prevGod;
            if (ctx.Player != null) ctx.Player.Stats.GodMode = prevGod;
            yield return ctx.Observe(3f);
        }
    }
}
