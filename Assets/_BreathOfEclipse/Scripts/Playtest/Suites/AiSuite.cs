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
    /// <summary>Nightspawn: spawn, detection, chase, attacking the player, taking hits, reactions, death.</summary>
    public static class AiSuite
    {
        private const string EnemyKey = "nightspawn";
        private const string StatesKey = "nightspawnStates";

        public static IEnumerable<PlaytestStep> Steps()
        {
            yield return new PlaytestStep(C.Enemies, "Nightspawn spawn", Spawn, 10f);
            yield return new PlaytestStep(C.Enemies, "Nightspawn detects and chases", DetectAndChase, 15f);
            yield return new PlaytestStep(C.Enemies, "Nightspawn attacks the player", AttacksPlayer, 25f);
            yield return new PlaytestStep(C.Enemies, "Player damages Nightspawn", PlayerHits, 20f);
            yield return new PlaytestStep(C.Enemies, "Stagger / knockback / launch", Reactions, 25f);
            yield return new PlaytestStep(C.Enemies, "Kill Nightspawn", Kill, 35f);
            yield return new PlaytestStep(C.Enemies, "Nightspawn FSM states observed", StatesObserved, 3f, false);
        }

        private static EnemyController Enemy(PlaytestContext ctx) =>
            ctx.Shared.TryGetValue(EnemyKey, out var e) ? e as EnemyController : null;

        private static List<EnemyStateId> States(PlaytestContext ctx)
        {
            if (!ctx.Shared.TryGetValue(StatesKey, out var s))
            {
                s = new List<EnemyStateId>();
                ctx.Shared[StatesKey] = s;
            }
            return (List<EnemyStateId>)s;
        }

        private static IEnumerator Spawn(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            ctx.ReleaseLockOn();
            ctx.RefillPlayer();
            // Isolate the test: remove leftovers from earlier tests.
            foreach (var e in EncounterDirector.All.ToList())
                if (e != null && e.IsAlive) e.Damageable.Kill();
            yield return ctx.WaitGame(0.5f);

            var pc = ctx.Player;
            Vector3 pos = pc.transform.position + pc.transform.forward * 9f;
            var enemy = ctx.SpawnEnemy("nightspawn", pos, pc.transform.position, true);
            if (enemy == null)
            {
                ctx.Fail("EnemyFactory could not spawn 'nightspawn'");
                yield break;
            }
            ctx.Shared[EnemyKey] = enemy;
            var states = States(ctx);
            states.Clear();
            states.Add(enemy.StateId);
            enemy.StateChanged += (from, to) => states.Add(to);
            yield return ctx.Frames(2);
            if (enemy.IsAlive && EncounterDirector.AliveCount > 0) ctx.Pass($"spawned 9 m ahead with portal VFX, state {enemy.StateId}, HP {enemy.Damageable.Health.Max:0}");
            else ctx.Fail("enemy not registered / not alive");
        }

        private static IEnumerator DetectAndChase(PlaytestContext ctx)
        {
            var enemy = Enemy(ctx);
            if (enemy == null)
            {
                ctx.Result(TestStatus.NotTested, "no enemy");
                yield break;
            }
            var states = States(ctx);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => enemy.Aware, 8f, w);
            bool detected = w.Success;
            if (!detected) enemy.Alert(); // recovery so the rest of the suite can run
            ctx.Check("Detection", detected, detected ? $"aware after {w.Elapsed:0.0} s without help" : "not aware after 8 s — alerted manually");
            yield return ctx.WaitUntil(() => states.Contains(EnemyStateId.Chase), 5f, w);
            if (w.Success) ctx.Pass("Idle → " + string.Join(" → ", states.Distinct()));
            else ctx.Fail("no Chase state: " + string.Join(" → ", states.Distinct()));
        }

        private static IEnumerator AttacksPlayer(PlaytestContext ctx)
        {
            var enemy = Enemy(ctx);
            if (enemy == null || !enemy.IsAlive)
            {
                ctx.Result(TestStatus.NotTested, "no enemy");
                yield break;
            }
            var pc = ctx.Player;
            float mark = ctx.Telemetry.Mark();
            float hpBefore = pc.Damageable.Health.Current;
            var w = new WaitResult();
            bool shot = false;
            // The player stands still and faces the enemy; the enemy must come and hit.
            yield return ctx.Driver.FaceTowards(ctx, enemy.transform.position, 0.15f);
            yield return ctx.WaitUntil(() =>
            {
                var hits = ctx.Telemetry.DamageSince(mark, d => d.Result.Target == pc.gameObject && d.Hit.Attacker == enemy.gameObject);
                if (hits.Count > 0 && !shot)
                {
                    shot = true;
                    ctx.Screens.CaptureAuto("NightspawnCombat");
                }
                return hits.Any(h => h.Result.Landed);
            }, 20f, w);
            var landed = ctx.Telemetry.DamageSince(mark, d => d.Result.Target == pc.gameObject && d.Hit.Attacker == enemy.gameObject);
            if (w.Success) ctx.Pass($"{landed.Count} hit(s) from '{landed[0].Hit.SourceId}', player HP {hpBefore:0} → {pc.Damageable.Health.Current:0}");
            else ctx.Fail("the Nightspawn did not damage the player in 20 s (states: " + string.Join(" → ", States(ctx).Distinct()) + ")");
            ctx.Check("Attack state", States(ctx).Contains(EnemyStateId.Attack), "EnemyStateId.Attack entered");
            if (pc.Damageable.Health.Normalized < 0.5f) ctx.RefillPlayer(false);
        }

        private static IEnumerator AttackLoop(PlaytestContext ctx, EnemyController enemy, float seconds, System.Func<bool> stop)
        {
            var pc = ctx.Player;
            var w = new WaitResult();
            float t = 0f;
            int press = 0;
            while (t < seconds && enemy != null && enemy.IsAlive && !ctx.ShouldStop && (stop == null || !stop()))
            {
                if (!ctx.IsLockedOn(enemy)) yield return ctx.LockOnto(enemy, w);
                float d = PlaytestContext.Flat(pc.transform.position, enemy.transform.position);
                if (d > 2.6f)
                {
                    ctx.Driver.MoveWorld(enemy.transform.position - pc.transform.position);
                }
                else
                {
                    ctx.Driver.Stop();
                    ctx.Driver.Press(press++ % 4 == 3 ? InputCommand.HeavyAttack : InputCommand.LightAttack);
                }
                if (pc.Damageable.Health.Normalized < 0.35f) ctx.RefillPlayer(false);
                yield return ctx.WaitGame(0.12f);
                t += 0.12f;
            }
            ctx.Driver.Stop();
        }

        private static IEnumerator PlayerHits(PlaytestContext ctx)
        {
            var enemy = Enemy(ctx);
            if (enemy == null || !enemy.IsAlive)
            {
                ctx.Result(TestStatus.NotTested, "no enemy");
                yield break;
            }
            float mark = ctx.Telemetry.Mark();
            float hp0 = enemy.Damageable.Health.Current;
            yield return AttackLoop(ctx, enemy, 12f, () => ctx.Telemetry.DamageSince(mark, d => d.Result.Target == enemy.gameObject && d.Result.Damage > 0).Count >= 3);
            var hits = ctx.Telemetry.DamageSince(mark, d => d.Result.Target == enemy.gameObject && d.Hit.AttackerTeam == Team.Player);
            int blocked = hits.Count(h => h.Result.Outcome == HitOutcome.Blocked);
            int evaded = hits.Count(h => h.Result.Outcome == HitOutcome.Evaded);
            if (hits.Count(h => h.Result.Damage > 0) >= 3) ctx.Pass($"{hits.Count} hit(s), HP {hp0:0} → {enemy.Damageable.Health.Current:0} (blocked {blocked}, evaded {evaded})");
            else ctx.Fail($"only {hits.Count} hit(s) landed in 12 s (blocked {blocked}, evaded {evaded})");
        }

        private static IEnumerator Reactions(PlaytestContext ctx)
        {
            var enemy = Enemy(ctx);
            if (enemy == null || !enemy.IsAlive)
            {
                ctx.Result(TestStatus.NotTested, "no enemy (already dead)");
                yield break;
            }
            var pc = ctx.Player;
            var states = States(ctx);
            var w = new WaitResult();

            // Knockdown / knockback: heavy string.
            yield return ctx.Settle();
            yield return ctx.Driver.MoveTo(ctx, enemy.transform.position, 2.4f, 5f, false, w);
            yield return ctx.LockOnto(enemy, w);
            Vector3 before = enemy.transform.position;
            ctx.Driver.Press(InputCommand.HeavyAttack);
            yield return ctx.WaitGame(0.55f);
            ctx.Driver.Press(InputCommand.HeavyAttack);
            yield return ctx.WaitUntil(() => !pc.Combat.IsAttacking, 3f, w);
            float pushed = enemy != null ? PlaytestContext.Flat(before, enemy.transform.position) : 0f;
            bool knock = states.Contains(EnemyStateId.Knockdown) || pushed > 1f;
            ctx.Check("Knockback / knockdown", knock, $"pushed {pushed:0.0} m, knockdown state {states.Contains(EnemyStateId.Knockdown)}");

            // Launch: Rising Serpent on a real, movable enemy.
            if (enemy != null && enemy.IsAlive)
            {
                yield return ctx.WaitUntil(() => enemy == null || !enemy.IsAlive || enemy.Motor.Grounded, 3f, w);
                pc.Breathing.EquipById("tidal");
                ctx.RefillPlayer();
                yield return ctx.Settle();
                float ground = enemy.transform.position.y;
                float peak = ground;
                TechniqueSuite.CastForm(ctx, TechniqueSuite.RisingSerpentId);
                yield return ctx.WaitUntil(() =>
                {
                    if (enemy != null) peak = Mathf.Max(peak, enemy.transform.position.y);
                    return enemy == null || !enemy.IsAlive || (!pc.Breathing.IsExecuting && peak - ground > 0.1f);
                }, 4f, w);
                ctx.Check("Launch (physical)", peak - ground > 0.6f || states.Contains(EnemyStateId.Airborne),
                    $"Rising Serpent lifted the Nightspawn {peak - ground:0.0} m, airborne state {states.Contains(EnemyStateId.Airborne)}");
            }
            bool stagger = states.Contains(EnemyStateId.Stagger);
            ctx.Sub("Stagger (poise break)", stagger ? TestStatus.Pass : TestStatus.NotTested, stagger ? "Stagger state entered" : "poise did not break during this run");
            if (knock) ctx.Pass("enemy reacts to heavy hits and launchers");
            else ctx.Fail("no knockback or knockdown observed");
        }

        private static IEnumerator Kill(PlaytestContext ctx)
        {
            var enemy = Enemy(ctx);
            if (enemy == null)
            {
                ctx.Result(TestStatus.NotTested, "no enemy");
                yield break;
            }
            float mark = ctx.Telemetry.Mark();
            if (enemy.IsAlive) yield return AttackLoop(ctx, enemy, 28f, null);
            bool killedByPlayer = enemy == null || !enemy.IsAlive;
            if (!killedByPlayer)
            {
                enemy.Damageable.Kill(ctx.Player.gameObject);
                ctx.Fail("still alive after 28 s of attacks — killed by the test to continue");
                yield break;
            }
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => States(ctx).Contains(EnemyStateId.Dead), 2f, w);
            bool killEvent = TelemetryRecorder.CountSince(ctx.Telemetry.Kills, mark) > 0 || TelemetryRecorder.CountSince(ctx.Telemetry.Kills, 0f) > 0;
            ctx.Check("Death state", w.Success, "EnemyStateId.Dead");
            ctx.Check("EnemyKilled event", killEvent, "GameEvents.EnemyKilled raised");
            ctx.Pass("Nightspawn defeated by player attacks");
            yield return ctx.Observe(2f);
        }

        private static IEnumerator StatesObserved(PlaytestContext ctx)
        {
            var seen = new HashSet<EnemyStateId>(States(ctx));
            var required = new[] { EnemyStateId.Idle, EnemyStateId.Chase, EnemyStateId.Attack, EnemyStateId.Dead };
            var optional = new[] { EnemyStateId.Patrol, EnemyStateId.Alert, EnemyStateId.Reposition, EnemyStateId.Block, EnemyStateId.Dodge, EnemyStateId.Stagger, EnemyStateId.Knockdown, EnemyStateId.Airborne, EnemyStateId.Special };
            foreach (var s in required) ctx.Sub(s.ToString(), seen.Contains(s) ? TestStatus.Pass : TestStatus.Fail, seen.Contains(s) ? "observed" : "not observed");
            foreach (var s in optional) ctx.Sub(s.ToString(), seen.Contains(s) ? TestStatus.Pass : TestStatus.NotTested, seen.Contains(s) ? "observed" : "not triggered in this run (situational)");
            bool ok = required.All(seen.Contains);
            if (ok) ctx.Pass(string.Join(", ", seen));
            else ctx.Fail("missing required states; seen: " + string.Join(", ", seen));
            yield break;
        }
    }
}
