using System.Collections.Generic;
using BreathOfEclipse.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Animates an imported Humanoid model from the character's <see cref="ProceduralAnimator"/> (which stays the
    /// single animation authority for gameplay, running on the hidden mannequin):
    /// <list type="bullet">
    /// <item>Real clips (Quaternius UAL, Humanoid-retargeted) through a Playables mixer: idle / combat idle, synced
    /// walk-jog-sprint blend with stride-matched playback, strafing by orientation warping, backward by reversed
    /// cycles, jump / fall / land, hit, knockdown + get up, death, and a body clip for each attack (time-warped so
    /// the clip's fastest frame lands on the attack's active phase).</item>
    /// <item>The procedural pose retargeted on top: the katana hands (two-bone IK, grip frame from the finger
    /// bones), the torso during attacks and techniques, full body for techniques without a matching clip, head
    /// look-at with anatomical limits, foot placement, finger grip, inhale shoulders.</item>
    /// </list>
    /// Gameplay reads BladeBase / BladeTip, which live on the model's hand, so hits follow what is drawn.
    /// Root motion stays off: the CharacterController moves the character.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class HumanoidVisualDriver : MonoBehaviour
    {
        private sealed class Slot
        {
            public string Name;
            public AnimationClip Clip;
            public AnimationClipPlayable Playable;
            public int Input;
            public float Length;
            public float Time;
            public float Weight;
        }

        private sealed class Track
        {
            public Slot Slot;
            public ClipSegment Segment;
            public string Id;
            public bool Attack;
            public float T;
            public float Windup, Active, Recovery, Length;
            public float Weight;
            public float Fade = 1f;
        }

        private enum DownPhase
        {
            None,
            Falling,
            GettingUp
        }

        public HumanoidSkeleton Skeleton => _sk;
        public Transform VisualRoot => _visualRoot;
        public LocomotionSet Set => _set;
        public bool ClipsActive => _clips;
        /// <summary>Debug / Animation Lab: a clip that overrides everything (null = normal behaviour).</summary>
        public string LabClip { get; private set; }
        public float LabSpeed { get; set; } = 1f;
        public bool LabLoop { get; set; } = true;
        public bool LabFrozen { get; set; }
        public float LabTime => _labSlot != null ? _labSlot.Time : 0f;
        public float LabLength => _labSlot != null ? _labSlot.Length : 0f;

        private ProceduralAnimator _src;
        private CharacterRig _rig;
        private HumanoidSkeleton _sk;
        private Animator _animator;
        private Transform _visualRoot;
        private LocomotionSet _set;
        private bool _clips;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly Dictionary<string, Slot> _byName = new Dictionary<string, Slot>();

        private Slot _idle, _combatIdle, _walk, _jog, _sprint, _jumpUp, _jumpLoop, _land, _hitLight, _hitFront, _hitHeavy, _knock, _getUp, _death, _block;
        private Slot _labSlot;

        private Track _act, _prev;
        private int _actionSerial = -1, _hitSerial = -1;
        private Slot _hitSlotActive;
        private float _hitT, _hitLen, _hitWeight;
        private DownPhase _down;
        private float _downT, _getUpLen;
        private bool _wasDown, _wasDead;
        private float _deadT, _deadWeight;
        private float _phase, _idleTime;
        private float _warpYaw, _bank, _lastYaw;
        private float _airT, _landT, _upWeight;
        private bool _wasGrounded = true;
        private float _kBody = 1f, _kArmR = 1f, _kArmL = 1f;
        private float _mAnkle;
        private float _heavyRate = 1f;
        private float _locoArmsProcedural = 1f;
        private float _gripCurl = 1f;

        private Transform _mHips, _mSpine, _mChest, _mHead, _mUpperR, _mLowerR, _mHandR, _mUpperL, _mLowerL, _mHandL, _mThighL, _mShinL, _mFootL, _mThighR, _mShinR, _mFootR;
        private Transform _weapon;

        private static readonly RaycastHit[] GroundHits = new RaycastHit[4];

        // ------------------------------------------------------------------ setup

        public void Initialize(ProceduralAnimator source, CharacterRig rig, HumanoidSkeleton skeleton, Transform visualRoot, LocomotionSet set)
        {
            _src = source;
            _rig = rig;
            _sk = skeleton;
            _animator = skeleton.Animator;
            _visualRoot = visualRoot;
            _set = set;
            _lastYaw = transform.eulerAngles.y;

            _mHips = rig.Bone(RigBone.Hips);
            _mSpine = rig.Bone(RigBone.Spine);
            _mChest = rig.Bone(RigBone.Chest);
            _mHead = rig.Bone(RigBone.Head);
            _mUpperR = rig.Bone(RigBone.UpperArmR);
            _mLowerR = rig.Bone(RigBone.LowerArmR);
            _mHandR = rig.Bone(RigBone.HandR);
            _mUpperL = rig.Bone(RigBone.UpperArmL);
            _mLowerL = rig.Bone(RigBone.LowerArmL);
            _mHandL = rig.Bone(RigBone.HandL);
            _mThighL = rig.Bone(RigBone.ThighL);
            _mShinL = rig.Bone(RigBone.ShinL);
            _mFootL = rig.Bone(RigBone.FootL);
            _mThighR = rig.Bone(RigBone.ThighR);
            _mShinR = rig.Bone(RigBone.ShinR);
            _mFootR = rig.Bone(RigBone.FootR);
            _weapon = rig.Bone(RigBone.Weapon);

            float scale = rig.Scale;
            var p = rig.Profile;
            _kBody = _sk.HipHeight / Mathf.Max(0.1f, p.hipHeight * scale);
            _kArmR = _sk.ArmLengthR / Mathf.Max(0.1f, (p.upperArm + p.lowerArm) * scale);
            _kArmL = _sk.ArmLengthL / Mathf.Max(0.1f, (p.upperArm + p.lowerArm) * scale);
            _mAnkle = 0.05f * scale;
            _heavyRate = set == LocomotionSet.Brute ? 0.85f : 1f;
            _locoArmsProcedural = set == LocomotionSet.Demon ? 0.45f : 1f;
            _gripCurl = set == LocomotionSet.Demon ? 0.3f : 1f;

            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.updateMode = AnimatorUpdateMode.Normal;
            _animator.runtimeAnimatorController = null;
            BuildGraph();
        }

        private void BuildGraph()
        {
            _clips = HumanoidClipLibrary.Available;
            if (!_clips) return;
            _graph = PlayableGraph.Create(name + ".Humanoid");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Visual", _animator);
            _mixer = AnimationMixerPlayable.Create(_graph, 0);
            output.SetSourcePlayable(_mixer);

            bool demon = _set == LocomotionSet.Demon;
            bool brute = _set == LocomotionSet.Brute;
            _idle = Add(demon ? "Zombie_Idle_Loop" : "Idle_Loop") ?? Add("Idle_Loop");
            _combatIdle = Add(demon ? "Zombie_Idle_Loop" : "Sword_Idle") ?? _idle;
            _walk = Add(demon ? "Zombie_Walk_Fwd_Loop" : "Walk_Loop") ?? Add("Walk_Loop");
            _jog = Add("Jog_Fwd_Loop");
            _sprint = Add("Sprint_Loop") ?? _jog;
            _jumpUp = Add(brute || demon ? "Jump_Start" : "NinjaJump_Start") ?? Add("Jump_Start");
            _jumpLoop = Add(brute || demon ? "Jump_Loop" : "NinjaJump_Idle_Loop") ?? Add("Jump_Loop");
            _land = Add(brute || demon ? "Jump_Land" : "NinjaJump_Land") ?? Add("Jump_Land");
            _hitLight = Add("Hit_Chest");
            _hitFront = Add("Hit_Head") ?? _hitLight;
            _hitHeavy = Add("Idle_Shield_Break") ?? _hitFront;
            _knock = Add("Hit_Knockback");
            _getUp = Add("LayToIdle");
            _death = Add("Death01");
            _block = Add("Sword_Block");
            foreach (var clip in HumanoidClipLibrary.SegmentClips()) Add(clip);
            if (_idle == null)
            {
                _clips = false;
                _graph.Destroy();
                return;
            }
            _graph.Play();
        }

        private Slot Add(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            if (_byName.TryGetValue(clipName, out var existing)) return existing;
            var clip = HumanoidClipLibrary.Get(clipName);
            if (clip == null) return null;
            var playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetApplyFootIK(true);
            playable.SetSpeed(0);
            int input = _mixer.AddInput(playable, 0, 0f);
            var slot = new Slot { Name = clipName, Clip = clip, Playable = playable, Input = input, Length = Mathf.Max(0.05f, clip.length) };
            _slots.Add(slot);
            _byName[clipName] = slot;
            return slot;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        private void OnDisable()
        {
            if (_graph.IsValid()) _graph.Stop();
        }

        private void OnEnable()
        {
            if (_graph.IsValid()) _graph.Play();
        }

        // ------------------------------------------------------------------ Animation Lab

        /// <summary>Plays a clip by name over everything (Animation Lab). Null or unknown clears it.</summary>
        public bool SetLabClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName) || !_clips)
            {
                LabClip = null;
                _labSlot = null;
                return false;
            }
            var slot = Add(clipName);
            LabClip = slot != null ? clipName : null;
            _labSlot = slot;
            if (slot != null) slot.Time = 0f;
            return slot != null;
        }

        public void LabRestart()
        {
            if (_labSlot != null) _labSlot.Time = 0f;
        }

        /// <summary>Clips the graph can play (for the Animation Lab list).</summary>
        public IEnumerable<string> AvailableClips() => HumanoidClipLibrary.Names;

        // ------------------------------------------------------------------ clip layer (before the Animator evaluates)

        private void Update()
        {
            if (!_clips || _src == null) return;
            float dt = Time.deltaTime;
            foreach (var s in _slots) s.Weight = 0f;

            if (_labSlot != null)
            {
                if (!LabFrozen) _labSlot.Time += dt * LabSpeed;
                _labSlot.Time = LabLoop ? Mathf.Repeat(_labSlot.Time, _labSlot.Length) : Mathf.Clamp(_labSlot.Time, 0f, _labSlot.Length);
                _labSlot.Weight = 1f;
                Push();
                return;
            }

            float react = UpdateReactions(dt);
            float action = UpdateAction(dt) * (1f - react);
            float loco = Mathf.Max(0f, 1f - react - action);
            UpdateLocomotion(dt, loco);
            Push();
        }

        private void Push()
        {
            float total = 0f;
            foreach (var s in _slots) total += s.Weight;
            if (total < 1e-4f && _idle != null)
            {
                _idle.Weight = 1f;
                total = 1f;
            }
            foreach (var s in _slots)
            {
                float w = s.Weight / total;
                _mixer.SetInputWeight(s.Input, w);
                if (w > 0f) s.Playable.SetTime(s.Time);
            }
        }

        /// <summary>Hit / knockdown / death clips. Returns their total weight (they own the body).</summary>
        private float UpdateReactions(float dt)
        {
            var S = _src;
            // death: fall and stay down
            if (S.Dead && !_wasDead)
            {
                _deadT = 0f;
                _down = DownPhase.None;
            }
            _wasDead = S.Dead;
            _deadWeight = Mathf.MoveTowards(_deadWeight, S.Dead && _death != null ? 1f : 0f, dt * (S.Dead ? 7f : 3f));
            if (_deadWeight > 0f && _death != null)
            {
                _deadT += dt;
                _death.Time = Mathf.Min(_deadT, _death.Length);
                _death.Weight += _deadWeight;
                if (_deadWeight >= 1f) return 1f;
            }

            // knockdown: thrown on the back, stay down, then rise with the get-up clip
            if (S.Down && !_wasDown)
            {
                _down = DownPhase.Falling;
                _downT = 0f;
            }
            else if (!S.Down && _wasDown && _down == DownPhase.Falling)
            {
                _down = DownPhase.GettingUp;
                _downT = 0f;
                _getUpLen = Mathf.Max(0.85f, S.GetUpDuration * 1.8f);
            }
            _wasDown = S.Down;
            float downW = 0f;
            if (_down == DownPhase.Falling && _knock != null)
            {
                _downT += dt;
                _knock.Time = Mathf.Min(_downT * 1.1f, _knock.Length);
                downW = Mathf.Clamp01(_downT / 0.06f);
                _knock.Weight += downW * (1f - _deadWeight);
            }
            else if (_down == DownPhase.GettingUp && _getUp != null)
            {
                _downT += dt;
                float u = Mathf.Clamp01(_downT / _getUpLen);
                _getUp.Time = u * _getUp.Length;
                downW = u < 0.75f ? 1f : 1f - (u - 0.75f) / 0.25f;
                _getUp.Weight += downW * (1f - _deadWeight);
                if (u >= 1f) _down = DownPhase.None;
            }
            else if (_down != DownPhase.None && (_knock == null || _getUp == null)) _down = DownPhase.None;

            // hit flinch
            if (S.HitSerial != _hitSerial)
            {
                bool first = _hitSerial < 0;
                _hitSerial = S.HitSerial;
                if (!first && !S.Dead && _down == DownPhase.None)
                {
                    bool heavy = S.HitStrength > 0.8f;
                    bool fromFront = S.HitLocalDirection.z < -0.3f;
                    _hitSlotActive = heavy ? _hitHeavy : fromFront ? _hitFront : _hitLight;
                    _hitT = 0f;
                    _hitLen = _hitSlotActive != null ? (heavy ? Mathf.Min(_hitSlotActive.Length, 0.75f) : _hitSlotActive.Length) : 0f;
                }
            }
            if (_hitSlotActive != null && _hitT < _hitLen)
            {
                _hitT += dt;
                float u = Mathf.Clamp01(_hitT / _hitLen);
                _hitSlotActive.Time = Mathf.Min(_hitT * (_hitSlotActive == _hitHeavy ? 1.4f : 1f), _hitSlotActive.Length);
                _hitWeight = Mathf.Min(Mathf.Clamp01(_hitT / 0.04f), Mathf.Clamp01((1f - u) / 0.35f)) * 0.85f;
                _hitSlotActive.Weight += _hitWeight * (1f - downW) * (1f - _deadWeight);
            }
            else _hitWeight = 0f;

            return Mathf.Clamp01(_deadWeight + downW * (1f - _deadWeight) + _hitWeight * (1f - downW) * (1f - _deadWeight));
        }

        /// <summary>The body clip of the playing attack / technique. Returns its weight.</summary>
        private float UpdateAction(float dt)
        {
            var S = _src;
            if (S.ActionSerial != _actionSerial)
            {
                _actionSerial = S.ActionSerial;
                if (S.ActionId != null)
                {
                    if (_act != null)
                    {
                        _prev = _act;
                        _prev.Fade = 1f;
                    }
                    _act = null;
                    if (HumanoidClipLibrary.TryGetSegment(S.ActionId, out var seg) && _byName.TryGetValue(seg.Clip, out var slot))
                    {
                        _act = new Track
                        {
                            Slot = slot,
                            Segment = seg,
                            Id = S.ActionId,
                            Attack = S.ActionIsAttack,
                            Windup = S.ActionWindup,
                            Active = S.ActionActive,
                            Recovery = S.ActionRecovery,
                            Length = Mathf.Max(0.05f, S.ActionLength)
                        };
                    }
                }
            }

            float w = 0f;
            if (_act != null)
            {
                bool current = S.ActionId == _act.Id;
                _act.T += dt;
                _act.Weight = current ? S.ActionWeight : Mathf.MoveTowards(_act.Weight, 0f, dt * 8f);
                if (!current && _act.Weight <= 0f) _act = null;
                else
                {
                    _act.Slot.Time = TrackTime(_act);
                    float blendIn = Mathf.Clamp01(_act.T / 0.06f);
                    w = _act.Weight * blendIn;
                    _act.Slot.Weight += w;
                }
            }
            if (_prev != null)
            {
                _prev.T += dt;
                _prev.Fade = Mathf.MoveTowards(_prev.Fade, 0f, dt / 0.08f);
                if (_prev.Fade <= 0f || (_act != null && _prev.Slot == _act.Slot)) _prev = null;
                else
                {
                    _prev.Slot.Time = TrackTime(_prev);
                    float pw = _prev.Fade * (1f - w);
                    _prev.Slot.Weight += pw;
                    w += pw;
                }
            }
            return Mathf.Clamp01(w);
        }

        /// <summary>Clip time of a track: attacks put the clip's contact frame on the active phase (visual contact = hit phase).</summary>
        private static float TrackTime(Track a)
        {
            var seg = a.Segment;
            float len = a.Slot.Length;
            if (seg.Loop) return Mathf.Repeat(a.T, len);
            float u;
            if (a.Attack)
            {
                float contactT = a.Windup + a.Active * 0.35f;
                float endT = a.Windup + a.Active + a.Recovery;
                if (a.T <= contactT)
                    u = Mathf.Lerp(seg.Start, seg.Contact, contactT > 0f ? a.T / contactT : 1f);
                else
                    u = Mathf.Lerp(seg.Contact, seg.End, Mathf.Clamp01((a.T - contactT) / Mathf.Max(0.05f, endT - contactT)));
            }
            else u = Mathf.Lerp(seg.Start, seg.End, Mathf.Clamp01(a.T / a.Length));
            return Mathf.Clamp(u, 0f, 1f) * len;
        }

        private void UpdateLocomotion(float dt, float weight)
        {
            var S = _src;
            var st = S.Locomotion;
            float heightK = _sk.Height / HumanoidClipLibrary.ReferenceHeight;
            Vector3 v = transform.InverseTransformDirection(st.Velocity);
            v.y = 0f;
            float speed = v.magnitude;

            // ---- orientation warping (strafe / backpedal while facing a target)
            float warpTarget = 0f;
            bool reverse = false;
            if (speed > 0.4f && (S.LookTarget != null || st.CombatStance))
            {
                float angle = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(angle) > 110f)
                {
                    reverse = true;
                    angle -= Mathf.Sign(angle) * 180f;
                    warpTarget = Mathf.Clamp(angle, -55f, 55f);
                }
                else warpTarget = Mathf.Clamp(angle, -70f, 70f);
            }
            if (_act != null || S.ActionId != null) warpTarget = 0f;
            _warpYaw = Mathf.Lerp(_warpYaw, warpTarget, 1f - Mathf.Exp(-10f * dt));

            // ---- ground cycles (walk / jog / sprint share one normalized phase)
            float vWalk = NaturalSpeed(_walk, 0.97f) * heightK;
            float vJog = NaturalSpeed(_jog, 5.36f) * heightK;
            float vSprint = NaturalSpeed(_sprint, 8.25f) * heightK;
            float wIdle = 0f, wWalk = 0f, wJog = 0f, wSprint = 0f;
            const float still = 0.15f;
            if (speed <= still) wIdle = 1f;
            else if (speed < vWalk)
            {
                float t = (speed - still) / Mathf.Max(0.01f, vWalk - still);
                wIdle = 1f - t;
                wWalk = t;
            }
            else if (speed < vJog)
            {
                float t = (speed - vWalk) / Mathf.Max(0.01f, vJog - vWalk);
                wWalk = 1f - t;
                wJog = t;
            }
            else if (speed < vSprint)
            {
                float t = (speed - vJog) / Mathf.Max(0.01f, vSprint - vJog);
                wJog = 1f - t;
                wSprint = t;
            }
            else wSprint = 1f;

            float moving = wWalk + wJog + wSprint;
            if (moving > 0.001f)
            {
                float len = (wWalk * Len(_walk) + wJog * Len(_jog) + wSprint * Len(_sprint)) / moving;
                float natural = (wWalk * vWalk + wJog * vJog + wSprint * vSprint) / moving;
                float rate = Mathf.Clamp(speed / Mathf.Max(0.1f, natural), 0.5f, 2.2f) * _heavyRate;
                _phase = Mathf.Repeat(_phase + dt * rate / Mathf.Max(0.1f, len) * (reverse ? -1f : 1f), 1f);
            }
            _idleTime += dt;

            // ---- air / landing
            bool grounded = st.Grounded;
            if (!grounded) _airT += dt;
            if (grounded && !_wasGrounded && _airT > 0.25f) _landT = 0.4f;
            if (grounded) _airT = 0f;
            _wasGrounded = grounded;
            if (_landT > 0f) _landT -= dt;
            float air = S.AirWeight;
            _upWeight = Mathf.MoveTowards(_upWeight, st.Velocity.y > 0.5f ? 1f : 0f, dt * 5f);

            float ground = weight * (1f - air);
            float block = S.BlockWeight;
            float idleW = ground * wIdle;
            AddCycle(_idle, idleW * (1f - S.CombatWeight) * (1f - block), _idleTime);
            AddCycle(_combatIdle, idleW * S.CombatWeight * (1f - block), _idleTime);
            if (_block != null) Hold(_block, idleW * block, 0.3f);
            else AddCycle(_combatIdle, idleW * block, _idleTime);
            SetPhase(_walk, ground * wWalk);
            SetPhase(_jog, ground * wJog);
            SetPhase(_sprint, ground * wSprint);

            float airW = weight * air;
            if (airW > 0f)
            {
                if (_jumpUp != null) Hold(_jumpUp, airW * _upWeight, Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01(_airT / 0.45f)));
                AddCycle(_jumpLoop, airW * (1f - (_jumpUp != null ? _upWeight : 0f)), _airT);
            }
            if (_landT > 0f && _land != null)
            {
                float u = 1f - _landT / 0.4f;
                float lw = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * ground * 0.8f;
                Hold(_land, lw, Mathf.Lerp(0.15f, 0.55f, u));
            }
        }

        private static float NaturalSpeed(Slot s, float fallback) =>
            s != null && HumanoidClipLibrary.NaturalSpeed.TryGetValue(s.Name, out var v) ? v : fallback;

        private static float Len(Slot s) => s != null ? s.Length : 1f;

        private static void AddCycle(Slot s, float w, float time)
        {
            if (s == null || w <= 0f) return;
            s.Weight += w;
            s.Time = Mathf.Repeat(time, s.Length);
        }

        private void SetPhase(Slot s, float w)
        {
            if (s == null || w <= 0f) return;
            s.Weight += w;
            s.Time = _phase * s.Length;
        }

        private static void Hold(Slot s, float w, float normalized)
        {
            if (s == null || w <= 0f) return;
            s.Weight += w;
            s.Time = Mathf.Clamp01(normalized) * s.Length;
        }

        // ------------------------------------------------------------------ procedural layer (after the Animator)

        private void LateUpdate()
        {
            if (_sk == null || _src == null || _rig == null) return;
            var S = _src;
            float dt = Time.deltaTime;
            bool lab = _labSlot != null;
            // Without clips nothing rewrites the bones each frame: start from the bind pose so IK never drifts.
            if (!_clips) _sk.ResetToRest();
            // Hit stop (time frozen): the Animator may not rewrite the pose, so additive layers must not stack.
            bool frozen = dt <= 0f;

            // ---- layer weights
            float react = Mathf.Clamp01(_deadWeight + (_down != DownPhase.None ? 1f : 0f));
            float action = S.ActionId != null ? S.ActionWeight : 0f;
            bool hasClip = _act != null && S.ActionId == _act.Id;
            float torsoProc = action * (hasClip ? _act.Segment.TorsoProcedural : 1f);
            float legsProc = action * (hasClip && _act.Segment.ClipLegs && S.AirWeight < 0.5f ? 0f : 1f);
            float dodge = S.DodgeWeight;
            torsoProc = Mathf.Max(torsoProc, dodge * 0.85f);
            legsProc = Mathf.Max(legsProc, dodge * 0.85f);
            if (!_clips)
            {
                // No real clips: the retargeted procedural animation drives the whole body.
                torsoProc = 1f;
                legsProc = 1f;
                react = 0f;
            }
            torsoProc *= 1f - react;
            legsProc *= 1f - react;
            float arms = (1f - react) * (1f - _hitWeight * 0.5f);
            float armsLoco = Mathf.Lerp(_locoArmsProcedural, 1f, Mathf.Max(action, S.CombatWeight * 0.5f, S.BlockWeight));
            float armR = arms * armsLoco;
            float twoHand = S.CurrentPose.twoHand;
            float armL = arms * Mathf.Lerp(Mathf.Max(action, 0.35f) * armsLoco, 1f, Mathf.Clamp01(twoHand));
            if (lab)
            {
                torsoProc = legsProc = 0f;
                armR = armL = LabHoldsSword ? 1f : 0f;
            }

            // ---- visual root: orientation warp + banking into turns
            float yaw = transform.eulerAngles.y;
            float yawRate = dt > 0f ? Mathf.DeltaAngle(_lastYaw, yaw) / dt : 0f;
            _lastYaw = yaw;
            if (dt > 0f)
            {
                float speed01 = Mathf.Clamp01(new Vector2(S.Locomotion.Velocity.x, S.Locomotion.Velocity.z).magnitude / 6f);
                _bank = Mathf.Lerp(_bank, Mathf.Clamp(-yawRate * 0.012f * speed01, -9f, 9f) * (1f - action), 1f - Mathf.Exp(-6f * dt));
            }
            _visualRoot.localRotation = Quaternion.Euler(0f, _warpYaw, _bank);

            var sk = _sk;
            Transform hips = sk[HumanBodyBones.Hips];
            Transform footL = sk[HumanBodyBones.LeftFoot], footR = sk[HumanBodyBones.RightFoot];
            Vector3 clipFootL = footL.position, clipFootR = footR.position;
            Quaternion clipFootLRot = footL.rotation, clipFootRRot = footR.rotation;
            Vector3 root = transform.position;
            Quaternion up = transform.rotation;

            // ---- hips
            float hipsRotW = Mathf.Max(legsProc, torsoProc * 0.5f);
            if (hipsRotW > 0f)
            {
                Vector3 target = root + (_mHips.position - root) * _kBody;
                hips.position = Vector3.Lerp(hips.position, target, legsProc);
                SetWorld(hips, _mHips.rotation * sk.Rest(HumanBodyBones.Hips), hipsRotW);
            }

            // ---- torso (procedural during actions)
            if (torsoProc > 0f)
            {
                var spine = sk[HumanBodyBones.Spine];
                var chest = sk[HumanBodyBones.Chest];
                var upper = sk[HumanBodyBones.UpperChest];
                var neck = sk[HumanBodyBones.Neck];
                var head = sk[HumanBodyBones.Head];
                SetWorld(spine, _mSpine.rotation * sk.Rest(HumanBodyBones.Spine), torsoProc);
                if (upper != null)
                {
                    if (chest != null) SetWorld(chest, Quaternion.Slerp(_mSpine.rotation, _mChest.rotation, 0.5f) * sk.Rest(HumanBodyBones.Chest), torsoProc);
                    SetWorld(upper, _mChest.rotation * sk.Rest(HumanBodyBones.UpperChest), torsoProc);
                }
                else if (chest != null) SetWorld(chest, _mChest.rotation * sk.Rest(HumanBodyBones.Chest), torsoProc);
                SetWorld(sk[HumanBodyBones.LeftShoulder], _mChest.rotation * sk.Rest(HumanBodyBones.LeftShoulder), torsoProc);
                SetWorld(sk[HumanBodyBones.RightShoulder], _mChest.rotation * sk.Rest(HumanBodyBones.RightShoulder), torsoProc);
                if (neck != null) SetWorld(neck, Quaternion.Slerp(_mChest.rotation, _mHead.rotation, 0.4f) * sk.Rest(HumanBodyBones.Neck), torsoProc);
                SetWorld(head, _mHead.rotation * sk.Rest(HumanBodyBones.Head), torsoProc);
            }

            // ---- clip-driven additive layers: strafe counter-twist, stance posture, look-at, inhale
            float clipTorso = 1f - torsoProc;
            if (!lab && !frozen && clipTorso > 0f)
            {
                Vector3 axisUp = Vector3.up;
                Vector3 axisRight = up * Vector3.right;
                if (Mathf.Abs(_warpYaw) > 0.5f)
                {
                    var twist = Quaternion.AngleAxis(-_warpYaw * 0.45f * clipTorso, axisUp);
                    HumanoidIK.RotateWorld(sk[HumanBodyBones.Spine], twist);
                    HumanoidIK.RotateWorld(sk[HumanBodyBones.Chest] ?? sk[HumanBodyBones.Spine], twist);
                }
                if (Mathf.Abs(S.PostureLean) > 0.1f || S.PostureCrouch > 0.001f)
                {
                    var lean = Quaternion.AngleAxis(S.PostureLean * 0.5f * clipTorso, axisRight);
                    HumanoidIK.RotateWorld(sk[HumanBodyBones.Spine], lean);
                    HumanoidIK.RotateWorld(sk[HumanBodyBones.Chest] ?? sk[HumanBodyBones.Spine], lean);
                    hips.position -= Vector3.up * (S.PostureCrouch * _kBody * clipTorso);
                }
                if (S.LookTarget != null && S.LookWeight > 0f && !S.Dead)
                {
                    var head = sk[HumanBodyBones.Head];
                    var neck = sk[HumanBodyBones.Neck];
                    var basis = Quaternion.LookRotation(Vector3.ProjectOnPlane(up * Vector3.forward, Vector3.up), Vector3.up);
                    Vector2 look = HumanoidIK.LookAngles(Quaternion.AngleAxis(_warpYaw * 0.1f, Vector3.up) * basis, head.position, S.LookTarget.position, 70f, 35f);
                    float w = S.LookWeight * clipTorso * (1f - react);
                    var yawQ = Quaternion.AngleAxis(look.x * w, Vector3.up);
                    var pitchQ = Quaternion.AngleAxis(look.y * w, up * Vector3.right);
                    if (neck != null)
                    {
                        HumanoidIK.RotateWorld(neck, Quaternion.Slerp(Quaternion.identity, yawQ * pitchQ, 0.4f));
                        HumanoidIK.RotateWorld(head, Quaternion.Slerp(Quaternion.identity, yawQ * pitchQ, 0.6f));
                    }
                    else HumanoidIK.RotateWorld(head, yawQ * pitchQ);
                }
            }
            if (S.ActionId == "SkillInhale" && !frozen)
            {
                // Breathing in: the shoulders and collarbones rise, the neck lengthens.
                float u = S.ActionLength > 0f ? Mathf.Clamp01(S.ActionElapsed / S.ActionLength) : 0f;
                float breath = Mathf.Sin(u * Mathf.PI) * S.ActionWeight;
                Vector3 fwd = up * Vector3.forward;
                HumanoidIK.RotateWorld(sk[HumanBodyBones.LeftShoulder], Quaternion.AngleAxis(-9f * breath, fwd));
                HumanoidIK.RotateWorld(sk[HumanBodyBones.RightShoulder], Quaternion.AngleAxis(9f * breath, fwd));
                HumanoidIK.RotateWorld(sk[HumanBodyBones.Neck] ?? sk[HumanBodyBones.Head], Quaternion.AngleAxis(-5f * breath, up * Vector3.right));
            }

            // ---- legs: procedural feet (techniques / dodge) or clip feet on uneven ground
            {
                Vector3 procL = MapFoot(_mFootL), procR = MapFoot(_mFootR);
                Vector3 tL = Vector3.Lerp(clipFootL, procL, legsProc);
                Vector3 tR = Vector3.Lerp(clipFootR, procR, legsProc);
                bool grounded = S.Locomotion.Grounded && react < 0.5f && legsProc < 0.5f && !lab;
                if (grounded && dt > 0f)
                {
                    float lowest = 0f;
                    tL = GroundFoot(tL, ref lowest);
                    tR = GroundFoot(tR, ref lowest);
                    if (lowest < 0f) hips.position += Vector3.up * lowest;
                }
                Vector3 kneeFwd = up * Vector3.forward * (0.4f * _sk.Height / 1.8f);
                Vector3 poleL = Vector3.Lerp(sk[HumanBodyBones.LeftLowerLeg].position + kneeFwd, root + (_mShinL.position - root) * _kBody + kneeFwd * 0.5f, legsProc);
                Vector3 poleR = Vector3.Lerp(sk[HumanBodyBones.RightLowerLeg].position + kneeFwd, root + (_mShinR.position - root) * _kBody + kneeFwd * 0.5f, legsProc);
                HumanoidIK.SolveTwoBone(sk[HumanBodyBones.LeftUpperLeg], sk[HumanBodyBones.LeftLowerLeg], footL, tL, poleL);
                HumanoidIK.SolveTwoBone(sk[HumanBodyBones.RightUpperLeg], sk[HumanBodyBones.RightLowerLeg], footR, tR, poleR);
                footL.rotation = Quaternion.Slerp(clipFootLRot, _mFootL.rotation * sk.Rest(HumanBodyBones.LeftFoot), legsProc);
                footR.rotation = Quaternion.Slerp(clipFootRRot, _mFootR.rotation * sk.Rest(HumanBodyBones.RightFoot), legsProc);
            }

            // ---- sword arm: the grip follows the procedural blade exactly (hit detection = drawn blade)
            var handR = sk[HumanBodyBones.RightHand];
            var handL = sk[HumanBodyBones.LeftHand];
            if (armR > 0f)
            {
                var upperR = sk[HumanBodyBones.RightUpperArm];
                var lowerR = sk[HumanBodyBones.RightLowerArm];
                Vector3 grip = upperR.position + (_mHandR.position - _mUpperR.position) * _kArmR;
                Quaternion handRot = _mHandR.rotation * sk.GripToHandR;
                Vector3 target = grip - handRot * Vector3.Scale(sk.PalmLocalR, handR.lossyScale);
                Vector3 pole = upperR.position + (_mLowerR.position - _mUpperR.position) * _kArmR * 1.2f;
                Quaternion clipRot = handR.rotation;
                HumanoidIK.SolveTwoBone(upperR, lowerR, handR, target, Vector3.Lerp(lowerR.position, pole, armR), armR);
                handR.rotation = Quaternion.Slerp(clipRot, handRot, armR);
            }
            sk.CurlFingers(true, _gripCurl);

            // ---- off hand: on the grip behind the right hand (two-handed kamae) or free
            if (armL > 0f && _weapon != null)
            {
                var upperL = sk[HumanBodyBones.LeftUpperArm];
                var lowerL = sk[HumanBodyBones.LeftLowerArm];
                Quaternion clipRot = handL.rotation;
                Vector3 target;
                Quaternion rot;
                if (twoHand > 0.5f && _set != LocomotionSet.Demon)
                {
                    Quaternion gripRot = _weapon.rotation;
                    Vector3 blade = gripRot * Vector3.forward;
                    Vector3 gripL = _weapon.position - blade * (0.13f * _rig.Scale);
                    rot = gripRot * sk.GripToHandL;
                    target = gripL - rot * Vector3.Scale(sk.PalmLocalL, handL.lossyScale);
                }
                else
                {
                    target = upperL.position + (_mHandL.position - _mUpperL.position) * _kArmL;
                    rot = clipRot;
                }
                Vector3 pole = upperL.position + (_mLowerL.position - _mUpperL.position) * _kArmL * 1.2f;
                HumanoidIK.SolveTwoBone(upperL, lowerL, handL, target, Vector3.Lerp(lowerL.position, pole, armL), armL);
                handL.rotation = Quaternion.Slerp(clipRot, rot, armL);
                if (twoHand > 0.5f) sk.CurlFingers(false, armL * _gripCurl);
            }
        }

        /// <summary>Animation Lab: keep the katana grip on while previewing clips.</summary>
        public bool LabHoldsSword { get; set; } = true;

        private Vector3 MapFoot(Transform mannequinFoot)
        {
            Vector3 root = transform.position;
            Vector3 rel = transform.InverseTransformVector(mannequinFoot.position - root);
            Vector3 local = new Vector3(rel.x * _kBody, rel.y - _mAnkle + _sk.AnkleHeight, rel.z * _kBody);
            return root + transform.TransformVector(local);
        }

        /// <summary>Foot IK on uneven ground for clip-driven legs; returns the adjusted target.</summary>
        private Vector3 GroundFoot(Vector3 foot, ref float lowest)
        {
            Vector3 root = transform.position;
            float lift = foot.y - root.y - _sk.AnkleHeight;
            if (lift > 0.12f * _sk.Height / 1.8f) return foot; // foot in the air (step, jump)
            Vector3 origin = new Vector3(foot.x, root.y + 0.6f * _sk.Height / 1.8f, foot.z);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, 1.4f * _sk.Height / 1.8f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
                if (GroundHits[i].point.y > best && GroundHits[i].point.y <= origin.y) best = GroundHits[i].point.y;
            if (float.IsNegativeInfinity(best)) return foot;
            float offset = Mathf.Clamp(best - root.y, -0.35f, 0.35f);
            if (Mathf.Abs(offset) < 0.015f) return foot;
            if (offset < lowest) lowest = offset;
            foot.y += offset;
            return foot;
        }

        private static void SetWorld(Transform bone, Quaternion world, float weight)
        {
            if (bone == null || weight <= 0f) return;
            var parent = bone.parent;
            Quaternion local = parent != null ? Quaternion.Inverse(parent.rotation) * world : world;
            bone.localRotation = weight >= 1f ? local : Quaternion.Slerp(bone.localRotation, local, weight);
        }
    }
}
