using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Code-driven animation for the procedural mannequins: locomotion cycles, attack/technique clips from
    /// <see cref="MotionLibrary"/>, hit reactions, knockdown and death, all solved with two-bone IK
    /// (hands on the sword, feet planted on the ground) plus head look-at toward the lock-on target.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ProceduralAnimator : MonoBehaviour, ICharacterAnimator
    {
        private sealed class ActionTrack
        {
            public MotionClip Clip;
            public float[] Times;
            public bool IsAttack;
            public float Windup, Active, Recovery;
            /// <summary>-1..1: sword hand travels right→left (negative) or left→right (positive).</summary>
            public float SwingSign;
            /// <summary>Body involvement: heavier / longer swings drive the hips and torso further.</summary>
            public float BodyAmount = 1f;
            public float Elapsed;
            public MotionPose Start;
            public float BlendOutStart = float.PositiveInfinity;
            public float BlendOutDuration = 0.15f;
            public float Weight = 1f;
        }

        public Transform WeaponBase => _rig != null ? _rig.WeaponBase : transform;
        public Transform WeaponTip => _rig != null ? _rig.WeaponTip : transform;
        public bool IsActionPlaying => _action != null;
        public CharacterRig Rig => _rig;
        /// <summary>Last evaluated pose (rig space).</summary>
        public MotionPose CurrentPose => _output;

        private CharacterRig _rig;
        private LocomotionProfile _profile;
        private Transform _visual, _hips, _spine, _chest, _head;
        private Transform _upperR, _lowerR, _handR, _upperL, _lowerL, _handL;
        private Transform _thighR, _shinR, _footR, _thighL, _shinL, _footL;
        private float _hipHeight;

        private LocomotionState _state;
        private MotionPose _output;
        private ActionTrack _action;
        private float _phase;
        private float _speed;
        private float _airWeight;
        private float _blockWeight;
        private float _sprintWeight;
        private float _combatWeight = 1f;
        private float _landTimer;
        private bool _wasGrounded = true;
        private float _lastVerticalVelocity;
        private float _turnRoll;
        private float _lastYaw;

        private float _dodgeTimer, _dodgeDuration;
        private Vector3 _dodgeLocalDir;
        private float _hitTimer, _hitDuration, _hitStrength;
        private Vector3 _hitLocalDir;
        private bool _down, _dead;
        private float _downWeight, _deadWeight;
        private float _downBlendOutRate = 2.5f;
        private float _postureLean, _postureCrouch, _postureWeight;
        private Transform _lookTarget;
        private float _lookWeight;
        private float _breathTime;

        private static readonly RaycastHit[] GroundHits = new RaycastHit[4];

        public void Initialize(CharacterRig rig)
        {
            _rig = rig;
            _profile = MotionLibrary.For(rig.Profile.decoration);
            _visual = rig.Bone(RigBone.Visual);
            _hips = rig.Bone(RigBone.Hips);
            _spine = rig.Bone(RigBone.Spine);
            _chest = rig.Bone(RigBone.Chest);
            _head = rig.Bone(RigBone.Head);
            _upperR = rig.Bone(RigBone.UpperArmR);
            _lowerR = rig.Bone(RigBone.LowerArmR);
            _handR = rig.Bone(RigBone.HandR);
            _upperL = rig.Bone(RigBone.UpperArmL);
            _lowerL = rig.Bone(RigBone.LowerArmL);
            _handL = rig.Bone(RigBone.HandL);
            _thighR = rig.Bone(RigBone.ThighR);
            _shinR = rig.Bone(RigBone.ShinR);
            _footR = rig.Bone(RigBone.FootR);
            _thighL = rig.Bone(RigBone.ThighL);
            _shinL = rig.Bone(RigBone.ShinL);
            _footL = rig.Bone(RigBone.FootL);
            _hipHeight = rig.Profile.hipHeight;
            _output = _profile.Guard;
            _lastYaw = transform.eulerAngles.y;
            _state.Grounded = true;
            _breathTime = Random.value * 10f;
            ApplyPose(_output, true);
        }

        // ------------------------------------------------------------------ ICharacterAnimator

        public void SetLocomotion(LocomotionState state) => _state = state;

        public void PlayAttack(string motionId, float windup, float active, float recovery)
        {
            var clip = MotionLibrary.Get(motionId) ?? MotionLibrary.Get("L1");
            if (clip == null) return;
            int n = clip.Keys.Length;
            var times = new float[n];
            windup = Mathf.Max(0.01f, windup);
            active = Mathf.Max(0.01f, active);
            times[0] = windup;
            for (int i = 1; i < n; i++) times[i] = windup + active * (i / (float)Mathf.Max(1, n - 1));
            _action = new ActionTrack
            {
                Clip = clip,
                Times = times,
                IsAttack = true,
                Windup = windup,
                Active = active,
                Recovery = Mathf.Max(0.05f, recovery),
                SwingSign = Mathf.Clamp((clip.Keys[n - 1].pose.handR.x - clip.Keys[0].pose.handR.x) / 0.4f, -1f, 1f),
                BodyAmount = Mathf.Clamp(0.8f + (windup + active) * 1.4f, 0.8f, 1.45f),
                Start = _output,
                BlendOutStart = windup + active + recovery * 0.35f,
                BlendOutDuration = Mathf.Max(0.05f, recovery * 0.65f)
            };
        }

        public void PlayMotion(string motionId, float duration, float blendIn)
        {
            var clip = MotionLibrary.Get(motionId);
            if (clip == null)
            {
                Debug.LogWarning($"[ProceduralAnimator] Unknown motion '{motionId}'.");
                return;
            }
            int n = clip.Keys.Length;
            var times = new float[n];
            duration = Mathf.Max(0.02f, duration);
            if (n == 1)
            {
                times[0] = Mathf.Clamp(blendIn, 0.01f, duration);
            }
            else
            {
                for (int i = 0; i < n; i++) times[i] = Mathf.Max(0.01f, clip.Keys[i].time * duration);
            }
            _action = new ActionTrack { Clip = clip, Times = times, IsAttack = false, Start = _output };
        }

        public void StopAction(float blendOut)
        {
            if (_action == null) return;
            if (blendOut <= 0f)
            {
                _action = null;
                return;
            }
            float start = Mathf.Min(_action.BlendOutStart, _action.Elapsed);
            _action.BlendOutStart = start;
            _action.BlendOutDuration = blendOut;
        }

        public void PlayHit(HitReaction reaction, Vector3 worldDirection)
        {
            _hitLocalDir = transform.InverseTransformDirection(worldDirection.sqrMagnitude > 0.001f ? worldDirection.normalized : -transform.forward);
            _hitStrength = reaction == HitReaction.Light ? 0.6f : 1f;
            _hitDuration = reaction == HitReaction.Light ? 0.28f : 0.5f;
            _hitTimer = _hitDuration;
        }

        public void SetDodge(Vector3 worldDirection, float duration)
        {
            _dodgeLocalDir = transform.InverseTransformDirection(worldDirection.sqrMagnitude > 0.001f ? worldDirection.normalized : transform.forward);
            _dodgeDuration = Mathf.Max(0.05f, duration);
            _dodgeTimer = _dodgeDuration;
        }

        /// <summary>Knockdown pose on/off. <paramref name="getUpDuration"/> is how long the blend back to standing takes.</summary>
        public void SetKnockedDown(bool value, float getUpDuration = 0.4f)
        {
            _down = value;
            _downBlendOutRate = 1f / Mathf.Max(0.05f, getUpDuration);
            if (value) _dodgeTimer = 0f;
        }

        /// <summary>Permanent stance change (boss phase 2: hunched forward, lower, more aggressive).</summary>
        public void SetPosture(float leanDegrees, float crouch)
        {
            _postureLean = leanDegrees;
            _postureCrouch = crouch;
            _postureWeight = 0f;
        }

        /// <summary>Diagnostics: the knockdown pose still has weight.</summary>
        public bool KnockdownPoseActive => _downWeight > 0.001f;

        public void SetDead(bool value)
        {
            _dead = value;
            if (value) _action = null;
            else
            {
                _deadWeight = 0f;
                _downWeight = 0f;
            }
        }

        public void SetLookTarget(Transform target) => _lookTarget = target;

        // ------------------------------------------------------------------ evaluation

        private void LateUpdate()
        {
            if (_rig == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                // Hit stop: the pose freezes; a character that was just hit shudders so the contact reads.
                if (_hitTimer > 0f && _visual != null)
                {
                    float j = 0.03f * _hitStrength * _rig.Scale;
                    float tu = Time.unscaledTime;
                    _visual.localPosition = _output.visualOffset * _rig.Scale + new Vector3(Mathf.Sin(tu * 95f) * j, 0f, Mathf.Cos(tu * 71f) * j * 0.5f);
                }
                return;
            }
            _output = Evaluate(dt);
            ApplyPose(_output, false);
        }

        private MotionPose Evaluate(float dt)
        {
            var p = _profile;
            float scale = _rig.Scale;

            // ---- stance blend weights
            _combatWeight = Mathf.MoveTowards(_combatWeight, _state.CombatStance ? 1f : 0f, dt * 3f);
            _blockWeight = Mathf.MoveTowards(_blockWeight, _state.Blocking ? 1f : 0f, dt * 14f);
            Vector3 horizontal = new Vector3(_state.Velocity.x, 0f, _state.Velocity.z);
            float targetSpeed = horizontal.magnitude / scale;
            _speed = Mathf.Lerp(_speed, targetSpeed, 1f - Mathf.Exp(-12f * dt));
            _sprintWeight = Mathf.MoveTowards(_sprintWeight, _state.Sprinting && _speed > 5f ? 1f : 0f, dt * 5f);
            _airWeight = Mathf.MoveTowards(_airWeight, _state.Grounded ? 0f : 1f, dt * (_state.Grounded ? 12f : 6f));

            if (_state.Grounded && !_wasGrounded && _lastVerticalVelocity < -4f)
                _landTimer = Mathf.Clamp(-_lastVerticalVelocity * 0.025f, 0.08f, 0.22f);
            _wasGrounded = _state.Grounded;
            _lastVerticalVelocity = _state.Velocity.y;
            if (_landTimer > 0f) _landTimer -= dt;

            MotionPose pose = MotionPose.Lerp(p.Relaxed, p.Guard, _combatWeight);
            if (_sprintWeight > 0f) pose = MotionPose.Lerp(pose, p.Sprint, _sprintWeight);
            if (_blockWeight > 0f) pose = MotionPose.Lerp(pose, p.Block, _blockWeight);

            // ---- locomotion cycle
            float move = Mathf.Clamp01(_speed / 2f) * (1f - _airWeight);
            if (move > 0.001f)
            {
                Vector3 localVel = transform.InverseTransformDirection(horizontal);
                localVel.y = 0f;
                Vector3 moveDir = localVel.sqrMagnitude > 0.0001f ? localVel.normalized : Vector3.forward;
                float stride = Mathf.Max(0.3f, p.StrideLength * (1f + _sprintWeight * 0.35f));
                _phase += _speed / stride * Mathf.PI * dt;
                float amp = Mathf.Clamp(_speed * 0.075f, 0f, 0.44f);
                float lift = Mathf.Clamp(_speed * 0.035f, 0f, 0.26f);
                float sinL = Mathf.Sin(_phase), cosL = Mathf.Cos(_phase);
                var neutralL = new Vector3(-p.StanceWidth, 0f, 0f);
                var neutralR = new Vector3(p.StanceWidth, 0f, 0f);
                Vector3 fl = Vector3.Lerp(pose.footL, neutralL, move) + moveDir * (sinL * amp * move) + Vector3.up * (Mathf.Max(0f, cosL) * lift * move);
                Vector3 fr = Vector3.Lerp(pose.footR, neutralR, move) + moveDir * (-sinL * amp * move) + Vector3.up * (Mathf.Max(0f, -cosL) * lift * move);
                pose.footL = fl;
                pose.footR = fr;
                pose.hips.y -= (0.03f + 0.03f * (0.5f + 0.5f * Mathf.Cos(_phase * 2f))) * move;
                pose.hipsEuler.y += sinL * 8f * move;
                pose.chestEuler.y -= sinL * 6f * move;
                // Shoulders rock with the stride, the head stays level, the pelvis rolls over the planted leg.
                pose.chestEuler.z += cosL * 3f * move;
                pose.headEuler.z -= cosL * 2f * move;
                pose.hipsEuler.z -= cosL * 2.5f * move;
                pose.headEuler.x += Mathf.Abs(sinL) * 1.5f * move * (1f + _sprintWeight);
                if (pose.twoHand < 0.5f)
                {
                    pose.handL += new Vector3(0f, 0f, -sinL * 0.2f * move);
                    pose.handR += new Vector3(0f, 0f, sinL * 0.08f * move);
                }
                pose.visualEuler.x += _speed * p.LeanPerSpeed * 0.8f + _sprintWeight * p.SprintLean;
            }
            else
            {
                // idle breathing, plus a slow weight shift between the feet; the combat stance breathes deeper
                // and bounces slightly on the balls of the feet, ready to move.
                _breathTime += dt;
                float br = Mathf.Sin(_breathTime * 2.1f);
                float shift = Mathf.Sin(_breathTime * 0.45f);
                pose.chestEuler.x += br * (1.3f + _combatWeight * 0.9f);
                pose.hips.y += br * 0.006f - _combatWeight * 0.012f * (0.5f + 0.5f * Mathf.Sin(_breathTime * 4.2f));
                pose.hips.x += shift * 0.018f;
                pose.hipsEuler.z += shift * 1.5f;
                pose.headEuler.z -= shift * 1f;
                pose.handR.y += br * 0.008f;
            }

            // turning lean
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastYaw = yaw;
            _turnRoll = Mathf.Lerp(_turnRoll, Mathf.Clamp(-yawRate * 0.02f * Mathf.Clamp01(_speed / 4f), -14f, 14f), 1f - Mathf.Exp(-8f * dt));
            pose.visualEuler.z += _turnRoll;

            // ---- air
            if (_airWeight > 0f)
            {
                var air = _state.Velocity.y > 0.5f ? p.AirRise : p.AirFall;
                pose = MotionPose.Lerp(pose, air, _airWeight);
            }
            if (_landTimer > 0f) pose.hips.y -= 0.16f * Mathf.Sin(Mathf.Clamp01(_landTimer / 0.22f) * Mathf.PI);

            // ---- action layer (attacks, techniques)
            if (_action != null)
            {
                _action.Elapsed += dt;
                var actionPose = EvaluateAction(_action);
                float w = 1f;
                if (_action.Elapsed > _action.BlendOutStart)
                {
                    w = 1f - Mathf.Clamp01((_action.Elapsed - _action.BlendOutStart) / _action.BlendOutDuration);
                }
                _action.Weight = w;
                pose = MotionPose.Lerp(pose, actionPose, MotionPose.Evaluate(Ease.InOut, w));
                if (_action.IsAttack) pose = BodyMechanics(pose, _action);
                if (w <= 0f) _action = null;
            }

            // ---- dodge overlay
            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= dt;
                float t = 1f - Mathf.Clamp01(_dodgeTimer / _dodgeDuration);
                float w = Mathf.Sin(t * Mathf.PI);
                var dodgeClip = MotionLibrary.Get(_rig.Profile.decoration == RigDecoration.Hero ? "Dodge" : "EnemyDodge");
                if (dodgeClip != null)
                {
                    var d = dodgeClip.Keys[dodgeClip.Keys.Length - 1].pose;
                    d.visualEuler = new Vector3(_dodgeLocalDir.z * 24f, 0f, -_dodgeLocalDir.x * 26f);
                    pose = MotionPose.Lerp(pose, d, w * 0.9f);
                }
            }

            // ---- hit reaction (additive)
            if (_hitTimer > 0f)
            {
                _hitTimer -= dt;
                float t = Mathf.Clamp01(_hitTimer / _hitDuration);
                float s = Mathf.Sin(t * Mathf.PI * 0.5f) * _hitStrength;
                pose = pose.WithBodyOffset(
                    new Vector3(_hitLocalDir.z * 16f * s, 0f, -_hitLocalDir.x * 12f * s),
                    new Vector3(_hitLocalDir.z * 10f * s, 0f, 0f),
                    new Vector3(_hitLocalDir.z * 22f * s, _hitLocalDir.x * 14f * s, 0f));
                pose.hips.y -= 0.05f * s;
            }

            // ---- stance (blends in over ~1 s)
            _postureWeight = Mathf.MoveTowards(_postureWeight, 1f, dt);
            if (_postureLean != 0f || _postureCrouch != 0f)
            {
                pose.visualEuler.x += _postureLean * _postureWeight;
                pose.chestEuler.x += _postureLean * 0.6f * _postureWeight;
                pose.headEuler.x -= _postureLean * 0.8f * _postureWeight;
                pose.hips.y -= _postureCrouch * _postureWeight;
            }

            // ---- knockdown / death
            _downWeight = Mathf.MoveTowards(_downWeight, _down && !_dead ? 1f : 0f, dt * (_down ? 5f : _downBlendOutRate));
            _deadWeight = Mathf.MoveTowards(_deadWeight, _dead ? 1f : 0f, dt * 2.2f);
            if (_downWeight > 0f) pose = MotionPose.Lerp(pose, p.Down, MotionPose.Evaluate(Ease.Out, _downWeight));
            if (_deadWeight > 0f) pose = MotionPose.Lerp(pose, p.Dead, MotionPose.Evaluate(Ease.In, _deadWeight));

            // ---- look at lock-on target
            float lookTarget = _lookTarget != null && !_dead && _downWeight < 0.5f ? 1f : 0f;
            _lookWeight = Mathf.MoveTowards(_lookWeight, lookTarget, dt * 4f);
            if (_lookWeight > 0f && _lookTarget != null && _head != null)
            {
                Vector3 local = _visual.InverseTransformDirection(_lookTarget.position - _head.position);
                float lookYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                float lookPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -35f, 35f);
                float w = _lookWeight * (_action != null ? 0.5f : 1f);
                pose.chestEuler.y += lookYaw * 0.3f * w;
                pose.headEuler.y = Mathf.Lerp(pose.headEuler.y, lookYaw * 0.6f, w);
                pose.headEuler.x = Mathf.Lerp(pose.headEuler.x, lookPitch * 0.7f, w);
            }

            return pose;
        }

        /// <summary>
        /// Whole-body attack mechanics on top of the arm/blade clip: coil away from the cut and sink in the
        /// anticipation, uncoil through hips → spine → chest (shoulders lead, head stays on the target) and step
        /// into the strike, then settle through the follow-through. The legs, hips and torso sell the weight.
        /// </summary>
        private static MotionPose BodyMechanics(MotionPose pose, ActionTrack a)
        {
            float t = a.Elapsed;
            float coil, drive;
            if (t < a.Windup)
            {
                coil = Mathf.SmoothStep(0f, 1f, t / a.Windup);
                drive = 0f;
            }
            else if (t < a.Windup + a.Active)
            {
                float u = (t - a.Windup) / a.Active;
                float e = 1f - (1f - u) * (1f - u);
                coil = 1f - e;
                drive = e;
            }
            else
            {
                float u = Mathf.Clamp01((t - a.Windup - a.Active) / a.Recovery);
                coil = 0f;
                drive = 1f - Mathf.SmoothStep(0f, 1f, u);
            }
            float k = a.Weight * a.BodyAmount;
            float s = a.SwingSign;
            float twist = -10f * coil + 16f * drive;
            pose.hipsEuler.y += twist * s * k;
            pose.spineEuler.y += (-7f * coil + 10f * drive) * s * k;
            pose.chestEuler.y += (-16f * coil + 24f * drive) * s * k;
            pose.chestEuler.z += (4f * coil - 6f * drive) * s * k;
            pose.headEuler.y += (8f * coil - 12f * drive) * s * k;
            pose.hips.y -= (0.035f * coil + 0.055f * drive) * k;
            pose.hips.z += 0.07f * drive * k;
            pose.visualEuler.x += (-3f * coil + 7f * drive) * k;
            // Step into the cut: the lead foot slides forward with a small lift, the rear foot pushes off.
            pose.footL.z += 0.17f * drive * k;
            pose.footL.y += 0.05f * Mathf.Sin(Mathf.Clamp01(drive) * Mathf.PI) * k;
            pose.footR.z -= 0.06f * drive * k;
            pose.elbowHintR += new Vector3(0f, 0.08f * coil, -0.05f * drive) * k;
            return pose;
        }

        private static MotionPose EvaluateAction(ActionTrack a)
        {
            var keys = a.Clip.Keys;
            int n = keys.Length;
            float t = a.Elapsed;
            if (t <= a.Times[0])
            {
                float u = a.Times[0] <= 0f ? 1f : t / a.Times[0];
                return MotionPose.Lerp(a.Start, keys[0].pose, MotionPose.Evaluate(keys[0].ease, u));
            }
            if (n == 1) return keys[0].pose;

            if (a.IsAttack)
            {
                float u = Mathf.Clamp01((t - a.Windup) / a.Active);
                if (u >= 1f) return keys[n - 1].pose;
                // One continuous fast-to-slow ease across the whole swing (anime smear timing).
                float e = 1f - (1f - u) * (1f - u) * (1f - u);
                float s = e * (n - 1);
                int i = Mathf.Min(n - 2, Mathf.FloorToInt(s));
                return Segment(keys[i], keys[i + 1], s - i);
            }

            for (int i = 0; i < n - 1; i++)
            {
                if (t <= a.Times[i + 1])
                {
                    float span = a.Times[i + 1] - a.Times[i];
                    float u = span <= 0f ? 1f : (t - a.Times[i]) / span;
                    return Segment(keys[i], keys[i + 1], MotionPose.Evaluate(keys[i + 1].ease, u));
                }
            }
            return keys[n - 1].pose;
        }

        private static MotionPose Segment(in MotionKey from, in MotionKey to, float u)
        {
            var pose = MotionPose.Lerp(from.pose, to.pose, u);
            if (to.autoEdge)
            {
                Vector3 motion = to.pose.bladeDir - from.pose.bladeDir;
                Vector3 edge = Vector3.ProjectOnPlane(motion, pose.bladeDir);
                if (edge.sqrMagnitude > 0.0025f) pose.bladeEdge = edge.normalized;
            }
            return pose;
        }

        // ------------------------------------------------------------------ apply

        private void ApplyPose(in MotionPose pose, bool immediate)
        {
            float scale = _rig.Scale;
            _visual.localRotation = Quaternion.Euler(pose.visualEuler);
            _visual.localPosition = pose.visualOffset * scale;

            _hips.localPosition = new Vector3(0f, _hipHeight, 0f) + pose.hips;
            _hips.localRotation = Quaternion.Euler(pose.hipsEuler);
            _spine.localRotation = Quaternion.Euler(pose.spineEuler);
            _chest.localRotation = Quaternion.Euler(pose.chestEuler);
            _head.localRotation = Quaternion.Euler(pose.headEuler);

            Quaternion vr = _visual.rotation;

            // ---- right arm (sword hand)
            Vector3 bladeDir = pose.bladeDir.sqrMagnitude > 0.0001f ? pose.bladeDir.normalized : Vector3.forward;
            Vector3 edge = Vector3.ProjectOnPlane(pose.bladeEdge, bladeDir);
            if (edge.sqrMagnitude < 0.0001f) edge = Vector3.ProjectOnPlane(Vector3.up, bladeDir);
            if (edge.sqrMagnitude < 0.0001f) edge = Vector3.ProjectOnPlane(Vector3.right, bladeDir);
            Quaternion gripRot = vr * Quaternion.LookRotation(bladeDir, edge.normalized);

            Vector3 handRWorld = _visual.TransformPoint(pose.handR);
            Vector3 shoulderR = _upperR.position;
            TwoBoneIK.Solve(_upperR, _lowerR, _handR, handRWorld, shoulderR + vr * pose.elbowHintR * scale);
            _handR.rotation = gripRot;

            // ---- left arm (on the hilt or free)
            Vector3 bladeWorld = vr * bladeDir;
            Vector3 gripL = _handR.position - bladeWorld * (0.13f * scale);
            Vector3 freeL = _visual.TransformPoint(pose.handL);
            Vector3 handLWorld = Vector3.Lerp(freeL, gripL, Mathf.Clamp01(pose.twoHand));
            TwoBoneIK.Solve(_upperL, _lowerL, _handL, handLWorld, _upperL.position + vr * pose.elbowHintL * scale);
            if (pose.twoHand > 0.5f) _handL.rotation = gripRot;
            else _handL.localRotation = Quaternion.identity;

            // ---- legs
            bool plant = _state.Grounded && _downWeight < 0.1f && _deadWeight < 0.1f && Mathf.Abs(pose.visualEuler.x) < 35f && Mathf.Abs(pose.visualEuler.z) < 35f;
            float ankle = 0.05f;
            Vector3 footLWorld = _visual.TransformPoint(pose.footL + new Vector3(0f, ankle, 0f));
            Vector3 footRWorld = _visual.TransformPoint(pose.footR + new Vector3(0f, ankle, 0f));
            if (plant && !immediate)
            {
                float lowest = 0f;
                footLWorld = GroundFoot(footLWorld, pose.footL.y, ankle * scale, scale, ref lowest);
                footRWorld = GroundFoot(footRWorld, pose.footR.y, ankle * scale, scale, ref lowest);
                if (lowest < 0f) _hips.position += Vector3.up * lowest;
            }
            Vector3 knee = pose.kneeHint.sqrMagnitude > 0.001f ? pose.kneeHint : Vector3.forward;
            TwoBoneIK.Solve(_thighL, _shinL, _footL, footLWorld, _thighL.position + vr * (knee + Vector3.left * 0.15f) * scale);
            TwoBoneIK.Solve(_thighR, _shinR, _footR, footRWorld, _thighR.position + vr * (knee + Vector3.right * 0.15f) * scale);
            Quaternion footRot = vr;
            _footL.rotation = footRot;
            _footR.rotation = footRot;
        }

        /// <summary>Snaps a foot to uneven ground (foot IK). Returns the adjusted world target.</summary>
        private Vector3 GroundFoot(Vector3 target, float poseLift, float ankle, float scale, ref float lowest)
        {
            if (poseLift > 0.08f) return target; // foot is lifted by the animation (step / kick)
            Vector3 origin = target + Vector3.up * (0.6f * scale);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, 1.4f * scale, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                if (GroundHits[i].point.y > bestY && GroundHits[i].point.y <= origin.y) bestY = GroundHits[i].point.y;
            }
            if (float.IsNegativeInfinity(bestY)) return target;
            float rootY = transform.position.y;
            float offset = Mathf.Clamp(bestY - rootY, -0.35f * scale, 0.35f * scale);
            if (offset < lowest) lowest = offset;
            target.y = rootY + offset + ankle + poseLift * scale;
            return target;
        }
    }
}
