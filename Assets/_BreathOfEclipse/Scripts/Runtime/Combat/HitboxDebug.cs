using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Records hit queries (sweeps, spheres, capsules) while "Show Hitboxes" is enabled in the debug menu so
    /// <see cref="HitboxDebugRenderer"/> can draw them for a short time. Zero cost when disabled.
    /// </summary>
    public static class HitboxDebug
    {
        public enum ShapeKind { Sphere, Capsule, Segment }

        public struct Shape
        {
            public ShapeKind Kind;
            public Vector3 A;
            public Vector3 B;
            public float Radius;
            public float Time;
            public Color Color;
        }

        public const float Lifetime = 0.35f;
        public static readonly Color QueryColor = new Color(1f, 0.25f, 0.2f, 1f);
        public static readonly Color SweepColor = new Color(1f, 0.8f, 0.2f, 1f);

        private static readonly List<Shape> Shapes = new List<Shape>(256);
        public static IReadOnlyList<Shape> Recent => Shapes;
        public static bool Enabled { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Enabled = false;
            Shapes.Clear();
        }

        public static void Sphere(Vector3 center, float radius) => Add(ShapeKind.Sphere, center, center, radius, QueryColor);
        public static void Capsule(Vector3 a, Vector3 b, float radius) => Add(ShapeKind.Capsule, a, b, radius, QueryColor);
        public static void Segment(Vector3 a, Vector3 b, Color color) => Add(ShapeKind.Segment, a, b, 0f, color);

        private static void Add(ShapeKind kind, Vector3 a, Vector3 b, float radius, Color color)
        {
            if (!Enabled) return;
            if (Shapes.Count >= 512) Shapes.RemoveAt(0);
            Shapes.Add(new Shape { Kind = kind, A = a, B = b, Radius = radius, Time = UnityEngine.Time.unscaledTime, Color = color });
        }

        public static void Prune()
        {
            float now = UnityEngine.Time.unscaledTime;
            Shapes.RemoveAll(s => now - s.Time > Lifetime);
        }
    }
}
