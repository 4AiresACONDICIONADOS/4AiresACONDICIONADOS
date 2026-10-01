using System.Diagnostics;
using BreathOfEclipse.Core;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// 04_FrontierRegion — v0.5 LIVING WORLD. Builds the always-present part of the Asagiri Frontier (terrain data,
    /// river, waterfall, landmarks, far views), restores or creates the world memory, then hands over to the
    /// persistent world root (<see cref="LivingWorld"/>): clock, sector streaming, NPC life, demons, events, saving.
    /// A fresh world starts in the village at 07:00 in front of the inn.
    /// </summary>
    public sealed class FrontierRegionBuilder : GameplaySceneBuilder
    {
        public static readonly Vector3 VillageSpawn = new Vector3(-19f, 0.5f, -165f);
        public const float VillageSpawnYaw = 90f;

        private void Reset()
        {
            playerSpawn = VillageSpawn;
            playerSpawnYaw = VillageSpawnYaw;
            music = "";
        }

        protected override string AmbientLoop => null;

        public override void BuildEnvironment(Transform root)
        {
            var sw = Stopwatch.StartNew();
            RegionTerrain.Initialize();
            RegionBase.Build(root);

            WorldStateData data = WorldSave.StartFresh ? null : WorldSave.Load();
            WorldSave.StartFresh = false;
            bool continued = data != null;
            if (data == null) data = new WorldStateData { day = 1, hour = 7f };

            Vector3 spawn = VillageSpawn;
            float yaw = VillageSpawnYaw;
            if (continued && ValidSavedPosition(data))
            {
                spawn = new Vector3(data.playerX, data.playerY + 0.5f, data.playerZ);
                yaw = data.playerYaw;
            }
            playerSpawn = spawn;
            playerSpawnYaw = yaw;

            LivingWorld.Create(transform, data, continued, spawn);
            Debug.Log($"[FrontierRegion] Region built in {sw.ElapsedMilliseconds} ms ({(continued ? "continued world" : "new world")}, {LivingWorld.Instance.Sectors.LoadedCount} sectors loaded).");
        }

        private static bool ValidSavedPosition(WorldStateData d)
        {
            if (float.IsNaN(d.playerX) || float.IsNaN(d.playerZ)) return false;
            if (d.playerX == 0f && d.playerZ == 0f) return false;
            if (Mathf.Abs(d.playerX) > L.HalfSize - 12f || Mathf.Abs(d.playerZ) > L.HalfSize - 12f) return false;
            if (L.IsWater(d.playerX, d.playerZ)) return false;
            return L.SectorAt(d.playerX, d.playerZ) != null;
        }

        protected override void ApplyAtmosphere()
        {
            // The world clock drives sky, fog, ambient and lights every frame.
            Shader.SetGlobalFloat(ShaderIds.GlobalFlashFrame, 0f);
        }

        protected override void SpawnActors(PlayerController player)
        {
            var world = LivingWorld.Instance;
            if (world == null) return;
            world.BindPlayer(player);
            GameEvents.Notify(world.Continued ? $"{L.RegionName} — {world.Time.Describe()}" : $"{L.RegionName.ToUpperInvariant()} — the village wakes up");
        }
    }
}
