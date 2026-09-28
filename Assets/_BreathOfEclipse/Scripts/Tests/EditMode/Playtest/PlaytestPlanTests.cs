using System.Collections.Generic;
using System.Linq;
using BreathOfEclipse.Playtest;
using NUnit.Framework;

namespace BreathOfEclipse.Tests
{
    /// <summary>Sanity checks of the playtest plans (the gameplay itself is exercised by the runtime playtest).</summary>
    public class PlaytestPlanTests
    {
        [Test]
        public void AutomatedModes_HaveSteps_WithUniqueNames()
        {
            foreach (var mode in new[] { PlaytestMode.Full, PlaytestMode.Auto, PlaytestMode.AiTest, PlaytestMode.BossTest })
            {
                var steps = PlaytestPlans.Build(mode);
                Assert.Greater(steps.Count, 0, mode.ToString());
                var names = new HashSet<string>();
                foreach (var s in steps)
                {
                    Assert.IsTrue(names.Add(s.Category + "/" + s.Name), $"{mode}: duplicate step {s.Name}");
                    Assert.IsNotNull(s.Body, s.Name);
                    Assert.Greater(s.Timeout, 0f, s.Name);
                }
            }
        }

        [Test]
        public void FullTest_FollowsTheRequestedOrder()
        {
            var steps = PlaytestPlans.Build(PlaytestMode.Full);
            int Index(string category) => steps.FindIndex(s => s.Category == category);
            Assert.AreEqual(PlaytestCategories.Boot, steps[0].Category, "Full test starts at Boot");
            Assert.Less(Index(PlaytestCategories.MainMenu), Index(PlaytestCategories.Movement));
            Assert.Less(Index(PlaytestCategories.Combat), Index(PlaytestCategories.Techniques));
            Assert.Less(Index(PlaytestCategories.Techniques), Index(PlaytestCategories.Cameras));
            Assert.Less(Index(PlaytestCategories.Cameras), Index(PlaytestCategories.Enemies));
            Assert.Less(Index(PlaytestCategories.Enemies), Index(PlaytestCategories.Boss));
            Assert.AreEqual(PlaytestCategories.Performance, steps.Last().Category, "Full test ends with the performance summary");
            Assert.IsTrue(steps.Any(s => s.Name.Contains("Rising Serpent")));
            Assert.IsTrue(steps.Any(s => s.Name.Contains("Eclipse Cleave")));
        }

        [Test]
        public void Labs_AreNotAutomated()
        {
            Assert.IsFalse(PlaytestPlans.IsAutomated(PlaytestMode.Manual));
            Assert.IsFalse(PlaytestPlans.IsAutomated(PlaytestMode.VfxLab));
            Assert.IsFalse(PlaytestPlans.IsAutomated(PlaytestMode.CameraLab));
            Assert.IsFalse(PlaytestPlans.IsAutomated(PlaytestMode.RisingSerpentVisual));
            Assert.IsTrue(PlaytestPlans.IsAutomated(PlaytestMode.Full));
        }

        [Test]
        public void ScreenshotNames_AreFileSafe()
        {
            Assert.AreEqual("Rising_Serpent_Impact", PlaytestPaths.Sanitize("Rising Serpent — Impact"));
            Assert.AreEqual("None", PlaytestPaths.Sanitize(""));
        }
    }
}
