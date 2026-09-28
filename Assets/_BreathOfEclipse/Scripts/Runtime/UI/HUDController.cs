using System.Collections.Generic;
using BreathOfEclipse.Breathing;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace BreathOfEclipse.UI
{
    /// <summary>
    /// Clean anime HUD: HP / stamina / BREATH (bottom-left), technique slots with cooldowns and the ultimate
    /// (bottom-right), boss bar (top), combo counter, technique callout banner, lock-on marker and notifications.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        private sealed class SkillSlot
        {
            public RectTransform Root;
            public Image Icon;
            public Image Cooldown;
            public Text Key;
            public Text Name;
            public Text Timer;
            public Image Glow;
        }

        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _hpFill, _hpTrail, _stFill, _stTrail;
        private Image[] _breathSegments;
        private Image _breathGlow;
        private Text _hpText, _styleText, _breathLabel, _cameraText;
        private Image _styleIcon;
        private readonly SkillSlot[] _slots = new SkillSlot[5];
        private RectTransform _bossRoot;
        private Image _bossFill, _bossTrail;
        private Text _bossName, _bossPhase;
        private CanvasGroup _bossGroup;
        private RectTransform _comboRoot;
        private Text _comboCount, _comboLabel;
        private float _comboScale;
        private float _comboAlpha;
        private RectTransform _bannerRoot;
        private Text _bannerText, _bannerForm;
        private Image _bannerBrush;
        private float _bannerTimer;
        private Text _notice;
        private readonly Queue<string> _notices = new Queue<string>();
        private float _noticeTimer;
        private RectTransform _lockMarker;
        private Image _lockImage;
        private Text _prompt;
        private CanvasGroup _deathGroup;
        private Text _deathText;
        private bool _dead;
        private Text _hints;
        private float _hintsTimer = 14f;
        private GameObject _bossTarget;

        private float _hpShown = 1f, _stShown = 1f;
        private float _hpTrailValue = 1f, _bossTrailValue = 1f;

        /// <summary>Diagnostics: the boss health bar is shown.</summary>
        public bool BossBarVisible => _bossRoot != null && _bossRoot.gameObject.activeSelf;

        public static HUDController Create()
        {
            var canvas = UIFactory.Canvas("HUD", 100);
            var hud = canvas.gameObject.AddComponent<HUDController>();
            hud._canvas = canvas;
            hud.Build();
            return hud;
        }

        private void OnEnable()
        {
            GameEvents.Notification += OnNotification;
            GameEvents.ComboChanged += OnCombo;
            GameEvents.BossEncounter += OnBossEncounter;
            GameEvents.BossPhaseChanged += OnBossPhase;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PlayerRespawned += OnPlayerRespawned;
            GameEvents.CameraModeChanged += OnCameraMode;
            GameEvents.StyleChanged += OnStyleChanged;
        }

        private void OnDisable()
        {
            GameEvents.Notification -= OnNotification;
            GameEvents.ComboChanged -= OnCombo;
            GameEvents.BossEncounter -= OnBossEncounter;
            GameEvents.BossPhaseChanged -= OnBossPhase;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PlayerRespawned -= OnPlayerRespawned;
            GameEvents.CameraModeChanged -= OnCameraMode;
            GameEvents.StyleChanged -= OnStyleChanged;
            if (_boundBreathing != null) _boundBreathing.TechniqueStarted -= OnTechnique;
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            var root = _canvas.transform;
            var bl = new Vector2(0f, 0f);
            var br = new Vector2(1f, 0f);

            // Bottom-left: vitals.
            var vitals = UIFactory.Rect("Vitals", root, bl, bl, Vector2.zero, new Vector2(48f, 44f), new Vector2(620f, 190f));
            _styleIcon = UIFactory.Image("StyleIcon", vitals, Color.white, bl, bl, new Vector2(0f, 150f), new Vector2(34f, 34f), ProceduralTextures.UISprite("diamond"));
            _styleText = UIFactory.Text("Style", vitals, "TIDAL BREATH", 30, UIColors.Text, bl, bl, new Vector2(46f, 146f), new Vector2(520f, 42f), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            var hp = UIFactory.Bar("HP", vitals, bl, bl, new Vector2(0f, 104f), new Vector2(520f, 26f), UIColors.Health, UIColors.HealthTrail);
            _hpFill = hp.fill;
            _hpTrail = hp.trail;
            _hpText = UIFactory.Text("HPText", hp.root, "320 / 320", 18, UIColors.Text, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(200f, 26f), TextAnchor.MiddleRight);
            var st = UIFactory.Bar("Stamina", vitals, bl, bl, new Vector2(0f, 82f), new Vector2(400f, 12f), UIColors.Stamina, new Color(1f, 1f, 1f, 0.4f));
            _stFill = st.fill;
            _stTrail = st.trail;
            // BREATH gauge: 4 segments (advanced forms cost 35, ultimate needs a full gauge).
            _breathLabel = UIFactory.Text("BreathLabel", vitals, "BREATH", 20, UIColors.TextDim, bl, bl, new Vector2(0f, 40f), new Vector2(120f, 30f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _breathSegments = new Image[4];
            var sprite = ProceduralTextures.UISprite("default");
            for (int i = 0; i < 4; i++)
            {
                var segBg = UIFactory.Image("SegBg" + i, vitals, new Color(0f, 0f, 0f, 0.55f), bl, bl, new Vector2(96f + i * 106f, 42f), new Vector2(100f, 24f), sprite);
                var seg = UIFactory.Image("Seg" + i, segBg.rectTransform, Color.white, sprite);
                UIFactory.Fill(seg.rectTransform, 2f);
                seg.type = Image.Type.Filled;
                seg.fillMethod = Image.FillMethod.Horizontal;
                _breathSegments[i] = seg;
            }
            _breathGlow = UIFactory.Image("BreathGlow", vitals, new Color(1f, 1f, 1f, 0f), bl, bl, new Vector2(90f, 32f), new Vector2(438f, 44f), ProceduralTextures.UISprite("default"));
            _breathGlow.transform.SetAsFirstSibling();

            // Bottom-right: technique slots.
            var skills = UIFactory.Rect("Skills", root, br, br, new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(640f, 180f));
            string[] keys = { "1", "2", "3", "4", "R" };
            for (int i = 0; i < 5; i++)
            {
                bool ult = i == 4;
                float size = ult ? 124f : 92f;
                float x = ult ? -62f : -170f - (3 - i) * 106f;
                var slot = new SkillSlot();
                slot.Root = UIFactory.Rect("Slot" + i, skills, br, br, new Vector2(0.5f, 0f), new Vector2(x, ult ? 28f : 30f), new Vector2(size, size));
                slot.Glow = UIFactory.Image("Glow", slot.Root, new Color(1f, 1f, 1f, 0f), ProceduralTextures.UISprite("circle"));
                UIFactory.Fill(slot.Glow.rectTransform, -14f);
                var ring = UIFactory.Image("Ring", slot.Root, new Color(1f, 1f, 1f, 0.85f), ProceduralTextures.UISprite("ring"));
                UIFactory.Fill(ring.rectTransform);
                slot.Icon = UIFactory.Image("Icon", slot.Root, Color.white, ProceduralTextures.UISprite("circle"));
                UIFactory.Fill(slot.Icon.rectTransform, 7f);
                slot.Cooldown = UIFactory.Image("Cooldown", slot.Root, new Color(0f, 0f, 0f, 0.72f), ProceduralTextures.UISprite("circle"));
                UIFactory.Fill(slot.Cooldown.rectTransform, 7f);
                slot.Cooldown.type = Image.Type.Filled;
                slot.Cooldown.fillMethod = Image.FillMethod.Radial360;
                slot.Cooldown.fillOrigin = (int)Image.Origin360.Top;
                slot.Cooldown.fillClockwise = false;
                slot.Key = UIFactory.Text("Key", slot.Root, keys[i], ult ? 34 : 28, UIColors.Text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), TextAnchor.MiddleCenter, FontStyle.Bold);
                slot.Timer = UIFactory.Text("Timer", slot.Root, "", 22, new Color(1f, 0.9f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(size, 30f));
                slot.Name = UIFactory.Text("Name", slot.Root, "", 15, UIColors.TextDim, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(150f, 22f));
                _slots[i] = slot;
            }

            // Top: boss bar.
            _bossRoot = UIFactory.Rect("Boss", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1000f, 80f));
            _bossGroup = _bossRoot.gameObject.AddComponent<CanvasGroup>();
            _bossGroup.alpha = 0f;
            _bossName = UIFactory.Text("Name", _bossRoot, "", 32, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 40f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            var boss = UIFactory.Bar("BossBar", _bossRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(1000f, 20f), UIColors.Boss, UIColors.HealthTrail);
            _bossFill = boss.fill;
            _bossTrail = boss.trail;
            UIFactory.Image("Phase2Mark", boss.root, new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 26f), ProceduralTextures.UISprite("default"));
            _bossPhase = UIFactory.Text("Phase", _bossRoot, "", 18, new Color(1f, 0.5f, 0.7f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 2f), new Vector2(200f, 24f), TextAnchor.MiddleRight, FontStyle.Bold);
            _bossRoot.gameObject.SetActive(false);

            // Right: combo counter.
            _comboRoot = UIFactory.Rect("Combo", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 120f), new Vector2(300f, 140f));
            _comboCount = UIFactory.Text("Count", _comboRoot, "", 92, Color.white, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 14f), new Vector2(300f, 100f), TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
            _comboLabel = UIFactory.Text("Label", _comboRoot, "HITS", 28, UIColors.TextDim, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, -48f), new Vector2(300f, 34f), TextAnchor.MiddleRight, FontStyle.BoldAndItalic);

            // Center: technique banner (brush stroke + callout).
            _bannerRoot = UIFactory.Rect("Banner", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1300f, 150f));
            _bannerBrush = UIFactory.Image("Brush", _bannerRoot, new Color(0.02f, 0.02f, 0.05f, 0.85f), ProceduralTextures.UISprite("brush"));
            UIFactory.Fill(_bannerBrush.rectTransform);
            _bannerForm = UIFactory.Text("Form", _bannerRoot, "", 24, UIColors.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(1200f, 30f), TextAnchor.MiddleCenter, FontStyle.Italic);
            _bannerText = UIFactory.Text("Callout", _bannerRoot, "", 54, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(1250f, 70f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            _bannerRoot.gameObject.SetActive(false);

            // Top-center: notifications.
            _notice = UIFactory.Text("Notice", root, "", 26, new Color(1f, 0.95f, 0.8f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 40f), TextAnchor.MiddleCenter, FontStyle.Bold);

            // Lock-on marker.
            _lockMarker = UIFactory.Rect("LockOn", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            _lockImage = UIFactory.Image("Diamond", _lockMarker, new Color(1f, 0.35f, 0.3f, 0.95f), ProceduralTextures.UISprite("ring"));
            UIFactory.Fill(_lockImage.rectTransform);
            var dot = UIFactory.Image("Dot", _lockMarker, new Color(1f, 0.9f, 0.9f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), ProceduralTextures.UISprite("diamond"));
            dot.raycastTarget = false;
            _lockMarker.gameObject.SetActive(false);

            // Interaction prompt.
            _prompt = UIFactory.Text("Prompt", root, "", 26, UIColors.Text, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(900f, 40f), TextAnchor.MiddleCenter, FontStyle.Bold);

            // Camera mode label.
            _cameraText = UIFactory.Text("Camera", root, "", 18, UIColors.TextDim, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -40f), new Vector2(500f, 26f), TextAnchor.MiddleRight);

            // Controls hint (fades after a while).
            _hints = UIFactory.Text("Hints", root,
                "WASD move · Shift sprint · Space jump · Alt dodge · LMB/RMB attack · Q block/parry · Tab/MMB lock-on · 1-4 techniques · R ultimate · X/Z style · C camera · E interact · Esc menu · F1 debug",
                17, UIColors.TextDim, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(1800f, 26f));

            // Death overlay.
            var death = UIFactory.Stretch("Death", root);
            _deathGroup = death.gameObject.AddComponent<CanvasGroup>();
            _deathGroup.alpha = 0f;
            var shade = UIFactory.Image("Shade", death, new Color(0.08f, 0f, 0.02f, 0.65f), ProceduralTextures.UISprite("default"));
            UIFactory.Fill(shade.rectTransform);
            _deathText = UIFactory.Text("Text", death, "DEFEATED", 110, new Color(0.9f, 0.15f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 160f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UIFactory.Text("Sub", death, "The breath fades... returning to the last shrine", 28, UIColors.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(1200f, 40f));
        }

        // ------------------------------------------------------------------ events

        private BreathingStyleSystem _boundBreathing;

        private void OnNotification(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_notices.Count > 4) _notices.Dequeue();
            _notices.Enqueue(message);
        }

        private void OnCombo(int count)
        {
            if (count <= 1)
            {
                if (count == 0) _comboAlpha = Mathf.Min(_comboAlpha, 1f);
                return;
            }
            _comboCount.text = count.ToString();
            _comboScale = 1.35f;
            _comboAlpha = 1f;
        }

        private void OnBossEncounter(string bossName, GameObject boss)
        {
            _bossTarget = boss;
            if (boss == null)
            {
                _bossRoot.gameObject.SetActive(false);
                return;
            }
            _bossRoot.gameObject.SetActive(true);
            _bossName.text = bossName;
            _bossPhase.text = "";
            _bossTrailValue = 1f;
        }

        private void OnBossPhase(GameObject boss, int phase)
        {
            _bossPhase.text = phase >= 2 ? "PHASE II" : "";
        }

        private void OnTechnique(SkillData skill, string callout)
        {
            _bannerRoot.gameObject.SetActive(true);
            _bannerText.text = callout;
            _bannerForm.text = skill.formName + (skill.tier == SkillTier.Advanced ? " · ADVANCED" : skill.tier == SkillTier.Ultimate ? " · ULTIMATE" : "");
            var pc = PlayerController.Instance;
            Color c = pc != null ? ElementPalette.Display(pc.Breathing.CurrentElement) : Color.white;
            _bannerText.color = Color.Lerp(Color.white, c, 0.35f);
            _bannerBrush.color = new Color(c.r * 0.15f, c.g * 0.15f, c.b * 0.2f, 0.88f);
            _bannerTimer = skill.tier == SkillTier.Ultimate ? 2.6f : 1.5f;
            _bannerRoot.localScale = new Vector3(1.4f, 0.6f, 1f);
        }

        private void OnPlayerDied()
        {
            _dead = true;
        }

        private void OnPlayerRespawned()
        {
            _dead = false;
        }

        private void OnCameraMode(string mode)
        {
            string label = mode == "FirstPerson" ? "FIRST PERSON" : mode == "SecondPerson" ? "SECOND PERSON (RIVAL VIEW)" : "THIRD PERSON";
            _cameraText.text = $"CAMERA: {label}  [C]";
            _cameraTextTimer = 3f;
        }

        private float _cameraTextTimer = 4f;

        private void OnStyleChanged(string id, string displayName) => _styleDirty = true;
        private bool _styleDirty = true;

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var pc = PlayerController.Instance;
            if (pc != null && pc.Breathing != _boundBreathing)
            {
                if (_boundBreathing != null) _boundBreathing.TechniqueStarted -= OnTechnique;
                _boundBreathing = pc.Breathing;
                if (_boundBreathing != null) _boundBreathing.TechniqueStarted += OnTechnique;
                _styleDirty = true;
            }

            if (pc != null) UpdateVitals(pc, dt);
            if (pc != null && _styleDirty) UpdateStyle(pc);
            if (pc != null) UpdateSkills(pc);
            UpdateBoss(dt);
            UpdateCombo(dt);
            UpdateBanner(dt);
            UpdateNotices(dt);
            UpdateLockOn(pc);
            UpdatePrompt(pc);

            _cameraTextTimer -= dt;
            _cameraText.color = new Color(UIColors.TextDim.r, UIColors.TextDim.g, UIColors.TextDim.b, Mathf.Clamp01(_cameraTextTimer));
            _hintsTimer -= dt;
            _hints.color = new Color(UIColors.TextDim.r, UIColors.TextDim.g, UIColors.TextDim.b, Mathf.Clamp01(_hintsTimer / 2f));
            _deathGroup.alpha = Mathf.MoveTowards(_deathGroup.alpha, _dead ? 1f : 0f, dt * (_dead ? 1.2f : 3f));
        }

        private void UpdateVitals(PlayerController pc, float dt)
        {
            var hp = pc.Stats.Health;
            if (hp != null)
            {
                float target = hp.Normalized;
                _hpShown = Mathf.MoveTowards(_hpShown, target, dt * 3f);
                _hpFill.fillAmount = target;
                _hpTrailValue = target > _hpTrailValue ? target : Mathf.MoveTowards(_hpTrailValue, target, dt * 0.6f);
                _hpTrail.fillAmount = _hpTrailValue;
                _hpText.text = $"{Mathf.CeilToInt(hp.Current)} / {Mathf.CeilToInt(hp.Max)}";
            }
            var st = pc.Stats.Stamina;
            _stShown = Mathf.MoveTowards(_stShown, st.Normalized, dt * 4f);
            _stFill.fillAmount = st.Normalized;
            _stTrail.fillAmount = _stShown;
            _stFill.color = st.IsExhausted ? UIColors.StaminaExhausted : UIColors.Stamina;

            var breath = pc.Stats.Breath;
            float value = breath.Current / breath.Max * 4f;
            Color c = ElementPalette.Display(pc.Breathing.CurrentElement);
            for (int i = 0; i < 4; i++)
            {
                float f = Mathf.Clamp01(value - i);
                _breathSegments[i].fillAmount = f;
                _breathSegments[i].color = f >= 1f ? Color.Lerp(c, Color.white, 0.2f) : c * 0.75f;
            }
            float pulse = breath.IsFull ? 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            _breathGlow.color = new Color(c.r, c.g, c.b, pulse);
            _breathLabel.color = breath.IsFull ? c : UIColors.TextDim;
        }

        private void UpdateStyle(PlayerController pc)
        {
            _styleDirty = false;
            var style = pc.Breathing.Current;
            if (style == null) return;
            Color c = ElementPalette.Display(style.element);
            _styleText.text = style.displayName + "   <size=18><color=#9aa0c0>[X / Z]</color></size>";
            _styleText.supportRichText = true;
            _styleIcon.color = c;
            for (int i = 0; i < 5; i++)
            {
                var skill = pc.Breathing.GetSkill(i);
                _slots[i].Name.text = skill != null ? skill.displayName : "";
                _slots[i].Icon.color = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.95f);
            }
        }

        private void UpdateSkills(PlayerController pc)
        {
            var b = pc.Breathing;
            Color c = ElementPalette.Display(b.CurrentElement);
            for (int i = 0; i < 5; i++)
            {
                var slot = _slots[i];
                float cd = b.CooldownNormalized(i);
                slot.Cooldown.fillAmount = cd;
                float remaining = b.CooldownRemaining(i);
                slot.Timer.text = remaining > 0.05f ? remaining.ToString("0.0") : "";
                bool usable = b.CanUse(i, out _);
                bool active = b.ExecutingSkill != null && b.ExecutingSkill == b.GetSkill(i);
                float glow = active ? 0.8f : (i == 4 && usable) ? 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
                slot.Glow.color = new Color(c.r, c.g, c.b, glow);
                slot.Key.color = usable ? UIColors.Text : UIColors.TextDim * 0.8f;
            }
        }

        private void UpdateBoss(float dt)
        {
            if (!_bossRoot.gameObject.activeSelf) return;
            _bossGroup.alpha = Mathf.MoveTowards(_bossGroup.alpha, 1f, dt * 2f);
            if (_bossTarget == null)
            {
                _bossRoot.gameObject.SetActive(false);
                return;
            }
            var dmg = _bossTarget.GetComponent<Damageable>();
            if (dmg == null || dmg.Health == null) return;
            float target = dmg.Health.Normalized;
            _bossFill.fillAmount = target;
            _bossTrailValue = target > _bossTrailValue ? target : Mathf.MoveTowards(_bossTrailValue, target, dt * 0.4f);
            _bossTrail.fillAmount = _bossTrailValue;
        }

        private void UpdateCombo(float dt)
        {
            _comboScale = Mathf.MoveTowards(_comboScale, 1f, dt * 4f);
            var pc = PlayerController.Instance;
            bool active = pc != null && pc.Stats.Combo.Count > 1;
            if (!active) _comboAlpha = Mathf.MoveTowards(_comboAlpha, 0f, dt * 2f);
            _comboRoot.localScale = Vector3.one * _comboScale;
            Color c = pc != null ? ElementPalette.Display(pc.Breathing.CurrentElement) : Color.white;
            _comboCount.color = new Color(Mathf.Lerp(1f, c.r, 0.4f), Mathf.Lerp(1f, c.g, 0.4f), Mathf.Lerp(1f, c.b, 0.4f), _comboAlpha);
            _comboLabel.color = new Color(UIColors.TextDim.r, UIColors.TextDim.g, UIColors.TextDim.b, _comboAlpha);
        }

        private void UpdateBanner(float dt)
        {
            if (!_bannerRoot.gameObject.activeSelf) return;
            _bannerTimer -= dt;
            _bannerRoot.localScale = Vector3.Lerp(_bannerRoot.localScale, Vector3.one, 1f - Mathf.Exp(-18f * dt));
            float a = Mathf.Clamp01(_bannerTimer * 3f);
            _bannerText.color = new Color(_bannerText.color.r, _bannerText.color.g, _bannerText.color.b, a);
            _bannerForm.color = new Color(UIColors.TextDim.r, UIColors.TextDim.g, UIColors.TextDim.b, a);
            _bannerBrush.color = new Color(_bannerBrush.color.r, _bannerBrush.color.g, _bannerBrush.color.b, 0.88f * a);
            if (_bannerTimer <= 0f) _bannerRoot.gameObject.SetActive(false);
        }

        private void UpdateNotices(float dt)
        {
            _noticeTimer -= dt;
            if (_noticeTimer <= 0f)
            {
                if (_notices.Count > 0)
                {
                    _notice.text = _notices.Dequeue();
                    _noticeTimer = 1.8f;
                }
                else _notice.text = "";
            }
            _notice.color = new Color(_notice.color.r, _notice.color.g, _notice.color.b, Mathf.Clamp01(_noticeTimer * 2f));
        }

        private void UpdateLockOn(PlayerController pc)
        {
            var target = pc != null ? pc.LockOn.CurrentPoint : null;
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Camera : Camera.main;
            if (target == null || cam == null)
            {
                _lockMarker.gameObject.SetActive(false);
                return;
            }
            Vector3 sp = cam.WorldToScreenPoint(target.position);
            if (sp.z < 0f)
            {
                _lockMarker.gameObject.SetActive(false);
                return;
            }
            _lockMarker.gameObject.SetActive(true);
            var canvasRt = (RectTransform)_canvas.transform;
            float scale = canvasRt.rect.width / Mathf.Max(1f, Screen.width);
            _lockMarker.anchoredPosition = new Vector2(sp.x * scale, sp.y * scale);
            _lockMarker.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 90f);
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f);
            _lockMarker.localScale = Vector3.one * (pc.LockOn.IsBoss ? 1.5f * s : s);
        }

        private void UpdatePrompt(PlayerController pc)
        {
            var i = pc != null ? pc.NearbyInteractable : null;
            _prompt.text = i != null ? $"[E]  {i.Prompt}" : "";
        }
    }
}
