using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    public enum GraphicsQuality
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3
    }

    public enum DodgeInputMode
    {
        /// <summary>Dedicated dodge button (Left Alt / gamepad East).</summary>
        DedicatedButton = 0,
        /// <summary>Dedicated button, and Direction + Space dodges while locked on (Space still jumps otherwise).</summary>
        DirectionalJumpWhenLockedOn = 1,
        /// <summary>Direction + Space always dodges on the ground; Space without direction jumps.</summary>
        DirectionalJumpAlways = 2
    }

    /// <summary>Quick slots 1-4 chosen by the player for one breathing style (form indices, -1 = empty).</summary>
    [Serializable]
    public sealed class StyleQuickSlots
    {
        public string styleId = "";
        public int[] forms = { -1, -1, -1, -1 };
    }

    /// <summary>
    /// Player settings persisted by <see cref="SaveSystem"/>. Plain serializable data; add fields freely —
    /// JsonUtility keeps defaults for fields missing in older save files.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        [Header("Audio")]
        [Range(0f, 1f)] public float masterVolume = 0.9f;
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float ambientVolume = 0.7f;

        [Header("Controls")]
        [Range(0.1f, 4f)] public float mouseSensitivity = 1f;
        [Range(0.1f, 4f)] public float gamepadSensitivity = 1f;
        public bool invertY;
        public int dodgeInputMode = (int)DodgeInputMode.DirectionalJumpWhenLockedOn;
        /// <summary>Input System binding overrides JSON (rebinding).</summary>
        public string bindingOverrides = string.Empty;

        [Header("Camera")]
        [Range(50f, 90f)] public float fieldOfView = 60f;
        [Range(0f, 2f)] public float cameraShake = 1f;
        public bool autoRecenterCamera = true;
        public int lastCameraMode;

        [Header("Graphics")]
        public int graphicsQuality = (int)GraphicsQuality.High;
        public bool motionBlur = true;
        public bool vSync = true;

        [Header("Gameplay / UI")]
        public bool showDamageNumbers = true;
        public bool flashFramesEnabled = true;
        public bool skipUltimateCinematics;
        [Tooltip("The swordsman announces style, form and technique (Voice volume applies).")]
        public bool techniqueVoice = true;
        public bool techniqueSubtitles = true;
        [Tooltip("Small STYLE / FORM / NAME title while a technique starts.")]
        public bool techniqueTitles = true;
        [Tooltip("Voice clip folder under Resources/BreathOfEclipse/Voice (es-419 = Latin American Spanish).")]
        public string voiceLanguage = "es-419";
        public bool showFps;
        public string lastEquippedStyle = "tidal";
        [Tooltip("Per-style quick slot assignments made from the form wheel (hold F, press 1-4).")]
        public List<StyleQuickSlots> quickSlots = new List<StyleQuickSlots>();
        [Tooltip("0 = real 3D anime model (humanoid, real animations), 1 = procedural mannequin. Saves without it keep the 3D model.")]
        public int playerVisualMode;
        [Tooltip("0 = real 3D demons (humanoid), 1 = procedural demon mannequins.")]
        public int demonVisualMode;

        /// <summary>The saved quick slots of a style, or null when the player never changed them.</summary>
        public StyleQuickSlots FindQuickSlots(string styleId)
        {
            if (quickSlots == null || string.IsNullOrEmpty(styleId)) return null;
            foreach (var q in quickSlots) if (q != null && q.styleId == styleId) return q;
            return null;
        }

        public GraphicsQuality Quality
        {
            get => (GraphicsQuality)Mathf.Clamp(graphicsQuality, 0, 3);
            set => graphicsQuality = (int)value;
        }

        public DodgeInputMode DodgeMode
        {
            get => (DodgeInputMode)Mathf.Clamp(dodgeInputMode, 0, 2);
            set => dodgeInputMode = (int)value;
        }

        /// <summary>Clamps values loaded from disk so a corrupted file cannot break the game.</summary>
        public void Sanitize()
        {
            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            if (string.IsNullOrEmpty(voiceLanguage)) voiceLanguage = "es-419";
            sfxVolume = Mathf.Clamp01(sfxVolume);
            voiceVolume = Mathf.Clamp01(voiceVolume);
            ambientVolume = Mathf.Clamp01(ambientVolume);
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.1f, 4f);
            gamepadSensitivity = Mathf.Clamp(gamepadSensitivity, 0.1f, 4f);
            fieldOfView = Mathf.Clamp(fieldOfView, 50f, 90f);
            cameraShake = Mathf.Clamp(cameraShake, 0f, 2f);
            graphicsQuality = Mathf.Clamp(graphicsQuality, 0, 3);
            dodgeInputMode = Mathf.Clamp(dodgeInputMode, 0, 2);
            lastCameraMode = Mathf.Clamp(lastCameraMode, 0, 2);
            if (bindingOverrides == null) bindingOverrides = string.Empty;
            if (string.IsNullOrEmpty(lastEquippedStyle)) lastEquippedStyle = "tidal";
            if (quickSlots == null) quickSlots = new List<StyleQuickSlots>();
            quickSlots.RemoveAll(q => q == null || string.IsNullOrEmpty(q.styleId) || q.forms == null);
            playerVisualMode = Mathf.Clamp(playerVisualMode, 0, 1);
            demonVisualMode = Mathf.Clamp(demonVisualMode, 0, 1);
            version = CurrentVersion;
        }

        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));
    }
}
