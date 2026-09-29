using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Data;
using BreathOfEclipse.VFX;
using NUnit.Framework;
using UnityEngine;

namespace BreathOfEclipse.Tests
{
    /// <summary>
    /// Validates the default content: every id referenced by attacks, techniques and enemies must exist, so a typo
    /// in data never silently removes an animation, effect or sound.
    /// </summary>
    public class ContentValidationTests
    {
        private GameDatabase _db;

        [OneTimeSetUp]
        public void Build() => _db = DefaultContent.Build();

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (_db != null) Object.DestroyImmediate(_db);
        }

        private IEnumerable<SkillData> AllSkills()
        {
            foreach (var style in _db.styles)
                for (int i = 0; i < style.TechniqueCount; i++)
                {
                    var s = style.GetTechnique(i);
                    if (s != null) yield return s;
                }
        }

        private IEnumerable<AttackData> AllAttacks()
        {
            var set = new HashSet<AttackData>();
            var c = _db.playerCombos;
            foreach (var n in c.nodes) { set.Add(n.attack); set.Add(n.nextLight); set.Add(n.nextHeavy); }
            set.Add(c.groundLight); set.Add(c.groundHeavy); set.Add(c.dashLight); set.Add(c.dashHeavy);
            set.Add(c.airLight); set.Add(c.airHeavy); set.Add(c.perfectDodgeCounter); set.Add(c.parryCounter);
            set.Remove(null);
            return set;
        }

        [Test]
        public void FiveBreathingStyles_EachWithFormsQuickSlotsAndUltimate()
        {
            Assert.AreEqual(5, _db.styles.Count);
            var ids = new HashSet<string>();
            foreach (var style in _db.styles)
            {
                Assert.IsTrue(ids.Add(style.styleId), $"Duplicate style id {style.styleId}");
                Assert.GreaterOrEqual(style.FormCount, 4, style.styleId);
                Assert.LessOrEqual(style.FormCount, 11, style.styleId);
                int lastNumber = 0;
                for (int i = 0; i < style.FormCount; i++)
                {
                    var form = style.GetForm(i);
                    Assert.IsNotNull(form.skill, $"{style.styleId} form {i + 1}");
                    Assert.Greater(form.formNumber, lastNumber, $"{style.styleId}: form numbers must increase");
                    Assert.LessOrEqual(form.formNumber, 11, style.styleId);
                    Assert.IsNotEmpty(form.skill.voiceTechniqueCall, $"{form.skill.skillId}: Spanish name call");
                    lastNumber = form.formNumber;
                }
                Assert.IsNotEmpty(style.styleCall, style.styleId);
                for (int slot = 0; slot < BreathingStyleData.QuickSlotCount; slot++)
                    Assert.IsNotNull(style.GetSkill(slot), $"{style.styleId} quick slot {slot + 1}");
                Assert.IsNotNull(style.ultimate, style.styleId);
                Assert.AreEqual(SkillTier.Ultimate, style.ultimate.tier, style.styleId);
            }
        }

        [Test]
        public void WaterBreathing_HasSevenForms_RisingSerpentIsTheSeventh()
        {
            var tidal = _db.FindStyle("tidal");
            Assert.IsNotNull(tidal);
            Assert.AreEqual(7, tidal.FormCount);
            Assert.AreEqual("tidal_rising_serpent", tidal.GetForm(6).skill.skillId);
        }

        [Test]
        public void ReferenceTechniques_Exist()
        {
            Assert.IsNotNull(_db.FindSkill("tidal_rising_serpent"), "TIDAL BREATH — RISING SERPENT missing");
            Assert.IsNotNull(_db.FindSkill("thunder_flash_breaker"), "THUNDER BREATH — FLASH BREAKER missing");
        }

        [Test]
        public void SkillIds_AreUnique_AndEverySkillHasPhases()
        {
            var ids = new HashSet<string>();
            foreach (var s in AllSkills())
            {
                Assert.IsTrue(ids.Add(s.skillId), $"Duplicate skill id {s.skillId}");
                Assert.Greater(s.phases.Count, 0, s.skillId);
            }
        }

        [Test]
        public void AllMotionIds_Exist()
        {
            foreach (var a in AllAttacks())
                Assert.IsTrue(MotionLibrary.Has(a.motionId), $"Attack {a.attackId}: unknown motion '{a.motionId}'");
            foreach (var s in AllSkills())
                foreach (var p in s.phases)
                    if (!string.IsNullOrEmpty(p.motionId))
                        Assert.IsTrue(MotionLibrary.Has(p.motionId), $"Skill {s.skillId}/{p.name}: unknown motion '{p.motionId}'");
            foreach (var e in _db.enemies)
            {
                foreach (var atk in e.attacks) Assert.IsTrue(MotionLibrary.Has(atk.motionId), $"{e.enemyId}/{atk.attackId}: unknown motion '{atk.motionId}'");
                foreach (var atk in e.phase2Attacks) Assert.IsTrue(MotionLibrary.Has(atk.motionId), $"{e.enemyId}/{atk.attackId}: unknown motion '{atk.motionId}'");
            }
        }

