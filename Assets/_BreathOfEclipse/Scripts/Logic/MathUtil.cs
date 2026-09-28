using System;

namespace BreathOfEclipse.Core
{
    /// <summary>Engine independent math helpers so pure logic can be unit tested outside Unity.</summary>
    public static class MathUtil
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Max(float a, float b) => a > b ? a : b;

        public static float Min(float a, float b) => a < b ? a : b;

        public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        public static bool Approximately(float a, float b, float epsilon = 0.0001f) => Math.Abs(a - b) <= epsilon;
    }
}
