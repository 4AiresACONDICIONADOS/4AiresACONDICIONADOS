using System.Collections.Generic;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BreathOfEclipse.World
{
    /// <summary>Body pose of a light NPC (each maps to a real clip of the Universal Animation Library).</summary>
    public enum NpcPose
    {
        Idle, Walk, Jog, Sprint, Talk, FoldArms, Sit, SitTalk, Harvest, Plant, Water, Chop, Kneel, Hammer, Carry, CarryIdle,
        Pray, Eat, Lantern, Torch, Cower, Lie, InjuredWalk, Dead, Nod, Wave, Hit, Rail
    }

    /// <summary>What the NPC holds in its right hand.</summary>
    public enum NpcProp { None, Hoe, Axe, Rod, Hammer, Spear, Lantern, Torch, Staff, Basket, Bundle }

    /// <summary>
    /// The visual of a light NPC: the CC0 base body dressed by <see cref="VillagerLook"/>, animated by real clips
    /// through a small Playables mixer evaluated by the NPC scheduler (full rate near the player, a few times per
    /// second further away, not at all when hidden), hand props, and an anime face that blinks and talks.
    /// Gameplay never depends on it: the logical NPC lives in <see cref="NpcSimulation"/>.
    /// </summary>
    public sealed class NpcBody : MonoBehaviour
    {
        private struct ClipDef
        {
            public string Clip;
            public bool Loop;
            /// <summary>Hold this normalized time (lying, sitting down) instead of playing.</summary>
            public float Hold;
            public float Speed;
            public ClipDef(string clip, bool loop = true, float speed = 1f, float hold = -1f) { Clip = clip; Loop = loop; Speed = speed; Hold = hold; }
        }

        private static readonly Dictionary<NpcPose, ClipDef[]> Clips = new Dictionary<NpcPose, ClipDef[]>
        {
            { NpcPose.Idle, new[] { new ClipDef("Idle_Loop") } },
            { NpcPose.Walk, new[] { new ClipDef("Walk_Loop") } },
            { NpcPose.Jog, new[] { new ClipDef("Jog_Fwd_Loop") } },
            { NpcPose.Sprint, new[] { new ClipDef("Sprint_Loop"), new ClipDef("Jog_Fwd_Loop") } },
            { NpcPose.Talk, new[] { new ClipDef("Idle_Talking_Loop"), new ClipDef("Idle_Loop") } },
            { NpcPose.FoldArms, new[] { new ClipDef("Idle_FoldArms_Loop"), new ClipDef("Idle_Loop") } },
            { NpcPose.Sit, new[] { new ClipDef("Sitting_Idle_Loop"), new ClipDef("Crouch_Idle_Loop") } },
            { NpcPose.SitTalk, new[] { new ClipDef("Sitting_Talking_Loop"), new ClipDef("Sitting_Idle_Loop") } },
            { NpcPose.Harvest, new[] { new ClipDef("Farm_Harvest"), new ClipDef("Fixing_Kneeling") } },
            { NpcPose.Plant, new[] { new ClipDef("Farm_PlantSeed"), new ClipDef("Fixing_Kneeling") } },
            { NpcPose.Water, new[] { new ClipDef("Farm_Watering"), new ClipDef("Idle_Loop") } },
            { NpcPose.Chop, new[] { new ClipDef("TreeChopping_Loop"), new ClipDef("Sword_Attack") } },
            { NpcPose.Kneel, new[] { new ClipDef("Fixing_Kneeling"), new ClipDef("Crouch_Idle_Loop") } },
            { NpcPose.Hammer, new[] { new ClipDef("Sword_Attack", true, 0.75f), new ClipDef("Fixing_Kneeling") } },
            { NpcPose.Carry, new[] { new ClipDef("Walk_Carry_Loop"), new ClipDef("Walk_Loop") } },
            { NpcPose.CarryIdle, new[] { new ClipDef("Walk_Carry_Loop", false, 0f, 0.1f), new ClipDef("Idle_Loop") } },
            { NpcPose.Pray, new[] { new ClipDef("Crouch_Idle_Loop", true, 0.5f), new ClipDef("Idle_Loop") } },
            { NpcPose.Eat, new[] { new ClipDef("Consume"), new ClipDef("Idle_Loop") } },
            { NpcPose.Lantern, new[] { new ClipDef("Idle_Lantern_Loop"), new ClipDef("Idle_Loop") } },
            { NpcPose.Torch, new[] { new ClipDef("Idle_Torch_Loop"), new ClipDef("Idle_Lantern_Loop") } },
            { NpcPose.Cower, new[] { new ClipDef("Crouch_Idle_Loop", true, 1.6f), new ClipDef("Idle_Loop") } },
            { NpcPose.Lie, new[] { new ClipDef("LayToIdle", false, 0f, 0.02f), new ClipDef("Death01", false, 0f, 0.99f) } },
            { NpcPose.InjuredWalk, new[] { new ClipDef("Zombie_Walk_Fwd_Loop", true, 0.8f), new ClipDef("Walk_Loop") } },
            { NpcPose.Dead, new[] { new ClipDef("Death01", false) } },
            { NpcPose.Nod, new[] { new ClipDef("Yes", false), new ClipDef("Idle_Talking_Loop") } },
            { NpcPose.Wave, new[] { new ClipDef("Interact", false), new ClipDef("Idle_Talking_Loop") } },
            { NpcPose.Hit, new[] { new ClipDef("Hit_Chest", false) } },
            { NpcPose.Rail, new[] { new ClipDef("Idle_Rail_Loop"), new ClipDef("Idle_Loop") } },
        };

        private sealed class Slot
        {
            public AnimationClipPlayable Playable;
            public int Input;
            public float Length;
            public float Time;
            public float Weight;
            public ClipDef Def;
        }

        public NpcPose Pose { get; private set; } = NpcPose.Idle;
        public NpcProp Prop { get; private set; }
        public AnimeFace Face { get; private set; }
        public float Height { get; private set; } = 1.7f;
        public bool Built => _sk != null;
        public bool ClipsActive => _graph.IsValid();
        public Transform Head => _sk != null ? _sk[HumanBodyBones.Head] : null;
        /// <summary>A renderer visible to any camera this frame (scheduler skips animation otherwise).</summary>
        public bool IsVisible => _bodyRenderer != null && _bodyRenderer.isVisible;

        private HumanoidSkeleton _sk;
        private SkinnedMeshRenderer _bodyRenderer;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();
        private readonly List<Slot> _all = new List<Slot>();
        private Slot _current;
        private NpcPose _gestureReturn;
        private float _gestureUntil = -1f;
        private float _moveSpeed;
        private float _fade = 0.25f;
        private float _pending;
        private Transform _grip, _propRoot, _chest;
        private NightLight _propLight;

        /// <summary>
        /// Builds the visual under <paramref name="parent"/>. Returns null when the base body is missing or cannot be
        /// used (the NPC then keeps a simple placeholder).
        /// </summary>
        public static NpcBody Build(Transform parent, NpcDef def, VillagerOutfit outfit, float height)
        {
            var prefab = Resources.Load<GameObject>(def.Female ? "Quaternius/Characters/Superhero_Female_FullBody" : CharacterVisualProfile.BaseBody);
            if (prefab == null) prefab = Resources.Load<GameObject>(CharacterVisualProfile.BaseBody);
            if (prefab == null) return null;
            var root = new GameObject("NpcVisual");
            root.transform.SetParent(parent, false);
            var body = root.AddComponent<NpcBody>();
            try
            {
                var model = Instantiate(prefab, root.transform, false);
                model.name = "Model";
                var sk = HumanoidSkeleton.Build(model, height, out string report);
                if (sk == null)
                {
                    Debug.LogWarning($"[NpcBody] {def.Id}: base body unusable ({report}).");
                    Destroy(root);
                    return null;
                }
                Layers.SetLayerRecursively(model, Layers.Npc);
                var look = VillagerLookParams.For(def.LookSeed, def.Female, def.Child, def.Elder, outfit);
                var result = VillagerLook.Apply(sk, look, Layers.Npc);
                body._sk = sk;
                body.Height = height;
                if (result != null)
                {
                    body.Face = result.Face;
                    body._bodyRenderer = result.Body;
                    body._renderers.AddRange(result.Renderers);
                }
                else
                {
                    foreach (var r in sk.Renderers) body._renderers.Add(r);
                    body._bodyRenderer = sk.Renderers.Count > 0 ? sk.Renderers[0] as SkinnedMeshRenderer : null;
                }
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!body._renderers.Contains(r)) body._renderers.Add(r);
                    r.gameObject.layer = Layers.Npc;
                }
                body._chest = sk[HumanBodyBones.UpperChest] ?? sk[HumanBodyBones.Chest] ?? sk[HumanBodyBones.Spine];
                body.BuildGrip();
                body.BuildGraph();
                body.SetPose(NpcPose.Idle, 0f);
                body.Evaluate(0f);
                return body;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NpcBody] {def.Id}: could not build the visual ({e.Message}).\n{e.StackTrace}");
                if (root != null) Destroy(root);
                return null;
            }
        }

        private void BuildGrip()
        {
            var hand = _sk[HumanBodyBones.RightHand];
            if (hand == null) return;
            _grip = new GameObject("Grip").transform;
            _grip.SetParent(hand, false);
            _grip.localPosition = _sk.PalmLocalR;
            _grip.localRotation = Quaternion.Inverse(_sk.GripToHandR);
            _grip.localScale = Vector3.one / Mathf.Max(1e-5f, hand.lossyScale.x);
        }

        private void BuildGraph()
        {
            if (!HumanoidClipLibrary.Available || _sk.Animator == null) return;
            var animator = _sk.Animator;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create(name + ".Npc");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Npc", animator);
            _mixer = AnimationMixerPlayable.Create(_graph, 0);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
        }

        private Slot Get(string clipName)
        {
            if (!_graph.IsValid() || string.IsNullOrEmpty(clipName)) return null;
            if (_slots.TryGetValue(clipName, out var s)) return s;
            var clip = HumanoidClipLibrary.Get(clipName);
            if (clip == null)
            {
                _slots[clipName] = null;
                return null;
            }
            var p = AnimationClipPlayable.Create(_graph, clip);
            p.SetApplyFootIK(true);
            p.SetSpeed(0);
            int input = _mixer.AddInput(p, 0, 0f);
            s = new Slot { Playable = p, Input = input, Length = Mathf.Max(0.05f, clip.length) };
            _slots[clipName] = s;
            _all.Add(s);
            return s;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        // ------------------------------------------------------------------ control

        /// <summary>Cross-fades to a pose. Gestures (nod, wave, hit) play once and return to the previous pose.</summary>
        public void SetPose(NpcPose pose, float fade = 0.3f)
        {
            bool gesture = pose == NpcPose.Nod || pose == NpcPose.Wave || pose == NpcPose.Hit;
            if (!gesture && _gestureUntil > 0f)
            {
                _gestureReturn = pose;
                return;
            }
            if (pose == Pose && _current != null && !gesture) return;
            if (!Clips.TryGetValue(pose, out var defs)) return;
            Slot slot = null;
            ClipDef chosen = default;
            foreach (var d in defs)
            {
                slot = Get(d.Clip);
                if (slot == null) continue;
                chosen = d;
                break;
            }
            if (gesture)
            {
                _gestureReturn = _gestureUntil > 0f ? _gestureReturn : Pose;
                _gestureUntil = Time.time + (slot != null ? slot.Length * 0.95f : 1f);
            }
            Pose = pose;
            if (slot == null) return;
            slot.Def = chosen;
            if (_current != slot)
            {
                slot.Time = chosen.Hold >= 0f ? chosen.Hold * slot.Length : (chosen.Loop ? Random.Range(0f, slot.Length) : 0f);
                if (gesture || !chosen.Loop) slot.Time = chosen.Hold >= 0f ? chosen.Hold * slot.Length : 0f;
            }
            _current = slot;
            _fade = Mathf.Max(0.01f, fade);
        }

        /// <summary>Ground speed for locomotion clips (m/s): their playback rate follows it so feet do not slide.</summary>
        public void SetMoveSpeed(float metersPerSecond) => _moveSpeed = metersPerSecond;

        /// <summary>
        /// Advances the animation (called by the NPC scheduler; at lower rates for distant NPCs, with the accumulated
        /// time). Evaluates the graph and fixes hands.
        /// </summary>
        public void Tick(float dt, bool evaluate)
        {
            if (_gestureUntil > 0f && Time.time >= _gestureUntil)
            {
                _gestureUntil = -1f;
                var back = _gestureReturn;
                Pose = NpcPose.Hit; // force the change
                SetPose(back, 0.25f);
            }
            _pending += dt;
            if (!evaluate || !_graph.IsValid()) return;
            Evaluate(_pending);
            _pending = 0f;
        }

        private void Evaluate(float dt)
        {
            if (!_graph.IsValid()) return;
            float step = dt / _fade;
            float rate = 1f;
            if (_current != null)
            {
                string clip = _current.Def.Clip;
                if (HumanoidClipLibrary.NaturalSpeed.TryGetValue(clip, out float natural) && _moveSpeed > 0.05f)
                    rate = Mathf.Clamp(_moveSpeed / (natural * Height / HumanoidClipLibrary.ReferenceHeight), 0.5f, 2.2f);
                else if (clip == "Walk_Carry_Loop" && _moveSpeed > 0.05f)
                    rate = Mathf.Clamp(_moveSpeed / (0.9f * Height / HumanoidClipLibrary.ReferenceHeight), 0.5f, 2f);
            }
            float total = 0f;
            foreach (var s in _all)
            {
                float target = s == _current ? 1f : 0f;
                s.Weight = Mathf.MoveTowards(s.Weight, target, step);
                if (s.Weight <= 0f) continue;
                var d = s.Def;
                if (d.Hold >= 0f) s.Time = d.Hold * s.Length;
                else
                {
                    s.Time += dt * d.Speed * (s == _current ? rate : 1f);
                    s.Time = d.Loop ? Mathf.Repeat(s.Time, s.Length) : Mathf.Min(s.Time, s.Length - 0.01f);
                }
                total += s.Weight;
            }
            foreach (var s in _all)
            {
                float w = total > 1e-4f ? s.Weight / total : (s == _current ? 1f : 0f);
                _mixer.SetInputWeight(s.Input, w);
                if (w > 0f) s.Playable.SetTime(s.Time);
            }
            _graph.Evaluate(0f);
            if (Prop != NpcProp.None && Prop != NpcProp.Basket && Prop != NpcProp.Bundle) _sk.CurlFingers(true, 1f);
        }

        // ------------------------------------------------------------------ props

        public void SetProp(NpcProp prop)
        {
            if (prop == Prop) return;
            Prop = prop;
            if (_propRoot != null) Destroy(_propRoot.gameObject);
            _propRoot = null;
            _propLight = null;
            if (prop == NpcProp.None) return;
            bool chestProp = prop == NpcProp.Basket || prop == NpcProp.Bundle;
            var parent = chestProp ? _chest : _grip;
            if (parent == null) return;
            _propRoot = new GameObject("Prop_" + prop).transform;
            _propRoot.SetParent(parent, false);
            float k = Height / 1.75f;
            if (chestProp)
            {
                _propRoot.localScale = Vector3.one / Mathf.Max(1e-5f, parent.lossyScale.x);
                var rootRot = transform.rotation;
                _propRoot.rotation = rootRot;
                var wood = RegionMats.Straw;
                if (prop == NpcProp.Basket)
                {
                    _propRoot.position = parent.position + rootRot * new Vector3(0f, -0.12f * k, 0.32f * k);
                    Part(PrimitiveType.Cylinder, wood, Vector3.zero, Vector3.zero, new Vector3(0.42f, 0.13f, 0.32f) * k);
                    Part(PrimitiveType.Sphere, RegionMats.Plant(new Color(0.5f, 0.62f, 0.3f)), Vector3.up * 0.1f * k, Vector3.zero, new Vector3(0.34f, 0.12f, 0.26f) * k);
                }
                else
                {
                    _propRoot.position = parent.position + rootRot * new Vector3(0f, 0.02f * k, -0.22f * k);
                    Part(PrimitiveType.Sphere, RegionMats.Cloth(new Color(0.55f, 0.48f, 0.38f)), Vector3.zero, Vector3.zero, new Vector3(0.36f, 0.42f, 0.24f) * k);
                }
                SetLayer(_propRoot);
                return;
            }
            // Grip frame: +Z along the held shaft (away from the fist), +Y the "edge" side.
            var shaft = RegionMats.Wood;
            switch (prop)
            {
                case NpcProp.Hoe:
                    Part(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0f, 0.35f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 0.68f, 0.035f));
                    Part(PrimitiveType.Cube, RegionMats.Iron, new Vector3(0f, -0.08f, 1.0f), Vector3.zero, new Vector3(0.05f, 0.18f, 0.03f));
                    break;
                case NpcProp.Axe:
                    Part(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0f, 0.22f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.4f, 0.04f));
                    Part(PrimitiveType.Cube, RegionMats.Iron, new Vector3(0f, 0.07f, 0.55f), Vector3.zero, new Vector3(0.03f, 0.16f, 0.12f));
                    break;
                case NpcProp.Rod:
                    Part(PrimitiveType.Cylinder, RegionMats.FreshWood, new Vector3(0f, 0f, 1.0f), new Vector3(90f, 0f, 0f), new Vector3(0.025f, 1.2f, 0.025f));
                    break;
                case NpcProp.Hammer:
                    Part(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0f, 0.12f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.18f, 0.03f));
                    Part(PrimitiveType.Cube, RegionMats.Iron, new Vector3(0f, 0f, 0.3f), Vector3.zero, new Vector3(0.07f, 0.13f, 0.07f));
                    break;
                case NpcProp.Spear:
                    Part(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0f, 0.55f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 1.05f, 0.035f));
                    Part(ProceduralMeshes.Cone(6), RegionMats.Iron, new Vector3(0f, 0f, 1.6f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.22f, 0.03f));
                    break;
                case NpcProp.Staff:
                    Part(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0f, 0.2f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.8f, 0.04f));
                    break;
                case NpcProp.Lantern:
                case NpcProp.Torch:
                    {
                        bool torch = prop == NpcProp.Torch;
                        Part(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0f, 0.2f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.25f, 0.03f));
                        var glow = Part(torch ? PrimitiveType.Sphere : PrimitiveType.Cylinder, torch ? RegionMats.Coals : RegionMats.Lantern,
                            torch ? new Vector3(0f, 0f, 0.48f) : new Vector3(0f, -0.12f, 0.45f), Vector3.zero, torch ? new Vector3(0.1f, 0.14f, 0.1f) : new Vector3(0.16f, 0.13f, 0.16f));
                        _propLight = NightLight.Create(_propRoot, glow.transform.position, torch ? new Color(1f, 0.55f, 0.25f) : new Color(1f, 0.75f, 0.45f), 1.4f, 5.5f,
                            new Color(1f, 0.6f, 0.3f) * 2f, glow.GetComponent<Renderer>());
                        break;
                    }
            }
            _propRoot.localScale = Vector3.one * k;
            SetLayer(_propRoot);
        }

        private GameObject Part(PrimitiveType type, Material m, Vector3 pos, Vector3 euler, Vector3 scale) =>
            Part(ProceduralMeshes.Primitive(type), m, pos, euler, scale);

        private GameObject Part(Mesh mesh, Material m, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var go = ProceduralMeshes.CreatePart("Part", mesh, m, _propRoot, pos, Quaternion.Euler(euler), scale);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        private static void SetLayer(Transform t) => Layers.SetLayerRecursively(t.gameObject, Layers.Npc);

        /// <summary>Shows / hides every renderer (indoors, culled) without destroying the visual.</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
