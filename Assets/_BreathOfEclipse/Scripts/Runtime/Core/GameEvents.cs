using System;
using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Decoupled global gameplay events. Producers never know who listens (HUD, audio, camera, VFX, analytics).
    /// Listeners must unsubscribe in OnDisable/OnDestroy. Statics are reset on play-mode entry.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Any resolved hit (player or enemy).</summary>
        public static event Action<HitData, HitResult> Damage;
        public static event Action<HitData, HitResult> Critical;
        /// <summary>(victim, killer)</summary>
        public static event Action<GameObject, GameObject> EnemyKilled;
        /// <summary>(player position, attacker)</summary>
        public static event Action<Vector3, GameObject> PerfectDodge;
        /// <summary>(player position, attacker)</summary>
        public static event Action<Vector3, GameObject> Parry;
        /// <summary>(current, max, delta)</summary>
        public static event Action<float, float, float> BreathChanged;
        /// <summary>(skill id, display name, element)</summary>
        public static event Action<string, string, Element> SkillUsed;
        /// <summary>(skill id, display name, element)</summary>
        public static event Action<string, string, Element> UltimateActivated;
        public static event Action PlayerDied;
        public static event Action PlayerRespawned;
        /// <summary>(boss, phase index starting at 1)</summary>
        public static event Action<GameObject, int> BossPhaseChanged;
        /// <summary>(boss name, boss root) — null root when the encounter ends.</summary>
        public static event Action<string, GameObject> BossEncounter;
        public static event Action<GameObject> BossDefeated;
        /// <summary>(new target or null)</summary>
        public static event Action<Transform> LockTargetChanged;
        /// <summary>(mode name)</summary>
        public static event Action<string> CameraModeChanged;
        /// <summary>(style id, display name)</summary>
        public static event Action<string, string> StyleChanged;
        /// <summary>(combo count)</summary>
        public static event Action<int> ComboChanged;
        /// <summary>Short on-screen notification.</summary>
        public static event Action<string> Notification;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Damage = null;
            Critical = null;
            EnemyKilled = null;
            PerfectDodge = null;
            Parry = null;
            BreathChanged = null;
            SkillUsed = null;
            UltimateActivated = null;
            PlayerDied = null;
            PlayerRespawned = null;
            BossPhaseChanged = null;
            BossEncounter = null;
            BossDefeated = null;
            LockTargetChanged = null;
            CameraModeChanged = null;
            StyleChanged = null;
            ComboChanged = null;
            Notification = null;
        }

        public static void RaiseDamage(HitData hit, HitResult result)
        {
            Damage?.Invoke(hit, result);
            if (result.IsCritical && result.Landed) Critical?.Invoke(hit, result);
        }

        public static void RaiseEnemyKilled(GameObject victim, GameObject killer) => EnemyKilled?.Invoke(victim, killer);
        public static void RaisePerfectDodge(Vector3 position, GameObject attacker) => PerfectDodge?.Invoke(position, attacker);
        public static void RaiseParry(Vector3 position, GameObject attacker) => Parry?.Invoke(position, attacker);
        public static void RaiseBreathChanged(float current, float max, float delta) => BreathChanged?.Invoke(current, max, delta);
        public static void RaiseSkillUsed(string id, string displayName, Element element) => SkillUsed?.Invoke(id, displayName, element);
        public static void RaiseUltimateActivated(string id, string displayName, Element element) => UltimateActivated?.Invoke(id, displayName, element);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        public static void RaiseBossPhaseChanged(GameObject boss, int phase) => BossPhaseChanged?.Invoke(boss, phase);
        public static void RaiseBossEncounter(string bossName, GameObject boss) => BossEncounter?.Invoke(bossName, boss);
        public static void RaiseBossDefeated(GameObject boss) => BossDefeated?.Invoke(boss);
        public static void RaiseLockTargetChanged(Transform target) => LockTargetChanged?.Invoke(target);
        public static void RaiseCameraModeChanged(string mode) => CameraModeChanged?.Invoke(mode);
        public static void RaiseStyleChanged(string id, string displayName) => StyleChanged?.Invoke(id, displayName);
        public static void RaiseComboChanged(int count) => ComboChanged?.Invoke(count);
        public static void Notify(string message) => Notification?.Invoke(message);
    }
}
