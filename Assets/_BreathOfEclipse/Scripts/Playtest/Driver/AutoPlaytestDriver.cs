using System.Collections;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Drives the player through <see cref="InputReader"/>'s simulated input — the same buffer, events and
    /// held states the keyboard/gamepad feed — so PlayerController, combat, techniques and cameras run their
    /// normal code paths. Movement is expressed as a camera-relative stick vector, exactly like a player would.
    /// </summary>
    public sealed class AutoPlaytestDriver
    {
        private readonly InputReader _input;

        public SimulatedInput Sim { get; }

        public AutoPlaytestDriver(InputReader input)
        {
            _input = input;
            Sim = input.BeginSimulation();
        }

        public void Dispose()
        {
            if (_input != null) _input.EndSimulation();
        }

        public static PlayerController Player => PlayerController.Instance;

        // ------------------------------------------------------------------ primitives

        public void Press(InputCommand command) => Sim.Press(command);

        public void SetMove(Vector2 stick) => Sim.Move = Vector2.ClampMagnitude(stick, 1f);

        public void Stop()
        {
            Sim.Move = Vector2.zero;
            Sim.Sprint = false;
        }

        public void ReleaseAll() => Sim.ReleaseAll();

        /// <summary>Camera-relative stick vector that moves the player along a world direction.</summary>
        public static Vector2 StickFor(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return Vector2.zero;
            worldDirection.Normalize();
            var rig = CameraRig.Instance;
            Vector3 fwd = rig != null ? rig.PlanarForward : Vector3.forward;
            Vector3 right = rig != null ? rig.PlanarRight : Vector3.right;
            return new Vector2(Vector3.Dot(worldDirection, right), Vector3.Dot(worldDirection, fwd)).normalized;
        }

        public void MoveWorld(Vector3 worldDirection, float magnitude = 1f) => SetMove(StickFor(worldDirection) * magnitude);

        // ------------------------------------------------------------------ composite actions

        /// <summary>Walks/runs to a point (steering every frame). Presses Jump once if stuck.</summary>
        public IEnumerator MoveTo(PlaytestContext ctx, Vector3 target, float stopDistance, float timeout, bool sprint, WaitResult result)
        {
            result.Success = false;
            float start = Time.realtimeSinceStartup;
            float stuckTimer = 0f;
            Vector3 lastPos = Player != null ? Player.transform.position : Vector3.zero;
            bool jumped = false;
            while (Time.realtimeSinceStartup - start < timeout)
            {
                if (ctx.Paused)
                {
                    start += Time.unscaledDeltaTime; // paused time does not count
                    yield return null;
                    continue;
                }
                var pc = Player;
                if (pc == null) break;
                Vector3 to = target - pc.transform.position;
                to.y = 0f;
                if (to.magnitude <= stopDistance)
                {
                    result.Success = true;
                    break;
                }
                MoveWorld(to);
                Sim.Sprint = sprint && to.magnitude > 4f;
                if (Time.deltaTime > 0f)
                {
                    stuckTimer += Time.unscaledDeltaTime;
                    if (stuckTimer > 1.2f)
                    {
                        if (Vector3.Distance(pc.transform.position, lastPos) < 0.3f && !jumped)
                        {
                            Press(InputCommand.Jump);
                            jumped = true;
                        }
                        lastPos = pc.transform.position;
                        stuckTimer = 0f;
                    }
                }
                yield return null;
                if (ctx.ShouldStop) break;
            }
            Stop();
            result.Elapsed = Time.realtimeSinceStartup - start;
        }

        /// <summary>Turns the player (and the third-person camera behind it) toward a point without moving far.</summary>
        public IEnumerator FaceTowards(PlaytestContext ctx, Vector3 point, float duration = 0.25f)
        {
            var pc = Player;
            if (pc == null) yield break;
            Vector3 dir = point - pc.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) yield break;
            MoveWorld(dir, 0.3f);
            yield return ctx.WaitGame(duration);
            Stop();
            yield return AimCameraAt(ctx, point, 1f);
        }

        /// <summary>Rotates the camera (simulated look input) until the point is near the screen center.</summary>
        public IEnumerator AimCameraAt(PlaytestContext ctx, Vector3 point, float timeout)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < timeout)
            {
                var rig = CameraRig.Instance;
                if (rig == null || rig.Mode != CameraMode.ThirdPerson || rig.CinematicActive) yield break;
                Vector3 to = point - rig.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f) yield break;
                float yawError = Vector3.SignedAngle(rig.PlanarForward, to, Vector3.up);
                if (Mathf.Abs(yawError) < 6f) yield break;
                Sim.AddLook(new Vector2(Mathf.Clamp(yawError * 0.35f, -25f, 25f), 0f));
                yield return null;
                if (ctx.ShouldStop) yield break;
            }
        }

        /// <summary>Holds block, waits, releases.</summary>
        public IEnumerator HoldBlock(PlaytestContext ctx, float seconds)
        {
            Sim.Block = true;
            Press(InputCommand.Block);
            yield return ctx.WaitGame(seconds);
            Sim.Block = false;
        }
    }
}
