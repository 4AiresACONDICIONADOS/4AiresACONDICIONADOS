using System;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>Parametric curves for ribbon/tube effects.</summary>
    public static class PathUtil
    {
        public static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float v = 1f - u;
            return v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
        }

        public static Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float v = 1f - u;
            Vector3 t = 3f * v * v * (p1 - p0) + 6f * v * u * (p2 - p1) + 3f * u * u * (p3 - p2);
            return t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.forward;
        }

        /// <summary>A cubic Bezier with a helical coil wrapped around it (serpents, dragons, drills).</summary>
        public static Func<float, Vector3> CoiledBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float coilTurns, float coilRadius, float coilTaper = 0.6f, float phase = 0f)
        {
            return u =>
            {
                Vector3 p = Bezier(p0, p1, p2, p3, u);
                if (coilRadius <= 0f) return p;
                Vector3 t = BezierTangent(p0, p1, p2, p3, u);
                Vector3 n = Vector3.Cross(t, Vector3.up);
                if (n.sqrMagnitude < 1e-4f) n = Vector3.Cross(t, Vector3.right);
                n.Normalize();
                Vector3 b = Vector3.Cross(t, n);
                float a = u * coilTurns * Mathf.PI * 2f + phase;
                float r = coilRadius * (1f - u * coilTaper);
                return p + (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * r;
            };
        }

        /// <summary>Horizontal circle (or arc) at a height, optionally rising.</summary>
        public static Func<float, Vector3> Circle(float radius, float height, float startDeg = 0f, float sweepDeg = 360f, float rise = 0f, float waveAmp = 0f, float waves = 0f)
        {
            return u =>
            {
                float a = (startDeg + sweepDeg * u) * Mathf.Deg2Rad;
                float y = height + rise * u + Mathf.Sin(u * waves * Mathf.PI * 2f) * waveAmp;
                return new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);
            };
        }

        /// <summary>Spiral along +Z (drills), radius shrinking toward the tip.</summary>
        public static Func<float, Vector3> ForwardSpiral(float length, float radius, float turns, float phase, float height = 1.1f)
        {
            return u =>
            {
                float a = u * turns * Mathf.PI * 2f + phase;
                float r = radius * (1f - u * 0.7f);
                return new Vector3(Mathf.Cos(a) * r, height + Mathf.Sin(a) * r, u * length);
            };
        }

        /// <summary>Vertical helix (tornados, phoenix ascent).</summary>
        public static Func<float, Vector3> Helix(float height, float radiusBottom, float radiusTop, float turns, float phase)
        {
            return u =>
            {
                float a = u * turns * Mathf.PI * 2f + phase;
                float r = Mathf.Lerp(radiusBottom, radiusTop, u);
                return new Vector3(Mathf.Cos(a) * r, u * height, Mathf.Sin(a) * r);
            };
        }

        /// <summary>Vertical arc in front of the character (overhead / rising slashes).</summary>
        public static Func<float, Vector3> VerticalArc(float radius, float centerHeight, float forward, float startDeg, float endDeg, float side = 0f)
        {
            return u =>
            {
                float a = Mathf.Lerp(startDeg, endDeg, u) * Mathf.Deg2Rad;
                return new Vector3(side, centerHeight + Mathf.Sin(a) * radius, forward + Mathf.Cos(a) * radius);
            };
        }
    }
}
