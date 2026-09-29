using BreathOfEclipse.Breathing;
using BreathOfEclipse.CameraSystem;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.Player
{
    /// <summary>Builds the playable swordsman from data (procedural mannequin + all player components).</summary>
    public static class PlayerFactory
    {
        public static PlayerController Create(GameDatabase db, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Player");
            go.tag = "Player";
            go.layer = Layers.Player;
            go.transform.SetPositionAndRotation(position, rotation);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.02f;
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.minMoveDistance = 0f;

            // Gameplay root → visual → animator → weapon socket. The mannequin always provides the gameplay points
            // (weapon, blade base / tip, lock-on, eyes) and the procedural animator stays the animation authority;
            // the real 3D model (default: the anime swordsman on the Quaternius base body) only replaces the look.
            var rig = go.AddComponent<CharacterRig>();
            rig.Build(RigProfile.Hero(), Layers.Player);
            ICharacterAnimator animator = null;
            var profile = PlayerVisualProfile(db);
            if (profile != null && !profile.hybridAnimation)
                animator = HumanoidCharacterVisual.Attach(go.transform, rig, profile, Layers.Player);
            if (animator == null)
            {
                var procedural = go.AddComponent<ProceduralAnimator>();
                procedural.Initialize(rig);
                animator = procedural;
                if (profile != null && profile.hybridAnimation)
                    HumanoidCharacterVisual.AttachHybrid(go.transform, rig, procedural, profile, Layers.Player);
            }

            var damageable = go.AddComponent<Damageable>();
            damageable.Configure(Team.Player, db.player.maxHealth, rig.LockOnPoint);

            var stats = go.AddComponent<PlayerStats>();
            stats.Initialize(db.player, damageable);
            var motor = go.AddComponent<PlayerMotor>();
            motor.Initialize(db.player, cc);
            var defense = go.AddComponent<PlayerDefense>();
            var combat = go.AddComponent<PlayerCombat>();
            var lockOn = go.AddComponent<TargetLockSystem>();
            var breathing = go.AddComponent<BreathingStyleSystem>();
            var pc = go.AddComponent<PlayerController>();
            defense.Initialize(pc, db.player);
            damageable.AddInterceptor(defense);
            combat.Initialize(pc, db.playerCombos, db.playerWeapon);
            pc.Initialize(db.player, db.playerWeapon, db.playerCombos, db, rig, animator, motor, stats, combat, defense, lockOn, breathing, damageable);
            return pc;
        }

        /// <summary>
        /// The player's look: the GameDatabase's imported model when one is assigned, otherwise the built-in anime
        /// swordsman; null when Settings choose the procedural mannequin.
        /// </summary>
        public static CharacterVisualProfile PlayerVisualProfile(GameDatabase db)
        {
            if (SaveSystem.Settings != null && SaveSystem.Settings.playerVisualMode == 1) return null;
            if (db != null && db.playerVisual != null) return db.playerVisual;
            return CharacterVisualProfile.Runtime(VisualLook.AnimeSwordsman, LocomotionSet.Swordsman, 1.78f);
        }

        /// <summary>Connects the camera rig to the player.</summary>
        public static void BindCamera(CameraRig cam, PlayerController pc)
        {
            cam.Bind(pc.transform, pc.Rig);
            cam.LockTargetProvider = () => pc != null ? pc.LockOn.CurrentPoint : null;
            cam.LockTargetIsBossProvider = () => pc != null && pc.LockOn.IsBoss;
            cam.PlayerVelocityProvider = () => pc != null ? pc.Motor.Velocity : Vector3.zero;
            cam.PlayerSprintingProvider = () => pc != null && pc.IsSprinting;
            cam.PlayerAttackingProvider = () => pc != null && (pc.State == PlayerState.Attack || pc.State == PlayerState.Skill);
        }
    }
}
