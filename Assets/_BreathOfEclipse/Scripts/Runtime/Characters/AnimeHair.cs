using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Procedural anime hair fitted to the model's skull (measured from the head vertices): a cap that follows
    /// the head's convex envelope with a hairline cut around the face, layered tapered locks flowing back and
    /// down, bangs, side locks and a high ponytail (or a wild mane). Swinging parts are skinned to
    /// <see cref="SpringBoneChain"/>s with head / back colliders.
    /// </summary>
    public static class AnimeHair
    {
        public enum Style
        {
            /// <summary>Player: layered spikes, bangs, side locks, high ponytail tied with a cord.</summary>
            Swordsman = 0,
            /// <summary>Hollow Oni: long wild mane down the back, no bangs (the mask covers the face).</summary>
            Mane = 1,
            /// <summary>Nightspawn: short ragged spikes.</summary>
            Ragged = 2
        }

        private sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<int> T = new List<int>();
            public readonly List<float> Param = new List<float>();
        }

        private static readonly Vector3 G = Vector3.down;

        public static void Build(AnimeBodyMesh body, CharacterRig rig, Style style, Color color, Color tieColor, int layer, float outline, int seed)
        {
            var sk = body.Skeleton;
            var head = sk[HumanBodyBones.Head];
            if (head == null) return;
            float k = body.K;
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---- skull envelope from the head vertices
            var headVerts = new List<Vector3>();
            Vector3 lo = Vector3.one * 1e9f, hi = Vector3.one * -1e9f;
            for (int i = 0; i < body.V.Length; i++)
            {
                if (body.Region[i] != BodyRegion.Head) continue;
                headVerts.Add(body.V[i]);
                lo = Vector3.Min(lo, body.V[i]);
                hi = Vector3.Max(hi, body.V[i]);
            }
            if (headVerts.Count < 20) return;
            var c = new Vector3((lo.x + hi.x) * 0.5f, hi.y - 0.095f * k, (lo.z + hi.z) * 0.5f - 0.012f * k);
            float Support(Vector3 u)
            {
                float best = 0f;
                foreach (var v in headVerts)
                {
                    float d = Vector3.Dot(v - c, u);
                    if (d > best) best = d;
                }
                return best;
            }
            Vector3 Dir(float azDeg, float elDeg)
            {
                float az = azDeg * Mathf.Deg2Rad, el = elDeg * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            }
            float capGap = 0.011f * k;
            Vector3 OnCap(float az, float el, float inset = 0.004f)
            {
                var u = Dir(az, el);
                return c + u * (Support(u) + capGap - inset * k);
            }

            var solid = new Builder();
            BuildCap(solid, c, Support, Dir, capGap, style);

            // ---- static locks
            if (style == Style.Swordsman)
            {
                // bangs over the forehead, asymmetric
                float[,] bangs = { { -40f, 0.085f, 0.03f, 0.2f }, { -22f, 0.1f, 0.032f, -0.1f }, { -4f, 0.095f, 0.034f, 0.15f }, { 14f, 0.1f, 0.032f, -0.2f }, { 32f, 0.085f, 0.03f, 0.1f }, { 48f, 0.07f, 0.026f, 0f } };
                for (int i = 0; i < bangs.GetLength(0); i++)
                {
                    float az = bangs[i, 0];
                    var u = Dir(az, 30f);
                    var p = OnCap(az, 30f);
                    Vector3 d = u * 0.35f + G * 0.8f + Vector3.forward * 0.25f;
                    Vector3 bend = Vector3.forward * 0.9f + Vector3.right * (az == 0f ? 0f : Mathf.Sign(az) * 0.3f);
                    Lock(solid, p, d, bangs[i, 1] * k, bangs[i, 2] * k, bend, bangs[i, 3], 0.4f, 6);
                }
            }
            int[] rowsN;
            float[] rowsEl, rowsL, rowsW;
            if (style == Style.Mane)
            {
                rowsEl = new[] { 78f, 58f, 38f, 18f, -2f, -22f };
                rowsN = new[] { 5, 8, 10, 11, 10, 8 };
                rowsL = new[] { 0.16f, 0.22f, 0.3f, 0.36f, 0.38f, 0.34f };
                rowsW = new[] { 0.055f, 0.06f, 0.06f, 0.058f, 0.052f, 0.045f };
            }
            else if (style == Style.Ragged)
            {
                rowsEl = new[] { 70f, 45f, 20f, -5f };
                rowsN = new[] { 5, 7, 8, 7 };
                rowsL = new[] { 0.07f, 0.08f, 0.08f, 0.07f };
                rowsW = new[] { 0.04f, 0.04f, 0.038f, 0.034f };
            }
            else
            {
                rowsEl = new[] { 78f, 60f, 40f, 18f, -5f, -25f };
                rowsN = new[] { 5, 7, 9, 9, 8, 6 };
                rowsL = new[] { 0.1f, 0.12f, 0.13f, 0.13f, 0.12f, 0.1f };
                rowsW = new[] { 0.05f, 0.052f, 0.05f, 0.046f, 0.042f, 0.036f };
            }
            for (int row = 0; row < rowsEl.Length; row++)
            {
                float el = rowsEl[row];
                int n = rowsN[row];
                float spread = el > 50f ? 0.55f : el > 10f ? 0.8f : 0.9f;
                for (int i = 0; i < n; i++)
                {
                    float az = 180f + (i - (n - 1) * 0.5f) * (300f / n) * spread + R(-6f, 6f);
                    float faceAz = Mathf.Abs(Mathf.DeltaAngle(az, 0f));
                    if (el < 30f && faceAz < (style == Style.Mane ? 60f : 45f)) continue; // keep the face clear
                    float e = el + R(-4f, 4f);
                    var u = Dir(az, e);
                    var p = OnCap(az, e);
                    var back = Vector3.back;
                    var side = new Vector3(Mathf.Sin(az * Mathf.Deg2Rad), 0f, 0f);
                    float down = style == Style.Mane ? 0.9f : 0.35f + (el < 30f ? 0.25f : 0f);
                    Vector3 d = u * (style == Style.Ragged ? 0.8f : 0.35f) + back * 0.55f + G * down + side * 0.25f;
                    Vector3 bend = G * (style == Style.Mane ? 2.4f : 1.6f) + back * 0.2f;
                    Lock(solid, p, d, rowsL[row] * k * R(0.85f, 1.15f), rowsW[row] * k * R(0.9f, 1.1f), bend, R(-0.5f, 0.5f), 0.45f, style == Style.Mane ? 8 : 6);
                }
            }

            var root = new GameObject("Hair").transform;
            root.SetParent(head, false);
            root.gameObject.layer = layer;
            Matrix4x4 canonToHead = root.worldToLocalMatrix * body.CanonToWorld;
            var hairMat = MaterialFactory.AnimeCharacter(color, MaterialFactory.CharacterSurface.Hair, outline);
            var mesh = ToMesh("HairCap", solid, canonToHead);
            var mf = root.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = root.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = hairMat;
            rig.RegisterExternal(mr, CharacterRig.ExternalPart.FirstPersonHidden, color);

            // ---- swinging parts
            var headCollider = new SpringBoneChain.Collider { Bone = head, Offset = head.InverseTransformPoint(body.CanonToWorld.MultiplyPoint3x4(c)), Radius = 0.1f };
            var backBone = sk[HumanBodyBones.UpperChest] ?? sk[HumanBodyBones.Chest];
            var backCollider = backBone != null
                ? new SpringBoneChain.Collider { Bone = backBone, Offset = backBone.InverseTransformPoint(body.CanonToWorld.MultiplyPoint3x4(body.Joint(HumanBodyBones.Neck) + new Vector3(0f, -0.12f * k, -0.03f * k))), Radius = 0.13f }
                : headCollider;
            if (style == Style.Swordsman)
            {
                // high ponytail tied with a cord
                var tie = OnCap(180f, 25f, -0.01f);
                Vector3 start = new Vector3(0f, 0.35f, -1f).normalized;
                BuildChain(root, rig, body, "Ponytail", tie, start, G * 6.5f, 0.32f * k, 4, hairMat, color, layer, rng, k, 4, 0.045f, headCollider, backCollider);
                // cord around the tie
                var cord = new Builder();
                Ring(cord, tie + start * 0.012f * k, start, 0.028f * k, 0.008f * k);
                var cordGo = new GameObject("HairCord");
                cordGo.transform.SetParent(head, false);
                cordGo.layer = layer;
                cordGo.AddComponent<MeshFilter>().sharedMesh = ToMesh("HairCord", cord, cordGo.transform.worldToLocalMatrix * body.CanonToWorld);
                var cr = cordGo.AddComponent<MeshRenderer>();
                cr.sharedMaterial = MaterialFactory.AnimeCharacter(tieColor, MaterialFactory.CharacterSurface.Cloth, outline * 0.6f);
                rig.RegisterExternal(cr, CharacterRig.ExternalPart.FirstPersonHidden | CharacterRig.ExternalPart.Accent, tieColor);
                // side locks framing the face
                for (int s = -1; s <= 1; s += 2)
                {
                    var p = OnCap(s * 78f, 8f);
                    var u = Dir(s * 78f, 8f);
                    BuildChain(root, rig, body, s < 0 ? "SideLockL" : "SideLockR", p, (G + u * 0.25f + Vector3.forward * 0.1f).normalized, Vector3.forward * 0.3f, 0.13f * k, 2, hairMat, color, layer, rng, k, 1, 0.02f, headCollider);
                }
            }
            else if (style == Style.Mane)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    var p = OnCap(180f + s * 35f, 5f);
                    BuildChain(root, rig, body, s < 0 ? "ManeL" : "ManeR", p, (Vector3.back * 0.6f + G + Vector3.right * s * 0.3f).normalized, G * 3f, 0.42f * k, 4, hairMat, color, layer, rng, k, 3, 0.06f, headCollider, backCollider);
                }
            }
        }

        /// <summary>Hair cap: the skull's convex envelope + a gap, cut at the hairline (forehead, sideburns, nape).</summary>
        private static void BuildCap(Builder b, Vector3 c, System.Func<Vector3, float> support, System.Func<float, float, Vector3> dir, float gap, Style style)
        {
            const int nu = 28, nv = 14;
            var grid = new int[nv + 1, nu];
            for (int iv = 0; iv <= nv; iv++)
            {
                float el = 90f - iv * (125f / nv);
                for (int iu = 0; iu < nu; iu++)
                {
                    float az = 360f * iu / nu;
                    float front = Mathf.Cos(az * Mathf.Deg2Rad);
                    float frontCut = style == Style.Mane ? 38f : 26f;
                    float cut = frontCut * Mathf.Pow(Mathf.Max(front, 0f), 0.7f) - 8f * (1f - Mathf.Abs(front)) - 38f * Mathf.Max(-front, 0f);
                    grid[iv, iu] = -1;
                    if (el < cut) continue;
                    var u = dir(az, el);
                    grid[iv, iu] = b.V.Count;
                    b.V.Add(c + u * (support(u) + gap));
                    b.Param.Add(0f);
                }
            }
            int start = b.T.Count;
            for (int iv = 0; iv < nv; iv++)
            {
                for (int iu = 0; iu < nu; iu++)
                {
                    int a = grid[iv, iu], b1 = grid[iv, (iu + 1) % nu], c1 = grid[iv + 1, iu], d = grid[iv + 1, (iu + 1) % nu];
                    if (a < 0 || b1 < 0) continue;
                    if (c1 >= 0) Tri(b, a, c1, b1);
                    if (c1 >= 0 && d >= 0) Tri(b, b1, c1, d);
                    else if (c1 < 0 && d >= 0) Tri(b, a, d, b1);
                }
            }
            // outward winding
            for (int t = start; t < b.T.Count; t += 3)
            {
                Vector3 p0 = b.V[b.T[t]], p1 = b.V[b.T[t + 1]], p2 = b.V[b.T[t + 2]];
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                if (Vector3.Dot(n, (p0 + p1 + p2) / 3f - c) < 0f)
                {
                    b.T[t + 1] = b.T[t + 1] ^ b.T[t + 2];
                    b.T[t + 2] = b.T[t + 1] ^ b.T[t + 2];
                    b.T[t + 1] = b.T[t + 1] ^ b.T[t + 2];
                }
            }
        }

        private static void Tri(Builder b, int a, int c, int d)
        {
            b.T.Add(a);
            b.T.Add(c);
            b.T.Add(d);
        }

        /// <summary>A tapered, curved lock with a flattened diamond cross-section. Param = 0 at the root … 1 at the tip.</summary>
        private static void Lock(Builder b, Vector3 root, Vector3 dir, float length, float width, Vector3 bend, float twist, float thick, int seg)
        {
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.down;
            dir.Normalize();
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(Vector3.Dot(dir, G)) < 0.95f ? G : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, dir).normalized;
            float ca = Mathf.Cos(twist), sa = Mathf.Sin(twist);
            Vector3 s2 = side * ca + up * sa, u2 = -side * sa + up * ca;
            side = s2;
            up = u2;
            int first = b.V.Count, firstTri = b.T.Count;
            Vector3 p = root, d = dir;
            float step = length / seg;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float w = width * Mathf.Pow(1f - t, 0.85f);
                b.V.Add(p + side * w);
                b.V.Add(p + up * (w * thick));
                b.V.Add(p - side * w);
                b.V.Add(p - up * (w * thick));
                for (int q = 0; q < 4; q++) b.Param.Add(t);
                d = (d + bend * step).normalized;
                p += d * step;
            }
            for (int i = 0; i < seg; i++)
            {
                for (int q = 0; q < 4; q++)
                {
                    int a = first + i * 4 + q, b1 = first + i * 4 + (q + 1) % 4, c1 = a + 4, d1 = b1 + 4;
                    Tri(b, a, c1, b1);
                    Tri(b, b1, c1, d1);
                }
            }
            // outward winding (checked on the first quad against the lock axis)
            Vector3 p0 = b.V[b.T[firstTri]], p1 = b.V[b.T[firstTri + 1]], p2 = b.V[b.T[firstTri + 2]];
            Vector3 axis = (b.V[first] + b.V[first + 2]) * 0.5f;
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), (p0 + p1 + p2) / 3f - axis) < 0f)
            {
                for (int t = firstTri; t < b.T.Count; t += 3)
                {
                    int tmp = b.T[t + 1];
                    b.T[t + 1] = b.T[t + 2];
                    b.T[t + 2] = tmp;
                }
            }
        }

        /// <summary>A torus-like cord ring around <paramref name="axis"/>.</summary>
        private static void Ring(Builder b, Vector3 centre, Vector3 axis, float radius, float thickness)
        {
            axis.Normalize();
            Vector3 a = Vector3.Cross(axis, Vector3.up);
            if (a.sqrMagnitude < 1e-6f) a = Vector3.Cross(axis, Vector3.right);
            a.Normalize();
            Vector3 bb = Vector3.Cross(axis, a);
            const int n = 12, m = 5;
            int first = b.V.Count;
            for (int i = 0; i < n; i++)
            {
                float t = i * Mathf.PI * 2f / n;
                Vector3 ringDir = a * Mathf.Cos(t) + bb * Mathf.Sin(t);
                for (int j = 0; j < m; j++)
                {
                    float s = j * Mathf.PI * 2f / m;
                    b.V.Add(centre + ringDir * (radius + Mathf.Cos(s) * thickness) + axis * (Mathf.Sin(s) * thickness));
                    b.Param.Add(0f);
                }
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    int p00 = first + i * m + j, p01 = first + i * m + (j + 1) % m;
                    int p10 = first + (i + 1) % n * m + j, p11 = first + (i + 1) % n * m + (j + 1) % m;
                    Tri(b, p00, p01, p10);
                    Tri(b, p01, p11, p10);
                }
            }
        }

        private static Mesh ToMesh(string name, Builder b, Matrix4x4 m)
        {
            var verts = new Vector3[b.V.Count];
            for (int i = 0; i < verts.Length; i++) verts[i] = m.MultiplyPoint3x4(b.V[i]);
            var mesh = new Mesh { name = name, vertices = verts };
            mesh.SetTriangles(b.T, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A bundle of locks skinned to a spring chain hanging from the head.</summary>
        private static void BuildChain(Transform hairRoot, CharacterRig rig, AnimeBodyMesh body, string name, Vector3 start, Vector3 dir, Vector3 bend,
            float length, int joints, Material mat, Color color, int layer, System.Random rng, float k, int lockCount, float width, params SpringBoneChain.Collider[] colliders)
        {
            // rest centre line
            var line = new List<Vector3>();
            Vector3 p = start, d = dir.normalized;
            float step = length / joints;
            line.Add(p);
            for (int i = 0; i < joints; i++)
            {
                d = (d + bend * step).normalized;
                p += d * step;
                line.Add(p);
            }
            var chainRoot = new GameObject(name).transform;
            chainRoot.SetParent(hairRoot, false);
            chainRoot.gameObject.layer = layer;
            var bones = new Transform[joints + 1];
            Transform parent = hairRoot;
            for (int i = 0; i <= joints; i++)
            {
                var j = new GameObject(name + "_J" + i).transform;
                j.SetParent(parent, true);
                j.position = body.CanonToWorld.MultiplyPoint3x4(line[i]);
                j.rotation = hairRoot.rotation;
                bones[i] = j;
                parent = j;
            }

            var b = new Builder();
            for (int l = 0; l < lockCount; l++)
            {
                Vector3 off = l == 0 ? Vector3.zero : new Vector3((float)(rng.NextDouble() - 0.5) * 0.04f * k, (float)(rng.NextDouble() - 0.5) * 0.02f * k, 0f);
                float len = length * (l == 0 ? 1f : 0.8f + (float)rng.NextDouble() * 0.15f);
                Lock(b, start + off, dir, len, width * k * (l == 0 ? 1f : 0.8f), bend, (float)(rng.NextDouble() - 0.5), 0.45f, joints * 2);
            }

            var go = new GameObject(name + "Mesh");
            go.transform.SetParent(hairRoot, false);
            go.layer = layer;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            var toLocal = go.transform.worldToLocalMatrix * body.CanonToWorld;
            var verts = new Vector3[b.V.Count];
            var weights = new BoneWeight[b.V.Count];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = toLocal.MultiplyPoint3x4(b.V[i]);
                float f = Mathf.Clamp(b.Param[i] * joints, 0f, joints - 0.001f);
                int j0 = Mathf.FloorToInt(f);
                float w1 = f - j0;
                weights[i] = new BoneWeight { boneIndex0 = j0, weight0 = 1f - w1, boneIndex1 = Mathf.Min(j0 + 1, joints), weight1 = w1 };
            }
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
            var mesh = new Mesh { name = name, vertices = verts, boneWeights = weights, bindposes = bindposes };
            mesh.SetTriangles(b.T, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterial = mat;
            var bounds = mesh.bounds;
            bounds.Expand(length * 2f / Mathf.Max(1e-4f, go.transform.lossyScale.x));
            smr.localBounds = bounds;
            rig.RegisterExternal(smr, CharacterRig.ExternalPart.FirstPersonHidden, color);

            var spring = chainRoot.gameObject.AddComponent<SpringBoneChain>();
            spring.stiffness = joints > 2 ? 0.1f : 0.18f;
            spring.damping = 0.15f;
            spring.gravity = 3f;
            spring.Initialize(bones, k, colliders);
        }
    }
}
