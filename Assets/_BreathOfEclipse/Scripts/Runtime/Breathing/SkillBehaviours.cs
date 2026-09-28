using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Data;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Breathing
{
    /// <summary>Extension point for technique logic that the data timeline cannot express.</summary>
    public interface ISkillBehaviour
    {
        /// <summary>When true the phase's HitSpecs are not auto-scheduled; the behaviour applies them.</summary>
        bool HandlesHits { get; }
        void Enter(SkillExecutor exec, SkillPhase phase);
        void Tick(SkillExecutor exec, float dt);
        void Exit(SkillExecutor exec);
    }

    /// <summary>Registry of named behaviours referenced by <see cref="SkillPhase.customBehaviour"/>.</summary>
    public static class SkillBehaviours
    {
        private static Dictionary<string, ISkillBehaviour> _registry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _registry = null;

        public static void Register(string id, ISkillBehaviour behaviour)
        {
            Ensure();
            _registry[id] = behaviour;
        }

        public static ISkillBehaviour Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Ensure();
            return _registry.TryGetValue(id, out var b) ? b : null;
        }

        private static void Ensure()
        {
            if (_registry != null) return;
            _registry = new Dictionary<string, ISkillBehaviour>
            {
                { "blink_chain", new BlinkChainBehaviour() }
            };
        }
    }

    /// <summary>
    /// Flash-steps between several enemies in sequence (Rolling Thunder, Thousand Flashes). Each blink leaves a
    /// light path and afterimages; hits use the phase's first HitSpec. hitCount = number of blinks.
    /// </summary>
    public sealed class BlinkChainBehaviour : ISkillBehaviour
    {
        public bool HandlesHits => true;

        private readonly List<ITargetable> _targets = new List<ITargetable>();
        private readonly List<IDamageable> _buffer = new List<IDamageable>();
        private int _blinks;
        private int _done;
        private float _interval;
        private float _timer;
        private HitSpec _spec;

        public void Enter(SkillExecutor exec, SkillPhase phase)
        {
            _spec = phase.hits.Count > 0 ? phase.hits[0] : new HitSpec { shape = HitShape.AtTarget, radius = 2f, damageMultiplier = 1f };
            _blinks = Mathf.Max(1, _spec.hitCount);
            _interval = phase.duration / (_blinks + 0.5f);
            _timer = 0f;
            _done = 0;
            TargetRegistry.InRadius(exec.Player.transform.position, Mathf.Max(8f, exec.Skill.autoTargetRange), _targets);
            _targets.Sort((a, b) =>
                Vector3.SqrMagnitude(a.LockOnPoint.position - exec.Player.transform.position)
                    .CompareTo(Vector3.SqrMagnitude(b.LockOnPoint.position - exec.Player.transform.position)));
        }

        public void Tick(SkillExecutor exec, float dt)
        {
            if (_done >= _blinks) return;
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _interval;

            Transform target = null;
            if (_targets.Count > 0)
            {
                // Cycle through alive targets.
                for (int k = 0; k < _targets.Count; k++)
                {
                    var t = _targets[(_done + k) % _targets.Count];
                    if (t != null && t.IsTargetable && t.LockOnPoint != null)
                    {
                        target = t.LockOnPoint;
                        break;
                    }
                }
            }
            exec.FlashStep(exec.Phase, target);
            Sfx.Play("thunder", exec.Player.transform.position + Vector3.up, 0.6f, 1f + _done * 0.05f);
            if (target != null)
            {
                var d = target.GetComponentInParent<IDamageable>();
                if (d != null && d.IsAlive)
                {
                    exec.ApplyHit(_spec, d, d.CenterPoint, _done);
                    VFXLibrary.Spawn("thunder_cut", d.CenterPoint, Quaternion.LookRotation(exec.Forward), 0.7f, exec.Element);
                }
            }
            else
            {
                HitQuery.Sphere(exec.Player.transform.position + Vector3.up, _spec.radius, Team.Player, _buffer);
                foreach (var d in _buffer) exec.ApplyHit(_spec, d, d.CenterPoint, _done);
            }
            _done++;
        }

        public void Exit(SkillExecutor exec) => _targets.Clear();
    }
}