        [Test]
        public void AllVfxIds_Exist()
        {
            void Check(string id, string owner)
            {
                if (string.IsNullOrEmpty(id)) return;
                Assert.IsTrue(VFXLibrary.Has(id), $"{owner}: unknown VFX '{id}'");
            }
            foreach (var a in AllAttacks()) Check(a.impactVfx, a.attackId);
            foreach (var s in AllSkills())
                foreach (var p in s.phases)
                {
                    foreach (var v in p.vfx) Check(v.vfxId, s.skillId);
                    foreach (var h in p.hits) Check(h.impactVfx, s.skillId);
                    Check(p.pathVfx, s.skillId);
                }
            foreach (var style in _db.styles)
            {
                Check(style.swordAuraVfx, style.styleId);
                Check(style.lightHitVfx, style.styleId);
            }
            foreach (var e in _db.enemies)
                foreach (var atk in e.attacks)
                {
                    Check(atk.telegraphVfx, e.enemyId);
                    Check(atk.swingVfx, e.enemyId);
                    Check(atk.impactVfx, e.enemyId);
                }
        }

        [Test]
        public void AllSfxIds_HaveProceduralPlaceholders()
        {
            var checkedIds = new HashSet<string>();
            void Check(string id, string owner)
            {
                if (string.IsNullOrEmpty(id) || !checkedIds.Add(id)) return;
                var clip = ProceduralAudio.Generate(id);
                Assert.IsNotNull(clip, $"{owner}: no procedural sound for '{id}'");
                Object.DestroyImmediate(clip);
            }
            foreach (var a in AllAttacks())
            {
                Check(a.swingSfx, a.attackId);
                Check(a.hitSfx, a.attackId);
            }
            foreach (var s in AllSkills())
                foreach (var p in s.phases)
                {
                    foreach (var c in p.sfx) Check(c.sfxId, s.skillId);
                    foreach (var h in p.hits) Check(h.hitSfx, s.skillId);
                }
            foreach (var style in _db.styles) Check(style.equipSfx, style.styleId);
            foreach (var e in _db.enemies)
                foreach (var atk in e.attacks) Check(atk.sfx, e.enemyId);
        }

        [Test]
        public void RequiredCombos_Resolve()
        {
            var graph = _db.playerCombos.BuildGraph();
            var ground = ComboContext.Ground;
            string Run(params ComboInput[] inputs)
            {
                string current = null;
                var ids = new List<string>();
                foreach (var input in inputs)
                {
                    current = graph.Resolve(current, input, new ComboContext());
                    if (current == null) return null;
                    ids.Add(current);
                }
                return string.Join(">", ids);
            }
            const ComboInput L = ComboInput.Light, H = ComboInput.Heavy;
            Assert.AreEqual("L1>L2>L3>L4", Run(L, L, L, L));
            Assert.AreEqual("L1>L2>L2H", Run(L, L, H));
            Assert.AreEqual("L1>L1H>L1HH", Run(L, H, H));
            Assert.AreEqual("H1>H2", Run(H, H));
            Assert.IsNotNull(graph.Resolve(null, L, new ComboContext { Dashing = true }), "Dash + L");
            Assert.IsNotNull(graph.Resolve(null, L, new ComboContext { Airborne = true }), "Jump + L");
            Assert.IsNotNull(graph.Resolve(null, H, new ComboContext { Airborne = true }), "Jump + H");
            Assert.AreEqual(_db.playerCombos.perfectDodgeCounter.attackId, graph.Resolve(null, L, new ComboContext { PerfectDodgeWindow = true }));
            Assert.AreEqual(_db.playerCombos.parryCounter.attackId, graph.Resolve(null, H, new ComboContext { ParryWindow = true }));
            Assert.IsNotNull(graph.Resolve(null, L, ground));
        }

        [Test]
        public void Enemies_NightspawnAndHollowOni_Exist()
        {
            var ns = _db.FindEnemy("nightspawn");
            var oni = _db.FindEnemy("hollow_oni");
            Assert.IsNotNull(ns);
            Assert.IsNotNull(oni);
            Assert.Greater(ns.attacks.Count, 0);
            Assert.Greater(oni.phase2Attacks.Count, 0, "Boss needs phase 2 attacks");
        }
    }
}
