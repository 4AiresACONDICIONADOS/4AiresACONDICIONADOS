using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Data
{
    /// <summary>Fluent helpers used by <see cref="DefaultContent"/> to author technique timelines compactly.</summary>
    public static class PhaseBuilder
    {
        public static SkillPhase Phase(string name, float duration, string motion = null, float blendIn = 0.08f)
        {
            return new SkillPhase { name = name, duration = duration, motionId = motion, blendIn = blendIn };
        }

        public static SkillPhase MotionSpan(this SkillPhase p, float seconds)
        {
            p.motionDuration = seconds;
            return p;
        }

        public static SkillPhase Move(this SkillPhase p, SkillMoveMode mode, float distance = 0f, float height = 0f)
        {
            p.movement = mode;
            p.moveDistance = distance;
            p.moveHeight = height;
            return p;
        }

        public static SkillPhase Invulnerable(this SkillPhase p)
        {
            p.invulnerable = true;
            return p;
        }

        public static SkillPhase NoGravity(this SkillPhase p)
        {
            p.suspendGravity = true;
            return p;
        }

        public static SkillPhase Hide(this SkillPhase p)
        {
            p.hideCharacter = true;
            return p;
        }

        public static SkillPhase Afterimages(this SkillPhase p)
        {
            p.afterimages = true;
            return p;
        }

        public static SkillPhase Trail(this SkillPhase p, TrailMode mode)
        {
            p.trail = mode;
            return p;
        }

        public static SkillPhase Voice(this SkillPhase p, VoiceCue cue)
        {
            p.voiceCue = cue;
            return p;
        }

        public static SkillPhase Cancelable(this SkillPhase p)
        {
            p.allowCancel = true;
            return p;
        }

        public static SkillPhase Behaviour(this SkillPhase p, string id)
        {
            p.customBehaviour = id;
            return p;
        }

        public static SkillPhase Vfx(this SkillPhase p, string id, VFXAnchor anchor, float delay = 0f, float scale = 1f, bool follow = false,
            Vector3 offset = default, Vector3 rotation = default, float lifetime = 0f)
        {
            p.vfx.Add(new VFXCue { vfxId = id, anchor = anchor, delay = delay, scale = scale, follow = follow, offset = offset, rotation = rotation, lifetime = lifetime });
            return p;
        }

        public static SkillPhase Sfx(this SkillPhase p, string id, float delay = 0f, float volume = 1f, float pitch = 1f)
        {
            p.sfx.Add(new SfxCue(id, delay, volume, pitch));
            return p;
        }

        public static SkillPhase Hit(this SkillPhase p, HitSpec spec)
        {
            p.hits.Add(spec);
            return p;
        }

        public static SkillPhase Projectile(this SkillPhase p, ProjectileSpec spec)
        {
            spec.enabled = true;
            p.projectile = spec;
            return p;
        }

        public static SkillPhase Buff(this SkillPhase p, StatType stat, float magnitude, float duration)
        {
            p.buff = new BuffSpec { enabled = true, stat = stat, magnitude = magnitude, duration = duration };
            return p;
        }

        public static SkillPhase Cam(this SkillPhase p, float shake = 0f, float fovPunch = 0f, float zoom = 1f, float slowScale = 1f, float slowDuration = 0f,
            float saturation = 0f, float chromatic = 0f, float lens = 0f, float bloom = 0f, bool speedLines = false, bool radialBlur = false, bool flashFrame = false)
        {
            var c = p.camera;
            c.shake = shake;
            c.fovPunch = fovPunch;
            c.zoom = zoom;
            c.slowMoScale = slowScale;
            c.slowMoDuration = slowDuration;
            c.saturation = saturation;
            c.chromatic = chromatic;
            c.lensDistortion = lens;
            c.bloomBoost = bloom;
            c.speedLines = speedLines;
            c.radialBlur = radialBlur;
            c.flashFrame = flashFrame;
            return p;
        }

        public static SkillPhase Shot(this SkillPhase p, CinematicShot shot, float distance = 5f, float height = 1.5f)
        {
            p.camera.shot = shot;
            p.camera.shotDistance = distance;
            p.camera.shotHeight = height;
            return p;
        }

        public static HitSpec Spec(HitShape shape, float damage, HitReaction reaction = HitReaction.Heavy, float radius = 2f, float range = 2.5f,
            float delay = 0f, int count = 1, float interval = 0.1f, float knockback = 3f, float launch = 0f, float airHang = 0f,
            float hitStop = 0.06f, float shake = 0.25f, string impact = null, string sfx = "hit_heavy", float angle = 90f)
        {
            return new HitSpec
            {
                shape = shape,
                damageMultiplier = damage,
                reaction = reaction,
                radius = radius,
                range = range,
                delay = delay,
                hitCount = count,
                hitInterval = interval,
                knockback = knockback,
                launchHeight = launch,
                airHang = airHang,
                hitStop = hitStop,
                cameraShake = shake,
                impactVfx = impact,
                hitSfx = sfx,
                angle = angle,
                poiseDamage = 20f + damage * 10f
            };
        }

        public static HitSpec Finisher(this HitSpec s, bool flash = true)
        {
            s.isFinisher = true;
            s.flashFrame = flash;
            return s;
        }

        public static HitSpec Crit(this HitSpec s)
        {
            s.forceCritical = true;
            return s;
        }

        public static HitSpec Status(this HitSpec s, float seconds)
        {
            s.statusDuration = seconds;
            return s;
        }

        public static HitSpec Pull(this HitSpec s, float strength)
        {
            s.pullStrength = strength;
            return s;
        }

        public static HitSpec Targets(this HitSpec s, int max)
        {
            s.maxTargets = max;
            return s;
        }

        public static HitSpec Detached(this HitSpec s)
        {
            s.detached = true;
            return s;
        }
    }
}
