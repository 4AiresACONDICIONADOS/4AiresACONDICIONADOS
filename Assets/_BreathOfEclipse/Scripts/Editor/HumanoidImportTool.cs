using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BreathOfEclipse.EditorTools
{
    /// <summary>
    /// Imported humanoid characters without hand work:
    /// 1. Drop the model (VRoid → UniVRM prefab, or an FBX) and its animation FBX files (Mixamo, Quaternius UAL…)
    ///    into Assets/_BreathOfEclipse/Characters/Import/. FBX files there are imported as Humanoid automatically
    ///    and locomotion clips loop.
    /// 2. Select the model and run "Breath of Eclipse/Characters/Build Player Visual From Selection".
    ///    It builds an Animator Controller with the states the game expects (clips matched by name), a
    ///    CharacterVisualProfile, and assigns it to the GameDatabase. Motions without a clip fall back to the
    ///    "Attack" / "Skill" states (see MecanimCharacterAnimator); leave playerVisual empty to keep the mannequin.
    /// </summary>
    public static class HumanoidImportTool
    {
        public const string ImportFolder = "Assets/_BreathOfEclipse/Characters/Import";
        public const string GeneratedFolder = "Assets/_BreathOfEclipse/Characters/Generated";

        /// <summary>State name → keywords searched in clip names (lower case, first match wins, most specific first).</summary>
        private static readonly (string state, string[] keys)[] StateKeywords =
        {
            ("Idle", new[] { "combat idle", "combat_idle", "sword idle", "idle" }),
            ("Walk", new[] { "walk" }),
            ("Run", new[] { "run", "jog" }),
            ("Sprint", new[] { "sprint" }),
            ("Jump", new[] { "jump" }),
            ("Dodge", new[] { "dodge", "roll", "evade", "dash" }),
            ("Block", new[] { "block", "guard" }),
            ("Parry", new[] { "parry", "deflect" }),
            ("L1", new[] { "slash 1", "slash1", "slash_1", "attack 1", "attack1", "attack_1", "combo 1", "combo1", "light 1", "light1" }),
            ("L2", new[] { "slash 2", "slash2", "slash_2", "attack 2", "attack2", "attack_2", "combo 2", "combo2", "light 2", "light2" }),
            ("L3", new[] { "slash 3", "slash3", "slash_3", "attack 3", "attack3", "attack_3", "combo 3", "combo3", "light 3", "light3" }),
            ("H1", new[] { "heavy", "strong", "power attack", "great slash" }),
            ("SkillRisingCut", new[] { "launcher", "rising", "uppercut" }),
            ("A1", new[] { "air slash", "air attack", "jump attack", "aerial" }),
            ("Hit", new[] { "hit", "impact", "react", "damage" }),
            ("KnockedDown", new[] { "knockdown", "knock down", "knocked", "fall down" }),
            ("GetUp", new[] { "get up", "getup", "get_up", "stand up" }),
            ("SkillLowStance", new[] { "stance", "ready", "unsheath", "draw" }),
            ("SkillSheathe", new[] { "sheath" }),
            ("SkillInhale", new[] { "inhale", "breath" }),
            ("SkillRaise", new[] { "ultimate", "power up", "powerup", "charge" }),
            ("Dead", new[] { "death", "die", "dying" }),
        };

        /// <summary>Motion ids that fall back to another clip when their own is missing.</summary>
        private static readonly (string state, string from)[] Aliases =
        {
            ("L4", "L3"), ("L2H", "H1"), ("L1H", "H1"), ("L1HH", "H1"), ("H2", "H1"), ("DashL", "L1"), ("A2", "A1"), ("A3", "A1"), ("AH", "H1"),
            ("PDCounter", "L2"), ("Riposte", "L3"), ("SkillIaiDraw", "L1"), ("SkillDash", "Dodge"), ("SkillThrust", "L2"), ("SkillSpin", "L3"),
            ("SkillSpinLong", "L3"), ("SkillOverhead", "H1"), ("SkillFocus", "SkillLowStance"), ("SkillWhirl", "L3"), ("SkillThrow", "L2"),
            ("SkillFlip", "Jump"), ("SkillHover", "Jump"), ("SkillPlunge", "H1"), ("SkillAirReady", "Jump"), ("Attack", "L1"), ("Skill", "SkillLowStance")
        };

        private static readonly string[] LoopStates = { "Idle", "Walk", "Run", "Sprint", "Block", "Dead" };

        [MenuItem("Breath of Eclipse/Characters/Open Import Folder", priority = 60)]
        private static void OpenImportFolder()
        {
            EnsureFolder(ImportFolder);
            EditorUtility.RevealInFinder(ImportFolder);
        }

        [MenuItem("Breath of Eclipse/Characters/Build Player Visual From Selection", priority = 61)]
        private static void BuildFromSelection()
        {
            var model = Selection.activeObject as GameObject;
            if (model == null || !AssetDatabase.Contains(model))
            {
                EditorUtility.DisplayDialog("Build Player Visual", "Select the character's model prefab or FBX in the Project window first.", "OK");
                return;
            }
            var profile = Build(model, out var report);
            Debug.Log(report);
            if (profile != null)
            {
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
            }
        }

        [MenuItem("Breath of Eclipse/Characters/Use Procedural Mannequin", priority = 62)]
        private static void UseMannequin()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentExporter.DatabasePath);
            if (db == null) return;
            db.playerVisual = null;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("[Breath of Eclipse] Player visual cleared: the procedural mannequin is used again.");
        }

        /// <summary>Builds controller + profile for <paramref name="model"/> and assigns it to the GameDatabase.</summary>
        public static CharacterVisualProfile Build(GameObject model, out string report)
        {
            var sb = new StringBuilder();
            string modelPath = AssetDatabase.GetAssetPath(model);
            var animatorOnModel = model.GetComponentInChildren<Animator>();
            bool human = animatorOnModel != null && animatorOnModel.avatar != null && animatorOnModel.avatar.isHuman;
            sb.AppendLine($"[Breath of Eclipse] Player visual from {modelPath}: {(human ? "Humanoid avatar OK" : "WARNING: no Humanoid avatar (set Rig → Animation Type = Humanoid)")}");

            var clips = CollectClips(Path.GetDirectoryName(modelPath)?.Replace('\\', '/'));
            var chosen = new Dictionary<string, AnimationClip>();
            foreach (var (state, keys) in StateKeywords)
            {
                var clip = clips.FirstOrDefault(c => keys.Any(k => Normalize(c.name).Contains(k)) && !chosen.ContainsValue(c));
                if (clip != null) chosen[state] = clip;
            }
            foreach (var (state, from) in Aliases)
                if (!chosen.ContainsKey(state) && chosen.TryGetValue(from, out var clip)) chosen[state] = clip;

            EnsureFolder(GeneratedFolder);
            string baseName = Sanitize(model.name);
            string controllerPath = $"{GeneratedFolder}/{baseName}_Controller.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (var f in new[] { "Speed", "VelocityX", "VelocityZ" }) controller.AddParameter(f, AnimatorControllerParameterType.Float);
            foreach (var b in new[] { "Grounded", "Blocking", "Sprinting", "KnockedDown", "Dead" }) controller.AddParameter(b, AnimatorControllerParameterType.Bool);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            // Locomotion: 1D blend on Speed (m/s, same units as the player motor).
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            AddChild(tree, chosen, "Idle", 0f);
            AddChild(tree, chosen, "Walk", 2.4f);
            AddChild(tree, chosen, "Run", 6.2f);
            AddChild(tree, chosen, "Sprint", 9f);
            sm.defaultState = locomotion;

            var states = new Dictionary<string, AnimatorState>();
            foreach (var kv in chosen)
            {
                if (kv.Key == "Idle" || kv.Key == "Walk" || kv.Key == "Run" || kv.Key == "Sprint") continue;
                var st = sm.AddState(kv.Key);
                st.motion = kv.Value;
                states[kv.Key] = st;
                bool loops = LoopStates.Contains(kv.Key);
                if (!loops && kv.Key != "KnockedDown")
                {
                    var back = st.AddTransition(locomotion);
                    back.hasExitTime = true;
                    back.exitTime = 0.92f;
                    back.duration = 0.12f;
                }
            }
            if (states.TryGetValue("Hit", out var hit))
            {
                var t = sm.AddAnyStateTransition(hit);
                t.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                t.duration = 0.05f;
            }
            if (states.TryGetValue("Block", out var block))
            {
                var tIn = sm.AddAnyStateTransition(block);
                tIn.AddCondition(AnimatorConditionMode.If, 0f, "Blocking");
                tIn.canTransitionToSelf = false;
                tIn.duration = 0.08f;
                var tOut = block.AddTransition(locomotion);
                tOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "Blocking");
                tOut.duration = 0.1f;
            }
            if (states.TryGetValue("KnockedDown", out var down))
            {
                var tIn = sm.AddAnyStateTransition(down);
                tIn.AddCondition(AnimatorConditionMode.If, 0f, "KnockedDown");
                tIn.canTransitionToSelf = false;
                tIn.duration = 0.08f;
                var target = states.TryGetValue("GetUp", out var getUp) ? getUp : locomotion;
                var tOut = down.AddTransition(target);
                tOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "KnockedDown");
                tOut.duration = 0.12f;
            }
            if (states.TryGetValue("Dead", out var dead))
            {
                var t = sm.AddAnyStateTransition(dead);
                t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                t.canTransitionToSelf = false;
                t.duration = 0.1f;
            }

            string profilePath = $"{GeneratedFolder}/{baseName}_Visual.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CharacterVisualProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.modelPrefab = model;
            profile.controller = controller;
            EditorUtility.SetDirty(profile);

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentExporter.DatabasePath);
            if (db != null)
            {
                db.playerVisual = profile;
                EditorUtility.SetDirty(db);
                sb.AppendLine($"Assigned to {ContentExporter.DatabasePath} (playerVisual). 'Characters/Use Procedural Mannequin' reverts it.");
            }
            else
            {
                sb.AppendLine("No GameDatabase asset yet: run 'Data/Export Default Content' and run this again (or assign playerVisual by hand).");
            }
            AssetDatabase.SaveAssets();

            sb.AppendLine($"Controller: {controllerPath} · Profile: {profilePath}");
            sb.AppendLine("Clips: " + string.Join(", ", chosen.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value.name}")));
            var missing = StateKeywords.Select(s => s.state).Where(s => !chosen.ContainsKey(s)).ToList();
            if (missing.Count > 0) sb.AppendLine("No clip found for: " + string.Join(", ", missing) + " (those motions fall back to Attack / Skill / Locomotion).");
            sb.AppendLine("Next: press Play, check the katana pose in the hand (profile: weaponLocalPosition / weaponLocalEuler) and the scale.");
            report = sb.ToString();
            return profile;
        }

        private static void AddChild(BlendTree tree, Dictionary<string, AnimationClip> chosen, string state, float threshold)
        {
            if (chosen.TryGetValue(state, out var clip)) tree.AddChild(clip, threshold);
            else if (state == "Idle" && chosen.Count > 0) tree.AddChild(chosen.Values.First(), threshold);
        }

        /// <summary>Every animation clip in the model's folder, the import folder and their sub folders.</summary>
        private static List<AnimationClip> CollectClips(string modelFolder)
        {
            var folders = new List<string>();
            if (!string.IsNullOrEmpty(modelFolder) && AssetDatabase.IsValidFolder(modelFolder)) folders.Add(modelFolder);
            if (AssetDatabase.IsValidFolder(ImportFolder) && !folders.Contains(ImportFolder)) folders.Add(ImportFolder);
            var clips = new List<AnimationClip>();
            if (folders.Count == 0) return clips;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", folders.ToArray()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && !clips.Contains(clip)) clips.Add(clip);
            }
            // Shorter names first: "Idle" wins over "Idle To Run".
            clips.Sort((a, b) => a.name.Length.CompareTo(b.name.Length));
            return clips;
        }

        private static string Normalize(string name) => name.ToLowerInvariant().Replace('-', ' ').Replace('|', ' ');

        private static string Sanitize(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.Length > 0 ? sb.ToString() : "Character";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    /// <summary>FBX files dropped in the character import folder are imported as Humanoid; locomotion clips loop.</summary>
    public sealed class HumanoidImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(HumanoidImportTool.ImportFolder)) return;
            var importer = (ModelImporter)assetImporter;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(HumanoidImportTool.ImportFolder)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                bool loop = n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("sprint") || n.Contains("jog");
                c.loopTime = loop;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = loop;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
