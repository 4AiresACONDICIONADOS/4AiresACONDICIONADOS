using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Dresses an imported model for the game's anime style (runs once when the visual is attached, while the
    /// model is in its bind pose):
    /// <list type="bullet">
    /// <item><b>AnimeSwordsman</b> (player, original design): flat anime skin, separate face mesh (hidden in first
    /// person), anime eyes with expressions, procedural hair with a spring ponytail, dark fitted top, short
    /// pale haori (open front, hanging sleeves, skirt, style-colored hem), style-colored obi, wide pleated
    /// hakama, forearm and shin wraps, tabi boots. Cloth are skinned shells of the body, so they deform with it.</item>
    /// <item><b>Nightspawn</b> / <b>HollowOni</b>: demon skin with glowing markings and eyes, claws, fangs, back
    /// spines / horns, torn hakama, rope belt; the Oni is bulked up and wears a bone mask and a wild mane.</item>
    /// <item><b>Toonify</b> (VRoid / custom): the model's own textures on the anime toon shader, blendshape face.</item>
    /// </list>
    /// Every renderer is registered with the <see cref="CharacterRig"/> (hit flash, dissolve, visibility,
    /// first-person hiding, afterimages, style accent, demon glow).
    /// </summary>
    public static class AnimeCharacterLook
    {
        private sealed class Cached
        {
            public Mesh Body, Head;
            public readonly List<(string name, Mesh mesh, int kind)> Shells = new List<(string, Mesh, int)>();
        }

        private static readonly Dictionary<string, Cached> Cache = new Dictionary<string, Cached>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        // shell kinds (material slots)
        private const int KTop = 0, KHakama = 1, KBelt = 2, KHaori = 3, KHem = 4, KWrap = 5, KBoots = 6, KMask = 7, KPants = 8, KRope = 9;

        // swordsman palette (original design)
        private static readonly Color Skin = new Color(0.98f, 0.84f, 0.74f);
        private static readonly Color TopColor = new Color(0.1f, 0.105f, 0.14f);
        private static readonly Color HakamaColor = new Color(0.13f, 0.14f, 0.27f);
        private static readonly Color HaoriColor = new Color(0.8f, 0.82f, 0.88f);
        private static readonly Color WrapColor = new Color(0.9f, 0.88f, 0.82f);
        private static readonly Color BootColor = new Color(0.08f, 0.075f, 0.1f);
        private static readonly Color HairColor = new Color(0.075f, 0.08f, 0.15f);
        private static readonly Color IrisTop = new Color(0.07f, 0.09f, 0.22f);
        private static readonly Color IrisLow = new Color(0.25f, 0.46f, 0.78f);

        public static void Apply(HumanoidSkeleton sk, CharacterRig rig, CharacterVisualProfile profile, int layer)
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

            AnimeBodyMesh bm = null;
            if (profile.look != VisualLook.Toonify && body != null)
            {
                var visualRoot = sk.ModelRoot.parent != null ? sk.ModelRoot.parent : sk.ModelRoot;
                var canon = Matrix4x4.TRS(visualRoot.position, sk.ModelRoot.rotation * Quaternion.Inverse(sk.FacingFix), Vector3.one);
                bm = AnimeBodyMesh.From(sk, body, canon);
                if (bm == null) Debug.LogWarning($"[AnimeCharacterLook] Body mesh '{body.sharedMesh.name}' is not readable (enable Read/Write): using toon materials only.");
            }
            if (bm == null)
            {
                Toonify(sk, rig, profile, layer);
                return;
            }

            switch (profile.look)
            {
                case VisualLook.AnimeSwordsman:
                    Swordsman(sk, rig, bm, eyes, brows, profile, layer);
                    break;
                case VisualLook.Nightspawn:
                case VisualLook.HollowOni:
                    Demon(sk, rig, bm, eyes, brows, profile, layer, profile.look == VisualLook.HollowOni);
                    break;
            }
            // anything else the model carries (accessories) gets the toon shader too
            foreach (var r in sk.Renderers)
            {
                if (r == null || r == body || r == eyes || r == brows) continue;
                ToonifyRenderer(r, profile.outline);
                rig.RegisterExternal(r, CharacterRig.ExternalPart.None, Color.white);
            }
        }

        // ------------------------------------------------------------------ player

        private static void Swordsman(HumanoidSkeleton sk, CharacterRig rig, AnimeBodyMesh bm, SkinnedMeshRenderer eyes, SkinnedMeshRenderer brows, CharacterVisualProfile profile, int layer)
        {
            float k = bm.K;
            float outline = profile.outline;
            var cached = GetOrBuild(bm, VisualLook.AnimeSwordsman, () => BuildSwordsmanMeshes(bm));

            // skin (covered areas pulled in so nothing pokes through the cloth) + face mesh
            bm.Renderer.sharedMesh = cached.Body;
            bm.Renderer.sharedMaterial = MaterialFactory.AnimeCharacter(Skin, MaterialFactory.CharacterSurface.Skin, outline);
            rig.RegisterExternal(bm.Renderer, CharacterRig.ExternalPart.None, Skin);
            var head = bm.Attach("Face", cached.Head, MaterialFactory.AnimeCharacter(Skin, MaterialFactory.CharacterSurface.Face, outline * 0.9f));
            rig.RegisterExternal(head, CharacterRig.ExternalPart.FirstPersonHidden, Skin);

            var accent = rig.Profile != null ? rig.Profile.accent : new Color(0.2f, 0.6f, 1f);
            foreach (var (name, mesh, kind) in cached.Shells)
            {
                Color c;
                var surface = MaterialFactory.CharacterSurface.Cloth;
                Texture tex = null;
                var flags = CharacterRig.ExternalPart.None;
                switch (kind)
                {
                    case KTop: c = TopColor; break;
                    case KHakama: c = HakamaColor; break;
                    case KBelt: c = accent; flags = CharacterRig.ExternalPart.Accent; break;
                    case KHaori: c = HaoriColor; break;
                    case KHem: c = accent; flags = CharacterRig.ExternalPart.Accent; break;
                    case KWrap: c = WrapColor; tex = WrapStripes(); break;
                    default: c = BootColor; break;
                }
                var smr = bm.Attach(name, mesh, MaterialFactory.AnimeCharacter(c, surface, kind == KWrap ? outline * 0.6f : outline, tex));
                rig.RegisterExternal(smr, flags, c);
            }

            // eyes, brows, mouth, hair
            var eyeMats = EyeMaterials(false, IrisTop, IrisLow);
            if (eyes != null)
            {
                eyes.sharedMaterial = eyeMats[0];
                eyes.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rig.RegisterExternal(eyes, CharacterRig.ExternalPart.FirstPersonHidden, Color.white);
            }
            if (brows != null)
            {
                brows.sharedMaterial = MaterialFactory.AnimeCharacter(HairColor, MaterialFactory.CharacterSurface.Hair, 0f);
                rig.RegisterExternal(brows, CharacterRig.ExternalPart.FirstPersonHidden, HairColor);
            }
            var mouth = BuildMouth(bm, rig, layer, new Color(0.26f, 0.07f, 0.09f));
            AnimeHair.Build(bm, rig, AnimeHair.Style.Swordsman, HairColor, accent, layer, outline, 7);

            var face = sk.ModelRoot.gameObject.AddComponent<AnimeFace>();
            var source = rig.GetComponent<ProceduralAnimator>();
            face.Initialize(source, eyes, eyeMats, mouth, false);
        }

        private static Cached BuildSwordsmanMeshes(AnimeBodyMesh bm)
        {
            float k = bm.K;
            var c = new Cached();
            var R = bm.Region;
            float hipsY = bm.Joint(HumanBodyBones.Hips).y;
            float spineZ = bm.Joint(HumanBodyBones.Spine).z;
            float neckY = bm.Joint(HumanBodyBones.Neck).y;

            bool Covered(int i)
            {
                var r = R[i];
                return r == BodyRegion.Torso || r == BodyRegion.Hips || r == BodyRegion.UpperArm || r == BodyRegion.ForearmHi || r == BodyRegion.ForearmLo
                       || r == BodyRegion.Thigh || r == BodyRegion.CalfHi || r == BodyRegion.CalfLo || r == BodyRegion.Foot;
            }
            bool HeadTri(int a, int b, int d) => IsHead(R[a]) && IsHead(R[b]) && IsHead(R[d]);
            c.Body = bm.Subset("AnimeBody", (a, b, d) => !HeadTri(a, b, d), (i, p) => Covered(i) ? p - bm.N[i] * (0.006f * k) : p);
            c.Head = bm.Subset("AnimeFace", HeadTri);

            // dark fitted top: torso, high collar, sleeves to mid forearm; tucked under the obi
            var top = bm.Shell("Top", i =>
                R[i] == BodyRegion.Torso || R[i] == BodyRegion.UpperArm || R[i] == BodyRegion.ForearmHi ||
                (R[i] == BodyRegion.Neck && bm.V[i].y < neckY + 0.03f * k) ||
                (R[i] == BodyRegion.Hips && bm.V[i].y > hipsY - 0.02f * k), 0.007f, null, 4);
            if (top != null) c.Shells.Add(("Top", top, KTop));

            // hakama: wide, pleated, flaring toward the calves
            var hakama = bm.Shell("Hakama", i =>
                R[i] == BodyRegion.Hips || R[i] == BodyRegion.Thigh || R[i] == BodyRegion.CalfHi ||
                (R[i] == BodyRegion.Torso && bm.V[i].y < hipsY + 0.08f * k), 0.012f, (i, p, n) => LegFlare(bm, i, p, 0.1f * k, 0.007f * k), 3);
            if (hakama != null) c.Shells.Add(("Hakama", hakama, KHakama));

            // obi (style color)
            var belt = bm.Shell("Obi", i =>
                (R[i] == BodyRegion.Torso || R[i] == BodyRegion.Hips) && bm.V[i].y > hipsY + 0.01f * k && bm.V[i].y < hipsY + 0.115f * k, 0.024f, null, 2);
            if (belt != null) c.Shells.Add(("Obi", belt, KBelt));

            // haori: open front, hanging sleeves
            var haori = bm.Shell("Haori", i => R[i] == BodyRegion.Torso || R[i] == BodyRegion.UpperArm || R[i] == BodyRegion.ForearmHi, 0.02f,
                (i, p, n) => Sleeve(bm, i, p, n), 4, -1f,
                (a, b, d) =>
                {
                    Vector3 ctr = (bm.V[a] + bm.V[b] + bm.V[d]) / 3f;
                    bool front = ctr.z > spineZ && Mathf.Abs(ctr.x) < 0.075f * k;
                    bool neck = ctr.y > neckY - 0.02f * k;
                    return !front && !neck;
                });
            if (haori != null) c.Shells.Add(("Haori", haori, KHaori));
            BuildHaoriSkirt(bm, c);

            // wraps (forearms, shins) and tabi boots
            var wrapA = bm.Shell("ArmWraps", i => R[i] == BodyRegion.ForearmLo, 0.005f, null, 1, -1f, null, (i, p) => LimbUV(bm, i, p, true));
            if (wrapA != null) c.Shells.Add(("ArmWraps", wrapA, KWrap));
            var wrapL = bm.Shell("LegWraps", i => R[i] == BodyRegion.CalfLo, 0.007f, null, 1, -1f, null, (i, p) => LimbUV(bm, i, p, false));
            if (wrapL != null) c.Shells.Add(("LegWraps", wrapL, KWrap));
            var boots = bm.Shell("Boots", i => R[i] == BodyRegion.Foot, 0.006f, null, 2);
            if (boots != null) c.Shells.Add(("Boots", boots, KBoots));
            return c;
        }

        private static bool IsHead(BodyRegion r) => r == BodyRegion.Head || r == BodyRegion.Neck;

        /// <summary>Radial flare from the leg axis growing toward the hem, with soft pleats.</summary>
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

        /// <summary>Wide haori sleeves: the lower half hangs below the arm and widens toward the elbow.</summary>
        private static Vector3 Sleeve(AnimeBodyMesh bm, int i, Vector3 p, Vector3 n)
        {
            var r = bm.Region[i];
            if (r != BodyRegion.UpperArm && r != BodyRegion.ForearmHi) return p;
            bool left = bm.Side[i] < 0;
            Vector3 sh = bm.Joint(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Vector3 el = bm.Joint(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Vector3 axis = el - sh;
            float len = axis.magnitude;
            if (len < 1e-4f) return p;
            float t = Mathf.Clamp(Vector3.Dot(bm.V[i] - sh, axis / len) / len, 0f, 1.5f);
            Vector3 centre = sh + axis / len * Vector3.Dot(bm.V[i] - sh, axis / len);
            float k = bm.K;
            if (bm.V[i].y < centre.y - 0.01f * k) p.y -= 0.07f * k * t;
            return p + n * (0.02f * k * t);
        }

        /// <summary>Spiral UVs along the forearm / shin for the wrap stripes.</summary>
        private static Vector2 LimbUV(AnimeBodyMesh bm, int i, Vector3 p, bool arm)
        {
            bool left = bm.Side[i] < 0;
            Vector3 a = bm.Joint(arm ? (left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm) : (left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg));
            Vector3 b = bm.Joint(arm ? (left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand) : (left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot));
            Vector3 axis = b - a;
            float len = Mathf.Max(1e-4f, axis.magnitude);
            axis /= len;
            float along = Vector3.Dot(p - a, axis);
            Vector3 radial = p - (a + axis * along);
            Vector3 refDir = Vector3.Cross(axis, Vector3.forward);
            if (refDir.sqrMagnitude < 1e-4f) refDir = Vector3.Cross(axis, Vector3.up);
            float ang = Vector3.SignedAngle(refDir, radial, axis) / 360f;
            return new Vector2(along / (0.028f * bm.K) + ang, 0.5f);
        }

        /// <summary>Short haori skirt from the waist to mid-thigh, open at the front, double-sided, legs push it.</summary>
        private static void BuildHaoriSkirt(AnimeBodyMesh bm, Cached c)
        {
            float k = bm.K;
            int hips = bm.BoneIndex(HumanBodyBones.Hips);
            int thighL = bm.BoneIndex(HumanBodyBones.LeftUpperLeg), thighR = bm.BoneIndex(HumanBodyBones.RightUpperLeg);
            if (hips < 0) return;
            float hipsY = bm.Joint(HumanBodyBones.Hips).y;
            float top = hipsY + 0.05f * k, bottom = hipsY - 0.3f * k;
            Vector3 centre = new Vector3(0f, 0f, bm.Joint(HumanBodyBones.Hips).z);
            const int cols = 22, rows = 5;
            const float open = 26f;
            var verts = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();
            var hem = new List<Vector3>();
            var hemW = new List<BoneWeight>();
            var hemT = new List<int>();
            var R = bm.Region;
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                float y = Mathf.Lerp(top, bottom, t);
                float slab = 0.05f * k;
                for (int col = 0; col <= cols; col++)
                {
                    float az = Mathf.Lerp(open, 360f - open, col / (float)cols) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
                    var ctr = new Vector3(centre.x, y, centre.z);
                    float r = bm.Support(ctr, dir, i => Mathf.Abs(bm.V[i].y - y) < slab &&
                        (R[i] == BodyRegion.Torso || R[i] == BodyRegion.Hips || R[i] == BodyRegion.Thigh));
                    r = Mathf.Max(r, 0.12f * k) + Mathf.Lerp(0.035f, 0.1f, t) * k;
                    verts.Add(ctr + dir * r);
                    float leg = t * 0.55f * Mathf.Abs(Mathf.Cos(az));
                    int thigh = Mathf.Sin(az) < 0f ? thighL : thighR;
                    weights.Add(thigh >= 0
                        ? new BoneWeight { boneIndex0 = hips, weight0 = 1f - leg, boneIndex1 = thigh, weight1 = leg }
                        : new BoneWeight { boneIndex0 = hips, weight0 = 1f });
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
            // inner side (slightly inward) so the skirt reads from any angle
            int n = verts.Count;
            int triCount = tris.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = verts[i];
                Vector3 radial = new Vector3(p.x - centre.x, 0f, p.z - centre.z).normalized;
                verts.Add(p - radial * (0.005f * k));
                weights.Add(weights[i]);
            }
            for (int t = 0; t < triCount; t += 3)
            {
                tris.Add(tris[t] + n);
                tris.Add(tris[t + 2] + n);
                tris.Add(tris[t + 1] + n);
            }
            var skirt = bm.Custom("HaoriSkirt", verts, tris, weights);
            if (skirt != null) c.Shells.Add(("HaoriSkirt", skirt, KHaori));

            // style-colored hem band on the last row
            for (int col = 0; col <= cols; col++)
            {
                int bottomIdx = rows * (cols + 1) + col;
                Vector3 p = verts[bottomIdx];
                Vector3 radial = new Vector3(p.x - centre.x, 0f, p.z - centre.z).normalized;
                hem.Add(p + radial * (0.003f * k));
                hem.Add(p + radial * (0.003f * k) + Vector3.up * (0.035f * k));
                hemW.Add(weights[bottomIdx]);
                hemW.Add(weights[bottomIdx]);
            }
            for (int col = 0; col < cols; col++)
            {
                int a = col * 2, b = a + 2;
                hemT.Add(a); hemT.Add(a + 1); hemT.Add(b);
                hemT.Add(b); hemT.Add(a + 1); hemT.Add(b + 1);
            }
            OrientOutward(hem, hemT, centre);
            var hemMesh = bm.Custom("HaoriHem", hem, hemT, hemW);
            if (hemMesh != null) c.Shells.Add(("HaoriHem", hemMesh, KHem));
        }

        /// <summary>Flips triangles facing the vertical axis through <paramref name="centre"/>.</summary>
        private static void OrientOutward(List<Vector3> v, List<int> t, Vector3 centre)
        {
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], d = v[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, d - a);
                Vector3 mid = (a + b + d) / 3f;
                Vector3 radial = new Vector3(mid.x - centre.x, 0f, mid.z - centre.z);
                if (Vector3.Dot(n, radial) < 0f)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }
        }

        // ------------------------------------------------------------------ demons

        private static void Demon(HumanoidSkeleton sk, CharacterRig rig, AnimeBodyMesh bm, SkinnedMeshRenderer eyes, SkinnedMeshRenderer brows, CharacterVisualProfile profile, int layer, bool oni)
        {
            float outline = profile.outline;
            var p = rig.Profile;
            Color skin = p != null ? p.skin : new Color(0.16f, 0.12f, 0.2f);
            var cached = GetOrBuild(bm, profile.look, () => BuildDemonMeshes(bm, oni));
            var skinMat = MaterialFactory.AnimeCharacter(skin, MaterialFactory.CharacterSurface.DemonSkin, outline, null, null, oni ? 7f : 10f);
            bm.Renderer.sharedMesh = cached.Body;
            bm.Renderer.sharedMaterial = skinMat;
            rig.RegisterExternal(bm.Renderer, CharacterRig.ExternalPart.Mark, skin);
            var head = bm.Attach("Face", cached.Head, skinMat);
            rig.RegisterExternal(head, CharacterRig.ExternalPart.Mark, skin);

            Color cloth = oni ? new Color(0.1f, 0.07f, 0.08f) : new Color(0.07f, 0.05f, 0.09f);
            Color rope = oni ? new Color(0.55f, 0.4f, 0.15f) : new Color(0.3f, 0.22f, 0.15f);
            Color bone = new Color(0.88f, 0.85f, 0.78f);
            foreach (var (name, mesh, kind) in cached.Shells)
            {
                Color c = kind == KRope ? rope : kind == KMask ? bone : cloth;
                var surface = kind == KMask ? MaterialFactory.CharacterSurface.Bone : MaterialFactory.CharacterSurface.Cloth;
                var smr = bm.Attach(name, mesh, MaterialFactory.AnimeCharacter(c, surface, outline, null, null, 6f));
                rig.RegisterExternal(smr, kind == KMask ? CharacterRig.ExternalPart.Mark : CharacterRig.ExternalPart.None, c);
            }
            if (eyes != null)
            {
                eyes.sharedMaterial = MaterialFactory.AnimeCharacter(Color.white, MaterialFactory.CharacterSurface.Eye, 0f,
                    AnimeFace.EyeTexture(AnimeFace.Expression.Neutral, Color.white, new Color(1f, 0.85f, 0.6f), Color.black, true));
                eyes.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rig.RegisterExternal(eyes, CharacterRig.ExternalPart.Eye, Color.white);
            }
            if (brows != null)
            {
                brows.sharedMaterial = MaterialFactory.AnimeCharacter(new Color(0.05f, 0.03f, 0.06f), MaterialFactory.CharacterSurface.Hair, 0f);
                rig.RegisterExternal(brows, CharacterRig.ExternalPart.None, Color.black);
            }
            var mouth = BuildMouth(bm, rig, layer, new Color(0.3f, 0.02f, 0.05f));
            DemonFeatures.Build(bm, rig, layer, oni, outline);
            AnimeHair.Build(bm, rig, oni ? AnimeHair.Style.Mane : AnimeHair.Style.Ragged,
                oni ? new Color(0.86f, 0.86f, 0.9f) : new Color(0.06f, 0.04f, 0.08f), rope, layer, outline, oni ? 11 : 5);
            var face = sk.ModelRoot.gameObject.AddComponent<AnimeFace>();
            face.Initialize(rig.GetComponent<ProceduralAnimator>(), eyes, null, mouth, true, 0.45f);
        }

        private static Cached BuildDemonMeshes(AnimeBodyMesh bm, bool oni)
        {
            float k = bm.K;
            var c = new Cached();
            var R = bm.Region;
            float hipsY = bm.Joint(HumanBodyBones.Hips).y;
            bool HeadTri(int a, int b, int d) => IsHead(R[a]) && IsHead(R[b]) && IsHead(R[d]);
            // The Oni is bulkier (inflated limbs and torso); the Nightspawn stays lean.
            float bulk = oni ? 0.014f * k : 0f;
            c.Body = bm.Subset("DemonBody", (a, b, d) => !HeadTri(a, b, d), (i, p) =>
            {
                var r = R[i];
                bool limb = r == BodyRegion.Torso || r == BodyRegion.UpperArm || r == BodyRegion.ForearmHi || r == BodyRegion.ForearmLo || r == BodyRegion.Thigh || r == BodyRegion.CalfHi;
                bool covered = r == BodyRegion.Hips || r == BodyRegion.Thigh || r == BodyRegion.CalfHi;
                Vector3 q = limb ? p + bm.N[i] * bulk : p;
                return covered ? q - bm.N[i] * (0.004f * k) : q;
            });
            c.Head = bm.Subset("DemonFace", HeadTri);

            // torn hakama: flared, ragged hem
            var pants = bm.Shell("TornHakama", i => R[i] == BodyRegion.Hips || R[i] == BodyRegion.Thigh || R[i] == BodyRegion.CalfHi ||
                                                 (R[i] == BodyRegion.Torso && bm.V[i].y < hipsY + 0.07f * k), 0.012f + bulk / k,
                (i, p, n) =>
                {
                    var q = LegFlare(bm, i, p, (oni ? 0.08f : 0.05f) * k, 0.006f * k);
                    if (R[i] == BodyRegion.CalfHi)
                    {
                        float ang = Mathf.Atan2(bm.V[i].z, bm.V[i].x);
                        q.y += (Mathf.Sin(ang * 11f) * 0.5f + Mathf.Sin(ang * 5f + 1.3f) * 0.5f) * 0.035f * k;
                    }
                    return q;
                }, 3);
            if (pants != null) c.Shells.Add(("TornHakama", pants, KPants));
            var rope = bm.Shell("Rope", i => (R[i] == BodyRegion.Torso || R[i] == BodyRegion.Hips) && bm.V[i].y > hipsY + 0.02f * k && bm.V[i].y < hipsY + (oni ? 0.1f : 0.07f) * k,
                (oni ? 0.034f : 0.022f) + bulk / k, null, 2);
            if (rope != null) c.Shells.Add(("Rope", rope, KRope));

            if (oni)
            {
                // bone mask over the upper face with eye holes (glowing markings follow the rig's mark intensity)
                float mouthY = MouthPoint(bm).y;
                Vector3 eyeL = Vector3.zero, eyeR = Vector3.zero;
                int nl = 0, nr = 0;
                float headZ = 0f;
                int nh = 0;
                for (int i = 0; i < bm.V.Length; i++)
                {
                    if (R[i] != BodyRegion.Head) continue;
                    headZ += bm.V[i].z;
                    nh++;
                }
                headZ /= Mathf.Max(1, nh);
                Vector3 eyeCentre = new Vector3(0f, bm.Skeleton.Height * 0.938f, 0f);
                for (int i = 0; i < bm.V.Length; i++)
                {
                    if (R[i] != BodyRegion.Head || Mathf.Abs(bm.V[i].y - eyeCentre.y) > 0.012f * k || bm.V[i].z < headZ + 0.05f * k) continue;
                    if (bm.V[i].x < 0f) { eyeL += bm.V[i]; nl++; } else { eyeR += bm.V[i]; nr++; }
                }
                if (nl > 0) eyeL /= nl;
                if (nr > 0) eyeR /= nr;
                var mask = bm.Shell("OniMask", i => R[i] == BodyRegion.Head && bm.V[i].z > headZ + 0.015f * k && bm.V[i].y > mouthY + 0.008f * k, 0.013f, null, 3, -1f,
                    (a, b, d) =>
                    {
                        Vector3 ctr = (bm.V[a] + bm.V[b] + bm.V[d]) / 3f;
                        float hole = 0.02f * k;
                        return (nl == 0 || (ctr - eyeL).sqrMagnitude > hole * hole) && (nr == 0 || (ctr - eyeR).sqrMagnitude > hole * hole);
                    });
                if (mask != null) c.Shells.Add(("OniMask", mask, KMask));
            }
            return c;
        }

        // ------------------------------------------------------------------ shared pieces

        /// <summary>Front of the lips, measured on the face (just below the nose, the most forward point).</summary>
        public static Vector3 MouthPoint(AnimeBodyMesh bm)
        {
            float h = bm.Skeleton.Height;
            float lo = h * 0.885f, hi = h * 0.908f;
            Vector3 best = new Vector3(0f, h * 0.9f, 0f);
            float bestZ = float.NegativeInfinity;
            for (int i = 0; i < bm.V.Length; i++)
            {
                var v = bm.V[i];
                if (bm.Region[i] != BodyRegion.Head || Mathf.Abs(v.x) > 0.012f * bm.K || v.y < lo || v.y > hi) continue;
                if (v.z > bestZ)
                {
                    bestZ = v.z;
                    best = v;
                }
            }
            return best;
        }

        /// <summary>An anime mouth: a dark ellipse in front of the lips that opens vertically (scale y). <paramref name="rig"/> may be null.</summary>
        public static Transform BuildMouth(AnimeBodyMesh bm, CharacterRig rig, int layer, Color color)
        {
            var head = bm.Skeleton[HumanBodyBones.Head];
            if (head == null) return null;
            float k = bm.K;
            Vector3 lips = MouthPoint(bm) + new Vector3(0f, -0.004f * k, 0.003f * k);
            var go = new GameObject("Mouth");
            var t = go.transform;
            t.SetParent(head, false);
            t.position = bm.CanonToWorld.MultiplyPoint3x4(lips);
            t.rotation = bm.CanonToWorld.rotation;
            go.layer = layer;
            const int seg = 14;
            var verts = new Vector3[seg + 1];
            var tris = new int[seg * 3];
            float w = 0.017f * k, hgt = 0.011f * k;
            float s = 1f / Mathf.Max(1e-5f, t.lossyScale.x);
            verts[0] = Vector3.zero;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * w, Mathf.Sin(a) * hgt, -Mathf.Abs(Mathf.Sin(a)) * 0.003f * k) * s;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % seg + 1;
                tris[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Mouth", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            // Face +Z (toward the viewer in front of the character): flip if the fan came out backwards.
            if (Vector3.Dot(mesh.normals[0], Vector3.forward) < 0f)
            {
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int tmp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = tmp;
                }
                mesh.triangles = tris;
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFactory.AnimeCharacter(color, MaterialFactory.CharacterSurface.Eye, 0f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.enabled = false;
            if (rig != null) rig.RegisterExternal(mr, CharacterRig.ExternalPart.FirstPersonHidden, color);
            return t;
        }

        /// <summary>Eye materials for the five expressions (index = <see cref="AnimeFace.Expression"/>).</summary>
        public static Material[] EyeMaterials(bool demon, Color iris, Color irisLow)
        {
            var mats = new Material[5];
            for (int i = 0; i < mats.Length; i++)
            {
                var tex = AnimeFace.EyeTexture((AnimeFace.Expression)i, iris, irisLow, Skin, demon);
                mats[i] = MaterialFactory.AnimeCharacter(Color.white, MaterialFactory.CharacterSurface.Eye, 0f, tex);
            }
            return mats;
        }

        private static Texture2D WrapStripes() => ProceduralTextures.Generate("wrap_stripes", 64, 8, (u, v) =>
        {
            float band = Mathf.Repeat(u * 2f, 1f);
            float edge = Mathf.SmoothStep(0f, 0.08f, band) * (1f - Mathf.SmoothStep(0.86f, 0.94f, band));
            float shade = Mathf.Lerp(0.72f, 1f, edge);
            return new Color(shade, shade, shade * 0.98f, 1f);
        }, TextureWrapMode.Repeat);

        private static Cached GetOrBuild(AnimeBodyMesh bm, VisualLook look, System.Func<Cached> build)
        {
            string key = $"{bm.Source.GetInstanceID()}_{look}_{bm.K:F3}";
            if (Cache.TryGetValue(key, out var c) && c != null && c.Body != null) return c;
            c = build();
            Cache[key] = c;
            return c;
        }

        // ------------------------------------------------------------------ toonify (imported look kept)

        private static void Toonify(HumanoidSkeleton sk, CharacterRig rig, CharacterVisualProfile profile, int layer)
        {
            SkinnedMeshRenderer shapes = null;
            foreach (var r in sk.Renderers)
            {
                if (r == null) continue;
                string n = r.name.ToLowerInvariant();
                bool faceLike = n.Contains("face") || n.Contains("head") || n.Contains("hair") || n.Contains("eye") || n.Contains("brow") || n.Contains("mouth");
                if (profile.toonMaterials) ToonifyRenderer(r, profile.outline);
                rig.RegisterExternal(r, faceLike ? CharacterRig.ExternalPart.FirstPersonHidden : CharacterRig.ExternalPart.None, Color.white);
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0 && (shapes == null || n.Contains("face"))) shapes = smr;
            }
            var face = sk.ModelRoot.gameObject.AddComponent<AnimeFace>();
            face.Initialize(rig.GetComponent<ProceduralAnimator>(), null, null, null, false);
            face.UseBlendShapes(shapes);
        }

        private static void ToonifyRenderer(Renderer r, float outline)
        {
            var mats = r.sharedMaterials;
            string n = r.name.ToLowerInvariant();
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                Texture tex = m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m != null && m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                Color color = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m != null && m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                string mn = m != null ? m.name.ToLowerInvariant() : "";
                var surface = n.Contains("eye") || mn.Contains("eye") ? MaterialFactory.CharacterSurface.Eye
                    : n.Contains("hair") || mn.Contains("hair") ? MaterialFactory.CharacterSurface.Hair
                    : n.Contains("face") || mn.Contains("face") || mn.Contains("skin") ? MaterialFactory.CharacterSurface.Face
                    : MaterialFactory.CharacterSurface.Cloth;
                mats[i] = MaterialFactory.AnimeCharacter(color, surface, outline, tex);
            }
            r.sharedMaterials = mats;
        }
    }
}
