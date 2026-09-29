using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>Body area of a vertex, from its dominant bone.</summary>
    public enum BodyRegion : byte
    {
        Other = 0,
        Head = 1,
        Neck = 2,
        Torso = 3,
        Hips = 4,
        UpperArm = 5,
        ForearmHi = 6,
        ForearmLo = 7,
        Hand = 8,
        Thigh = 9,
        CalfHi = 10,
        CalfLo = 11,
        Foot = 12
    }

    /// <summary>
    /// The body mesh of an imported base character analysed in a canonical frame (feet at the origin, facing +Z,
    /// metres): per-vertex region and side from the bone weights, welded vertex groups, joint positions. Builds
    /// skinned "shells" (clothes, masks, wraps) that reuse the body's bone weights so they deform with it.
    /// </summary>
    public sealed class AnimeBodyMesh
    {
        public delegate Vector3 Deform(int vertex, Vector3 position, Vector3 normal);

        public SkinnedMeshRenderer Renderer { get; private set; }
        public Mesh Source { get; private set; }
        public HumanoidSkeleton Skeleton { get; private set; }
        public Vector3[] V { get; private set; }
        public Vector3[] N { get; private set; }
        public Vector2[] UV { get; private set; }
        public BoneWeight[] W { get; private set; }
        public int[] T { get; private set; }
        public BodyRegion[] Region { get; private set; }
        /// <summary>-1 left, +1 right, 0 centre.</summary>
        public sbyte[] Side { get; private set; }
        /// <summary>Vertices at the same position share a group (UV seams): smoothing moves them together.</summary>
        public int[] Group { get; private set; }
        public int GroupCount { get; private set; }
        public Matrix4x4 MeshToCanon { get; private set; }
        public Matrix4x4 CanonToMesh { get; private set; }
        public Matrix4x4 CanonToWorld { get; private set; }
        /// <summary>Height / 1.8 m: every distance below is authored for a 1.8 m character.</summary>
        public float K { get; private set; }

        private readonly Dictionary<HumanBodyBones, Vector3> _joints = new Dictionary<HumanBodyBones, Vector3>();
        private readonly Dictionary<HumanBodyBones, int> _boneIndex = new Dictionary<HumanBodyBones, int>();

        /// <summary>Canonical position of a humanoid joint (zero if the bone is missing).</summary>
        public Vector3 Joint(HumanBodyBones b) => _joints.TryGetValue(b, out var p) ? p : Vector3.zero;
        public bool HasJoint(HumanBodyBones b) => _joints.ContainsKey(b);
        /// <summary>Index of a humanoid bone in the renderer's bone array (-1 if absent).</summary>
        public int BoneIndex(HumanBodyBones b) => _boneIndex.TryGetValue(b, out var i) ? i : -1;

        /// <summary>
        /// Analyses <paramref name="body"/> while the model is still in its bind pose. <paramref name="canonToWorld"/>
        /// places the canonical frame (feet, facing) in the world.
        /// </summary>
        public static AnimeBodyMesh From(HumanoidSkeleton sk, SkinnedMeshRenderer body, Matrix4x4 canonToWorld)
        {
            var mesh = body.sharedMesh;
            if (mesh == null || !mesh.isReadable) return null;
            var b = new AnimeBodyMesh { Renderer = body, Source = mesh, Skeleton = sk, K = sk.Height / 1.8f };
            b.CanonToWorld = canonToWorld;
            b.MeshToCanon = canonToWorld.inverse * body.transform.localToWorldMatrix;
            b.CanonToMesh = b.MeshToCanon.inverse;

            var verts = mesh.vertices;
            var normals = mesh.normals;
            b.UV = mesh.uv;
            if (b.UV == null || b.UV.Length != verts.Length) b.UV = new Vector2[verts.Length];
            b.W = mesh.boneWeights;
            b.T = mesh.triangles;
            if (b.W == null || b.W.Length != verts.Length) return null;
            b.V = new Vector3[verts.Length];
            b.N = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                b.V[i] = b.MeshToCanon.MultiplyPoint3x4(verts[i]);
                b.N[i] = normals != null && normals.Length == verts.Length ? b.MeshToCanon.MultiplyVector(normals[i]).normalized : Vector3.up;
            }

            // joints in the canonical frame
            var worldToCanon = canonToWorld.inverse;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var t = sk[(HumanBodyBones)i];
                if (t != null) b._joints[(HumanBodyBones)i] = worldToCanon.MultiplyPoint3x4(t.position);
            }

            // renderer bone index → humanoid bone (unmapped bones inherit their nearest mapped ancestor)
            var map = new Dictionary<Transform, HumanBodyBones>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var t = sk[(HumanBodyBones)i];
                if (t != null && !map.ContainsKey(t)) map[t] = (HumanBodyBones)i;
            }
            var bones = body.bones;
            var boneHuman = new HumanBodyBones[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                boneHuman[i] = HumanBodyBones.LastBone;
                for (var t = bones[i]; t != null && t != sk.ModelRoot; t = t.parent)
                {
                    if (map.TryGetValue(t, out var hb))
                    {
                        boneHuman[i] = hb;
                        if (t == bones[i] && !b._boneIndex.ContainsKey(hb)) b._boneIndex[hb] = i;
                        break;
                    }
                }
            }

            b.Region = new BodyRegion[verts.Length];
            b.Side = new sbyte[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var w = b.W[i];
                int dominant = w.weight0 >= w.weight1 && w.weight0 >= w.weight2 && w.weight0 >= w.weight3 ? w.boneIndex0
                    : w.weight1 >= w.weight2 && w.weight1 >= w.weight3 ? w.boneIndex1
                    : w.weight2 >= w.weight3 ? w.boneIndex2 : w.boneIndex3;
                var hb = dominant >= 0 && dominant < boneHuman.Length ? boneHuman[dominant] : HumanBodyBones.LastBone;
                b.Region[i] = b.Classify(hb, b.V[i]);
                string n = hb.ToString();
                b.Side[i] = (sbyte)(n.StartsWith("Left") ? -1 : n.StartsWith("Right") ? 1 : 0);
            }

            // weld groups (same position within 0.1 mm)
            var groups = new Dictionary<Vector3Int, int>();
            b.Group = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(b.V[i].x * 10000f), Mathf.RoundToInt(b.V[i].y * 10000f), Mathf.RoundToInt(b.V[i].z * 10000f));
                if (!groups.TryGetValue(key, out int g))
                {
                    g = groups.Count;
                    groups[key] = g;
                }
                b.Group[i] = g;
            }
            b.GroupCount = groups.Count;
            return b;
        }

        private BodyRegion Classify(HumanBodyBones hb, Vector3 p)
        {
            switch (hb)
            {
                case HumanBodyBones.Head:
                case HumanBodyBones.Jaw:
                case HumanBodyBones.LeftEye:
                case HumanBodyBones.RightEye:
                    return BodyRegion.Head;
                case HumanBodyBones.Neck:
                    return BodyRegion.Neck;
                case HumanBodyBones.Spine:
                case HumanBodyBones.Chest:
                case HumanBodyBones.UpperChest:
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.RightShoulder:
                    return BodyRegion.Torso;
                case HumanBodyBones.Hips:
                    return p.y > Joint(HumanBodyBones.Hips).y + 0.035f * K ? BodyRegion.Torso : BodyRegion.Hips;
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.RightUpperArm:
                    return BodyRegion.UpperArm;
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightLowerArm:
                {
                    bool left = hb == HumanBodyBones.LeftLowerArm;
                    Vector3 e = Joint(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    Vector3 h = Joint(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    Vector3 axis = h - e;
                    float t = axis.sqrMagnitude > 1e-8f ? Vector3.Dot(p - e, axis) / axis.sqrMagnitude : 0f;
                    return t < 0.4f ? BodyRegion.ForearmHi : BodyRegion.ForearmLo;
                }
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg:
                    return BodyRegion.Thigh;
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightLowerLeg:
                {
                    bool left = hb == HumanBodyBones.LeftLowerLeg;
                    float knee = Joint(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).y;
                    float ankle = Joint(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot).y;
                    return p.y > Mathf.Lerp(ankle, knee, 0.5f) ? BodyRegion.CalfHi : BodyRegion.CalfLo;
                }
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.RightFoot:
                case HumanBodyBones.LeftToes:
                case HumanBodyBones.RightToes:
                    return BodyRegion.Foot;
                case HumanBodyBones.LastBone:
                    return BodyRegion.Other;
                default:
                    return hb >= HumanBodyBones.LeftThumbProximal && hb <= HumanBodyBones.RightLittleDistal ? BodyRegion.Hand
                        : hb == HumanBodyBones.LeftHand || hb == HumanBodyBones.RightHand ? BodyRegion.Hand : BodyRegion.Other;
            }
        }

        /// <summary>Region mask helper: true when the region is in <paramref name="set"/>.</summary>
        public static bool In(BodyRegion r, params BodyRegion[] set) => Array.IndexOf(set, r) >= 0;

        // ------------------------------------------------------------------ shells

        /// <summary>
        /// A skinned shell over the triangles whose three vertices pass <paramref name="include"/>: offset
        /// <paramref name="push"/> m along the normals (× K), optionally deformed (flare, hang), smoothed so the body's
        /// muscle detail does not show through cloth, and kept at least <paramref name="minOffset"/> above the skin.
        /// </summary>
        public Mesh Shell(string name, Func<int, bool> include, float push, Deform deform = null, int smooth = 3, float minOffset = -1f,
            Func<int, int, int, bool> triangleFilter = null, Func<int, Vector3, Vector2> uvOf = null)
        {
            var tris = new List<int>();
            for (int t = 0; t < T.Length; t += 3)
            {
                int a = T[t], b = T[t + 1], c = T[t + 2];
                if (!include(a) || !include(b) || !include(c)) continue;
                if (triangleFilter != null && !triangleFilter(a, b, c)) continue;
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
            }
            if (tris.Count == 0) return null;
            var remap = new Dictionary<int, int>();
            var src = new List<int>();
            for (int i = 0; i < tris.Count; i++)
            {
                if (!remap.TryGetValue(tris[i], out int n))
                {
                    n = src.Count;
                    remap[tris[i]] = n;
                    src.Add(tris[i]);
                }
                tris[i] = n;
            }
            int count = src.Count;
            float offset = push * K;
            float floor = (minOffset < 0f ? push * 0.75f : minOffset) * K;
            var P = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int v = src[i];
                P[i] = V[v] + N[v] * offset;
                if (deform != null) P[i] = deform(v, P[i], N[v]);
            }
            if (smooth > 0) SmoothWelded(P, src, tris, smooth);
            for (int i = 0; i < count; i++)
            {
                int v = src[i];
                float d = Vector3.Dot(P[i] - V[v], N[v]);
                if (d < floor) P[i] += N[v] * (floor - d);
            }
            return Build(name, P, src, tris, uvOf);
        }

        /// <summary>Laplacian smoothing on welded vertex groups (boundary groups move less), so seams never open.</summary>
        private void SmoothWelded(Vector3[] P, List<int> src, List<int> tris, int iterations)
        {
            int count = src.Count;
            var local = new Dictionary<int, int>();
            var groupOf = new int[count];
            for (int i = 0; i < count; i++)
            {
                int g = Group[src[i]];
                if (!local.TryGetValue(g, out int lg))
                {
                    lg = local.Count;
                    local[g] = lg;
                }
                groupOf[i] = lg;
            }
            int groups = local.Count;
            var neighbours = new List<int>[groups];
            for (int i = 0; i < groups; i++) neighbours[i] = new List<int>(6);
            var edgeUse = new Dictionary<long, int>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = groupOf[tris[t + e]], b = groupOf[tris[t + (e + 1) % 3]];
                    if (a == b) continue;
                    if (!neighbours[a].Contains(b)) neighbours[a].Add(b);
                    if (!neighbours[b].Contains(a)) neighbours[b].Add(a);
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edgeUse.TryGetValue(key, out int used);
                    edgeUse[key] = used + 1;
                }
            }
            var boundary = new bool[groups];
            foreach (var kv in edgeUse)
            {
                if (kv.Value != 1) continue;
                boundary[(int)(kv.Key >> 32)] = true;
                boundary[(int)(kv.Key & 0xffffffff)] = true;
            }
            var gp = new Vector3[groups];
            var gc = new int[groups];
            for (int i = 0; i < count; i++)
            {
                gp[groupOf[i]] += P[i];
                gc[groupOf[i]]++;
            }
            for (int g = 0; g < groups; g++) gp[g] /= Mathf.Max(1, gc[g]);
            var next = new Vector3[groups];
            for (int it = 0; it < iterations; it++)
            {
                for (int g = 0; g < groups; g++)
                {
                    var nb = neighbours[g];
                    if (nb.Count == 0)
                    {
                        next[g] = gp[g];
                        continue;
                    }
                    Vector3 avg = Vector3.zero;
                    foreach (int n in nb) avg += gp[n];
                    avg /= nb.Count;
                    next[g] = Vector3.Lerp(gp[g], avg, boundary[g] ? 0.12f : 0.5f);
                }
                var swap = gp;
                gp = next;
                next = swap;
            }
            for (int i = 0; i < count; i++) P[i] = gp[groupOf[i]];
        }

        /// <summary>Skinned mesh from canonical positions of copied body vertices (weights, UVs, normals copied).</summary>
        private Mesh Build(string name, Vector3[] P, List<int> src, List<int> tris, Func<int, Vector3, Vector2> uvOf = null)
        {
            int count = src.Count;
            var verts = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                int v = src[i];
                verts[i] = CanonToMesh.MultiplyPoint3x4(P[i]);
                normals[i] = CanonToMesh.MultiplyVector(N[v]).normalized;
                uv[i] = uvOf != null ? uvOf(v, V[v]) : UV[v];
                weights[i] = W[v];
            }
            var mesh = new Mesh { name = name };
            if (count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.boneWeights = weights;
            mesh.bindposes = Source.bindposes;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A skinned mesh built from scratch in canonical space (skirts, hair pieces on the body bones).
        /// <paramref name="weights"/> use the renderer's bone indices (see <see cref="BoneIndex"/>).
        /// </summary>
        public Mesh Custom(string name, List<Vector3> canonVerts, List<int> tris, List<BoneWeight> weights, List<Vector2> uv = null)
        {
            var verts = new Vector3[canonVerts.Count];
            for (int i = 0; i < verts.Length; i++) verts[i] = CanonToMesh.MultiplyPoint3x4(canonVerts[i]);
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            if (uv != null && uv.Count == verts.Length) mesh.SetUVs(0, uv);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = Source.bindposes;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Copy of the body mesh keeping only the triangles that pass the filter; vertices can be moved (canonical space).</summary>
        public Mesh Subset(string name, Func<int, int, int, bool> keepTriangle, Func<int, Vector3, Vector3> move = null)
        {
            var mesh = UnityEngine.Object.Instantiate(Source);
            mesh.name = name;
            var tris = new List<int>(T.Length);
            for (int t = 0; t < T.Length; t += 3)
            {
                if (!keepTriangle(T[t], T[t + 1], T[t + 2])) continue;
                tris.Add(T[t]);
                tris.Add(T[t + 1]);
                tris.Add(T[t + 2]);
            }
            if (move != null)
            {
                var verts = new Vector3[V.Length];
                for (int i = 0; i < V.Length; i++) verts[i] = CanonToMesh.MultiplyPoint3x4(move(i, V[i]));
                mesh.vertices = verts;
            }
            if (mesh.subMeshCount > 1) mesh.subMeshCount = 1;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A new skinned renderer next to the body, sharing its bones.</summary>
        public SkinnedMeshRenderer Attach(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(Renderer.transform.parent, false);
            t.localPosition = Renderer.transform.localPosition;
            t.localRotation = Renderer.transform.localRotation;
            t.localScale = Renderer.transform.localScale;
            go.layer = Renderer.gameObject.layer;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.bones = Renderer.bones;
            smr.rootBone = Renderer.rootBone;
            smr.sharedMesh = mesh;
            smr.sharedMaterial = material;
            var bounds = Renderer.localBounds;
            bounds.Expand(0.6f / Mathf.Max(1e-4f, Renderer.transform.lossyScale.x) * K);
            smr.localBounds = bounds;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return smr;
        }

        /// <summary>Support distance of the vertices passing <paramref name="filter"/> in a direction (convex envelope).</summary>
        public float Support(Vector3 centre, Vector3 dir, Func<int, bool> filter)
        {
            float best = 0f;
            for (int i = 0; i < V.Length; i++)
            {
                if (!filter(i)) continue;
                float d = Vector3.Dot(V[i] - centre, dir);
                if (d > best) best = d;
            }
            return best;
        }
    }
}
