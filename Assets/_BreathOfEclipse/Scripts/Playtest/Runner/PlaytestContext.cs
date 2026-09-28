using System;
using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.AI;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Everything a test step needs: the driver, recorders, reporting, pacing-aware waits and small setup helpers.
    /// Setup helpers (refill, reset cooldowns, spawn) are test preparation only — they never change tuning values.
    /// </summary>
    public sealed class PlaytestContext
    {
        public PlaytestMode Mode;
        public PlaytestSpeed Speed;
        public AutoPlaytestDriver Driver;
        public PlaytestLogger Logger;
        public TelemetryRecorder Telemetry;
        public PlaytestScreenshots Screens;
        public RuntimePerformanceMonitor Perf;
        public PlaytestRunner Runner;
        public FrameSampler Sampler;

        public PlaytestStep CurrentStep { get; private set; }
        public int ResultsThisStep { get; private set; }
        /// <summary>Set by the runner when the step must end (skip, abort, timeout).</summary>
        public bool ShouldStop => Runner != null && (Runner.SkipRequested || Runner.AbortRequested || Runner.StepTimedOut);
        public bool Paused => Runner != null && Runner.Paused;

        /// <summary>Objects tests share (spawned enemies, dummies) — keyed by name.</summary>
        public readonly Dictionary<string, object> Shared = new Dictionary<string, object>();

        public PlayerController Player => PlayerController.Instance;
        public CameraRig Rig => CameraRig.Instance;
        public GameDatabase Db => GameManager.Instance != null ? GameManager.Instance.Database : null;

        public void BeginStep(PlaytestStep step)
        {
            CurrentStep = step;
            ResultsThisStep = 0;
        }

        // ------------------------------------------------------------------ reporting

        public void Report(string category, string name, TestStatus status, string details = null)
        {
            Logger.Add(category, name, status, details);
            if (CurrentStep != null && category == CurrentStep.Category) ResultsThisStep++;
        }

        /// <summary>Result for the current step.</summary>
        public void Result(TestStatus status, string details = null) => Report(CurrentStep.Category, CurrentStep.Name, status, details);
        public void Pass(string details = null) => Result(TestStatus.Pass, details);
        public void Fail(string details = null) => Result(TestStatus.Fail, details);
        public void Warn(string details = null) => Result(TestStatus.Warning, details);

        /// <summary>Named sub-result of the current step ("Rising Serpent — Dash").</summary>
        public void Sub(string subName, TestStatus status, string details = null) =>
            Report(CurrentStep.Category, $"{CurrentStep.Name} — {subName}", status, details);

        public void Check(string subName, bool ok, string details, bool warningOnly = false) =>
            Sub(subName, ok ? TestStatus.Pass : warningOnly ? TestStatus.Warning : TestStatus.Fail, details);

        // ------------------------------------------------------------------ pacing

        /// <summary>Scales generic waits: FAST shortens them, NORMAL/VISUAL keep game timing.</summary>
        public float Pace(float seconds) => Speed == PlaytestSpeed.Fast ? seconds * 0.6f : seconds;

        /// <summary>Observation pause after something worth watching (long in VISUAL).</summary>
        public IEnumerator Observe(float visualSeconds = 2f)
        {
            float s = Speed == PlaytestSpeed.Visual ? visualSeconds : Speed == PlaytestSpeed.Normal ? 0.45f : 0.12f;
            yield return WaitReal(s);
        }

        /// <summary>Waits game time (slows with hit stop / slow motion, stops while paused).</summary>
        public IEnumerator WaitGame(float seconds)
        {
            float t = 0f;
            float guard = Time.realtimeSinceStartup + seconds * 20f + 5f;
            while (t < seconds && !ShouldStop && Time.realtimeSinceStartup < guard)
            {
                if (!Paused) t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Waits real time (not affected by time scale); pausing the test extends it.</summary>
        public IEnumerator WaitReal(float seconds)
        {
            float t = 0f;
            while (t < seconds && !ShouldStop)
            {
                if (!Paused) t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        public IEnumerator Frames(int count)
        {
            for (int i = 0; i < count && !ShouldStop; i++) yield return null;
        }

        /// <summary>Waits until the condition is true (checked every frame) or the real-time timeout expires.</summary>
        public IEnumerator WaitUntil(Func<bool> condition, float timeout, WaitResult result)
        {
            result.Success = false;
            float start = Time.realtimeSinceStartup;
            float pausedTime = 0f;
            while (!ShouldStop)
            {
                bool ok;
                try
                {
                    ok = condition();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Playtest] Wait condition threw: {e.Message}");
                    ok = false;
                }
                if (ok)
                {
                    result.Success = true;
                    break;
                }
                if (Paused) pausedTime += Time.unscaledDeltaTime;
                else if (Time.realtimeSinceStartup - start - pausedTime > timeout) break;
                yield return null;
            }
            result.Elapsed = Time.realtimeSinceStartup - start;
        }

        // ------------------------------------------------------------------ scenes

        public static string ActiveScene => SceneManager.GetActiveScene().name;

        public IEnumerator LoadScene(string scene, WaitResult result, float timeout = 25f)
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(scene, 0.3f);
            else SceneManager.LoadScene(scene);
            yield return WaitUntil(() => ActiveScene == scene && (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading), timeout, result);
            if (result.Success) Logger.NoteScene(scene);
        }

        /// <summary>Makes sure 03_CombatTest is loaded and the player exists.</summary>
        public IEnumerator EnsureCombatTest(WaitResult result)
        {
            if (ActiveScene != SceneNames.CombatTest || Player == null)
            {
                yield return LoadScene(SceneNames.CombatTest, result);
                if (!result.Success) yield break;
            }
            yield return WaitUntil(() => Player != null && Rig != null, 10f, result);
            if (result.Success) yield return Frames(10);
        }

        // ------------------------------------------------------------------ player setup / recovery

        /// <summary>Releases simulated input and waits until the player is back in free locomotion.</summary>
        public IEnumerator Settle(float timeout = 3f)
        {
            Driver.ReleaseAll();
            var w = new WaitResult();
            yield return WaitUntil(() => Player == null || (Player.State == PlayerState.Locomotion && Player.Motor.Grounded), timeout, w);
            yield return Frames(2);
        }

        /// <summary>Test preparation: full HP / stamina / BREATH and no cooldowns (like the training shrine).</summary>
        public void RefillPlayer(bool fullBreath = true)
        {
            var pc = Player;
            if (pc == null) return;
            pc.Damageable.Health.Revive(1f);
            pc.Stats.Stamina.Refill();
            if (fullBreath) pc.Stats.Breath.SetValue(pc.Stats.Breath.Max);
            pc.Breathing.Cooldowns.ResetAll();
        }

        /// <summary>Recovery only: respawns a dead or lost player (logged).</summary>
        public void RecoverPlayerIfNeeded()
        {
            var pc = Player;
            if (pc == null) return;
            bool dead = pc.State == PlayerState.Dead || !pc.Damageable.IsAlive;
            bool fell = pc.transform.position.y < -20f;
            if (!dead && !fell) return;
            pc.Respawn();
            Debug.LogWarning($"[Playtest] Recovery: player {(dead ? "was dead" : "fell out of the world")} — respawned.");
            Report(PlaytestCategories.Movement, "Recovery", TestStatus.Warning, dead ? "player died during tests — respawned" : "player fell out of the world — respawned");
        }

        public bool IsLockedOn(Component target) =>
            Player != null && Player.LockOn.Current is Component c && target != null && c.gameObject == target.gameObject;

        /// <summary>Presses lock-on (simulated) until the given target is locked.</summary>
        public IEnumerator LockOnto(Component target, WaitResult result)
        {
            result.Success = false;
            var pc = Player;
            if (pc == null || target == null) yield break;
            for (int attempt = 0; attempt < 4 && !ShouldStop; attempt++)
            {
                if (IsLockedOn(target))
                {
                    result.Success = true;
                    yield break;
                }
                if (pc.LockOn.Current != null)
                {
                    // Locked on something else: release first.
                    Driver.Press(InputCommand.LockOn);
                    yield return Frames(2);
                }
                yield return Driver.FaceTowards(this, target.transform.position + Vector3.up, 0.2f);
                Driver.Press(InputCommand.LockOn);
                yield return Frames(3);
            }
            result.Success = IsLockedOn(target);
        }

        public void ReleaseLockOn()
        {
            if (Player != null && Player.LockOn.Current != null) Player.LockOn.Release();
        }

        // ------------------------------------------------------------------ world helpers

        public TrainingDummy ClosestDummy()
        {
            TrainingDummy best = null;
            float bestDist = float.MaxValue;
            Vector3 from = Player != null ? Player.transform.position : Vector3.zero;
            foreach (var d in UnityEngine.Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
            {
                float dist = Vector3.Distance(from, d.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = d;
                }
            }
            return best;
        }

        /// <summary>A training dummy for tests; spawns one in front of the player if the scene has none.</summary>
        public TrainingDummy EnsureDummy()
        {
            var d = ClosestDummy();
            if (d != null) return d;
            var pc = Player;
            Vector3 pos = pc != null ? pc.transform.position + pc.transform.forward * 4f : Vector3.zero;
            return TrainingDummy.Create(pos, Quaternion.LookRotation(-(pc != null ? pc.transform.forward : Vector3.forward)));
        }

        public static Damageable DamageableOf(Component c) => c != null ? c.GetComponent<Damageable>() : null;

        public Vector3 GroundPoint(Vector3 around)
        {
            HitQuery.GroundPoint(around + Vector3.up * 6f, out var p, out _, 20f);
            return p;
        }

        public EnemyController SpawnEnemy(string enemyId, Vector3 position, Vector3 faceTowards, bool portal)
        {
            var data = Db != null ? Db.FindEnemy(enemyId) : null;
            if (data == null) return null;
            Vector3 ground = GroundPoint(position) + Vector3.up * 0.05f;
            Vector3 dir = faceTowards - ground;
            dir.y = 0f;
            return EnemyFactory.Spawn(data, ground, dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity, portal);
        }

        public static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
