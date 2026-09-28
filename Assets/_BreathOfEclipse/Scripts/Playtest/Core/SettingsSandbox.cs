using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// During a playtest session all settings writes (camera mode, style, test values) go to an in-memory copy
    /// of the player's settings; the real settings file is untouched and reloaded when the session ends.
    /// </summary>
    public sealed class SettingsSandbox
    {
        private ISaveStorage _original;
        public bool Active { get; private set; }

        public void Enter()
        {
            if (Active) return;
            _original = SaveSystem.Storage;
            var memory = new MemorySaveStorage();
            memory.Write(SaveSystem.SettingsKey, JsonUtility.ToJson(SaveSystem.Settings));
            SaveSystem.Storage = memory;
            Active = true;
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            SaveSystem.Storage = _original;
            SaveSystem.LoadSettings();
            SaveSystem.NotifyChanged();
        }
    }
}
