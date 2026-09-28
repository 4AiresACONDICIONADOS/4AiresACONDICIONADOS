using System;
using System.IO;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>Where save data lives. Swappable for tests and future platforms.</summary>
    public interface ISaveStorage
    {
        bool Exists(string key);
        string Read(string key);
        void Write(string key, string content);
        void Delete(string key);
    }

    /// <summary>JSON files under Application.persistentDataPath/BreathOfEclipse.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _root;

        public FileSaveStorage(string root = null)
        {
            _root = root ?? Path.Combine(Application.persistentDataPath, "BreathOfEclipse");
        }

        private string PathFor(string key) => Path.Combine(_root, key + ".json");

        public bool Exists(string key) => File.Exists(PathFor(key));

        public string Read(string key) => File.ReadAllText(PathFor(key));

        public void Write(string key, string content)
        {
            Directory.CreateDirectory(_root);
            string path = PathFor(key);
            string tmp = path + ".tmp";
            // Write to a temp file first so a crash mid-write never corrupts the previous save.
            File.WriteAllText(tmp, content);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public void Delete(string key)
        {
            string path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>In-memory storage used by tests.</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _data = new System.Collections.Generic.Dictionary<string, string>();
        public bool Exists(string key) => _data.ContainsKey(key);
        public string Read(string key) => _data[key];
        public void Write(string key, string content) => _data[key] = content;
        public void Delete(string key) => _data.Remove(key);
    }

    /// <summary>
    /// Local persistence for settings (audio, controls, sensitivity, graphics, last equipped style).
    /// Campaign progress is intentionally out of scope for the vertical slice.
    /// </summary>
    public static class SaveSystem
    {
        public const string SettingsKey = "settings";

        private static ISaveStorage _storage;
        private static GameSettings _settings;

        /// <summary>Raised whenever settings are applied (menu changes, load).</summary>
        public static event Action<GameSettings> SettingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _storage = null;
            _settings = null;
            SettingsChanged = null;
        }

        public static ISaveStorage Storage
        {
            get => _storage ?? (_storage = new FileSaveStorage());
            set => _storage = value;
        }

        /// <summary>Current settings (loaded lazily).</summary>
        public static GameSettings Settings => _settings ?? (_settings = LoadSettings());

        public static GameSettings LoadSettings()
        {
            var settings = new GameSettings();
            try
            {
                if (Storage.Exists(SettingsKey))
                {
                    string json = Storage.Read(SettingsKey);
                    if (!string.IsNullOrWhiteSpace(json)) JsonUtility.FromJsonOverwrite(json, settings);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not read settings, using defaults. {e.Message}");
                settings = new GameSettings();
            }
            settings.Sanitize();
            _settings = settings;
            return settings;
        }

        public static void SaveSettings()
        {
            if (_settings == null) return;
            try
            {
                _settings.Sanitize();
                Storage.Write(SettingsKey, JsonUtility.ToJson(_settings, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not write settings. {e.Message}");
            }
        }

        /// <summary>Notifies listeners and persists. Call after modifying <see cref="Settings"/>.</summary>
        public static void ApplyAndSave()
        {
            SettingsChanged?.Invoke(Settings);
            SaveSettings();
        }

        /// <summary>Notifies listeners without writing to disk (live slider previews).</summary>
        public static void NotifyChanged() => SettingsChanged?.Invoke(Settings);

        public static void ResetToDefaults()
        {
            _settings = new GameSettings();
            ApplyAndSave();
        }

        /// <summary>For tests: replace in-memory settings.</summary>
        public static void OverrideSettings(GameSettings settings) => _settings = settings;
    }
}
