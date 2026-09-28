using System.Collections;
using System.Collections.Generic;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>Which steps each automated mode runs, in order.</summary>
    public static class PlaytestPlans
    {
        public static List<PlaytestStep> Build(PlaytestMode mode)
        {
            var steps = new List<PlaytestStep>();
            switch (mode)
            {
                case PlaytestMode.Full:
                    steps.AddRange(SceneFlowSuite.BootAndMenu());
                    steps.AddRange(MovementSuite.Steps());
                    steps.AddRange(CombatSuite.Steps());
                    steps.AddRange(TechniqueSuite.Reference());
                    steps.AddRange(CameraSuite.Steps());
                    steps.AddRange(AiSuite.Steps());
                    steps.AddRange(BossSuite.Steps());
                    steps.AddRange(SystemsSuite.Ui());
                    steps.AddRange(SystemsSuite.AudioSteps());
                    steps.AddRange(SystemsSuite.SaveSteps());
                    steps.AddRange(SystemsSuite.VfxLibrary());
                    steps.AddRange(SceneFlowSuite.Forest());
                    steps.AddRange(SystemsSuite.PerformanceSteps());
                    break;
                case PlaytestMode.Auto:
                    steps.Add(EnsureCombatTestStep());
                    steps.AddRange(MovementSuite.Steps());
                    steps.AddRange(CombatSuite.Steps());
                    steps.AddRange(TechniqueSuite.Reference());
                    steps.AddRange(TechniqueSuite.Sweep());
                    steps.AddRange(CameraSuite.Steps());
                    steps.AddRange(SystemsSuite.Ui());
                    steps.AddRange(SystemsSuite.AudioSteps());
                    steps.AddRange(SystemsSuite.SaveSteps());
                    steps.AddRange(SystemsSuite.VfxLibrary());
                    steps.AddRange(SystemsSuite.PerformanceSteps());
                    break;
                case PlaytestMode.AiTest:
                    steps.Add(EnsureCombatTestStep());
                    steps.AddRange(AiSuite.Steps());
                    steps.AddRange(SystemsSuite.PerformanceSteps());
                    break;
                case PlaytestMode.BossTest:
                    steps.Add(EnsureCombatTestStep());
                    steps.AddRange(BossSuite.Steps());
                    steps.AddRange(SystemsSuite.PerformanceSteps());
                    break;
            }
            return steps;
        }

        public static bool IsAutomated(PlaytestMode mode) =>
            mode == PlaytestMode.Full || mode == PlaytestMode.Auto || mode == PlaytestMode.AiTest || mode == PlaytestMode.BossTest;

        public static PlaytestStep EnsureCombatTestStep() =>
            new PlaytestStep(C.Scenes, "03_CombatTest ready", EnsureCombatTest, 45f, false);

        private static IEnumerator EnsureCombatTest(PlaytestContext ctx)
        {
            var w = new WaitResult();
            yield return ctx.EnsureCombatTest(w);
            if (!w.Success)
            {
                ctx.Fail("03_CombatTest could not be loaded");
                ctx.Runner.Abort("scene cannot load: 03_CombatTest");
                yield break;
            }
            SceneSanityChecker.Report(ctx, C.Scenes, "03_CombatTest", true);
            ctx.Pass("scene loaded, player spawned");
        }
    }
}
