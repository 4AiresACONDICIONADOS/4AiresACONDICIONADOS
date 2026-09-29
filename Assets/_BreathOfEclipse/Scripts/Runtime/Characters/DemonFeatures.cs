using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Demon parts attached to the model's bones: claws on every fingertip, fangs, horns (small curved pair for
    /// the Nightspawn, huge swept horns for the Hollow Oni — named "Horn*" so the phase-2 transformation grows
    /// them) and a row of spines down the Nightspawn's back.
    /// </summary>
    public static class DemonFeatures
    {
        private static readonly HumanBodyBones[] Distal =
        {
            HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbDistal, HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleDistal
        };

        public static void Build(AnimeBodyMesh bm, CharacterRig rig, int layer, bool oni, float outline)
        {
            var sk = bm.Skeleton;
            float k = bm.K;
            var clawMat = MaterialFactory.AnimeCharacter(oni ? new Color(0.25f, 0.2f, 0.18f) : new Color(0.78f, 0.74f, 0.7f), MaterialFactory.CharacterSurface.Bone, outline * 0.5f);
            var hornMat = MaterialFactory.AnimeCharacter(oni ? new Color(0.9f, 0.86f, 0.76f) : new Color(0.16f, 0.1f, 0.18f), MaterialFactory.CharacterSurface.Bone, outline);
            var toothMat = MaterialFactory.AnimeCharacter(new Color(0.95f, 0.93f, 0.88f), MaterialFactory.CharacterSurface.Bone, 0f);

            // ---- claws
            foreach (var d in Distal)
            {
                var tip = sk[d];
                var mid = sk[d - 1];
                if (tip == null || mid == null) continue;
                Vector3 dir = tip.position - mid.position;
                float seg = dir.magnitude;
                if (seg < 1e-5f) continue;
                dir /= seg;
                bool thumb = d == HumanBodyBones.LeftThumbDistal || d == HumanBodyBones.RightThumbDistal;
                float len = (thumb ? 0.04f : 0.055f) * k * (oni ? 0.8f : 1f);
                Vector3 start = tip.position + dir * seg * 0.55f;
                Vector3 palmward = Vector3.ProjectOnPlane(Vector3.down, dir);
                Part("Claw", tip, start, dir, palmward, len, 0.009f * k, clawMat, rig, layer, 0.35f);
            }

            // ---- fangs in front of the lips (the demon mouth stays parted)
            var head = sk[HumanBodyBones.Head];
            if (head != null)
            {
                Vector3 lips = bm.CanonToWorld.MultiplyPoint3x4(AnimeCharacterLook.MouthPoint(bm) + new Vector3(0f, 0f, 0.001f * k));
                Vector3 right = bm.CanonToWorld.MultiplyVector(Vector3.right);
                Vector3 fwd = bm.CanonToWorld.MultiplyVector(Vector3.forward);
                for (int i = -2; i <= 2; i++)
                {
                    bool canine = Mathf.Abs(i) == 2;
                    float len = (canine ? 0.022f : 0.011f) * k;
                    Vector3 p = lips + right * (i * 0.0065f * k) + Vector3.up * (0.004f * k);
                    Part("Fang", head, p, Vector3.down, fwd, len, (canine ? 0.0055f : 0.004f) * k, toothMat, rig, layer, 0f);
                }
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector3 p = lips + right * (i * 0.011f * k) - Vector3.up * (0.009f * k);
                    Part("Fang", head, p, Vector3.up, fwd, 0.013f * k, 0.0045f * k, toothMat, rig, layer, 0f);
                }

                // ---- horns on the skull
                float spread = oni ? 34f : 26f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 canonDir = Quaternion.Euler(0f, s * spread, 0f) * Quaternion.Euler(oni ? -45f : -55f, 0f, 0f) * Vector3.forward;
                    Vector3 root = SkullPoint(bm, canonDir);
                    Vector3 grow = oni
                        ? (Vector3.up * 0.75f + Vector3.right * (s * 0.55f) + Vector3.back * 0.15f).normalized
                        : (Vector3.up * 0.8f + Vector3.right * (s * 0.3f) + Vector3.forward * 0.2f).normalized;
                    Vector3 curl = oni ? (Vector3.back + Vector3.right * (-s * 0.4f)).normalized : Vector3.back;
                    var horn = Part(s < 0 ? "HornL" : "HornR", head, bm.CanonToWorld.MultiplyPoint3x4(root), bm.CanonToWorld.MultiplyVector(grow),
                        bm.CanonToWorld.MultiplyVector(curl), (oni ? 0.26f : 0.09f) * k, (oni ? 0.07f : 0.03f) * k, hornMat, rig, layer, oni ? 0.45f : 0.3f);
                    horn.name = s < 0 ? "HornL" : "HornR";
                }
            }

            // ---- spines down the back (Nightspawn)
            if (!oni)
            {
                HumanBodyBones[] along = { HumanBodyBones.Neck, HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine };
                float[] lengths = { 0.06f, 0.11f, 0.12f, 0.09f };
                for (int i = 0; i < along.Length; i++)
                {
                    var bone = sk[along[i]];
                    if (bone == null) continue;
                    Vector3 j = bm.Joint(along[i]);
                    float back = 0f;
                    for (int v = 0; v < bm.V.Length; v++)
                    {
                        if (Mathf.Abs(bm.V[v].y - j.y) > 0.03f * k || Mathf.Abs(bm.V[v].x) > 0.03f * k) continue;
                        if (bm.Region[v] != BodyRegion.Torso && bm.Region[v] != BodyRegion.Neck) continue;
                        back = Mathf.Max(back, j.z - bm.V[v].z);
                    }
                    if (back <= 0f) continue;
                    Vector3 root = new Vector3(j.x, j.y, j.z - back + 0.004f * k);
                    Vector3 dir = (Vector3.back * 0.8f + Vector3.up * 0.6f).normalized;
                    Part("Spine", bone, bm.CanonToWorld.MultiplyPoint3x4(root), bm.CanonToWorld.MultiplyVector(dir), bm.CanonToWorld.MultiplyVector(Vector3.up),
                        lengths[i] * k, 0.022f * k, hornMat, rig, layer, 0.2f);
                }
            }
        }

        /// <summary>Point of the skull (head vertices' envelope) in a canonical direction from the head centre.</summary>
        private static Vector3 SkullPoint(AnimeBodyMesh bm, Vector3 dir)
        {
            Vector3 lo = Vector3.one * 1e9f, hi = Vector3.one * -1e9f;
            for (int i = 0; i < bm.V.Length; i++)
            {
                if (bm.Region[i] != BodyRegion.Head) continue;
                lo = Vector3.Min(lo, bm.V[i]);
                hi = Vector3.Max(hi, bm.V[i]);
            }
            var c = new Vector3((lo.x + hi.x) * 0.5f, hi.y - 0.095f * bm.K, (lo.z + hi.z) * 0.5f - 0.012f * bm.K);
            float best = 0f;
            for (int i = 0; i < bm.V.Length; i++)
            {
                if (bm.Region[i] != BodyRegion.Head) continue;
                best = Mathf.Max(best, Vector3.Dot(bm.V[i] - c, dir));
            }
            return c + dir * (best - 0.004f * bm.K);
        }

        /// <summary>A cone (base at <paramref name="start"/>) growing along <paramref name="dir"/>, bending toward <paramref name="bendDir"/>.</summary>
        private static Transform Part(string name, Transform parent, Vector3 start, Vector3 dir, Vector3 bendDir, float length, float radius, Material mat,
            CharacterRig rig, int layer, float bend)
        {
            dir.Normalize();
            Vector3 b = Vector3.ProjectOnPlane(bendDir, dir);
            if (b.sqrMagnitude < 1e-6f) b = Vector3.ProjectOnPlane(Vector3.forward, dir);
            if (b.sqrMagnitude < 1e-6f) b = Vector3.ProjectOnPlane(Vector3.right, dir);
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.position = start;
            t.rotation = Quaternion.LookRotation(b.normalized, dir);
            float s = 1f / Mathf.Max(1e-5f, parent.lossyScale.x);
            t.localScale = new Vector3(radius * 2f, length, radius * 2f) * s;
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Cone(8, bend);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            rig.RegisterExternal(mr, CharacterRig.ExternalPart.None, Color.white);
            return t;
        }
    }
}
