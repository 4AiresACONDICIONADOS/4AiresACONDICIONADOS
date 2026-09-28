using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.AI;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using UnityEngine;
using C = BreathOfEclipse.Playtest.PlaytestCategories;

namespace BreathOfEclipse.Playtest
{
    /// <summary>Third, first and second person through the C key, plus the second-person fallback.</summary>
    public static class CameraSuite
    {
        public static IEnumerable<PlaytestStep> Steps()
        {
            yield return new PlaytestStep(C.Cameras, "Third person", ThirdPerson, 10f);
            yield return new PlaytestStep(C.Cameras, "Third person collision (probe)", CollisionProbe, 5f);
            yield return new PlaytestStep(C.Cameras, "First person (C)", FirstPerson, 12f);
            yield return new PlaytestStep(C.Cameras, "Second person (C, locked on)", SecondPerson, 12f);
            yield return new PlaytestStep(C.Cameras, "Second person target lost → third person", TargetLost, 8f);
        }

        private static bool OnScreen(Camera cam, Vector3 point, float margin = 0.02f)
        {
            Vector3 vp = cam.WorldToViewportPoint(point);
            return vp.z > 0f && vp.x > margin && vp.x < 1f - margin && vp.y > margin && vp.y < 1f - margin;
        }

        private static IEnumerator EnsureMode(PlaytestContext ctx, CameraMode mode)
        {
            var rig = ctx.Rig;
            for (int i = 0; i < 3 && rig.Mode != mode && !ctx.ShouldStop; i++)
            {
                ctx.Driver.Press(InputCommand.CameraMode);
                yield return ctx.WaitReal(0.15f);
            }
        }

        private static IEnumerator ThirdPerson(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            yield return EnsureMode(ctx, CameraMode.ThirdPerson);
            var rig = ctx.Rig;
            var pc = ctx.Player;
            yield return ctx.WaitReal(0.5f);
            float distance = rig.DistanceToPlayer;
            float fov = rig.Camera.fieldOfView;
            bool visible = OnScreen(rig.Camera, pc.Rig.LockOnPoint.position);
            var cues = ctx.Telemetry.CameraCueKindsSince(0f);
            ctx.Check("Follows the player", visible, "player on screen");
            ctx.Check("Distance", distance > 2f && distance < 10f, $"{distance:0.0} m");
            ctx.Check("FOV", fov > 40f && fov < 100f, $"{fov:0.0}°");
            ctx.Check("Shake / FOV punch / zoom used", cues.Contains("shake") && (cues.Contains("fov") || cues.Contains("zoom")), "cues this session: " + string.Join(", ", cues));
            ctx.Screens.CaptureAuto("ThirdPerson");
            if (rig.Mode == CameraMode.ThirdPerson && visible) ctx.Pass($"mode {rig.Mode}, lock target {(rig.LockTarget != null ? rig.LockTarget.name : "none")}");
            else ctx.Fail($"mode {rig.Mode}, player visible {visible}");
            yield return ctx.Observe(1.5f);
        }

