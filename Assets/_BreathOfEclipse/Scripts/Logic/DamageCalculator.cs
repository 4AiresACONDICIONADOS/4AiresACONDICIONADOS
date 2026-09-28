using BreathOfEclipse.Core;

namespace BreathOfEclipse.Combat
{
    /// <summary>All inputs needed to resolve one hit. Plain data so it can be unit tested.</summary>
    public struct DamageRequest
    {
        public float BaseDamage;
        /// <summary>Attack / technique multiplier (1 = weapon base damage).</summary>
        public float Multiplier;
        /// <summary>Additive bonus from buffs and breathing style (0.2 = +20%).</summary>
        public float BonusPercent;
        public float CritChance;
        public float CritMultiplier;
        /// <summary>Forces a critical hit (parry counters, perfect dodge counters, flagged techniques).</summary>
        public bool ForceCritical;
        public Element Element;
        /// <summary>Element the defender is weak against (takes <see cref="DamageCalculator.WeaknessMultiplier"/>).</summary>
        public Element DefenderWeakness;
        /// <summary>Element the defender resists.</summary>
        public Element DefenderResistance;
        public float Defense;
        public bool Blocked;
        /// <summary>0..1 portion of damage removed by a block.</summary>
        public float BlockReduction;
        /// <summary>Random roll in [0,1). Supplied externally for determinism.</summary>
        public float CritRoll;

        public static DamageRequest Simple(float baseDamage, float multiplier = 1f)
        {
            return new DamageRequest
            {
                BaseDamage = baseDamage,
                Multiplier = multiplier,
                CritMultiplier = 1.5f,
                CritRoll = 1f
            };
        }
    }

    public struct DamageResult
    {
        public int Amount;
        public bool IsCritical;
        public bool WasBlocked;
        public bool ElementalWeakness;
        public bool Resisted;
    }

    /// <summary>Stateless damage formula shared by player, enemies and destructibles.</summary>
    public static class DamageCalculator
    {
        public const float WeaknessMultiplier = 1.35f;
        public const float ResistanceMultiplier = 0.7f;
        public const float DefenseConstant = 100f;
        public const int MinimumDamage = 1;

        public static DamageResult Calculate(DamageRequest r)
        {
            var result = new DamageResult();
            float multiplier = r.Multiplier <= 0f ? 1f : r.Multiplier;
            float raw = r.BaseDamage * multiplier * (1f + MathUtil.Max(-0.9f, r.BonusPercent));

            bool crit = r.ForceCritical || r.CritRoll < MathUtil.Clamp01(r.CritChance);
            if (crit)
            {
                raw *= r.CritMultiplier < 1f ? 1f : r.CritMultiplier;
                result.IsCritical = true;
            }

            if (r.Element != Element.None)
            {
                if (r.Element == r.DefenderWeakness)
                {
                    raw *= WeaknessMultiplier;
                    result.ElementalWeakness = true;
                }
                else if (r.Element == r.DefenderResistance)
                {
                    raw *= ResistanceMultiplier;
                    result.Resisted = true;
                }
            }

            if (r.Defense > 0f) raw *= DefenseConstant / (DefenseConstant + r.Defense);

            if (r.Blocked)
            {
                raw *= 1f - MathUtil.Clamp01(r.BlockReduction);
                result.WasBlocked = true;
            }

            int amount = MathUtil.RoundToInt(raw);
            if (r.BaseDamage > 0f && amount < MinimumDamage) amount = MinimumDamage;
            result.Amount = amount;
            return result;
        }
    }
}
