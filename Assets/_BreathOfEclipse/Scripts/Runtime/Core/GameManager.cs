using BreathOfEclipse.Audio;
using BreathOfEclipse.Data;
using BreathOfEclipse.UI;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Persistent root created before the first scene loads. It only wires independent services together
    /// (time, pooling, input, audio, scene loading, screen FX, debug tools, content database) — each service
    /// owns its own responsibility, so this never becomes a god object.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public const string Version = "0.4.0";

        public GameDatabase Database { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[BreathOfEclipse]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Services.Register(this);

            Layers.ConfigureCollisionMatrix();
            SaveSystem.LoadSettings();

            Database = LoadDatabase();
            VFXLibrary.Database = Database;

            gameObject.AddComponent<TimeController>();
            gameObject.AddComponent<PoolManager>();
            gameObject.AddComponent<InputReader>();
            var audio = gameObject.AddComponent<AudioManager>();
            if (Database.audioLibrary != null) audio.Library = Database.audioLibrary;
            gameObject.AddComponent<SceneLoader>();
            gameObject.AddComponent<FpsCounter>();
            gameObject.AddComponent<DebugMenu>();
            ScreenFX.Create(transform);
            FlashFrameSystem.Create(transform);
            gameObject.AddComponent<EventSystemBootstrap>();

            SaveSystem.SettingsChanged += ApplySettings;
            ApplySettings(SaveSystem.Settings);
            Debug.Log($"[Breath of Eclipse] v{Version} booted. Content v{Database.contentVersion}, {Database.styles.Count} breathing styles.");
        }

        private void OnDestroy()
        {
            SaveSystem.SettingsChanged -= ApplySettings;
            if (Instance == this)
            {
                Instance = null;
                Services.Unregister(this);
            }
        }

        private void OnApplicationQuit() => SaveSystem.SaveSettings();

        private static GameDatabase LoadDatabase()
        {
            var db = Resources.Load<GameDatabase>(GameDatabase.ResourcePath);
            if (db != null && db.styles.Count > 0 && db.player != null && db.playerCombos != null && db.playerWeapon != null)
            {
                if (db.contentVersion >= DefaultContent.ContentVersion) return db;
                // An older export would hide new content (e.g. the 7 Water forms): play with the code defaults instead.
                Debug.LogWarning($"[GameManager] GameDatabase asset is content v{db.contentVersion}; code defaults are v{DefaultContent.ContentVersion}. " +
                                 "Using the code defaults. 'Breath of Eclipse/Data/Export Default Content (overwrite)' refreshes the editable assets.");
            }
            return DefaultContent.Build();
        }

        private static void ApplySettings(GameSettings s)
        {
            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.vSync ? -1 : 60;
            int levels = QualitySettings.names.Length;
            if (levels > 0)
            {
                int level = s.Quality == GraphicsQuality.Low ? 0 : levels - 1;
                if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            }
            VFXQuality.Apply(s.Quality);
            if (FpsCounter.Instance != null) FpsCounter.Instance.Visible = s.showFps;
        }
    }
}
