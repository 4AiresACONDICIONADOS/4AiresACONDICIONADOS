using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.Rendering
{
    public enum VfxBlend
    {
        Additive = 0,
        AlphaBlend = 1,
        /// <summary>Premultiplied-style "soft additive" that keeps colors from blowing out.</summary>
        SoftAdditive = 2
    }

    /// <summary>
    /// Creates and caches materials for the project's hand-written shaders, with safe URP fallbacks
    /// if a shader is missing. Per-object variations use MaterialPropertyBlocks, not new materials.
    /// </summary>
    public static class MaterialFactory
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            Shaders.Clear();
        }

        public static Shader FindShader(string name)
        {
            if (Shaders.TryGetValue(name, out var s) && s != null) return s;
            s = Shader.Find(name);
            if (s == null)
            {
                s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Unlit/Color");
                Debug.LogWarning($"[MaterialFactory] Shader '{name}' not found, using fallback '{(s != null ? s.name : "none")}'.");
            }
            Shaders[name] = s;
            return s;
        }

        private static Material Get(string key, string shader, System.Action<Material> setup)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(FindShader(shader)) { name = "BoE_" + key };
            setup?.Invoke(m);
            Cache[key] = m;
            return m;
        }

        private static string ColorKey(Color c) => $"{c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}";

        /// <summary>Cel-shaded material for characters and props.</summary>
        /// <param name="outline">Outline width (0 = none). Characters ~1.6, props ~0.8, scenery 0.</param>
        /// <param name="character">Characters stay lit during anime flash frames.</param>
        public static Material Toon(Color baseColor, float outline = 1.4f, bool character = false, Color? emission = null,
            float shadeDarken = 0.52f, float rim = 0.35f, float spec = 0f, Texture detail = null, float tiling = 1f)
        {
            Color em = emission ?? Color.black;
            string key = $"toon_{ColorKey(baseColor)}_{outline:F2}_{character}_{ColorKey(em)}_{shadeDarken:F2}_{rim:F2}_{spec:F2}_{(detail != null ? detail.name : "none")}_{tiling:F2}";
            return Get(key, ShaderIds.ToonLit, m =>
            {
                m.SetColor(ShaderIds.BaseColor, baseColor);
                // Shadow color: darker, slightly shifted to cool purple for a night anime palette.
                Color shade = new Color(baseColor.r * shadeDarken * 0.9f, baseColor.g * shadeDarken * 0.88f, baseColor.b * shadeDarken * 1.12f + 0.02f, 1f);
                m.SetColor(ShaderIds.ShadeColor, shade);
                m.SetFloat(ShaderIds.ShadeThreshold, 0.42f);
                m.SetFloat(ShaderIds.ShadeSoftness, 0.035f);
                m.SetColor(ShaderIds.RimColor, new Color(0.55f, 0.65f, 1f) * rim);
                m.SetFloat(ShaderIds.RimPower, 3.5f);
                m.SetColor(ShaderIds.SpecColor, Color.white * spec);
                m.SetFloat(ShaderIds.SpecSize, 0.08f);
                m.SetColor(ShaderIds.EmissionColor, em);
                m.SetColor(ShaderIds.OutlineColor, new Color(0.03f, 0.02f, 0.05f, 1f));
                m.SetFloat(ShaderIds.OutlineWidth, outline);
                m.SetFloat(ShaderIds.IsCharacter, character ? 1f : 0f);
                if (detail != null)
                {
                    m.SetTexture(ShaderIds.BaseMap, detail);
                    m.SetTextureScale(ShaderIds.BaseMap, new Vector2(tiling, tiling));
                }
                m.enableInstancing = true;
            });
        }

        /// <summary>Look presets of <see cref="AnimeCharacter"/>.</summary>
        public enum CharacterSurface
        {
            /// <summary>Cloth: two soft bands, low rim.</summary>
            Cloth = 0,
            /// <summary>Skin of the body: warm shade, soft.</summary>
            Skin = 1,
            /// <summary>Face: flattened shading (anime faces carry little shadow).</summary>
            Face = 2,
            /// <summary>Hair: deep shade, crisp rim and a hard highlight.</summary>
            Hair = 3,
            /// <summary>Eyes: unlit-ish, no outline.</summary>
            Eye = 4,
            /// <summary>Demon skin: dark bands, strong rim, markings (color from _MarkColor, set per renderer).</summary>
            DemonSkin = 5,
            /// <summary>Horn / bone / claw / mask: bright rim, glossy highlight.</summary>
            Bone = 6
        }

        /// <summary>
        /// AnimeCharacterToon material for imported characters (cached per look). Per-character values (hit flash,
        /// dissolve, accent, eye glow, markings) are MaterialPropertyBlocks set by CharacterRig.
        /// </summary>
        public static Material AnimeCharacter(Color baseColor, CharacterSurface surface, float outline = 1.4f, Texture texture = null,
            Color? emission = null, float markScale = 9f)
        {
            Color em = emission ?? Color.black;
            string key = $"anime_{surface}_{ColorKey(baseColor)}_{outline:F2}_{(texture != null ? texture.name : "none")}_{ColorKey(em)}_{markScale:F1}";
            return Get(key, ShaderIds.AnimeCharacterToon, m =>
            {
                float shadeK, shade2K, threshold, rim, spec, flatten = 0f, rimThreshold = 0.26f;
                Color tint;
                switch (surface)
                {
                    case CharacterSurface.Skin: shadeK = 0.72f; shade2K = 0.5f; threshold = 0.4f; rim = 0.3f; spec = 0f; tint = new Color(1f, 0.82f, 0.86f); break;
                    case CharacterSurface.Face: shadeK = 0.8f; shade2K = 0.62f; threshold = 0.36f; rim = 0.18f; spec = 0f; flatten = 0.75f; tint = new Color(1f, 0.84f, 0.88f); break;
                    case CharacterSurface.Hair: shadeK = 0.5f; shade2K = 0.3f; threshold = 0.45f; rim = 0.6f; spec = 0.35f; tint = new Color(0.85f, 0.85f, 1.1f); rimThreshold = 0.22f; break;
                    case CharacterSurface.Eye: shadeK = 0.9f; shade2K = 0.8f; threshold = 0.2f; rim = 0f; spec = 0f; flatten = 1f; tint = Color.white; break;
                    case CharacterSurface.DemonSkin: shadeK = 0.45f; shade2K = 0.22f; threshold = 0.46f; rim = 0.9f; spec = 0.1f; tint = new Color(0.9f, 0.8f, 1.15f); rimThreshold = 0.2f; break;
                    case CharacterSurface.Bone: shadeK = 0.6f; shade2K = 0.4f; threshold = 0.42f; rim = 0.55f; spec = 0.45f; tint = new Color(0.95f, 0.9f, 1f); break;
                    default: shadeK = 0.55f; shade2K = 0.34f; threshold = 0.44f; rim = 0.35f; spec = 0f; tint = new Color(0.88f, 0.86f, 1.12f); break;
                }
                Color shade = new Color(baseColor.r * shadeK * tint.r, baseColor.g * shadeK * tint.g, Mathf.Min(1f, baseColor.b * shadeK * tint.b + 0.015f), 1f);
                Color shade2 = new Color(baseColor.r * shade2K * tint.r, baseColor.g * shade2K * tint.g, Mathf.Min(1f, baseColor.b * shade2K * tint.b + 0.02f), 1f);
                m.SetColor(ShaderIds.BaseColor, baseColor);
                m.SetColor(ShaderIds.ShadeColor, shade);
                m.SetColor(ShaderIds.ShadeColor2, shade2);
                m.SetFloat(ShaderIds.ShadeThreshold, threshold);
                m.SetFloat(ShaderIds.Shade2Threshold, threshold * 0.42f);
                m.SetFloat(ShaderIds.ShadeSoftness, surface == CharacterSurface.Face || surface == CharacterSurface.Skin ? 0.05f : 0.03f);
                m.SetFloat(ShaderIds.FaceFlatten, flatten);
                m.SetColor(ShaderIds.RimColor, new Color(0.55f, 0.65f, 1f) * rim);
                m.SetFloat(ShaderIds.RimPower, 3.2f);
                m.SetFloat(ShaderIds.RimThreshold, rimThreshold);
                m.SetColor(ShaderIds.SpecColor, Color.white * spec);
                m.SetFloat(ShaderIds.SpecSize, surface == CharacterSurface.Hair ? 0.05f : 0.08f);
                m.SetColor(ShaderIds.EmissionColor, em);
                m.SetColor(ShaderIds.MarkColor, Color.black);
                m.SetFloat(ShaderIds.MarkScale, markScale);
                m.SetColor(ShaderIds.OutlineColor, surface == CharacterSurface.Hair ? new Color(0.02f, 0.02f, 0.06f, 1f) : new Color(0.03f, 0.02f, 0.05f, 1f));
                m.SetFloat(ShaderIds.OutlineWidth, surface == CharacterSurface.Eye ? 0f : outline);
                m.SetFloat(ShaderIds.IsCharacter, 1f);
                if (texture != null) m.SetTexture(ShaderIds.BaseMap, texture);
                m.enableInstancing = true;
            });
        }

        /// <summary>Particle / sprite material.</summary>
        public static Material Vfx(Texture texture, VfxBlend blend = VfxBlend.Additive, float softParticles = 0.4f)
        {
            string tex = texture != null ? texture.name : "none";
            string key = $"vfx_{tex}_{blend}_{softParticles:F2}";
            string shader = blend == VfxBlend.AlphaBlend ? ShaderIds.VfxAlpha : ShaderIds.VfxAdditive;
            return Get(key, shader, m =>
            {
                if (texture != null) m.SetTexture(ShaderIds.MainTex, texture);
                m.SetColor(ShaderIds.TintColor, Color.white);
                m.SetFloat("_SoftFade", softParticles);
                if (blend == VfxBlend.SoftAdditive)
                {
                    m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.OneMinusDstColor);
                    m.SetFloat(ShaderIds.DstBlend, (float)BlendMode.One);
                }
                else if (blend == VfxBlend.Additive)
                {
                    m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.SrcAlpha);
                    m.SetFloat(ShaderIds.DstBlend, (float)BlendMode.One);
                }
            });
        }

        /// <summary>Material for procedural ribbon / tube meshes (water serpent, fire arcs, wind spirals...).</summary>
        public static Material Ribbon(string key, Color core, Color edge, VfxBlend blend, Texture noise = null, float scroll = 1.5f, float fresnel = 2f)
        {
            string k = $"ribbon_{key}_{ColorKey(core)}_{ColorKey(edge)}_{blend}";
            var m = Get(k, ShaderIds.ElementRibbon, mat =>
            {
                mat.SetColor(ShaderIds.ColorA, core);
                mat.SetColor(ShaderIds.ColorB, edge);
                mat.SetTexture(ShaderIds.NoiseTex, noise != null ? noise : ProceduralTextures.Noise);
                mat.SetFloat(ShaderIds.ScrollSpeed, scroll);
                mat.SetFloat(ShaderIds.FresnelPower, fresnel);
                SetBlend(mat, blend);
            });
            return m;
        }

        /// <summary>Look presets for <see cref="TidalWater"/>.</summary>
        public enum WaterLook
        {
            /// <summary>Main body: deep → cyan bands, edge + crest foam.</summary>
            Body,
            /// <summary>Mostly white foam (crests, breakers, spray sheets).</summary>
            Foam,
            /// <summary>Thin bright cutting arc (slash edges).</summary>
            Edge
        }

        /// <summary>TIDAL BREATH anime water (TidalWaterAnime shader) for water ribbons, serpent bodies and heads.</summary>
        public static Material TidalWater(WaterLook look, bool tube, VfxBlend blend = VfxBlend.AlphaBlend)
        {
            string k = $"tidal_{look}_{tube}_{blend}";
            return Get(k, ShaderIds.TidalWater, mat =>
            {
                mat.SetTexture(ShaderIds.NoiseTex, ProceduralTextures.Caustics != null ? ProceduralTextures.Caustics : ProceduralTextures.Noise);
                mat.SetFloat(ShaderIds.TubeMode, tube ? 1f : 0f);
                switch (look)
                {
                    case WaterLook.Foam:
                        mat.SetColor(ShaderIds.DeepColor, new Color(0.35f, 0.7f, 1.2f));
                        mat.SetColor(ShaderIds.MidColor, new Color(1.1f, 1.5f, 1.9f));
                        mat.SetFloat(ShaderIds.FoamEdge, 0.45f);
                        mat.SetFloat(ShaderIds.CrestFoam, 0.9f);
                        mat.SetFloat(ShaderIds.Emission, 1.15f);
                        mat.SetFloat(ShaderIds.FlowSpeed, 2.4f);
                        break;
                    case WaterLook.Edge:
                        mat.SetColor(ShaderIds.DeepColor, new Color(0.15f, 0.55f, 1.4f));
                        mat.SetColor(ShaderIds.MidColor, new Color(0.9f, 1.8f, 2.6f));
                        mat.SetFloat(ShaderIds.FoamEdge, 0.3f);
                        mat.SetFloat(ShaderIds.CrestFoam, 0.2f);
                        mat.SetFloat(ShaderIds.Emission, 1.4f);
                        mat.SetFloat(ShaderIds.FlowSpeed, 3f);
                        break;
                    default:
                        mat.SetFloat(ShaderIds.FoamEdge, tube ? 0.12f : 0.18f);
                        mat.SetFloat(ShaderIds.CrestFoam, 0.45f);
                        mat.SetFloat(ShaderIds.Emission, 1f);
                        mat.SetFloat(ShaderIds.FlowSpeed, 1.6f);
                        break;
                }
                SetBlend(mat, blend);
            });
        }

        /// <summary>A fresh (uncached) instance for effects that animate material values themselves.</summary>
        public static Material Instance(Material source) => new Material(source) { name = source.name + "_inst" };

        public static Material SwordTrail(string key, Gradient gradient, VfxBlend blend)
        {
            string k = $"trail_{key}_{blend}";
            return Get(k, ShaderIds.SwordTrail, m =>
            {
                m.SetTexture(ShaderIds.GradientTex, ProceduralTextures.FromGradient(k, gradient));
                m.SetTexture(ShaderIds.NoiseTex, ProceduralTextures.Noise);
                SetBlend(m, blend);
            });
        }

        public static Material Ghost(Color color)
        {
            return Get($"ghost_{ColorKey(color)}", ShaderIds.Ghost, m => m.SetColor(ShaderIds.BaseColor, color));
        }

        public static Material Telegraph(Color color)
        {
            return Get($"telegraph_{ColorKey(color)}", ShaderIds.Telegraph, m => m.SetColor(ShaderIds.BaseColor, color));
        }

        public static Material Distortion()
        {
            return Get("distortion", ShaderIds.Distortion, m => m.SetTexture(ShaderIds.NoiseTex, ProceduralTextures.Noise));
        }

        public static Material Decal(Texture texture, Color tint)
        {
            return Get($"decal_{texture.name}_{ColorKey(tint)}", ShaderIds.VfxAlpha, m =>
            {
                m.SetTexture(ShaderIds.MainTex, texture);
                m.SetColor(ShaderIds.TintColor, tint);
                m.SetFloat("_SoftFade", 0f);
                m.renderQueue = (int)RenderQueue.Transparent - 50;
            });
        }

        public static void SetBlend(Material m, VfxBlend blend)
        {
            switch (blend)
            {
                case VfxBlend.AlphaBlend:
                    m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.SrcAlpha);
                    m.SetFloat(ShaderIds.DstBlend, (float)BlendMode.OneMinusSrcAlpha);
                    break;
                case VfxBlend.SoftAdditive:
                    m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.OneMinusDstColor);
                    m.SetFloat(ShaderIds.DstBlend, (float)BlendMode.One);
                    break;
                default:
                    m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.SrcAlpha);
                    m.SetFloat(ShaderIds.DstBlend, (float)BlendMode.One);
                    break;
            }
        }
    }
}
