using System.Collections.Generic;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Records what the game reported during a test window: damage events, hit stops, camera cues, spawned
    /// effects, telegraphs, techniques, parries, perfect dodges, combo counter and boss events.
    /// Tests call <see cref="Mark"/> and then query everything recorded after the mark.
    /// </summary>
    public sealed class TelemetryRecorder
    {
        public struct DamageRecord
        {
            public float Time;
            public HitData Hit;
            public HitResult Result;
        }

        public struct TimedValue
        {
            public float Time;
            public string Id;
            public float Value;
            public Vector3 Position;
        }

        public readonly List<DamageRecord> Damage = new List<DamageRecord>();
        public readonly List<TimedValue> HitStops = new List<TimedValue>();
        public readonly List<TimedValue> CameraCues = new List<TimedValue>();
        public readonly List<TimedValue> Vfx = new List<TimedValue>();
        public readonly List<TimedValue> Telegraphs = new List<TimedValue>();
        public readonly List<TimedValue> Skills = new List<TimedValue>();
        public readonly List<TimedValue> Ultimates = new List<TimedValue>();
        public readonly List<TimedValue> Parries = new List<TimedValue>();
        public readonly List<TimedValue> PerfectDodges = new List<TimedValue>();
        public readonly List<TimedValue> Combo = new List<TimedValue>();
        public readonly List<TimedValue> BossPhases = new List<TimedValue>();
        public readonly List<TimedValue> BossEncounters = new List<TimedValue>();
        public readonly List<TimedValue> BossDefeats = new List<TimedValue>();
        public readonly List<TimedValue> Kills = new List<TimedValue>();

        private bool _attached;

        public static float Now => Time.realtimeSinceStartup;

        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            GameEvents.Damage += OnDamage;
            GameEvents.SkillUsed += OnSkill;
            GameEvents.UltimateActivated += OnUltimate;
            GameEvents.Parry += OnParry;
            GameEvents.PerfectDodge += OnPerfectDodge;
            GameEvents.ComboChanged += OnCombo;
            GameEvents.BossPhaseChanged += OnBossPhase;
            GameEvents.BossEncounter += OnBossEncounter;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.EnemyKilled += OnKilled;
            DevTelemetry.HitStopRequested += OnHitStop;
            DevTelemetry.CameraCue += OnCameraCue;
            DevTelemetry.VfxSpawned += OnVfx;
            DevTelemetry.TelegraphShown += OnTelegraph;
        }

        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            GameEvents.Damage -= OnDamage;
            GameEvents.SkillUsed -= OnSkill;
            GameEvents.UltimateActivated -= OnUltimate;
            GameEvents.Parry -= OnParry;
            GameEvents.PerfectDodge -= OnPerfectDodge;
            GameEvents.ComboChanged -= OnCombo;
            GameEvents.BossPhaseChanged -= OnBossPhase;
            GameEvents.BossEncounter -= OnBossEncounter;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.EnemyKilled -= OnKilled;
            DevTelemetry.HitStopRequested -= OnHitStop;
            DevTelemetry.CameraCue -= OnCameraCue;
            DevTelemetry.VfxSpawned -= OnVfx;
            DevTelemetry.TelegraphShown -= OnTelegraph;
        }

        /// <summary>Returns a timestamp; query helpers take it as "since".</summary>
        public float Mark() => Now;

        public void Clear()
        {
            Damage.Clear();
            HitStops.Clear();
            CameraCues.Clear();
            Vfx.Clear();
            Telegraphs.Clear();
            Skills.Clear();
            Ultimates.Clear();
            Parries.Clear();
            PerfectDodges.Clear();
            Combo.Clear();
            BossPhases.Clear();
            BossEncounters.Clear();
            BossDefeats.Clear();
            Kills.Clear();
        }

        private void OnDamage(HitData hit, HitResult result)
        {
            if (Damage.Count > 4000) Damage.RemoveRange(0, 1000);
            Damage.Add(new DamageRecord { Time = Now, Hit = hit, Result = result });
        }

        private void OnSkill(string id, string name, Element e) => Skills.Add(new TimedValue { Time = Now, Id = id });
        private void OnUltimate(string id, string name, Element e) => Ultimates.Add(new TimedValue { Time = Now, Id = id });
        private void OnParry(Vector3 p, GameObject a) => Parries.Add(new TimedValue { Time = Now, Position = p });
        private void OnPerfectDodge(Vector3 p, GameObject a) => PerfectDodges.Add(new TimedValue { Time = Now, Position = p });
        private void OnCombo(int count) => Combo.Add(new TimedValue { Time = Now, Value = count });
        private void OnBossPhase(GameObject boss, int phase) => BossPhases.Add(new TimedValue { Time = Now, Value = phase });
        private void OnBossEncounter(string name, GameObject boss) => BossEncounters.Add(new TimedValue { Time = Now, Id = name });
        private void OnBossDefeated(GameObject boss) => BossDefeats.Add(new TimedValue { Time = Now });
        private void OnKilled(GameObject victim, GameObject killer) => Kills.Add(new TimedValue { Time = Now, Id = victim != null ? victim.name : "?" });
        private void OnHitStop(float d) => HitStops.Add(new TimedValue { Time = Now, Value = d });
        private void OnCameraCue(string kind, float amount) => Add(CameraCues, new TimedValue { Time = Now, Id = kind, Value = amount });
        private void OnVfx(string id, Vector3 p) => Add(Vfx, new TimedValue { Time = Now, Id = id, Position = p });
        private void OnTelegraph(Vector3 p, float radius, float duration) => Telegraphs.Add(new TimedValue { Time = Now, Value = radius, Position = p });

        private static void Add(List<TimedValue> list, TimedValue v)
        {
            if (list.Count > 4000) list.RemoveRange(0, 1000);
            list.Add(v);
        }

        // ------------------------------------------------------------------ queries

        public static int CountSince(List<TimedValue> list, float since, string id = null)
        {
            int n = 0;
            foreach (var v in list)
                if (v.Time >= since && (id == null || v.Id == id)) n++;
            return n;
        }

        public static float MaxSince(List<TimedValue> list, float since)
        {
            float m = 0f;
            foreach (var v in list)
                if (v.Time >= since) m = Mathf.Max(m, v.Value);
            return m;
        }

        public List<DamageRecord> DamageSince(float since, System.Predicate<DamageRecord> filter = null)
        {
            var list = new List<DamageRecord>();
            foreach (var d in Damage)
                if (d.Time >= since && (filter == null || filter(d))) list.Add(d);
            return list;
        }

        public HashSet<string> VfxIdsSince(float since)
        {
            var set = new HashSet<string>();
            foreach (var v in Vfx)
                if (v.Time >= since) set.Add(v.Id);
            return set;
        }

        public HashSet<string> CameraCueKindsSince(float since)
        {
            var set = new HashSet<string>();
            foreach (var v in CameraCues)
                if (v.Time >= since) set.Add(v.Id);
            return set;
        }
    }
}
