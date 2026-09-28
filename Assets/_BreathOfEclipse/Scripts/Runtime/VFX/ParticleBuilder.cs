using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// Fluent helper to configure Particle Systems from code (the prototype builds every effect procedurally).
    /// Systems are created with play-on-awake off, world simulation and hierarchy scaling.
    /// </summary>
    public sealed class ParticleBuilder
    {
        public ParticleSystem System { get; }
        public ParticleSystemRenderer Renderer { get; }

        private ParticleBuilder(ParticleSystem ps)
        {
            System = ps;
            Renderer = ps.GetComponent<ParticleSystemRenderer>();
        }

        public static ParticleBuilder Create(string name, Transform parent, Material material, Vector3 localPosition = default, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.layer = Core.Layers.VFX;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 600;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f;
            main.startSize = 0.3f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0f;
            r.maxParticleSize = 3f;
            return new ParticleBuilder(ps);
        }

        public ParticleBuilder Burst(int count, float time = 0f, int cycles = 1, float interval = 0.05f)
        {
            var e = System.emission;
            e.enabled = true;
            e.SetBursts(new[] { new ParticleSystem.Burst(time, (short)VFXQuality.Count(count), (short)VFXQuality.Count(count), cycles, interval) });
            return this;
        }

        public ParticleBuilder Rate(float perSecond, float perMeter = 0f)
        {
            var e = System.emission;
            e.enabled = true;
            e.rateOverTime = perSecond * VFXQuality.Density;
            e.rateOverDistance = perMeter * VFXQuality.Density;
            return this;
        }

        public ParticleBuilder Duration(float seconds, bool loop = false)
        {
            var m = System.main;
            m.duration = Mathf.Max(0.05f, seconds);
            m.loop = loop;
            return this;
        }

        public ParticleBuilder Delay(float seconds)
        {
            var m = System.main;
            m.startDelay = seconds;
            return this;
        }

        public ParticleBuilder Life(float min, float max)
        {
            var m = System.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(min, max);
            return this;
        }

        public ParticleBuilder Speed(float min, float max)
        {
            var m = System.main;
            m.startSpeed = new ParticleSystem.MinMaxCurve(min, max);
            return this;
        }

        public ParticleBuilder Size(float min, float max)
        {
            var m = System.main;
            m.startSize = new ParticleSystem.MinMaxCurve(min, max);
            return this;
        }

        public ParticleBuilder Size3D(Vector3 min, Vector3 max)
        {
            var m = System.main;
            m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(min.x, max.x);
            m.startSizeY = new ParticleSystem.MinMaxCurve(min.y, max.y);
            m.startSizeZ = new ParticleSystem.MinMaxCurve(min.z, max.z);
            return this;
        }

        public ParticleBuilder Color(Color a, Color? b = null)
        {
            var m = System.main;
            m.startColor = b.HasValue ? new ParticleSystem.MinMaxGradient(a, b.Value) : new ParticleSystem.MinMaxGradient(a);
            return this;
        }

        public ParticleBuilder Rotation(float minDeg, float maxDeg)
        {
            var m = System.main;
            m.startRotation = new ParticleSystem.MinMaxCurve(minDeg * Mathf.Deg2Rad, maxDeg * Mathf.Deg2Rad);
            return this;
        }

        public ParticleBuilder Spin(float minDegPerSec, float maxDegPerSec)
        {
            var rot = System.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(minDegPerSec * Mathf.Deg2Rad, maxDegPerSec * Mathf.Deg2Rad);
            return this;
        }

        public ParticleBuilder Gravity(float modifier)
        {
            var m = System.main;
            m.gravityModifier = modifier;
            return this;
        }

        /// <summary>Auto-plays when its GameObject activates (parts toggled at runtime, e.g. serpent heads).</summary>
        public ParticleBuilder PlayOnAwake()
        {
            var m = System.main;
            m.playOnAwake = true;
            return this;
        }

        public ParticleBuilder Local()
        {
            var m = System.main;
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            return this;
        }

        public ParticleBuilder MaxParticles(int max)
        {
            var m = System.main;
            m.maxParticles = max;
            return this;
        }

        public ParticleBuilder Shape(ParticleSystemShapeType type, float radius, float angle = 25f, float arc = 360f,
            Vector3? scale = null, Vector3? rotation = null, Vector3? position = null, float radiusThickness = 1f)
        {
            var s = System.shape;
            s.enabled = true;
            s.shapeType = type;
            s.radius = radius;
            s.angle = angle;
            s.arc = arc;
            s.radiusThickness = radiusThickness;
            if (scale.HasValue) s.scale = scale.Value;
            if (rotation.HasValue) s.rotation = rotation.Value;
            if (position.HasValue) s.position = position.Value;
            return this;
        }

        public ParticleBuilder NoShape()
        {
            var s = System.shape;
            s.enabled = false;
            return this;
        }

        /// <summary>Color over lifetime: fades in quickly, holds, fades out.</summary>
        public ParticleBuilder Fade(float fadeIn = 0.05f, float holdUntil = 0.5f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(UnityEngine.Color.white, 0f), new GradientColorKey(UnityEngine.Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, Mathf.Clamp(fadeIn, 0.001f, 0.9f)), new GradientAlphaKey(1f, Mathf.Clamp(holdUntil, fadeIn + 0.001f, 0.99f)), new GradientAlphaKey(0f, 1f) });
            var col = System.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
            return this;
        }

        public ParticleBuilder ColorOverLife(Gradient gradient)
        {
            var col = System.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(gradient);
            return this;
        }

        public ParticleBuilder SizeOverLife(AnimationCurve curve)
        {
            var sol = System.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
            return this;
        }

        public ParticleBuilder Grow(float from = 0.3f, float to = 1f) => SizeOverLife(AnimationCurve.EaseInOut(0f, from, 1f, to));

        public ParticleBuilder Shrink(float from = 1f, float to = 0f) => SizeOverLife(AnimationCurve.EaseInOut(0f, from, 1f, to));

        public ParticleBuilder Noise(float strength, float frequency = 0.8f, float scroll = 0.5f)
        {
            var n = System.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = frequency;
            n.scrollSpeed = scroll;
            n.quality = ParticleSystemNoiseQuality.Medium;
            return this;
        }

        public ParticleBuilder Drag(float dampen)
        {
            var l = System.limitVelocityOverLifetime;
            l.enabled = true;
            l.limit = 0f;
            l.dampen = dampen;
            return this;
        }

        public ParticleBuilder Velocity(Vector3 linear, float orbitalY = 0f, float radial = 0f, bool local = true)
        {
            var v = System.velocityOverLifetime;
            v.enabled = true;
            v.space = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(linear.x);
            v.y = new ParticleSystem.MinMaxCurve(linear.y);
            v.z = new ParticleSystem.MinMaxCurve(linear.z);
            v.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            v.orbitalY = new ParticleSystem.MinMaxCurve(orbitalY);
            v.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            v.radial = new ParticleSystem.MinMaxCurve(radial);
            return this;
        }

        public ParticleBuilder Stretch(float lengthScale = 2f, float velocityScale = 0.05f)
        {
            Renderer.renderMode = ParticleSystemRenderMode.Stretch;
            Renderer.lengthScale = lengthScale;
            Renderer.velocityScale = velocityScale;
            return this;
        }

        public ParticleBuilder Horizontal()
        {
            Renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return this;
        }

        public ParticleBuilder Sorting(float fudge)
        {
            Renderer.sortingFudge = fudge;
            return this;
        }

        public ParticleBuilder MaxScreenSize(float size)
        {
            Renderer.maxParticleSize = size;
            return this;
        }

        public ParticleSystem Done() => System;
    }
}
