using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Rendering
{
    /// <summary>
    /// Runtime-generated textures so the prototype needs no imported art: particle sprites, noise,
    /// crack decals, gradients and simple environment detail maps. Everything is cached.
    /// </summary>
    public static class ProceduralTextures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            SpriteCache.Clear();
        }

        private delegate Color PixelFunc(float u, float v);

        private static Texture2D Make(string key, int w, int h, PixelFunc f, TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = true)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips, false)
            {
                name = "BoE_" + key,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    pixels[y * w + x] = f(u, v);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(mips, true);
            Cache[key] = tex;
            return tex;
        }

        /// <summary>Cached procedural texture from a pixel function (u, v in 0..1).</summary>
        public static Texture2D Generate(string key, int width, int height, System.Func<float, float, Color> pixel,
            TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = true) =>
            Make(key, width, height, (u, v) => pixel(u, v), wrap, mips);

        // ---------------------------------------------------------------- noise helpers
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144665;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        /// <summary>Tileable value noise with the given integer period.</summary>
        public static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            int x1 = x0 + 1, y1 = y0 + 1;
            int px0 = ((x0 % period) + period) % period, px1 = ((x1 % period) + period) % period;
            int py0 = ((y0 % period) + period) % period, py1 = ((y1 % period) + period) % period;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);
            float a = Hash(px0, py0, seed), b = Hash(px1, py0, seed), c = Hash(px0, py1, seed), d = Hash(px1, py1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        public static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(u * period, v * period, period, seed + i * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        // ---------------------------------------------------------------- particle sprites
        public static Texture2D SoftCircle => Make("soft_circle", 64, 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);
            return new Color(1f, 1f, 1f, a);
        });

        /// <summary>Bright core with a long falloff (flashes, glows).</summary>
        public static Texture2D Glow => Make("glow", 64, 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            a = Mathf.Pow(a, 2.2f) + Mathf.Pow(Mathf.Clamp01(1f - d * 3f), 2f) * 0.6f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        });

        /// <summary>Horizontal streak (sparks, speed lines, rain of light).</summary>
        public static Texture2D Streak => Make("streak", 64, 16, (u, v) =>
        {
            float along = Mathf.Sin(u * Mathf.PI);
            float across = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f);
            float a = Mathf.Pow(along, 1.5f) * Mathf.Pow(across, 2.5f);
            return new Color(1f, 1f, 1f, a);
        });

        /// <summary>Four-pointed star glint (impacts, parry sparks, stars).</summary>
        public static Texture2D Star => Make("star", 64, 64, (u, v) =>
        {
            float x = Mathf.Abs(u - 0.5f) * 2f, y = Mathf.Abs(v - 0.5f) * 2f;
            float cross = Mathf.Clamp01(1f - x * 8f * (0.15f + y)) + Mathf.Clamp01(1f - y * 8f * (0.15f + x));
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2.2f);
            float a = Mathf.Clamp01(cross * (1f - Mathf.Max(x, y)) + core * core);
            return new Color(1f, 1f, 1f, a);
        });

        public static Texture2D RingSprite => Make("ring", 128, 128, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 9f);
            return new Color(1f, 1f, 1f, a * a);
        });

        /// <summary>Bright band across V (ring meshes: glow in the middle of the ring width).</summary>
        public static Texture2D Band => Make("band", 8, 64, (u, v) =>
        {
            float a = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f);
            return new Color(1f, 1f, 1f, a * a);
        });

        /// <summary>Crescent blade glow: bright cutting edge (V=1) fading inward and toward the tips.</summary>
        public static Texture2D CrescentBand => Make("crescent_band", 64, 64, (u, v) =>
        {
            float edge = Mathf.Pow(v, 2.2f);
            float tips = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.6f);
            float core = Mathf.Clamp01(1f - Mathf.Abs(v - 0.9f) * 8f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(edge * tips + core * tips * 0.6f));
        });

        /// <summary>Teardrop flame tongue pointing up.</summary>
        public static Texture2D Flame => Make("flame", 64, 128, (u, v) =>
        {
            float x = (u - 0.5f) * 2f;
            float width = Mathf.Lerp(0.85f, 0.05f, Mathf.Pow(v, 0.8f));
            float body = Mathf.Clamp01(1f - Mathf.Abs(x) / Mathf.Max(0.01f, width));
            float bottom = Mathf.Clamp01(v * 6f);
            float n = Fbm(u, v * 0.5f, 4, 3, 11);
            float a = Mathf.Clamp01(body * bottom * (0.7f + n * 0.6f));
            a = Mathf.SmoothStep(0.05f, 0.9f, a);
            return new Color(1f, 1f, 1f, a);
        });

        public static Texture2D Smoke => Make("smoke", 64, 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float n = Fbm(u, v, 4, 4, 23);
            float a = Mathf.Clamp01((1f - d) * 1.4f) * Mathf.SmoothStep(0.25f, 0.8f, n);
            return new Color(1f, 1f, 1f, a);
        });

        public static Texture2D Leaf => Make("leaf", 32, 32, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float shape = 1f - (x * x / 0.35f + y * y);
            float vein = Mathf.Abs(x) < 0.06f ? 0.75f : 1f;
            float a = Mathf.Clamp01(shape * 4f);
            return new Color(vein, vein, vein, a);
        });

        public static Texture2D Petal => Make("petal", 32, 32, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.35f) * 2f;
            float shape = 1f - (x * x / (0.25f + Mathf.Max(0f, y) * 0.3f) + y * y * 0.8f);
            float notch = v > 0.9f && Mathf.Abs(x) < 0.12f ? 0f : 1f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(shape * 5f) * notch);
        });

        public static Texture2D Droplet => Make("droplet", 32, 32, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01((1f - d) * 5f);
            float highlight = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.38f, 0.62f)) * 6f);
            float c = 0.75f + highlight * 0.25f;
            return new Color(c, c, c, a);
        });

        /// <summary>Radial ground crack decal.</summary>
        public static Texture2D Crack => Make("crack", 256, 256, (u, v) =>
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float r = p.magnitude;
            float ang = Mathf.Atan2(p.y, p.x);
            float lines = 0f;
            for (int i = 0; i < 9; i++)
            {
                float baseAng = i / 9f * Mathf.PI * 2f + Hash(i, 3, 5) * 0.6f;
                float wobble = (Fbm(r * 0.5f, i * 0.37f, 4, 3, 91) - 0.5f) * 0.9f * r;
                float da = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, (baseAng + wobble) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float width = Mathf.Lerp(0.09f, 0.012f, r) / Mathf.Max(0.15f, r);
                float reach = 0.55f + Hash(i, 9, 1) * 0.45f;
                lines = Mathf.Max(lines, Mathf.Clamp01(1f - Mathf.Abs(da) / width) * Mathf.Clamp01((reach - r) * 6f));
            }
            float center = Mathf.Clamp01(1f - r * 5f);
            float a = Mathf.Clamp01(lines + center);
            return new Color(0.02f, 0.015f, 0.02f, a);
        });

        /// <summary>Scorch / impact dust decal (soft dark splat with noise).</summary>
        public static Texture2D Scorch => Make("scorch", 128, 128, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float n = Fbm(u, v, 4, 4, 51);
            float a = Mathf.Clamp01((1f - d) * 1.6f - n * 0.6f);
            return new Color(0.05f, 0.04f, 0.04f, a * 0.85f);
        });

        public static Texture2D Noise => Make("noise", 128, 128, (u, v) =>
        {
            float n = Fbm(u, v, 4, 5, 7);
            float n2 = Fbm(u, v, 8, 3, 33);
            float n3 = Fbm(u, v, 2, 4, 77);
            return new Color(n, n2, n3, 1f);
        }, TextureWrapMode.Repeat);

        /// <summary>Stretched caustic-like noise used by water ribbons and stream surface.</summary>
        public static Texture2D Caustics => Make("caustics", 128, 128, (u, v) =>
        {
            float n = Fbm(u, v, 4, 4, 101);
            float ridge = 1f - Mathf.Abs(n * 2f - 1f);
            ridge = Mathf.Pow(ridge, 6f);
            float n2 = Fbm(u + 0.3f, v, 8, 3, 202);
            return new Color(ridge, n2, n, 1f);
        }, TextureWrapMode.Repeat);

        public static Texture2D Grid => Make("grid", 256, 256, (u, v) =>
        {
            float gx = Mathf.Min(u * 8f % 1f, 1f - u * 8f % 1f);
            float gy = Mathf.Min(v * 8f % 1f, 1f - v * 8f % 1f);
            float line = Mathf.Clamp01(1f - Mathf.Min(gx, gy) * 30f);
            float major = (Mathf.Min(u % 0.5f, 0.5f - u % 0.5f) < 0.004f || Mathf.Min(v % 0.5f, 0.5f - v % 0.5f) < 0.004f) ? 1f : 0f;
            float n = Fbm(u, v, 8, 3, 5) * 0.08f;
            float c = 0.42f + n - line * 0.08f - major * 0.1f;
            return new Color(c, c, c * 1.04f, 1f);
        }, TextureWrapMode.Repeat);

        public static Texture2D GroundDetail => Make("ground_detail", 256, 256, (u, v) =>
        {
            float n = Fbm(u, v, 8, 5, 13);
            float n2 = Fbm(u, v, 32, 2, 17);
            float c = 0.75f + (n - 0.5f) * 0.35f + (n2 - 0.5f) * 0.12f;
            return new Color(c, c, c, 1f);
        }, TextureWrapMode.Repeat);

        public static Texture2D StoneDetail => Make("stone_detail", 128, 128, (u, v) =>
        {
            float n = Fbm(u, v, 4, 5, 29);
            float cracks = Mathf.Pow(1f - Mathf.Abs(Fbm(u, v, 8, 3, 31) * 2f - 1f), 12f);
            float c = 0.8f + (n - 0.5f) * 0.3f - cracks * 0.35f;
            return new Color(c, c, c, 1f);
        }, TextureWrapMode.Repeat);

        public static Texture2D WoodDetail => Make("wood_detail", 64, 128, (u, v) =>
        {
            float grain = Mathf.Sin((u * 18f + Fbm(u, v, 2, 3, 41) * 6f) * Mathf.PI) * 0.5f + 0.5f;
            float c = 0.78f + grain * 0.18f;
            return new Color(c, c * 0.97f, c * 0.94f, 1f);
        }, TextureWrapMode.Repeat);

        /// <summary>Converts a gradient into a 256x1 lookup texture.</summary>
        public static Texture2D FromGradient(string key, Gradient gradient)
        {
            string k = "grad_" + key;
            if (Cache.TryGetValue(k, out var cached) && cached != null) return cached;
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false, false)
            {
                name = "BoE_" + k,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[256];
            for (int i = 0; i < 256; i++) pixels[i] = gradient.Evaluate(i / 255f);
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            Cache[k] = tex;
            return tex;
        }

        /// <summary>Simple UI sprites (rounded rect / circle) for the procedural HUD.</summary>
        public static Sprite UISprite(string shape)
        {
            string key = "ui_" + shape;
            if (SpriteCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;
            Texture2D tex;
            switch (shape)
            {
                case "circle":
                    tex = Make(key, 128, 128, (u, v) =>
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                        return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 64f));
                    });
                    break;
                case "ring":
                    tex = Make(key, 128, 128, (u, v) =>
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                        return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 64f) * Mathf.Clamp01((d - 0.84f) * 64f));
                    });
                    break;
                case "diamond":
                    tex = Make(key, 64, 64, (u, v) =>
                    {
                        float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                        return new Color(1f, 1f, 1f, Mathf.Clamp01((0.5f - d) * 40f));
                    });
                    break;
                case "brush":
                    // Horizontal ink brush stroke for anime banners.
                    tex = Make(key, 256, 64, (u, v) =>
                    {
                        float n = Fbm(u, v, 8, 4, 61);
                        float thickness = 0.42f * Mathf.Sin(Mathf.Clamp01(u * 1.05f) * Mathf.PI) + 0.08f;
                        float body = Mathf.Clamp01((thickness - Mathf.Abs(v - 0.5f)) * 14f);
                        float dry = Mathf.SmoothStep(0.2f, 0.45f, n + (1f - u) * 0.25f);
                        return new Color(1f, 1f, 1f, body * dry);
                    });
                    break;
                case "fade_right":
                    // Opaque on the left, transparent on the right (menu backdrops).
                    tex = Make(key, 128, 8, (u, v) => new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.3f, 1f, u)));
                    break;
                default:
                    tex = Make(key, 16, 16, (u, v) => Color.white);
                    break;
            }
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            SpriteCache[key] = sprite;
            return sprite;
        }
    }
}
