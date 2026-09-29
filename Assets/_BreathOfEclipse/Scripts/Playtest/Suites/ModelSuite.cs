using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.AI;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// v0.4 real 3D characters: the anime swordsman is attached (never invisible), the katana lives on the model's
    /// hand and follows attacks, the clips animate the legs, first person hides the head, demons get their models.
    /// </summary>
    public static class ModelSuite
    {
        public static IEnumerable<PlaytestStep> Steps()
        {
            yield return new PlaytestStep(C.Model, "Player 3D model attached", Attached, 8f);
            yield return new PlaytestStep(C.Model, "Katana on the real hand follows attacks", BladeFollows, 10f);
            yield return new PlaytestStep(C.Model, "Real locomotion clips move the legs", Locomotion, 10f);
            yield return new PlaytestStep(C.Model, "First person hides head / hair", FirstPerson, 10f);
            yield return new PlaytestStep(C.Model, "Hidden technique frames restore the model", VisibilityRestore, 6f);
            yield return new PlaytestStep(C.Model, "Demon 3D models (Nightspawn, Hollow Oni)", Demons, 15f);
        }

        private static HumanoidVisualDriver Driver(PlayerController pc) => pc != null ? pc.GetComponent<HumanoidVisualDriver>() : null;

        private static bool ExpectModel() => SaveSystem.Settings == null || SaveSystem.Settings.playerVisualMode == 0;

        private static IEnumerator Attached(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            var pc = ctx.Player;
            var d = Driver(pc);
            if (!ExpectModel())
            {
                ctx.Result(TestStatus.NotTested, "Settings use the procedural mannequin (playerVisualMode = 1)");
                yield break;
            }
            if (d == null)
            {
                ctx.Fail("no HumanoidVisualDriver: the model failed to build (see the console warning) — the mannequin is shown instead");
                yield break;
            }
            var weapon = pc.Rig.Bone(RigBone.Weapon);
            bool onModel = weapon != null && weapon.IsChildOf(d.VisualRoot);
            int visible = 0;
            foreach (var r in d.VisualRoot.GetComponentsInChildren<Renderer>()) if (r.enabled) visible++;
            float h = d.Skeleton != null ? d.Skeleton.Height : 0f;
            ctx.Check("Katana socket on the model's right hand", onModel, weapon != null && weapon.parent != null ? weapon.parent.name : "none");
            ctx.Check("Model renderers visible", visible > 5, $"{visible} renderers");
            ctx.Check("Height 1.7–1.85 m", h > 1.7f && h < 1.85f, $"{h:0.00} m");
            ctx.Check("Real clips (Humanoid)", d.ClipsActive, d.ClipsActive ? $"{HumanoidClipLibrary.Count} clips" : "clips not Humanoid: procedural retarget only", true);
            ctx.Screens.CaptureAuto("Model_Attached");
            if (onModel && visible > 5) ctx.Pass(d.Skeleton != null ? d.Skeleton.Report.Split('\n')[0] : "ok");
            else ctx.Fail("model not complete");
            yield return ctx.Observe(1f);
        }

        private static IEnumerator BladeFollows(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var d = Driver(pc);
            if (d == null)
            {
                ctx.Result(TestStatus.NotTested, "no 3D model");
                yield break;
            }
            var tip = pc.Animator.WeaponTip;
            var hand = d.Skeleton[HumanBodyBones.RightHand];
            Vector3 start = tip.position;
            float travel = 0f, maxFromHand = 0f, maxFromBody = 0f;
            Vector3 last = start;
            ctx.Driver.Press(InputCommand.LightAttack);
            float t = 0f;
            while (t < 0.7f && !ctx.ShouldStop)
            {
                t += Time.unscaledDeltaTime;
                travel += Vector3.Distance(last, tip.position);
                last = tip.position;
                maxFromHand = Mathf.Max(maxFromHand, Vector3.Distance(tip.position, hand.position));
                maxFromBody = Mathf.Max(maxFromBody, Vector3.Distance(tip.position, pc.transform.position + Vector3.up));
                yield return null;
            }
            ctx.Check("Blade swings", travel > 0.6f, $"tip travelled {travel:0.00} m");
            ctx.Check("Blade stays in the hand", maxFromHand < 1.4f, $"max tip-to-hand {maxFromHand:0.00} m");
            ctx.Check("Blade near the body", maxFromBody < 2.6f, $"max {maxFromBody:0.00} m");
            if (travel > 0.6f && maxFromHand < 1.4f) ctx.Pass($"travel {travel:0.00} m");
            else ctx.Fail($"travel {travel:0.00} m, hand {maxFromHand:0.00} m");
            yield return ctx.Observe(0.8f);
        }

        private static IEnumerator Locomotion(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var d = Driver(pc);
            if (d == null)
            {
                ctx.Result(TestStatus.NotTested, "no 3D model");
                yield break;
            }
            var footL = d.Skeleton[HumanBodyBones.LeftFoot];
            var footR = d.Skeleton[HumanBodyBones.RightFoot];
            float minSep = float.MaxValue, maxSep = 0f;
            ctx.Driver.MoveWorld(pc.transform.forward);
            float t = 0f;
            while (t < 1.4f && !ctx.ShouldStop)
            {
                t += Time.unscaledDeltaTime;
                Vector3 a = pc.transform.InverseTransformPoint(footL.position), b = pc.transform.InverseTransformPoint(footR.position);
                float sep = a.z - b.z;
                minSep = Mathf.Min(minSep, sep);
                maxSep = Mathf.Max(maxSep, sep);
                yield return null;
            }
            ctx.Driver.ReleaseAll();
            float stride = maxSep - minSep;
            ctx.Check("Feet alternate (stride)", stride > 0.25f, $"stride range {stride:0.00} m");
            float footY = Mathf.Min(footL.position.y, footR.position.y) - pc.transform.position.y;
            ctx.Check("Feet on the ground", footY > -0.12f && footY < 0.25f, $"lowest ankle {footY:0.00} m above the root");
            if (stride > 0.25f) ctx.Pass($"stride {stride:0.00} m");
            else ctx.Fail($"stride {stride:0.00} m (clips {(d.ClipsActive ? "on" : "off")})");
            yield return ctx.Observe(0.5f);
        }

        private static IEnumerator FirstPerson(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var d = Driver(pc);
            var rig = ctx.Rig;
            if (d == null || rig == null)
            {
                ctx.Result(TestStatus.NotTested, "no 3D model");
                yield break;
            }
            rig.SetMode(CameraMode.FirstPerson, true);
            yield return ctx.WaitReal(0.3f);
            Renderer face = null, hair = null;
            foreach (var r in d.VisualRoot.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Face") face = r;
                if (r.name == "Hair") hair = r;
            }
            bool faceHidden = face == null || face.gameObject.layer == Layers.PlayerHidden;
            bool hairHidden = hair == null || hair.gameObject.layer == Layers.PlayerHidden;
            ctx.Check("Face hidden from the first-person camera", faceHidden, face != null ? LayerMask.LayerToName(face.gameObject.layer) : "no face mesh");
            ctx.Check("Hair hidden from the first-person camera", hairHidden, hair != null ? LayerMask.LayerToName(hair.gameObject.layer) : "no hair mesh");
            ctx.Screens.CaptureAuto("Model_FirstPerson");
            rig.SetMode(CameraMode.ThirdPerson, true);
            yield return ctx.WaitReal(0.3f);
            bool restored = face == null || face.gameObject.layer != Layers.PlayerHidden;
            ctx.Check("Head back in third person", restored, "layer restored");
            if (faceHidden && hairHidden && restored) ctx.Pass("head / hair hidden only in first person");
            else ctx.Fail("first-person hiding incomplete");
        }

        private static IEnumerator VisibilityRestore(PlaytestContext ctx)
        {
            var pc = ctx.Player;
            var d = Driver(pc);
            if (d == null)
            {
                ctx.Result(TestStatus.NotTested, "no 3D model");
                yield break;
            }
            pc.Rig.SetVisible(false);
            yield return null;
            int shown = 0;
            foreach (var r in d.VisualRoot.GetComponentsInChildren<Renderer>()) if (r.enabled) shown++;
            pc.Rig.SetVisible(true);
            yield return null;
            int after = 0;
            foreach (var r in d.VisualRoot.GetComponentsInChildren<Renderer>()) if (r.enabled) after++;
            ctx.Check("Hidden during flash-step frames", shown <= 1, $"{shown} visible while hidden (mouth excluded)");
            ctx.Check("Visible again", after > 5, $"{after} visible");
            if (shown <= 1 && after > 5) ctx.Pass("hide / show round trip");
            else ctx.Fail($"hidden {shown}, shown {after}");
        }

        private static IEnumerator Demons(PlaytestContext ctx)
        {
            if (SaveSystem.Settings != null && SaveSystem.Settings.demonVisualMode == 1)
            {
                ctx.Result(TestStatus.NotTested, "Settings use the procedural demons");
                yield break;
            }
            var pc = ctx.Player;
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            if (db == null || pc == null)
            {
                ctx.Result(TestStatus.NotTested, "no database / player");
                yield break;
            }
            var spawned = new List<EnemyController>();
            bool ok = true;
            string details = "";
            foreach (var id in new[] { "nightspawn", "hollow_oni" })
            {
                var data = db.FindEnemy(id);
                if (data == null) continue;
                Vector3 p = pc.transform.position + pc.transform.right * (id == "nightspawn" ? 7f : -12f) + pc.transform.forward * 8f;
                var e = EnemyFactory.Spawn(data, p, Quaternion.LookRotation(-pc.transform.forward), false);
                spawned.Add(e);
                yield return null;
                var d = e.GetComponent<HumanoidVisualDriver>();
                bool has = d != null;
                ok &= has;
                details += $"{id}: {(has ? $"3D {d.Skeleton.Height:0.00} m" : "mannequin")}  ";
            }
            yield return ctx.WaitReal(1f);
            ctx.Screens.CaptureAuto("Model_Demons");
            foreach (var e in spawned) if (e != null) e.Damageable.Kill();
            if (ok) ctx.Pass(details);
            else ctx.Fail(details);
            yield return ctx.WaitReal(2.5f);
        }
    }
}
