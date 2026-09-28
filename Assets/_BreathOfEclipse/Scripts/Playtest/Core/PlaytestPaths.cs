using System.IO;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>
    /// Output folders. In the Editor they sit at the project root (next to Assets/, ignored by git);
    /// in development builds next to the executable.
    /// </summary>
    public static class PlaytestPaths
    {
        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string Reports => Path.Combine(Root, "PlaytestReports");
        public static string Captures => Path.Combine(Root, "PlaytestCaptures");

        public static string EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "None";
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            string s = new string(chars);
            while (s.Contains("__")) s = s.Replace("__", "_");
            return s.Trim('_');
        }
    }
}
