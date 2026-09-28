using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Data
{
    public enum AudioCategory
    {
        Master = 0,
        Music = 1,
        SFX = 2,
        Voice = 3,
        Ambient = 4
    }

    /// <summary>
    /// Maps sound ids to real audio clips. Ids without an entry fall back to procedural placeholders,
    /// so real sounds can be added one by one.
    /// </summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibraryData : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public AudioCategory category = AudioCategory.SFX;
            public AudioClip[] clips;
            [Range(0f, 2f)] public float volume = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string id)
        {
            foreach (var e in entries) if (e != null && e.id == id && e.clips != null && e.clips.Length > 0) return e;
            return null;
        }
    }
}
