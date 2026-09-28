using System.Collections.Generic;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace BreathOfEclipse.UI
{
    /// <summary>
    /// Optional, restrained damage numbers (not MMO-sized): normal, critical and elemental colors.
    /// Pooled text elements on the HUD canvas that follow the hit point and float up.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private sealed class Entry
        {
            public Text Text;
            public Vector3 World;
            public float Age;
            public float Life;
            public float Scale;
            public Vector2 Drift;
            public bool Active;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private RectTransform _root;
        private Canvas _canvas;

        public static DamageNumbers Create(Canvas canvas)
        {
            var root = UIFactory.Stretch("DamageNumbers", canvas.transform);
            root.SetAsFirstSibling();
            var dn = root.gameObject.AddComponent<DamageNumbers>();
            dn._root = root;
            dn._canvas = canvas;
            return dn;
        }

        private void OnEnable() => GameEvents.Damage += OnDamage;
        private void OnDisable() => GameEvents.Damage -= OnDamage;

        private void OnDamage(HitData hit, HitResult result)
        {
            if (!SaveSystem.Settings.showDamageNumbers) return;
            if (result.Outcome == HitOutcome.Ignored) return;
            if (result.Target != null && result.Target.GetComponent<Destructible>() != null) return;
            bool playerHurt = result.Target != null && result.Target.GetComponent<Player.PlayerController>() != null;

            string label;
            Color color;
            int size;
            switch (result.Outcome)
            {
                case HitOutcome.Evaded: label = "MISS"; color = new Color(0.7f, 0.8f, 1f); size = 22; break;
                case HitOutcome.PerfectEvaded: label = "PERFECT"; color = new Color(0.5f, 0.9f, 1f); size = 30; break;
                case HitOutcome.Parried: label = "PARRY"; color = new Color(1f, 0.85f, 0.35f); size = 32; break;
                case HitOutcome.Blocked: label = result.Damage > 0 ? result.Damage.ToString() : "BLOCK"; color = new Color(0.65f, 0.65f, 0.7f); size = 22; break;
                default:
                    if (result.Damage <= 0) return;
                    label = result.Damage.ToString();
                    size = result.IsCritical ? 40 : 28;
                    if (playerHurt) color = new Color(1f, 0.3f, 0.3f);
                    else if (result.IsCritical) color = new Color(1f, 0.78f, 0.2f);
                    else if (hit.Element != Element.None) color = Color.Lerp(ElementPalette.Display(hit.Element), Color.white, 0.25f);
                    else color = Color.white;
                    if (result.IsCritical) label += "!";
                    if (result.Weakness) size += 6;
                    break;
            }
            var e = Rent();
            e.Text.text = label;
            e.Text.color = color;
            e.Text.fontSize = size;
            e.Text.fontStyle = result.IsCritical ? FontStyle.BoldAndItalic : FontStyle.Bold;
            e.World = result.HitPoint + Vector3.up * 0.3f;
            e.Age = 0f;
            e.Life = result.IsCritical ? 1.1f : 0.8f;
            e.Scale = result.IsCritical ? 1.6f : 1.2f;
            e.Drift = new Vector2(Random.Range(-40f, 40f), Random.Range(60f, 90f));
            e.Active = true;
            e.Text.gameObject.SetActive(true);
        }

        private Entry Rent()
        {
            foreach (var e in _entries) if (!e.Active) return e;
            if (_entries.Count >= 40)
            {
                // Recycle the oldest.
                Entry oldest = _entries[0];
                foreach (var e in _entries) if (e.Age > oldest.Age) oldest = e;
                return oldest;
            }
            var t = UIFactory.Text("Num", _root, "", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.zero;
            t.rectTransform.sizeDelta = new Vector2(220f, 50f);
            var entry = new Entry { Text = t };
            _entries.Add(entry);
            return entry;
        }

        private void LateUpdate()
        {
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Camera : Camera.main;
            float scale = ((RectTransform)_canvas.transform).rect.width / Mathf.Max(1f, Screen.width);
            float dt = Time.unscaledDeltaTime;
            foreach (var e in _entries)
            {
                if (!e.Active) continue;
                e.Age += dt;
                if (e.Age >= e.Life || cam == null)
                {
                    e.Active = false;
                    e.Text.gameObject.SetActive(false);
                    continue;
                }
                Vector3 sp = cam.WorldToScreenPoint(e.World);
                if (sp.z < 0f)
                {
                    e.Text.gameObject.SetActive(false);
                    continue;
                }
                e.Text.gameObject.SetActive(true);
                float t = e.Age / e.Life;
                Vector2 pos = new Vector2(sp.x, sp.y) * scale + e.Drift * EaseOut(t);
                e.Text.rectTransform.anchoredPosition = pos;
                float pop = t < 0.12f ? Mathf.Lerp(e.Scale * 1.4f, e.Scale, t / 0.12f) : Mathf.Lerp(e.Scale, e.Scale * 0.8f, (t - 0.12f) / 0.88f);
                e.Text.rectTransform.localScale = Vector3.one * pop * 0.8f;
                var c = e.Text.color;
                c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                e.Text.color = c;
            }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
