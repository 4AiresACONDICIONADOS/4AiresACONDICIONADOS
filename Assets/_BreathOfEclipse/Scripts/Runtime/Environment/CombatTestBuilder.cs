using BreathOfEclipse.AI;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Environment
{
    /// <summary>
    /// 03_CombatTest: a simple night arena to test systems fast — training dummies, destructibles, pillars and
    /// ramps for camera collision, and shrines to summon Nightspawn packs or the Hollow Oni.
    /// </summary>
    public sealed class CombatTestBuilder : GameplaySceneBuilder
    {
        private static readonly Vector3 MoonDir = new Vector3(-0.35f, 0.55f, 0.75f);

        private void Reset()
        {
            playerSpawn = new Vector3(0f, 0.05f, -10f);
            music = "combat";
        }

        public override void BuildEnvironment(Transform root)
        {
            EnvironmentKit.CreateMoonLights(root, MoonDir, 1.8f);
            EnvironmentKit.SkyDome(root, MoonDir, 0.07f);
            EnvironmentKit.Ground(root, "ArenaGround", new Vector2(90f, 90f), 3f, null, new Color(0.34f, 0.36f, 0.44f), ProceduralTextures.Grid, 1f);

            var stone = MaterialFactory.Toon(new Color(0.4f, 0.41f, 0.47f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 1f);
            // Boundary walls.
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var wall = ProceduralMeshes.CreatePart("Wall" + i, PrimitiveType.Cube, stone, root, dir * 44f + Vector3.up * 2f, Quaternion.Euler(0f, a, 0f), new Vector3(90f, 4f, 1f));
                wall.AddComponent<BoxCollider>();
            }
            // Pillars (camera collision tests) and ramps / platforms (movement tests).
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 20f;
                var pillar = ProceduralMeshes.CreatePart("Pillar" + i, PrimitiveType.Cylinder, stone, root, p + Vector3.up * 3f, Vector3.zero, new Vector3(1.4f, 3f, 1.4f));
                pillar.AddComponent<CapsuleCollider>();
            }
            var ramp = ProceduralMeshes.CreatePart("Ramp", PrimitiveType.Cube, stone, root, new Vector3(-24f, 1.2f, 18f), new Vector3(-16f, 0f, 0f), new Vector3(6f, 0.4f, 9f));
            ramp.AddComponent<BoxCollider>();
            var platform = ProceduralMeshes.CreatePart("Platform", PrimitiveType.Cube, stone, root, new Vector3(-24f, 1.25f, 26.5f), Vector3.zero, new Vector3(8f, 2.5f, 8f));
            platform.AddComponent<BoxCollider>();
            var high = ProceduralMeshes.CreatePart("HighPlatform", PrimitiveType.Cube, stone, root, new Vector3(-14f, 2.4f, 30f), Vector3.zero, new Vector3(5f, 4.8f, 5f));
            high.AddComponent<BoxCollider>();

            // Destructibles corner.
            for (int i = 0; i < 6; i++) EnvironmentKit.Crate(root, new Vector3(18f + (i % 3) * 1.1f, 0f, -18f + (i / 3) * 1.1f));
            EnvironmentKit.Crate(root, new Vector3(19.1f, 0.9f, -17.4f));
            for (int i = 0; i < 5; i++) EnvironmentKit.Vase(root, new Vector3(14f + i * 1.2f, 0f, -22f));
            EnvironmentKit.SmallShrine(root, new Vector3(22f, 0f, -24f), 180f);

            EnvironmentKit.StoneLantern(root, new Vector3(-6f, 0f, -6f), true);
            EnvironmentKit.StoneLantern(root, new Vector3(6f, 0f, -6f), true);
            EnvironmentKit.MoonGate(root, new Vector3(0f, 0f, 32f), 180f, 1.2f);
            for (int i = 0; i < 18; i++)
            {
                float a = i * 20f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 40f;
                EnvironmentKit.Tree(root, p, 1.3f, i * 31, new Color(0.14f, 0.24f, 0.26f));
            }
            EnvironmentKit.AmbientParticles(root, Vector3.zero, new Vector3(70f, 6f, 70f), true, true, true);
        }

        protected override void ApplyAtmosphere()
        {
            EnvironmentKit.ApplyNightAtmosphere(WorldRoot, MoonDir, 0.009f);
        }

        protected override void SpawnActors(PlayerController player)
        {
            var db = Db;
            // Training dummies in a line.
            for (int i = 0; i < 3; i++) TrainingDummy.Create(new Vector3(-4f + i * 4f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));

            var nightspawn = db.FindEnemy("nightspawn");
            var boss = db.FindEnemy("hollow_oni");
            var spawner = new GameObject("PackSpawner").AddComponent<WaveSpawner>();
            spawner.transform.position = new Vector3(0f, 0f, 14f);
            spawner.Enemy = nightspawn;
            spawner.Count = 3;
            spawner.Radius = 5f;

            ShrineInteractable.Create(WorldRoot, new Vector3(-8f, 0f, -12f), "Summon a Nightspawn pack", new Color(0.9f, 0.2f, 0.35f), p =>
            {
                spawner.SpawnWave();
                GameEvents.Notify("A pack of Nightspawn emerges!");
            });
            ShrineInteractable.Create(WorldRoot, new Vector3(-12f, 0f, -12f), "Summon a single Nightspawn", new Color(0.7f, 0.3f, 0.9f), p =>
            {
                var e = EnemyFactory.Spawn(nightspawn, p.transform.position + p.transform.forward * 7f, Quaternion.LookRotation(-p.transform.forward));
                e.Alert();
            });
            ShrineInteractable.Create(WorldRoot, new Vector3(8f, 0f, -12f), "Challenge THE HOLLOW ONI", new Color(1f, 0.35f, 0.1f), p =>
            {
                if (FindAnyObjectByType<BossController>() != null)
                {
                    GameEvents.Notify("The Hollow Oni is already here.");
                    return;
                }
                var oni = EnemyFactory.Spawn(boss, new Vector3(0f, 0.05f, 16f), Quaternion.Euler(0f, 180f, 0f));
                var bc = oni.GetComponent<BossController>();
                if (bc != null) bc.StartEncounter();
            });
            ShrineInteractable.Create(WorldRoot, new Vector3(12f, 0f, -12f), "Meditate (restore HP, stamina and BREATH)", new Color(0.4f, 0.8f, 1f), p =>
            {
                p.Damageable.Health.Revive(1f);
                p.Stats.Stamina.Refill();
                p.Stats.Breath.SetValue(p.Stats.Breath.Max);
                p.Breathing.Cooldowns.ResetAll();
                GameEvents.Notify("Breath restored.");
            });
            GameEvents.Notify("TRAINING GROUND — use the shrines [E] to summon enemies");
        }
    }
}
