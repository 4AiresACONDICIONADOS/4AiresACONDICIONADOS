using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BreathOfEclipse.AI;
using BreathOfEclipse.Breathing;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Breathing techniques through the real input path (skill keys → PlayerController → BreathingStyleSystem →
    /// SkillExecutor). Checks that every component is invoked; whether it looks good is for the human eye.
    /// </summary>
    public static class TechniqueSuite
    {
        public const string RisingSerpentId = "tidal_rising_serpent";
        public const string FlashBreakerId = "thunder_flash_breaker";

        public static IEnumerable<PlaytestStep> Reference()
        {
            yield return new PlaytestStep(C.Techniques, "Tidal Breath — Rising Serpent", Test_RisingSerpent, 15f);
            yield return new PlaytestStep(C.Techniques, "Thunder Breath — Flash Breaker", Test_FlashBreaker, 15f);
        }

        public static IEnumerable<PlaytestStep> Sweep()
        {
            yield return new PlaytestStep(C.Techniques, "Technique sweep (every form of every style)", TechniqueSweep, 300f);
        }

        /// <summary>Prepares a clean cast: style equipped, resources full, locked on the dummy at a medium distance.</summary>
        public static IEnumerator PrepareCast(PlaytestContext ctx, string styleId, TrainingDummy dummy, float distance, WaitResult ready)
        {
            ready.Success = false;
            yield return ctx.Settle();
            var pc = ctx.Player;
            if (pc.Breathing.Current == null || pc.Breathing.Current.styleId != styleId) pc.Breathing.EquipById(styleId);
            ctx.RefillPlayer();
            var w = new WaitResult();
            float d = PlaytestContext.Flat(pc.transform.position, dummy.transform.position);
            if (d > distance + 1.5f || d < distance - 1.5f)
            {
                Vector3 dir = pc.transform.position - dummy.transform.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : -dummy.transform.forward;
                yield return ctx.Driver.MoveTo(ctx, dummy.transform.position + dir * distance, 0.8f, 6f, false, w);
            }
            yield return ctx.LockOnto(dummy, w);
            yield return ctx.WaitGame(0.2f);
            ready.Success = pc.Breathing.Current != null && pc.Breathing.Current.styleId == styleId && ctx.IsLockedOn(dummy);
        }

        /// <summary>
        /// Casts a technique of the equipped style the way a player would: its quick slot key when it has one,
        /// otherwise through the form wheel request. Returns false when the style has no such form.
        /// </summary>
        public static bool CastForm(PlaytestContext ctx, string skillId)
        {
            var b = ctx.Player.Breathing;
            var style = b.Current;
            if (style == null) return false;
            for (int i = 0; i < style.FormCount; i++)
            {
                var form = style.GetForm(i);
                if (form == null || form.skill == null || form.skill.skillId != skillId) continue;
                int slot = b.SlotOfForm(i);
                if (slot >= 0) ctx.Driver.Press(SlotCommand(slot));
                else ctx.Player.RequestForm(i);
                return true;
            }
            return false;
        }

        private static InputCommand SlotCommand(int slot)
        {
            switch (slot)
            {
                case 0: return InputCommand.Skill1;
                case 1: return InputCommand.Skill2;
                case 2: return InputCommand.Skill3;
                case 3: return InputCommand.Skill4;
                default: return InputCommand.Ultimate;
            }
        }

        // ------------------------------------------------------------------ Rising Serpent

        public static IEnumerator Test_RisingSerpent(PlaytestContext ctx)
        {
            var dummy = ctx.EnsureDummy();
            var ready = new WaitResult();
            yield return PrepareCast(ctx, "tidal", dummy, 5f, ready);
            if (!ready.Success)
            {
                ctx.Fail("could not equip TIDAL BREATH and lock on the dummy");
                yield break;
            }
            var pc = ctx.Player;
            var exec = pc.Breathing.Executor;
            float mark = ctx.Telemetry.Mark();
            ctx.Sampler.Reset();

            var phases = new List<string>();
            bool techniqueState = false;
            bool anticipationShot = false, impactShot = false;
            float dashDistance = 0f;
            Vector3 dashStart = Vector3.zero;
            string lastPhase = null;

            CastForm(ctx, RisingSerpentId);
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => TelemetryRecorder.CountSince(ctx.Telemetry.Skills, mark, RisingSerpentId) > 0, 1f, w);
            bool started = w.Success;

            float t = 0f;
            while (started && t < 4f && !ctx.ShouldStop)
            {
                techniqueState |= pc.State == PlayerState.Skill;
                if (exec.Running && exec.Phase != null && exec.Phase.name != lastPhase)
                {
                    if (lastPhase == "Serpent Dash") dashDistance = PlaytestContext.Flat(dashStart, pc.transform.position);
                    lastPhase = exec.Phase.name;
                    phases.Add(lastPhase);
                    if (lastPhase == "Serpent Dash") dashStart = pc.transform.position;
                }
                if (!anticipationShot && lastPhase == "Low Stance" && exec.PhaseTime > 0.15f)
                {
                    anticipationShot = true;
                    ctx.Screens.CaptureAuto("RisingSerpent_Anticipation");
                }
                if (!impactShot && ctx.Telemetry.DamageSince(mark, d => d.Hit.SourceId == RisingSerpentId).Count > 0)
                {
                    impactShot = true;
                    ctx.Screens.CaptureAuto("RisingSerpent_Impact");
                }
                if (!exec.Running && t > 0.2f) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (lastPhase == "Serpent Dash") dashDistance = PlaytestContext.Flat(dashStart, pc.transform.position);

            bool finished = !exec.Running;
            var hits = ctx.Telemetry.DamageSince(mark, d => d.Hit.SourceId == RisingSerpentId && d.Result.Target == dummy.gameObject);
            var vfx = ctx.Telemetry.VfxIdsSince(mark);
            var cues = ctx.Telemetry.CameraCueKindsSince(mark);
            float hitStop = TelemetryRecorder.MaxSince(ctx.Telemetry.HitStops, mark);
            string[] expectedVfx = { "water_charge", "water_ground_ripple", "water_dash_wake", "water_serpent", "water_splash", "water_suspended" };
            var missingVfx = expectedVfx.Where(v => !vfx.Contains(v)).ToList();

            ctx.Check("1 Skill starts", started, started ? "SkillUsed event from the Skill1 key" : "no SkillUsed event");
            ctx.Check("2 Technique state", techniqueState, "PlayerState.Skill while executing");
            ctx.Check("3 Phases", phases.Count >= 4, string.Join(" → ", phases));
            ctx.Check("4 Dash occurs", dashDistance > 1f, $"dash phase moved {dashDistance:0.0} m");
            ctx.Check("5 VFX spawn", missingVfx.Count == 0, missingVfx.Count == 0 ? string.Join(", ", expectedVfx) : "missing: " + string.Join(", ", missingVfx), missingVfx.Count < 3);
            ctx.Check("6 Sword + water trail", ctx.Sampler.SwordTrail && ctx.Sampler.ElementTrail, $"sword {ctx.Sampler.SwordTrail}, element {ctx.Sampler.ElementTrail}");
            ctx.Check("7 Enemy receives damage", hits.Count > 0, hits.Count > 0 ? $"{hits.Count} hit(s), {hits.Sum(h => h.Result.Damage)} damage" : "no damage on the dummy");
            bool launch = hits.Any(h => h.Hit.Reaction == HitReaction.Launch && h.Hit.LaunchHeight > 0f);
            ctx.Check("8 Enemy launched", launch, launch ? "Launch reaction delivered (dummy is anchored; physical launch is verified on a Nightspawn in AI TEST)" : "no Launch reaction in the hit");
            ctx.Check("9 Camera cue", cues.Count > 0, "cues: " + string.Join(", ", cues));
            ctx.Check("10 Hit stop requested", hitStop >= 0.07f, $"longest hit stop {hitStop:0.000} s (design 0.08 s)");
            ctx.Check("11 Technique finishes", finished, finished ? "executor finished" : "still running after 4 s");

            // Player regains control: the move input moves the character again.
            yield return ctx.WaitUntil(() => pc.State != PlayerState.Skill, 2f, w);
            yield return ctx.WaitUntil(() => pc.Motor.Grounded, 3f, w);
            Vector3 p0 = pc.transform.position;
            ctx.Driver.MoveWorld(pc.transform.right);
            yield return ctx.WaitGame(0.4f);
            ctx.Driver.Stop();
            bool control = pc.State == PlayerState.Locomotion && PlaytestContext.Flat(p0, pc.transform.position) > 0.5f;
            ctx.Check("12 Player regains control", control, $"state {pc.State}, moved {PlaytestContext.Flat(p0, pc.transform.position):0.0} m after the technique");

            bool critical = started && techniqueState && hits.Count > 0 && finished && control;
            if (critical) ctx.Pass("all components invoked — judge the look in VFX TEST / Rising Serpent Visual Test");
            else ctx.Fail("see sub-results");
            yield return ctx.Observe(2.5f);
        }

        // ------------------------------------------------------------------ Flash Breaker

        private static IEnumerator Test_FlashBreaker(PlaytestContext ctx)
        {
            var dummy = ctx.EnsureDummy();
            var ready = new WaitResult();
            yield return PrepareCast(ctx, "thunder", dummy, 5f, ready);
            if (!ready.Success)
            {
                ctx.Fail("could not equip THUNDER BREATH and lock on the dummy");
                yield break;
            }
            var pc = ctx.Player;
            float mark = ctx.Telemetry.Mark();
            Vector3 before = pc.transform.position - dummy.transform.position;
            before.y = 0f;
            float minScale = 1f;
            CastForm(ctx, FlashBreakerId);
            var w = new WaitResult();
            bool shot = false;
            yield return ctx.WaitUntil(() =>
            {
                if (TimeController.Instance != null) minScale = Mathf.Min(minScale, TimeController.Instance.CurrentScale);
                if (!shot && ctx.Telemetry.DamageSince(mark, d => d.Hit.SourceId == FlashBreakerId).Count > 0)
                {
                    shot = true;
                    ctx.Screens.CaptureAuto("ThunderAttack");
                }
                return TelemetryRecorder.CountSince(ctx.Telemetry.Skills, mark, FlashBreakerId) > 0 && !pc.Breathing.IsExecuting;
            }, 5f, w);
            Vector3 after = pc.transform.position - dummy.transform.position;
            after.y = 0f;
            var hits = ctx.Telemetry.DamageSince(mark, d => d.Hit.SourceId == FlashBreakerId && d.Result.Target == dummy.gameObject);
            bool behind = Vector3.Dot(before.normalized, after.normalized) < 0.2f;
            ctx.Check("Charge slow-motion", minScale < 0.9f, $"lowest time scale {minScale:0.00}");
            ctx.Check("Appears behind the enemy", behind, $"side before/after dot {Vector3.Dot(before.normalized, after.normalized):0.00}");
            ctx.Check("Delayed cut damages", hits.Count > 0, hits.Count > 0 ? $"{hits.Sum(h => h.Result.Damage)} damage, critical {hits.Any(h => h.Result.IsCritical)}" : "no damage");
            ctx.Check("Thunder VFX", ctx.Telemetry.VfxIdsSince(mark).Any(id => id.StartsWith("thunder")), string.Join(", ", ctx.Telemetry.VfxIdsSince(mark).Where(id => id.StartsWith("thunder"))));
            if (w.Success && hits.Count > 0) ctx.Pass("sheathe → flash → pause → cut");
            else ctx.Fail($"finished {w.Success}, hits {hits.Count}");
            yield return ctx.Observe(2.5f);
        }

        // ------------------------------------------------------------------ sweep

        private static IEnumerator TechniqueSweep(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var dummy = ctx.EnsureDummy();
            int ok = 0, total = 0;
            var styles = pc.Breathing.Styles.ToList();
            foreach (var style in styles)
            {
                // Every form (I … XI), not only the four quick slots: this is what the form wheel exposes.
                for (int formIndex = 0; formIndex < style.FormCount && !ctx.ShouldStop; formIndex++)
                {
                    var form = style.GetForm(formIndex);
                    var skill = form != null ? form.skill : null;
                    if (skill == null) continue;
                    total++;
                    var ready = new WaitResult();
                    yield return PrepareCast(ctx, style.styleId, dummy, 5f, ready);
                    float mark = ctx.Telemetry.Mark();
                    pc.RequestForm(formIndex);
                    var w = new WaitResult();
                    yield return ctx.WaitUntil(() => TelemetryRecorder.CountSince(ctx.Telemetry.Skills, mark, skill.skillId) > 0, 1f, w);
                    bool started = w.Success;
                    if (started) yield return ctx.WaitUntil(() => !pc.Breathing.IsExecuting, 6f, w);
                    bool finished = started && w.Success;
                    int vfx = TelemetryRecorder.CountSince(ctx.Telemetry.Vfx, mark);
                    int hits = ctx.Telemetry.DamageSince(mark, d => d.Hit.AttackerTeam == Team.Player && d.Result.Target == dummy.gameObject).Count;
                    bool damaging = skill.phases.Any(p => p.hits.Count > 0 || (p.projectile != null && p.projectile.enabled));
                    TestStatus status = !started || !finished || vfx == 0 ? TestStatus.Fail : damaging && hits == 0 ? TestStatus.Warning : TestStatus.Pass;
                    if (status == TestStatus.Pass) ok++;
                    ctx.Report(C.Techniques, $"{style.displayName} — {Roman.Of(form.formNumber)} {skill.displayName}", status,
                        $"started {started}, finished {finished}, {vfx} VFX, {hits} hit(s){(damaging ? "" : " (no damage by design)")}");
                    yield return ctx.Observe(1f);
                }
            }
            if (ok == total) ctx.Pass($"{ok}/{total} techniques executed with VFX (and damage where designed)");
            else ctx.Warn($"{ok}/{total} fully passed — see individual lines");
        }
    }
}
