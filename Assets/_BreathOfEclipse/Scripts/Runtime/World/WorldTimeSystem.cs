using System;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.World
{
    public enum WeatherState
    {
        Clear = 0,
        LightFog = 1
    }

    /// <summary>
    /// The world clock and everything that follows it, gradually: sun and moon (direction, colour, intensity, which
    /// one casts shadows), sky gradient, stars, day clouds, ambient light, fog, colour grading, lanterns and windows
    /// (<see cref="NightLight.Level"/>). Weather is limited to clear / light morning fog (interface ready for more).
    /// </summary>
    public sealed class WorldTimeSystem : MonoBehaviour
    {
        public static WorldTimeSystem Instance { get; private set; }

        public WorldClock Clock { get; } = new WorldClock(1, 7f);
        public WeatherState Weather { get; private set; }
        /// <summary>0..1 extra danger mood (Ash Hollow, exceptional presence): redder, denser fog.</summary>
        public float DangerMood { get; set; }
        public float Daylight { get; private set; }
        public Light Sun { get; private set; }
        public Light Moon { get; private set; }
        public Light Rim { get; private set; }

        public event Action<DayPhase, DayPhase> PhaseChanged;
        public event Action<int> NewDay;
        /// <summary>The clock jumped (rest, debug set time): absolute hours before and after. Systems snap to the new time.</summary>
        public event Action<double, double> TimeJumped;

        private Material _sky;
        private float _danger;
        private float _nextAmbient;

        private struct Key
        {
            public float Hour;
            public Color Top, Horizon, Fog, AmbSky, AmbEq, AmbGround, Sun, Filter;
            public float FogDensity, SunIntensity, Temperature, Exposure;
        }

        private static Key K(float h, int top, int hor, int fog, int sky, int eq, int ground, int sun, int filter, float density, float sunI, float temp, float exposure) => new Key
        {
            Hour = h, Top = C(top), Horizon = C(hor), Fog = C(fog), AmbSky = C(sky), AmbEq = C(eq), AmbGround = C(ground), Sun = C(sun), Filter = C(filter),
            FogDensity = density, SunIntensity = sunI, Temperature = temp, Exposure = exposure
        };

        private static Color C(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        // Night is the established moonlit look of the game; day, dawn and dusk are new.
        private static readonly Key[] Keys =
        {
            K(0f,    0x050817, 0x1F2147, 0x12172B, 0x29335C, 0x1A1F38, 0x0D0D14, 0x000000, 0xF0F7FF, 0.015f, 0f, -10f, 0.15f),
            K(4.6f,  0x070A1C, 0x24264D, 0x151A30, 0x2B3560, 0x1C2139, 0x0D0D14, 0x000000, 0xF0F7FF, 0.016f, 0f, -10f, 0.15f),
            K(5.6f,  0x2B3466, 0xC98B8A, 0x8A7488, 0x5A5C80, 0x6A5866, 0x26222A, 0xFF9C80, 0xFFF2F0, 0.012f, 0.45f, 2f, 0.18f),
            K(6.6f,  0x3E68B0, 0xF2C49A, 0xC9B4A6, 0x8A93B0, 0x9A8C84, 0x3A342E, 0xFFD0A0, 0xFFF8EE, 0.008f, 1.0f, 6f, 0.22f),
            K(8.5f,  0x3A79D0, 0xBFD9F0, 0xA9C1D9, 0x9AB0D0, 0x8C9590, 0x433E36, 0xFFF4E4, 0xFAFCFF, 0.0042f, 1.45f, 0f, 0.25f),
            K(12f,   0x3478D8, 0xB8D6F2, 0xA5C0DC, 0xA2B8D8, 0x929A94, 0x47423A, 0xFFF8EE, 0xFAFCFF, 0.0036f, 1.6f, 0f, 0.25f),
            K(16f,   0x3A70C4, 0xC9D8EC, 0xB0C2D8, 0x9EAED0, 0x958F86, 0x45403A, 0xFFE9CC, 0xFFFAF2, 0.0042f, 1.4f, 4f, 0.24f),
            K(17.6f, 0x3F4F92, 0xF29A62, 0xC9876E, 0x8A7890, 0x9A6E5E, 0x3A2C26, 0xFFA060, 0xFFEEE0, 0.0075f, 1.0f, 14f, 0.22f),
            K(18.7f, 0x241F5A, 0xC9585A, 0x7A4A62, 0x5A4C78, 0x6A4456, 0x221A20, 0xFF6A48, 0xFFE4E4, 0.011f, 0.35f, 8f, 0.18f),
            K(19.7f, 0x0B0E2A, 0x2E2A5A, 0x1A1D36, 0x2D3560, 0x1E2240, 0x0E0E16, 0x000000, 0xF0F4FF, 0.014f, 0f, -8f, 0.15f),
            K(24f,   0x050817, 0x1F2147, 0x12172B, 0x29335C, 0x1A1F38, 0x0D0D14, 0x000000, 0xF0F7FF, 0.015f, 0f, -10f, 0.15f),
        };

        public static WorldTimeSystem Create(Transform parent, int day, float hour)
        {
            var go = new GameObject("WorldTimeSystem");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<WorldTimeSystem>();
            t.Clock.Set(day, hour);
            t.Build(parent);
            return t;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Clock.PhaseChanged += (a, b) => PhaseChanged?.Invoke(a, b);
            Clock.NewDay += d =>
            {
                RollWeather(d);
                NewDay?.Invoke(d);
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            NightLight.Level = 1f;
        }

        private void Build(Transform parent)
        {
            Sun = NewLight(parent, "Sun", new Color(1f, 0.96f, 0.9f), true);
            Moon = NewLight(parent, "Moonlight", EnvironmentKit.MoonColor, true);
            // Cool anime rim/fill opposite the key light (the established night look keeps its violet rim).
            Rim = NewLight(parent, "RimLight", new Color(0.45f, 0.35f, 0.8f), false);
            var sky = EnvironmentKit.SkyDome(parent, new Vector3(0.2f, 0.42f, 1f), 0.085f);
            _sky = sky.GetComponent<MeshRenderer>().sharedMaterial;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.skybox = null;
            RollWeather(Clock.Day);
            Apply(true);
        }

        private static Light NewLight(Transform parent, string name, Color color, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            return l;
        }

        /// <summary>Morning fog on some days (deterministic per day).</summary>
        private void RollWeather(int day)
        {
            Weather = WorldEventRules.Roll("weather", day, 1) < 0.35f ? WeatherState.LightFog : WeatherState.Clear;
        }

        public void SetWeather(WeatherState w) => Weather = w;

        // ------------------------------------------------------------------ control

        /// <summary>Jumps forward to the next <paramref name="hour"/> (debug: set morning / noon / sunset / night).</summary>
        public void SetHour(float hour)
        {
            double from = Clock.TotalHours;
            Clock.SetHourForward(hour);
            Apply(true);
            TimeJumped?.Invoke(from, Clock.TotalHours);
        }

        /// <summary>Skips <paramref name="hours"/> (rest).</summary>
        public void Advance(double hours)
        {
            if (hours <= 0) return;
            double from = Clock.TotalHours;
            Clock.AdvanceHours(hours);
            Apply(true);
            TimeJumped?.Invoke(from, Clock.TotalHours);
        }

        public void SetPaused(bool paused) => Clock.Paused = paused;

        public void SetMultiplier(float m) => Clock.Multiplier = Mathf.Max(0f, m);

        private void Update()
        {
            Clock.Tick(Time.deltaTime);
            Apply(false);
        }

        // ------------------------------------------------------------------ visuals

        private void Apply(bool immediate)
        {
            float hour = Clock.Hour;
            Daylight = WorldClock.Daylight(hour);
            var k = Evaluate(hour);
            _danger = immediate ? DangerMood : Mathf.MoveTowards(_danger, DangerMood, Time.deltaTime * 0.3f);

            // Sun arc: rises in the east (+X) at ~6:00, high in the south at noon, sets in the west at ~18:30.
            float sunAngle = (hour - 6f) / 12.5f * Mathf.PI;
            Vector3 toSun = new Vector3(Mathf.Cos(sunAngle) * 0.85f, Mathf.Sin(sunAngle) * 0.9f, -0.38f).normalized;
            // The big anime moon swings across the night sky, low and huge.
            float moonAngle = Mathf.Repeat(hour - 18.5f, 24f) / 12f * Mathf.PI;
            Vector3 toMoon = new Vector3(-Mathf.Cos(moonAngle) * 0.6f, 0.28f + Mathf.Sin(Mathf.Clamp(moonAngle, 0f, Mathf.PI)) * 0.3f, 0.8f).normalized;

            float sunI = k.SunIntensity * Mathf.Clamp01(toSun.y * 4f + 0.3f);
            float moonI = 1.6f * (1f - Mathf.SmoothStep(0f, 0.6f, Daylight));
            Sun.transform.rotation = Quaternion.LookRotation(-toSun);
            Moon.transform.rotation = Quaternion.LookRotation(-toMoon);
            Sun.color = k.Sun;
            Sun.intensity = sunI;
            Moon.intensity = moonI;
            Sun.enabled = sunI > 0.01f;
            Moon.enabled = moonI > 0.01f;
            bool sunMain = sunI >= moonI;
            Sun.shadows = sunMain ? LightShadows.Soft : LightShadows.None;
            Moon.shadows = sunMain ? LightShadows.None : LightShadows.Soft;
            RenderSettings.sun = sunMain ? Sun : Moon;
            Vector3 key = sunMain ? toSun : toMoon;
            Rim.transform.rotation = Quaternion.LookRotation(new Vector3(key.x, -0.3f, key.z).normalized);
            float night = 1f - Mathf.SmoothStep(0.1f, 0.6f, Daylight);
            Rim.color = Color.Lerp(new Color(0.75f, 0.82f, 1f), new Color(0.45f, 0.35f, 0.8f), night);
            Rim.intensity = Mathf.Lerp(0.16f, 0.35f, night);

            // Danger mood: crimson, thicker air (Ash Hollow, exceptional presence).
            Color dangerFog = new Color(0.22f, 0.06f, 0.1f);
            Color fog = Color.Lerp(k.Fog, dangerFog, _danger * 0.7f);
            float density = k.FogDensity * (Weather == WeatherState.LightFog && hour > 4.5f && hour < 10f ? 2.6f : 1f) * (1f + _danger * 1.2f);
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = density;
            RenderSettings.ambientSkyColor = Color.Lerp(k.AmbSky, new Color(0.35f, 0.12f, 0.18f), _danger * 0.4f);
            RenderSettings.ambientEquatorColor = k.AmbEq;
            RenderSettings.ambientGroundColor = k.AmbGround;
            if (immediate || Time.unscaledTime >= _nextAmbient)
            {
                _nextAmbient = Time.unscaledTime + 0.25f;
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight(k.AmbEq);
                sh.AddDirectionalLight(Vector3.up, (RenderSettings.ambientSkyColor - k.AmbEq) * 0.9f, 1f);
                sh.AddDirectionalLight(Vector3.down, (k.AmbGround - k.AmbEq) * 0.6f, 1f);
                RenderSettings.ambientProbe = sh;
            }

            if (_sky != null)
            {
                _sky.SetColor("_TopColor", Color.Lerp(k.Top, new Color(0.1f, 0.02f, 0.05f), _danger * 0.5f));
                _sky.SetColor("_HorizonColor", Color.Lerp(k.Horizon, new Color(0.45f, 0.1f, 0.15f), _danger * 0.5f));
                _sky.SetVector("_MoonDir", toMoon);
                _sky.SetVector("_SunDir", toSun);
                _sky.SetColor("_SunColor", k.Sun * 1.6f * Mathf.Clamp01(toSun.y * 3f + 0.4f));
                _sky.SetFloat("_StarVisibility", 1f - Mathf.SmoothStep(0f, 0.35f, Daylight));
                _sky.SetFloat("_MoonVisibility", 1f - Mathf.SmoothStep(0.2f, 0.85f, Daylight) * 0.85f);
                _sky.SetFloat("_CloudDay", Mathf.SmoothStep(0.1f, 0.7f, Daylight));
                _sky.SetColor("_CloudColor", Color.Lerp(Color.white, k.Horizon, 0.35f));
            }
            Shader.SetGlobalVector(ShaderIds.GlobalMoonDir, sunMain ? toSun : toMoon);

            var post = PostProcessController.Instance;
            if (post != null)
            {
                post.baseTemperature = k.Temperature + _danger * 8f;
                post.colorFilter = Color.Lerp(k.Filter, new Color(1f, 0.85f, 0.88f), _danger * 0.5f);
                post.baseExposure = k.Exposure;
            }
            NightLight.Level = 1f - Mathf.SmoothStep(0.15f, 0.65f, Daylight);
        }

        private static Key Evaluate(float hour)
        {
            for (int i = 1; i < Keys.Length; i++)
            {
                if (hour > Keys[i].Hour) continue;
                var a = Keys[i - 1];
                var b = Keys[i];
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.Hour, b.Hour, hour));
                return new Key
                {
                    Hour = hour,
                    Top = Color.Lerp(a.Top, b.Top, t),
                    Horizon = Color.Lerp(a.Horizon, b.Horizon, t),
                    Fog = Color.Lerp(a.Fog, b.Fog, t),
                    AmbSky = Color.Lerp(a.AmbSky, b.AmbSky, t),
                    AmbEq = Color.Lerp(a.AmbEq, b.AmbEq, t),
                    AmbGround = Color.Lerp(a.AmbGround, b.AmbGround, t),
                    Sun = Color.Lerp(a.Sun, b.Sun, t),
                    Filter = Color.Lerp(a.Filter, b.Filter, t),
                    FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t),
                    SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
                    Temperature = Mathf.Lerp(a.Temperature, b.Temperature, t),
                    Exposure = Mathf.Lerp(a.Exposure, b.Exposure, t)
                };
            }
            return Keys[Keys.Length - 1];
        }

        public string Describe() => $"Day {Clock.Day} · {WorldClock.Format(Clock.Hour)} · {Clock.Phase}{(Clock.Paused ? " (paused)" : Clock.Multiplier != 1f ? $" ×{Clock.Multiplier:0}" : "")}";
    }
}
