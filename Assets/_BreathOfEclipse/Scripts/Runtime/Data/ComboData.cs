using System;
using System.Collections.Generic;
using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Data
{
    /// <summary>
    /// Designer-facing combo tree. Each node references an attack and what follows on Light / Heavy.
    /// Converted at runtime into the engine-independent <see cref="ComboGraph"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Combo Set", fileName = "Combos_")]
    public sealed class ComboData : ScriptableObject
    {
        [Serializable]
        public sealed class Node
        {
            public AttackData attack;
            public AttackData nextLight;
            public AttackData nextHeavy;
        }

        public List<Node> nodes = new List<Node>();

        [Header("Entry points")]
        public AttackData groundLight;
        public AttackData groundHeavy;
        public AttackData dashLight;
        public AttackData dashHeavy;
        public AttackData airLight;
        public AttackData airHeavy;
        public AttackData perfectDodgeCounter;
        public AttackData parryCounter;

        private Dictionary<string, AttackData> _lookup;

        public AttackData Find(string attackId)
        {
            if (string.IsNullOrEmpty(attackId)) return null;
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(attackId, out var a) ? a : null;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, AttackData>(StringComparer.Ordinal);
            void Add(AttackData a)
            {
                if (a != null && !string.IsNullOrEmpty(a.attackId)) _lookup[a.attackId] = a;
            }
            foreach (var n in nodes)
            {
                Add(n.attack);
                Add(n.nextLight);
                Add(n.nextHeavy);
            }
            Add(groundLight); Add(groundHeavy); Add(dashLight); Add(dashHeavy);
            Add(airLight); Add(airHeavy); Add(perfectDodgeCounter); Add(parryCounter);
        }

        public ComboGraph BuildGraph()
        {
            BuildLookup();
            var graph = new ComboGraph();
            foreach (var a in _lookup.Values) graph.AddNode(a.attackId);
            foreach (var n in nodes)
            {
                if (n.attack == null) continue;
                graph.AddNode(n.attack.attackId, n.nextLight != null ? n.nextLight.attackId : null, n.nextHeavy != null ? n.nextHeavy.attackId : null);
            }
            void Entry(ComboEntry e, AttackData a)
            {
                if (a != null) graph.SetEntry(e, a.attackId);
            }
            Entry(ComboEntry.GroundLight, groundLight);
            Entry(ComboEntry.GroundHeavy, groundHeavy);
            Entry(ComboEntry.DashLight, dashLight);
            Entry(ComboEntry.DashHeavy, dashHeavy);
            Entry(ComboEntry.AirLight, airLight);
            Entry(ComboEntry.AirHeavy, airHeavy);
            Entry(ComboEntry.PerfectDodgeLight, perfectDodgeCounter);
            Entry(ComboEntry.ParryHeavy, parryCounter);
            return graph;
        }

        private void OnValidate() => _lookup = null;
    }
}
