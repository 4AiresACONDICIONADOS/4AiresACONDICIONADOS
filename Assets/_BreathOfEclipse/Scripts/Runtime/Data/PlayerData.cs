using UnityEngine;

namespace BreathOfEclipse.Data
{
    /// <summary>Player tuning: movement, defense timings and resources.</summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerData : ScriptableObject
    {
        [Header("Resources")]
        public float maxHealth = 320f;
        public float maxStamina = 100f;
        public float staminaRegen = 42f;
        public float staminaRegenDelay = 0.4f;
        public float maxBreath = 100f;

        [Header("Movement")]
        public float walkSpeed = 2.4f;
        public float runSpeed = 6.2f;
        public float sprintSpeed = 9.6f;
        public float acceleration = 38f;
        public float airControl = 0.55f;
        public float turnSpeed = 900f;
        public float gravity = -28f;
        public float jumpHeight = 1.7f;
        public int airJumps = 1;
        public float sprintStaminaPerSecond = 9f;

        [Header("Dodge")]
        public float dodgeDistance = 5.2f;
        public float dodgeDuration = 0.32f;
        public float dodgeInvulnerability = 0.26f;
        public float dodgeStaminaCost = 20f;
        [Tooltip("A hit arriving within this many seconds after the dodge starts triggers a perfect dodge.")]
        public float perfectDodgeWindow = 0.18f;
        public float perfectDodgeSlowScale = 0.25f;
        public float perfectDodgeSlowDuration = 0.4f;
        public float counterWindow = 1.1f;

        [Header("Block / Parry")]
        [Tooltip("Hits within this many seconds after pressing block are parried.")]
        public float parryWindow = 0.17f;
        [Range(0f, 1f)] public float blockReduction = 0.8f;
        public float blockStaminaPerHit = 14f;
        public float blockMoveSpeed = 2.2f;
        public float parrySlowScale = 0.2f;
        public float parrySlowDuration = 0.28f;

        [Header("Damage taken")]
        public float hitStunLight = 0.32f;
        public float hitStunHeavy = 0.55f;
        [Tooltip("Seconds lying on the ground once a knockdown / launch lands.")]
        public float knockdownGroundTime = 0.55f;
        [Tooltip("Seconds to get back up (blend to the combat stance, brief invulnerability, dodge allowed).")]
        public float getUpDuration = 0.45f;
        public float respawnDelay = 3f;
    }
}
