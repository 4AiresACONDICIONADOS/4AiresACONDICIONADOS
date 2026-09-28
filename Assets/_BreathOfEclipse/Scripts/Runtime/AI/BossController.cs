using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    /// <summary>
    /// Mini boss layer on top of <see cref="EnemyController"/>: encounter start (boss bar + music), phase 2 at
    /// ~50% HP (transformation, aura, speed, new attacks, music, aggression) and the defeat finale.
    /// </summary>
    public sealed class BossController : MonoBehaviour
    {
        public EnemyController Enemy { get; private set; }
        public bool EncounterStarted { get; private set; }
        public int Phase => Enemy != null ? Enemy.Phase : 1;

        private VFXInstance _aura;
        private bool _transitioning;

        public void Initialize(EnemyController enemy)
        {
            Enemy = enemy;
            enemy.SuperArmor = true;
            enemy.Damageable.Health.Damaged += OnDamaged;
            enemy.Damageable.Died += OnDied;
        }

        private void Update()
        {
            if (Enemy == null || !Enemy.IsAlive) return;
            if (!EncounterStarted && Enemy.Aware) StartEncounter();
        }

        public void StartEncounter()
        {
            if (EncounterStarted) return;
            EncounterStarted = true;
            GameEvents.RaiseBossEncounter(Enemy.Data.displayName.ToUpperInvariant(), Enemy.gameObject);
            Sfx.Music(Enemy.Data.phase1Music, 1.2f);
            Enemy.Alert();
        }

        private void OnDamaged(float amount, float current)
        {
            if (_transitioning || Enemy.Phase >= 2) return;
            if (current / Enemy.Damageable.Health.Max <= Enemy.Data.phase2Threshold) BeginPhase2();
        }

        private void BeginPhase2()
        {
            _transitioning = true;
            var e = Enemy;
            e.Damageable.Invulnerable = true;
            var special = e.GetState<SpecialState>(EnemyStateId.Special);
            special.Duration = 2.6f;
            special.OnDone = FinishTransition;
            e.ChangeState(EnemyStateId.Special);
            e.Anim.PlayMotion("OniRoar", 2.6f, 0.15f);
            e.PlaySfx("boss_roar", 1f);
            CameraFX.Shake(0.9f);
            CameraFX.Zoom(1.3f, 2.4f, 0.3f, 0.6f);
            ScreenFX.Wash(new Color(0.6f, 0f, 0.3f), 0.35f, 0.6f);
            var post = CameraFX.Post;
            if (post != null)
            {
                post.HoldSaturation("boss_phase2", -60f, 2.2f);
                post.PulseChromatic(0.6f);
                post.PulseLensDistortion(-0.35f);
            }
            GameEvents.Notify("THE HOLLOW ONI AWAKENS");
            // The change must read instantly, without text: eyes blaze, markings ignite, the aura erupts and the
            // night itself turns crimson.
            e.Rig.SetEyeColor(e.Data.phase2EyeColor * 1.5f);
            e.Rig.SetAccentColor(e.Data.phase2EyeColor * 0.6f);
            e.Rig.SetHitFlash(1f, new Color(1f, 0.2f, 0.45f));
            _aura = VFXLibrary.Spawn("boss_aura", e.transform.position, Quaternion.identity, e.Data.scale * 0.8f, Element.Dark, e.transform);
            FlashFrameSystem.Trigger(e.LockOnPoint.position, new Color(3f, 0.4f, 1.2f), 2, FlashFrameStyle.Silhouette);
            ShiftNight(PhaseTwoFog, PhaseTwoAmbient, PhaseTwoSun, 2.2f);
            // Push the player back with a shockwave.
            VFXLibrary.Spawn("shockwave", e.transform.position, Quaternion.identity, 2f, Element.Dark);
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                Vector3 away = pc.transform.position - e.transform.position;
                away.y = 0f;
                if (away.magnitude < 9f) pc.Motor.AddKnockback(away.normalized * 14f);
            }
            e.Phase = 2;
            GameEvents.RaiseBossPhaseChanged(e.gameObject, 2);
            Sfx.Music(e.Data.phase2Music, 0.8f);
        }

        private void FinishTransition()
        {
            var e = Enemy;
            _transitioning = false;
            e.Damageable.Invulnerable = false;
            e.SpeedMultiplier = e.Data.phase2SpeedMultiplier;
            e.AggressionBonus = e.Data.phase2AggressionBonus;
            e.Rig.SetEyeColor(e.Data.phase2EyeColor);
        }

        private static readonly Color PhaseTwoFog = new Color(0.16f, 0.04f, 0.09f);
        private static readonly Color PhaseTwoAmbient = new Color(0.3f, 0.1f, 0.2f);
        private static readonly Color PhaseTwoSun = new Color(1f, 0.55f, 0.65f);
        private bool _nightSaved;
        private Color _savedFog, _savedAmbient, _savedSun;

        private void ShiftNight(Color fog, Color ambient, Color sun, float seconds)
        {
            if (!_nightSaved)
            {
                _nightSaved = true;
                _savedFog = RenderSettings.fogColor;
                _savedAmbient = RenderSettings.ambientSkyColor;
                _savedSun = RenderSettings.sun != null ? RenderSettings.sun.color : Color.white;
            }
            NightShift.Run(fog, ambient, sun, seconds);
        }

        private void OnDied(HitData hit)
        {
            if (_aura != null) _aura.StopEmitting();
            if (_nightSaved) NightShift.Run(_savedFog, _savedAmbient, _savedSun, 3f);
            if (TimeController.Instance != null) TimeController.Instance.SlowMotion(0.2f, 1.6f, "boss_death", 0.5f);
            FlashFrameSystem.Trigger(Enemy.LockOnPoint.position, new Color(4f, 3f, 3.5f), 3, FlashFrameStyle.Silhouette);
            CameraFX.Zoom(0.7f, 1.5f);
            CameraFX.Shake(1f);
            GameEvents.RaiseBossDefeated(Enemy.gameObject);
            GameEvents.RaiseBossEncounter(null, null);
            GameEvents.Notify("THE HOLLOW ONI HAS FALLEN");
            Sfx.Music("victory", 2f);
        }

        private void OnDestroy()
        {
            if (Enemy != null && Enemy.Damageable != null && Enemy.Damageable.Health != null)
                Enemy.Damageable.Health.Damaged -= OnDamaged;
        }
    }

    /// <summary>
    /// Slight environment reaction for boss phases: fog, sky ambient and moonlight drift toward a target over a
    /// few seconds. Runs on its own scene object so it completes even if the boss is removed meanwhile.
    /// </summary>
    internal sealed class NightShift : MonoBehaviour
    {
        private Color _fog0, _amb0, _sun0, _fog, _amb, _sun;
        private float _seconds, _t;

        public static void Run(Color fog, Color ambient, Color sun, float seconds)
        {
            foreach (var old in FindObjectsByType<NightShift>(FindObjectsSortMode.None)) Destroy(old.gameObject);
            var n = new GameObject("NightShift").AddComponent<NightShift>();
            n._fog0 = RenderSettings.fogColor;
            n._amb0 = RenderSettings.ambientSkyColor;
            n._sun0 = RenderSettings.sun != null ? RenderSettings.sun.color : Color.white;
            n._fog = fog;
            n._amb = ambient;
            n._sun = sun;
            n._seconds = Mathf.Max(0.05f, seconds);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / _seconds));
            RenderSettings.fogColor = Color.Lerp(_fog0, _fog, k);
            RenderSettings.ambientSkyColor = Color.Lerp(_amb0, _amb, k);
            if (RenderSettings.sun != null) RenderSettings.sun.color = Color.Lerp(_sun0, _sun, k);
            if (_t >= _seconds) Destroy(gameObject);
        }
    }
}
