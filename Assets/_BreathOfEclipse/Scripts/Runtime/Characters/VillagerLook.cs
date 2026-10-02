using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>What a villager wears (original designs; period-inspired, no franchise uniforms).</summary>
    public enum VillagerOutfit
    {
        /// <summary>Short work jacket to mid-thigh, rope belt, fitted trousers, sandals (farmers, woodcutters, fishers, smith).</summary>
        WorkJacket = 0,
        /// <summary>Long kimono with hanging sleeves and a wide obi.</summary>
        Kimono = 1,
        /// <summary>Kimono with a work apron (shopkeeper, innkeeper, mothers).</summary>
        Apron = 2,
        /// <summary>White top and red pleated hakama (shrine keeper).</summary>
        ShrineRobe = 3,
        /// <summary>Dark jacket with long sleeves, pleated hakama, headband (village guards).</summary>
        Guard = 4,
        /// <summary>Work jacket with a short cloak, leg wraps, straw hat (travellers, wanderers).</summary>
        Traveler = 5,
        /// <summary>Short kimono to the knees, bare legs (children).</summary>
        Child = 6,
        /// <summary>Close-fitting dark jacket, hakama, leg wraps, short cloak (demon hunters of the frontier).</summary>
        Hunter = 7
    }

    /// <summary>Everything that varies between villagers.</summary>
    public struct VillagerLookParams
    {
        public VillagerOutfit Outfit;
        public Color Skin, Top, Bottom, Obi, Accent, Feet, Hair, Iris;
        public AnimeHair.Style HairStyle;
        public bool Headband, StrawHat;
        public float HeadScale;
        public int Seed;

        /// <summary>A deterministic look from an NPC seed and its kind.</summary>
        public static VillagerLookParams For(int seed, bool female, bool child, bool elder, VillagerOutfit outfit)
        {
            var rng = new System.Random(seed * 7919 + 17);
            T Pick<T>(T[] a) => a[rng.Next(a.Length)];
            Color[] skins = { new Color(0.98f, 0.84f, 0.74f), new Color(0.95f, 0.8f, 0.68f), new Color(0.9f, 0.74f, 0.6f), new Color(0.99f, 0.87f, 0.78f), new Color(0.84f, 0.66f, 0.52f) };
            Color[] menTops = { new Color(0.2f, 0.27f, 0.42f), new Color(0.36f, 0.3f, 0.24f), new Color(0.3f, 0.36f, 0.28f), new Color(0.42f, 0.4f, 0.36f), new Color(0.24f, 0.22f, 0.3f), new Color(0.46f, 0.36f, 0.24f) };
            Color[] womenTops = { new Color(0.55f, 0.3f, 0.36f), new Color(0.24f, 0.3f, 0.5f), new Color(0.26f, 0.44f, 0.44f), new Color(0.44f, 0.3f, 0.46f), new Color(0.62f, 0.5f, 0.32f), new Color(0.36f, 0.42f, 0.3f) };
            Color[] kidTops = { new Color(0.85f, 0.45f, 0.35f), new Color(0.35f, 0.6f, 0.8f), new Color(0.9f, 0.72f, 0.3f), new Color(0.5f, 0.75f, 0.45f), new Color(0.8f, 0.5f, 0.7f) };
            Color[] bottoms = { new Color(0.16f, 0.17f, 0.24f), new Color(0.26f, 0.23f, 0.2f), new Color(0.2f, 0.24f, 0.32f), new Color(0.3f, 0.3f, 0.28f) };
            Color[] obis = { new Color(0.75f, 0.3f, 0.25f), new Color(0.85f, 0.7f, 0.35f), new Color(0.3f, 0.45f, 0.65f), new Color(0.7f, 0.45f, 0.6f), new Color(0.35f, 0.55f, 0.4f) };
            Color[] hairs = { new Color(0.06f, 0.06f, 0.09f), new Color(0.13f, 0.09f, 0.07f), new Color(0.07f, 0.08f, 0.14f), new Color(0.2f, 0.13f, 0.09f) };
            Color[] irises = { new Color(0.22f, 0.13f, 0.08f), new Color(0.1f, 0.08f, 0.07f), new Color(0.3f, 0.22f, 0.12f), new Color(0.14f, 0.16f, 0.24f) };

            var p = new VillagerLookParams
            {
                Outfit = outfit,
                Seed = seed,
                Skin = Pick(skins),
                Top = child ? Pick(kidTops) : female ? Pick(womenTops) : Pick(menTops),
                Bottom = Pick(bottoms),
                Obi = Pick(obis),
                Feet = new Color(0.62f, 0.55f, 0.4f),
                Hair = elder ? Color.Lerp(new Color(0.78f, 0.78f, 0.8f), new Color(0.55f, 0.55f, 0.58f), (float)rng.NextDouble()) : Pick(hairs),
                Iris = Pick(irises),
                HeadScale = child ? 1.16f : 1f
            };
            p.Accent = Color.Lerp(p.Obi, Color.white, 0.25f);
            if (elder)
            {
                p.Top = Color.Lerp(p.Top, new Color(0.4f, 0.4f, 0.42f), 0.5f);
                p.HairStyle = female ? AnimeHair.Style.Bun : AnimeHair.Style.Elder;
            }
            else if (child) p.HairStyle = female ? AnimeHair.Style.Long : AnimeHair.Style.Short;
            else if (female) p.HairStyle = rng.NextDouble() < 0.65 ? AnimeHair.Style.Bun : AnimeHair.Style.Long;
            else p.HairStyle = rng.NextDouble() < 0.5 ? AnimeHair.Style.Topknot : AnimeHair.Style.Short;

            switch (outfit)
            {
                case VillagerOutfit.ShrineRobe:
                    p.Top = new Color(0.94f, 0.93f, 0.9f);
                    p.Bottom = new Color(0.72f, 0.12f, 0.14f);
                    p.Obi = new Color(0.72f, 0.12f, 0.14f);
                    p.Feet = new Color(0.95f, 0.95f, 0.93f);
                    break;
                case VillagerOutfit.Guard:
                    p.Top = new Color(0.12f, 0.16f, 0.3f);
                    p.Bottom = new Color(0.18f, 0.18f, 0.22f);
                    p.Obi = new Color(0.55f, 0.15f, 0.15f);
                    p.Headband = true;
                    p.Accent = new Color(0.9f, 0.88f, 0.82f);
                    break;
                case VillagerOutfit.Traveler:
                    p.StrawHat = true;
                    p.Top = Color.Lerp(p.Top, new Color(0.45f, 0.42f, 0.36f), 0.5f);
                    break;
                case VillagerOutfit.Hunter:
                    p.Top = new Color(0.1f, 0.11f, 0.15f);
                    p.Bottom = new Color(0.14f, 0.15f, 0.2f);
                    p.Feet = new Color(0.1f, 0.09f, 0.11f);
                    break;
                case VillagerOutfit.WorkJacket:
                    p.Headband = !female && rng.NextDouble() < 0.4;
                    p.Feet = new Color(0.55f, 0.48f, 0.34f);
                    break;
            }
            return p;
        }
    }

    /// <summary>
    /// Light anime villager visual on the Quaternius base body (no combat rig): toon skin, eyes, mouth, procedural
    /// hair, clothes generated as skinned shells / skirts of the body and merged with it into ONE skinned renderer
    /// (one skinning job, one sub-mesh per material). Meshes are cached per body and outfit (they do not depend on
    /// the character's height).
    /// </summary>
    public static class VillagerLook
    {
        private enum Slot { Skin = 0, Top = 1, Bottom = 2, Obi = 3, Feet = 4, Apron = 5, Wrap = 6, Cloak = 7 }

        private sealed class Cached
        {
            public Mesh Combined;
            public Slot[] Slots;
        }

        private static readonly Dictionary<string, Cached> Cache = new Dictionary<string, Cached>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        public sealed class Result
        {
            public AnimeFace Face;
            public SkinnedMeshRenderer Body;
            public Transform Head;
            public readonly List<Renderer> Renderers = new List<Renderer>();
        }

        /// <summary>Dresses the model (bind pose). Returns null if the body mesh cannot be analysed.</summary>
        public static Result Apply(HumanoidSkeleton sk, VillagerLookParams p, int layer, float outline = 1.15f)
        {
            SkinnedMeshRenderer body = null, eyes = null, brows = null;
            int most = -1;
            foreach (var r in sk.Renderers)
            {
                if (!(r is SkinnedMeshRenderer smr) || smr.sharedMesh == null) continue;
                string n = smr.name.ToLowerInvariant();
                if (n.Contains("brow")) brows = smr;
                else if (n.Contains("eye")) eyes = smr;
                if (smr.sharedMesh.vertexCount > most && !n.Contains("brow") && !(n.Contains("eye") && !n.Contains("body")))
                {
                    most = smr.sharedMesh.vertexCount;
                    body = smr;
                }
            }
            if (body == null) return null;
            var visualRoot = sk.ModelRoot.parent != null ? sk.ModelRoot.parent : sk.ModelRoot;
            var canon = Matrix4x4.TRS(visualRoot.position, sk.ModelRoot.rotation * Quaternion.Inverse(sk.FacingFix), Vector3.one);
            var bm = AnimeBodyMesh.From(sk, body, canon);
            if (bm == null) return null;

            string key = $"{body.sharedMesh.GetInstanceID()}_{p.Outfit}";
            if (!Cache.TryGetValue(key, out var cached) || cached == null || cached.Combined == null)
            {
                cached = Build(bm, p.Outfit);
                Cache[key] = cached;
            }

            var result = new Result { Body = body, Head = sk[HumanBodyBones.Head] };
            var mats = new Material[cached.Slots.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = SlotMaterial(cached.Slots[i], p, outline);
            body.sharedMesh = cached.Combined;
            body.sharedMaterials = mats;
            var bounds = body.localBounds;
            bounds.Expand(0.6f / Mathf.Max(1e-4f, body.transform.lossyScale.x) * bm.K);
            body.localBounds = bounds;
            result.Renderers.Add(body);

            var eyeMats = AnimeCharacterLook.EyeMaterials(false, Color.Lerp(p.Iris, Color.black, 0.4f), p.Iris);
            if (eyes != null)
            {
                eyes.sharedMaterial = eyeMats[0];
                eyes.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                result.Renderers.Add(eyes);
            }
            if (brows != null)
            {
                brows.sharedMaterial = MaterialFactory.AnimeCharacter(p.Hair * 0.8f, MaterialFactory.CharacterSurface.Hair, 0f);
                result.Renderers.Add(brows);
            }
            var mouth = AnimeCharacterLook.BuildMouth(bm, null, layer, new Color(0.3f, 0.09f, 0.1f));
            AnimeHair.Build(bm, null, p.HairStyle, p.Hair, p.Obi, layer, outline, p.Seed);
            if (p.Headband) Headband(bm, p.Outfit == VillagerOutfit.Guard ? p.Accent : new Color(0.92f, 0.9f, 0.85f), layer, outline);
            if (p.StrawHat) StrawHat(bm, layer, outline);

            var face = sk.ModelRoot.gameObject.AddComponent<AnimeFace>();
            face.Initialize(null, eyes, eyeMats, mouth, false);
            result.Face = face;

            // Children: a slightly larger head (anime proportions).
            if (p.HeadScale > 1.001f && result.Head != null) result.Head.localScale *= p.HeadScale;
            if (result.Head != null)
            {
                var headParts = new List<Renderer>();
                result.Head.GetComponentsInChildren(true, headParts);
                foreach (var r in headParts) if (!result.Renderers.Contains(r)) result.Renderers.Add(r);
            }
            return result;
        }

        private static Material SlotMaterial(Slot s, VillagerLookParams p, float outline)
        {
            switch (s)
            {
                case Slot.Skin: return MaterialFactory.AnimeCharacter(p.Skin, MaterialFactory.CharacterSurface.Skin, outline);
                case Slot.Top: return MaterialFactory.AnimeCharacter(p.Top, MaterialFactory.CharacterSurface.Cloth, outline);
                case Slot.Bottom: return MaterialFactory.AnimeCharacter(p.Bottom, MaterialFactory.CharacterSurface.Cloth, outline);
                case Slot.Obi: return MaterialFactory.AnimeCharacter(p.Obi, MaterialFactory.CharacterSurface.Cloth, outline);
                case Slot.Feet: return MaterialFactory.AnimeCharacter(p.Feet, MaterialFactory.CharacterSurface.Cloth, outline * 0.7f);
                case Slot.Apron: return MaterialFactory.AnimeCharacter(new Color(0.86f, 0.84f, 0.78f), MaterialFactory.CharacterSurface.Cloth, outline);
                case Slot.Wrap: return MaterialFactory.AnimeCharacter(new Color(0.82f, 0.8f, 0.74f), MaterialFactory.CharacterSurface.Cloth, outline * 0.6f);
                default: return MaterialFactory.AnimeCharacter(Color.Lerp(p.Top, new Color(0.35f, 0.32f, 0.28f), 0.6f), MaterialFactory.CharacterSurface.Cloth, outline);
            }
        }

        // ------------------------------------------------------------------ outfits

        private static Cached Build(AnimeBodyMesh bm, VillagerOutfit outfit)
        {
            float k = bm.K;
            var R = bm.Region;
            float hipsY = bm.Joint(HumanBodyBones.Hips).y;
            float neckY = bm.Joint(HumanBodyBones.Neck).y;
            float kneeY = (bm.Joint(HumanBodyBones.LeftLowerLeg).y + bm.Joint(HumanBodyBones.RightLowerLeg).y) * 0.5f;
            float ankleY = (bm.Joint(HumanBodyBones.LeftFoot).y + bm.Joint(HumanBodyBones.RightFoot).y) * 0.5f;
            var parts = new List<(Mesh mesh, Slot slot)>();
            void Add(Mesh m, Slot s)
            {
                if (m != null) parts.Add((m, s));
            }

            bool longSleeves = outfit == VillagerOutfit.Kimono || outfit == VillagerOutfit.Apron || outfit == VillagerOutfit.ShrineRobe || outfit == VillagerOutfit.Guard || outfit == VillagerOutfit.Hunter;
            bool skirt = outfit == VillagerOutfit.Kimono || outfit == VillagerOutfit.Apron;
            bool hakama = outfit == VillagerOutfit.ShrineRobe || outfit == VillagerOutfit.Guard || outfit == VillagerOutfit.Hunter;
            bool pants = outfit == VillagerOutfit.WorkJacket || outfit == VillagerOutfit.Traveler;
            bool child = outfit == VillagerOutfit.Child;

            bool Covered(int i)
            {
                var r = R[i];
                if (r == BodyRegion.Torso || r == BodyRegion.Hips || r == BodyRegion.UpperArm || r == BodyRegion.Foot) return true;
                if (longSleeves && r == BodyRegion.ForearmHi) return true;
                if (!child && (r == BodyRegion.Thigh || r == BodyRegion.CalfHi || r == BodyRegion.CalfLo)) return true;
                if (child && r == BodyRegion.Thigh) return true;
                return false;
            }
            var bodyMesh = bm.Subset("VillagerBody", (a, b, d) => true, (i, pos) => Covered(i) ? pos - bm.N[i] * (0.006f * k) : pos);
            Add(bodyMesh, Slot.Skin);

            // top: torso and sleeves (to the elbow for workers, hanging sleeves for kimono)
            Add(bm.Shell("Top", i =>
                R[i] == BodyRegion.Torso || R[i] == BodyRegion.UpperArm || (longSleeves && R[i] == BodyRegion.ForearmHi) ||
                (R[i] == BodyRegion.Neck && bm.V[i].y < neckY + 0.02f * k) ||
                (R[i] == BodyRegion.Hips && bm.V[i].y > hipsY - 0.03f * k), 0.008f,
                skirt || outfit == VillagerOutfit.ShrineRobe ? (AnimeBodyMesh.Deform)((i, pos, n) => HangingSleeve(bm, i, pos, n)) : null, 4), Slot.Top);

            // jacket skirt below the belt (workers, guards, travellers, children)
            if (!skirt)
            {
                float bottom = child ? kneeY + 0.04f * k : outfit == VillagerOutfit.Guard || outfit == VillagerOutfit.Hunter ? hipsY - 0.22f * k : hipsY - 0.27f * k;
                Add(Skirt(bm, "JacketSkirt", hipsY + 0.04f * k, bottom, 14f, 0.03f, 0.07f, false), Slot.Top);
            }

            if (skirt)
            {
                // long kimono skirt to the ankles over a dark under-layer
                Add(bm.Shell("Under", i => R[i] == BodyRegion.Thigh || R[i] == BodyRegion.CalfHi || R[i] == BodyRegion.CalfLo, 0.006f, null, 2), Slot.Bottom);
                Add(Skirt(bm, "KimonoSkirt", hipsY + 0.06f * k, ankleY + 0.03f * k, 6f, 0.04f, 0.075f, true), Slot.Top);
            }
            else if (hakama)
            {
                Add(bm.Shell("Hakama", i =>
                    R[i] == BodyRegion.Hips || R[i] == BodyRegion.Thigh || R[i] == BodyRegion.CalfHi || (outfit == VillagerOutfit.ShrineRobe && R[i] == BodyRegion.CalfLo) ||
                    (R[i] == BodyRegion.Torso && bm.V[i].y < hipsY + 0.06f * k), 0.012f, (i, pos, n) => LegFlare(bm, i, pos, 0.08f * k, 0.006f * k), 3), Slot.Bottom);
            }
            else if (pants)
            {
                Add(bm.Shell("Trousers", i => R[i] == BodyRegion.Thigh || R[i] == BodyRegion.CalfHi || R[i] == BodyRegion.CalfLo ||
                    (R[i] == BodyRegion.Hips && bm.V[i].y < hipsY - 0.02f * k), 0.007f, null, 3), Slot.Bottom);
            }
            else if (child)
            {
                Add(bm.Shell("Shorts", i => R[i] == BodyRegion.Thigh && bm.V[i].y > kneeY + 0.1f * k || (R[i] == BodyRegion.Hips && bm.V[i].y < hipsY - 0.02f * k), 0.007f, null, 2), Slot.Bottom);
            }

            // belt / obi
            float obiTop = skirt ? hipsY + 0.24f * k : hipsY + 0.11f * k;
            float obiLow = skirt ? hipsY + 0.06f * k : hipsY + 0.03f * k;
            Add(bm.Shell("Obi", i => (R[i] == BodyRegion.Torso || R[i] == BodyRegion.Hips) && bm.V[i].y > obiLow && bm.V[i].y < obiTop, skirt ? 0.03f : 0.022f, null, 2), Slot.Obi);

            // apron over the front
            if (outfit == VillagerOutfit.Apron) Add(Apron(bm, hipsY + 0.05f * k, kneeY - 0.05f * k), Slot.Apron);
            // short cloak for travellers and hunters
            if (outfit == VillagerOutfit.Traveler || outfit == VillagerOutfit.Hunter)
                Add(bm.Shell("Cloak", i => R[i] == BodyRegion.Torso && bm.V[i].y > hipsY + 0.2f * k || R[i] == BodyRegion.UpperArm && bm.V[i].y > bm.Joint(HumanBodyBones.LeftLowerArm).y + 0.06f * k, 0.026f, null, 4), Slot.Cloak);
            // leg wraps
            if (outfit == VillagerOutfit.Traveler || outfit == VillagerOutfit.Hunter || outfit == VillagerOutfit.Guard)
                Add(bm.Shell("LegWraps", i => R[i] == BodyRegion.CalfLo, 0.012f, null, 1), Slot.Wrap);
            // sandals / tabi
            Add(bm.Shell("Feet", i => R[i] == BodyRegion.Foot, 0.006f, null, 2), Slot.Feet);

            return Combine(bm, parts);
        }

        /// <summary>Merges body and clothes (same bones and bind poses) into one skinned mesh with a sub-mesh per part.</summary>
        private static Cached Combine(AnimeBodyMesh bm, List<(Mesh mesh, Slot slot)> parts)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var w = new List<BoneWeight>();
            var subs = new List<int[]>();
            var slots = new List<Slot>();
            foreach (var (mesh, slot) in parts)
            {
                int baseIndex = v.Count;
                var mv = mesh.vertices;
                var mn = mesh.normals;
                var muv = mesh.uv;
                var mw = mesh.boneWeights;
                if (mw == null || mw.Length != mv.Length) continue;
                v.AddRange(mv);
                if (mn != null && mn.Length == mv.Length) n.AddRange(mn);
                else for (int i = 0; i < mv.Length; i++) n.Add(Vector3.up);
                if (muv != null && muv.Length == mv.Length) uv.AddRange(muv);
                else for (int i = 0; i < mv.Length; i++) uv.Add(Vector2.zero);
                w.AddRange(mw);
                var t = mesh.GetTriangles(0);
                for (int i = 0; i < t.Length; i++) t[i] += baseIndex;
                subs.Add(t);
                slots.Add(slot);
                if (mesh != bm.Source) Object.Destroy(mesh);
            }
            var combined = new Mesh { name = "Villager_" + bm.Source.name };
            if (v.Count > 65000) combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.SetVertices(v);
            combined.SetNormals(n);
            combined.SetUVs(0, uv);
            combined.boneWeights = w.ToArray();
            combined.bindposes = bm.Source.bindposes;
            combined.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) combined.SetTriangles(subs[i], i);
            combined.RecalculateBounds();
            combined.UploadMeshData(false);
            return new Cached { Combined = combined, Slots = slots.ToArray() };
        }

        // ------------------------------------------------------------------ shapes

        private static Vector3 HangingSleeve(AnimeBodyMesh bm, int i, Vector3 p, Vector3 n)
        {
            var r = bm.Region[i];
            if (r != BodyRegion.UpperArm && r != BodyRegion.ForearmHi) return p;
            bool left = bm.Side[i] < 0;
            Vector3 sh = bm.Joint(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Vector3 el = bm.Joint(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Vector3 axis = el - sh;
            float len = axis.magnitude;
            if (len < 1e-4f) return p;
            float t = Mathf.Clamp(Vector3.Dot(bm.V[i] - sh, axis / len) / len, 0f, 1.6f);
            Vector3 centre = sh + axis / len * Vector3.Dot(bm.V[i] - sh, axis / len);
            float k = bm.K;
            if (bm.V[i].y < centre.y - 0.01f * k) p.y -= 0.1f * k * Mathf.Clamp01(t - 0.2f);
            return p + n * (0.015f * k * t);
        }

        private static Vector3 LegFlare(AnimeBodyMesh bm, int i, Vector3 p, float flare, float pleat)
        {
            bool left = bm.Side[i] < 0 || (bm.Side[i] == 0 && bm.V[i].x < 0f);
            Vector3 hip = bm.Joint(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            Vector3 ankle = bm.Joint(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            Vector3 axis = ankle - hip;
            float len = axis.magnitude;
            if (len < 1e-4f) return p;
            axis /= len;
            float along = Vector3.Dot(bm.V[i] - hip, axis);
            float t = Mathf.Clamp01(along / len);
            Vector3 centre = hip + axis * along;
            Vector3 r = bm.V[i] - centre;
            r.y = 0f;
            if (r.sqrMagnitude < 1e-8f) return p;
            float ang = Mathf.Atan2(r.z, r.x);
            float pl = Mathf.Max(0f, Mathf.Sin(ang * 7f)) * pleat;
            return p + r.normalized * (flare * Mathf.Pow(t, 1.5f) + pl * t);
        }

        /// <summary>
        /// A wrapped skirt from <paramref name="top"/> to <paramref name="bottom"/> (canonical heights), open
        /// <paramref name="openDeg"/> at the front. Upper rows follow the hips, lower rows the legs (left/right by side,
        /// front and back shared) so it moves with the stride. Double-sided.
        /// </summary>
        private static Mesh Skirt(AnimeBodyMesh bm, string name, float top, float bottom, float openDeg, float flareTop, float flareBottom, bool toAnkles)
        {
            float k = bm.K;
            int hips = bm.BoneIndex(HumanBodyBones.Hips);
            if (hips < 0 || bottom >= top) return null;
            int thighL = bm.BoneIndex(HumanBodyBones.LeftUpperLeg), thighR = bm.BoneIndex(HumanBodyBones.RightUpperLeg);
            int calfL = bm.BoneIndex(HumanBodyBones.LeftLowerLeg), calfR = bm.BoneIndex(HumanBodyBones.RightLowerLeg);
            float kneeY = (bm.Joint(HumanBodyBones.LeftLowerLeg).y + bm.Joint(HumanBodyBones.RightLowerLeg).y) * 0.5f;
            Vector3 centre = new Vector3(0f, 0f, bm.Joint(HumanBodyBones.Hips).z);
            int cols = 24, rows = toAnkles ? 9 : 5;
            var verts = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();
            var R = bm.Region;
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                float y = Mathf.Lerp(top, bottom, t);
                float slab = 0.05f * k;
                for (int col = 0; col <= cols; col++)
                {
                    float az = Mathf.Lerp(openDeg, 360f - openDeg, col / (float)cols) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
                    var ctr = new Vector3(centre.x, y, centre.z);
                    float r = bm.Support(ctr, dir, i => Mathf.Abs(bm.V[i].y - y) < slab &&
                        (R[i] == BodyRegion.Torso || R[i] == BodyRegion.Hips || R[i] == BodyRegion.Thigh || R[i] == BodyRegion.CalfHi || R[i] == BodyRegion.CalfLo));
                    r = Mathf.Max(r, 0.1f * k) + Mathf.Lerp(flareTop, flareBottom, t) * k;
                    verts.Add(ctr + dir * r);
                    // legs drive the lower rows: by side, front/back shared between both legs
                    float side = Mathf.Sin(az);
                    float wl = Mathf.Clamp01(0.5f - side * 0.9f);
                    float legT = Mathf.Clamp01((top - y) / Mathf.Max(0.01f, top - bottom));
                    float leg = Mathf.Clamp01(legT * 1.1f) * 0.9f;
                    bool lowerLeg = y < kneeY && calfL >= 0 && calfR >= 0;
                    int mainThigh = wl >= 0.5f ? thighL : thighR, otherThigh = wl >= 0.5f ? thighR : thighL;
                    int mainCalf = wl >= 0.5f ? calfL : calfR;
                    float main = Mathf.Max(wl, 1f - wl), other = 1f - main;
                    if (thighL < 0 || thighR < 0)
                    {
                        weights.Add(new BoneWeight { boneIndex0 = hips, weight0 = 1f });
                        continue;
                    }
                    float wh = 1f - leg;
                    float wMain = leg * main, wOther = leg * other;
                    float wCalf = 0f;
                    if (lowerLeg)
                    {
                        float c = Mathf.Clamp01((kneeY - y) / Mathf.Max(0.01f, kneeY - bottom)) * 0.55f;
                        wCalf = wMain * c;
                        wMain -= wCalf;
                    }
                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = hips, weight0 = wh,
                        boneIndex1 = mainThigh, weight1 = wMain,
                        boneIndex2 = otherThigh, weight2 = wOther,
                        boneIndex3 = lowerLeg ? mainCalf : hips, weight3 = wCalf
                    });
                }
            }
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int a = row * (cols + 1) + col, b = a + 1, d = a + cols + 1, e = d + 1;
                    tris.Add(a); tris.Add(d); tris.Add(b);
                    tris.Add(b); tris.Add(d); tris.Add(e);
                }
            }
            OrientOutward(verts, tris, centre);
            int count = verts.Count, triCount = tris.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = verts[i];
                Vector3 radial = new Vector3(p.x - centre.x, 0f, p.z - centre.z).normalized;
                verts.Add(p - radial * (0.005f * k));
                weights.Add(weights[i]);
            }
            for (int t = 0; t < triCount; t += 3)
            {
                tris.Add(tris[t] + count);
                tris.Add(tris[t + 2] + count);
                tris.Add(tris[t + 1] + count);
            }
            return bm.Custom(name, verts, tris, weights);
        }

        /// <summary>Front apron panel from the waist to below the knees, following the thighs.</summary>
        private static Mesh Apron(AnimeBodyMesh bm, float top, float bottom)
        {
            float k = bm.K;
            int hips = bm.BoneIndex(HumanBodyBones.Hips);
            int thighL = bm.BoneIndex(HumanBodyBones.LeftUpperLeg), thighR = bm.BoneIndex(HumanBodyBones.RightUpperLeg);
            if (hips < 0 || thighL < 0 || thighR < 0) return null;
            Vector3 centre = new Vector3(0f, 0f, bm.Joint(HumanBodyBones.Hips).z);
            const int cols = 8, rows = 5;
            var verts = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();
            var R = bm.Region;
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                float y = Mathf.Lerp(top, bottom, t);
                for (int col = 0; col <= cols; col++)
                {
                    float az = Mathf.Lerp(-48f, 48f, col / (float)cols) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
                    var ctr = new Vector3(centre.x, y, centre.z);
                    float r = bm.Support(ctr, dir, i => Mathf.Abs(bm.V[i].y - y) < 0.05f * k && (R[i] == BodyRegion.Hips || R[i] == BodyRegion.Thigh || R[i] == BodyRegion.Torso));
                    r = Mathf.Max(r, 0.1f * k) + (0.085f + 0.02f * t) * k;
                    verts.Add(ctr + dir * r);
                    float leg = t * 0.7f;
                    weights.Add(new BoneWeight { boneIndex0 = hips, weight0 = 1f - leg, boneIndex1 = Mathf.Sin(az) < 0f ? thighL : thighR, weight1 = leg });
                }
            }
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int a = row * (cols + 1) + col, b = a + 1, d = a + cols + 1, e = d + 1;
                    tris.Add(a); tris.Add(d); tris.Add(b);
                    tris.Add(b); tris.Add(d); tris.Add(e);
                }
            }
            OrientOutward(verts, tris, centre);
            return bm.Custom("Apron", verts, tris, weights);
        }

        private static void OrientOutward(List<Vector3> v, List<int> t, Vector3 centre)
        {
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], d = v[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, d - a);
                Vector3 mid = (a + b + d) / 3f;
                if (Vector3.Dot(n, new Vector3(mid.x - centre.x, 0f, mid.z - centre.z)) < 0f)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }
        }

        // ------------------------------------------------------------------ head accessories

        private static void HeadFrame(AnimeBodyMesh bm, out Vector3 centre, out float rx, out float rz, out float topY)
        {
            Vector3 lo = Vector3.one * 1e9f, hi = Vector3.one * -1e9f;
            for (int i = 0; i < bm.V.Length; i++)
            {
                if (bm.Region[i] != BodyRegion.Head) continue;
                lo = Vector3.Min(lo, bm.V[i]);
                hi = Vector3.Max(hi, bm.V[i]);
            }
            centre = (lo + hi) * 0.5f;
            rx = (hi.x - lo.x) * 0.5f;
            rz = (hi.z - lo.z) * 0.5f;
            topY = hi.y;
        }

        /// <summary>Cloth band around the forehead (rigid, on the head bone).</summary>
        private static void Headband(AnimeBodyMesh bm, Color color, int layer, float outline)
        {
            var head = bm.Skeleton[HumanBodyBones.Head];
            if (head == null) return;
            HeadFrame(bm, out var c, out float rx, out float rz, out float topY);
            float k = bm.K;
            float y = topY - 0.075f * k;
            const int seg = 20;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                // follow the skull at the band height (+ hair thickness)
                float ex = rx * 1.08f + 0.012f * k, ez = rz * 1.05f + 0.012f * k;
                var p = new Vector3(c.x + Mathf.Sin(a) * ex, y, c.z + Mathf.Cos(a) * ez);
                verts.Add(p + Vector3.down * 0.018f * k);
                verts.Add(p + Vector3.up * 0.018f * k);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
                tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 3);
            }
            OrientOutward(verts, tris, c);
            Rigid("Headband", head, bm, verts, tris, MaterialFactory.AnimeCharacter(color, MaterialFactory.CharacterSurface.Cloth, outline * 0.6f), layer);
        }

        /// <summary>Wide conical straw hat (rigid, on the head bone).</summary>
        private static void StrawHat(AnimeBodyMesh bm, int layer, float outline)
        {
            var head = bm.Skeleton[HumanBodyBones.Head];
            if (head == null) return;
            HeadFrame(bm, out var c, out _, out _, out float topY);
            float k = bm.K;
            var straw = MaterialFactory.AnimeCharacter(new Color(0.8f, 0.68f, 0.42f), MaterialFactory.CharacterSurface.Cloth, outline);
            var go = new GameObject("StrawHat");
            go.layer = layer;
            go.transform.SetParent(head, false);
            go.transform.position = bm.CanonToWorld.MultiplyPoint3x4(new Vector3(c.x, topY - 0.035f * k, c.z - 0.005f * k));
            go.transform.rotation = bm.CanonToWorld.rotation;
            float s = 1f / Mathf.Max(1e-5f, head.lossyScale.x);
            var cone = ProceduralMeshes.CreatePart("Crown", ProceduralMeshes.Cone(12), straw, go.transform, Vector3.zero, Quaternion.identity, new Vector3(0.52f, 0.15f, 0.52f) * k * s);
            var brim = ProceduralMeshes.CreatePart("Brim", PrimitiveType.Cylinder, straw, go.transform, Vector3.up * 0.002f * k * s, Vector3.zero, new Vector3(0.53f, 0.006f, 0.53f) * k * s);
            foreach (var part in new[] { cone, brim })
            {
                part.layer = layer;
                var col = part.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
        }

        private static void Rigid(string name, Transform bone, AnimeBodyMesh bm, List<Vector3> canonVerts, List<int> tris, Material mat, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(bone, false);
            var toLocal = go.transform.worldToLocalMatrix * bm.CanonToWorld;
            var v = new Vector3[canonVerts.Count];
            for (int i = 0; i < v.Length; i++) v[i] = toLocal.MultiplyPoint3x4(canonVerts[i]);
            var mesh = new Mesh { name = name, vertices = v };
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }
    }
}
