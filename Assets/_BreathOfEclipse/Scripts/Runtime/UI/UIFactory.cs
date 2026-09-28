using System;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BreathOfEclipse.UI
{
    /// <summary>Palette used by all procedural UI.</summary>
    public static class UIColors
    {
        public static readonly Color Panel = new Color(0.03f, 0.03f, 0.07f, 0.82f);
        public static readonly Color PanelLight = new Color(0.1f, 0.1f, 0.18f, 0.9f);
        public static readonly Color Text = new Color(0.94f, 0.94f, 0.98f);
        public static readonly Color TextDim = new Color(0.65f, 0.66f, 0.78f);
        public static readonly Color Accent = new Color(0.55f, 0.75f, 1f);
        public static readonly Color Health = new Color(0.86f, 0.16f, 0.22f);
        public static readonly Color HealthTrail = new Color(1f, 0.85f, 0.85f);
        public static readonly Color Stamina = new Color(0.45f, 0.9f, 0.45f);
        public static readonly Color StaminaExhausted = new Color(0.95f, 0.55f, 0.2f);
        public static readonly Color Boss = new Color(0.75f, 0.05f, 0.3f);
        public static readonly Color Button = new Color(0.08f, 0.08f, 0.14f, 0.85f);
        public static readonly Color ButtonHover = new Color(0.25f, 0.3f, 0.55f, 0.95f);
    }

    /// <summary>
    /// Builds uGUI elements from code (no prefabs needed for the prototype). Uses the built-in legacy font so the
    /// UI works without importing TextMesh Pro essentials.
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public static Image Image(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite sprite = null)
        {
            var img = Image(name, parent, color, sprite);
            var rt = img.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return img;
        }

        public static Text Text(string name, Transform parent, string content, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.85f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        public static Text Text(string name, Transform parent, string content, int size, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 boxSize,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var t = Text(name, parent, content, size, color, align, style);
            var rt = t.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = boxSize;
            return t;
        }

        /// <summary>Horizontal bar: background, delayed "trail" and fill (Image.Filled).</summary>
        public static (Image fill, Image trail, RectTransform root) Bar(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            Color fillColor, Color trailColor)
        {
            var root = Rect(name, parent, anchor, anchor, pivot, pos, size);
            var sprite = ProceduralTextures.UISprite("default");
            var bg = Image("Background", root, new Color(0f, 0f, 0f, 0.6f), sprite);
            Fill(bg.rectTransform);
            var border = bg.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.25f);
            border.effectDistance = new Vector2(1.5f, 1.5f);
            var trail = Image("Trail", root, trailColor, sprite);
            Fill(trail.rectTransform, 2f);
            trail.type = UnityEngine.UI.Image.Type.Filled;
            trail.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            trail.fillOrigin = 0;
            var fill = Image("Fill", root, fillColor, sprite);
            Fill(fill.rectTransform, 2f);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            return (fill, trail, root);
        }

        public static void Fill(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Button Button(string name, Transform parent, string label, Vector2 size, Action onClick, int fontSize = 30)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            var img = go.GetComponent<Image>();
            img.sprite = ProceduralTextures.UISprite("default");
            img.color = UIColors.Button;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.6f, 1.7f, 2.2f, 1f);
            colors.selectedColor = new Color(1.5f, 1.6f, 2.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            colors.colorMultiplier = 1.6f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var text = Text("Label", go.transform, label, fontSize, UIColors.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Fill(text.rectTransform);
            btn.onClick.AddListener(() =>
            {
                Sfx.Play2D("ui_click", 0.6f);
                onClick?.Invoke();
            });
            var hover = go.AddComponent<UIHoverSound>();
            hover.Accent = img;
            return btn;
        }

        public static VerticalLayoutGroup Vertical(Transform target, float spacing, TextAnchor align = TextAnchor.MiddleCenter, RectOffset padding = null)
        {
            var v = target.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlHeight = false;
            v.childControlWidth = false;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = false;
            if (padding != null) v.padding = padding;
            return v;
        }

        public static HorizontalLayoutGroup Horizontal(Transform target, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlHeight = false;
            h.childControlWidth = false;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;
            return h;
        }
    }

    /// <summary>Plays a hover tick and slides the button slightly (anime menu feel).</summary>
    public sealed class UIHoverSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public Image Accent;

        public void OnPointerEnter(PointerEventData eventData) => Sfx.Play2D("ui_hover", 0.4f);
        public void OnSelect(BaseEventData eventData) => Sfx.Play2D("ui_hover", 0.3f);
    }

    /// <summary>Cursor lock rules: locked in gameplay, free in menus.</summary>
    public static class CursorManager
    {
        public static void SetGameplay(bool gameplay)
        {
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }
    }
}
