using System;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Persistence of the living world (separate file from settings). Versioned: <see cref="WorldStateDatabase.Migrate"/>
    /// fills defaults for anything an older save lacks; an unreadable save is kept aside and a fresh world starts.
    /// </summary>
    public static class WorldSave
    {
        public const string Key = "world_frontier";

        public static bool Exists
        {
            get
            {
                try { return SaveSystem.Storage.Exists(Key); }
                catch (Exception) { return false; }
            }
        }

        /// <summary>The saved world, or null when there is none (or it cannot be read).</summary>
        public static WorldStateData Load()
        {
            try
            {
                if (!SaveSystem.Storage.Exists(Key)) return null;
                string json = SaveSystem.Storage.Read(Key);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var data = JsonUtility.FromJson<WorldStateData>(json);
                if (data == null) return null;
                new WorldStateDatabase(data); // migrate in place
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldSave] Could not read the world save ({e.Message}); starting a fresh world.");
                try { SaveSystem.Storage.Write(Key + "_unreadable", SaveSystem.Storage.Read(Key)); } catch (Exception) { }
                return null;
            }
        }

        public static bool Save(WorldStateData data)
        {
            if (data == null) return false;
            try
            {
                data.version = WorldStateData.CurrentVersion;
                SaveSystem.Storage.Write(Key, JsonUtility.ToJson(data));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldSave] Save failed: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try { SaveSystem.Storage.Delete(Key); }
            catch (Exception e) { Debug.LogWarning($"[WorldSave] Delete failed: {e.Message}"); }
        }

        /// <summary>Set by the main menu: start the next Living World session from a fresh world.</summary>
        public static bool StartFresh { get; set; }
    }
}