        private static IEnumerator CollisionProbe(PlaytestContext ctx)
        {
            // Uses the same collision routine the third-person camera uses, against the nearest static obstacle.
            var pc = ctx.Player;
            Collider best = null;
            float bestDist = float.MaxValue;
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger || col.attachedRigidbody != null || ((1 << col.gameObject.layer) & Layers.CameraObstacleMask) == 0) continue;
                if (col is MeshCollider || col.bounds.size.y < 2f || col.bounds.size.x > 20f) continue; // skip ground/walls
                float d = Vector3.Distance(pc.transform.position, col.bounds.center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = col;
                }
            }
            if (best == null)
            {
                ctx.Result(TestStatus.NotTested, "no suitable obstacle found near the player");
                yield break;
            }
            Vector3 center = best.bounds.center;
            Vector3 dir = pc.transform.position - center;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            float surface = Mathf.Max(best.bounds.extents.x, best.bounds.extents.z);
            Vector3 pivot = center + dir * (surface + 3f);
            float safe = CameraCollision.SafeDistance(pivot, -dir, 8f, 0.22f, Layers.CameraObstacleMask);
            if (safe < 3.5f) ctx.Pass($"camera pulled in to {safe:0.0} m in front of '{best.name}' (desired 8 m)");
            else ctx.Fail($"no pull-in in front of '{best.name}': safe distance {safe:0.0} m");
            yield break;
        }

        private static IEnumerator FirstPerson(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            var dummy = ctx.EnsureDummy();
            var w = new WaitResult();
            yield return ctx.Driver.MoveTo(ctx, dummy.transform.position, 2.4f, 6f, false, w);
            yield return ctx.LockOnto(dummy, w);
            yield return EnsureMode(ctx, CameraMode.FirstPerson);
            var rig = ctx.Rig;
            var pc = ctx.Player;
            yield return ctx.WaitReal(0.5f);
            bool isFirst = rig.Mode == CameraMode.FirstPerson;
            float eyeDist = Vector3.Distance(rig.Camera.transform.position, pc.Rig.EyePoint.position);
            bool headHidden = (rig.Camera.cullingMask & (1 << Layers.PlayerHidden)) == 0;

            float mark = ctx.Telemetry.Mark();
            bool swordSeen = false, shot = false;
            ctx.Driver.Press(InputCommand.LightAttack);
            yield return ctx.WaitUntil(() =>
            {
                if (OnScreen(rig.Camera, pc.Animator.WeaponTip.position, 0f) || OnScreen(rig.Camera, pc.Animator.WeaponBase.position, 0f)) swordSeen = true;
                if (!shot && pc.Combat.InActiveFrames)
                {
                    shot = true;
                    ctx.Screens.CaptureAuto("FirstPerson");
                }
                return !pc.Combat.IsAttacking && TelemetryRecorder.CountSince(ctx.Telemetry.Vfx, mark) > 0;
            }, 2f, w);
            bool damaged = ctx.Telemetry.DamageSince(mark, d => d.Result.Target == dummy.gameObject).Count > 0;
            ctx.Check("Camera at the eyes", eyeDist < 0.8f, $"{eyeDist:0.00} m from the eye point");
            ctx.Check("Head hidden, arms/sword visible", headHidden && swordSeen, $"head layer culled {headHidden}, sword on screen during the attack {swordSeen}");
            ctx.Check("Can fight", damaged, damaged ? "light attack hit the dummy" : "no hit");
            ctx.Check("Enemy on screen", OnScreen(rig.Camera, dummy.LockOnPoint.position, 0f), "locked dummy visible");
            if (isFirst && damaged) ctx.Pass("first person playable (VFX coverage needs human review)");
            else ctx.Fail($"mode {rig.Mode}, damage {damaged}");
            yield return ctx.Observe(2f);
        }

        private static IEnumerator SecondPerson(PlaytestContext ctx)
        {
            yield return ctx.Settle();
            var dummy = ctx.EnsureDummy();
            var w = new WaitResult();
            if (!ctx.IsLockedOn(dummy)) yield return ctx.LockOnto(dummy, w);
            yield return EnsureMode(ctx, CameraMode.SecondPerson);
            var rig = ctx.Rig;
            var pc = ctx.Player;
            yield return ctx.WaitReal(0.6f);
            if (rig.Mode != CameraMode.SecondPerson)
            {
                ctx.Fail($"could not enter second person (mode {rig.Mode}, locked {ctx.IsLockedOn(dummy)})");
                yield break;
            }
            Vector3 camPos = rig.Camera.transform.position;
            float nearTarget = Vector3.Distance(camPos, dummy.LockOnPoint.position);
            Vector3 toPlayer = (pc.Rig.LockOnPoint.position - camPos).normalized;
            float facing = Vector3.Dot(rig.Camera.transform.forward, toPlayer);
            ctx.Check("Viewpoint from the enemy", nearTarget < 4f, $"{nearTarget:0.0} m from the locked target");
            ctx.Check("Looks at the protagonist", facing > 0.5f && OnScreen(rig.Camera, pc.Rig.LockOnPoint.position, 0f), $"facing dot {facing:0.00}");

            Vector3 p0 = pc.transform.position;
            ctx.Driver.MoveWorld(pc.transform.right);
            yield return ctx.WaitGame(0.6f);
            ctx.Driver.Stop();
            float moved = PlaytestContext.Flat(p0, pc.transform.position);
            ctx.Check("Movement", moved > 1f, $"moved {moved:0.0} m");

            float mark = ctx.Telemetry.Mark();
            yield return ctx.Driver.MoveTo(ctx, dummy.transform.position, 2.4f, 4f, false, w);
            ctx.Driver.Press(InputCommand.LightAttack);
            bool shot = false;
            yield return ctx.WaitUntil(() =>
            {
                if (!shot && pc.Combat.InActiveFrames)
                {
                    shot = true;
                    ctx.Screens.CaptureAuto("SecondPerson");
                }
                return ctx.Telemetry.DamageSince(mark, d => d.Result.Target == dummy.gameObject).Count > 0;
            }, 2f, w);
            ctx.Check("Attacks", w.Success, w.Success ? "hit the dummy" : "no hit");
            ctx.Check("Lock-on kept", ctx.IsLockedOn(dummy), "target still locked");
            ctx.Pass($"second person active, camera {rig.DistanceToPlayer:0.0} m from the player (walls/obstacles: verify in CAMERA TEST)");
            yield return ctx.Observe(2f);
        }

        private static IEnumerator TargetLost(PlaytestContext ctx)
        {
            var rig = ctx.Rig;
            if (rig.Mode != CameraMode.SecondPerson)
            {
                ctx.Result(TestStatus.NotTested, "second person was not active");
                yield break;
            }
            bool lost = false;
            void OnLost() => lost = true;
            rig.SecondPersonTargetLost += OnLost;
            ctx.Driver.Press(InputCommand.LockOn); // releases the lock
            var w = new WaitResult();
            yield return ctx.WaitUntil(() => rig.Mode == CameraMode.ThirdPerson, 1.5f, w);
            rig.SecondPersonTargetLost -= OnLost;
            if (lost && w.Success) ctx.Pass("SECOND PERSON TARGET LOST → third person automatically");
            else ctx.Fail($"target-lost event {lost}, back to third {w.Success} (mode {rig.Mode})");
            yield return ctx.Observe(1f);
        }
    }
}
