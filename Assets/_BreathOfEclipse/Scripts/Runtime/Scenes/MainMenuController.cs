using System;
using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.UI;
using BreathOfEclipse.VFX;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BreathOfEclipse.Scenes
{
    /// <summary>
    /// 01_MainMenu: the swordsman practising katas on a moonlit hill behind the title, with PLAY / TRAINING /
    /// SETTINGS / QUIT. Each kata shows a different breathing style's colours.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private static readonly Vector3 MoonDir = new Vector3(-0.25f, 0.35f, 1f);
        private static readonly Element[] KataElements = { Element.Water, Element.Fire, Element.Thunder, Element.Wind, Element.Moon };
        private static readonly string[][] Katas =
        {
            new[] { "L1", "L2", "H1" },
            new[] { "L3", "L4" },
            new[] { "SkillIaiDraw" },
            new[] { "L1", "SkillSpin" },
            new[] { "H2", "L2H" }
        };

        private Transform _world;
        private CameraRig _cam;
        private CharacterRig _hero;
        private ProceduralAnimator _animator;
        private SwordTrail _trail;
        private RectTransform _menu;
        private RectTransform _buttonList;
        private SettingsPanel _settings;
        private Button _first;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();

        /// <summary>
        /// Raised after the menu UI is built. Development tools use it to add entries (e.g. DEVELOPER PLAYTEST in
        /// Editor / Development builds) without the game depending on them.
        /// </summary>
        public static event Action<MainMenuController> MenuBuilt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => MenuBuilt = null;

        /// <summary>Main menu buttons by id: "Play", "Training", "Settings", "Quit" (+ entries added by tools).</summary>
        public IReadOnlyDictionary<string, Button> Buttons => _buttons;
        public bool SettingsOpen => _settings != null && _settings.gameObject.activeSelf;

        /// <summary>Adds a button to the menu list (before QUIT). Returns the existing one if the id is taken.</summary>
        public Button AddMenuButton(string id, string label, Action onClick)
        {
            if (_buttons.TryGetValue(id, out var existing)) return existing;
            var b = UIFactory.Button(id, _buttonList, label, new Vector2(420f, 58f), onClick, 26);
            if (_buttons.TryGetValue("Quit", out var quit)) b.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());
            _buttons[id] = b;
            _buttonList.sizeDelta += new Vector2(0f, 76f);
            return b;
        }

        /// <summary>Shows or hides the main button list (development panels drawn on top of the menu).</summary>
        public void SetMenuVisible(bool visible)
        {
            if (_menu != null) _menu.gameObject.SetActive(visible);
        }

        public void OpenSettingsPanel() => OpenSettings();

        public void CloseSettingsPanel()
        {
            if (_settings == null || !_settings.gameObject.activeSelf) return;
            _settings.Hide();
            _menu.gameObject.SetActive(true);
        }
        private float _orbit;
        private float _nextKata = 2.5f;
        private int _kataIndex;
        private int _kataStep;
        private float _nextStep;

        private void Awake()
        {
            _world = new GameObject("MenuWorld").transform;
            _world.SetParent(transform, false);
            BuildWorld();
            EnvironmentKit.ApplyNightAtmosphere(_world, MoonDir, 0.02f);
            _cam = CameraRig.Create();
            _cam.Camera.fieldOfView = 38f;
            BuildHero();
            BuildUI();
        }

        private void Start()
        {
            CursorManager.SetGameplay(false);
            if (InputReader.Instance != null) InputReader.Instance.SetGameplayEnabled(false);
            if (TimeController.Instance != null) TimeController.Instance.ResetAll();
            Sfx.Music("menu", 2.5f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAmbient("ambient_forest");
            if (EventSystem.current != null && _first != null) EventSystem.current.SetSelectedGameObject(_first.gameObject);
        }

        private void BuildWorld()
        {
            EnvironmentKit.CreateMoonLights(_world, MoonDir, 1.4f);
            EnvironmentKit.SkyDome(_world, MoonDir, 0.16f);
            EnvironmentKit.Ground(_world, "Hill", new Vector2(160f, 160f), 2f, HillHeight, new Color(0.18f, 0.24f, 0.24f), ProceduralTextures.GroundDetail, 1f);
            var rng = new System.Random(77);
            for (int i = 0; i < 60; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 14f + (float)rng.NextDouble() * 50f;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                // Keep the view towards the moon open.
                if (z > 0f && Mathf.Abs(x) < 16f + z * 0.25f) continue;
                var leaves = i % 4 == 0 ? new Color(0.36f, 0.2f, 0.33f) : new Color(0.12f, 0.21f, 0.22f);
                EnvironmentKit.Tree(_world, new Vector3(x, HillHeight(x, z) - 0.1f, z), 1f + (float)rng.NextDouble() * 0.9f, rng.Next(), leaves);
            }
            EnvironmentKit.MoonGate(_world, new Vector3(0f, HillHeight(0f, 34f), 34f), 0f, 1.6f);
            EnvironmentKit.StoneLantern(_world, new Vector3(-2.6f, HillHeight(-2.6f, 1.5f), 1.5f), true);
            EnvironmentKit.Rock(_world, new Vector3(2.4f, HillHeight(2.4f, 0.8f), 0.8f), new Vector3(1.2f, 0.6f, 1f), 3, false);
            EnvironmentKit.AmbientParticles(_world, new Vector3(0f, 0f, 6f), new Vector3(40f, 6f, 40f), true, true, true);
        }

        private static float HillHeight(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float hill = Mathf.Max(0f, 1f - d / 18f);
            float h = hill * hill * 1.5f;
            h += (Mathf.PerlinNoise(x * 0.05f + 4f, z * 0.05f + 9f) - 0.5f) * 2.5f * Mathf.Clamp01(d / 20f);
            return h - 1.5f;
        }

        private void BuildHero()
        {
            var go = new GameObject("MenuHero");
            go.transform.SetParent(_world, false);
            go.transform.SetPositionAndRotation(new Vector3(0f, HillHeight(0f, 0f), 0f), Quaternion.Euler(0f, 160f, 0f));
            _hero = go.AddComponent<CharacterRig>();
            _hero.Build(RigProfile.Hero(), Layers.Default);
            _animator = go.AddComponent<ProceduralAnimator>();
            _animator.Initialize(_hero);
            _animator.SetLocomotion(new LocomotionState { Grounded = true, CombatStance = true });
            _trail = SwordTrail.Create("MenuTrail", go.transform, _hero.WeaponBase, _hero.WeaponTip,
                MaterialFactory.SwordTrail("menu", TrailGradient(Element.Water), VfxBlend.Additive));
            ApplyElement(Element.Water);
        }

        private static Gradient TrailGradient(Element element)
        {
            var p = ElementPalette.Get(element);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(p.Bright, 0f), new GradientColorKey(p.Core, 0.4f), new GradientColorKey(p.Edge, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private void ApplyElement(Element element)
        {
            var p = ElementPalette.Get(element);
            _hero.SetWeaponGlow(p.Core * 2.2f);
            _hero.SetAccentColor(p.Core);
            _trail.SetMaterial(MaterialFactory.SwordTrail("menu_" + element, TrailGradient(element), VfxBlend.Additive));
        }

        private void BuildUI()
        {
            var canvas = UIFactory.Canvas("MainMenu", 50);
            var root = canvas.transform;
            _menu = UIFactory.Stretch("Menu", root);
            var gradient = UIFactory.Image("LeftShade", _menu, new Color(0.01f, 0.01f, 0.04f, 0.72f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(900f, 1400f), ProceduralTextures.UISprite("fade_right"));
            gradient.raycastTarget = false;
            UIFactory.Text("Title", _menu, "BREATH OF ECLIPSE", 92, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 300f), new Vector2(1100f, 130f), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            UIFactory.Text("Subtitle", _menu, "ANIME SWORD ACTION RPG — VERTICAL SLICE", 26, UIColors.Accent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(116f, 222f), new Vector2(1000f, 40f), TextAnchor.MiddleLeft, FontStyle.Normal);

            var list = UIFactory.Rect("Buttons", _menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, -60f), new Vector2(460f, 420f));
            _buttonList = list;
            UIFactory.Vertical(list, 18f, TextAnchor.UpperLeft);
            _first = UIFactory.Button("Play", list, "PLAY", new Vector2(420f, 74f), () => Load(SceneNames.MoonlitForest), 34);
            _buttons["Play"] = _first;
            _buttons["Training"] = UIFactory.Button("Training", list, "TRAINING", new Vector2(420f, 74f), () => Load(SceneNames.CombatTest), 34);
            _buttons["Settings"] = UIFactory.Button("Settings", list, "SETTINGS", new Vector2(420f, 74f), OpenSettings, 34);
            _buttons["Quit"] = UIFactory.Button("Quit", list, "QUIT", new Vector2(420f, 74f), Quit, 34);

            UIFactory.Text("Version", _menu, $"v{GameManager.Version}  —  prototype build (editable project)", 20, UIColors.TextDim,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(800f, 30f), TextAnchor.LowerRight, FontStyle.Normal);
            UIFactory.Text("Hint", _menu, "F1 debug  ·  F2 FPS  ·  Esc pause", 18, UIColors.TextDim,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(700f, 30f), TextAnchor.LowerLeft, FontStyle.Normal);

            _settings = SettingsPanel.Create(root, () =>
            {
                _menu.gameObject.SetActive(true);
                if (EventSystem.current != null && _first != null) EventSystem.current.SetSelectedGameObject(_first.gameObject);
            });
            MenuBuilt?.Invoke(this);
        }

        private void OpenSettings()
        {
            _menu.gameObject.SetActive(false);
            _settings.Show();
        }

        private static void Load(string scene)
        {
            Sfx.Play2D("ui_confirm");
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(scene, 0.6f);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
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

        private void Update()
        {
            UpdateKata();
            if (InputReader.Instance != null && _settings != null && _settings.gameObject.activeSelf && Escape())
            {
                _settings.Hide();
                _menu.gameObject.SetActive(true);
            }
        }

        private static bool Escape()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }

        private void UpdateKata()
        {
            float t = Time.time;
            if (_kataStep == 0 && t >= _nextKata)
            {
                ApplyElement(KataElements[_kataIndex % KataElements.Length]);
                _kataStep = 1;
                _nextStep = t;
            }
            if (_kataStep > 0 && t >= _nextStep)
            {
                var kata = Katas[_kataIndex % Katas.Length];
                int i = _kataStep - 1;
                if (i < kata.Length)
                {
                    string motion = kata[i];
                    bool skill = motion.StartsWith("Skill");
                    if (skill) _animator.PlayMotion(motion, 0.9f, 0.1f);
                    else _animator.PlayAttack(motion, 0.14f, 0.12f, 0.3f);
                    _trail.Emitting = true;
                    var element = KataElements[_kataIndex % KataElements.Length];
                    Sfx.Play(i == kata.Length - 1 ? "slash_heavy" : "slash", _hero.transform.position, 0.35f);
                    if (i == kata.Length - 1)
                        VFXLibrary.Spawn("impact_slash", _hero.WeaponTip.position, _hero.transform.rotation, 0.8f, element);
                    _kataStep++;
                    _nextStep = t + (skill ? 0.95f : 0.42f);
                }
                else
                {
                    _trail.Emitting = false;
                    _kataStep = 0;
                    _kataIndex++;
                    _nextKata = t + 3.2f;
                }
            }
        }

        private void LateUpdate()
        {
            if (_cam == null || _hero == null) return;
            // Slow cinematic drift around the hero, framing the moon behind.
            _orbit += Time.deltaTime * 2.2f;
            float yaw = 200f + Mathf.Sin(_orbit * Mathf.Deg2Rad * 6f) * 12f;
            Vector3 focus = _hero.transform.position + Vector3.up * 1.25f;
            Vector3 offset = Quaternion.Euler(4f, yaw, 0f) * Vector3.forward * 7.2f;
            Vector3 pos = focus + offset;
            // Push the hero to the right third of the frame.
            Vector3 right = Vector3.Cross(Vector3.up, (focus - pos).normalized);
            _cam.transform.SetPositionAndRotation(pos - right * 2.1f, Quaternion.LookRotation(focus - (pos) + Vector3.up * 1.4f));
        }
    }
}
