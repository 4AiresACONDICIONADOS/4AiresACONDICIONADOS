using System.Collections.Generic;
using BreathOfEclipse.Combat;
using UnityEngine;
using static BreathOfEclipse.Data.PhaseBuilder;

namespace BreathOfEclipse.Data
{
    /// <summary>
    /// Default game content authored in code (source of truth for version <see cref="ContentVersion"/>).
    /// The editor exports it to editable .asset files; at runtime the exported GameDatabase takes precedence.
    /// </summary>
    public static class DefaultContent
    {
        public const int ContentVersion = 2;

        public static GameDatabase Build()
        {
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.name = "GameDatabase";
            db.contentVersion = ContentVersion;
            db.player = ScriptableObject.CreateInstance<PlayerData>();
            db.player.name = "PlayerTuning";
            db.playerWeapon = Weapon();
            db.playerCombos = Combos();
            db.styles = new List<BreathingStyleData> { Tidal(), Thunder(), Ember(), Gale(), Moonlight() };
            db.enemies = new List<EnemyData> { Nightspawn(), HollowOni() };
            db.audioLibrary = ScriptableObject.CreateInstance<AudioLibraryData>();
            db.audioLibrary.name = "AudioLibrary";
            return db;
        }

        // ================================================================== weapon & combos

        private static WeaponData Weapon()
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.name = "Weapon_EclipseKatana";
            w.trailGradient = WeaponData.DefaultTrailGradient();
            return w;
        }

        private static AttackData Attack(string id, string display, DamageCategory cat, float windup, float active, float recovery, float damage,
            HitReaction reaction = HitReaction.Light, float knockback = 1.2f, float forward = 0.8f, string motion = null)
        {
            var a = ScriptableObject.CreateInstance<AttackData>();
            a.name = "Attack_" + id;
            a.attackId = id;
            a.displayName = display;
            a.category = cat;
            a.windup = windup;
            a.active = active;
            a.recovery = recovery;
            a.damageMultiplier = damage;
            a.reaction = reaction;
            a.knockback = knockback;
            a.forwardDistance = forward;
            a.motionId = motion ?? id;
            a.poiseDamage = 8f + damage * 8f;
            bool heavy = cat != DamageCategory.Light;
            a.hitStop = cat == DamageCategory.Light ? 0.025f : cat == DamageCategory.Heavy ? 0.045f : 0.07f;
            a.cameraShake = heavy ? 0.28f : 0.1f;
            a.swingSfx = heavy ? "slash_heavy" : "slash";
            a.hitSfx = heavy ? "hit_heavy" : "hit";
            a.impactVfx = heavy ? "impact_heavy" : "impact_slash";
            a.canBreakObjects = heavy;
            a.SetStandardCancelWindows();
            return a;
        }

        private static ComboData Combos()
        {
            var L1 = Attack("L1", "Crescent Cut", DamageCategory.Light, 0.1f, 0.1f, 0.28f, 1f);
            var L2 = Attack("L2", "Returning Cut", DamageCategory.Light, 0.08f, 0.1f, 0.28f, 1.05f);
            var L3 = Attack("L3", "Rising Cut", DamageCategory.Light, 0.1f, 0.11f, 0.3f, 1.15f);
            var L4 = Attack("L4", "Eclipse Whirl", DamageCategory.Finisher, 0.14f, 0.22f, 0.38f, 1.9f, HitReaction.Knockback, 6f, 1.2f);
            L4.isFinisher = true;
            L4.arcRadius = 2.4f;
            L4.arcAngle = 360f;
            L4.maxHitsPerTarget = 2;
            L4.multiHitInterval = 0.1f;
            L4.cameraZoom = 0.85f;
            L4.fovPunch = -3f;
            L4.elementTrail = true;
            L4.speedLines = true;
            L4.cameraShake = 0.35f;
            L4.SetStandardCancelWindows(0.85f);

            var H1 = Attack("H1", "Heaven Cleave", DamageCategory.Heavy, 0.24f, 0.12f, 0.4f, 2f, HitReaction.Heavy, 3f, 1f);
            H1.elementTrail = true;
            H1.groundImpact = true;
            var H2 = Attack("H2", "Heaven Splitter", DamageCategory.Finisher, 0.26f, 0.2f, 0.46f, 2.8f, HitReaction.Knockdown, 7f, 1.8f);
            H2.isFinisher = true;
            H2.flashFrame = true;
            H2.groundImpact = true;
            H2.elementTrail = true;
            H2.speedLines = true;
            H2.cameraZoom = 0.8f;
            H2.cameraShake = 0.55f;
            H2.SetStandardCancelWindows(0.85f);

            var L2H = Attack("L2H", "Rising Launcher", DamageCategory.Heavy, 0.14f, 0.12f, 0.34f, 1.4f, HitReaction.Launch, 1f, 0.6f);
            L2H.launchHeight = 3.2f;
            L2H.targetAirHang = 1.1f;
            L2H.verticalVelocity = 12.5f;
            L2H.elementTrail = true;
            L2H.SetStandardCancelWindows(0.55f);
            var L1H = Attack("L1H", "Cross Cut", DamageCategory.Heavy, 0.18f, 0.12f, 0.36f, 1.7f, HitReaction.Heavy, 3.5f, 1.2f);
            L1H.elementTrail = true;
            var L1HH = Attack("L1HH", "Crescent Slam", DamageCategory.Finisher, 0.2f, 0.18f, 0.45f, 2.4f, HitReaction.Knockdown, 6f, 1.4f);
            L1HH.isFinisher = true;
            L1HH.groundImpact = true;
            L1HH.elementTrail = true;
            L1HH.speedLines = true;
            L1HH.SetStandardCancelWindows(0.85f);

            var dash = Attack("DashL", "Swift Lunge", DamageCategory.Heavy, 0.06f, 0.14f, 0.3f, 1.4f, HitReaction.Heavy, 3f, 4.5f);
            dash.speedLines = true;
            dash.arcRadius = 1.3f;
            dash.arcAngle = 70f;
            dash.elementTrail = true;

            var A1 = Attack("A1", "Sky Cut", DamageCategory.Light, 0.07f, 0.1f, 0.24f, 0.95f, HitReaction.Light, 0.8f, 0.4f);
            A1.airHang = true;
            A1.targetAirHang = 0.6f;
            var A2 = Attack("A2", "Sky Return", DamageCategory.Light, 0.07f, 0.1f, 0.24f, 1f, HitReaction.Light, 0.8f, 0.4f);
            A2.airHang = true;
            A2.targetAirHang = 0.6f;
            var A3 = Attack("A3", "Falling Wheel", DamageCategory.Finisher, 0.1f, 0.18f, 0.3f, 1.5f, HitReaction.Knockdown, 4f, 0.5f);
            A3.airHang = true;
            A3.isFinisher = true;
            A3.elementTrail = true;
            A3.SetStandardCancelWindows(0.8f);
            var AH = Attack("AH", "Falling Moon", DamageCategory.Heavy, 0.12f, 0.3f, 0.35f, 2.2f, HitReaction.Knockdown, 5f, 0.3f);
            AH.plunge = true;
            AH.elementTrail = true;

            var counter = Attack("PDCounter", "Phantom Counter", DamageCategory.Counter, 0.05f, 0.14f, 0.3f, 2.6f, HitReaction.Heavy, 4f, 3f);
            counter.flashFrame = true;
            counter.slowMoScale = 0.5f;
            counter.slowMoDuration = 0.2f;
            counter.speedLines = true;
            counter.elementTrail = true;
            var riposte = Attack("Riposte", "Riposte Break", DamageCategory.Counter, 0.08f, 0.14f, 0.36f, 3.2f, HitReaction.Knockdown, 6f, 1.2f);
            riposte.flashFrame = true;
            riposte.isFinisher = true;
            riposte.cameraZoom = 0.75f;
            riposte.elementTrail = true;

            var c = ScriptableObject.CreateInstance<ComboData>();
            c.name = "Combos_Player";
            c.nodes = new List<ComboData.Node>
            {
                new ComboData.Node { attack = L1, nextLight = L2, nextHeavy = L1H },
                new ComboData.Node { attack = L2, nextLight = L3, nextHeavy = L2H },
                new ComboData.Node { attack = L3, nextLight = L4 },
                new ComboData.Node { attack = L4 },
                new ComboData.Node { attack = L1H, nextHeavy = L1HH },
                new ComboData.Node { attack = L1HH },
                new ComboData.Node { attack = L2H },
                new ComboData.Node { attack = H1, nextHeavy = H2 },
                new ComboData.Node { attack = H2 },
                new ComboData.Node { attack = dash },
                new ComboData.Node { attack = A1, nextLight = A2 },
                new ComboData.Node { attack = A2, nextLight = A3 },
                new ComboData.Node { attack = A3 },
                new ComboData.Node { attack = AH },
                new ComboData.Node { attack = counter },
                new ComboData.Node { attack = riposte }
            };
            c.groundLight = L1;
            c.groundHeavy = H1;
            c.dashLight = dash;
            c.dashHeavy = H1;
            c.airLight = A1;
            c.airHeavy = AH;
            c.perfectDodgeCounter = counter;
            c.parryCounter = riposte;
            return c;
        }

