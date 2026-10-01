using System;
using System.Collections.Generic;

namespace BreathOfEclipse.World
{
    public enum WorldEventState
    {
        Dormant = 0,
        Eligible = 1,
        Active = 2,
        ResolvedSuccess = 3,
        ResolvedFailure = 4,
        /// <summary>The scene was cleared after the hard deadline; consequences stay as facts. Cooldown runs.</summary>
        Expired = 5
    }

    /// <summary>Saved state of one dynamic event.</summary>
    [Serializable]
    public sealed class WorldEventRecord
    {
        public string id;
        public WorldEventState state = WorldEventState.Dormant;
        public double startedAt = -1;
        public double softDeadline = -1;
        public double hardDeadline = -1;
        public double resolvedAt = -1;
        public double cooldownUntil;
        public bool playerInvolved;
        public bool resolvedOffscreen;
        public int triggerCount;
        public int successes;
        public int failures;
        /// <summary>Which of the event's spots / variants this run uses.</summary>
        public int variant;

        public bool IsActive => state == WorldEventState.Active;
        public bool IsResolved => state == WorldEventState.ResolvedSuccess || state == WorldEventState.ResolvedFailure;
        /// <summary>Started and not yet cleared (actors or aftermath may be in the world).</summary>
        public bool IsLive => IsActive || IsResolved;
    }

    /// <summary>Static description of a dynamic event (data-driven; the runtime stages and resolves it).</summary>
    public sealed class WorldEventDef
    {
        public string Id;
        public string Title;
        public string Sector;
        /// <summary>Hours of the day the event may start (wraps past midnight).</summary>
        public float WindowStart = 0f, WindowEnd = 24f;
        public bool NightOnly;
        public int MinDay = 1;
        /// <summary>Chance to start at each director check (every 15 in-game minutes) while eligible.</summary>
        public float ChancePerCheck = 0.05f;
        /// <summary>Hours after the start when an unattended event resolves on its own.</summary>
        public float SoftDeadline = 0.35f;
        /// <summary>Hours after the start when the scene is cleared (aftermath removed, facts kept).</summary>
        public float HardDeadline = 1f;
        public float CooldownHours = 18f;
        public bool Repeatable = true;
        /// <summary>Only started from the debug menu / scripts (exceptional presence).</summary>
        public bool ManualOnly;
        /// <summary>Chance the event ends well without the player.</summary>
        public float BaseSuccessChance = 0.4f;
        /// <summary>Number of spots / variants the runtime can stage it at.</summary>
        public int Variants = 1;
    }

    /// <summary>
    /// Event lifecycle: Dormant → Eligible (conditions met) → Active (started, deadlines set) → Resolved (by the player
    /// or on its own at the soft deadline) → Expired (scene cleared at the hard deadline) → cooldown → Dormant.
    /// Events never wait for the player.
    /// </summary>
    public static class WorldEventRules
    {
        public static bool ConditionsMet(WorldEventDef def, int day, float hour)
        {
            if (def.ManualOnly || day < def.MinDay) return false;
            if (def.NightOnly && !WorldClock.IsNight(hour)) return false;
            return WorldClock.InWindow(hour, def.WindowStart, def.WindowEnd);
        }

        /// <summary>Updates Dormant ⇄ Eligible from the conditions and the cooldown.</summary>
        public static void UpdateEligibility(WorldEventDef def, WorldEventRecord r, int day, float hour, double total)
        {
            if (r.state == WorldEventState.Expired)
            {
                if (def.Repeatable && total >= r.cooldownUntil) r.state = WorldEventState.Dormant;
                else return;
            }
            if (r.state != WorldEventState.Dormant && r.state != WorldEventState.Eligible) return;
            bool ok = total >= r.cooldownUntil && ConditionsMet(def, day, hour) && (def.Repeatable || r.triggerCount == 0);
            r.state = ok ? WorldEventState.Eligible : WorldEventState.Dormant;
        }

        public static void Start(WorldEventDef def, WorldEventRecord r, double total, int variant)
        {
            r.state = WorldEventState.Active;
            r.startedAt = total;
            r.softDeadline = total + Math.Max(0.01f, def.SoftDeadline);
            r.hardDeadline = total + Math.Max(def.SoftDeadline + 0.01f, def.HardDeadline);
            r.resolvedAt = -1;
            r.playerInvolved = false;
            r.resolvedOffscreen = false;
            r.variant = def.Variants > 0 ? ((variant % def.Variants) + def.Variants) % def.Variants : 0;
            r.triggerCount++;
        }

        public static void Resolve(WorldEventRecord r, bool success, double total, bool offscreen)
        {
            if (!r.IsActive) return;
            r.state = success ? WorldEventState.ResolvedSuccess : WorldEventState.ResolvedFailure;
            r.resolvedAt = total;
            r.resolvedOffscreen = offscreen;
            if (success) r.successes++;
            else r.failures++;
        }

