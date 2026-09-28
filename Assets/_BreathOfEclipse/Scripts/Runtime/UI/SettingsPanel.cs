using System;
using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BreathOfEclipse.UI
{
    /// <summary>
    /// Settings screen shared by the main menu and the pause menu. Every option is a "&lt; value &gt;" row
    /// (mouse and gamepad friendly). Changes apply live and are saved by <see cref="SaveSystem"/>.
    /// Includes a Controls page with interactive rebinding.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private sealed class Row
        {
            public Text Value;
            public Func<string> Read;
        }

        private readonly List<Row> _rows = new List<Row>();
        private RectTransform _settingsPage;
        private RectTransform _controlsPage;
        private Action _onBack;
        private readonly List<(Text label, InputAction action, int binding)> _bindingRows = new List<(Text, InputAction, int)>();
        private InputActionRebindingExtensions.RebindingOperation _rebind;
        private Text _rebindHint;

        public static SettingsPanel Create(Transform parent, Action onBack)
        {
            var root = UIFactory.Stretch("Settings", parent);
            var panel = root.gameObject.AddComponent<SettingsPanel>();
            panel._onBack = onBack;
            panel.Build(root);
            root.gameObject.SetActive(false);
            return panel;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ShowPage(false);
            Refresh();
        }

        public void Hide()
        {
            CancelRebind();
            SaveSystem.ApplyAndSave();
            gameObject.SetActive(false);
        }

        private void Build(RectTransform root)
        {
            var shade = UIFactory.Image("Shade", root, new Color(0f, 0f, 0.02f, 0.75f), ProceduralTextures.UISprite("default"));
            UIFactory.Fill(shade.rectTransform);
            shade.raycastTarget = true;

            _settingsPage = UIFactory.Rect("SettingsPage", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 960f));
            UIFactory.Text("Title", _settingsPage, "SETTINGS", 56, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(800f, 70f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);

            var left = UIFactory.Rect("Left", _settingsPage, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -100f), new Vector2(720f, 760f));
            UIFactory.Vertical(left, 6f, TextAnchor.UpperLeft);
            var right = UIFactory.Rect("Right", _settingsPage, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -100f), new Vector2(720f, 760f));
            UIFactory.Vertical(right, 6f, TextAnchor.UpperLeft);

            var s = SaveSystem.Settings;
            Header(left, "AUDIO");
            Percent(left, "Master", () => s.masterVolume, v => s.masterVolume = v);
            Percent(left, "Music", () => s.musicVolume, v => s.musicVolume = v);
            Percent(left, "SFX", () => s.sfxVolume, v => s.sfxVolume = v);
            Percent(left, "Voice", () => s.voiceVolume, v => s.voiceVolume = v);
            Percent(left, "Ambient", () => s.ambientVolume, v => s.ambientVolume = v);
            Header(left, "CONTROLS");
            Number(left, "Mouse sensitivity", () => s.mouseSensitivity, v => s.mouseSensitivity = v, 0.1f, 4f, 0.1f, "0.0");
            Number(left, "Gamepad sensitivity", () => s.gamepadSensitivity, v => s.gamepadSensitivity = v, 0.1f, 4f, 0.1f, "0.0");
            Toggle(left, "Invert Y", () => s.invertY, v => s.invertY = v);
            Cycle(left, "Dodge input", new[] { "Alt only", "Alt / Dir+Space (lock-on)", "Alt / Dir+Space (always)" }, () => s.dodgeInputMode, v => s.dodgeInputMode = v);

            Header(right, "CAMERA");
            Number(right, "Field of view", () => s.fieldOfView, v => s.fieldOfView = v, 50f, 90f, 1f, "0");
            Percent(right, "Camera shake", () => s.cameraShake / 2f, v => s.cameraShake = v * 2f);
            Toggle(right, "Auto recenter", () => s.autoRecenterCamera, v => s.autoRecenterCamera = v);
            Header(right, "GRAPHICS");
            Cycle(right, "Quality", new[] { "Low", "Medium", "High", "Ultra" }, () => s.graphicsQuality, v => s.graphicsQuality = v);
            Toggle(right, "Motion blur", () => s.motionBlur, v => s.motionBlur = v);
            Toggle(right, "VSync", () => s.vSync, v => s.vSync = v);
            Toggle(right, "Show FPS", () => s.showFps, v => s.showFps = v);
            Header(right, "COMBAT PRESENTATION");
            Toggle(right, "Damage numbers", () => s.showDamageNumbers, v => s.showDamageNumbers = v);
            Toggle(right, "Anime flash frames", () => s.flashFramesEnabled, v => s.flashFramesEnabled = v);
            Toggle(right, "Skip ultimate cinematics", () => s.skipUltimateCinematics, v => s.skipUltimateCinematics = v);

            var bottom = UIFactory.Rect("Bottom", _settingsPage, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1400f, 70f));
            UIFactory.Horizontal(bottom, 24f);
            UIFactory.Button("Controls", bottom, "CONTROLS", new Vector2(300f, 60f), () => ShowPage(true), 26);
            UIFactory.Button("Defaults", bottom, "RESET DEFAULTS", new Vector2(320f, 60f), () =>
            {
                SaveSystem.ResetToDefaults();
                Refresh();
            }, 26);
            UIFactory.Button("Back", bottom, "BACK", new Vector2(260f, 60f), () =>
            {
                Hide();
                _onBack?.Invoke();
            }, 26);

            BuildControlsPage(root);
        }

        private void Header(Transform parent, string label)
        {
            var rt = UIFactory.Rect("Header_" + label, parent, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(720f, 50f));
            UIFactory.Text("Label", rt, label, 26, UIColors.Accent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -4f), new Vector2(700f, 40f), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
        }

        private void AddRow(Transform parent, string label, Func<string> read, Action left, Action right)
        {
            var rt = UIFactory.Rect("Row_" + label, parent, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(720f, 46f));
            var bg = UIFactory.Image("Bg", rt, new Color(1f, 1f, 1f, 0.04f), ProceduralTextures.UISprite("default"));
            UIFactory.Fill(bg.rectTransform);
            UIFactory.Text("Label", rt, label, 24, UIColors.Text, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(380f, 40f), TextAnchor.MiddleLeft);
            var l = UIFactory.Button("Left", rt, "<", new Vector2(46f, 40f), () => { left(); Changed(); }, 26);
            var lrt = (RectTransform)l.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f);
            lrt.pivot = new Vector2(1f, 0.5f);
            lrt.anchoredPosition = new Vector2(-260f, 0f);
            var value = UIFactory.Text("Value", rt, "", 24, Color.white, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-58f, 0f), new Vector2(200f, 40f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var r = UIFactory.Button("Right", rt, ">", new Vector2(46f, 40f), () => { right(); Changed(); }, 26);
            var rrt = (RectTransform)r.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 0.5f);
            rrt.pivot = new Vector2(1f, 0.5f);
            rrt.anchoredPosition = new Vector2(-8f, 0f);
            _rows.Add(new Row { Value = value, Read = read });
        }

        private void Percent(Transform parent, string label, Func<float> get, Action<float> set)
        {
            AddRow(parent, label, () => Mathf.RoundToInt(get() * 100f) + "%",
                () => set(Mathf.Clamp01(Mathf.Round((get() - 0.05f) * 20f) / 20f)),
                () => set(Mathf.Clamp01(Mathf.Round((get() + 0.05f) * 20f) / 20f)));
        }

        private void Number(Transform parent, string label, Func<float> get, Action<float> set, float min, float max, float step, string format)
        {
            AddRow(parent, label, () => get().ToString(format),
                () => set(Mathf.Clamp(get() - step, min, max)),
                () => set(Mathf.Clamp(get() + step, min, max)));
        }

        private void Toggle(Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            AddRow(parent, label, () => get() ? "ON" : "OFF", () => set(!get()), () => set(!get()));
        }

        private void Cycle(Transform parent, string label, string[] options, Func<int> get, Action<int> set)
        {
            AddRow(parent, label, () => options[Mathf.Clamp(get(), 0, options.Length - 1)],
                () => set((get() - 1 + options.Length) % options.Length),
                () => set((get() + 1) % options.Length));
        }

        private void Changed()
        {
            SaveSystem.NotifyChanged();
            Refresh();
        }

        private void Refresh()
        {
            foreach (var r in _rows) r.Value.text = r.Read();
            RefreshBindings();
        }

        // ------------------------------------------------------------------ controls page

        private void BuildControlsPage(RectTransform root)
        {
            _controlsPage = UIFactory.Rect("ControlsPage", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 960f));
            UIFactory.Text("Title", _controlsPage, "CONTROLS", 56, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(800f, 70f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            _rebindHint = UIFactory.Text("Hint", _controlsPage, "Click a binding to change it (keyboard / mouse). Esc cancels.", 22, UIColors.TextDim,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1200f, 30f));

            var reader = InputReader.Instance;
            var left = UIFactory.Rect("Left", _controlsPage, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -130f), new Vector2(720f, 720f));
            UIFactory.Vertical(left, 6f, TextAnchor.UpperLeft);
            var right = UIFactory.Rect("Right", _controlsPage, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -130f), new Vector2(720f, 720f));
            UIFactory.Vertical(right, 6f, TextAnchor.UpperLeft);

            if (reader != null && reader.Asset != null)
            {
                string[] names = { "Jump", "Dodge", "Sprint", "LightAttack", "HeavyAttack", "Block", "LockOn", "Skill1", "Skill2", "Skill3", "Skill4", "FormWheel", "Ultimate", "CameraMode", "Interact", "NextStyle", "PrevStyle" };
                for (int i = 0; i < names.Length; i++)
                {
                    var action = reader.Asset.FindAction(names[i]);
                    if (action == null) continue;
                    int binding = FirstKeyboardMouseBinding(action);
                    if (binding < 0) continue;
                    BindingRow(i < 8 ? left : right, names[i], action, binding);
                }
            }

            var bottom = UIFactory.Rect("Bottom", _controlsPage, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1400f, 70f));
            UIFactory.Horizontal(bottom, 24f);
            UIFactory.Button("ResetBindings", bottom, "RESET BINDINGS", new Vector2(340f, 60f), () =>
            {
                InputReader.Instance?.ResetBindings();
                RefreshBindings();
            }, 26);
            UIFactory.Button("Back", bottom, "BACK", new Vector2(260f, 60f), () => ShowPage(false), 26);
            _controlsPage.gameObject.SetActive(false);
        }

        private static int FirstKeyboardMouseBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (b.path.StartsWith("<Keyboard>") || b.path.StartsWith("<Mouse>")) return i;
            }
            return -1;
        }

        private void BindingRow(Transform parent, string name, InputAction action, int bindingIndex)
        {
            var rt = UIFactory.Rect("Bind_" + name, parent, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(720f, 46f));
            var bg = UIFactory.Image("Bg", rt, new Color(1f, 1f, 1f, 0.04f), ProceduralTextures.UISprite("default"));
            UIFactory.Fill(bg.rectTransform);
            UIFactory.Text("Label", rt, Pretty(name), 24, UIColors.Text, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(380f, 40f), TextAnchor.MiddleLeft);
            Text label = null;
            var btn = UIFactory.Button("Rebind", rt, "", new Vector2(260f, 40f), () => StartRebind(action, bindingIndex, label), 24);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-8f, 0f);
            label = btn.GetComponentInChildren<Text>();
            _bindingRows.Add((label, action, bindingIndex));
        }

        private static string Pretty(string actionName)
        {
            switch (actionName)
            {
                case "LightAttack": return "Light attack";
                case "HeavyAttack": return "Heavy attack";
                case "LockOn": return "Lock-on";
                case "Skill1": return "Quick form 1";
                case "Skill2": return "Quick form 2";
                case "Skill3": return "Quick form 3";
                case "Skill4": return "Quick form 4";
                case "FormWheel": return "Form wheel (hold)";
                case "CameraMode": return "Camera mode";
                case "NextStyle": return "Next style";
                case "PrevStyle": return "Previous style";
                case "Block": return "Block / Parry";
                default: return actionName;
            }
        }

        private void RefreshBindings()
        {
            foreach (var (label, action, binding) in _bindingRows)
                if (label != null) label.text = action.GetBindingDisplayString(binding);
        }

        private void StartRebind(InputAction action, int bindingIndex, Text label)
        {
            CancelRebind();
            bool wasEnabled = action.enabled;
            action.Disable();
            if (label != null) label.text = "...";
            _rebindHint.text = "Press a key or mouse button (Esc to cancel)";
            _rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => FinishRebind(action, wasEnabled))
                .OnCancel(op => FinishRebind(action, wasEnabled))
                .Start();
        }

        private void FinishRebind(InputAction action, bool reenable)
        {
            if (_rebind != null)
            {
                _rebind.Dispose();
                _rebind = null;
            }
            if (reenable) action.Enable();
            var reader = InputReader.Instance;
            if (reader != null)
            {
                SaveSystem.Settings.bindingOverrides = reader.SaveBindingOverrides();
                SaveSystem.SaveSettings();
            }
            _rebindHint.text = "Click a binding to change it (keyboard / mouse). Esc cancels.";
            RefreshBindings();
        }

        private void CancelRebind()
        {
            if (_rebind == null) return;
            _rebind.Cancel();
            if (_rebind != null)
            {
                _rebind.Dispose();
                _rebind = null;
            }
        }

        private void ShowPage(bool controls)
        {
            CancelRebind();
            _settingsPage.gameObject.SetActive(!controls);
            _controlsPage.gameObject.SetActive(controls);
            RefreshBindings();
        }

        private void OnDestroy() => CancelRebind();
    }
}
