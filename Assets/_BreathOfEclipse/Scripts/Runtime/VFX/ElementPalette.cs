using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>HDR colors for one element. Recipes combine these with different shapes per element.</summary>
    public struct ElementPalette
    {
        public Color Core;
        public Color Edge;
        public Color Bright;
        public Color Dark;
        public Color Accent;

        public static ElementPalette Get(Element element)
        {
            switch (element)
            {
                case Element.Water:
                    return new ElementPalette
                    {
                        Core = new Color(0.35f, 0.85f, 2.2f),
                        Edge = new Color(0.05f, 0.35f, 1.1f),
                        Bright = new Color(2.2f, 2.6f, 3.0f),
                        Dark = new Color(0.02f, 0.12f, 0.4f),
                        Accent = new Color(0.4f, 1.8f, 2.4f)
                    };
                case Element.Fire:
                    return new ElementPalette
                    {
                        Core = new Color(3.6f, 1.35f, 0.3f),
                        Edge = new Color(1.8f, 0.25f, 0.04f),
                        Bright = new Color(4.5f, 3.0f, 1.2f),
                        Dark = new Color(0.25f, 0.03f, 0.01f),
                        Accent = new Color(4f, 0.8f, 0.1f)
                    };
                case Element.Thunder:
                    return new ElementPalette
                    {
                        Core = new Color(4.2f, 3.6f, 1.2f),
                        Edge = new Color(2.2f, 1.4f, 0.2f),
                        Bright = new Color(6f, 6f, 4.5f),
                        Dark = new Color(0.25f, 0.18f, 0.02f),
                        Accent = new Color(0.9f, 1.4f, 4f)
                    };
                case Element.Wind:
                    return new ElementPalette
                    {
                        Core = new Color(1.3f, 2.6f, 2.0f),
                        Edge = new Color(0.3f, 1.0f, 0.75f),
                        Bright = new Color(2.6f, 3f, 2.8f),
                        Dark = new Color(0.05f, 0.2f, 0.15f),
                        Accent = new Color(0.7f, 2.2f, 0.9f)
                    };
                case Element.Moon:
                    return new ElementPalette
                    {
                        Core = new Color(1.6f, 1.1f, 3.8f),
                        Edge = new Color(0.5f, 0.28f, 1.6f),
                        Bright = new Color(3.2f, 3.1f, 4f),
                        Dark = new Color(0.08f, 0.03f, 0.25f),
                        Accent = new Color(0.5f, 0.9f, 3f)
                    };
                case Element.Dark:
                    return new ElementPalette
                    {
                        Core = new Color(3f, 0.25f, 0.7f),
                        Edge = new Color(0.8f, 0.03f, 0.2f),
                        Bright = new Color(4f, 1f, 1.6f),
                        Dark = new Color(0.1f, 0.0f, 0.05f),
                        Accent = new Color(1.2f, 0.1f, 2.4f)
                    };
                default:
                    return new ElementPalette
                    {
                        Core = new Color(1.6f, 1.8f, 2.4f),
                        Edge = new Color(0.5f, 0.6f, 1f),
                        Bright = new Color(3f, 3f, 3.2f),
                        Dark = new Color(0.05f, 0.05f, 0.1f),
                        Accent = new Color(1f, 1.3f, 2.4f)
                    };
            }
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>LDR display color (for UI).</summary>
        public static Color Display(Element element)
        {
            var c = Get(element).Core;
            float m = Mathf.Max(1f, c.maxColorComponent);
            return new Color(c.r / m, c.g / m, c.b / m, 1f);
        }
    }
}
