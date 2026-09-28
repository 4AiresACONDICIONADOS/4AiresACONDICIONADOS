using System;
using System.Collections.Generic;

namespace BreathOfEclipse.Combat
{
    /// <summary>One node of a combo string: an attack id and the attacks that can follow it.</summary>
    public sealed class ComboNode
    {
        public string Id;
        public string NextLight;
        public string NextHeavy;

        public ComboNode(string id, string nextLight = null, string nextHeavy = null)
        {
            Id = id;
            NextLight = nextLight;
            NextHeavy = nextHeavy;
        }

        public string Next(ComboInput input) => input == ComboInput.Light ? NextLight : NextHeavy;
    }

    /// <summary>
    /// Data-only combo tree. Resolves "which attack comes next" from the current attack, the button pressed and
    /// the context (airborne, dashing, counter windows). Engine independent and fully unit tested.
    /// </summary>
    public sealed class ComboGraph
    {
        private readonly Dictionary<string, ComboNode> _nodes = new Dictionary<string, ComboNode>(StringComparer.Ordinal);
        private readonly Dictionary<ComboEntry, string> _entries = new Dictionary<ComboEntry, string>();

        public int NodeCount => _nodes.Count;

        public ComboGraph AddNode(string id, string nextLight = null, string nextHeavy = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Combo node id cannot be empty", nameof(id));
            _nodes[id] = new ComboNode(id, nextLight, nextHeavy);
            return this;
        }

        public ComboGraph SetEntry(ComboEntry entry, string nodeId)
        {
            _entries[entry] = nodeId;
            return this;
        }

        public bool HasNode(string id) => !string.IsNullOrEmpty(id) && _nodes.ContainsKey(id);

        public string GetEntry(ComboEntry entry) => _entries.TryGetValue(entry, out var id) ? id : null;

        /// <summary>
        /// Resolves the next attack.
        /// </summary>
        /// <param name="currentNodeId">Attack currently executing (null/empty when idle).</param>
        /// <param name="input">Pressed button.</param>
        /// <param name="context">Current situation of the attacker.</param>
        /// <returns>The attack id to execute, or null when nothing can follow.</returns>
        public string Resolve(string currentNodeId, ComboInput input, ComboContext context)
        {
            // Special counters take priority over everything: they are the reward for skillful defense.
            if (context.PerfectDodgeWindow && input == ComboInput.Light)
            {
                var id = GetEntry(ComboEntry.PerfectDodgeLight);
                if (HasNode(id)) return id;
            }
            if (context.ParryWindow && input == ComboInput.Heavy)
            {
                var id = GetEntry(ComboEntry.ParryHeavy);
                if (HasNode(id)) return id;
            }

            // Continue the current string when possible.
            if (!string.IsNullOrEmpty(currentNodeId) && _nodes.TryGetValue(currentNodeId, out var node))
            {
                var next = node.Next(input);
                if (HasNode(next)) return next;
                // A string that has ended can only restart from an entry point when the caller allows it.
                if (!context.AllowRestart) return null;
            }

            ComboEntry entry;
            if (context.Airborne) entry = input == ComboInput.Light ? ComboEntry.AirLight : ComboEntry.AirHeavy;
            else if (context.Dashing) entry = input == ComboInput.Light ? ComboEntry.DashLight : ComboEntry.DashHeavy;
            else entry = input == ComboInput.Light ? ComboEntry.GroundLight : ComboEntry.GroundHeavy;

            var entryId = GetEntry(entry);
            if (HasNode(entryId)) return entryId;

            // Dash heavy falls back to ground heavy, etc.
            if (entry == ComboEntry.DashHeavy) entryId = GetEntry(ComboEntry.GroundHeavy);
            else if (entry == ComboEntry.DashLight) entryId = GetEntry(ComboEntry.GroundLight);
            return HasNode(entryId) ? entryId : null;
        }
    }

    /// <summary>Situation the attacker is in when a combo input is resolved.</summary>
    public struct ComboContext
    {
        public bool Airborne;
        public bool Dashing;
        public bool PerfectDodgeWindow;
        public bool ParryWindow;
        /// <summary>Allows a finished string to restart from an entry point.</summary>
        public bool AllowRestart;

        public static ComboContext Ground => new ComboContext { AllowRestart = true };
        public static ComboContext Air => new ComboContext { Airborne = true, AllowRestart = true };
    }
}