        // ================================================================== helpers

        private static SkillData Skill(string id, string name, string form, string style, SkillTier tier, float stamina, float breath, float cooldown, string description)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.name = "Skill_" + id;
            s.skillId = id;
            s.displayName = name;
            s.formName = form;
            s.callout = $"{style} — {name.ToUpperInvariant()}";
            s.tier = tier;
            s.staminaCost = stamina;
            s.breathCost = breath;
            s.cooldown = cooldown;
            s.description = description;
            if (tier == SkillTier.Ultimate)
            {
                s.cinematic = true;
                s.lockInput = true;
                s.usableInAir = false;
            }
            return s;
        }

        private static readonly string[] EnglishOrdinals =
            { "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth", "Tenth", "Eleventh" };

        /// <summary>Registers a technique as form <paramref name="number"/> (I … XI) with its Spanish name call.</summary>
        private static BreathingForm Form(int number, SkillData skill, string techniqueCall)
        {
            skill.formName = (number >= 1 && number <= EnglishOrdinals.Length ? EnglishOrdinals[number - 1] : "Form " + number) + " Form";
            skill.voiceTechniqueCall = techniqueCall;
            return new BreathingForm { formNumber = number, skill = skill };
        }

        private static void Ultimate(BreathingStyleData style, SkillData ult, string techniqueCall)
        {
            ult.voiceFormCall = "Forma Final";
            ult.voiceTechniqueCall = techniqueCall;
            style.ultimate = ult;
        }

        private static BreathingStyleData Style(string id, string display, Element element, Color baseColor, Color secondary, Color glow,
            ElementTrailStyle trail, string sfx, string description, Gradient trailGradient)
        {
            var s = ScriptableObject.CreateInstance<BreathingStyleData>();
            s.name = "Style_" + display.Replace(" ", "");
            s.styleId = id;
            s.displayName = display;
            s.element = element;
            s.baseColor = baseColor;
            s.secondaryColor = secondary;
            s.glowColor = glow;
            s.trailStyle = trail;
            s.equipSfx = sfx;
            s.description = description;
            s.elementTrailGradient = trailGradient;
            return s;
        }

        private static Gradient Grad(Color a, Color b, Color c, float alphaStart = 0.9f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.35f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(alphaStart, 0f), new GradientAlphaKey(alphaStart * 0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ================================================================== TIDAL BREATH (water)

        private static BreathingStyleData Tidal()
        {
            const string S = "TIDAL BREATH";
            var style = Style("tidal", S, Element.Water, new Color(0.2f, 0.6f, 1f), new Color(0.85f, 0.95f, 1f), new Color(0.4f, 1.2f, 3f),
                ElementTrailStyle.Liquid, "water", "Balanced water style: flowing combos and good mobility.",
                Grad(new Color(0.9f, 0.97f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.05f, 0.25f, 0.8f)));
            style.passives.Add(new StylePassive { stat = StatType.MoveSpeed, magnitude = 0.05f });
            style.passives.Add(new StylePassive { stat = StatType.StaminaRegen, magnitude = 0.15f });

            // First Form — the reference technique.
            var serpent = Skill("tidal_rising_serpent", "Rising Serpent", "Seventh Form", S, SkillTier.Normal, 14f, 0f, 3.5f,
                "Low stance, water gathers on the blade, dash and rising diagonal cut: a serpent of water launches the enemy. Continue with an air combo.");
            serpent.endsAirborne = true;
            // Timeline (hero reference): anticipation 0.34 → charge 0.18 → dash 0.17 → slash 0.16 (hit + 0.08 hit stop)
            // → serpent on screen ~1.3 s while the player rises → aftermath 0.5, cancelable into the air combo.
            serpent.phases.Add(Phase("Low Stance", 0.34f, "SkillLowStance", 0.06f).Voice(VoiceCue.Style).Trail(TrailMode.SwordAndElement)
                .Vfx("water_charge", VFXAnchor.Sword, 0f, 0.7f, true).Vfx("water_ground_ripple", VFXAnchor.Ground)
                .Sfx("water", 0.05f, 0.6f)
                .Cam(zoom: 0.9f, slowScale: 0.75f, slowDuration: 0.3f, bloom: 0.2f));
            serpent.phases.Add(Phase("Charge", 0.18f, "SkillLowStance", 0.02f).Voice(VoiceCue.Form).Trail(TrailMode.SwordAndElement)
                .Vfx("water_charge", VFXAnchor.Sword, 0f, 1.25f, true).Vfx("water_ground_ripple", VFXAnchor.Ground, 0.02f, 0.7f)
                .Sfx("charge", 0f, 0.6f)
                .Cam(zoom: 0.86f, bloom: 0.35f, saturation: -10f));
            serpent.phases.Add(Phase("Serpent Dash", 0.17f, "SkillDash", 0.04f).Voice(VoiceCue.Name).Move(SkillMoveMode.DashToTarget, 7f).Afterimages()
                .Vfx("water_dash_wake", VFXAnchor.Self, 0f, 1f, true).Sfx("dash")
                .Cam(fovPunch: 6f, speedLines: true, radialBlur: true));
            serpent.phases.Add(Phase("Rising Cut", 0.16f, "SkillRisingCut", 0.03f)
                .Vfx("water_serpent", VFXAnchor.Self).Vfx("water_splash", VFXAnchor.Target, 0.06f, 1.2f)
                .Sfx("slash_heavy").Sfx("water_big", 0.03f)
                .Hit(Spec(HitShape.CapsuleForward, 2.6f, HitReaction.Launch, 1.5f, 2.8f, 0.05f, knockback: 1f, launch: 3.4f, airHang: 1.1f,
                    hitStop: 0.08f, shake: 0.45f, impact: "water_splash"))
                // Hero camera: pull back ~15% so player, serpent and enemy share the frame.
                .Cam(shake: 0.2f, fovPunch: 4f, zoom: 1.15f, chromatic: 0.3f, bloom: 0.6f));
            serpent.phases.Add(Phase("Ascend", 0.3f, "SkillAirReady").Move(SkillMoveMode.Rise, 0.8f, 2.8f).NoGravity()
                .Vfx("water_suspended", VFXAnchor.Target, 0.02f).Sfx("water", 0f, 0.4f)
                .Cam(zoom: 1.15f));
            serpent.phases.Add(Phase("Poise", 0.2f, "SkillHover").Move(SkillMoveMode.Hover).Cancelable());

            var tide = Skill("tidal_crescent_tide", "Crescent Tide", "Second Form", S, SkillTier.Normal, 18f, 0f, 5f,
                "A full spin that releases a ring-shaped wave, pushing every surrounding enemy away.");
            tide.phases.Add(Phase("Tide Spin", 0.5f, "SkillSpin").Trail(TrailMode.SwordAndElement)
                .Vfx("water_charge", VFXAnchor.Sword, 0f, 1f, true).Vfx("water_crescent_tide", VFXAnchor.Ground, 0.12f)
                .Sfx("slash_heavy", 0.1f).Sfx("water_big", 0.12f)
                .Hit(Spec(HitShape.SphereAroundSelf, 1.9f, HitReaction.Knockback, 3.6f, 0f, 0.2f, knockback: 7f, hitStop: 0.06f, impact: "water_splash"))
                .Cam(shake: 0.25f, zoom: 1.15f));
            tide.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var flow = Skill("tidal_flowing_current", "Wandering Current", "Third Form", S, SkillTier.Normal, 16f, 0f, 4.5f,
                "Weaving dash through the enemy line leaving liquid arcs, ending with a rising cut.");
            flow.phases.Add(Phase("Flow", 0.5f, "SkillDash").Move(SkillMoveMode.Weave, 9f).Afterimages().Trail(TrailMode.SwordAndElement)
                .Vfx("water_flow_step", VFXAnchor.Self).Vfx("water_flow_step", VFXAnchor.Self, 0.17f).Vfx("water_flow_step", VFXAnchor.Self, 0.34f)
                .Sfx("slash", 0.05f).Sfx("slash", 0.2f).Sfx("slash", 0.35f).Sfx("water", 0.1f)
                .Hit(Spec(HitShape.AlongPath, 0.9f, HitReaction.Light, 1.4f, 0f, 0.1f, 3, 0.16f, 1f, hitStop: 0.03f, impact: "spark_water", sfx: "hit"))
                .Cam(fovPunch: 5f, speedLines: true));
            flow.phases.Add(Phase("Finish", 0.26f, "L3").Vfx("water_splash", VFXAnchor.InFront, 0.1f, 1f, false, new Vector3(0f, 1f, 1.5f))
                .Sfx("water_big", 0.1f).Hit(Spec(HitShape.SphereInFront, 1.4f, HitReaction.Heavy, 1.6f, 1.6f, 0.1f, knockback: 4f)));
            flow.phases.Add(Phase("Recover", 0.2f).Cancelable());

            var fang = Skill("tidal_whirlpool_fang", "Abyss Fang", "Sixth Form", S, SkillTier.Advanced, 20f, 35f, 10f,
                "Advanced form. The body becomes the tip of a spiraling water drill that pulls enemies in and bursts.");
            fang.phases.Add(Phase("Coil", 0.3f, "SkillLowStance").Vfx("water_charge", VFXAnchor.Sword, 0f, 1.2f, true).Sfx("charge")
                .Cam(zoom: 0.85f, saturation: -20f));
            fang.phases.Add(Phase("Fang", 0.7f, "SkillThrust").Move(SkillMoveMode.DashForward, 6f).Invulnerable().Trail(TrailMode.SwordAndElement)
                .Vfx("water_whirlpool", VFXAnchor.Self).Sfx("water_big")
                .Hit(Spec(HitShape.CapsuleForward, 0.7f, HitReaction.Light, 1.8f, 5.5f, 0.05f, 7, 0.09f, 0.5f, hitStop: 0.02f, shake: 0.1f, impact: "spark_water", sfx: "hit").Pull(6f))
                .Hit(Spec(HitShape.SphereInFront, 2.2f, HitReaction.Knockback, 2.5f, 3f, 0.65f, knockback: 9f, impact: "water_splash").Finisher())
                .Cam(shake: 0.3f, zoom: 1.1f, speedLines: true, radialBlur: true));
            fang.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var ult = Skill("tidal_leviathan", "Leviathan's Requiem", "Final Form", S, SkillTier.Ultimate, 0f, 100f, 20f,
                "Ultimate. A colossal water dragon coils around the battlefield and crashes onto the enemy.");
            // ~4.6 s in three shots: summon (low hero angle) → the leviathan coils the whirlpool (wide) → dive and impact.
            ult.phases.Add(Phase("Summon", 1.2f, "SkillRaise").Voice(VoiceCue.Style).Invulnerable()
                .Vfx("water_charge", VFXAnchor.Sword, 0f, 1.6f, true).Vfx("water_ground_ripple", VFXAnchor.Ground, 0f, 2f)
                .Sfx("charge").Sfx("water_big", 0.3f)
                .Cam(saturation: -30f).Shot(CinematicShot.LowAngleHero, 5f, 1.2f));
            ult.phases.Add(Phase("Leviathan", 1.8f, "SkillFocus").Voice(VoiceCue.Form).Invulnerable()
                .Vfx("water_leviathan", VFXAnchor.TargetGround).Sfx("ultimate").Sfx("water_big", 0.4f)
                .Cam(shake: 0.2f).Shot(CinematicShot.WideArena, 8f, 3f));
            ult.phases.Add(Phase("Requiem", 1.0f, "SkillOverhead").Voice(VoiceCue.Name).Invulnerable()
                .Vfx("water_splash", VFXAnchor.Target, 0f, 3f).Vfx("water_splash", VFXAnchor.Target, 0.7f, 4f)
                .Vfx("ground_impact", VFXAnchor.TargetGround, 0.7f, 2.5f).Vfx("water_suspended", VFXAnchor.Target, 0.75f, 2.5f)
                .Sfx("water_big", 0.7f).Sfx("explosion", 0.7f, 0.7f)
                .Hit(Spec(HitShape.AtTarget, 1.2f, HitReaction.Light, 7f, 0f, 0.05f, 5, 0.12f, 1f, hitStop: 0.03f, impact: "spark_water", sfx: "hit"))
                .Hit(Spec(HitShape.AtTarget, 6f, HitReaction.Knockdown, 8f, 0f, 0.7f, knockback: 12f, hitStop: 0.1f, shake: 0.8f, impact: "water_splash").Finisher().Crit())
                .Cam(shake: 0.3f, bloom: 1.2f, chromatic: 0.5f).Shot(CinematicShot.OverShoulderTarget, 4f, 1.5f));
            ult.phases.Add(Phase("Stillness", 0.6f, "SkillFocus"));

            // First Form — quick and elegant: one clean horizontal cut that leaves an arc of water.
            var cutter = Skill("tidal_tide_cutter", "Tide Cutter", "First Form", S, SkillTier.Normal, 10f, 0f, 2.5f,
                "A single elegant horizontal cut delivered from a gliding step; the blade leaves a clean arc of water.");
            cutter.phases.Add(Phase("Tide Draw", 0.14f, "SkillLowStance", 0.05f).Trail(TrailMode.SwordAndElement)
                .Vfx("water_charge", VFXAnchor.Sword, 0f, 0.7f, true).Sfx("water", 0f, 0.45f));
            cutter.phases.Add(Phase("Tide Cut", 0.2f, "SkillIaiDraw", 0.03f).Move(SkillMoveMode.DashToTarget, 4.5f).Afterimages()
                .Trail(TrailMode.SwordAndElement)
                .Vfx("water_tide_cut", VFXAnchor.Self, 0.03f).Vfx("water_splash", VFXAnchor.Target, 0.08f, 0.8f)
                .Sfx("slash", 0.02f).Sfx("water", 0.06f, 0.7f)
                .Hit(Spec(HitShape.CapsuleForward, 1.9f, HitReaction.Heavy, 1.3f, 2.6f, 0.06f, knockback: 3f, hitStop: 0.06f, shake: 0.2f, impact: "spark_water"))
                .Cam(fovPunch: 3f));
            cutter.phases.Add(Phase("Settle", 0.2f, "SkillSheathe").Cancelable());

            // Fourth Form — a leap and a descending cut that falls like a split waterfall.
            var cascade = Skill("tidal_parting_cascade", "Parting Cascade", "Fourth Form", S, SkillTier.Normal, 16f, 0f, 5.5f,
                "Leap and fall on the enemy with a vertical cut; the water splits around the impact like a waterfall.");
            cascade.phases.Add(Phase("Rise", 0.3f, "SkillFlip", 0.05f).Move(SkillMoveMode.Leap, 3f, 2.6f).Trail(TrailMode.SwordAndElement)
                .Vfx("water_charge", VFXAnchor.Sword, 0f, 1f, true).Sfx("jump", 0f, 0.6f).Sfx("water", 0.05f, 0.6f)
                .Cam(zoom: 1.12f));
            cascade.phases.Add(Phase("Cascade", 0.2f, "SkillPlunge", 0.02f).Move(SkillMoveMode.Plunge).Trail(TrailMode.SwordAndElement)
                .Vfx("water_cascade", VFXAnchor.Self).Vfx("water_splash", VFXAnchor.Ground, 0.12f, 2f).Vfx("water_ground_ripple", VFXAnchor.Ground, 0.12f, 1.4f)
                .Vfx("ground_impact", VFXAnchor.Ground, 0.12f, 1.2f).Vfx("water_suspended", VFXAnchor.InFront, 0.14f, 1.4f, false, new Vector3(0f, 0.6f, 1.2f))
                .Sfx("slash_heavy").Sfx("water_big", 0.12f)
                .Hit(Spec(HitShape.SphereInFront, 3.2f, HitReaction.Knockdown, 2.4f, 1.4f, 0.12f, knockback: 5f, hitStop: 0.08f, shake: 0.5f, impact: "water_splash"))
                .Cam(shake: 0.3f, zoom: 0.9f, bloom: 0.4f));
            cascade.phases.Add(Phase("Recover", 0.3f, "SkillFocus").Cancelable());

            // Fifth Form — a standing vortex that drags enemies in, then erupts as a ring of tides.
            var ring = Skill("tidal_ring_of_tides", "Ring of Tides", "Fifth Form", S, SkillTier.Normal, 20f, 0f, 7f,
                "The current circles the swordsman and drags nearby enemies in before erupting outward as a ring of tides.");
            ring.phases.Add(Phase("Gather", 0.4f, "SkillWhirl", 0.05f).Trail(TrailMode.SwordAndElement)
                .Vfx("water_ring_current", VFXAnchor.Self).Sfx("water", 0f, 0.6f).Sfx("water_big", 0.2f, 0.5f)
                .Hit(Spec(HitShape.SphereAroundSelf, 0.5f, HitReaction.Light, 4.5f, 0f, 0.05f, 4, 0.09f, 0.3f, hitStop: 0.02f, shake: 0.1f, impact: "spark_water", sfx: "hit").Pull(5f))
                .Cam(zoom: 1.15f, saturation: -10f));
            ring.phases.Add(Phase("Tide Ring", 0.3f, "SkillSpinLong", 0.03f).Trail(TrailMode.SwordAndElement)
                .Vfx("water_crescent_tide", VFXAnchor.Ground, 0f, 1.6f).Vfx("water_ground_ripple", VFXAnchor.Ground, 0f, 2f)
                .Sfx("slash_heavy").Sfx("water_big", 0.05f)
                .Hit(Spec(HitShape.SphereAroundSelf, 3f, HitReaction.Knockback, 4.2f, 0f, 0.06f, knockback: 8f, hitStop: 0.07f, shake: 0.4f, impact: "water_splash"))
                .Cam(shake: 0.3f, zoom: 1.2f, bloom: 0.3f));
            ring.phases.Add(Phase("Recover", 0.25f).Cancelable());

            style.styleCall = "Respiración del Agua";
            style.forms = new List<BreathingForm>
            {
                Form(1, cutter, "Corte de Marea"),
                Form(2, tide, "Marea Creciente"),
                Form(3, flow, "Corriente Errante"),
                Form(4, cascade, "Cascada Partida"),
                Form(5, ring, "Anillo de Mareas"),
                Form(6, fang, "Colmillo del Abismo"),
                Form(7, serpent, "Serpiente Ascendente")
            };
            // Keys 1-4 keep the v0.1 layout: Rising Serpent, Crescent Tide, Wandering Current, Abyss Fang.
            style.quickSlots = new[] { 6, 1, 2, 5 };
            Ultimate(style, ult, "Réquiem del Leviatán");
            return style;
        }

        // ================================================================== THUNDER BREATH

        private static BreathingStyleData Thunder()
        {
            const string S = "THUNDER BREATH";
            var style = Style("thunder", S, Element.Thunder, new Color(1f, 0.82f, 0.18f), new Color(1f, 1f, 0.8f), new Color(4f, 3.4f, 1.2f),
                ElementTrailStyle.Lightning, "thunder", "Extreme speed: flash steps, dash attacks and critical damage.",
                Grad(new Color(1f, 1f, 0.9f), new Color(1f, 0.85f, 0.2f), new Color(1f, 0.45f, 0.05f)));
            style.passives.Add(new StylePassive { stat = StatType.CritChance, magnitude = 0.08f });
            style.passives.Add(new StylePassive { stat = StatType.MoveSpeed, magnitude = 0.08f });

            var flash = Skill("thunder_flash_breaker", "Flash Breaker", "First Form", S, SkillTier.Normal, 16f, 0f, 4.5f,
                "The blade returns to the sheath, lightning gathers, then one blinding step behind the enemy. The cut appears a moment later.");
            flash.phases.Add(Phase("Sheathe", 0.35f, "SkillSheathe", 0.06f).Trail(TrailMode.Off)
                .Vfx("thunder_charge", VFXAnchor.Self, 0f, 1f, true).Sfx("charge").Sfx("thunder", 0.1f, 0.4f)
                .Cam(zoom: 0.88f, slowScale: 0.75f, slowDuration: 0.3f, saturation: -55f, bloom: 0.4f));
            flash.phases.Add(Phase("Flash", 0.06f, "SkillIaiDraw", 0.01f).MotionSpan(0.2f).Move(SkillMoveMode.TeleportBehindTarget, 7f).Trail(TrailMode.Sword)
                .Sfx("thunder").Sfx("dash", 0f, 0.8f)
                .Cam(fovPunch: 10f, chromatic: 0.6f, lens: -0.4f, speedLines: true));
            flash.phases.Add(Phase("Dramatic Pause", 0.12f).Cam(slowScale: 0.35f, slowDuration: 0.12f));
            flash.phases.Add(Phase("The Cut", 0.12f)
                .Vfx("thunder_explosion", VFXAnchor.Target, 0.02f).Sfx("thunder_big")
                .Hit(Spec(HitShape.AtTarget, 3.2f, HitReaction.Stun, 1.8f, 0f, 0f, knockback: 2f, hitStop: 0.08f, shake: 0.5f, impact: "thunder_cut", sfx: "thunder_big").Crit().Status(1.2f).Finisher())
                .Cam(bloom: 1f));
            flash.phases.Add(Phase("Resheathe", 0.3f, "SkillSheathe").Cancelable());

            var chain = Skill("thunder_chain_spark", "Chain Spark", "Second Form", S, SkillTier.Normal, 14f, 0f, 5f,
                "A lightning thrust whose discharge jumps between up to five enemies, stunning them.");
            chain.phases.Add(Phase("Spark Thrust", 0.42f, "SkillThrust").Vfx("thunder_charge", VFXAnchor.Self, 0f, 0.8f, true)
                .Sfx("thunder_big", 0.22f)
                .Hit(Spec(HitShape.ChainLightning, 1.3f, HitReaction.Stun, 9f, 3f, 0.22f, knockback: 1f, hitStop: 0.05f, impact: "spark_thunder", sfx: "thunder").Targets(5).Status(0.8f))
                .Cam(chromatic: 0.3f));
            chain.phases.Add(Phase("Recover", 0.22f).Cancelable());

            var rolling = Skill("thunder_rolling_thunder", "Rolling Thunder", "Third Form", S, SkillTier.Normal, 18f, 0f, 6f,
                "Three flash steps through nearby enemies. Critical chance rises for a few seconds.");
            rolling.phases.Add(Phase("Crouch", 0.15f, "SkillSheathe").Vfx("thunder_charge", VFXAnchor.Self, 0f, 0.8f, true).Sfx("charge"));
            rolling.phases.Add(Phase("Rolling", 0.55f, "SkillIaiDraw").Behaviour("blink_chain").Afterimages().Trail(TrailMode.SwordAndElement)
                .Hit(Spec(HitShape.AtTarget, 1.2f, HitReaction.Heavy, 1.8f, 0f, 0f, 3, 0.15f, 3f, hitStop: 0.05f, impact: "spark_thunder", sfx: "thunder"))
                .Buff(StatType.CritChance, 0.15f, 6f)
                .Cam(fovPunch: 8f, speedLines: true, radialBlur: true));
            rolling.phases.Add(Phase("Recover", 0.25f, "SkillSheathe").Cancelable());

            var drum = Skill("thunder_heavenly_drum", "Heavenly Drum", "Fifth Form", S, SkillTier.Advanced, 20f, 35f, 11f,
                "Advanced form. Leap and call lightning down on every enemy around the target.");
            drum.phases.Add(Phase("Ascend", 0.35f, "SkillRaise").Move(SkillMoveMode.Leap, 1f, 2.2f)
                .Vfx("thunder_charge", VFXAnchor.Self, 0f, 1f, true).Sfx("thunder").Cam(zoom: 1.2f));
            drum.phases.Add(Phase("Drumroll", 0.9f, "SkillHover").Move(SkillMoveMode.Hover).Invulnerable()
                .Hit(Spec(HitShape.ScatterAroundTarget, 1.8f, HitReaction.Knockdown, 6f, 4f, 0.05f, knockback: 4f, impact: "thunder_strike", sfx: "thunder_big").Targets(6))
                .Cam(shake: 0.4f, bloom: 0.8f));
            drum.phases.Add(Phase("Land", 0.3f, "SkillPlunge").Move(SkillMoveMode.Plunge).Vfx("ground_impact", VFXAnchor.Ground, 0.2f).Cancelable());

            var ult = Skill("thunder_thousand_flashes", "Thousand Flashes", "Final Form", S, SkillTier.Ultimate, 0f, 100f, 20f,
                "Ultimate. Time stops for everyone but you: a storm of flash steps, then every cut appears at once.");
            ult.phases.Add(Phase("Focus", 0.8f, "SkillSheathe").Invulnerable()
                .Vfx("thunder_charge", VFXAnchor.Self, 0f, 1.5f, true).Sfx("charge").Sfx("thunder", 0.4f)
                .Cam(saturation: -70f).Shot(CinematicShot.CloseUpFace, 2f, 1f));
            ult.phases.Add(Phase("Thousand Flashes", 1.4f, "SkillIaiDraw").Invulnerable().Behaviour("blink_chain").Afterimages()
                .Hit(Spec(HitShape.AtTarget, 1.1f, HitReaction.Light, 2f, 0f, 0f, 8, 0.15f, 0.5f, hitStop: 0.03f, impact: "spark_thunder", sfx: "thunder"))
                .Cam(chromatic: 0.5f, speedLines: true).Shot(CinematicShot.WideArena, 7f, 2.5f));
            ult.phases.Add(Phase("Stillness", 0.7f, "SkillSheathe").Invulnerable()
                .Cam(saturation: -90f, slowScale: 0.5f, slowDuration: 0.5f).Shot(CinematicShot.SideProfile, 4f, 1.2f));
            ult.phases.Add(Phase("Thunderclap", 0.8f).Invulnerable()
                .Vfx("thunder_web", VFXAnchor.Self).Vfx("thunder_explosion", VFXAnchor.Target, 0f, 2f)
                .Sfx("thunder_big").Sfx("ultimate")
                .Hit(Spec(HitShape.SphereAroundSelf, 5f, HitReaction.Knockdown, 12f, 0f, 0.05f, knockback: 10f, hitStop: 0.1f, shake: 1f, impact: "thunder_explosion", sfx: "thunder_big").Finisher().Crit())
                .Cam(bloom: 1.5f).Shot(CinematicShot.WideArena, 9f, 3f));

            style.styleCall = "Respiración del Trueno";
            style.forms = new List<BreathingForm>
            {
                Form(1, flash, "Destello Quebrantador"),
                Form(2, chain, "Chispa Encadenada"),
                Form(3, rolling, "Trueno Rodante"),
                Form(5, drum, "Tambor Celestial")
            };
            Ultimate(style, ult, "Mil Destellos");
            return style;
        }

        // ================================================================== EMBER BREATH

        private static BreathingStyleData Ember()
        {
            const string S = "EMBER BREATH";
            var style = Style("ember", S, Element.Fire, new Color(1f, 0.42f, 0.1f), new Color(1f, 0.85f, 0.4f), new Color(4f, 1.5f, 0.3f),
                ElementTrailStyle.Flame, "fire", "Heavy strikes, directional explosions and burns. High damage, heavier stamina use.",
                Grad(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.5f, 0.1f), new Color(0.6f, 0.05f, 0.02f)));
            style.passives.Add(new StylePassive { stat = StatType.Damage, magnitude = 0.12f });
            style.passives.Add(new StylePassive { stat = StatType.StaminaRegen, magnitude = -0.1f });
            style.staminaCostMultiplier = 1.15f;

            var arc = Skill("ember_blazing_arc", "Blazing Arc", "First Form", S, SkillTier.Normal, 18f, 0f, 4.5f,
                "An overhead cut that draws a tall arc of fire and explodes forward, burning enemies.");
            arc.phases.Add(Phase("Rising Flame", 0.3f, "SkillOverhead").MotionSpan(0.55f).Trail(TrailMode.SwordAndElement)
                .Vfx("fire_charge", VFXAnchor.Sword, 0f, 1f, true).Sfx("fire").Cam(zoom: 0.9f));
            arc.phases.Add(Phase("Blazing Arc", 0.25f)
                .Vfx("fire_arc", VFXAnchor.Self).Vfx("fire_explosion", VFXAnchor.InFront, 0.03f, 1f, false, new Vector3(0f, 0f, 2f))
                .Vfx("ground_impact", VFXAnchor.InFront, 0.03f, 1f, false, new Vector3(0f, 0f, 2f))
                .Sfx("slash_heavy").Sfx("fire_big", 0.03f)
                .Hit(Spec(HitShape.CapsuleForward, 3f, HitReaction.Knockdown, 1.5f, 3.5f, 0.02f, knockback: 6f, hitStop: 0.07f, shake: 0.4f, impact: "impact_heavy").Status(3f))
                .Cam(shake: 0.25f, bloom: 0.7f, chromatic: 0.2f));
            arc.phases.Add(Phase("Recover", 0.3f).Cancelable());

            var wheel = Skill("ember_cinder_wheel", "Cinder Wheel", "Second Form", S, SkillTier.Normal, 16f, 0f, 5f,
                "A flipping slash that sends a rolling wheel of fire along the ground.");
            wheel.phases.Add(Phase("Somersault", 0.45f, "SkillFlip").Move(SkillMoveMode.Leap, 2f, 1.2f).Trail(TrailMode.SwordAndElement)
                .Vfx("fire_charge", VFXAnchor.Sword, 0f, 1f, true).Sfx("fire").Sfx("slash_heavy", 0.35f)
                .Projectile(new ProjectileSpec { vfxId = "fire_wheel", delay = 0.4f, speed = 14f, lifetime = 1.3f, radius = 1.1f, pierce = true,
                    damageMultiplier = 1.6f, reaction = HitReaction.Knockback, knockback = 5f, impactVfx = "impact_heavy" })
                .Cam(fovPunch: 5f));
            wheel.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var burst = Skill("ember_kindling_burst", "Kindling Burst", "Third Form", S, SkillTier.Normal, 15f, 0f, 4f,
                "A thrust that detonates a cone of flame, blasting enemies back.");
            burst.phases.Add(Phase("Kindling Thrust", 0.4f, "SkillThrust").Move(SkillMoveMode.DashForward, 2.5f).Trail(TrailMode.SwordAndElement)
                .Vfx("fire_cone", VFXAnchor.Self, 0.2f).Sfx("fire_big", 0.2f)
                .Hit(Spec(HitShape.Cone, 2.2f, HitReaction.Knockback, 2f, 6f, 0.22f, knockback: 9f, shake: 0.35f, impact: "impact_heavy", angle: 50f).Status(2.5f)));
            burst.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var phoenix = Skill("ember_phoenix_ascent", "Phoenix Ascent", "Sixth Form", S, SkillTier.Advanced, 20f, 35f, 10f,
                "Advanced form. A rising spiral of flame with phoenix wings launches everything around you.");
            phoenix.endsAirborne = true;
            phoenix.phases.Add(Phase("Ignite", 0.2f, "SkillLowStance").Vfx("fire_charge", VFXAnchor.Sword, 0f, 1f, true).Sfx("charge"));
            phoenix.phases.Add(Phase("Ascent", 0.55f, "SkillRisingCut").Move(SkillMoveMode.Rise, 1f, 3.5f).Invulnerable().Trail(TrailMode.SwordAndElement)
                .Vfx("fire_phoenix", VFXAnchor.Self).Sfx("fire_big")
                .Hit(Spec(HitShape.SphereAroundSelf, 1f, HitReaction.Launch, 2.8f, 0f, 0.05f, 4, 0.12f, 1f, 3.8f, 1.2f, 0.04f, 0.2f, "spark_fire").Status(3f))
                .Cam(speedLines: true, bloom: 0.8f));
            phoenix.phases.Add(Phase("Wings", 0.3f, "SkillHover").Move(SkillMoveMode.Hover)
                .Hit(Spec(HitShape.SphereAroundSelf, 2.4f, HitReaction.Knockback, 3.5f, 0f, 0.05f, knockback: 8f, impact: "fire_explosion").Finisher())
                .Cancelable());

            var ult = Skill("ember_solar_cataclysm", "Solar Cataclysm", "Final Form", S, SkillTier.Ultimate, 0f, 100f, 20f,
                "Ultimate. A small sun forms above the blade and is brought down as a pillar of fire.");
            ult.phases.Add(Phase("Gathering Sun", 1.4f, "SkillRaise").Invulnerable()
                .Vfx("fire_sun", VFXAnchor.Self).Vfx("fire_charge", VFXAnchor.Sword, 0f, 1.5f, true)
                .Sfx("charge").Sfx("fire_big", 0.8f)
                .Cam(bloom: 1f, saturation: -10f).Shot(CinematicShot.SkyLookUp, 5f, 1.5f));
            ult.phases.Add(Phase("Descent", 0.5f, "SkillOverhead").MotionSpan(0.6f).Invulnerable().Move(SkillMoveMode.Leap, 5f, 1.5f)
                .Cam(speedLines: true).Shot(CinematicShot.FollowBehind, 5f, 2f));
            ult.phases.Add(Phase("Cataclysm", 1f).Invulnerable()
                .Vfx("fire_column", VFXAnchor.TargetGround, 0f, 1.4f).Vfx("fire_explosion", VFXAnchor.Target, 0f, 2f).Vfx("ground_impact", VFXAnchor.TargetGround, 0f, 2.5f)
                .Sfx("explosion").Sfx("ultimate")
                .Hit(Spec(HitShape.AtTarget, 7f, HitReaction.Knockdown, 9f, 0f, 0.05f, knockback: 12f, hitStop: 0.1f, shake: 1f, impact: "fire_explosion").Finisher().Crit().Status(4f))
                .Hit(Spec(HitShape.AtTarget, 0.6f, HitReaction.Light, 9f, 0f, 0.2f, 5, 0.12f, 0.5f, hitStop: 0.02f, impact: "spark_fire", sfx: "hit"))
                .Cam(shake: 0.5f, bloom: 1.5f, chromatic: 0.5f).Shot(CinematicShot.WideArena, 9f, 3f));
            ult.phases.Add(Phase("Embers", 0.5f, "SkillFocus"));

            style.styleCall = "Respiración de las Brasas";
            style.forms = new List<BreathingForm>
            {
                Form(1, arc, "Arco Abrasador"),
                Form(2, wheel, "Rueda de Cenizas"),
                Form(3, burst, "Estallido de Brasas"),
                Form(6, phoenix, "Ascenso del Fénix")
            };
            Ultimate(style, ult, "Cataclismo Solar");
            return style;
        }

        // ================================================================== GALE BREATH

        private static BreathingStyleData Gale()
        {
            const string S = "GALE BREATH";
            var style = Style("gale", S, Element.Wind, new Color(0.45f, 0.95f, 0.7f), new Color(0.9f, 1f, 0.95f), new Color(1.3f, 2.6f, 2f),
                ElementTrailStyle.Wind, "wind", "Mobility, area control and air blades.",
                Grad(new Color(0.95f, 1f, 0.98f), new Color(0.55f, 1f, 0.8f), new Color(0.15f, 0.55f, 0.4f), 0.7f));
            style.passives.Add(new StylePassive { stat = StatType.MoveSpeed, magnitude = 0.15f });
            style.passives.Add(new StylePassive { stat = StatType.StaminaRegen, magnitude = 0.2f });

            var rend = Skill("gale_sky_rend", "Sky Rend", "First Form", S, SkillTier.Normal, 12f, 0f, 3.5f,
                "Throws a flying crescent of compressed air that pierces through enemies.");
            rend.phases.Add(Phase("Rend", 0.35f, "SkillThrow").Trail(TrailMode.SwordAndElement).Sfx("wind", 0.18f).Sfx("slash", 0.2f)
                .Projectile(new ProjectileSpec { vfxId = "wind_crescent", delay = 0.21f, speed = 22f, lifetime = 1.1f, radius = 1.2f, pierce = true,
                    damageMultiplier = 1.8f, reaction = HitReaction.Heavy, knockback = 3f, impactVfx = "spark_wind" }));
            rend.phases.Add(Phase("Recover", 0.2f).Cancelable());

            var cyclone = Skill("gale_cyclone_dance", "Cyclone Dance", "Second Form", S, SkillTier.Normal, 18f, 0f, 6f,
                "Spinning dance inside a cyclone that drags enemies in, then tosses them up.");
            cyclone.phases.Add(Phase("Cyclone", 0.9f, "SkillSpinLong").Trail(TrailMode.SwordAndElement)
                .Vfx("wind_cyclone", VFXAnchor.Self, 0f, 1f, true).Sfx("wind_big")
                .Hit(Spec(HitShape.SphereAroundSelf, 0.55f, HitReaction.Light, 3.8f, 0f, 0.05f, 7, 0.12f, 0.2f, hitStop: 0.02f, shake: 0.1f, impact: "spark_wind", sfx: "hit").Pull(7f))
                .Hit(Spec(HitShape.SphereAroundSelf, 1.6f, HitReaction.Launch, 4.2f, 0f, 0.85f, knockback: 1f, launch: 2.2f, airHang: 0.8f))
                .Cam(shake: 0.2f, zoom: 1.15f));
            cyclone.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var step = Skill("gale_tailwind_step", "Tailwind Step", "Third Form", S, SkillTier.Normal, 10f, 0f, 5f,
                "A gust carries you through the enemy. Movement speed rises for a few seconds.");
            step.phases.Add(Phase("Step", 0.22f, "SkillDash").Move(SkillMoveMode.DashForward, 7f).Invulnerable().Afterimages().Trail(TrailMode.SwordAndElement)
                .Vfx("wind_step", VFXAnchor.Self).Sfx("wind")
                .Hit(Spec(HitShape.AlongPath, 1.2f, HitReaction.Heavy, 1.3f, 0f, 0.18f, knockback: 3f, impact: "spark_wind"))
                .Buff(StatType.MoveSpeed, 0.3f, 5f)
                .Cam(fovPunch: 7f, speedLines: true));
            step.phases.Add(Phase("Glide", 0.15f).Cancelable());

            var pillar = Skill("gale_tempest_pillar", "Tempest Pillar", "Fifth Form", S, SkillTier.Advanced, 20f, 35f, 11f,
                "Advanced form. A tornado erupts at the target, lifting and shredding everything inside.");
            pillar.phases.Add(Phase("Call Storm", 0.35f, "SkillWhirl").Vfx("wind_step", VFXAnchor.Self).Sfx("wind"));
            pillar.phases.Add(Phase("Pillar", 1.1f, "SkillWhirl")
                .Vfx("wind_tornado", VFXAnchor.TargetGround).Sfx("wind_big")
                .Hit(Spec(HitShape.AtTarget, 0.6f, HitReaction.Launch, 3.2f, 0f, 0.1f, 8, 0.12f, 0.2f, 1.2f, 0.6f, 0.02f, 0.1f, "spark_wind", "hit").Pull(5f))
                .Hit(Spec(HitShape.AtTarget, 2f, HitReaction.Knockback, 3.8f, 0f, 1f, knockback: 8f)));
            pillar.phases.Add(Phase("Recover", 0.2f).Cancelable());

            var ult = Skill("gale_heavens_gale", "Heaven's Gale", "Final Form", S, SkillTier.Ultimate, 0f, 100f, 20f,
                "Ultimate. Ride a giant tornado into the sky while air blades rain down, then dive.");
            ult.phases.Add(Phase("Ascend", 0.8f, "SkillSpinLong").Invulnerable().Move(SkillMoveMode.Rise, 0f, 4f)
                .Vfx("wind_cyclone", VFXAnchor.Self, 0f, 1.5f, true).Sfx("wind_big")
                .Shot(CinematicShot.LowAngleHero, 6f, 0.5f));
            ult.phases.Add(Phase("Storm Eye", 1.6f, "SkillHover").Invulnerable().Move(SkillMoveMode.Hover)
                .Vfx("wind_blade_rain", VFXAnchor.TargetGround).Sfx("ultimate").Sfx("wind_big", 0.5f)
                .Hit(Spec(HitShape.ScatterAroundTarget, 1.2f, HitReaction.Heavy, 10f, 5f, 0.1f, knockback: 2f, impact: "wind_slash", sfx: "hit").Targets(8))
                .Hit(Spec(HitShape.AtTarget, 0.4f, HitReaction.Light, 10f, 0f, 0.2f, 8, 0.18f, 0.2f, hitStop: 0.015f, shake: 0.05f, impact: "spark_wind", sfx: "hit"))
                .Cam(speedLines: true).Shot(CinematicShot.WideArena, 10f, 4f));
            ult.phases.Add(Phase("Descent", 0.6f, "SkillPlunge").Invulnerable().Move(SkillMoveMode.Plunge)
                .Vfx("ground_impact", VFXAnchor.Ground, 0.45f, 2.5f).Vfx("shockwave", VFXAnchor.Ground, 0.45f, 2f)
                .Hit(Spec(HitShape.SphereAroundSelf, 5f, HitReaction.Knockdown, 7f, 0f, 0.45f, knockback: 12f, hitStop: 0.1f, shake: 0.9f, impact: "impact_crit").Finisher().Crit())
                .Shot(CinematicShot.FollowBehind, 5f, 2.5f));

            style.styleCall = "Respiración del Vendaval";
            style.forms = new List<BreathingForm>
            {
                Form(1, rend, "Desgarro del Cielo"),
                Form(2, cyclone, "Danza del Ciclón"),
                Form(3, step, "Paso del Céfiro"),
                Form(5, pillar, "Pilar de Tempestad")
            };
            Ultimate(style, ult, "Vendaval Celestial");
            return style;
        }

        // ================================================================== MOONLIGHT BREATH

        private static BreathingStyleData Moonlight()
        {
            const string S = "MOONLIGHT BREATH";
            var style = Style("moonlight", S, Element.Moon, new Color(0.62f, 0.45f, 1f), new Color(0.85f, 0.9f, 1f), new Color(1.6f, 1.1f, 3.8f),
                ElementTrailStyle.Lunar, "moon", "Advanced mid-range techniques: crescents that linger and cut again.",
                Grad(new Color(0.95f, 0.95f, 1f), new Color(0.65f, 0.5f, 1f), new Color(0.25f, 0.2f, 0.8f)));
            style.passives.Add(new StylePassive { stat = StatType.CritChance, magnitude = 0.05f });
            style.passives.Add(new StylePassive { stat = StatType.Damage, magnitude = 0.06f });
            style.passives.Add(new StylePassive { stat = StatType.BreathGain, magnitude = 0.2f });

            var veil = Skill("moon_crescent_veil", "Crescent Veil", "First Form", S, SkillTier.Normal, 14f, 0f, 4f,
                "A wide slash that leaves glowing crescents hanging in the air; they keep cutting.");
            veil.phases.Add(Phase("Veil", 0.4f, "SkillThrow").Trail(TrailMode.SwordAndElement)
                .Vfx("moon_crescent", VFXAnchor.InFront, 0.18f, 1f, false, new Vector3(0f, 0f, 0.8f))
                .Vfx("moon_crescent", VFXAnchor.InFront, 0.24f, 0.8f, false, new Vector3(0f, 0.5f, 1.2f), new Vector3(0f, 0f, 25f))
                .Sfx("slash", 0.18f).Sfx("moon", 0.2f)
                .Hit(Spec(HitShape.Cone, 1.5f, HitReaction.Heavy, 2f, 3.5f, 0.2f, knockback: 2f, impact: "spark_moon", angle: 150f))
                .Hit(Spec(HitShape.SphereInFront, 0.6f, HitReaction.Light, 2f, 1.8f, 0.3f, 3, 0.22f, 0.5f, hitStop: 0.02f, shake: 0.08f, impact: "spark_moon", sfx: "hit").Detached()));
            veil.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var echo = Skill("moon_waning_echo", "Waning Echo", "Second Form", S, SkillTier.Normal, 15f, 0f, 5f,
                "A drawing cut; its echoes appear one after another further ahead.");
            echo.phases.Add(Phase("Echo Draw", 0.4f, "SkillIaiDraw").Trail(TrailMode.SwordAndElement)
                .Vfx("moon_echo_line", VFXAnchor.Self, 0.15f).Sfx("moon", 0.15f).Sfx("moon", 0.35f)
                .Hit(Spec(HitShape.SphereInFront, 1.2f, HitReaction.Heavy, 1.8f, 2.5f, 0.15f, knockback: 2f, impact: "moon_echo").Detached())
                .Hit(Spec(HitShape.SphereInFront, 1.2f, HitReaction.Heavy, 1.8f, 4.5f, 0.35f, knockback: 2f, impact: "spark_moon").Detached())
                .Hit(Spec(HitShape.SphereInFront, 1.4f, HitReaction.Knockback, 1.8f, 6.5f, 0.55f, knockback: 5f, impact: "spark_moon").Detached()));
            echo.phases.Add(Phase("Recover", 0.3f).Cancelable());

            var halo = Skill("moon_lunar_halo", "Lunar Halo", "Third Form", S, SkillTier.Normal, 16f, 0f, 6f,
                "Crescent moons orbit around you, shredding anyone close.");
            halo.phases.Add(Phase("Halo", 0.6f, "SkillSpin").Invulnerable().Trail(TrailMode.SwordAndElement)
                .Vfx("moon_halo", VFXAnchor.Self, 0f, 1f, true).Sfx("moon").Sfx("moon_big", 0.1f, 0.6f)
                .Hit(Spec(HitShape.SphereAroundSelf, 0.7f, HitReaction.Light, 3f, 0f, 0.05f, 4, 0.14f, 1.5f, hitStop: 0.03f, impact: "spark_moon", sfx: "hit")));
            halo.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var tide = Skill("moon_eclipse_tide", "Eclipse Tide", "Seventh Form", S, SkillTier.Advanced, 20f, 35f, 10f,
                "Advanced form. A fan of five lunar waves sweeps the field.");
            tide.phases.Add(Phase("Gather", 0.3f, "SkillFocus").Vfx("moon_sparkle", VFXAnchor.Sword, 0f, 1f, true).Sfx("charge")
                .Cam(zoom: 0.9f, saturation: -30f));
            tide.phases.Add(Phase("Tide", 0.45f, "SkillThrow").Trail(TrailMode.SwordAndElement).Sfx("moon_big", 0.2f)
                .Projectile(new ProjectileSpec { vfxId = "moon_wave", delay = 0.2f, count = 5, spreadAngle = 60f, speed = 18f, lifetime = 1.2f, radius = 1f, pierce = true,
                    damageMultiplier = 1.4f, reaction = HitReaction.Heavy, knockback = 3f, impactVfx = "moon_echo" })
                .Cam(fovPunch: 6f));
            tide.phases.Add(Phase("Recover", 0.25f).Cancelable());

            var ult = Skill("moon_eternal_eclipse", "Eternal Eclipse", "Final Form", S, SkillTier.Ultimate, 0f, 100f, 20f,
                "Ultimate. An eclipsed moon rises; countless crescents converge, then one final cut.");
            ult.phases.Add(Phase("Moonrise", 1.6f, "SkillRaise").Invulnerable()
                .Vfx("moon_eclipse", VFXAnchor.Self).Sfx("moon_big").Sfx("ultimate", 1f)
                .Cam(saturation: -40f, bloom: 0.6f).Shot(CinematicShot.SkyLookUp, 6f, 2f));
            ult.phases.Add(Phase("Eclipse", 1.2f, "SkillFocus").Invulnerable()
                .Vfx("moon_halo", VFXAnchor.Target, 0f, 2f)
                .Hit(Spec(HitShape.AtTarget, 0.9f, HitReaction.Light, 8f, 0f, 0.1f, 6, 0.16f, 0.3f, hitStop: 0.02f, impact: "moon_echo", sfx: "hit"))
                .Cam(chromatic: 0.3f).Shot(CinematicShot.OrbitSlow, 7f, 2f));
            ult.phases.Add(Phase("Eternal", 0.9f, "SkillIaiDraw").Invulnerable()
                .Vfx("moon_crescent", VFXAnchor.Target, 0.25f, 3f).Vfx("moon_echo", VFXAnchor.Target, 0.3f, 3f).Vfx("shockwave", VFXAnchor.TargetGround, 0.3f, 2f)
                .Sfx("moon_big", 0.3f)
                .Hit(Spec(HitShape.AtTarget, 6f, HitReaction.Knockdown, 9f, 0f, 0.3f, knockback: 10f, hitStop: 0.1f, shake: 0.8f, impact: "impact_crit").Finisher().Crit())
                .Shot(CinematicShot.OverShoulderTarget, 4f, 1.5f));
            ult.phases.Add(Phase("Afterglow", 0.4f, "SkillFocus"));

            style.styleCall = "Respiración Lunar";
            style.forms = new List<BreathingForm>
            {
                Form(1, veil, "Velo Creciente"),
                Form(2, echo, "Eco Menguante"),
                Form(3, halo, "Halo Lunar"),
                Form(7, tide, "Marea del Eclipse")
            };
            Ultimate(style, ult, "Eclipse Eterno");
            return style;
        }

        // ================================================================== enemies

        private static EnemyAttackData EAttack(string id, string name, string motion, float minRange, float maxRange, float weight, float cooldown,
            float windup, float active, float recovery, float damage, HitReaction reaction, float knockback, float reach, float angle,
            EnemyAttackMovement movement = EnemyAttackMovement.Lunge, float move = 1f)
        {
            return new EnemyAttackData
            {
                attackId = id,
                displayName = name,
                motionId = motion,
                minRange = minRange,
                maxRange = maxRange,
                weight = weight,
                cooldown = cooldown,
                windup = windup,
                active = active,
                recovery = recovery,
                damageMultiplier = damage,
                reaction = reaction,
                knockback = knockback,
                reach = reach,
                angle = angle,
                movement = movement,
                moveDistance = move
            };
        }

        private static EnemyData Nightspawn()
        {
            var e = ScriptableObject.CreateInstance<EnemyData>();
            e.name = "Enemy_Nightspawn";
            e.enemyId = "nightspawn";
            e.voicePrefix = "nightspawn";
            e.displayName = "Nightspawn";
            e.archetype = EnemyArchetype.Nightspawn;
            e.maxHealth = 150f;
            e.poise = 28f;
            e.weakness = Element.Fire;
            e.resistance = Element.Dark;
            e.baseDamage = 14f;
            e.walkSpeed = 2.4f;
            e.runSpeed = 5.6f;
            e.detectRadius = 14f;
            e.loseRadius = 30f;
            e.preferredDistance = 3.2f;
            e.aggression = 0.55f;
            e.dodgeChance = 0.18f;
            e.blockChance = 0.15f;
            e.staggerDuration = 0.65f;
            e.patrolRadius = 5f;
            e.breathOnKill = 6f;

            var claws = EAttack("claw_combo", "Rending Claws", "EnemySwipe", 0f, 2.4f, 3f, 1.8f, 0.55f, 0.14f, 0.55f, 1f, HitReaction.Light, 2.5f, 1.9f, 110f, EnemyAttackMovement.Lunge, 0.8f);
            claws.comboHits = 2;
            claws.comboInterval = 0.4f;
            var lunge = EAttack("lunge_bite", "Shadow Lunge", "EnemyLunge", 2.5f, 6f, 1.5f, 4f, 0.7f, 0.2f, 0.7f, 1.4f, HitReaction.Heavy, 5f, 1.6f, 90f, EnemyAttackMovement.Lunge, 4.5f);
            lunge.telegraphed = true;
            var leap = EAttack("shadow_leap", "Night Pounce", "EnemyLeap", 4f, 9f, 1f, 7f, 0.8f, 0.25f, 0.8f, 1.6f, HitReaction.Knockdown, 6f, 1.6f, 120f, EnemyAttackMovement.Leap, 0f);
            leap.telegraphed = true;
            leap.leapHeight = 2.5f;
            leap.groundSlam = true;
            leap.aoeRadius = 2.2f;
            leap.parryable = false;
            e.attacks = new List<EnemyAttackData> { claws, lunge, leap };
            return e;
        }

        private static EnemyData HollowOni()
        {
            var e = ScriptableObject.CreateInstance<EnemyData>();
            e.name = "Enemy_HollowOni";
            e.enemyId = "hollow_oni";
            e.voicePrefix = "oni";
            e.displayName = "The Hollow Oni";
            e.archetype = EnemyArchetype.HollowOni;
            e.isBoss = true;
            e.maxHealth = 1600f;
            e.poise = 120f;
            e.defense = 20f;
            e.weakness = Element.Moon;
            e.resistance = Element.Fire;
            e.baseDamage = 24f;
            e.walkSpeed = 2.2f;
            e.runSpeed = 4.8f;
            e.turnSpeed = 300f;
            e.scale = 1.85f;
            e.detectRadius = 22f;
            e.loseRadius = 60f;
            e.viewAngle = 360f;
            e.preferredDistance = 4.5f;
            e.aggression = 0.7f;
            e.dodgeChance = 0f;
            e.blockChance = 0.1f;
            e.knockbackResistance = 0.85f;
            e.canBeLaunched = false;
            e.staggerDuration = 1.1f;
            e.patrolRadius = 0f;
            e.breathOnKill = 40f;
            e.bodyColor = new Color(0.45f, 0.1f, 0.12f);
            e.accentColor = new Color(0.55f, 0.35f, 0.12f);
            e.eyeColor = new Color(4f, 1.2f, 0.2f);
            e.phase2EyeColor = new Color(4f, 0.1f, 0.6f);
            e.phase2SpeedMultiplier = 1.3f;
            e.phase2AggressionBonus = 0.25f;

            var smash = EAttack("smash", "Kanabo Smash", "OniSmash", 0f, 4.2f, 3f, 2.5f, 0.9f, 0.18f, 0.9f, 1.5f, HitReaction.Knockdown, 7f, 3.2f, 70f, EnemyAttackMovement.Lunge, 1f);
            smash.telegraphed = true;
            smash.groundSlam = true;
            smash.aoeRadius = 2.5f;
            smash.parryable = false;
            var sweep = EAttack("sweep", "Crushing Sweep", "OniSweep", 0f, 4.5f, 3f, 2f, 0.7f, 0.2f, 0.7f, 1.2f, HitReaction.Knockback, 8f, 3.6f, 200f, EnemyAttackMovement.None);
            var stomp = EAttack("stomp", "Quake Stomp", "OniStomp", 0f, 3f, 1.5f, 5f, 0.8f, 0.12f, 0.8f, 1f, HitReaction.Knockdown, 6f, 1f, 360f, EnemyAttackMovement.None);
            stomp.telegraphed = true;
            stomp.groundSlam = true;
            stomp.aoeRadius = 4f;
            stomp.parryable = false;
            var charge = EAttack("charge", "Demon Charge", "OniCharge", 5f, 14f, 1.5f, 6f, 0.6f, 0.9f, 0.9f, 1.4f, HitReaction.Knockdown, 10f, 1.8f, 120f, EnemyAttackMovement.Charge, 11f);
            charge.telegraphed = true;
            e.attacks = new List<EnemyAttackData> { smash, sweep, stomp, charge };

            var smash2 = EAttack("smash2", "Kanabo Smash", "OniSmash", 0f, 4.2f, 3f, 2f, 0.72f, 0.18f, 0.75f, 1.6f, HitReaction.Knockdown, 8f, 3.2f, 70f, EnemyAttackMovement.Lunge, 1.5f);
            smash2.telegraphed = true;
            smash2.groundSlam = true;
            smash2.aoeRadius = 3f;
            smash2.parryable = false;
            var sweep2 = EAttack("sweep2", "Twin Sweep", "OniSweep", 0f, 4.5f, 3f, 2f, 0.55f, 0.2f, 0.6f, 1.2f, HitReaction.Knockback, 8f, 3.6f, 200f, EnemyAttackMovement.Lunge, 0.8f);
            sweep2.comboHits = 2;
            sweep2.comboInterval = 0.45f;
            var stomp2 = EAttack("stomp2", "Quake Stomp", "OniStomp", 0f, 3.5f, 1.5f, 4f, 0.65f, 0.12f, 0.7f, 1.1f, HitReaction.Knockdown, 7f, 1f, 360f, EnemyAttackMovement.None);
            stomp2.telegraphed = true;
            stomp2.groundSlam = true;
            stomp2.aoeRadius = 4.5f;
            stomp2.parryable = false;
            var cleave = EAttack("eclipse_cleave", "Eclipse Cleave", "OniCleave", 3f, 16f, 2f, 9f, 1.1f, 0.35f, 1.1f, 2.4f, HitReaction.Knockdown, 12f, 2f, 120f, EnemyAttackMovement.Leap, 0f);
            cleave.telegraphed = true;
            cleave.leapHeight = 5f;
            cleave.groundSlam = true;
            cleave.aoeRadius = 5.5f;
            cleave.unblockable = true;
            cleave.parryable = false;
            var orbs = EAttack("hellfire_orbs", "Hellfire Wheels", "OniThrow", 6f, 18f, 1.5f, 6f, 0.6f, 0.1f, 0.6f, 1f, HitReaction.Heavy, 4f, 1f, 60f, EnemyAttackMovement.None);
            orbs.projectile = new ProjectileSpec
            {
                enabled = true, vfxId = "fire_wheel", count = 3, spreadAngle = 40f, speed = 12f, lifetime = 2f, radius = 0.8f, pierce = false,
                damageMultiplier = 1f, reaction = HitReaction.Heavy, knockback = 4f, impactVfx = "impact_dark"
            };
            e.phase2Attacks = new List<EnemyAttackData> { smash2, sweep2, stomp2, charge, cleave, orbs };
            return e;
        }
    }
}
