using System.Collections.Generic;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Rendering;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;
using M = BreathOfEclipse.World.RegionMats;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Buildings and set pieces of the frontier (original designs in a rural Japanese anime style): houses with
    /// plaster walls, wooden frames and tiled gable roofs, the two-storey inn, the store with its stall and shutters,
    /// the smithy, the hunters' post, the bell tower, shrines, the well, farm buildings, the arched bridge, the
    /// palisade, the camp, the cave, the ruins, the giant camphor and the cursed gate. Static parts are merged.
    /// </summary>
    public static class RegionProps
    {
        private static readonly Color WarmLight = new Color(1f, 0.68f, 0.38f);
        private static readonly Color WarmGlow = new Color(2.6f, 1.7f, 0.8f);

        // ------------------------------------------------------------------ houses

        /// <summary>Plaster-and-timber house with a tiled gable roof, porch, shoji door and windows that glow at night.</summary>
        public static void House(PropContext c, float w, float d, int seed, bool thatched = false, bool smoke = true)
        {
            var rng = new System.Random(seed);
            const float floor = 0.5f, wallH = 2.6f;
            c.Box(M.Stone, new Vector3(0f, 0.05f, 0f), new Vector3(w + 0.3f, 0.9f, d + 0.3f));
            c.Box(M.Plaster, new Vector3(0f, floor + wallH * 0.5f, 0f), new Vector3(w - 0.1f, wallH, d - 0.1f));
            // Timber frame: corner posts, sill and head beams, a middle post on the long sides.
            foreach (float sx in new[] { -0.5f, 0.5f })
            foreach (float sz in new[] { -0.5f, 0.5f })
                c.Box(M.DarkWood, new Vector3(sx * w, floor + wallH * 0.5f, sz * d), new Vector3(0.24f, wallH + 0.1f, 0.24f));
            foreach (float sz in new[] { -0.5f, 0.5f })
            {
                c.Box(M.DarkWood, new Vector3(0f, floor + wallH, sz * d), new Vector3(w + 0.1f, 0.22f, 0.26f));
                c.Box(M.DarkWood, new Vector3(0f, floor + 0.1f, sz * d), new Vector3(w + 0.1f, 0.2f, 0.26f));
                c.Box(M.DarkWood, new Vector3(0f, floor + wallH * 0.55f, sz * d), new Vector3(w, 0.12f, 0.24f));
            }
            foreach (float sx in new[] { -0.5f, 0.5f })
            {
                c.Box(M.DarkWood, new Vector3(sx * w, floor + wallH, 0f), new Vector3(0.26f, 0.22f, d + 0.1f));
                c.Box(M.DarkWood, new Vector3(sx * w, floor + wallH * 0.5f, 0f), new Vector3(0.22f, wallH, 0.18f));
            }
            // Roof.
            float rw = w + 1.5f, rd = d + 1.9f, rh = thatched ? 2.4f : 1.8f;
            c.Mesh(RegionMeshes.Gable(), thatched ? M.Thatch : M.Roof, new Vector3(0f, floor + wallH + 0.05f, 0f), Vector3.zero, new Vector3(rw, rh, rd));
            c.Box(thatched ? M.DarkWood : M.DarkStone, new Vector3(0f, floor + wallH + rh * 0.97f, 0f), new Vector3(rw + 0.2f, 0.2f, 0.36f));
            // Porch (engawa) and step.
            c.Box(M.Wood, new Vector3(0f, 0.42f, d * 0.5f + 0.6f), new Vector3(w, 0.12f, 1.2f));
            c.Box(M.Stone, new Vector3(0f, 0.15f, d * 0.5f + 1.45f), new Vector3(1.4f, 0.3f, 0.5f));
            // Door and windows: live panels that glow at night.
            var door = c.Live("Door", RegionMeshes.Panel(), M.Shoji, new Vector3(0f, floor + 1.05f, d * 0.5f + 0.02f), Vector3.zero, new Vector3(1.6f, 2.1f, 1f));
            var winL = c.Live("Window", RegionMeshes.Panel(), M.Shoji, new Vector3(-w * 0.3f, floor + 1.55f, d * 0.5f + 0.02f), Vector3.zero, new Vector3(1.2f, 0.9f, 1f));
            var winR = c.Live("Window", RegionMeshes.Panel(), M.Shoji, new Vector3(w * 0.3f, floor + 1.55f, d * 0.5f + 0.02f), Vector3.zero, new Vector3(1.2f, 0.9f, 1f));
            var winB = c.Live("Window", RegionMeshes.Panel(), M.Shoji, new Vector3(0f, floor + 1.55f, -d * 0.5f - 0.02f), new Vector3(0f, 180f, 0f), new Vector3(1.6f, 0.9f, 1f));
            var lit = c.Light(new Vector3(0f, floor + 1.5f, d * 0.5f + 0.9f), WarmLight, 0f, 0f, new Color(1.2f, 0.75f, 0.35f),
                door.GetComponent<Renderer>(), winL.GetComponent<Renderer>(), winR.GetComponent<Renderer>(), winB.GetComponent<Renderer>());
            lit.Flicker = 0.05f;
            // Door lantern (every other house).
            if (rng.NextDouble() < 0.6) HangingLantern(c, new Vector3(w * 0.5f - 0.3f, floor + 2.2f, d * 0.5f + 0.5f), false);
            // Side clutter: barrels, firewood, pots.
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            c.Cyl(M.Wood, new Vector3(side * (w * 0.5f + 0.6f), 0.45f, d * 0.2f), 0.35f, 0.9f);
            c.Cyl(M.Wood, new Vector3(side * (w * 0.5f + 0.6f), 0.4f, d * 0.2f - 0.8f), 0.32f, 0.8f);
            for (int i = 0; i < 4; i++)
                c.Cyl(M.FreshWood, new Vector3(-side * (w * 0.5f + 0.45f), 0.2f + i * 0.22f, -d * 0.25f), 0.11f, 1.6f, new Vector3(90f, 0f, 0f));
            c.Ball(M.Cloth(new Color(0.55f, 0.32f, 0.22f)), new Vector3(w * 0.35f, 0.75f, d * 0.5f + 0.9f), new Vector3(0.45f, 0.45f, 0.45f));
            c.Collider(new Vector3(0f, floor + wallH * 0.5f, 0f), new Vector3(w + 0.1f, wallH + floor * 2f, d + 0.1f));
            if (smoke)
            {
                var ps = c.Smoke(new Vector3(w * 0.3f, floor + wallH + rh * 0.7f, -d * 0.2f), 1.6f);
                ps.gameObject.AddComponent<HearthSmoke>().Smoke = ps;
            }
        }

        public static void Inn(PropContext c, float w, float d, int seed)
        {
            House(c, w, d, seed, false, true);
            // Second storey, narrower, with its own roof; red lanterns and a noren curtain at the door.
            const float upperY = 3.35f;
            c.Box(M.Plaster, new Vector3(0f, upperY + 1.2f, -0.4f), new Vector3(w - 1.6f, 2.4f, d - 2f));
            c.Mesh(RegionMeshes.Gable(), M.Roof, new Vector3(0f, upperY + 2.45f, -0.4f), Vector3.zero, new Vector3(w - 0.2f, 1.8f, d - 0.4f));
            c.Box(M.DarkStone, new Vector3(0f, upperY + 4.2f, -0.4f), new Vector3(w, 0.2f, 0.36f));
            c.Live("Noren", RegionMeshes.Panel(), M.Cloth(new Color(0.15f, 0.2f, 0.42f)), new Vector3(0f, 2.15f, d * 0.5f + 0.15f), Vector3.zero, new Vector3(1.9f, 0.7f, 1f));
            c.Box(M.DarkWood, new Vector3(0f, 3.15f, d * 0.5f + 0.25f), new Vector3(2.4f, 0.55f, 0.08f));
            HangingLantern(c, new Vector3(-1.4f, 2.4f, d * 0.5f + 0.6f), true);
            HangingLantern(c, new Vector3(1.4f, 2.4f, d * 0.5f + 0.6f), true);
            var upper = c.Live("Window", RegionMeshes.Panel(), M.Shoji, new Vector3(0f, upperY + 1.3f, d * 0.5f - 1.38f), Vector3.zero, new Vector3(3f, 1f, 1f));
            c.Light(new Vector3(0f, upperY + 1.3f, d * 0.5f), WarmLight, 0f, 0f, new Color(1.2f, 0.75f, 0.35f), upper.GetComponent<Renderer>());
            c.Collider(new Vector3(0f, upperY + 1.2f, -0.4f), new Vector3(w - 1.6f, 2.4f, d - 2f));
        }

        /// <summary>Paper lantern hanging from a short bracket (red for the inn and the cursed fort).</summary>
        public static NightLight HangingLantern(PropContext c, Vector3 pos, bool red, bool alwaysOn = false)
        {
            c.Box(M.DarkWood, pos + new Vector3(0f, 0.38f, 0f), new Vector3(0.06f, 0.3f, 0.06f));
            var body = c.Live("Lantern", ProceduralMeshes.Primitive(PrimitiveType.Sphere), red ? M.RedLantern : M.Lantern, pos, Vector3.zero, new Vector3(0.38f, 0.5f, 0.38f));
            var nl = c.Light(pos, red ? new Color(1f, 0.35f, 0.25f) : WarmLight, red ? 1.2f : 1.4f, 6f, red ? new Color(3f, 0.5f, 0.35f) : WarmGlow, body.GetComponent<Renderer>());
            nl.Flicker = 0.12f;
            nl.AlwaysOn = alwaysOn;
            return nl;
        }

        /// <summary>Paper lantern on a wooden post (roads, bridge).</summary>
        public static NightLight PostLantern(PropContext c, Vector3 pos)
        {
            c.Box(M.DarkWood, pos + new Vector3(0f, 0.9f, 0f), new Vector3(0.14f, 1.8f, 0.14f));
            c.Box(M.DarkWood, pos + new Vector3(0f, 1.82f, 0f), new Vector3(0.5f, 0.06f, 0.5f));
            var body = c.Live("Lantern", ProceduralMeshes.Primitive(PrimitiveType.Cube), M.Lantern, pos + new Vector3(0f, 2.1f, 0f), Vector3.zero, new Vector3(0.36f, 0.5f, 0.36f));
            c.Mesh(ProceduralMeshes.Cone(4), M.DarkWood, pos + new Vector3(0f, 2.35f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.62f, 0.25f, 0.62f));
            var nl = c.Light(pos + new Vector3(0f, 2.1f, 0f), WarmLight, 1.3f, 7f, WarmGlow, body.GetComponent<Renderer>());
            nl.Flicker = 0.1f;
            return nl;
        }

        public static void StoneLantern(PropContext c, Vector3 pos, bool lit, bool red = false)
        {
            c.Cyl(M.Stone, pos + new Vector3(0f, 0.15f, 0f), 0.3f, 0.3f);
            c.Cyl(M.Stone, pos + new Vector3(0f, 0.7f, 0f), 0.11f, 0.9f);
            c.Box(M.Stone, pos + new Vector3(0f, 1.3f, 0f), new Vector3(0.55f, 0.4f, 0.55f));
            c.Mesh(ProceduralMeshes.Cone(4), M.Stone, pos + new Vector3(0f, 1.5f, 0f), new Vector3(0f, 45f, 0f), new Vector3(1.1f, 0.45f, 1.1f));
            if (!lit) return;
            var fire = c.Live("Fire", ProceduralMeshes.Primitive(PrimitiveType.Cube), red ? M.RedLantern : M.Lantern, pos + new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(0.58f, 0.2f, 0.3f));
            var nl = c.Light(pos + new Vector3(0f, 1.3f, 0f), red ? new Color(1f, 0.3f, 0.3f) : WarmLight, 1.6f, 7f, red ? new Color(3f, 0.4f, 0.4f) : WarmGlow, fire.GetComponent<Renderer>());
            nl.Flicker = 0.1f;
            nl.AlwaysOn = red;
        }

        // ------------------------------------------------------------------ village buildings

        public static ShopShutters Shop(PropContext c, float w, float d, int seed)
        {
            House(c, w, d, seed, false, false);
            // Open storefront: counter, goods, an awning; shutters close it at night.
            c.Box(M.Wood, new Vector3(0f, 1f, d * 0.5f + 0.9f), new Vector3(w - 1.2f, 0.9f, 0.7f));
            var goods = new[] { new Color(0.8f, 0.3f, 0.2f), new Color(0.9f, 0.75f, 0.35f), new Color(0.35f, 0.55f, 0.3f), new Color(0.5f, 0.35f, 0.6f) };
            for (int i = 0; i < 6; i++)
                c.Box(M.Cloth(goods[i % goods.Length]), new Vector3(-w * 0.4f + i * w * 0.16f, 1.6f, d * 0.5f + 0.9f), new Vector3(0.45f, 0.35f, 0.4f), new Vector3(0f, i * 17f, 0f));
            c.Mesh(RegionMeshes.FlatQuad(), M.Cloth(new Color(0.85f, 0.82f, 0.7f)), new Vector3(0f, 2.75f, d * 0.5f + 1.3f), new Vector3(-18f, 0f, 0f), new Vector3(w, 1f, 1.8f));
            var shutters = c.Live("Shutters", ProceduralMeshes.Primitive(PrimitiveType.Cube), M.DarkWood, new Vector3(0f, 1.6f, d * 0.5f + 1.3f), Vector3.zero, new Vector3(w - 0.8f, 2.2f, 0.08f), true);
            var s = shutters.AddComponent<ShopShutters>();
            s.Panel = shutters;
            return s;
        }

        public static void Stall(PropContext c)
        {
            c.Box(M.Wood, new Vector3(0f, 0.85f, 0f), new Vector3(2.2f, 0.12f, 1f));
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -0.4f, 0.4f })
                c.Box(M.DarkWood, new Vector3(x, 0.42f, z), new Vector3(0.08f, 0.85f, 0.08f));
            c.Cyl(M.Wood, new Vector3(0f, 1.6f, -0.3f), 0.04f, 2.2f);
            c.Cone(M.Cloth(new Color(0.75f, 0.2f, 0.18f)), new Vector3(0f, 2.4f, -0.3f), new Vector3(2.8f, 0.6f, 2.8f), Vector3.zero, 0f, 12);
            for (int i = 0; i < 5; i++)
                c.Ball(M.Cloth(i % 2 == 0 ? new Color(0.9f, 0.55f, 0.2f) : new Color(0.6f, 0.75f, 0.3f)), new Vector3(-0.8f + i * 0.4f, 1.02f, 0.1f), new Vector3(0.22f, 0.2f, 0.22f));
            c.Collider(new Vector3(0f, 0.5f, 0f), new Vector3(2.3f, 1f, 1.1f));
        }

        public static NightLight Smithy(PropContext c, float w, float d)
        {
            // Open-fronted shed: posts, back wall, roof.
            foreach (float sx in new[] { -0.5f, 0.5f })
            foreach (float sz in new[] { -0.5f, 0.5f })
                c.Box(M.DarkWood, new Vector3(sx * w, 1.7f, sz * d), new Vector3(0.26f, 3.4f, 0.26f));
            c.Box(M.Plaster, new Vector3(0f, 1.5f, -d * 0.5f), new Vector3(w, 3f, 0.25f));
            c.Box(M.Plaster, new Vector3(-w * 0.5f, 1.5f, 0f), new Vector3(0.25f, 3f, d));
            c.Mesh(RegionMeshes.Gable(), M.Roof, new Vector3(0f, 3.4f, 0f), Vector3.zero, new Vector3(w + 1.2f, 1.6f, d + 1.4f));
            c.Box(M.Stone, new Vector3(0f, 0.05f, 0f), new Vector3(w + 0.2f, 0.25f, d + 0.2f));
            // Forge with glowing coals, anvil, quench barrel, rack of spears and a grindstone.
            c.Box(M.DarkStone, new Vector3(-w * 0.25f, 0.6f, -d * 0.25f), new Vector3(1.6f, 1.2f, 1.4f));
            c.Box(M.DarkStone, new Vector3(-w * 0.25f, 2.4f, -d * 0.35f), new Vector3(0.7f, 2.4f, 0.7f));
            var coals = c.Live("Coals", ProceduralMeshes.Primitive(PrimitiveType.Cube), M.Coals, new Vector3(-w * 0.25f, 1.22f, -d * 0.25f), Vector3.zero, new Vector3(1.1f, 0.08f, 0.9f));
            var forge = c.Light(new Vector3(-w * 0.25f, 1.6f, -d * 0.25f), new Color(1f, 0.45f, 0.15f), 2.2f, 8f, new Color(4f, 1.2f, 0.25f), coals.GetComponent<Renderer>());
            forge.Flicker = 0.25f;
            forge.AlwaysOn = true;
            c.Box(M.Iron, new Vector3(w * 0.15f, 0.75f, 0.2f), new Vector3(0.9f, 0.25f, 0.4f));
            c.Box(M.Iron, new Vector3(w * 0.15f, 0.4f, 0.2f), new Vector3(0.4f, 0.6f, 0.3f));
            c.Cyl(M.Wood, new Vector3(w * 0.32f, 0.45f, -d * 0.3f), 0.4f, 0.9f);
            for (int i = 0; i < 4; i++)
                c.Cyl(M.Wood, new Vector3(w * 0.45f - 0.1f, 1.5f, -d * 0.4f + i * 0.4f), 0.03f, 2.8f, new Vector3(0f, 0f, 8f));
            c.Collider(new Vector3(0f, 1.5f, -d * 0.5f), new Vector3(w, 3f, 0.3f));
            c.Collider(new Vector3(-w * 0.5f, 1.5f, 0f), new Vector3(0.3f, 3f, d));
            c.Collider(new Vector3(-w * 0.25f, 0.6f, -d * 0.25f), new Vector3(1.6f, 1.2f, 1.4f));
            return forge;
        }

        public static void HunterPost(PropContext c, float w, float d, int seed, Color crest)
        {
            House(c, w, d, seed, false, false);
            // Banner of the hunters (an original crest: crossed blades under a crescent), training post, weapon rack.
            c.Cyl(M.DarkWood, new Vector3(w * 0.5f + 1.2f, 2.5f, d * 0.5f + 1.2f), 0.06f, 5f);
            var banner = c.Live("Banner", RegionMeshes.Panel(), M.Cloth(new Color(0.1f, 0.12f, 0.2f)), new Vector3(w * 0.5f + 1.2f, 3.6f, d * 0.5f + 1.55f), new Vector3(0f, 90f, 0f), new Vector3(0.7f, 1.9f, 1f));
            banner.AddComponent<ClothSway>();
            c.Live("Crest", RegionMeshes.Panel(), MaterialFactory.Toon(crest, 0f, false, crest * 0.4f), new Vector3(w * 0.5f + 1.21f, 3.9f, d * 0.5f + 1.55f), new Vector3(0f, 90f, 0f), new Vector3(0.4f, 0.4f, 1f));
            c.Cyl(M.Straw, new Vector3(-w * 0.5f - 2.2f, 0.9f, d * 0.5f + 2f), 0.18f, 1.8f);
            c.Box(M.Straw, new Vector3(-w * 0.5f - 2.2f, 1.4f, d * 0.5f + 2f), new Vector3(0.9f, 0.14f, 0.14f));
            c.Ball(M.Straw, new Vector3(-w * 0.5f - 2.2f, 1.95f, d * 0.5f + 2f), new Vector3(0.32f, 0.36f, 0.32f));
            c.Capsule(new Vector3(-w * 0.5f - 2.2f, 0f, d * 0.5f + 2f), 0.25f, 2f);
            c.Box(M.DarkWood, new Vector3(w * 0.5f + 0.4f, 1f, -0.5f), new Vector3(0.1f, 2f, 1.6f));
            for (int i = 0; i < 3; i++)
                c.Cyl(M.Iron, new Vector3(w * 0.5f + 0.5f, 1.2f, -1f + i * 0.5f), 0.025f, 2.3f, new Vector3(0f, 0f, -10f));
        }

        public static void Watchtower(PropContext c)
        {
            const float h = 8f, s = 1.7f;
            foreach (float sx in new[] { -s, s })
            foreach (float sz in new[] { -s, s })
                c.Box(M.DarkWood, new Vector3(sx, h * 0.5f, sz), new Vector3(0.28f, h, 0.28f), new Vector3(-sz * 1.5f, 0f, sx * 1.5f));
            for (int i = 0; i < 3; i++)
            {
                float y = 1.5f + i * 2.3f;
                c.Box(M.Wood, new Vector3(0f, y, s), new Vector3(s * 2f, 0.12f, 0.12f), new Vector3(0f, 0f, i % 2 == 0 ? 35f : -35f));
                c.Box(M.Wood, new Vector3(0f, y, -s), new Vector3(s * 2f, 0.12f, 0.12f), new Vector3(0f, 0f, i % 2 == 0 ? -35f : 35f));
            }
            c.Box(M.Wood, new Vector3(0f, h - 0.6f, 0f), new Vector3(s * 2f + 1f, 0.2f, s * 2f + 1f));
            foreach (float sx in new[] { -1f, 1f })
            {
                c.Box(M.Wood, new Vector3(sx * (s + 0.45f), h, 0f), new Vector3(0.08f, 0.9f, s * 2f + 0.9f));
                c.Box(M.Wood, new Vector3(0f, h, sx * (s + 0.45f)), new Vector3(s * 2f + 0.9f, 0.9f, 0.08f));
            }
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                c.Box(M.DarkWood, new Vector3(sx * (s + 0.3f), h + 1.2f, sz * (s + 0.3f)), new Vector3(0.16f, 2.4f, 0.16f));
            c.Mesh(RegionMeshes.Gable(), M.Roof, new Vector3(0f, h + 2.35f, 0f), Vector3.zero, new Vector3(s * 2f + 1.8f, 1.4f, s * 2f + 1.8f));
            // The bronze alarm bell.
            c.Cyl(M.Bronze, new Vector3(0f, h + 1.35f, 0f), 0.42f, 0.8f);
            c.Ball(M.Bronze, new Vector3(0f, h + 1.75f, 0f), new Vector3(0.8f, 0.4f, 0.8f));
            c.Cyl(M.DarkWood, new Vector3(0f, h + 2.05f, 0f), 0.05f, 0.5f);
            // Ladder.
            c.Box(M.Wood, new Vector3(-0.35f, h * 0.5f, s + 0.7f), new Vector3(0.08f, h, 0.08f), new Vector3(-8f, 0f, 0f));
            c.Box(M.Wood, new Vector3(0.35f, h * 0.5f, s + 0.7f), new Vector3(0.08f, h, 0.08f), new Vector3(-8f, 0f, 0f));
            for (int i = 0; i < 14; i++) c.Box(M.Wood, new Vector3(0f, 0.4f + i * 0.55f, s + 0.7f - i * 0.077f), new Vector3(0.7f, 0.06f, 0.06f));
            foreach (float sx in new[] { -s, s })
            foreach (float sz in new[] { -s, s })
                c.Collider(new Vector3(sx, h * 0.5f, sz), new Vector3(0.3f, h, 0.3f));
            HangingLantern(c, new Vector3(s + 0.4f, h - 1f, s + 0.4f), false);
        }

        public static void Shrine(PropContext c, bool small)
        {
            float k = small ? 0.7f : 1f;
            c.Box(M.Stone, new Vector3(0f, 0.25f * k, 0f), new Vector3(3.2f * k, 0.5f * k, 2.6f * k));
            c.Box(M.Wood, new Vector3(0f, 1.2f * k, -0.2f * k), new Vector3(1.8f * k, 1.4f * k, 1.4f * k));
            foreach (float sx in new[] { -0.9f, 0.9f })
                c.Box(M.Red, new Vector3(sx * k, 1.1f * k, 0.6f * k), new Vector3(0.14f * k, 1.4f * k, 0.14f * k));
            c.Mesh(RegionMeshes.Gable(), M.Roof, new Vector3(0f, 1.9f * k, 0f), Vector3.zero, new Vector3(2.6f * k, 1f * k, 2.4f * k));
            // Sacred rope with paper streamers.
            c.Cyl(M.Straw, new Vector3(0f, 1.75f * k, 0.66f * k), 0.06f * k, 1.9f * k, new Vector3(0f, 0f, 90f));
            for (int i = 0; i < 3; i++)
                c.Mesh(RegionMeshes.Panel(), M.Paper, new Vector3((-0.6f + i * 0.6f) * k, 1.5f * k, 0.68f * k), Vector3.zero, new Vector3(0.14f * k, 0.38f * k, 1f), false);
            c.Box(M.DarkWood, new Vector3(0f, 0.75f * k, 1.1f * k), new Vector3(0.9f * k, 0.5f * k, 0.5f * k));
            StoneLantern(c, new Vector3(-1.9f * k, 0f, 1.2f * k), true);
            StoneLantern(c, new Vector3(1.9f * k, 0f, 1.2f * k), true);
            c.Collider(new Vector3(0f, 0.9f * k, -0.2f * k), new Vector3(2f * k, 1.8f * k, 1.8f * k));
        }

        public static void Well(PropContext c)
        {
            c.Cyl(M.Stone, new Vector3(0f, 0.45f, 0f), 0.9f, 0.9f);
            c.Cyl(MaterialFactory.Toon(new Color(0.05f, 0.08f, 0.12f), 0f), new Vector3(0f, 0.91f, 0f), 0.72f, 0.02f, default, false);
            foreach (float sx in new[] { -0.95f, 0.95f })
                c.Box(M.DarkWood, new Vector3(sx, 1.2f, 0f), new Vector3(0.14f, 2.4f, 0.14f));
            c.Box(M.DarkWood, new Vector3(0f, 2.2f, 0f), new Vector3(2.1f, 0.12f, 0.12f));
            c.Mesh(RegionMeshes.Gable(), M.Roof, new Vector3(0f, 2.35f, 0f), Vector3.zero, new Vector3(2.6f, 0.8f, 1.8f));
            c.Cyl(M.Wood, new Vector3(0.5f, 1.05f, 0.3f), 0.18f, 0.3f);
            c.Collider(new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.9f, 1.8f));
        }

        public static void Barn(PropContext c, float w, float d)
        {
            foreach (float sx in new[] { -0.5f, 0.5f })
            foreach (float sz in new[] { -0.5f, 0.5f })
                c.Box(M.DarkWood, new Vector3(sx * w, 1.6f, sz * d), new Vector3(0.28f, 3.2f, 0.28f));
            c.Box(M.Wood, new Vector3(0f, 1.6f, -d * 0.5f), new Vector3(w, 3.2f, 0.2f));
            c.Mesh(RegionMeshes.Gable(), M.Thatch, new Vector3(0f, 3.2f, 0f), Vector3.zero, new Vector3(w + 1.2f, 2.2f, d + 1.4f));
            for (int i = 0; i < 3; i++) c.Box(M.Straw, new Vector3(-w * 0.25f + i * w * 0.25f, 0.5f, -d * 0.15f), new Vector3(1.4f, 1f, 1f), new Vector3(0f, i * 12f, 0f));
            c.Box(M.Wood, new Vector3(w * 0.3f, 0.6f, d * 0.3f), new Vector3(1.6f, 0.8f, 1f));
            c.Collider(new Vector3(0f, 1.6f, -d * 0.5f), new Vector3(w, 3.2f, 0.3f));
        }

        // ------------------------------------------------------------------ outdoors

        public static void Haystack(PropContext c, Vector3 pos, float s)
        {
            c.Cone(M.Straw, pos, new Vector3(1.6f * s, 1.8f * s, 1.6f * s), Vector3.zero, 0f, 10);
            c.Cyl(M.Straw, pos + new Vector3(0f, 0.3f * s, 0f), 0.75f * s, 0.6f * s);
        }

        public static void Scarecrow(PropContext c, Vector3 pos, float yaw, Color cloth)
        {
            c.Push(pos, yaw);
            c.Cyl(M.Wood, new Vector3(0f, 1f, 0f), 0.05f, 2f);
            c.Box(M.Wood, new Vector3(0f, 1.5f, 0f), new Vector3(1.5f, 0.07f, 0.07f));
            c.Box(M.Cloth(cloth), new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.7f, 0.25f));
            c.Ball(M.Straw, new Vector3(0f, 1.95f, 0f), new Vector3(0.32f, 0.36f, 0.32f));
            c.Cone(M.Straw, new Vector3(0f, 2.08f, 0f), new Vector3(0.9f, 0.25f, 0.9f), Vector3.zero, 0f, 10);
            c.Pop();
        }

        public static void Cart(PropContext c, Vector3 pos, float yaw, bool broken, int seed)
        {
            var rng = new System.Random(seed);
            c.Push(pos, yaw);
            if (!broken)
            {
                c.Box(M.Wood, new Vector3(0f, 0.85f, 0f), new Vector3(1.5f, 0.12f, 2.6f));
                foreach (float sx in new[] { -0.75f, 0.75f }) c.Box(M.Wood, new Vector3(sx, 1.15f, 0f), new Vector3(0.08f, 0.5f, 2.6f));
                foreach (float sx in new[] { -0.82f, 0.82f })
                    c.Cyl(M.DarkWood, new Vector3(sx, 0.55f, -0.2f), 0.55f, 0.1f, new Vector3(0f, 0f, 90f));
                foreach (float sx in new[] { -0.4f, 0.4f }) c.Box(M.Wood, new Vector3(sx, 0.75f, 2.1f), new Vector3(0.07f, 0.07f, 1.8f));
                for (int i = 0; i < 4; i++)
                    c.Box(M.Cloth(new Color(0.85f, 0.8f, 0.65f)), new Vector3(-0.35f + (i % 2) * 0.7f, 1.15f, -0.7f + (i / 2) * 0.9f), new Vector3(0.6f, 0.45f, 0.7f));
                c.Collider(new Vector3(0f, 0.8f, 0f), new Vector3(1.6f, 1.2f, 2.7f));
            }
            else
            {
                // Overturned cart, a broken wheel, spilled rice sacks and scattered crates (no blood).
                c.Box(M.Wood, new Vector3(0.2f, 0.45f, 0f), new Vector3(1.5f, 0.12f, 2.6f), new Vector3(0f, 0f, 70f));
                c.Box(M.Wood, new Vector3(-0.4f, 0.12f, 0.3f), new Vector3(0.08f, 0.5f, 2.6f), new Vector3(0f, 0f, 85f));
                c.Cyl(M.DarkWood, new Vector3(1.2f, 0.08f, -0.6f), 0.55f, 0.1f, new Vector3(0f, 20f, 0f));
                c.Cyl(M.DarkWood, new Vector3(-1.4f, 0.35f, 1.2f), 0.55f, 0.1f, new Vector3(70f, 0f, 90f));
                for (int i = 0; i < 6; i++)
                    c.Box(M.Cloth(new Color(0.85f, 0.8f, 0.65f)), new Vector3((float)rng.NextDouble() * 4f - 2f, 0.2f, (float)rng.NextDouble() * 4f - 1.5f),
                        new Vector3(0.6f, 0.35f, 0.7f), new Vector3(0f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f));
                for (int i = 0; i < 12; i++)
                    c.Ball(M.Cloth(new Color(0.95f, 0.93f, 0.85f)), new Vector3((float)rng.NextDouble() * 3f - 1f, 0.03f, (float)rng.NextDouble() * 3f - 0.5f), new Vector3(0.25f, 0.04f, 0.25f), default, false);
                // Claw gouges and dragged tracks in the dirt.
                var gouge = MaterialFactory.Toon(new Color(0.25f, 0.18f, 0.12f), 0f);
                for (int i = 0; i < 3; i++)
                    c.Mesh(RegionMeshes.FlatQuad(), gouge, new Vector3(-2f + i * 0.35f, 0.04f, 2.5f), new Vector3(0f, 20f, 0f), new Vector3(0.12f, 1f, 2.4f), false);
                c.Collider(new Vector3(0.2f, 0.45f, 0f), new Vector3(1.2f, 1f, 2.6f));
            }
            c.Pop();
        }

        public static void LaundryLine(PropContext c, Vector3 a, Vector3 b, int seed)
        {
            var rng = new System.Random(seed);
            c.Cyl(M.Wood, a + Vector3.up * 1.1f, 0.05f, 2.2f);
            c.Cyl(M.Wood, b + Vector3.up * 1.1f, 0.05f, 2.2f);
            Vector3 d = b - a;
            float len = new Vector2(d.x, d.z).magnitude;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            c.Push((a + b) * 0.5f, yaw);
            c.Box(M.Straw, new Vector3(0f, 2.05f, 0f), new Vector3(0.02f, 0.02f, len));
            var colors = new[] { new Color(0.25f, 0.35f, 0.65f), new Color(0.9f, 0.88f, 0.8f), new Color(0.75f, 0.35f, 0.3f), new Color(0.5f, 0.6f, 0.4f) };
            for (float z = -len * 0.4f; z < len * 0.4f; z += 0.9f)
            {
                var cloth = c.Live("Cloth", RegionMeshes.Panel(), M.Cloth(colors[rng.Next(colors.Length)]), new Vector3(0f, 1.6f, z), new Vector3(0f, 90f, 0f), new Vector3(0.7f, 0.85f, 1f));
                cloth.AddComponent<ClothSway>();
            }
            c.Pop();
        }

        public static void Campfire(PropContext c, Vector3 pos, out NightLight light, out ParticleSystem fire)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                c.Ball(M.DarkStone, pos + new Vector3(Mathf.Cos(a) * 0.6f, 0.08f, Mathf.Sin(a) * 0.6f), new Vector3(0.3f, 0.2f, 0.28f));
            }
            for (int i = 0; i < 3; i++) c.Cyl(M.DarkWood, pos + new Vector3(0f, 0.15f, 0f), 0.07f, 1.1f, new Vector3(80f, i * 60f, 0f));
            fire = c.Fire(pos + Vector3.up * 0.15f, 1f);
            light = c.Light(pos + Vector3.up * 0.8f, new Color(1f, 0.55f, 0.25f), 2.2f, 9f, Color.black);
            light.Flicker = 0.3f;
            // Cooking tripod.
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                c.Cyl(M.DarkWood, pos + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.75f, 0.45f), 0.03f, 1.6f, new Vector3(-18f, a, 0f));
            }
            c.Ball(M.Iron, pos + new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 0.35f, 0.45f));
        }

        public static void Tent(PropContext c, Vector3 pos, float yaw, Color cloth)
        {
            c.Push(pos, yaw);
            c.Mesh(RegionMeshes.Gable(), M.Cloth(cloth), new Vector3(0f, 0f, 0f), new Vector3(0f, 90f, 0f), new Vector3(2.8f, 1.6f, 2.2f));
            c.Cyl(M.Wood, new Vector3(0f, 0.8f, 1.4f), 0.04f, 1.6f);
            c.Cyl(M.Wood, new Vector3(0f, 0.8f, -1.4f), 0.04f, 1.6f);
            c.Box(M.Cloth(new Color(0.35f, 0.3f, 0.25f)), new Vector3(0f, 0.04f, 0f), new Vector3(1.6f, 0.05f, 2.2f));
            c.Collider(new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 1.2f, 2.6f));
            c.Pop();
        }

        public static void LogPile(PropContext c, Vector3 pos, float yaw, int rows)
        {
            c.Push(pos, yaw);
            for (int r = 0; r < rows; r++)
            for (int i = 0; i < rows - r + 1; i++)
                c.Cyl(i % 2 == 0 ? M.Wood : M.FreshWood, new Vector3(-rows * 0.25f + i * 0.5f + r * 0.25f, 0.25f + r * 0.44f, 0f), 0.24f, 3.2f, new Vector3(90f, 0f, 0f));
            c.Collider(new Vector3(0f, rows * 0.22f, 0f), new Vector3(rows * 0.55f + 0.5f, rows * 0.44f, 3.2f));
            c.Pop();
        }

        public static void Stump(PropContext c, Vector3 pos, bool axe)
        {
            c.Cyl(M.Wood, pos + new Vector3(0f, 0.25f, 0f), 0.4f, 0.5f);
            c.Cyl(M.FreshWood, pos + new Vector3(0f, 0.51f, 0f), 0.36f, 0.02f);
            if (!axe) return;
            c.Box(M.Iron, pos + new Vector3(0.1f, 0.62f, 0f), new Vector3(0.05f, 0.22f, 0.3f), new Vector3(0f, 0f, 20f));
            c.Cyl(M.Wood, pos + new Vector3(0.35f, 0.9f, 0f), 0.03f, 0.8f, new Vector3(0f, 0f, 55f));
        }

        public static void Dock(PropContext c, float length)
        {
            c.Box(M.Wood, new Vector3(0f, 0.12f, length * 0.5f), new Vector3(1.6f, 0.1f, length));
            for (int i = 0; i <= 3; i++)
                foreach (float sx in new[] { -0.75f, 0.75f })
                    c.Cyl(M.DarkWood, new Vector3(sx, -0.5f, i * length / 3f), 0.08f, 1.4f);
            c.Collider(new Vector3(0f, 0.12f, length * 0.5f), new Vector3(1.6f, 0.15f, length));
        }

        /// <summary>The arched bridge over the Shirase (deck follows FrontierRegionLayout.BridgeDeck).</summary>
        public static void Bridge(PropContext c)
        {
            const float z0 = -66f, z1 = -30f;
            int seg = 18;
            for (int i = 0; i < seg; i++)
            {
                float za = Mathf.Lerp(z0, z1, i / (float)seg), zb = Mathf.Lerp(z0, z1, (i + 1) / (float)seg);
                float ya = L.BridgeDeck(za), yb = L.BridgeDeck(zb);
                float len = Mathf.Sqrt((zb - za) * (zb - za) + (yb - ya) * (yb - ya));
                float pitch = -Mathf.Atan2(yb - ya, zb - za) * Mathf.Rad2Deg;
                var mid = new Vector3(0f, (ya + yb) * 0.5f - 0.1f, (za + zb) * 0.5f);
                c.Box(M.Wood, mid, new Vector3(4.2f, 0.2f, len + 0.05f), new Vector3(pitch, 0f, 0f));
                c.Collider(mid, new Vector3(4.2f, 0.25f, len + 0.05f), new Vector3(pitch, 0f, 0f));
                foreach (float sx in new[] { -2.05f, 2.05f })
                    c.Box(M.Red, mid + new Vector3(sx, 0.95f, 0f), new Vector3(0.12f, 0.12f, len + 0.05f), new Vector3(pitch, 0f, 0f));
                if (i % 3 == 0)
                    foreach (float sx in new[] { -2.05f, 2.05f })
                    {
                        c.Box(M.Red, new Vector3(sx, ya + 0.5f, za), new Vector3(0.2f, 1.1f, 0.2f));
                        c.Box(M.DarkWood, new Vector3(sx, ya - 1.6f, za), new Vector3(0.3f, 3.2f, 0.3f));
                    }
                foreach (float sx in new[] { -2.05f, 2.05f })
                    c.Collider(mid + new Vector3(sx, 0.6f, 0f), new Vector3(0.15f, 1.2f, len + 0.05f), new Vector3(pitch, 0f, 0f));
            }
            PostLantern(c, new Vector3(-2.8f, L.BridgeDeck(z0) - 0.1f, z0 - 1f));
            PostLantern(c, new Vector3(2.8f, L.BridgeDeck(z1) - 0.1f, z1 + 1f));
        }

        public static void SteppingStones(PropContext c)
        {
            for (float z = -79f; z <= -31f; z += 1.7f)
            {
                float y = Mathf.Max(RegionTerrain.SampleHeight(190f, z), L.WaterLevel + 0.1f);
                float x = 190f + Mathf.Sin(z * 1.3f) * 0.4f;
                c.Cyl(M.Stone, new Vector3(x, y, z), 0.7f, 0.6f);
                c.Collider(new Vector3(x, y + 0.15f, z), new Vector3(1.3f, 0.3f, 1.3f));
            }
        }

        /// <summary>Village palisade section between two angles (degrees around the village). Damage stage changes it.</summary>
        public static void Fence(PropContext c, float fromDeg, float toDeg, RecoveryStage stage, int seed)
        {
            var rng = new System.Random(seed);
            float r = L.VillageFenceRadius;
            float step = 1.1f / r * Mathf.Rad2Deg;
            Vector3 prev = Vector3.zero;
            bool havePrev = false;
            for (float a = fromDeg; a <= toDeg; a += step)
            {
                float rad = a * Mathf.Deg2Rad;
                float x = L.VillageX + Mathf.Sin(rad) * r, z = L.VillageZ + Mathf.Cos(rad) * r;
                // Leave the gates and road crossings open.
                if (L.NearestRoad(x, z, 6f, out var kind, out float half) < half + 1.5f && kind != PathKind.Forest)
                {
                    havePrev = false;
                    continue;
                }
                float y = RegionTerrain.SampleHeight(x, z);
                bool broken = stage == RecoveryStage.Damaged && rng.NextDouble() < 0.4;
                bool fresh = stage == RecoveryStage.Repairing && rng.NextDouble() < 0.5;
                var p = new Vector3(x, y, z);
                if (broken)
                {
                    c.Cyl(M.DarkWood, p + new Vector3(0f, 0.3f, 0f), 0.11f, 1.6f, new Vector3(70f + (float)rng.NextDouble() * 15f, a + 90f, 0f));
                    havePrev = false;
                    continue;
                }
                c.Cyl(fresh ? M.FreshWood : M.Wood, p + new Vector3(0f, 1f, 0f), 0.12f, 2.1f);
                c.Cone(fresh ? M.FreshWood : M.Wood, p + new Vector3(0f, 2.05f, 0f), new Vector3(0.24f, 0.25f, 0.24f));
                if (havePrev)
                {
                    Vector3 mid = (prev + p) * 0.5f;
                    float len = Vector3.Distance(prev, p);
                    float yaw = Mathf.Atan2(p.x - prev.x, p.z - prev.z) * Mathf.Rad2Deg;
                    c.Box(M.DarkWood, mid + Vector3.up * 0.7f, new Vector3(0.08f, 0.1f, len), new Vector3(0f, yaw, 0f));
                    c.Box(M.DarkWood, mid + Vector3.up * 1.5f, new Vector3(0.08f, 0.1f, len), new Vector3(0f, yaw, 0f));
                    c.Collider(mid + Vector3.up * 1f, new Vector3(0.3f, 2f, len), new Vector3(0f, yaw, 0f));
                }
                prev = p;
                havePrev = true;
            }
            if (stage == RecoveryStage.Repairing)
            {
                // Lumber stacked by the repair work.
                float mid = (fromDeg + toDeg) * 0.5f * Mathf.Deg2Rad;
                float x = L.VillageX + Mathf.Sin(mid) * (r - 3f), z = L.VillageZ + Mathf.Cos(mid) * (r - 3f);
                LogPile(c, RegionTerrain.OnGround(x, z), (fromDeg + toDeg) * 0.5f, 2);
            }
        }

        // ------------------------------------------------------------------ landmarks and exploration

        /// <summary>The Elder Camphor: an enormous sacred tree with a rope around its trunk (visible from most of the region).</summary>
        public static void GiantTree(PropContext c, int seed)
        {
            var rng = new System.Random(seed);
            c.Mesh(ProceduralMeshes.OpenCylinder(2.6f, 1.5f, 12, 4), M.Bark, Vector3.zero, Vector3.zero, new Vector3(1f, 20f, 1f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + (float)rng.NextDouble() * 20f;
                c.Cone(M.Bark, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 2f), new Vector3(1.6f, 3f, 1.6f), new Vector3(-35f, a, 0f), 0.3f);
                c.Mesh(ProceduralMeshes.OpenCylinder(0.8f, 0.35f, 8, 2), M.Bark, new Vector3(0f, 14f + i * 0.8f, 0f), new Vector3(-60f, a, 0f), new Vector3(1f, 9f, 1f));
            }
            var lit = M.Leaves(new Color(0.26f, 0.45f, 0.33f));
            var shade = M.Leaves(new Color(0.16f, 0.3f, 0.25f));
            for (int i = 0; i < 11; i++)
            {
                float a = i / 11f * Mathf.PI * 2f;
                float r = i == 0 ? 0f : 6f + (float)rng.NextDouble() * 4f;
                float y = 21f + (float)rng.NextDouble() * 5f;
                float s = i == 0 ? 15f : 8f + (float)rng.NextDouble() * 4f;
                c.Mesh(ProceduralMeshes.Blob(rng.Next(4)), y > 23f ? lit : shade, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(s, s * 0.7f, s));
            }
            for (int i = 0; i < 16; i++)
            {
                float a = (i + 0.5f) / 16f * 360f;
                c.Cyl(M.Straw, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 3f, 2.62f), 0.12f, 1.1f, new Vector3(0f, a, 90f));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * 360f;
                c.Mesh(RegionMeshes.Panel(), M.Paper, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 2.6f, 2.65f), new Vector3(0f, a, 0f), new Vector3(0.18f, 0.5f, 1f), false);
            }
            c.Capsule(Vector3.zero, 2.4f, 18f);
            Shrine(c, true);
        }

        /// <summary>Cave of Echoes: a rock mound with a dark mouth, a chamber under boulders, glowing fungi, old gear.</summary>
        public static void Cave(PropContext c, int seed)
        {
            var rng = new System.Random(seed);
            // The hill above the cave: overlapping boulders around a hollow chamber (radius ~6 m).
            for (int i = 0; i < 16; i++)
            {
                float a = (i / 16f) * 300f - 150f;
                if (Mathf.Abs(a) < 26f) continue; // the mouth faces local +Z
                float rad = a * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(rad) * 7.5f, 2.5f, Mathf.Cos(rad) * 7.5f);
                c.Ball(M.Rock, p, new Vector3(6f, 7f, 6f), new Vector3((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, 0f));
                c.Collider(p, new Vector3(5f, 6.5f, 5f), new Vector3(0f, a, 0f));
            }
            c.Ball(M.Rock, new Vector3(0f, 8f, 0f), new Vector3(16f, 6f, 16f));
            c.Ball(M.Rock, new Vector3(0f, 6.2f, 6.6f), new Vector3(6f, 2.6f, 3f));
            c.Collider(new Vector3(0f, 8.5f, 0f), new Vector3(15f, 4f, 15f));
            // Inside: dark floor, fungi, bones of an old campfire and a hunter's forgotten gear.
            c.Cyl(MaterialFactory.Toon(new Color(0.12f, 0.12f, 0.14f), 0f), new Vector3(0f, 0.02f, 0f), 6.5f, 0.04f, default, false);
            var glow = MaterialFactory.Toon(new Color(0.3f, 0.9f, 0.85f), 0f, false, new Color(0.2f, 1.4f, 1.2f));
            for (int i = 0; i < 14; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 3f + (float)rng.NextDouble() * 3f;
                c.Ball(glow, new Vector3(Mathf.Cos(a) * r, 0.12f + (float)rng.NextDouble() * 0.6f, Mathf.Sin(a) * r), new Vector3(0.18f, 0.08f, 0.18f), default, false);
            }
            c.Light(new Vector3(0f, 1.2f, -2f), new Color(0.3f, 0.9f, 0.85f), 0.8f, 7f, Color.black).AlwaysOn = true;
            OldGear(c, new Vector3(-2f, 0f, -1.5f), seed + 1);
            for (int i = 0; i < 4; i++) c.Cyl(M.DarkWood, new Vector3(2f, 0.08f, -2f), 0.06f, 0.9f, new Vector3(85f, i * 45f, 0f));
        }

        /// <summary>Abandoned gear: a straw hat, a broken spear, a torn cloth — signs of a lost hunter (no bodies).</summary>
        public static void OldGear(PropContext c, Vector3 pos, int seed)
        {
            var rng = new System.Random(seed);
            c.Cone(M.Straw, pos + new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.18f, 0.9f), new Vector3(8f, (float)rng.NextDouble() * 360f, 0f), 0f, 12);
            c.Cyl(M.Wood, pos + new Vector3(0.8f, 0.06f, 0.3f), 0.03f, 1.3f, new Vector3(88f, 40f, 0f));
            c.Cyl(M.Wood, pos + new Vector3(1.6f, 0.06f, 1f), 0.03f, 0.8f, new Vector3(88f, 70f, 0f));
            c.Box(M.Iron, pos + new Vector3(2f, 0.05f, 1.3f), new Vector3(0.06f, 0.04f, 0.35f), new Vector3(0f, 70f, 0f));
            c.Mesh(RegionMeshes.FlatQuad(), M.Cloth(new Color(0.12f, 0.15f, 0.3f)), pos + new Vector3(-0.6f, 0.04f, 0.6f), new Vector3(0f, 30f, 0f), new Vector3(0.9f, 1f, 0.7f), false);
        }

        public static void HiddenShrine(PropContext c, int seed)
        {
            var rng = new System.Random(seed);
            // A ring of moss-covered stone guardians with faded red bibs around a tiny altar, hidden by boulders.
            for (int i = 0; i < 7; i++)
            {
                float a = -60f + i * 20f;
                var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, -2.2f);
                c.Ball(M.Stone, p + new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.45f));
                c.Ball(M.Stone, p + new Vector3(0f, 0.78f, 0f), new Vector3(0.36f, 0.38f, 0.36f));
                c.Mesh(RegionMeshes.Panel(), M.Red, p + new Vector3(0f, 0.5f, 0.24f), new Vector3(0f, a + 180f, 0f), new Vector3(0.36f, 0.28f, 1f), false);
            }
            c.Box(M.Stone, new Vector3(0f, 0.3f, -0.4f), new Vector3(1f, 0.6f, 0.7f));
            c.Ball(MaterialFactory.Toon(new Color(0.75f, 0.85f, 1f), 0f, false, new Color(0.5f, 0.7f, 1.4f)), new Vector3(0f, 0.75f, -0.4f), new Vector3(0.22f, 0.22f, 0.22f), default, false);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * 360f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 4.5f, 0.8f, Mathf.Cos(a) * 4.5f);
                if (p.z > 2f) continue;
                c.Ball(M.Rock, p, new Vector3(2.4f, 2f, 2.4f));
                c.Collider(p, new Vector3(2f, 1.8f, 2f));
            }
        }

        public static void Lookout(PropContext c)
        {
            c.Box(M.Wood, new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.15f, 3f));
            foreach (float sx in new[] { -2f, 2f })
            foreach (float sz in new[] { -1.5f, 1.5f })
                c.Box(M.DarkWood, new Vector3(sx, 0f, sz), new Vector3(0.18f, 1f, 0.18f));
            c.Box(M.Wood, new Vector3(0f, 1f, 1.5f), new Vector3(4f, 0.08f, 0.08f));
            c.Box(M.Wood, new Vector3(0f, 0.7f, -0.6f), new Vector3(1.8f, 0.1f, 0.5f));
            c.Collider(new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.2f, 3f));
        }

        /// <summary>The broken gate at the edge of the Ash Hollow: a snapped pillar, a fallen beam, warding charms, claw marks.</summary>
        public static void CursedGate(PropContext c, int seed)
        {
            var dark = MaterialFactory.Toon(new Color(0.35f, 0.08f, 0.1f), 1.2f);
            c.Box(dark, new Vector3(-2.3f, 2.4f, 0f), new Vector3(0.5f, 4.8f, 0.5f));
            c.Box(dark, new Vector3(2.3f, 0.9f, 0f), new Vector3(0.5f, 1.8f, 0.5f), new Vector3(0f, 0f, 6f));
            c.Box(dark, new Vector3(1.8f, 0.4f, 1.6f), new Vector3(0.5f, 2.6f, 0.5f), new Vector3(80f, 20f, 0f));
            c.Box(M.DarkWood, new Vector3(0.6f, 2.6f, 0.3f), new Vector3(6.2f, 0.4f, 0.5f), new Vector3(0f, 8f, -26f));
            for (int i = 0; i < 6; i++)
                c.Mesh(RegionMeshes.Panel(), M.Paper, new Vector3(-2.3f, 1.2f + i * 0.45f, 0.27f), new Vector3(0f, 0f, (i % 2 == 0 ? 8f : -6f)), new Vector3(0.14f, 0.4f, 1f), false);
            c.Collider(new Vector3(-2.3f, 2.4f, 0f), new Vector3(0.5f, 4.8f, 0.5f));
            c.Collider(new Vector3(2.3f, 0.9f, 0f), new Vector3(0.5f, 1.8f, 0.5f));
            OldGear(c, new Vector3(-1f, 0f, 3f), seed);
            StoneLantern(c, new Vector3(-3.6f, 0f, -1f), true, true);
        }

        /// <summary>The ruined palisade deep in the Ash Hollow: charred stakes, a burnt hut, red demon lamps.</summary>
        public static void Fort(PropContext c, float w, float d, int seed)
        {
            var rng = new System.Random(seed);
            var charred = MaterialFactory.Toon(new Color(0.12f, 0.1f, 0.1f), 1f);
            for (float a = 0f; a < 300f; a += 4f)
            {
                if (rng.NextDouble() < 0.25) continue;
                float rad = a * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(rad) * w * 0.5f, 0f, Mathf.Cos(rad) * d * 0.5f);
                float h = 1.2f + (float)rng.NextDouble() * 2f;
                float tilt = (float)rng.NextDouble() * 25f;
                c.Cyl(charred, p + new Vector3(0f, h * 0.5f, 0f), 0.16f, h, new Vector3(tilt, a, 0f));
                if (h > 2f) c.Collider(p + new Vector3(0f, 1f, 0f), new Vector3(0.35f, 2f, 0.35f));
            }
            c.Push(new Vector3(2f, 0f, 1f), 30f);
            c.Box(charred, new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 3f));
            c.Box(charred, new Vector3(0.5f, 2.4f, 0.4f), new Vector3(4.6f, 0.2f, 3.6f), new Vector3(10f, 0f, 14f));
            c.Collider(new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 3f));
            c.Pop();
            for (int i = 0; i < 4; i++)
            {
                float a = i * 70f + 20f;
                var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, w * 0.35f);
                c.Box(charred, p + Vector3.up * 1.2f, new Vector3(0.12f, 2.4f, 0.12f));
                HangingLantern(c, p + new Vector3(0f, 2.1f, 0.25f), true, true);
            }
        }

        // ------------------------------------------------------------------ water

        /// <summary>The waterfall: a curved sheet of anime water down the western cliff, foam and mist at its foot.</summary>
        public static void Waterfall(Transform parent)
        {
            float x = L.WaterfallX, z = L.RiverZ(x);
            const int cols = 8, rows = 14;
            const float width = 9f;
            var v = new Vector3[(cols + 1) * (rows + 1)];
            var uv = new Vector2[v.Length];
            var t = new System.Collections.Generic.List<int>();
            float top = L.CliffTop - 0.6f, bottom = L.WaterLevel;
            for (int r = 0; r <= rows; r++)
            {
                float k = r / (float)rows;
                // Leaves the lip, then falls with a slight outward bow.
                float y = Mathf.Lerp(top, bottom, k * k * 0.35f + k * 0.65f);
                float out_ = -3.5f + 4.5f * Mathf.Sqrt(k) + 0.6f * Mathf.Sin(k * Mathf.PI);
                for (int cIndex = 0; cIndex <= cols; cIndex++)
                {
                    float u = cIndex / (float)cols;
                    float zz = z + (u - 0.5f) * width * (1f + 0.25f * k);
                    v[r * (cols + 1) + cIndex] = new Vector3(x + out_, y, zz);
                    uv[r * (cols + 1) + cIndex] = new Vector2(k, u);
                }
            }
            for (int r = 0; r < rows; r++)
            for (int cIndex = 0; cIndex < cols; cIndex++)
            {
                int a = r * (cols + 1) + cIndex, b = a + cols + 1;
                t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
            var mesh = new Mesh { name = "Waterfall", vertices = v, uv = uv };
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var mat = MaterialFactory.Instance(MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, false));
            mat.SetFloat(ShaderIds.Reveal, 1.2f);
            mat.SetFloat(ShaderIds.Tail, -0.3f);
            var go = new GameObject("Waterfall");
            go.transform.SetParent(parent, false);
            go.layer = Core.Layers.Water;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var foam = new GameObject("FallsFoam");
            foam.transform.SetParent(parent, false);
            foam.transform.position = new Vector3(x + 3.5f, L.WaterLevel + 0.2f, z);
            VFX.ParticleBuilder.Create("Mist", foam.transform, MaterialFactory.Vfx(ProceduralTextures.Smoke, VfxBlend.AlphaBlend, 1f))
                .Duration(4f, true).Rate(10f).Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(6f, 0.5f, 8f)).Life(2.5f, 4f).Speed(0.6f, 1.6f)
                .Size(2.5f, 5f).Rotation(0f, 360f).Color(new Color(0.9f, 0.95f, 1f, 0.22f)).Fade(0.2f, 0.4f).Grow(0.6f, 1.6f).MaxParticles(60).PlayOnAwake();
            VFX.ParticleBuilder.Create("Spray", foam.transform, MaterialFactory.Vfx(ProceduralTextures.Droplet, VfxBlend.AlphaBlend, 0.5f))
                .Duration(2f, true).Rate(30f).Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(4f, 0.3f, 7f)).Life(0.8f, 1.4f).Speed(2f, 4f)
                .Size(0.08f, 0.16f).Color(new Color(0.95f, 0.98f, 1f, 0.8f)).Gravity(0.6f).Fade(0.05f, 0.5f).MaxParticles(120).PlayOnAwake();
        }

        private static readonly Dictionary<string, Material> WaterMats = new Dictionary<string, Material>();

        /// <summary>Water surface material (cached by name: sectors stream in and out).</summary>
        public static Material WaterMaterial(string name, Color shallow, Color deep)
        {
            if (WaterMats.TryGetValue(name, out var cached) && cached != null) return cached;
            var mat = new Material(MaterialFactory.FindShader(ShaderIds.ToonWater)) { name = name };
            WaterMats[name] = mat;
            mat.SetColor("_ShallowColor", shallow);
            mat.SetColor("_DeepColor", deep);
            mat.SetColor("_FoamColor", new Color(0.85f, 0.95f, 1f, 1f));
            mat.SetTexture("_NoiseTex", ProceduralTextures.Caustics);
            return mat;
        }
    }

    /// <summary>Gentle wind sway for banners, laundry and charms (cheap: one sine per object).</summary>
    public sealed class ClothSway : MonoBehaviour
    {
        private Quaternion _rest;
        private float _seed;

        private void Awake()
        {
            _rest = transform.localRotation;
            _seed = Random.value * 10f;
        }

        private void Update()
        {
            float t = Time.time * 1.6f + _seed;
            transform.localRotation = _rest * Quaternion.Euler(Mathf.Sin(t) * 6f, Mathf.Sin(t * 0.7f) * 4f, 0f);
        }
    }

    /// <summary>Store shutters: open in trading hours, closed at night (set by the merchant schedule).</summary>
    public sealed class ShopShutters : MonoBehaviour
    {
        public GameObject Panel;
        private bool _closed = true;

        public void SetClosed(bool closed)
        {
            if (_closed == closed && Panel != null && Panel.GetComponent<Renderer>().enabled == closed) return;
            _closed = closed;
            if (Panel == null) return;
            var r = Panel.GetComponent<Renderer>();
            if (r != null) r.enabled = closed;
        }
    }
}
