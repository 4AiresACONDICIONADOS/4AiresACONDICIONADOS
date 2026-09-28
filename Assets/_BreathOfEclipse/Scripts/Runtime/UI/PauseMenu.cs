using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BreathOfEclipse.UI
{
    /// <summary>Escape menu during gameplay: resume, settings, training, main menu, quit.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private RectTransform _main;
        private SettingsPanel _settings;
        private Button _first;

        public static PauseMenu Create()
        {
            var canvas = UIFactory.Canvas("PauseMenu", 200);
            var menu = canvas.gameObject.AddComponent<PauseMenu>();
            menu.Build(canvas.transform);
            return menu;
        }

        private void Build(Transform root)
        {
            _main = UIFactory.Stretch("Main", root);
            var shade = UIFactory.Image("Shade", _main, new Color(0.01f, 0.01f, 0.04f, 0.78f), ProceduralTextures.UISprite("default"));
            UIFactory.Fill(shade.rectTransform);
            shade.raycastTarget = true;
            UIFactory.Text("Title", _main, "PAUSED", 72, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(800f, 100f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            var list = UIFactory.Rect("Buttons", _main, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(520f, 460f));
            UIFactory.Vertical(list, 16f);
            _first = UIFactory.Button("Resume", list, "RESUME", new Vector2(460f, 70f), Close);
            UIFactory.Button("Settings", list, "SETTINGS", new Vector2(460f, 70f), OpenSettings);
            UIFactory.Button("Training", list, "TRAINING GROUND", new Vector2(460f, 70f), () => Load(SceneNames.CombatTest));
            UIFactory.Button("Menu", list, "MAIN MENU", new Vector2(460f, 70f), () => Load(SceneNames.MainMenu));
            UIFactory.Button("Quit", list, "QUIT", new Vector2(460f, 70f), Quit);
            _settings = SettingsPanel.Create(root, () => _main.gameObject.SetActive(true));
            gameObject.SetActive(true);
            _main.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (InputReader.Instance != null) InputReader.Instance.PausePressed += Toggle;
        }

        private void OnDisable()
        {
            if (InputReader.Instance != null) InputReader.Instance.PausePressed -= Toggle;
            if (IsOpen) SetOpen(false);
        }

        public void Toggle()
        {
            if (DebugMenu.IsOpen) return;
            if (_settings.gameObject.activeSelf)
            {
                _settings.Hide();
                _main.gameObject.SetActive(true);
                return;
            }
            SetOpen(!IsOpen);
        }

        private void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            IsOpen = open;
            _main.gameObject.SetActive(open);
            if (!open && _settings != null && _settings.gameObject.activeSelf) _settings.Hide();
            if (TimeController.Instance != null) TimeController.Instance.Paused = open;
            if (InputReader.Instance != null) InputReader.Instance.SetGameplayEnabled(!open);
            CursorManager.SetGameplay(!open);
            if (open && EventSystem.current != null && _first != null) EventSystem.current.SetSelectedGameObject(_first.gameObject);
        }

        private void OpenSettings()
        {
            _main.gameObject.SetActive(false);
            _settings.Show();
        }

        private void Load(string scene)
        {
            SetOpen(false);
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(scene);
        }

        private static void Quit()
        {
            SaveSystem.SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                IsOpen = false;
                if (TimeController.Instance != null) TimeController.Instance.Paused = false;
                if (InputReader.Instance != null) InputReader.Instance.SetGameplayEnabled(true);
            }
        }
    }
}
