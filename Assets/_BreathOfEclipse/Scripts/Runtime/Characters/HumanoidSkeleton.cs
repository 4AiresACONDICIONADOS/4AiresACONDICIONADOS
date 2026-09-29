using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Bones and rest-pose measurements of an imported Humanoid model, taken once when it is instantiated (bind
    /// pose): canonical rest rotations for retargeting, height / hip / arm / leg lengths, the katana grip frame of
    /// each hand (from the finger bones) and finger curl axes. <see cref="Build"/> also validates the model: invalid
    /// avatar, missing core bones or an absurd scale make it fail so the procedural mannequin stays in use.
    /// </summary>
    public sealed class HumanoidSkeleton
    {
        public sealed class Finger
        {
            public Transform[] Bones;
            public Quaternion[] RestLocal;
            /// <summary>Curl axis in the hand's local space (rotating about it closes the finger toward the palm).</summary>
            public Vector3 AxisInHand;
            public bool Thumb;
        }

        public Animator Animator { get; private set; }
        public Transform ModelRoot { get; private set; }
        public float Height { get; private set; }
        public float HipHeight { get; private set; }
        public float AnkleHeight { get; private set; }
        public float ArmLengthR { get; private set; }
        public float ArmLengthL { get; private set; }
        public float LegLength { get; private set; }
        /// <summary>Rotation that turns the model's rest facing onto +Z of its root (identity for Unity-ready models).</summary>
        public Quaternion FacingFix { get; private set; } = Quaternion.identity;
        /// <summary>Grip point (katana handle centre) in the hand bone's local space.</summary>
        public Vector3 PalmLocalR { get; private set; }
        public Vector3 PalmLocalL { get; private set; }
        /// <summary>hand rotation = grip frame (forward = blade, up = cutting edge) × GripToHand.</summary>
        public Quaternion GripToHandR { get; private set; } = Quaternion.identity;
        public Quaternion GripToHandL { get; private set; } = Quaternion.identity;
        public Finger[] FingersR { get; private set; } = new Finger[0];
        public Finger[] FingersL { get; private set; } = new Finger[0];
        public string Report { get; private set; }
        /// <summary>Renderers of the model (body, eyes, eyebrows…).</summary>
        public List<Renderer> Renderers { get; } = new List<Renderer>();

        private readonly Transform[] _bones = new Transform[(int)HumanBodyBones.LastBone];
        private readonly Quaternion[] _rest = new Quaternion[(int)HumanBodyBones.LastBone];
        private readonly Quaternion[] _restLocal = new Quaternion[(int)HumanBodyBones.LastBone];
        private Vector3 _hipsRestLocalPosition;

        public Transform this[HumanBodyBones b] => b >= 0 && b < HumanBodyBones.LastBone ? _bones[(int)b] : null;
        /// <summary>Rest rotation of a bone in the canonical model frame (facing +Z, y up).</summary>
        public Quaternion Rest(HumanBodyBones b) => _rest[(int)b];
        public Quaternion RestLocal(HumanBodyBones b) => _restLocal[(int)b];

        private static readonly HumanBodyBones[] Required =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
        };

        /// <summary>
        /// Measures and validates <paramref name="model"/> (freshly instantiated, still in its bind pose) and scales
        /// its root so it stands <paramref name="targetHeight"/> metres tall (0 keeps the imported scale).
        /// Returns null when the model cannot be used; <paramref name="report"/> says why.
        /// </summary>
        public static HumanoidSkeleton Build(GameObject model, float targetHeight, out string report)
        {
            var sb = new StringBuilder();
            var sk = new HumanoidSkeleton { ModelRoot = model.transform };
            sk.Animator = model.GetComponentInChildren<Animator>();
            bool ok = true;
            if (sk.Animator == null)
            {
                sb.AppendLine("no Animator on the model");
                ok = false;
            }
            else if (sk.Animator.avatar == null || !sk.Animator.avatar.isValid || !sk.Animator.avatar.isHuman)
            {
                sb.AppendLine($"invalid avatar ({(sk.Animator.avatar == null ? "none" : sk.Animator.avatar.isValid ? "not humanoid" : "invalid")}): set Rig → Animation Type = Humanoid");
                ok = false;
            }
            if (ok)
            {
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    sk._bones[i] = sk.Animator.GetBoneTransform((HumanBodyBones)i);
                var missing = new List<string>();
                foreach (var b in Required)
                    if (sk[b] == null) missing.Add(b.ToString());
                if (missing.Count > 0)
                {
                    sb.AppendLine("missing bones: " + string.Join(", ", missing));
                    ok = false;
                }
            }
            model.GetComponentsInChildren(true, sk.Renderers);
            if (sk.Renderers.Count == 0)
            {
                sb.AppendLine("no renderers");
                ok = false;
            }
            if (!ok)
            {
                report = sb.ToString().Trim();
                sk.Report = report;
                return null;
            }

            // ---- height and scale normalization
            float rawHeight = MeasureHeight(sk, out float minY);
            if (rawHeight < 0.05f || rawHeight > 500f || float.IsNaN(rawHeight))
            {
                report = $"root scale looks wrong (model height {rawHeight:0.###} units)";
                sk.Report = report;
                return null;
            }
            if (targetHeight > 0.1f)
            {
                float s = targetHeight / rawHeight;
                model.transform.localScale = model.transform.localScale * s;
                sb.AppendLine($"height {rawHeight:0.00} → {targetHeight:0.00} m (×{s:0.###})");
            }
            else sb.AppendLine($"height {rawHeight:0.00} m (imported scale kept)");
            sk.Height = targetHeight > 0.1f ? targetHeight : rawHeight;
            // Soles on the ground: the lowest vertex sits at the gameplay root's height.
            model.transform.localPosition += Vector3.up * (-minY * model.transform.lossyScale.y);

            // ---- facing (right shoulder − left shoulder, crossed with up)
            var root = model.transform;
            Vector3 right = root.InverseTransformDirection(sk[HumanBodyBones.RightUpperArm].position - sk[HumanBodyBones.LeftUpperArm].position);
            right.y = 0f;
            Vector3 fwd = right.sqrMagnitude > 1e-6f ? Vector3.Cross(right.normalized, Vector3.up) : Vector3.forward;
            sk.FacingFix = Quaternion.FromToRotation(fwd, Vector3.forward);
            if (Vector3.Dot(fwd, Vector3.forward) < 0.7f) sb.AppendLine($"model faces {fwd} in its rest pose: corrected");

            Quaternion canon = root.rotation * Quaternion.Inverse(sk.FacingFix);
            Quaternion invCanon = Quaternion.Inverse(canon);
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var t = sk._bones[i];
                if (t == null) continue;
                sk._rest[i] = invCanon * t.rotation;
                sk._restLocal[i] = t.localRotation;
            }
            sk._hipsRestLocalPosition = sk[HumanBodyBones.Hips].localPosition;

            // ---- lengths (canonical metres after scaling)
            float ground = root.parent != null ? root.parent.position.y : root.position.y + minY * root.lossyScale.y;
            float footY = Mathf.Min(sk[HumanBodyBones.LeftFoot].position.y, sk[HumanBodyBones.RightFoot].position.y);
            sk.HipHeight = sk[HumanBodyBones.Hips].position.y - ground;
            sk.AnkleHeight = Mathf.Max(0.02f, footY - ground);
            sk.ArmLengthR = Dist(sk[HumanBodyBones.RightUpperArm], sk[HumanBodyBones.RightLowerArm]) + Dist(sk[HumanBodyBones.RightLowerArm], sk[HumanBodyBones.RightHand]);
            sk.ArmLengthL = Dist(sk[HumanBodyBones.LeftUpperArm], sk[HumanBodyBones.LeftLowerArm]) + Dist(sk[HumanBodyBones.LeftLowerArm], sk[HumanBodyBones.LeftHand]);
            sk.LegLength = Dist(sk[HumanBodyBones.LeftUpperLeg], sk[HumanBodyBones.LeftLowerLeg]) + Dist(sk[HumanBodyBones.LeftLowerLeg], sk[HumanBodyBones.LeftFoot]);
            if (sk.HipHeight < sk.Height * 0.3f || sk.HipHeight > sk.Height * 0.75f)
                sb.AppendLine($"unusual hip height {sk.HipHeight:0.00} m for a {sk.Height:0.00} m character");

            // ---- hands: katana grip frames and finger curl
            sk.BuildHand(true, canon);
            sk.BuildHand(false, canon);
            if (sk[HumanBodyBones.RightMiddleProximal] == null) sb.AppendLine("no finger bones: grip estimated from the forearm");
            if (sk[HumanBodyBones.Chest] == null) sb.AppendLine("no chest bone (spine only)");
            sb.Insert(0, $"OK · hips {sk.HipHeight:0.00} m · arms {sk.ArmLengthR:0.00} m · legs {sk.LegLength:0.00} m\n");
            report = sb.ToString().Trim();
            sk.Report = report;
            return sk;
        }

        private static float Dist(Transform a, Transform b) => a != null && b != null ? Vector3.Distance(a.position, b.position) : 0f;

        /// <summary>Bind-pose height of the skinned meshes (falls back to the head bone).</summary>
        private static float MeasureHeight(HumanoidSkeleton sk, out float minY)
        {
            var root = sk.ModelRoot;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            var baked = new Mesh();
            var verts = new List<Vector3>();
            foreach (var r in sk.Renderers)
            {
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    try
                    {
                        smr.BakeMesh(baked, true);
                    }
                    catch
                    {
                        continue;
                    }
                    baked.GetVertices(verts);
                    var m = smr.transform.localToWorldMatrix;
                    foreach (var v in verts)
                    {
                        float y = root.InverseTransformPoint(m.MultiplyPoint3x4(v)).y;
                        if (y < lo) lo = y;
                        if (y > hi) hi = y;
                    }
                }
                else if (r is MeshRenderer)
                {
                    var b = r.bounds;
                    float y0 = root.InverseTransformPoint(b.min).y, y1 = root.InverseTransformPoint(b.max).y;
                    lo = Mathf.Min(lo, Mathf.Min(y0, y1));
                    hi = Mathf.Max(hi, Mathf.Max(y0, y1));
                }
            }
            if (Application.isPlaying) Object.Destroy(baked);
            else Object.DestroyImmediate(baked);
            if (float.IsInfinity(lo) || hi - lo < 1e-4f)
            {
                var head = sk[HumanBodyBones.Head];
                float h = head != null ? root.InverseTransformPoint(head.position).y * 1.13f : 0f;
                minY = 0f;
                return h * root.lossyScale.y;
            }
            minY = lo;
            // InverseTransformPoint removes the root scale: convert back to parent units.
            float bakedHeight = (hi - lo) * root.lossyScale.y;
            // Cross-check with the bone chain (head bone ≈ 87% of the height above the ankles): a mesh baked with an
            // unexpected node scale must not shrink or blow up the character.
            var headBone = sk[HumanBodyBones.Head];
            var fl = sk[HumanBodyBones.LeftFoot];
            var fr = sk[HumanBodyBones.RightFoot];
            if (headBone != null && fl != null && fr != null)
            {
                float ankleLocal = Mathf.Min(root.InverseTransformPoint(fl.position).y, root.InverseTransformPoint(fr.position).y);
                float headLocal = root.InverseTransformPoint(headBone.position).y;
                float estimate = (headLocal - ankleLocal) * 1.2f * root.lossyScale.y;
                if (estimate > 1e-4f && Mathf.Abs(bakedHeight - estimate) / estimate > 0.25f)
                {
                    minY = ankleLocal - (headLocal - ankleLocal) * 0.055f;
                    return estimate;
                }
            }
            return bakedHeight;
        }

        private void BuildHand(bool right, Quaternion canon)
        {
            var hand = this[right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand];
            var lower = this[right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm];
            var middle = this[right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal];
            var index = this[right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal];
            var little = this[right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal];
            if (little == null) little = this[right ? HumanBodyBones.RightRingProximal : HumanBodyBones.LeftRingProximal];

            Vector3 fingers = middle != null ? middle.position - hand.position : hand.position - lower.position;
            float palmLen = fingers.magnitude;
            if (palmLen < 1e-5f)
            {
                fingers = canon * (right ? Vector3.right : Vector3.left);
                palmLen = 0.08f * Height / 1.8f;
            }
            fingers.Normalize();
            if (middle == null) palmLen = Mathf.Min(palmLen, 0.09f * Height / 1.8f);
            Vector3 thumbSide = index != null && little != null ? index.position - little.position : canon * Vector3.forward;
            thumbSide = Vector3.ProjectOnPlane(thumbSide, fingers);
            if (thumbSide.sqrMagnitude < 1e-8f) thumbSide = Vector3.ProjectOnPlane(canon * Vector3.forward, fingers);
            thumbSide.Normalize();
            Vector3 palm = right ? Vector3.Cross(fingers, thumbSide) : Vector3.Cross(thumbSide, fingers);
            palm.Normalize();

            // Grip frame: the handle lies diagonally across the palm (heel of the hand → index knuckle), so the blade
            // leaves the fist on the thumb side tilted toward the fingers and the edge faces the front of the fist.
            // The tilt keeps the wrists straight in guard (forearm, fist and blade line up like a real kamae).
            const float tilt = 38f * Mathf.Deg2Rad;
            Vector3 blade = (thumbSide * Mathf.Cos(tilt) + fingers * Mathf.Sin(tilt)).normalized;
            Vector3 edge = Vector3.ProjectOnPlane(fingers, blade).normalized;
            Quaternion grip = Quaternion.LookRotation(blade, edge);
            Quaternion gripToHand = Quaternion.Inverse(grip) * hand.rotation;
            float scale = Height / 1.8f;
            Vector3 palmPoint = hand.position + fingers * (palmLen * 0.55f) + palm * (0.028f * scale);
            Vector3 palmLocal = hand.InverseTransformPoint(palmPoint);

            var list = new List<Finger>();
            AddFinger(list, hand, palm, false, right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            AddFinger(list, hand, palm, false, right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            AddFinger(list, hand, palm, false, right ? HumanBodyBones.RightRingProximal : HumanBodyBones.LeftRingProximal);
            AddFinger(list, hand, palm, false, right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            AddFinger(list, hand, palm, true, right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);

            if (right)
            {
                GripToHandR = gripToHand;
                PalmLocalR = palmLocal;
                FingersR = list.ToArray();
            }
            else
            {
                GripToHandL = gripToHand;
                PalmLocalL = palmLocal;
                FingersL = list.ToArray();
            }
        }

        private void AddFinger(List<Finger> list, Transform hand, Vector3 palm, bool thumb, HumanBodyBones proximal)
        {
            var b0 = this[proximal];
            var b1 = this[proximal + 1];
            var b2 = this[proximal + 2];
            if (b0 == null || b1 == null) return;
            Vector3 dir = b1.position - b0.position;
            if (dir.sqrMagnitude < 1e-10f) return;
            Vector3 axis = Vector3.Cross(dir.normalized, palm);
            if (axis.sqrMagnitude < 1e-8f) return;
            var bones = b2 != null ? new[] { b0, b1, b2 } : new[] { b0, b1 };
            var rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localRotation;
            list.Add(new Finger
            {
                Bones = bones,
                RestLocal = rest,
                AxisInHand = Quaternion.Inverse(hand.rotation) * axis.normalized,
                Thumb = thumb
            });
        }

        /// <summary>Puts every mapped bone back in its bind pose (used when no clip rewrites the pose each frame).</summary>
        public void ResetToRest()
        {
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i] != null) _bones[i].localRotation = _restLocal[i];
            var hips = this[HumanBodyBones.Hips];
            if (hips != null) hips.localPosition = _hipsRestLocalPosition;
        }

        /// <summary>Closes the fingers of a hand into a fist around a handle (0 = open rest pose, 1 = full grip).</summary>
        public void CurlFingers(bool right, float amount)
        {
            var hand = this[right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand];
            var fingers = right ? FingersR : FingersL;
            if (hand == null || amount <= 0f) return;
            foreach (var f in fingers)
            {
                Vector3 axis = hand.rotation * f.AxisInHand;
                for (int i = 0; i < f.Bones.Length; i++)
                {
                    var b = f.Bones[i];
                    if (b == null) continue;
                    b.localRotation = f.RestLocal[i];
                    float angle = f.Thumb ? (i == 0 ? 18f : 30f) : (i == 0 ? 72f : i == 1 ? 88f : 55f);
                    b.rotation = Quaternion.AngleAxis(angle * amount, axis) * b.rotation;
                }
            }
        }
    }
}
