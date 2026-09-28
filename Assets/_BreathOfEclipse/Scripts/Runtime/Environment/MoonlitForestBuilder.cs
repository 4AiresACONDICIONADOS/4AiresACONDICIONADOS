using BreathOfEclipse.AI;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Environment
{
    /// <summary>
    /// 02_MoonlitForest: an original night forest under a huge moon. A path runs north from the entrance gate,
    /// across a stream (bridge), through two combat clearings, to the abandoned temple courtyard where
    /// THE HOLLOW ONI waits. Layout favours open space for movement and combat.
    /// </summary>
    public sealed class MoonlitForestBuilder : GameplaySceneBuilder
    {
        private static readonly Vector3 MoonDir = new Vector3(0.2f, 0.42f, 1f);
        private static readonly Vector2 ClearingA = new Vector2(0f, -45f);
        private static readonly Vector2 ClearingB = new Vector2(0f, 8f);
        private static readonly Vector2 Courtyard = new Vector2(0f, 64f);
        private const float StreamZ = -20f;

        private void Reset()
        {
            playerSpawn = new Vector3(0f, 0.2f, -78f);
            playerSpawnYaw = 0f;
            music = "forest";
        }

        public static float Height(float x, float z)
        {
            float corridor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 40f, Mathf.Abs(x)));
            float hills = (Mathf.PerlinNoise(x * 0.03f + 3.1f, z * 0.03f + 7.7f) * 7f + 1f) * corridor;
            float undulation = (Mathf.PerlinNoise(x * 0.08f + 10f, z * 0.08f + 10f) - 0.5f) * 0.5f * (1f - corridor);
            float h = hills + undulation;
            float d = Mathf.Abs(z - StreamZ);
            h -= 1.0f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 4.5f, d)));
            float court = Vector2.Distance(new Vector2(x, z), Courtyard);
            h = Mathf.Lerp(0f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 30f, court)));
            // Rising back wall behind the temple and at the entrance.
            h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, 100f, z)) * 10f;
            h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-88f, -100f, z)) * 10f;
            return h;
        }

        private static bool Blocked(float x, float z)
        {
            if (Mathf.Abs(x) < 7f && z > -95f && z < 90f) return true; // path
            if (Vector2.Distance(new Vector2(x, z), ClearingA) < 15f) return true;
            if (Vector2.Distance(new Vector2(x, z), ClearingB) < 20f) return true;
            if (Vector2.Distance(new Vector2(x, z), Courtyard) < 24f) return true;
            if (Mathf.Abs(z - StreamZ) < 4.5f) return true;
            return false;
        }

        private static Vector3 OnGround(float x, float z) => new Vector3(x, Height(x, z), z);

        public override void BuildEnvironment(Transform root)
        {
            EnvironmentKit.CreateMoonLights(root, MoonDir, 1.6f);
            EnvironmentKit.SkyDome(root, MoonDir, 0.085f);
            EnvironmentKit.Ground(root, "ForestGround", new Vector2(220f, 210f), 2.5f, Height, new Color(0.2f, 0.27f, 0.25f), ProceduralTextures.GroundDetail, 1f);
            EnvironmentKit.Water(root, new Vector3(0f, -0.45f, StreamZ), new Vector2(220f, 8f), 0f);
            EnvironmentKit.Bridge(root, new Vector3(0f, -0.1f, StreamZ), 0f, 11f);

            // Path stones.
            var pathMat = MaterialFactory.Toon(new Color(0.38f, 0.37f, 0.36f), 0f, false, null, 0.55f, 0.1f, 0f, ProceduralTextures.StoneDetail, 1f);
            for (float z = -92f; z < 42f; z += 2.2f)
            {
                if (Mathf.Abs(z - StreamZ) < 6f) continue;
                float x = Mathf.Sin(z * 0.05f) * 1.2f;
                var p = OnGround(x, z);
                ProceduralMeshes.CreatePart("PathStone", PrimitiveType.Cylinder, pathMat, root, p + Vector3.up * 0.02f, new Vector3(0f, z * 37f, 0f), new Vector3(1.8f, 0.03f, 1.4f));
            }

            // Forest.
            var rng = new System.Random(1234);
            var leafColors = new[] { new Color(0.12f, 0.22f, 0.22f), new Color(0.15f, 0.26f, 0.2f), new Color(0.2f, 0.18f, 0.3f), new Color(0.35f, 0.2f, 0.32f) };
            int trees = 0;
            for (float x = -100f; x <= 100f; x += 8f)
            {
                for (float z = -95f; z <= 95f; z += 8f)
                {
                    float jx = x + (float)(rng.NextDouble() * 6 - 3);
                    float jz = z + (float)(rng.NextDouble() * 6 - 3);
                    if (Blocked(jx, jz)) continue;
                    if (rng.NextDouble() < 0.28) continue;
                    var color = leafColors[rng.Next(leafColors.Length)];
                    EnvironmentKit.Tree(root, OnGround(jx, jz) - Vector3.up * 0.1f, 1.1f + (float)rng.NextDouble() * 0.9f, rng.Next(), color);
                    trees++;
                }
            }
            for (int i = 0; i < 70; i++)
            {
                float x = (float)(rng.NextDouble() * 180 - 90);
                float z = (float)(rng.NextDouble() * 180 - 90);
                if (Mathf.Abs(x) < 5f) continue;
                var s = new Vector3(0.6f + (float)rng.NextDouble() * 1.8f, 0.4f + (float)rng.NextDouble() * 1.2f, 0.6f + (float)rng.NextDouble() * 1.8f);
                EnvironmentKit.Rock(root, OnGround(x, z), s, rng.Next(), s.y > 0.7f);
            }

            // Landmarks.
            EnvironmentKit.MoonGate(root, OnGround(0f, -66f), 0f, 1.2f);
            EnvironmentKit.MoonGate(root, OnGround(0f, 40f), 0f, 1.4f);
            EnvironmentKit.TempleRuins(root, OnGround(0f, 70f), 180f);
            for (float z = -88f; z < 40f; z += 14f)
            {
                if (Mathf.Abs(z - StreamZ) < 6f) continue;
                EnvironmentKit.StoneLantern(root, OnGround(-4.5f, z), true);
                EnvironmentKit.StoneLantern(root, OnGround(4.5f, z + 7f), true);
            }
            foreach (float a in new[] { 0f, 60f, 120f, 180f, 240f, 300f })
            {
                Vector3 p = new Vector3(Courtyard.x, 0f, Courtyard.y) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 20f;
                EnvironmentKit.StoneLantern(root, OnGround(p.x, p.z), true);
            }

            // Breakables.
            for (int i = 0; i < 4; i++) EnvironmentKit.Crate(root, OnGround(9f + i * 1.1f, -48f));
            EnvironmentKit.Crate(root, OnGround(9.5f, -46.9f));
            for (int i = 0; i < 4; i++) EnvironmentKit.Vase(root, OnGround(-10f, 4f + i * 1.3f));
            for (int i = 0; i < 3; i++) EnvironmentKit.Crate(root, OnGround(12f, 12f + i * 1.1f));
            EnvironmentKit.SmallShrine(root, OnGround(-9f, 52f), 90f);
            EnvironmentKit.SmallShrine(root, OnGround(9f, 52f), -90f);

            EnvironmentKit.LightShafts(root, new Vector3(0f, 0f, 10f), MoonDir, 6, 40f);
            EnvironmentKit.AmbientParticles(root, new Vector3(0f, 0f, 0f), new Vector3(90f, 8f, 170f), true, true, true);
            Debug.Log($"[MoonlitForest] Built forest with {trees} trees.");
        }

        protected override void ApplyAtmosphere() => EnvironmentKit.ApplyNightAtmosphere(WorldRoot, MoonDir, 0.017f);

        protected override void SpawnActors(PlayerController player)
        {
            var db = Db;
            var nightspawn = db.FindEnemy("nightspawn");
            var boss = db.FindEnemy("hollow_oni");

            // Checkpoints.
            var cp1 = new GameObject("Checkpoint_Entrance").AddComponent<Checkpoint>();
            cp1.transform.SetPositionAndRotation(OnGround(3f, -80f), Quaternion.identity);
            EnvironmentKit.StoneLantern(cp1.transform, cp1.transform.position, true);
            var cp2 = new GameObject("Checkpoint_Temple").AddComponent<Checkpoint>();
            cp2.transform.SetPositionAndRotation(OnGround(3f, 34f), Quaternion.identity);
            EnvironmentKit.StoneLantern(cp2.transform, cp2.transform.position, true);

            // Clearing A: a small pack.
            SpawnGroup(nightspawn, ClearingA, 2, 4f);
            // Clearing B: the main fight.
            SpawnGroup(nightspawn, ClearingB, 4, 7f);
            // Stragglers on the path.
            SpawnGroup(nightspawn, new Vector2(3f, 28f), 1, 0f);

            // Boss in the temple courtyard.
            var oni = EnemyFactory.Spawn(boss, OnGround(Courtyard.x, Courtyard.y + 4f) + Vector3.up * 0.1f, Quaternion.Euler(0f, 180f, 0f), false);
            var trigger = new GameObject("BossArena").AddComponent<BossArenaTrigger>();
            trigger.transform.position = new Vector3(Courtyard.x, 0f, Courtyard.y);
            trigger.Boss = oni.GetComponent<BossController>();
            trigger.Radius = 17f;

            GameEvents.Notify("MOONLIT FOREST — reach the abandoned temple");
        }

        private static void SpawnGroup(Data.EnemyData data, Vector2 center, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)Mathf.Max(1, count) * Mathf.PI * 2f + 0.4f;
                float x = center.x + Mathf.Cos(a) * radius;
                float z = center.y + Mathf.Sin(a) * radius;
                EnemyFactory.Spawn(data, OnGround(x, z) + Vector3.up * 0.1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), false);
            }
        }
    }
}
