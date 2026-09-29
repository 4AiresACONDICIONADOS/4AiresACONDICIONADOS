using System.Collections.Generic;
using System.IO;
using BreathOfEclipse.Data;
using UnityEditor;
using UnityEngine;

namespace BreathOfEclipse.EditorTools
{
    /// <summary>
    /// Writes the code-defined default content (<see cref="DefaultContent"/>) to editable ScriptableObject assets:
    /// player, weapon, combos + attacks, the five breathing styles + their techniques, enemies and the
    /// GameDatabase in Resources. After exporting, designers tweak the assets; the runtime loads the database
    /// asset instead of the code defaults.
    /// </summary>
    [InitializeOnLoad]
    public static class ContentExporter
    {
        public const string DataRoot = "Assets/_BreathOfEclipse/Data";
        public const string DatabasePath = "Assets/_BreathOfEclipse/Core/Resources/BreathOfEclipse/GameDatabase.asset";
        private const string AutoExportSessionKey = "BoE_AutoExportChecked";

        static ContentExporter()
        {
            // First time the project is opened: create the editable data assets automatically.
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(AutoExportSessionKey, false)) return;
                SessionState.SetBool(AutoExportSessionKey, true);
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var existing = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
                if (existing != null && existing.contentVersion < DefaultContent.ContentVersion)
                {
                    Debug.Log($"[Breath of Eclipse] Data assets are content v{existing.contentVersion}; exporting v{DefaultContent.ContentVersion} " +
                              "(new forms and voice lines). Previous versions stay in version control.");
                    Export();
                    return;
                }
                if (existing != null) return;
                if (AssetDatabase.IsValidFolder(DataRoot))
                {
                    Debug.LogWarning("[Breath of Eclipse] GameDatabase asset is missing but " + DataRoot + " exists. The game uses code defaults. " +
                                     "Use 'Breath of Eclipse/Data/Export Default Content (overwrite)' to regenerate the data assets.");
                    return;
                }
                Debug.Log("[Breath of Eclipse] First open: exporting default content to editable ScriptableObject assets.");
                Export();
            };
        }

        [MenuItem("Breath of Eclipse/Data/Export Default Content (overwrite)", priority = 10)]
        private static void ExportOverwriteMenu()
        {
            if (!EditorUtility.DisplayDialog("Export Default Content",
                    "This regenerates all data assets under " + DataRoot + " and the GameDatabase from code defaults.\n\n" +
                    "Manual edits to those assets will be lost (use version control to keep them). Continue?", "Export", "Cancel"))
                return;
            Export();
        }

        [MenuItem("Breath of Eclipse/Data/Select Game Database", priority = 11)]
        private static void SelectDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null)
            {
                EditorUtility.DisplayDialog("Game Database", "No GameDatabase asset yet. Use 'Export Default Content' first.", "OK");
                return;
            }
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        /// <summary>Exports (and overwrites) every default content asset.</summary>
        public static void Export()
        {
            var db = DefaultContent.Build();
            // Hand-assigned references survive a re-export (imported character, prefab VFX overrides).
            var previous = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (previous != null)
            {
                db.playerVisual = previous.playerVisual;
                foreach (var v in previous.vfxOverrides)
                    if (v != null && !db.vfxOverrides.Contains(v)) db.vfxOverrides.Add(v);
            }
            EnsureFolder(DataRoot);
            var written = new HashSet<Object>();

            Save(db.player, $"{DataRoot}/Player/PlayerData.asset", written);
            Save(db.playerWeapon, $"{DataRoot}/Player/Weapon_{Sanitize(db.playerWeapon.name, "Katana")}.asset", written);
            foreach (var attack in CollectAttacks(db.playerCombos))
                Save(attack, $"{DataRoot}/Player/Attacks/Attack_{Sanitize(attack.attackId, "attack")}.asset", written);
            Save(db.playerCombos, $"{DataRoot}/Player/PlayerCombos.asset", written);

            foreach (var style in db.styles)
            {
                string folder = $"{DataRoot}/BreathingStyles/{Sanitize(style.styleId, "style")}";
                for (int i = 0; i < style.TechniqueCount; i++)
                {
                    var skill = style.GetTechnique(i);
                    if (skill != null) Save(skill, $"{folder}/Skill_{Sanitize(skill.skillId, "skill")}.asset", written);
                }
                Save(style, $"{folder}/Style_{Sanitize(style.styleId, "style")}.asset", written);
            }
            foreach (var enemy in db.enemies)
                Save(enemy, $"{DataRoot}/Enemies/Enemy_{Sanitize(enemy.enemyId, "enemy")}.asset", written);
            if (db.audioLibrary != null) Save(db.audioLibrary, $"{DataRoot}/Audio/AudioLibrary.asset", written);

            db.name = "GameDatabase";
            Save(db, DatabasePath, written);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Breath of Eclipse] Default content v{DefaultContent.ContentVersion} exported to {DataRoot} and {DatabasePath}.");
        }

        private static IEnumerable<AttackData> CollectAttacks(ComboData combos)
        {
            var set = new List<AttackData>();
            void Add(AttackData a)
            {
                if (a != null && !set.Contains(a)) set.Add(a);
            }
            foreach (var n in combos.nodes)
            {
                Add(n.attack);
                Add(n.nextLight);
                Add(n.nextHeavy);
            }
            Add(combos.groundLight); Add(combos.groundHeavy); Add(combos.dashLight); Add(combos.dashHeavy);
            Add(combos.airLight); Add(combos.airHeavy); Add(combos.perfectDodgeCounter); Add(combos.parryCounter);
            return set;
        }

        private static void Save(Object obj, string path, HashSet<Object> written)
        {
            if (obj == null || !written.Add(obj)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            obj.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(obj, path);
        }

        private static string Sanitize(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
