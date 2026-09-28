namespace BreathOfEclipse.Combat
{
    /// <summary>Elemental affinity of an attack, technique or VFX palette.</summary>
    public enum Element
    {
        None = 0,
        Water = 1,
        Fire = 2,
        Thunder = 3,
        Wind = 4,
        Moon = 5,
        Dark = 6
    }

    /// <summary>How a target reacts when a hit lands.</summary>
    public enum HitReaction
    {
        None = 0,
        Light = 1,
        Heavy = 2,
        Knockback = 3,
        Launch = 4,
        Knockdown = 5,
        Stun = 6
    }

    /// <summary>What happened when damage was applied to a defender.</summary>
    public enum HitOutcome
    {
        Ignored = 0,
        Hit = 1,
        Blocked = 2,
        Parried = 3,
        Evaded = 4,
        PerfectEvaded = 5,
        Killed = 6
    }

    /// <summary>Combo inputs understood by <see cref="ComboGraph"/>.</summary>
    public enum ComboInput
    {
        Light = 0,
        Heavy = 1
    }

    /// <summary>Contextual starting points for a combo string.</summary>
    public enum ComboEntry
    {
        GroundLight = 0,
        GroundHeavy = 1,
        DashLight = 2,
        AirLight = 3,
        AirHeavy = 4,
        PerfectDodgeLight = 5,
        ParryHeavy = 6,
        DashHeavy = 7
    }

    /// <summary>Flags describing which actions may interrupt an attack during a window.</summary>
    [System.Flags]
    public enum CancelFlags
    {
        None = 0,
        Attack = 1 << 0,
        Dodge = 1 << 1,
        Skill = 1 << 2,
        Block = 1 << 3,
        Ultimate = 1 << 4,
        Jump = 1 << 5,
        Move = 1 << 6,
        All = Attack | Dodge | Skill | Block | Ultimate | Jump | Move
    }

    /// <summary>Stats that can be modified by buffs and breathing styles.</summary>
    public enum StatType
    {
        Damage = 0,
        MoveSpeed = 1,
        CritChance = 2,
        StaminaRegen = 3,
        Defense = 4,
        AttackSpeed = 5,
        BreathGain = 6
    }
}
