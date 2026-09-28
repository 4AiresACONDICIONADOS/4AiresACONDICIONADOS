using BreathOfEclipse.Audio;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>Presentation parameters that accompany a hit (from AttackData, HitSpec or enemy attacks).</summary>
    public struct HitFeel
    {
        public float CameraShake;
        public float FovPunch;
        public float Zoom;
        public float ZoomDuration;
        public string HitSfx;
        public string ImpactVfx;
        public bool GroundImpact;
        public bool SpeedLines;
        public float SlowMoScale;
        public float SlowMoDuration;
    }

    /// <summary>
    /// The "anime hit" recipe applied to every landed hit: hit stop + camera impulse + impact VFX + sound +
    /// flash frame when the hit is special. Keeps combat feel consistent across attacks, techniques and enemies.
    /// </summary>
    public static class CombatFeedback
    {
        private static int _lastFrame = -1;
        private static int _hitsThisFrame;

        public static void PlayerHitLanded(HitData hit, HitResult result, HitFeel feel, Element styleElement)
        {
            bool firstThisFrame = Time.frameCount != _lastFrame;
            if (firstThisFrame)
            {
                _lastFrame = Time.frameCount;
                _hitsThisFrame = 0;
            }
            _hitsThisFrame++;

            Vector3 point = result.HitPoint;
            var cam = Camera.main;
            Quaternion facing = cam != null ? Quaternion.LookRotation(cam.transform.position - point) : Quaternion.identity;
            Element vfxElement = hit.Element != Element.None ? hit.Element : styleElement;

            if (result.Outcome == HitOutcome.Blocked)
            {
                VFXLibrary.Spawn("block_spark", point, facing);
                Sfx.Play("block", point, 0.8f);
                if (firstThisFrame) CameraFX.Shake(0.12f);
                HitStopManager.Apply(DamageCategory.Light, false, 0.03f);
                return;
            }

            // Only the first target of a multi-hit frame drives global effects (no stacking).
            if (firstThisFrame)
            {
                HitStopManager.Apply(hit.Category, result.IsCritical, hit.HitStop);
                float shake = feel.CameraShake * (result.IsCritical ? 1.5f : 1f);
                if (shake > 0f) CameraFX.Shake(shake);
                CameraFX.Impulse(hit.Direction, 1.5f + shake * 6f);
                if (feel.FovPunch != 0f) CameraFX.FovPunch(feel.FovPunch);
                else if (result.IsCritical) CameraFX.FovPunch(-4f);
                if (feel.Zoom > 0f && !Mathf.Approximately(feel.Zoom, 1f)) CameraFX.Zoom(feel.Zoom, feel.ZoomDuration > 0f ? feel.ZoomDuration : 0.35f);
                else if (result.IsCritical || hit.IsFinisher) CameraFX.Zoom(0.82f, 0.35f);
                if (feel.SlowMoDuration > 0f && feel.SlowMoScale < 1f && Core.TimeController.Instance != null)
                    Core.TimeController.Instance.SlowMotion(feel.SlowMoScale, feel.SlowMoDuration, "hit_slowmo");
                if (feel.SpeedLines || hit.IsFinisher) ScreenFX.SpeedLines(0.6f, 0.25f);
                if (result.IsCritical || hit.IsFinisher)
                {
                    var post = CameraFX.Post;
                    if (post != null)
                    {
                        post.PulseChromatic(0.45f);
                        post.PulseBloom(0.8f);
                    }
                }
                if (hit.FlashFrame || (hit.IsFinisher && result.IsCritical))
                    FlashFrameSystem.Trigger(point, ElementPalette.Get(vfxElement).Core, hit.Category == DamageCategory.Ultimate ? 3 : 2);
                if (feel.GroundImpact)
                {
                    HitQuery.GroundPoint(point, out var ground, out _);
                    VFXLibrary.Spawn("ground_impact", ground, Quaternion.LookRotation(hit.Direction.sqrMagnitude > 0.01f ? hit.Direction : Vector3.forward), 1f, vfxElement);
                }
            }

            string impact = !string.IsNullOrEmpty(feel.ImpactVfx) ? feel.ImpactVfx
                : result.IsCritical ? "impact_crit"
                : hit.Category == DamageCategory.Heavy || hit.Category == DamageCategory.Finisher ? "impact_heavy" : "impact_slash";
            if (_hitsThisFrame <= 4)
            {
                VFXLibrary.Spawn(impact, point, facing, 1f, vfxElement);
                if (result.IsCritical && impact != "impact_crit") VFXLibrary.Spawn("impact_crit", point, facing, 0.8f, vfxElement);
                if (styleElement != Element.None && hit.Category == DamageCategory.Light) VFXLibrary.Spawn(SparkId(styleElement), point, facing, 1f, styleElement);
                if (result.Target != null && result.Target.GetComponent<AI.EnemyController>() != null) VFXLibrary.Spawn("demon_hit", point, facing);
            }

            if (firstThisFrame)
            {
                string sfx = !string.IsNullOrEmpty(feel.HitSfx) ? feel.HitSfx : "hit";
                if (result.IsCritical) sfx = "hit_crit";
                Sfx.Play(sfx, point, 1f);
            }
        }

        /// <summary>The player got hit by an enemy.</summary>
        public static void PlayerDamaged(HitData hit, HitResult result)
        {
            if (result.Outcome == HitOutcome.Blocked)
            {
                VFXLibrary.Spawn("block_spark", result.HitPoint, Quaternion.LookRotation(-hit.Direction.normalized + Vector3.up * 0.01f));
                Sfx.Play("block", result.HitPoint);
                CameraFX.Shake(0.18f);
                HitStopManager.Apply(DamageCategory.Light, false, 0.035f);
                return;
            }
            if (!result.Landed) return;
            bool heavy = hit.Category == DamageCategory.EnemyHeavy || hit.Reaction >= HitReaction.Knockback;
            HitStopManager.Apply(heavy ? DamageCategory.EnemyHeavy : DamageCategory.EnemyLight, false, heavy ? 0.06f : 0.03f);
            CameraFX.Shake(heavy ? 0.55f : 0.3f);
            CameraFX.Impulse(hit.Direction, heavy ? 6f : 3f);
            VFXLibrary.Spawn("impact_dark", result.HitPoint, Quaternion.identity);
            Sfx.Play(heavy ? "hit_heavy" : "hit", result.HitPoint);
            var post = CameraFX.Post;
            if (post != null)
            {
                post.PulseVignette(heavy ? 0.35f : 0.2f);
                post.PulseChromatic(heavy ? 0.4f : 0.2f);
            }
            ScreenFX.Wash(new Color(0.8f, 0.05f, 0.1f), heavy ? 0.22f : 0.12f, 2f);
        }

        public static string SparkId(Element e)
        {
            switch (e)
            {
                case Element.Water: return "spark_water";
                case Element.Fire: return "spark_fire";
                case Element.Thunder: return "spark_thunder";
                case Element.Wind: return "spark_wind";
                case Element.Moon: return "spark_moon";
                default: return "impact_slash";
            }
        }

        public static string AuraId(Element e)
        {
            switch (e)
            {
                case Element.Water: return "aura_water";
                case Element.Fire: return "aura_fire";
                case Element.Thunder: return "aura_thunder";
                case Element.Wind: return "aura_wind";
                case Element.Moon: return "aura_moon";
                default: return null;
            }
        }
    }
}
