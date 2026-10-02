using System;
using System.Collections;

namespace BreathOfEclipse.Playtest
{
    /// <summary>What a playtest session does.</summary>
    public enum PlaytestMode
    {
        /// <summary>Normal play in CombatTest with a self-filling checklist, logging and performance capture.</summary>
        Manual = 0,
        /// <summary>Automated gameplay suite in CombatTest (player, combat, techniques, cameras, UI, audio, save).</summary>
        Auto = 1,
        /// <summary>Interactive lab: cast any of the 25 techniques, auto-repeat, playback speed, freeze, frame step.</summary>
        VfxLab = 2,
        /// <summary>Interactive lab: player, dummy and a Nightspawn with camera diagnostics (keys 1/2/3 and C).</summary>
        CameraLab = 3,
        /// <summary>Automated Nightspawn AI suite.</summary>
        AiTest = 4,
        /// <summary>Automated Hollow Oni suite (phase 1, phase 2, Eclipse Cleave, ultimate, defeat).</summary>
        BossTest = 5,
        /// <summary>Everything: Boot, Main Menu, CombatTest suites, AI, boss, UI, audio, save, Moonlit Forest.</summary>
        Full = 6,
        /// <summary>VFX lab preset: Tidal Breath, Rising Serpent on auto-repeat.</summary>
        RisingSerpentVisual = 7,
        /// <summary>v0.5 FULL WORLD TEST: the living world (04_FrontierRegion) end to end.</summary>
        WorldTest = 8
    }

    /// <summary>Pacing of automated tests.</summary>
    public enum PlaytestSpeed
    {
        /// <summary>Functional test with short observation pauses.</summary>
        Normal = 0,
        /// <summary>Minimal waits.</summary>
        Fast = 1,
        /// <summary>Normal game speed with long observation pauses to watch each technique.</summary>
        Visual = 2
    }

    public enum TestStatus
    {
        Pass = 0,
        Fail = 1,
        Warning = 2,
        NotTested = 3
    }

    [Serializable]
    public sealed class TestResult
    {
        public string category;
        public string name;
        public string status;
        public string details;
        public float time;

        [NonSerialized] public TestStatus Status;
    }

    /// <summary>One automated test: runs as a coroutine and reports one or more results through the context.</summary>
    public sealed class PlaytestStep
    {
        public string Category;
        public string Name;
        public Func<PlaytestContext, IEnumerator> Body;
        /// <summary>Real seconds before the step is failed as a timeout.</summary>
        public float Timeout = 30f;
        public bool RequiresPlayer = true;

        public PlaytestStep(string category, string name, Func<PlaytestContext, IEnumerator> body, float timeout = 30f, bool requiresPlayer = true)
        {
            Category = category;
            Name = name;
            Body = body;
            Timeout = timeout;
            RequiresPlayer = requiresPlayer;
        }
    }

    /// <summary>Outcome holder for waits (iterators cannot return values).</summary>
    public sealed class WaitResult
    {
        public bool Success;
        public float Elapsed;
    }

    public static class PlaytestCategories
    {
        public const string Boot = "BOOT";
        public const string MainMenu = "MAIN MENU";
        public const string Input = "INPUT";
        public const string Movement = "MOVEMENT";
        public const string Combat = "COMBAT";
        public const string Techniques = "TECHNIQUES";
        public const string Cameras = "CAMERAS";
        public const string Model = "3D MODEL";
        public const string Vfx = "VFX";
        public const string Enemies = "ENEMIES";
        public const string Boss = "BOSS";
        public const string Audio = "AUDIO";
        public const string UI = "UI";
        public const string Save = "SAVE";
        public const string Performance = "PERFORMANCE";
        public const string Scenes = "SCENES";
        public const string World = "WORLD";
        public const string Manual = "MANUAL CHECKLIST";

        /// <summary>Report order.</summary>
        public static readonly string[] Order =
        {
            Boot, MainMenu, Scenes, Input, Movement, Combat, Techniques, Cameras, Vfx, Enemies, Boss, Audio, UI, Save, World, Manual, Performance
        };
    }
}
