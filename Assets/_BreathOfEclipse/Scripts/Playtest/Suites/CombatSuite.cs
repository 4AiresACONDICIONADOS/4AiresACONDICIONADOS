using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BreathOfEclipse.AI;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Attacks against the training dummy: damage, the required combo strings, dodge, block, simulated parry and
    /// perfect dodge (timed synthetic hits through the real damage pipeline), hit-detection integrity and feedback.
    /// </summary>
    public static class CombatSuite
    {
        private const string DummyKey = "dummy";
        private const string CombatStartKey = "combatStart";

        public static IEnumerable<PlaytestStep> Steps()
        {
            yield return new PlaytestStep(C.Combat, "Approach dummy + Lock-On", ApproachAndLock, 15f);
            yield return new PlaytestStep(C.Combat, "Light attack damages dummy", LightAttack, 10f);
            yield return new PlaytestStep(C.Combat, "Heavy attack", HeavyAttack, 10f);
            yield return new PlaytestStep(C.Combat, "Combo Light-Light-Light-Light", c => Combo(c, "LLLL", "L1>L2>L3>L4"), 12f);
            yield return new PlaytestStep(C.Combat, "Combo Light-Light-Heavy", c => Combo(c, "LLH", "L1>L2>L2H"), 12f);
            yield return new PlaytestStep(C.Combat, "Combo Light-Heavy-Heavy", c => Combo(c, "LHH", "L1>L1H>L1HH"), 12f);
            yield return new PlaytestStep(C.Combat, "Combo Heavy-Heavy", c => Combo(c, "HH", "H1>H2"), 12f);
            yield return new PlaytestStep(C.Combat, "Dash + Light", DashLight, 15f);
            yield return new PlaytestStep(C.Combat, "Jump + Light", c => AirAttack(c, InputCommand.LightAttack, "A1"), 10f);
            yield return new PlaytestStep(C.Combat, "Jump + Heavy", c => AirAttack(c, InputCommand.HeavyAttack, "AH"), 10f);
            yield return new PlaytestStep(C.Combat, "Dodge", Dodge, 10f);
            yield return new PlaytestStep(C.Combat, "Block", Block, 10f);
            yield return new PlaytestStep(C.Combat, "Parry simulation + Heavy (Riposte)", Parry, 12f);
            yield return new PlaytestStep(C.Combat, "Perfect Dodge simulation + Light", PerfectDodge, 12f);
            yield return new PlaytestStep(C.Combat, "Hit detection: one hit per AttackID", HitDetection, 5f);
            yield return new PlaytestStep(C.Combat, "Combat feedback", Feedback, 5f);
        }

        private static TrainingDummy Dummy(PlaytestContext ctx)
        {
            if (ctx.Shared.TryGetValue(DummyKey, out var d) && d is TrainingDummy dummy && dummy != null) return dummy;
            var created = ctx.EnsureDummy();
            ctx.Shared[DummyKey] = created;
            return created;
        }

        private static IEnumerator ReturnToDummy(PlaytestContext ctx, float distance = 2.6f)
        {
            var dummy = Dummy(ctx);
            var w = new WaitResult();
            if (PlaytestContext.Flat(ctx.Player.transform.position, dummy.transform.position) > distance + 0.8f)
                yield return ctx.Driver.MoveTo(ctx, dummy.transform.position, distance, 6f, false, w);
            if (!ctx.IsLockedOn(dummy)) yield return ctx.LockOnto(dummy, w);
        }

        /// <summary>Waits until the combo chain has reset (attack finished + 0.4 s), so a new string starts from its entry.</summary>
        private static IEnumerator Idle(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            yield return ctx.WaitGame(0.45f);
        }

        private static IEnumerator ApproachAndLock(PlaytestContext ctx)
        {
            ctx.Shared[CombatStartKey] = ctx.Telemetry.Mark();
            ctx.Sampler.Reset();
            ctx.Player.Stats.Breath.SetValue(0f); // test setup: lets the BREATH gain be measured
            var dummy = Dummy(ctx);
            var w = new WaitResult();
            yield return ctx.Driver.MoveTo(ctx, dummy.transform.position, 2.6f, 10f, false, w);
            ctx.Check("Reach the dummy", w.Success, $"distance {PlaytestContext.Flat(ctx.Player.transform.position, dummy.transform.position):0.0} m");
            yield return ctx.LockOnto(dummy, w);
            if (w.Success) ctx.Pass("Tab / Middle Mouse lock-on acquired the training dummy");
            else ctx.Fail("lock-on did not acquire the dummy");
            yield return ctx.Observe(1f);
        }

        private static IEnumerator LightAttack(PlaytestContext ctx)
        {
            yield return Idle(ctx);
            yield return ReturnToDummy(ctx);
            var dummy = Dummy(ctx);
            var dmg = PlaytestContext.DamageableOf(dummy);
            float before = dmg.Health.Current;
            float mark = ctx.Telemetry.Mark();
            var watcher = new AttackWatcher();
            watcher.Reset();
            ctx.Driver.Press(InputCommand.LightAttack);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return ctx.Telemetry.DamageSince(mark, d => d.Result.Target == dummy.gameObject).Count > 0;
            }, 1.5f, w);
            float after = dmg.Health.Current;
            if (w.Success) ctx.Screens.CaptureAuto("LightAttack");
            var hits = ctx.Telemetry.DamageSince(mark, d => d.Result.Target == dummy.gameObject);
            if (after < before) ctx.Pass($"HP {before:0} → {after:0} ({watcher.Sequence}, AttackID {(hits.Count > 0 ? hits[0].Hit.AttackInstanceId : 0)})");
            else ctx.Fail($"HP {before:0} → {after:0}: no damage ({watcher.Sequence})");
            ctx.Check("First attack of the string is L1", watcher.Started.Count > 0 && watcher.Started[0] == "L1", watcher.Sequence);
            yield return ctx.Observe(1f);
        }

        private static IEnumerator HeavyAttack(PlaytestContext ctx)
        {
            yield return Idle(ctx);
            yield return ReturnToDummy(ctx);
            var dummy = Dummy(ctx);
            float mark = ctx.Telemetry.Mark();
            var watcher = new AttackWatcher();
            watcher.Reset();
            ctx.Driver.Press(InputCommand.HeavyAttack);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return ctx.Telemetry.DamageSince(mark, d => d.Result.Target == dummy.gameObject).Count > 0;
            }, 2f, w);
            bool isH1 = watcher.Started.Count > 0 && watcher.Started[0] == "H1";
            if (w.Success && isH1) ctx.Pass($"{watcher.Sequence} hit the dummy");
            else ctx.Fail($"attack {watcher.Sequence}, damage {w.Success}");
            yield return ctx.Observe(1f);
        }

        private static IEnumerator Combo(PlaytestContext ctx, string buttons, string expected)
        {
            yield return Idle(ctx);
            yield return ReturnToDummy(ctx);
            var pc = ctx.Player;
            var dummy = Dummy(ctx);
            float mark = ctx.Telemetry.Mark();
            var watcher = new AttackWatcher();
            watcher.Reset();
            var w = new WaitResult();

            for (int i = 0; i < buttons.Length && !ctx.ShouldStop; i++)
            {
                int startedBefore = watcher.Started.Count;
                ctx.Driver.Press(buttons[i] == 'L' ? InputCommand.LightAttack : InputCommand.HeavyAttack);
                // Wait for this press to start an attack...
                yield return ctx.WaitUntil(() =>
                {
                    watcher.Poll();
                    return watcher.Started.Count > startedBefore;
                }, 1.2f, w);
                if (!w.Success) break;
                // ...then for the moment the next press is accepted (cancel window) or the attack ends.
                if (i < buttons.Length - 1)
                {
                    yield return ctx.WaitUntil(() =>
                    {
                        watcher.Poll();
                        return !pc.Combat.IsAttacking || (pc.Combat.CanCancel(CancelFlags.Attack) && pc.Combat.Normalized > 0.35f);
                    }, 2f, w);
                }
            }
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return !pc.Combat.IsAttacking;
            }, 3f, w);

            int hits = ctx.Telemetry.DamageSince(mark, d => d.Result.Target == dummy.gameObject && d.Hit.AttackerTeam == Team.Player).Count;
            string got = watcher.Sequence;
            if (got == expected) ctx.Pass($"{buttons}: {got}, {hits} hit(s) on the dummy");
            else ctx.Fail($"{buttons}: expected {expected}, got {got}");
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator DashLight(PlaytestContext ctx)
        {
            yield return Idle(ctx);
            var pc = ctx.Player;
            var dummy = Dummy(ctx);
            ctx.ReleaseLockOn();
            var w = new WaitResult();
            // Back off to get a running start, then sprint at the dummy.
            Vector3 away = pc.transform.position - dummy.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -dummy.transform.forward;
            yield return ctx.Driver.MoveTo(ctx, dummy.transform.position + away * 10f, 1f, 6f, false, w);
            var watcher = new AttackWatcher();
            watcher.Reset();
            ctx.Driver.Sim.Sprint = true;
            yield return ctx.WaitUntil(() =>
            {
                ctx.Driver.MoveWorld(dummy.transform.position - pc.transform.position);
                ctx.Driver.Sim.Sprint = true;
                return pc.IsSprinting && pc.Motor.PlanarVelocity.magnitude > 6.6f &&
                       PlaytestContext.Flat(pc.transform.position, dummy.transform.position) < 5.5f;
            }, 4f, w);
            bool fastEnough = w.Success;
            ctx.Driver.Press(InputCommand.LightAttack);
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return watcher.Started.Count > 0;
            }, 1f, w);
            ctx.Driver.Stop();
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return !pc.Combat.IsAttacking;
            }, 2f, w);
            if (watcher.Last == "DashL") ctx.Pass("sprint + Light → DashL (Swift Lunge)");
            else ctx.Fail($"expected DashL, got {watcher.Sequence} (sprint speed reached: {fastEnough})");
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator AirAttack(PlaytestContext ctx, InputCommand button, string expected)
        {
            yield return Idle(ctx);
            yield return ReturnToDummy(ctx);
            var pc = ctx.Player;
            var watcher = new AttackWatcher();
            watcher.Reset();
            ctx.Driver.Press(InputCommand.Jump);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => !pc.Motor.Grounded, 1f, w);
            yield return ctx.WaitGame(0.2f);
            ctx.Driver.Press(button);
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return watcher.Started.Count > 0;
            }, 1f, w);
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return !pc.Combat.IsAttacking && pc.Motor.Grounded;
            }, 4f, w);
            if (watcher.Started.Count > 0 && watcher.Started[0] == expected) ctx.Pass($"airborne {button} → {expected}");
            else ctx.Fail($"expected {expected}, got {watcher.Sequence}");
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator Dodge(PlaytestContext ctx)
        {
            yield return Idle(ctx);
            var pc = ctx.Player;
            ctx.RefillPlayer(false);
            Vector3 start = pc.transform.position;
            float stamina = pc.Stats.Stamina.Current;
            ctx.Driver.MoveWorld(pc.transform.right);
            ctx.Driver.Press(InputCommand.Dodge);
            var w = new WaitResult();
            bool iFrames = false;
            yield return ctx.WaitUntil(() =>
            {
                iFrames |= pc.Defense.IsDodging;
                return pc.State == PlayerState.Dodge;
            }, 0.5f, w);
            bool entered = w.Success;
            yield return ctx.WaitUntil(() =>
            {
                iFrames |= pc.Defense.IsDodging;
                return pc.State != PlayerState.Dodge;
            }, 1.5f, w);
            ctx.Driver.Stop();
            float d = PlaytestContext.Flat(start, pc.transform.position);
            if (entered && d > 2f) ctx.Pass($"dodged {d:0.0} m, stamina {stamina:0} → {pc.Stats.Stamina.Current:0}");
            else ctx.Fail($"dodge state {entered}, distance {d:0.0} m");
            ctx.Check("Invulnerability frames", iFrames, "PlayerDefense.IsDodging during the dodge");
            yield return ctx.Observe(1f);
        }

        private static IEnumerator Block(PlaytestContext ctx)
        {
            yield return Idle(ctx);
            yield return ReturnToDummy(ctx);
            var pc = ctx.Player;
            ctx.Driver.Sim.Block = true;
            ctx.Driver.Press(InputCommand.Block);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => pc.State == PlayerState.Block && pc.Defense.IsBlocking, 0.6f, w);
            bool blocking = w.Success;
            yield return ctx.WaitGame(0.6f);
            bool held = pc.State == PlayerState.Block;
            ctx.Driver.Sim.Block = false;
            yield return ctx.WaitUntil(() => pc.State == PlayerState.Locomotion, 1f, w);
            if (blocking && held && w.Success) ctx.Pass("Q held → Block state, released → locomotion");
            else ctx.Fail($"entered {blocking}, held {held}, released {w.Success}");
            yield return ctx.Observe(1f);
        }

        private static IEnumerator Parry(PlaytestContext ctx)
        {
            yield return Idle(ctx);
            yield return ReturnToDummy(ctx);
            var pc = ctx.Player;
            var dummy = Dummy(ctx);
            ctx.RefillPlayer(false);
            float mark = ctx.Telemetry.Mark();
            ctx.Driver.Sim.Block = true;
            ctx.Driver.Press(InputCommand.Block);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => pc.State == PlayerState.Block, 0.3f, w);
            // Enemy hit arrives 2 frames after the block press (inside the 0.17 s parry window).
            yield return ctx.Frames(2);
            var result = SyntheticHits.HitPlayerFrom(dummy.gameObject);
            bool parried = result.Outcome == HitOutcome.Parried && TelemetryRecorder.CountSince(ctx.Telemetry.Parries, mark) > 0;
            ctx.Check("Parry", parried, $"outcome {result.Outcome}, Parry event {TelemetryRecorder.CountSince(ctx.Telemetry.Parries, mark)}");

            var watcher = new AttackWatcher();
            watcher.Reset();
            yield return ctx.WaitGame(0.1f);
            ctx.Driver.Press(InputCommand.HeavyAttack);
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return watcher.Started.Count > 0;
            }, 1f, w);
            ctx.Driver.Sim.Block = false;
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return !pc.Combat.IsAttacking;
            }, 2f, w);
            if (parried && watcher.Started.Contains("Riposte")) ctx.Pass("parry → Heavy → Riposte Break");
            else ctx.Fail($"parried {parried}, counter {watcher.Sequence} (expected Riposte)");
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator PerfectDodge(PlaytestContext ctx)
        {
            yield return Idle(ctx);
            yield return ReturnToDummy(ctx, 3f);
            var pc = ctx.Player;
            var dummy = Dummy(ctx);
            ctx.RefillPlayer(false);
            float mark = ctx.Telemetry.Mark();
            ctx.Driver.MoveWorld(pc.transform.right);
            ctx.Driver.Press(InputCommand.Dodge);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => pc.State == PlayerState.Dodge, 0.4f, w);
            ctx.Driver.Stop();
            yield return ctx.Frames(1);
            var result = SyntheticHits.HitPlayerFrom(dummy.gameObject);
            bool perfect = result.Outcome == HitOutcome.PerfectEvaded && TelemetryRecorder.CountSince(ctx.Telemetry.PerfectDodges, mark) > 0;
            float minScale = 1f;
            yield return ctx.WaitUntil(() =>
            {
                if (TimeController.Instance != null) minScale = Mathf.Min(minScale, TimeController.Instance.CurrentScale);
                return minScale < 0.5f;
            }, 0.4f, w);
            ctx.Check("Perfect dodge", perfect, $"outcome {result.Outcome}");
            ctx.Check("Slow motion (x0.25)", minScale < 0.5f, $"lowest time scale {minScale:0.00}");

            var watcher = new AttackWatcher();
            watcher.Reset();
            ctx.Driver.Press(InputCommand.LightAttack);
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return watcher.Started.Count > 0;
            }, 1.2f, w);
            yield return ctx.WaitUntil(() =>
            {
                watcher.Poll();
                return !pc.Combat.IsAttacking;
            }, 2f, w);
            if (perfect && watcher.Started.Contains("PDCounter")) ctx.Pass("perfect dodge → Light → Phantom Counter");
            else ctx.Fail($"perfect {perfect}, counter {watcher.Sequence} (expected PDCounter)");
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator HitDetection(PlaytestContext ctx)
        {
            float since = ctx.Shared.TryGetValue(CombatStartKey, out var s) ? (float)s : 0f;
            var combos = ctx.Db != null ? ctx.Db.playerCombos : null;
            var groups = new Dictionary<(int, int), (string attack, int count)>();
            foreach (var d in ctx.Telemetry.DamageSince(since, r => r.Hit.AttackerTeam == Team.Player && r.Result.Target != null && combos != null && combos.Find(r.Hit.SourceId) != null))
            {
                var key = (d.Hit.AttackInstanceId, d.Result.Target.GetInstanceID());
                groups.TryGetValue(key, out var g);
                groups[key] = (d.Hit.SourceId, g.count + 1);
            }
            if (groups.Count == 0)
            {
                ctx.Result(TestStatus.NotTested, "no normal-attack hits recorded");
                yield break;
            }
            var violations = new List<string>();
            int maxSeen = 0;
            foreach (var kv in groups)
            {
                var attack = combos.Find(kv.Value.attack);
                int allowed = attack != null ? Mathf.Max(1, attack.maxHitsPerTarget) : 1;
                maxSeen = Mathf.Max(maxSeen, kv.Value.count);
                if (kv.Value.count > allowed) violations.Add($"{kv.Value.attack} AttackID {kv.Key.Item1}: {kv.Value.count} hits (allowed {allowed})");
            }
            if (violations.Count == 0) ctx.Pass($"{groups.Count} attack/target pairs checked, max hits per AttackID {maxSeen} (expected 1 unless multi-hit)");
            else ctx.Fail(string.Join("; ", violations.Take(6)));
        }

        private static IEnumerator Feedback(PlaytestContext ctx)
        {
            float since = ctx.Shared.TryGetValue(CombatStartKey, out var s) ? (float)s : 0f;
            var t = ctx.Telemetry;
            int hitStops = TelemetryRecorder.CountSince(t.HitStops, since);
            var cues = t.CameraCueKindsSince(since);
            var vfx = t.VfxIdsSince(since);
            int combo = (int)TelemetryRecorder.MaxSince(t.Combo, since);
            var impactIds = vfx.Where(id => id.StartsWith("impact") || id.StartsWith("spark") || id.Contains("parry")).ToList();
            ctx.Check("Hit stop", hitStops > 0, $"{hitStops} hit stop request(s), longest {TelemetryRecorder.MaxSince(t.HitStops, since):0.000} s");
            ctx.Check("Camera shake / FOV / zoom", cues.Contains("shake"), "cues: " + string.Join(", ", cues));
            ctx.Check("Sword trail", ctx.Sampler.SwordTrail, "trail emitted during attacks");
            ctx.Check("Impact VFX", impactIds.Count > 0, "effects: " + string.Join(", ", impactIds.Take(8)));
            ctx.Check("Combat audio", ctx.Sampler.SfxPlaying, "an SFX source played during combat");
            ctx.Check("Combo counter", combo >= 3, $"highest combo {combo}");
            ctx.Check("BREATH gauge fills", ctx.Sampler.BreathMax > 0f, $"peak BREATH {ctx.Sampler.BreathMax:0}");
            ctx.Pass("feedback systems invoked (look & feel need human review)");
            yield break;
        }
    }
}
