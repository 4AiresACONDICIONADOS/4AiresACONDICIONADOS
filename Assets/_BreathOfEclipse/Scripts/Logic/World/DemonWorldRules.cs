using System;

namespace BreathOfEclipse.World
{
    /// <summary>What a world demon is doing outside the combat state machine.</summary>
    public enum DemonMode
    {
        Hidden = 0,
        Stalking = 1,
        HuntingNpc = 2,
        Combat = 3,
        Fleeing = 4,
        Resting = 5,
        Patrolling = 6
    }

    /// <summary>
    /// Rules of the demons that roam the region: how many a sector holds by time and danger, where they may appear
    /// (never in sight, never close), when they give up a chase (territory / leash / interest) and when they flee.
    /// </summary>
    public static class DemonWorldRules
    {
        /// <summary>Minimum distance from the player for a spawn that is not hidden by terrain.</summary>
        public const float MinSpawnDistance = 38f;
        /// <summary>Spawns farther than this never pop into view (fog + distance).</summary>
        public const float SafeSpawnDistance = 70f;
        /// <summary>A chase never drags a demon farther than this from its territory.</summary>
        public const float MaxLeash = 75f;

        /// <summary>
        /// Demons a sector wants alive. <paramref name="danger"/>: 0 safe (village) … 3 cursed ground.
        /// Day: almost none outside dark places; night: more everywhere except the village; exceptional presence: more.
        /// </summary>
        public static int DesiredPopulation(float hour, int danger, bool exceptionalPresence)
        {
            bool night = WorldClock.IsNight(hour);
            float daylight = WorldClock.Daylight(hour);
            int count;
            switch (danger)
            {
                case 0: count = 0; break;
                case 1: count = night ? 1 : 0; break;
                case 2: count = night ? 2 : (daylight < 0.6f ? 1 : 0); break;
                default: count = night ? 4 : 2; break; // cursed ground: always dark under the canopy
            }
            if (exceptionalPresence && danger > 0) count += danger >= 2 ? 2 : 1;
            return count;
        }

        /// <summary>Whether a spawn point is acceptable: far enough, and out of the player's view unless very far.</summary>
        public static bool CanSpawnAt(float distanceToPlayer, bool visibleToPlayer)
        {
            if (distanceToPlayer < MinSpawnDistance) return false;
            if (distanceToPlayer >= SafeSpawnDistance) return true;
            return !visibleToPlayer;
        }

        /// <summary>
        /// A wounded demon decides to run: below 30 % health (25 % for elites), sooner when outnumbered, later when it
        /// already escaped once (it learned to fight back).
        /// </summary>
        public static bool ShouldFlee(float healthFraction, int opponents, bool escapedBefore, bool elite, float roll)
        {
            float threshold = elite ? 0.25f : 0.3f;
            if (opponents >= 2) threshold += 0.08f;
            if (escapedBefore) threshold -= 0.1f;
            if (healthFraction > threshold) return false;
            // Not every demon runs: some fight to the end.
            float chance = elite ? 0.55f : 0.75f;
            return roll < chance;
        }

        /// <summary>Gives up a chase when dragged too far from its territory or after losing interest.</summary>
        public static bool ShouldDisengage(float distanceFromTerritory, float territoryRadius, float secondsWithoutSight, bool targetInLight)
        {
            float leash = Math.Min(MaxLeash, territoryRadius + 45f);
            if (distanceFromTerritory > leash) return true;
            if (secondsWithoutSight > 9f) return true;
            // Lanterns and fire unnerve them: a lit target out of reach is not worth it.
            return targetInLight && secondsWithoutSight > 3f;
        }

        /// <summary>How far ahead a stalking demon keeps from its prey (metres): close in the dark, farther near light.</summary>
        public static float StalkDistance(float daylight, bool preyInLight) => 14f + daylight * 18f + (preyInLight ? 10f : 0f);
    }
}
