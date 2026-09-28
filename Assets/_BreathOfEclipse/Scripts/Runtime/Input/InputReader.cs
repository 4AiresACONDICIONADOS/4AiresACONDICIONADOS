using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Reads the "BreathOfEclipseControls" Input Actions asset and exposes gameplay input in a form the game uses:
    /// continuous values (move, look), held states, buffered presses and discrete events.
    /// Nothing else in the project references keys directly.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class InputReader : MonoBehaviour
    {
        public const string ResourcePath = "BreathOfEclipse/Input/BreathOfEclipseControls";

        public static InputReader Instance { get; private set; }

        public InputActionAsset Asset { get; private set; }
        public InputBuffer Buffer { get; } = new InputBuffer(0.3f);

        /// <summary>Movement stick / WASD (-1..1).</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Look delta for this frame in degrees (yaw, pitch), sensitivity and inversion applied.</summary>
        public Vector2 LookDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool LightHeld { get; private set; }
        public bool HeavyHeld { get; private set; }
        /// <summary>True when the last look input came from a gamepad stick.</summary>
        public bool UsingGamepad { get; private set; }
        /// <summary>Unscaled time of the last look input (camera auto-recentering).</summary>
        public float LastLookTime { get; private set; }

        public bool GameplayEnabled => _gameplay != null && _gameplay.enabled;

        public event Action LockOnPressed;
        /// <summary>+1 = next target to the right, -1 = left.</summary>
        public event Action<int> SwitchTargetRequested;
        public event Action CameraModePressed;
        public event Action InteractPressed;
        public event Action PausePressed;
        public event Action DebugMenuPressed;
        public event Action ToggleFpsPressed;
        public event Action NextStylePressed;
        public event Action PrevStylePressed;
        /// <summary>Block button pressed this frame (parry timing starts here).</summary>
        public event Action BlockPressed;

        /// <summary>Mouse degrees per pixel at sensitivity 1.</summary>
        public float MouseDegreesPerPixel = 0.09f;
        /// <summary>Gamepad degrees per second at sensitivity 1.</summary>
        public Vector2 StickDegreesPerSecond = new Vector2(220f, 150f);

        private InputActionMap _gameplay;
        private InputActionMap _system;
        private InputAction _move, _look, _lookStick, _sprint, _jump, _dodge, _light, _heavy, _block, _lockOn, _switchTarget;
        private InputAction _skill1, _skill2, _skill3, _skill4, _ultimate, _cameraMode, _interact, _nextStyle, _prevStyle;
        private InputAction _pause, _debugMenu, _toggleFps;
        private float _switchCooldown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Services.Register(this);
            LoadAsset();
            SaveSystem.SettingsChanged += OnSettingsChanged;
        }

        private void OnDestroy()
        {
            SaveSystem.SettingsChanged -= OnSettingsChanged;
            if (Instance != this) return;
            if (Asset != null) Asset.Disable();
            Instance = null;
            Services.Unregister(this);
        }

        private void LoadAsset()
        {
            var loaded = Resources.Load<InputActionAsset>(ResourcePath);
            if (loaded != null)
            {
                // Clone so rebinding at runtime never modifies the project asset in the editor.
                Asset = Instantiate(loaded);
            }
            else
            {
                Debug.LogWarning("[InputReader] Input Actions asset not found in Resources, using built-in defaults.");
                Asset = InputActionAsset.FromJson(DefaultInputActions.Json);
            }

            _gameplay = Asset.FindActionMap("Gameplay", true);
            _system = Asset.FindActionMap("System", true);

            _move = _gameplay.FindAction("Move", true);
            _look = _gameplay.FindAction("Look", true);
            _lookStick = _gameplay.FindAction("LookStick", true);
            _sprint = _gameplay.FindAction("Sprint", true);
            _jump = _gameplay.FindAction("Jump", true);
            _dodge = _gameplay.FindAction("Dodge", true);
            _light = _gameplay.FindAction("LightAttack", true);
            _heavy = _gameplay.FindAction("HeavyAttack", true);
            _block = _gameplay.FindAction("Block", true);
            _lockOn = _gameplay.FindAction("LockOn", true);
            _switchTarget = _gameplay.FindAction("SwitchTarget", true);
            _skill1 = _gameplay.FindAction("Skill1", true);
            _skill2 = _gameplay.FindAction("Skill2", true);
            _skill3 = _gameplay.FindAction("Skill3", true);
            _skill4 = _gameplay.FindAction("Skill4", true);
            _ultimate = _gameplay.FindAction("Ultimate", true);
            _cameraMode = _gameplay.FindAction("CameraMode", true);
            _interact = _gameplay.FindAction("Interact", true);
            _nextStyle = _gameplay.FindAction("NextStyle", true);
            _prevStyle = _gameplay.FindAction("PrevStyle", true);
            _pause = _system.FindAction("Pause", true);
            _debugMenu = _system.FindAction("DebugMenu", true);
            _toggleFps = _system.FindAction("ToggleFps", true);

            ApplyBindingOverrides(SaveSystem.Settings.bindingOverrides);
            _system.Enable();
            _gameplay.Enable();
        }

        private void OnSettingsChanged(GameSettings settings) { }

        public void ApplyBindingOverrides(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                Asset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputReader] Invalid binding overrides ignored: {e.Message}");
            }
        }

        public string SaveBindingOverrides() => Asset.SaveBindingOverridesAsJson();

        public void ResetBindings()
        {
            Asset.RemoveAllBindingOverrides();
            SaveSystem.Settings.bindingOverrides = string.Empty;
            SaveSystem.SaveSettings();
        }

        /// <summary>Enables or disables gameplay actions (menus, cinematics). System actions stay enabled.</summary>
        public void SetGameplayEnabled(bool enabled)
        {
            if (_gameplay == null) return;
            if (enabled) _gameplay.Enable();
            else
            {
                _gameplay.Disable();
                Move = Vector2.zero;
                LookDelta = Vector2.zero;
                SprintHeld = BlockHeld = JumpHeld = LightHeld = HeavyHeld = false;
                Buffer.ClearAll();
            }
        }

        /// <summary>Human readable binding for HUD prompts (e.g. "1", "R", "LMB").</summary>
        public string GetBindingLabel(string actionName)
        {
            var action = Asset?.FindAction(actionName);
            if (action == null) return "?";
            return action.GetBindingDisplayString(0);
        }

        private void Update()
        {
            if (_system.enabled)
            {
                if (_pause.WasPressedThisFrame()) PausePressed?.Invoke();
                if (_debugMenu.WasPressedThisFrame()) DebugMenuPressed?.Invoke();
                if (_toggleFps.WasPressedThisFrame()) ToggleFpsPressed?.Invoke();
            }

            if (!_gameplay.enabled)
            {
                Move = Vector2.zero;
                LookDelta = Vector2.zero;
                return;
            }

            var settings = SaveSystem.Settings;
            float now = Time.unscaledTime;

            Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);

            Vector2 mouse = _look.ReadValue<Vector2>();
            Vector2 stick = _lookStick.ReadValue<Vector2>();
            Vector2 look = mouse * (MouseDegreesPerPixel * settings.mouseSensitivity);
            if (stick.sqrMagnitude > 0.02f)
            {
                // Response curve: precise near the center, fast at the edge.
                Vector2 curved = stick * stick.magnitude;
                look += Vector2.Scale(curved, StickDegreesPerSecond) * (settings.gamepadSensitivity * Time.unscaledDeltaTime);
                UsingGamepad = true;
            }
            else if (mouse.sqrMagnitude > 0.01f)
            {
                UsingGamepad = false;
            }
            if (settings.invertY) look.y = -look.y;
            LookDelta = look;
            if (look.sqrMagnitude > 0.0001f) LastLookTime = now;

            SprintHeld = _sprint.IsPressed();
            BlockHeld = _block.IsPressed();
            JumpHeld = _jump.IsPressed();
            LightHeld = _light.IsPressed();
            HeavyHeld = _heavy.IsPressed();

            Record(_light, BufferedAction.LightAttack, now);
            Record(_heavy, BufferedAction.HeavyAttack, now);
            Record(_dodge, BufferedAction.Dodge, now);
            Record(_jump, BufferedAction.Jump, now);
            Record(_skill1, BufferedAction.Skill1, now);
            Record(_skill2, BufferedAction.Skill2, now);
            Record(_skill3, BufferedAction.Skill3, now);
            Record(_skill4, BufferedAction.Skill4, now);
            Record(_ultimate, BufferedAction.Ultimate, now);
            if (_block.WasPressedThisFrame())
            {
                Buffer.Record(BufferedAction.Block, now);
                BlockPressed?.Invoke();
            }

            if (_lockOn.WasPressedThisFrame()) LockOnPressed?.Invoke();
            if (_cameraMode.WasPressedThisFrame()) CameraModePressed?.Invoke();
            if (_interact.WasPressedThisFrame()) InteractPressed?.Invoke();
            if (_nextStyle.WasPressedThisFrame()) NextStylePressed?.Invoke();
            if (_prevStyle.WasPressedThisFrame()) PrevStylePressed?.Invoke();

            // Target switching: mouse wheel or a flick of the right stick.
            _switchCooldown -= Time.unscaledDeltaTime;
            float scroll = _switchTarget.ReadValue<float>();
            if (_switchCooldown <= 0f)
            {
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    SwitchTargetRequested?.Invoke(scroll > 0f ? 1 : -1);
                    _switchCooldown = 0.2f;
                }
                else if (Mathf.Abs(stick.x) > 0.85f && Mathf.Abs(stick.y) < 0.5f)
                {
                    SwitchTargetRequested?.Invoke(stick.x > 0f ? 1 : -1);
                    _switchCooldown = 0.35f;
                }
            }
        }

        private void Record(InputAction action, BufferedAction buffered, float now)
        {
            if (action.WasPressedThisFrame()) Buffer.Record(buffered, now);
        }
    }
}
