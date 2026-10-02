using System.Collections;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Resting at the inn or a campfire: "until morning" (06:00) or "until evening" (18:00). The screen fades, the
    /// world simulates the skipped hours (routines, events that started / expired / resolved off-screen, repairs),
    /// the player recovers, and the world is saved.
    /// </summary>
    public sealed class RestSystem : MonoBehaviour
    {
        public static RestSystem Instance { get; private set; }
        public bool Open { get; private set; }
        public bool Resting { get; private set; }

        private LivingWorld _world;
        private RectTransform _panel;
        private Text _title, _info;
        private Button _morning, _evening, _cancel;
        private RestPoint _point;

        public static RestSystem Create(LivingWorld world)
        {
            var canvas = UIFactory.Canvas("RestMenu", 150, world.transform);
            var r = canvas.gameObject.AddComponent<RestSystem>();
            r._world = world;
            r.Build(canvas.transform);
            return r;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Open) SetInput(false);
        }

        private void Build(Transform root)
        {
            _panel = UIFactory.Stretch("Rest", root);
            var shade = UIFactory.Image("Shade", _panel, new Color(0.01f, 0.01f, 0.04f, 0.6f), ProceduralTextures.UISprite("default"));
            UIFactory.Fill(shade.rectTransform);
            shade.raycastTarget = true;
            _title = UIFactory.Text("Title", _panel, "REST", 56, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(900f, 80f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            _info = UIFactory.Text("Info", _panel, "", 22, UIColors.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1000f, 34f), TextAnchor.MiddleCenter, FontStyle.Italic);
            var list = UIFactory.Rect("Buttons", _panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(560f, 260f));
            UIFactory.Vertical(list, 14f);
            _morning = UIFactory.Button("Morning", list, "REST UNTIL MORNING (06:00)", new Vector2(520f, 64f), () => Choose(6f), 26);
            _evening = UIFactory.Button("Evening", list, "REST UNTIL EVENING (18:00)", new Vector2(520f, 64f), () => Choose(18f), 26);
            _cancel = UIFactory.Button("Cancel", list, "CANCEL", new Vector2(520f, 64f), Close, 26);
            _panel.gameObject.SetActive(false);
        }

        public void Show(RestPoint point)
        {
            if (Resting || PauseMenu.IsOpen) return;
            _point = point;
            float h = _world.Time.Clock.Hour;
            _title.text = point != null ? point.Label.ToUpperInvariant() : "REST";
            _info.text = $"Day {_world.Time.Clock.Day} · {WorldClock.Format(h)} — the world keeps moving while you rest.";
            _morning.gameObject.SetActive(!(h >= 5f && h < 9f));
            _evening.gameObject.SetActive(h >= 5f && h < 16f);
            Open = true;
            _panel.gameObject.SetActive(true);
            SetInput(true);
            var first = _morning.gameObject.activeSelf ? _morning : _evening.gameObject.activeSelf ? _evening : _cancel;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private void Close()
        {
            Open = false;
            _panel.gameObject.SetActive(false);
            SetInput(false);
        }

        private static void SetInput(bool menu)
        {
            if (InputReader.Instance != null) InputReader.Instance.SetGameplayEnabled(!menu);
            CursorManager.SetGameplay(!menu);
        }

        private void Update()
        {
            if (Open && InputReader.Instance != null && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        private void Choose(float targetHour)
        {
            Close();
            StartCoroutine(RestRoutine(targetHour));
        }

        /// <summary>Rests until <paramref name="targetHour"/> (also used by the World Lab and the playtest).</summary>
        public void RestUntil(float targetHour)
        {
            if (!Resting) StartCoroutine(RestRoutine(targetHour));
        }

        private IEnumerator RestRoutine(float targetHour)
        {
            Resting = true;
            float hours = WorldClock.HoursUntil(_world.Time.Clock.Hour, targetHour);
            if (hours < 0.25f) hours += 24f;
            System.Action skip = () =>
            {
                _world.Time.Advance(hours);
                var pc = PlayerController.Instance;
                if (pc != null && pc.Damageable != null && pc.Damageable.Health != null)
                {
                    pc.Damageable.Health.Heal(pc.Damageable.Health.Max);
                    if (pc.Stats != null) pc.Stats.Stamina.Refill();
                }
                _world.SaveWorld("rest", true);
            };
            if (SceneLoader.Instance != null) yield return SceneLoader.Instance.FadeOutIn(0.7f, skip);
            else skip();
            if (_world.Hud != null) _world.Hud.ShowLine(null, $"You rested until {WorldClock.Format(_world.Time.Clock.Hour)} — Day {_world.Time.Clock.Day}.", 3.5f);
            Resting = false;
        }
    }
}
