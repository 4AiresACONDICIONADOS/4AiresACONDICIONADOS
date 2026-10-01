using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.UI;
using UnityEngine;

namespace BreathOfEclipse.Environment
{
    /// <summary>
    /// Base for playable scenes: builds (or reuses baked) world geometry, then spawns the runtime actors —
    /// camera, player, HUD, pause menu — so every scene is playable directly with PLAY.
    /// </summary>
    public abstract class GameplaySceneBuilder : MonoBehaviour
    {
        [Tooltip("Build the procedural environment at runtime. Disabled automatically when the environment was baked into the scene.")]
        [SerializeField] protected bool buildEnvironment = true;
        [SerializeField] protected Vector3 playerSpawn = Vector3.zero;
        [SerializeField] protected float playerSpawnYaw;
        [SerializeField] protected string music = "forest";

        protected GameDatabase Db => GameManager.Instance != null ? GameManager.Instance.Database : DefaultContent.Build();
        protected Transform WorldRoot { get; private set; }
        protected PlayerController Player { get; private set; }
        protected CameraRig Cam { get; private set; }

        private void Awake()
        {
            // Black until player, camera, world, lighting and HUD exist and their shaders are compiled.
            if (SceneLoader.Instance != null) SceneLoader.Instance.HoldBlackUntilReady();
            var existing = transform.Find("World");
            if (existing != null)
            {
                WorldRoot = existing;
            }
            else
            {
                WorldRoot = new GameObject("World").transform;
                WorldRoot.SetParent(transform, false);
                if (buildEnvironment) BuildEnvironment(WorldRoot);
            }
            // Atmosphere settings are not serialized with baked objects; always apply them.
            ApplyAtmosphere();

            Cam = CameraRig.Create();
            Player = PlayerFactory.Create(Db, GroundedSpawn(playerSpawn), Quaternion.Euler(0f, playerSpawnYaw, 0f));
            PlayerFactory.BindCamera(Cam, Player);
            var hud = HUDController.Create();
            DamageNumbers.Create(hud.GetComponent<Canvas>());
            PauseMenu.Create();
            SpawnActors(Player);
        }

        private void Start()
        {
            CursorManager.SetGameplay(true);
            if (InputReader.Instance != null) InputReader.Instance.SetGameplayEnabled(true);
            if (!string.IsNullOrEmpty(music)) Sfx.Music(music, 2f);
            if (AudioManager.Instance != null && !string.IsNullOrEmpty(AmbientLoop)) AudioManager.Instance.PlayAmbient(AmbientLoop);
            GameEvents.RaiseCameraModeChanged(Cam.Mode.ToString());
        }

        /// <summary>Places the spawn point on the ground below/above it so procedural terrain never traps the player.</summary>
        protected static Vector3 GroundedSpawn(Vector3 point)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(point + Vector3.up * 60f, Vector3.down, out var hit, 200f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return point;
        }

        /// <summary>Ambient loop started with the scene (null: the scene manages its own ambience).</summary>
        protected virtual string AmbientLoop => "ambient_forest";

        /// <summary>Static world (terrain, props). Called only when nothing is baked in the scene.</summary>
        public abstract void BuildEnvironment(Transform root);

        /// <summary>Lighting / fog / sky settings (always runtime).</summary>
        protected abstract void ApplyAtmosphere();

        /// <summary>Enemies, bosses, interactables that need the player.</summary>
        protected abstract void SpawnActors(PlayerController player);
    }
}
