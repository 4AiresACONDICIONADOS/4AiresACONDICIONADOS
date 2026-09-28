using System;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    public enum RigWeapon
    {
        None = 0,
        Katana = 1,
        Claws = 2,
        Kanabo = 3
    }

    public enum RigDecoration
    {
        Hero = 0,
        Demon = 1,
        Oni = 2,
        Dummy = 3
    }

    /// <summary>
    /// Proportions and palette of a procedural mannequin. All lengths are for a 1.8 m reference character;
    /// <see cref="scale"/> scales the whole visual (bosses).
    /// </summary>
    [Serializable]
    public sealed class RigProfile
    {
        public string name = "Hero";
        public float scale = 1f;
        public RigDecoration decoration = RigDecoration.Hero;
        public RigWeapon weapon = RigWeapon.Katana;

        [Header("Proportions")]
        public float hipHeight = 0.95f;
        public float spineLength = 0.24f;
        public float chestLength = 0.26f;
        public float neckLength = 0.08f;
        public float headSize = 0.25f;
        public float shoulderWidth = 0.19f;
        public float shoulderHeight = 0.2f;
        public float upperArm = 0.29f;
        public float lowerArm = 0.27f;
        public float hipWidth = 0.1f;
        public float thigh = 0.44f;
        public float shin = 0.44f;
        public float armThickness = 0.1f;
        public float legThickness = 0.13f;
        public float torsoWidth = 0.36f;
        public float torsoDepth = 0.22f;

        [Header("Palette")]
        public Color skin = new Color(0.95f, 0.8f, 0.7f);
        public Color primary = new Color(0.1f, 0.12f, 0.22f);
        public Color secondary = new Color(0.9f, 0.9f, 0.92f);
        public Color accent = new Color(0.2f, 0.6f, 1f);
        public Color hair = new Color(0.07f, 0.06f, 0.1f);
        [ColorUsage(true, true)] public Color eyes = Color.black;
        public float outline = 1.6f;

        public static RigProfile Hero()
        {
            return new RigProfile
            {
                name = "Hero",
                decoration = RigDecoration.Hero,
                weapon = RigWeapon.Katana,
                skin = new Color(0.96f, 0.82f, 0.72f),
                primary = new Color(0.08f, 0.09f, 0.18f),
                secondary = new Color(0.92f, 0.92f, 0.95f),
                accent = new Color(0.2f, 0.6f, 1f),
                hair = new Color(0.06f, 0.05f, 0.09f),
                eyes = new Color(0.1f, 0.1f, 0.15f)
            };
        }

        public static RigProfile Nightspawn()
        {
            return new RigProfile
            {
                name = "Nightspawn",
                decoration = RigDecoration.Demon,
                weapon = RigWeapon.Claws,
                hipHeight = 0.92f,
                upperArm = 0.36f,
                lowerArm = 0.36f,
                armThickness = 0.085f,
                legThickness = 0.11f,
                torsoWidth = 0.32f,
                torsoDepth = 0.2f,
                skin = new Color(0.16f, 0.12f, 0.2f),
                primary = new Color(0.07f, 0.05f, 0.09f),
                secondary = new Color(0.3f, 0.06f, 0.12f),
                accent = new Color(0.9f, 0.1f, 0.2f),
                hair = new Color(0.05f, 0.03f, 0.06f),
                eyes = new Color(4f, 0.35f, 0.25f),
                outline = 1.5f
            };
        }

        public static RigProfile HollowOni()
        {
            return new RigProfile
            {
                name = "HollowOni",
                decoration = RigDecoration.Oni,
                weapon = RigWeapon.Kanabo,
                scale = 1.85f,
                spineLength = 0.26f,
                chestLength = 0.3f,
                shoulderWidth = 0.25f,
                upperArm = 0.31f,
                lowerArm = 0.29f,
                armThickness = 0.16f,
                legThickness = 0.18f,
                torsoWidth = 0.5f,
                torsoDepth = 0.32f,
                hipWidth = 0.13f,
                skin = new Color(0.45f, 0.1f, 0.12f),
                primary = new Color(0.1f, 0.07f, 0.08f),
                secondary = new Color(0.85f, 0.82f, 0.76f),
                accent = new Color(0.55f, 0.35f, 0.12f),
                hair = new Color(0.9f, 0.9f, 0.92f),
                eyes = new Color(4f, 1.2f, 0.2f),
                outline = 1.3f
            };
        }
    }
}
