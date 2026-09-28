using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.Core;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>Player spawn, input system, movement, sprint and jump — measured, not assumed.</summary>
    public static class MovementSuite
    {
        public static IEnumerable<PlaytestStep> Steps()
        {
            yield return new PlaytestStep(C.Movement, "Player spawn", Spawn, 10f);
            yield return new PlaytestStep(C.Input, "Input actions", InputActions, 5f);
            yield return new PlaytestStep(C.Movement, "Movement", Movement, 10f);
            yield return new PlaytestStep(C.Movement, "Sprint", Sprint, 15f);
            yield return new PlaytestStep(C.Movement, "Jump", Jump, 10f);
        }

        private static IEnumerator Spawn(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => pc.Motor.Grounded, 3f, w);
            bool alive = pc.Damageable.IsAlive;
            ctx.Shared["spawn"] = pc.transform.position;
            ctx.Screens.CaptureAuto("PlayerSpawn");
            if (w.Success && alive && pc.transform.position.y > -5f)
                ctx.Pass($"at {pc.transform.position}, HP {pc.Damageable.Health.Current:0}/{pc.Damageable.Health.Max:0}, state {pc.State}");
            else ctx.Fail($"grounded {w.Success}, alive {alive}, position {pc.transform.position}");
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator InputActions(PlaytestContext ctx)
        {
            var input = InputReader.Instance;
            var labels = new List<string>();
            bool ok = input != null && input.Asset != null && input.GameplayEnabled;
            if (input != null)
            {
                foreach (var action in new[] { "Move", "Sprint", "Jump", "Dodge", "LightAttack", "HeavyAttack", "Block", "LockOn", "Skill1", "Ultimate", "CameraMode" })
                {
                    string label = input.GetBindingLabel(action);
                    if (label == "?" || string.IsNullOrEmpty(label)) ok = false;
                    labels.Add($"{action}={label}");
                }
            }
            if (ok) ctx.Pass("gameplay input enabled; " + string.Join(", ", labels));
            else ctx.Fail(input == null ? "InputReader missing" : "missing bindings or gameplay input disabled: " + string.Join(", ", labels));
            ctx.Sub("Simulated input layer", input != null && input.Simulation != null ? TestStatus.Pass : TestStatus.Fail,
                "auto test presses go through InputReader (same buffer/events as the keyboard)");
            yield break;
        }

        private static IEnumerator Movement(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            var pc = ctx.Player;
            Vector3 start = pc.transform.position;
            ctx.Driver.MoveWorld(Vector3.right);
            yield return ctx.WaitGame(1f);
            ctx.Driver.Stop();
            float d = PlaytestContext.Flat(start, pc.transform.position);
            if (d > 1.5f) ctx.Pass($"moved {d:0.00} m in 1.0 s (threshold 1.5 m)");
            else ctx.Fail($"moved only {d:0.00} m in 1.0 s (threshold 1.5 m)");
            yield return ctx.Observe(1f);
        }

        private static IEnumerator Sprint(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            var pc = ctx.Player;
            float runSpeed = 0f, sprintSpeed = 0f;

            // Normal run to the left.
            ctx.Driver.MoveWorld(Vector3.left);
            yield return ctx.WaitGame(0.45f);
            Vector3 a = pc.transform.position;
            yield return ctx.WaitGame(0.5f);
            runSpeed = PlaytestContext.Flat(a, pc.transform.position) / 0.5f;

            // Sprint in the same direction.
            float staminaBefore = pc.Stats.Stamina.Current;
            ctx.Driver.Sim.Sprint = true;
            yield return ctx.WaitGame(0.45f);
            a = pc.transform.position;
            yield return ctx.WaitGame(0.5f);
            sprintSpeed = PlaytestContext.Flat(a, pc.transform.position) / 0.5f;
            float staminaAfter = pc.Stats.Stamina.Current;
            ctx.Driver.Stop();

            if (sprintSpeed > runSpeed * 1.1f) ctx.Pass($"run {runSpeed:0.0} m/s → sprint {sprintSpeed:0.0} m/s");
            else ctx.Fail($"sprint not faster: run {runSpeed:0.0} m/s, sprint {sprintSpeed:0.0} m/s");
            ctx.Check("Stamina drains while sprinting", staminaAfter < staminaBefore, $"{staminaBefore:0} → {staminaAfter:0}");

            // Walk back towards the spawn so later tests have room.
            if (ctx.Shared.TryGetValue("spawn", out var s))
            {
                var w = new WaitResult();
                yield return ctx.Driver.MoveTo(ctx, (Vector3)s, 1f, 6f, false, w);
            }
        }

        private static IEnumerator Jump(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            var pc = ctx.Player;
            float y0 = pc.transform.position.y;
            float maxY = y0;
            ctx.Driver.Press(InputCommand.Jump);
            float t = 0f;
            while (t < 1.2f && !ctx.ShouldStop)
            {
                maxY = Mathf.Max(maxY, pc.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => pc.Motor.Grounded, 3f, w);
            float rise = maxY - y0;
            if (rise > 0.4f && w.Success) ctx.Pass($"rose {rise:0.00} m and landed");
            else ctx.Fail($"rise {rise:0.00} m (min 0.4), landed {w.Success}");
            yield return ctx.Observe(1f);
        }
    }
}
