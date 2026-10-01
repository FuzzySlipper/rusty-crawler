namespace PartyRpg.Kit.Combat;

/// <summary>A chance that one part of a damage roll counts several times over.</summary>
/// <remarks>
/// <para>
/// A game may state that a blow sometimes does double or triple what one of its parts rolled — one weapon of
/// two, say, with the arm behind it added only once. This is that statement in the kit's own terms: a part of
/// the roll, named as a run of its dice and a share of its bonus, a <see cref="Chance"/> drawn once per roll,
/// and the <see cref="Factor"/> the part counts for when the draw lands. What the part stands for and when a
/// game states one are the ruleset's; the kit only rolls it.
/// </para>
/// <para>
/// The part is a run of the roll's own dice rather than dice of its own, so one roll stays one handful of dice
/// and the part's dice are the same draws the rest of the roll counted.
/// </para>
/// </remarks>
public readonly record struct DamageMultiplier
{
    /// <summary>States a multiplied part.</summary>
    /// <param name="firstDie">The first of the roll's dice the part covers, counted from zero.</param>
    /// <param name="dice">How many of the roll's dice the part covers, from the first; zero covers none.</param>
    /// <param name="bonus">How much of the roll's bonus belongs to the part.</param>
    /// <param name="chance">How likely the part is to count again, in the kit's own unit of chance.</param>
    /// <param name="factor">How many times the part counts when the draw lands; one changes nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The first die or the dice count is negative, or the factor is below one — a part whose effect a reader
    /// could not state.
    /// </exception>
    public DamageMultiplier(int firstDie, int dice, int bonus, HitChance chance, int factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstDie);
        ArgumentOutOfRangeException.ThrowIfNegative(dice);
        ArgumentOutOfRangeException.ThrowIfLessThan(factor, 1);
        FirstDie = firstDie;
        Dice = dice;
        Bonus = bonus;
        Chance = chance;
        Factor = factor;
    }

    /// <summary>The first of the roll's dice the part covers.</summary>
    public int FirstDie { get; }

    /// <summary>How many of the roll's dice the part covers.</summary>
    public int Dice { get; }

    /// <summary>How much of the roll's bonus belongs to the part.</summary>
    public int Bonus { get; }

    /// <summary>How likely the part is to count again.</summary>
    public HitChance Chance { get; }

    /// <summary>How many times the part counts when the draw lands.</summary>
    public int Factor { get; }

    /// <inheritdoc />
    public override string ToString() => $"x{Factor} at {Chance} on dice {FirstDie}+{Dice}{Bonus:+#;-#;+0}";
}
