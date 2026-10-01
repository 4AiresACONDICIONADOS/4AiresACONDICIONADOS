using BreathOfEclipse.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Quiet world HUD: a small clock, the name of the area when you enter it, discoveries, the autosave note and
    /// the interaction prompt. No quest markers.
    /// </summary>
    public sealed class WorldHud : MonoBehaviour
    {
        private LivingWorld _world;
        private Text _clock;
        private CanvasGroup _areaGroup, _savedGroup, _promptGroup, _lineGroup;
        private Text _areaTitle, _areaSub, _prompt, _line;
        private float _areaUntil, _savedUntil, _lineUntil;
        private float _nextClock;
        private string _lastArea;
        private float _lastAreaTime = -99f;

        public static WorldHud Create(LivingWorld world)
        {
            var canvas = UIFactory.Canvas("WorldHUD", 12, world.transform);
            var hud = canvas.gameObject.AddComponent<WorldHud>();
            hud._world = world;
            hud.Build(canvas.transform);
            return hud;
        }

        private static CanvasGroup Group(string name, Transform parent)
        {
            var rt = UIFactory.Stretch(name, parent);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
            return g;
        }

        private void Build(Transform root)
        {
            _clock = UIFactory.Text("Clock", root, "", 18, new Color(0.9f, 0.9f, 0.95f, 0.9f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -10f), new Vector2(420f, 26f), TextAnchor.MiddleRight);

            _areaGroup = Group("Area", root);
            _areaTitle = UIFactory.Text("AreaName", _areaGroup.transform, "", 44, new Color(1f, 0.97f, 0.9f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1400f, 56f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            _areaSub = UIFactory.Text("AreaSub", _areaGroup.transform, "", 20, UIColors.Accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(1400f, 28f), TextAnchor.MiddleCenter, FontStyle.Italic);

            _savedGroup = Group("Saved", root);
            UIFactory.Text("SavedText", _savedGroup.transform, "World saved", 16, UIColors.TextDim, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(300f, 24f), TextAnchor.MiddleRight, FontStyle.Italic);

            _promptGroup = Group("Prompt", root);
            _prompt = UIFactory.Text("PromptText", _promptGroup.transform, "", 22, new Color(1f, 0.95f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(900f, 30f), TextAnchor.MiddleCenter, FontStyle.Bold);

            _lineGroup = Group("Line", root);
            _line = UIFactory.Text("LineText", _lineGroup.transform, "", 24, Color.white, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1300f, 34f), TextAnchor.MiddleCenter);
        }

        /// <summary>Entering an area: its name (only when it changed and not too often).</summary>
        public void ShowArea(SectorDef s, bool firstVisit)
        {
            if (s == null || s.Id == _lastArea && Time.unscaledTime - _lastAreaTime < 60f) return;
            if (Time.unscaledTime - _lastAreaTime < 8f && !firstVisit) return;
            _lastArea = s.Id;
            _lastAreaTime = Time.unscaledTime;
            _areaTitle.text = s.Name.ToUpperInvariant();
            _areaSub.text = s.Danger >= 3 ? "The air here is wrong." : s.SafeZone ? FrontierRegionLayout.RegionName : firstVisit ? FrontierRegionLayout.RegionName + " — new area" : FrontierRegionLayout.RegionName;
            _areaSub.color = s.Danger >= 3 ? new Color(1f, 0.45f, 0.5f) : UIColors.Accent;
            _areaUntil = Time.unscaledTime + 3.5f;
        }

        public void ShowDiscovery(DiscoverySpot d)
        {
            _areaTitle.text = d.Name;
            _areaSub.text = d.Secret ? "A place few people know" : "Discovered";
            _areaSub.color = d.Secret ? new Color(0.85f, 0.75f, 1f) : new Color(1f, 0.85f, 0.55f);
            _areaUntil = Time.unscaledTime + 4f;
        }

        public void ShowSaved() => _savedUntil = Time.unscaledTime + 2.5f;

        /// <summary>Interaction prompt ("[E] Talk — Ohara"); null hides it.</summary>
        public void SetPrompt(string text)
        {
            if (string.IsNullOrEmpty(text)) _promptGroup.alpha = 0f;
            else
            {
                _prompt.text = text;
                _promptGroup.alpha = 1f;
            }
        }

        /// <summary>An ambient line or a short spoken answer ("Ohara: ¡Arroz fresco!").</summary>
        public void ShowLine(string speaker, string text, float seconds = 3.5f)
        {
            _line.text = string.IsNullOrEmpty(speaker) ? text : $"<color=#C9D6FF>{speaker}</color>  {text}";
            _line.supportRichText = true;
            _lineUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _nextClock && _world != null && _world.Time != null)
            {
                _nextClock = now + 0.2f;
                _clock.text = _world.Time.Describe();
            }
            Fade(_areaGroup, now < _areaUntil, 1.6f);
            Fade(_savedGroup, now < _savedUntil, 3f);
            Fade(_lineGroup, now < _lineUntil, 4f);
        }

        private static void Fade(CanvasGroup g, bool on, float speed)
        {
            g.alpha = Mathf.MoveTowards(g.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * speed);
        }
    }
}
