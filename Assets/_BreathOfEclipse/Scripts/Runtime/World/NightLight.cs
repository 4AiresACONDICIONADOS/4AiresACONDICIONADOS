using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// A light that follows the time of day: lanterns, windows, campfires, the forge, cursed red lamps. The
    /// <see cref="WorldTimeSystem"/> sets <see cref="Level"/> (0 day … 1 night); each light fades its point light and
    /// emissive parts, flickers if it is a flame, and switches its point light off far from the camera.
    /// </summary>
    public sealed class NightLight : MonoBehaviour
    {
        public static readonly List<NightLight> All = new List<NightLight>();
        /// <summary>0 in daylight … 1 at night (smoothed by the time system).</summary>
        public static float Level = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            Level = 1f;
        }

        public Light Light;
        public float Intensity = 1.6f;
        public float Range = 7f;
        public Renderer[] Glow = new Renderer[0];
        [ColorUsage(true, true)] public Color GlowColor = new Color(3f, 1.8f, 0.7f);
        /// <summary>Lit regardless of the time (forge while working, cursed lamps).</summary>
        public bool AlwaysOn;
        /// <summary>Extra control (shop closed, campfire out): multiplies the level.</summary>
        public float Enabled = 1f;
        public float Flicker;
        public float CullDistance = 70f;

        private MaterialPropertyBlock _mpb;
        private float _appliedGlow = -1f;
        private float _seed;

        public static NightLight Create(Transform parent, Vector3 worldPos, Color lightColor, float intensity, float range, Color glow, params Renderer[] glowRenderers)
        {
            var go = new GameObject("NightLight");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var nl = go.AddComponent<NightLight>();
            nl.Intensity = intensity;
            nl.Range = range;
            nl.GlowColor = glow;
            nl.Glow = glowRenderers ?? new Renderer[0];
            if (intensity > 0f)
            {
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = lightColor;
                l.range = range;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                nl.Light = l;
            }
            return nl;
        }

        private void OnEnable()
        {
            All.Add(this);
            _appliedGlow = -1f;
            _seed = Random.value * 10f;
        }

        private void OnDisable() => All.Remove(this);

        private void Update()
        {
            float level = (AlwaysOn ? 1f : Level) * Enabled;
            if (Flicker > 0f && level > 0f)
                level *= 1f - Flicker * (0.5f + 0.5f * Mathf.PerlinNoise(Time.time * 7f + _seed, _seed));
            if (Light != null)
            {
                var cam = Camera.main;
                bool near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < CullDistance * CullDistance;
                bool on = level > 0.02f && near;
                if (Light.enabled != on) Light.enabled = on;
                if (on) Light.intensity = Intensity * level;
            }
            if (Glow.Length > 0 && Mathf.Abs(level - _appliedGlow) > 0.01f)
            {
                _appliedGlow = level;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                foreach (var r in Glow)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(ShaderIds.EmissionColor, GlowColor * level);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }
    }

    /// <summary>Chimney / kitchen smoke that only rises at meal times (morning and evening).</summary>
    public sealed class HearthSmoke : MonoBehaviour
    {
        public ParticleSystem Smoke;
        private bool _on = true;

        private void Update()
        {
            if (Smoke == null || WorldTimeSystem.Instance == null) return;
            float h = WorldTimeSystem.Instance.Clock.Hour;
            bool on = (h > 5.5f && h < 9f) || (h > 16.5f && h < 21f);
            if (on == _on) return;
            _on = on;
            var em = Smoke.emission;
            em.enabled = on;
        }
    }
}
