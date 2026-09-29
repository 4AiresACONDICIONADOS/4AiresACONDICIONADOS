using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace BreathOfEclipse.UI
{
    /// <summary>
    /// Breathing form wheel: hold the FormWheel key (F by default) to open a radial list of every form of the
    /// equipped style (any number, I … XI — one segment per form), aim with the mouse or right stick, release to
    /// use it. While it is open, keys 1-4 put the aimed form on that quick slot (saved per style). Time slows
    /// slightly while it is open.
    /// </summary>
    public sealed class FormWheel : MonoBehaviour
    {
        private const float Radius = 230f;
        private const float DeadZone = 26f;
        private const float AimClamp = 140f;
        private const float OpenSlowScale = 0.35f;
        private const string SlowId = "form_wheel";

        private sealed class Segment
        {
            public RectTransform Root;
            public Image Back;
            public Image Cooldown;
            public Text Numeral;
            public Text Name;
            public Text Slot;
        }

        public static bool IsOpen { get; private set; }

        private RectTransform _root;
        private RectTransform _ring;
        /// <summary>Wheel radius for the current form count: 7 forms fit at 230 px, 11 forms spread wider.</summary>
        private float _radius = Radius;
        private CanvasGroup _group;
        private Text _title;
        private Text _detail;
        private Image _pointer;
        private readonly List<Segment> _segments = new List<Segment>();
        private BreathingStyleData _builtFor;
        private int _builtCount = -1;
        private Vector2 _aim;
        private int _selected = -1;
        private bool _lookWasSuppressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsOpen = false;

        public static FormWheel Create(Canvas canvas)
        {
            var root = UIFactory.Stretch("FormWheel", canvas.transform);
            var wheel = root.gameObject.AddComponent<FormWheel>();
            wheel._root = root;
            wheel.Build();
            return wheel;
        }

        private void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var shade = UIFactory.Image("Shade", _root, new Color(0.01f, 0.02f, 0.05f, 0.45f), ProceduralTextures.UISprite("default"));
            UIFactory.Fill(shade.rectTransform);
            shade.raycastTarget = false;

            var center = new Vector2(0.5f, 0.5f);
            _ring = UIFactory.Image("Ring", _root, new Color(1f, 1f, 1f, 0.12f), center, center, Vector2.zero, new Vector2(Radius * 2f + 90f, Radius * 2f + 90f),
                ProceduralTextures.UISprite("ring")).rectTransform;
            _pointer = UIFactory.Image("Pointer", _root, new Color(0.6f, 0.9f, 1f, 0.9f), center, center, Vector2.zero, new Vector2(22f, 22f),
                ProceduralTextures.UISprite("diamond"));
            _title = UIFactory.Text("Title", _root, "", 30, UIColors.Text, center, center, new Vector2(0f, 18f), new Vector2(420f, 44f),
                TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            _detail = UIFactory.Text("Detail", _root, "", 18, UIColors.TextDim, center, center, new Vector2(0f, -30f), new Vector2(460f, 70f));
            _detail.supportRichText = true;
        }

        private void RebuildSegments(BreathingStyleData style)
        {
            foreach (var s in _segments) Destroy(s.Root.gameObject);
            _segments.Clear();
            _builtFor = style;
            _builtCount = style != null ? style.FormCount : 0;
            if (style == null) return;

            _radius = Mathf.Max(Radius, _builtCount * 30f);
            _ring.sizeDelta = new Vector2(_radius * 2f + 90f, _radius * 2f + 90f);
            var center = new Vector2(0.5f, 0.5f);
            var sprite = ProceduralTextures.UISprite("circle");
            for (int i = 0; i < _builtCount; i++)
            {
                var form = style.GetForm(i);
                Vector2 pos = Direction(i, _builtCount) * _radius;
                var rt = UIFactory.Rect("Form" + (i + 1), _root, center, center, center, pos, new Vector2(170f, 96f));
                var seg = new Segment { Root = rt };
                seg.Back = UIFactory.Image("Back", rt, new Color(0.05f, 0.07f, 0.14f, 0.85f), center, center, new Vector2(0f, 12f), new Vector2(74f, 74f), sprite);
                seg.Cooldown = UIFactory.Image("Cooldown", rt, new Color(0f, 0f, 0f, 0.6f), center, center, new Vector2(0f, 12f), new Vector2(74f, 74f), sprite);
                seg.Cooldown.type = Image.Type.Filled;
                seg.Cooldown.fillMethod = Image.FillMethod.Radial360;
                seg.Cooldown.fillOrigin = (int)Image.Origin360.Top;
                seg.Numeral = UIFactory.Text("Numeral", rt, Roman.Of(form != null ? form.formNumber : i + 1), 30, UIColors.Text, center, center,
                    new Vector2(0f, 12f), new Vector2(74f, 74f), TextAnchor.MiddleCenter, FontStyle.Bold);
                string name = form != null && form.skill != null ? form.skill.displayName : "-";
                seg.Name = UIFactory.Text("Name", rt, name, 17, UIColors.TextDim, center, center, new Vector2(0f, -40f), new Vector2(190f, 26f));
                seg.Slot = UIFactory.Text("Slot", rt, "", 18, new Color(1f, 0.9f, 0.55f), center, center, new Vector2(40f, 42f), new Vector2(40f, 26f),
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                _segments.Add(seg);
            }
        }

        /// <summary>Segment 0 at the top, then clockwise.</summary>
        private static Vector2 Direction(int index, int count)
        {
            float a = (90f - index * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        private void Update()
        {
            var input = InputReader.Instance;
            var pc = PlayerController.Instance;
            var style = pc != null && pc.Breathing != null ? pc.Breathing.Current : null;
            bool canOpen = input != null && pc != null && style != null && style.FormCount > 0 && pc.CanAct && !PauseMenu.IsOpen && !DebugMenu.IsOpen;

            if (!IsOpen)
            {
                if (canOpen && input.FormWheelHeld) Open(input, style);
                return;
            }

            if (!canOpen)
            {
                Close(input, -1);
                return;
            }
            if (style != _builtFor || style.FormCount != _builtCount) RebuildSegments(style);

            // Aim: right stick wins, otherwise the mouse accumulates a pointer around the center.
            Vector2 stick = input.AimStick;
            if (stick.sqrMagnitude > 0.25f) _aim = stick.normalized * AimClamp;
            else _aim = Vector2.ClampMagnitude(_aim + input.PointerDelta, AimClamp);
            _selected = _aim.magnitude < DeadZone ? -1 : IndexFromAim(_aim, _builtCount);
            _pointer.rectTransform.anchoredPosition = _aim * 0.7f;

            AssignQuickSlots(input, pc);
            RefreshVisuals(pc, style);
            if (!input.FormWheelHeld) Close(input, _selected);
        }

        /// <summary>1-4 while aiming at a form: that form goes to the quick slot (swaps with its old slot).</summary>
        private void AssignQuickSlots(InputReader input, PlayerController pc)
        {
            float now = Time.unscaledTime;
            for (int slot = 0; slot < BreathingStyleData.QuickSlotCount; slot++)
            {
                var action = (BufferedAction)((int)BufferedAction.Skill1 + slot);
                if (!input.Buffer.Consume(action, now)) continue;
                if (_selected < 0) continue;
                pc.Breathing.AssignQuickSlot(slot, _selected);
                var form = pc.Breathing.GetForm(_selected);
                if (form != null && form.skill != null) GameEvents.Notify($"Quick slot {slot + 1}: {Roman.Of(form.formNumber)} · {form.skill.displayName}");
                Audio.Sfx.Play2D("ui_confirm", 0.5f);
            }
        }

        private static int IndexFromAim(Vector2 aim, int count)
        {
            float clockwiseFromTop = Mathf.Repeat(90f - Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, 360f);
            float step = 360f / Mathf.Max(1, count);
            return Mathf.FloorToInt(Mathf.Repeat(clockwiseFromTop + step * 0.5f, 360f) / step) % count;
        }

        private void RefreshVisuals(PlayerController pc, BreathingStyleData style)
        {
            var b = pc.Breathing;
            Color accent = style.baseColor;
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                var form = style.GetForm(i);
                var skill = form != null ? form.skill : null;
                bool selected = i == _selected;
                bool usable = b.CanUseForm(i, out _);
                float cd = skill != null ? b.Cooldowns.NormalizedRemaining(skill.skillId, Time.time) : 0f;
                seg.Cooldown.fillAmount = cd;
                seg.Back.color = selected ? new Color(accent.r, accent.g, accent.b, 0.95f) : new Color(0.05f, 0.07f, 0.14f, usable ? 0.85f : 0.5f);
                seg.Numeral.color = usable ? UIColors.Text : UIColors.TextDim;
                seg.Name.color = selected ? UIColors.Text : UIColors.TextDim;
                int quick = b.SlotOfForm(i);
                seg.Slot.text = quick >= 0 ? (quick + 1).ToString() : "";
                float scale = Mathf.MoveTowards(seg.Root.localScale.x, selected ? 1.18f : 1f, Time.unscaledDeltaTime * 6f);
                seg.Root.localScale = new Vector3(scale, scale, 1f);
            }

            var pick = _selected >= 0 ? style.GetForm(_selected) : null;
            if (pick != null && pick.skill != null)
            {
                _title.text = $"{Roman.Of(pick.formNumber)} · {pick.skill.displayName.ToUpperInvariant()}";
                string state = b.CanUseForm(_selected, out var reason) ? $"Stamina {pick.skill.staminaCost:0}   Breath {pick.skill.breathCost:0}   CD {pick.skill.cooldown:0.#}s" : reason;
                _detail.text = state + "\n<size=15>Release: use · 1-4: set quick slot</size>";
            }
            else
            {
                _title.text = style.displayName;
                _detail.text = $"{style.FormCount} forms · aim and release\n<size=15>aim + 1-4: set quick slot</size>";
            }
        }

        private void Open(InputReader input, BreathingStyleData style)
        {
            IsOpen = true;
            if (style != _builtFor || style.FormCount != _builtCount) RebuildSegments(style);
            _aim = Vector2.zero;
            _selected = -1;
            _group.alpha = 1f;
            _lookWasSuppressed = input.PhysicalLookSuppressed;
            input.PhysicalLookSuppressed = true;
            input.QuickSlotsCaptured = true;
            if (TimeController.Instance != null) TimeController.Instance.SlowMotion(OpenSlowScale, 30f, SlowId, 0.08f);
        }

        private void Close(InputReader input, int confirmIndex)
        {
            IsOpen = false;
            _group.alpha = 0f;
            if (input != null)
            {
                input.PhysicalLookSuppressed = _lookWasSuppressed;
                input.QuickSlotsCaptured = false;
            }
            if (TimeController.Instance != null) TimeController.Instance.CancelSlowMotion(SlowId);
            var pc = PlayerController.Instance;
            if (confirmIndex >= 0 && pc != null) pc.RequestForm(confirmIndex);
        }

        private void OnDisable()
        {
            if (IsOpen) Close(InputReader.Instance, -1);
        }
    }
}
