using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>Which locomotion clips a humanoid visual uses.</summary>
    public enum LocomotionSet
    {
        /// <summary>Player swordsman: sword idle, walk / jog / sprint, ninja jump.</summary>
        Swordsman = 0,
        /// <summary>Nightspawn: hunched zombie idle and walk, fast jog / sprint.</summary>
        Demon = 1,
        /// <summary>Hollow Oni: heavy weapon stance, slow walk, charging jog.</summary>
        Brute = 2
    }

    /// <summary>A real clip segment standing in for a procedural motion id (normalized clip times).</summary>
    public readonly struct ClipSegment
    {
        public readonly string Clip;
        public readonly float Start;
        /// <summary>Fastest point of the swing: lands at the start of the attack's active phase (+35%).</summary>
        public readonly float Contact;
        public readonly float End;
        /// <summary>How much the retargeted procedural torso overrides the clip's torso (0 = clip, 1 = procedural).</summary>
        public readonly float TorsoProcedural;
        /// <summary>Legs come from the clip (false: procedural feet, e.g. airborne techniques).</summary>
        public readonly bool ClipLegs;
        public readonly bool Loop;

        public ClipSegment(string clip, float start, float contact, float end, float torsoProcedural = 0.6f, bool clipLegs = true, bool loop = false)
        {
            Clip = clip;
            Start = start;
            Contact = contact;
            End = end;
            TorsoProcedural = torsoProcedural;
            ClipLegs = clipLegs;
            Loop = loop;
        }

        public bool IsValid => !string.IsNullOrEmpty(Clip);
    }

    /// <summary>
    /// Real humanoid animation clips (Quaternius Universal Animation Library 1 + 2, CC0) loaded from Resources and
    /// retargeted through Unity's Humanoid avatars, plus the table that maps the game's motion ids to clip segments.
    /// Timings (contact frames, natural locomotion speeds) were measured from the clips themselves.
    /// </summary>
    public static class HumanoidClipLibrary
    {
        public static readonly string[] Sources =
        {
            "Quaternius/Animations/UAL1_Standard",
            "Quaternius/Animations/UAL2_Standard"
        };

        /// <summary>Ground speed (m/s) of each locomotion cycle for a 1.75 m character (measured on the root-motion versions).</summary>
        public static readonly Dictionary<string, float> NaturalSpeed = new Dictionary<string, float>
        {
            { "Walk_Loop", 0.97f }, { "Jog_Fwd_Loop", 5.36f }, { "Sprint_Loop", 8.25f }, { "Crouch_Fwd_Loop", 0.75f },
            { "Zombie_Walk_Fwd_Loop", 1.05f }, { "Walk_Formal_Loop", 0.97f }
        };

        public const float ReferenceHeight = 1.75f;

        /// <summary>Motion id → clip segment. Ids without an entry are fully procedural (retargeted pose).</summary>
        private static readonly Dictionary<string, ClipSegment> Segments = new Dictionary<string, ClipSegment>
        {
            // player combo / heavy
            { "L1", new ClipSegment("Sword_Regular_A", 0f, 0.54f, 1f) },
            { "L2", new ClipSegment("Sword_Regular_B", 0f, 0.47f, 1f) },
            { "L3", new ClipSegment("Sword_Regular_C", 0.15f, 0.32f, 0.55f) },
            { "L4", new ClipSegment("Sword_Attack", 0f, 0.24f, 0.5f) },
            { "H1", new ClipSegment("Sword_Heavy_Combo", 0.5f, 0.58f, 0.75f) },
            { "H2", new ClipSegment("Sword_Heavy_Combo", 0.35f, 0.42f, 0.5f) },
            { "L2H", new ClipSegment("Sword_Heavy_Combo", 0.18f, 0.27f, 0.35f) },
            { "L1H", new ClipSegment("Sword_Regular_Combo", 0.3f, 0.41f, 0.47f) },
            { "L1HH", new ClipSegment("Sword_Heavy_Combo", 0f, 0.08f, 0.18f) },
            { "DashL", new ClipSegment("Sword_Dash", 0.1f, 0.2f, 0.5f) },
            { "PDCounter", new ClipSegment("Melee_Hook", 0f, 0.5f, 1f) },
            { "Riposte", new ClipSegment("Sword_Regular_Combo", 0.15f, 0.24f, 0.33f) },
            { "Parry", new ClipSegment("Sword_Block", 0f, 0.07f, 0.45f, 0.5f) },
            // techniques that have a matching body motion (the rest keep the fully retargeted pose)
            { "SkillThrow", new ClipSegment("OverhandThrow", 0f, 0.27f, 0.6f, 0.7f) },
            { "SkillOverhead", new ClipSegment("Sword_Attack", 0f, 0.24f, 0.5f, 0.7f) },
            { "SkillThrust", new ClipSegment("Sword_Dash", 0.1f, 0.2f, 0.5f, 0.7f) },
            { "SkillDash", new ClipSegment("Sword_Dash", 0.05f, 0.2f, 0.5f, 0.8f) },
            { "SkillIaiDraw", new ClipSegment("Sword_Regular_B", 0f, 0.47f, 1f, 0.75f) },
            { "SkillLowStance", new ClipSegment("Crouch_Idle_Loop", 0f, 0.5f, 1f, 0.85f, true, true) },
            { "SkillFocus", new ClipSegment("Crouch_Idle_Loop", 0f, 0.5f, 1f, 0.85f, true, true) },
            // Nightspawn
            { "EnemySwipe", new ClipSegment("Zombie_Scratch", 0.1f, 0.31f, 0.6f, 0.5f) },
            { "EnemySwipe2", new ClipSegment("Melee_Hook", 0f, 0.5f, 1f, 0.5f) },
            { "EnemyLunge", new ClipSegment("Sword_Dash", 0.1f, 0.2f, 0.5f, 0.5f) },
            { "EnemyHeavy", new ClipSegment("Sword_Attack", 0f, 0.24f, 0.55f, 0.5f) },
            { "EnemyLeap", new ClipSegment("NinjaJump_Start", 0f, 0.5f, 1f, 0.6f) },
            { "EnemyStagger", new ClipSegment("Idle_Shield_Break", 0f, 0.1f, 1f, 0.2f) },
            { "EnemyBlock", new ClipSegment("Sword_Block", 0f, 0.07f, 0.45f, 0.5f) },
            // Hollow Oni
            { "OniSmash", new ClipSegment("Sword_Attack", 0f, 0.24f, 0.55f, 0.5f) },
            { "OniSweep", new ClipSegment("Sword_Regular_C", 0.15f, 0.32f, 0.55f, 0.5f) },
            { "OniCleave", new ClipSegment("Sword_Heavy_Combo", 0.5f, 0.58f, 0.75f, 0.5f) },
            { "OniCleaveHold", new ClipSegment("Sword_Heavy_Combo", 0.46f, 0.5f, 0.52f, 0.6f) },
            { "OniThrow", new ClipSegment("OverhandThrow", 0f, 0.27f, 0.6f, 0.5f) },
            { "OniCharge", new ClipSegment("Sprint_Loop", 0f, 0.5f, 1f, 0.3f, true, true) },
            { "OniTransform", new ClipSegment("Idle_Shield_Break", 0f, 0.1f, 1f, 0.5f) },
        };

        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        private static bool _loaded;
        private static bool _humanoid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clips.Clear();
            _loaded = false;
            _humanoid = false;
        }

        /// <summary>True when the libraries were found and imported as Humanoid (retargetable) clips.</summary>
        public static bool Available
        {
            get
            {
                Load();
                return _humanoid;
            }
        }

        public static int Count
        {
            get
            {
                Load();
                return Clips.Count;
            }
        }

        public static IEnumerable<string> Names
        {
            get
            {
                Load();
                return Clips.Keys;
            }
        }

        /// <summary>Clip by its short name ("Idle_Loop"), null if missing.</summary>
        public static AnimationClip Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Load();
            return Clips.TryGetValue(name, out var c) ? c : null;
        }

        public static bool TryGetSegment(string motionId, out ClipSegment segment)
        {
            segment = default;
            if (string.IsNullOrEmpty(motionId)) return false;
            if (!Segments.TryGetValue(motionId, out segment)) return false;
            return Get(segment.Clip) != null;
        }

        /// <summary>Every clip a visual may need, for building its playable graph once.</summary>
        public static IEnumerable<string> SegmentClips()
        {
            foreach (var kv in Segments) yield return kv.Value.Clip;
        }

        /// <summary>"Armature|Idle_Loop" / "Rig|Idle_Loop" → "Idle_Loop".</summary>
        public static string ShortName(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return clipName;
            int bar = clipName.LastIndexOf('|');
            return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            int human = 0;
            foreach (var path in Sources)
            {
                AnimationClip[] loaded;
                try
                {
                    loaded = Resources.LoadAll<AnimationClip>(path);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[HumanoidClipLibrary] Could not load '{path}': {e.Message}");
                    continue;
                }
                foreach (var clip in loaded)
                {
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;
                    string key = ShortName(clip.name);
                    if (Clips.ContainsKey(key)) continue;
                    Clips[key] = clip;
                    if (clip.humanMotion) human++;
                }
            }
            _humanoid = human > 0 && Get("Idle_Loop") != null;
            if (Clips.Count == 0)
                Debug.LogWarning("[HumanoidClipLibrary] No animation clips found under Resources/Quaternius/Animations: humanoid visuals use the retargeted procedural animation only.");
            else if (!_humanoid)
                Debug.LogWarning($"[HumanoidClipLibrary] {Clips.Count} clips found but not imported as Humanoid (check the FBX Rig tab): humanoid visuals use the retargeted procedural animation only.");
        }
    }
}
