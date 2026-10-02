using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Layer indices configured in ProjectSettings/TagManager.asset. Centralized so masks never use magic numbers.
    /// </summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Player = 6;
        public const int Enemy = 7;
        public const int Destructible = 8;
        public const int VFX = 9;
        public const int Debris = 10;
        /// <summary>Renderers hidden from the main camera in first person (head, hair).</summary>
        public const int PlayerHidden = 11;
        /// <summary>Villagers and other world NPCs (v0.5): solid for characters, hittable by demons (team rules keep the player's blade off them).</summary>
        public const int Npc = 12;

        public static int Mask(params int[] layers)
        {
            int mask = 0;
            foreach (var l in layers) mask |= 1 << l;
            return mask;
        }

        /// <summary>Static world geometry used for ground checks and camera collision.</summary>
        public static readonly int EnvironmentMask = Mask(Default, Water);
        /// <summary>Everything a sword can hit.</summary>
        public static readonly int HittableMask = Mask(Player, Enemy, Destructible, Npc);
        public static readonly int CameraObstacleMask = Mask(Default, Destructible);
        public static readonly int CharacterMask = Mask(Player, Enemy);

        /// <summary>Collision matrix tweaks applied once at boot (VFX and debris never block characters).</summary>
        public static void ConfigureCollisionMatrix()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(VFX, i, true);
            }
            Physics.IgnoreLayerCollision(Debris, Player, true);
            Physics.IgnoreLayerCollision(Debris, Enemy, true);
            Physics.IgnoreLayerCollision(Debris, Debris, false);
            Physics.IgnoreLayerCollision(PlayerHidden, Player, true);
            Physics.IgnoreLayerCollision(Debris, Npc, true);
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
