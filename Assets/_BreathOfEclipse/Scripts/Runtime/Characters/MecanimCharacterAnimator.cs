using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Adapter that drives a standard Animator Controller through <see cref="ICharacterAnimator"/>, so imported
    /// humanoid models can replace the procedural mannequin without touching gameplay code.
    /// Expected controller: float "Speed", "VelocityX", "VelocityZ"; bools "Grounded", "Blocking", "Sprinting",
    /// "KnockedDown", "Dead"; trigger "Hit"; states named after motion ids (e.g. "L1", "SkillRisingCut", "Dodge").
    /// Includes humanoid IK: look at the lock-on target and keep feet on the ground.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class MecanimCharacterAnimator : MonoBehaviour, ICharacterAnimator
    {
        [SerializeField] private Transform weaponBase = null;
        [SerializeField] private Transform weaponTip = null;
        [SerializeField] private float crossFade = 0.06f;
        [SerializeField] private bool footIK = true;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int VelXId = Animator.StringToHash("VelocityX");
        private static readonly int VelZId = Animator.StringToHash("VelocityZ");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int BlockingId = Animator.StringToHash("Blocking");
        private static readonly int SprintingId = Animator.StringToHash("Sprinting");
        private static readonly int KnockedDownId = Animator.StringToHash("KnockedDown");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int HitId = Animator.StringToHash("Hit");

        private Animator _animator;
        private Transform _lookTarget;
        private float _actionEnd;
        private bool _grounded = true;

        public Transform WeaponBase => weaponBase != null ? weaponBase : transform;
        public Transform WeaponTip => weaponTip != null ? weaponTip : transform;
        public bool IsActionPlaying => Time.time < _actionEnd;
        public bool KnockdownPoseActive => _animator != null && _animator.GetBool(KnockedDownId);

        /// <summary>Blade points on the imported model's weapon (hit detection, trails, VFX anchors).</summary>
        public void SetWeaponPoints(Transform bladeBase, Transform bladeTip)
        {
            weaponBase = bladeBase;
            weaponTip = bladeTip;
        }

        private void Awake() => _animator = GetComponent<Animator>();

        public void SetLocomotion(LocomotionState state)
        {
            Vector3 local = transform.InverseTransformDirection(state.Velocity);
            _grounded = state.Grounded;
            _animator.SetFloat(SpeedId, new Vector2(state.Velocity.x, state.Velocity.z).magnitude);
            _animator.SetFloat(VelXId, local.x);
            _animator.SetFloat(VelZId, local.z);
            _animator.SetBool(GroundedId, state.Grounded);
            _animator.SetBool(BlockingId, state.Blocking);
            _animator.SetBool(SprintingId, state.Sprinting);
        }

        public void PlayAttack(string motionId, float windup, float active, float recovery)
        {
            CrossFade(motionId, crossFade);
            _actionEnd = Time.time + windup + active + recovery;
        }

        public void PlayMotion(string motionId, float duration, float blendIn)
        {
            CrossFade(motionId, Mathf.Max(0.01f, blendIn));
            _actionEnd = Time.time + duration;
        }

        /// <summary>
        /// Plays the state named after the motion id. Imported controllers rarely have every procedural motion:
        /// missing states fall back to a family state ("Skill*" → "Skill", "L*"/"H*" → "Attack") and are reported
        /// once instead of spamming Animator errors.
        /// </summary>
        private void CrossFade(string motionId, float blend)
        {
            if (string.IsNullOrEmpty(motionId) || _animator == null || _animator.runtimeAnimatorController == null) return;
            string state = motionId;
            if (!_animator.HasState(0, Animator.StringToHash(state)))
            {
                string family = motionId.StartsWith("Skill") ? "Skill" : "Attack";
                if (_missingStates.Add(motionId))
                    Debug.Log($"[MecanimCharacterAnimator] No state '{motionId}' in {_animator.runtimeAnimatorController.name}; using '{family}' when available.");
                if (!_animator.HasState(0, Animator.StringToHash(family))) return;
                state = family;
            }
            _animator.CrossFadeInFixedTime(state, blend);
        }

        private readonly System.Collections.Generic.HashSet<string> _missingStates = new System.Collections.Generic.HashSet<string>();

        public void StopAction(float blendOut) => _actionEnd = 0f;

        public void PlayHit(HitReaction reaction, Vector3 worldDirection) => _animator.SetTrigger(HitId);

        public void SetDodge(Vector3 worldDirection, float duration) => PlayMotion("Dodge", duration, 0.03f);

        public void SetKnockedDown(bool value, float getUpDuration = 0.4f) => _animator.SetBool(KnockedDownId, value);

        public void SetDead(bool value) => _animator.SetBool(DeadId, value);

        public void SetLookTarget(Transform target) => _lookTarget = target;

        private void OnAnimatorIK(int layerIndex)
        {
            if (_lookTarget != null)
            {
                _animator.SetLookAtWeight(0.8f, 0.3f, 0.7f, 0.5f, 0.6f);
                _animator.SetLookAtPosition(_lookTarget.position);
            }
            else
            {
                _animator.SetLookAtWeight(0f);
            }

            if (!footIK || !_grounded) return;
            PlaceFoot(AvatarIKGoal.LeftFoot);
            PlaceFoot(AvatarIKGoal.RightFoot);
        }

        private void PlaceFoot(AvatarIKGoal goal)
        {
            Vector3 foot = _animator.GetIKPosition(goal);
            if (Physics.Raycast(foot + Vector3.up * 0.5f, Vector3.down, out var hit, 1f, Core.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                _animator.SetIKPositionWeight(goal, 1f);
                _animator.SetIKRotationWeight(goal, 0.6f);
                _animator.SetIKPosition(goal, hit.point + Vector3.up * 0.08f);
                _animator.SetIKRotation(goal, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal));
            }
        }
    }
}