        public static void Expire(WorldEventDef def, WorldEventRecord r, double total)
        {
            r.state = WorldEventState.Expired;
            r.cooldownUntil = total + def.CooldownHours;
        }

        /// <summary>What <see cref="Advance"/> did this step.</summary>
        public enum Step
        {
            None = 0,
            ResolvedOffscreen = 1,
            Expired = 2,
            Reopened = 3
        }

        /// <summary>
        /// Moves an event forward in time: an active event the player is not handling resolves itself at the soft
        /// deadline (or at the hard deadline even if the player is there), and a resolved event is cleared at the hard
        /// deadline. <paramref name="successChance"/> is the chance it ends well on its own (hunters nearby, night…).
        /// </summary>
        public static Step Advance(WorldEventDef def, WorldEventRecord r, double total, bool playerPresent, float successChance)
        {
            if (r.state == WorldEventState.Expired)
            {
                if (def.Repeatable && total >= r.cooldownUntil)
                {
                    r.state = WorldEventState.Dormant;
                    return Step.Reopened;
                }
                return Step.None;
            }
            if (r.IsActive && (total >= r.hardDeadline || (!playerPresent && total >= r.softDeadline)))
            {
                bool success = Roll(def.Id, r.triggerCount, r.variant) < Clamp01(successChance);
                Resolve(r, success, total, true);
                // Hard deadline already passed (long time skip): it resolves and is cleared in the same step.
                if (total >= r.hardDeadline) Expire(def, r, total);
                return Step.ResolvedOffscreen;
            }
            if (r.IsResolved && total >= r.hardDeadline)
            {
                Expire(def, r, total);
                return Step.Expired;
            }
            return Step.None;
        }

        /// <summary>Deterministic 0..1 roll from an event id and counters (same save → same outcome).</summary>
        public static float Roll(string id, int a, int b)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (id != null)
                    foreach (char c in id) h = (h ^ c) * 16777619u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)(b * 7919)) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// Offscreen / time-skip simulation of every event: steps the clock in small increments, opens eligible events
    /// (deterministic chance rolls), resolves and expires them. Used when the player rests, and by tests.
    /// </summary>
    public static class WorldEventSimulator
    {
        public const double CheckInterval = 0.25;

        public struct Report
        {
            public int Started;
            public int Resolved;
            public int Expired;
        }

        /// <summary>
        /// Simulates from <paramref name="fromTotal"/> to <paramref name="toTotal"/> hours. <paramref name="successChance"/>
        /// gives each event's chance to end well on its own; <paramref name="onStart"/> / <paramref name="onResolved"/> let the
        /// runtime write consequences (facts, dead NPCs, damaged structures).
        /// </summary>
        public static Report Simulate(IReadOnlyList<WorldEventDef> defs, WorldStateDatabase db, double fromTotal, double toTotal,
            Func<WorldEventDef, float> successChance, Action<WorldEventDef, WorldEventRecord> onStart = null,
            Action<WorldEventDef, WorldEventRecord> onResolved = null, bool allowStarts = true)
        {
            var report = new Report();
            if (toTotal <= fromTotal) return report;
            int steps = (int)Math.Ceiling((toTotal - fromTotal) / CheckInterval);
            for (int i = 1; i <= steps; i++)
            {
                double t = Math.Min(toTotal, fromTotal + i * CheckInterval);
                int day = (int)(t / WorldClock.HoursPerDay) + 1;
                float hour = (float)(t - (day - 1) * WorldClock.HoursPerDay);
                foreach (var def in defs)
                {
                    var r = db.Event(def.Id);
                    var step = WorldEventRules.Advance(def, r, t, false, successChance != null ? successChance(def) : def.BaseSuccessChance);
                    if (step == WorldEventRules.Step.ResolvedOffscreen)
                    {
                        report.Resolved++;
                        onResolved?.Invoke(def, r);
                        if (r.state == WorldEventState.Expired) report.Expired++;
                    }
                    else if (step == WorldEventRules.Step.Expired) report.Expired++;

                    if (!allowStarts) continue;
                    WorldEventRules.UpdateEligibility(def, r, day, hour, t);
                    if (r.state != WorldEventState.Eligible) continue;
                    int checkIndex = (int)(t / CheckInterval);
                    if (WorldEventRules.Roll(def.Id + ":start", checkIndex, r.triggerCount) >= def.ChancePerCheck) continue;
                    WorldEventRules.Start(def, r, t, checkIndex);
                    report.Started++;
                    onStart?.Invoke(def, r);
                }
            }
            return report;
        }
    }
}
