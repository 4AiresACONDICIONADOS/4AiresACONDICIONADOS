using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Breathing
{
    /// <summary>
    /// The swordsman breathes before a form, advanced form or ultimate (never before normal attacks):
    /// the chest opens and rises (SkillInhale motion), thin streams of the element's air converge on the mouth,
    /// a real-sounding inhale plays, then — breath held — the energy runs up the blade and through the body and
    /// the call begins ("Respiración del Agua…"). Duration comes from the style (Water ~0.4 s, Thunder ~0.18 s)
    /// or the technique. Driven by <see cref="SkillExecutor"/>: nothing is paid until the inhale completes, and a
    /// hit during the inhale cancels breath, streams, voice and technique together.
    /// </summary>
    public sealed class BreathingInhaleSystem : MonoBehaviour
    {
        private BreathingStyleSystem _breathing;
        private PlayerController _pc;
        private VFXInstance _streams;
        private float _duration;

        /// <summary>Diagnostics / playtests: an inhale is running.</summary>
        public bool Inhaling => _breathing != null && _breathing.Executor != null && _breathing.Executor.Inhaling;

        public static BreathingInhaleSystem Attach(BreathingStyleSystem breathing, PlayerController pc)
        {
            var s = breathing.gameObject.AddComponent<BreathingInhaleSystem>();
            s._breathing = breathing;
            s._pc = pc;
            breathing.Executor.InhaleStarted += s.OnInhaleStarted;
            breathing.Executor.InhaleEnded += s.OnInhaleEnded;
            return s;
        }

        private void OnDestroy()
        {
            if (_breathing == null || _breathing.Executor == null) return;
            _breathing.Executor.InhaleStarted -= OnInhaleStarted;
            _breathing.Executor.InhaleEnded -= OnInhaleEnded;
        }

        private void OnInhaleStarted(SkillData skill, float duration)
        {
            _duration = duration;
            var element = _breathing.Executor.Element;
            float scale = _pc.Rig != null ? _pc.Rig.Scale : 1f;

            _pc.Animator.PlayMotion("SkillInhale", duration + 0.06f, Mathf.Clamp(duration * 0.3f, 0.04f, 0.1f));

            string sound = duration < 0.3f ? "inhale_short" : duration > 0.5f ? "inhale_deep" : "inhale";
            Sfx.Play2D(sound, 0.7f, Random.Range(0.97f, 1.03f), AudioCategory.Voice);

            var head = _pc.Rig != null ? _pc.Rig.Bone(RigBone.Head) : null;
            Vector3 fwd = _pc.transform.forward;
            Vector3 mouth = head != null
                ? head.position + fwd * (0.12f * scale) - Vector3.up * (0.05f * scale)
                : _pc.transform.position + Vector3.up * (1.55f * scale) + fwd * (0.12f * scale);
            if (_streams != null) _streams.Release();
            _streams = VFXLibrary.Spawn("inhale", mouth, Quaternion.LookRotation(fwd, Vector3.up), scale, element, head, duration + 0.7f);

            // A breath of stillness: the camera leans in a touch (longer breaths only).
            if (duration >= 0.3f) CameraFX.Zoom(0.96f, duration, Mathf.Min(0.12f, duration * 0.4f), 0.25f);
        }

        private void OnInhaleEnded(SkillData skill, bool completed)
        {
            if (!completed)
            {
                // Interrupted: the air disperses at once; the executor already dropped the technique (nothing paid).
                if (_streams != null) _streams.Release();
                _streams = null;
                return;
            }
            _streams = null;
            var element = _breathing.Executor.Element;
            var bladeBase = _pc.Animator.WeaponBase;
            var bladeTip = _pc.Animator.WeaponTip;
            if (bladeBase != null && bladeTip != null)
            {
                Vector3 dir = bladeTip.position - bladeBase.position;
                if (dir.sqrMagnitude > 1e-4f)
                    VFXLibrary.Spawn("breath_focus", bladeBase.position, Quaternion.LookRotation(dir), 1f, element, bladeBase);
            }
            float scale = _pc.Rig != null ? _pc.Rig.Scale : 1f;
            VFXLibrary.Spawn("breath_focus_body", _pc.transform.position, _pc.transform.rotation, scale, element, _pc.transform);
            Sfx.Play2D("breath_focus", _duration < 0.3f ? 0.22f : 0.32f, 1f);
        }
    }
}
