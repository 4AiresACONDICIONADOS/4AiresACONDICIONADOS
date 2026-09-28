using System;
using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Executes a list of steps as guarded coroutines: exceptions, timeouts and skips fail/mark only the current
    /// step and the run continues. The whole run aborts only for critical problems (GameManager missing,
    /// player missing after recovery, scene cannot load, exception loop) or when the tester presses F12.
    /// </summary>
    public sealed class PlaytestRunner
    {
        public readonly List<PlaytestStep> Steps;
        public int Index { get; private set; } = -1;
        public PlaytestStep Current => Index >= 0 && Index < Steps.Count ? Steps[Index] : null;
        public bool Paused { get; set; }
        public bool SkipRequested { get; set; }
        public bool AbortRequested { get; private set; }
        public string AbortReason { get; private set; }
        public bool StepTimedOut { get; private set; }
        public bool Finished { get; private set; }

        public PlaytestRunner(List<PlaytestStep> steps) => Steps = steps;

        public void Abort(string reason)
        {
            if (AbortRequested) return;
            AbortRequested = true;
            AbortReason = reason;
            Debug.LogWarning($"[Playtest] ABORT: {reason}");
        }

        public IEnumerator Run(PlaytestContext ctx)
        {
            for (Index = 0; Index < Steps.Count; Index++)
            {
                var step = Steps[Index];
                if (AbortRequested)
                {
                    ctx.Report(step.Category, step.Name, TestStatus.NotTested, "run aborted: " + AbortReason);
                    continue;
                }

                ctx.BeginStep(step);
                SkipRequested = false;
                StepTimedOut = false;

                if (GameManager.Instance == null)
                {
                    Abort("GameManager missing");
                    ctx.Result(TestStatus.NotTested, "run aborted: GameManager missing");
                    continue;
                }

                if (step.RequiresPlayer)
                {
                    ctx.RecoverPlayerIfNeeded();
                    if (PlayerController.Instance == null)
                    {
                        // One recovery attempt: reload the combat test scene.
                        var w = new WaitResult();
                        yield return ctx.EnsureCombatTest(w);
                        if (PlayerController.Instance == null)
                        {
                            Abort("Player missing");
                            ctx.Result(TestStatus.NotTested, "run aborted: player missing");
                            continue;
                        }
                    }
                }

                float start = Time.realtimeSinceStartup;
                yield return Guarded(step, ctx, start);

                ctx.Driver.ReleaseAll();
                if (SkipRequested) ctx.Report(step.Category, step.Name, TestStatus.NotTested, "skipped by tester (F11)");
                else if (StepTimedOut) ctx.Report(step.Category, step.Name, TestStatus.Fail, $"timeout after {step.Timeout:0} s");
                else if (ctx.ResultsThisStep == 0 && !AbortRequested) ctx.Report(step.Category, step.Name, TestStatus.Warning, "step finished without reporting a result");

                if (ctx.Logger.ExceptionLoopDetected) Abort("critical exception loop (>60 errors in 5 s)");

                // Short gap between tests (longer in VISUAL so each test can be followed on screen).
                float gap = ctx.Speed == PlaytestSpeed.Visual ? 0.8f : ctx.Speed == PlaytestSpeed.Normal ? 0.25f : 0.08f;
                float g = 0f;
                while (g < gap && !AbortRequested)
                {
                    if (!Paused) g += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            Index = Steps.Count - 1;
            Finished = true;
        }

        /// <summary>Runs the step body with nested-iterator support, exception capture, timeout, pause and skip.</summary>
        private IEnumerator Guarded(PlaytestStep step, PlaytestContext ctx, float start)
        {
            IEnumerator root;
            try
            {
                root = step.Body(ctx);
            }
            catch (Exception e)
            {
                ctx.Report(step.Category, step.Name, TestStatus.Fail, "exception: " + e.Message);
                yield break;
            }

            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            float pausedTime = 0f;
            while (stack.Count > 0)
            {
                if (AbortRequested || SkipRequested) yield break;
                if (Paused)
                {
                    pausedTime += Time.unscaledDeltaTime;
                    yield return null;
                    continue;
                }
                if (Time.realtimeSinceStartup - start - pausedTime > step.Timeout)
                {
                    StepTimedOut = true;
                    yield break;
                }

                var top = stack.Peek();
                bool moved;
                object current = null;
                try
                {
                    moved = top.MoveNext();
                    if (moved) current = top.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    ctx.Report(step.Category, step.Name, TestStatus.Fail, $"exception: {e.GetType().Name}: {e.Message}");
                    yield break;
                }

                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return current;
            }
        }
    }
}
