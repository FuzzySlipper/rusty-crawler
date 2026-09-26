namespace PartyRpg.Kit.Combat;

/// <summary>How much harm one hit rolls, as a handful of dice and a bonus.</summary>
/// <remarks>
/// <para>
/// This is the kit's whole damage vocabulary: <c>count</c> dice of <c>sides</c> each, plus a flat bonus.
/// What the dice are — a fist, a sword, a monster's bite, a spell's blast — and what the bonus is worth are
/// the ruleset's, so a game states its damage as the numbers its own tables carry and the kit rolls them.
/// </para>
/// <para>
/// <b>The roll is the mechanism's and the bounds are the ruleset's.</b> <see cref="Minimum"/> and
/// <see cref="Maximum"/> are the honest bounds of the expression, which is what a test and a panel can hold
/// a roll against; <see cref="Roll"/> draws one uniform value per die from the rolls one attack was given,
/// so the same seed and the same attack produce the same damage.
/// </para>
/// </remarks>
public readonly record struct DamageRoll
{
    /// <summary>States a roll of dice.</summary>
    /// <param name="dice">How many dice are rolled; zero states a flat amount.</param>
    /// <param name="sides">How many faces each die has; one is a certain point of damage.</param>
    /// <param name="bonus">
    /// What is added to the dice. It may be negative — a weak character's own bonus is — and the floor is
    /// what keeps such a roll from landing for nothing.
    /// </param>
    /// <param name="floor">The least the roll may produce, whatever the dice and the bonus come to.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The dice count or the floor is negative, or dice are rolled on a die with no faces — a roll whose
    /// bounds a reader could not state.
    /// </exception>
    public DamageRoll(int dice, int sides, int bonus = 0, int floor = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dice);
        ArgumentOutOfRangeException.ThrowIfNegative(floor);
        if (dice > 0) ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);
        Dice = dice;
        Sides = dice > 0 ? sides : 0;
        Bonus = bonus;
        Floor = floor;
    }

    /// <summary>How many dice are rolled.</summary>
    public int Dice { get; }

    /// <summary>How many faces each die has, zero when nothing is rolled.</summary>
    public int Sides { get; }

    /// <summary>What is added to the dice.</summary>
    public int Bonus { get; }

    /// <summary>The least the roll may produce, whatever the dice and the bonus come to.</summary>
    /// <remarks>
    /// A game may state that a blow which lands does at least some harm however weak the arm behind it is;
    /// this is that statement, and it is a floor rather than a clamp so that the bounds a reader is given
    /// are the bounds the roll actually produces.
    /// </remarks>
    public int Floor { get; }

    /// <summary>The least this roll can produce.</summary>
    public int Minimum => Math.Max(Floor, Dice + Bonus);

    /// <summary>The most this roll can produce.</summary>
    public int Maximum => Math.Max(Minimum, (Dice * Sides) + Bonus);

    /// <summary>A roll that is certain: no dice, and this much damage.</summary>
    /// <param name="amount">The damage, which cannot be negative.</param>
    /// <returns>The roll.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public static DamageRoll Flat(int amount) => new(dice: 0, sides: 0, bonus: amount, floor: amount);

    /// <summary>Rolls the dice, one draw per die, under the rolls one attack was given.</summary>
    /// <param name="rolls">Where this attack's draws come from.</param>
    /// <param name="purpose">What the roll is for, which is what keeps two draws of one attack apart.</param>
    /// <returns>The damage rolled, between <see cref="Minimum"/> and <see cref="Maximum"/>.</returns>
    /// <exception cref="ArgumentNullException">No rolls were supplied.</exception>
    /// <exception cref="ArgumentException">No purpose was stated, so two draws of one attack could not be told apart.</exception>
    public int Roll(IAttackRolls rolls, string purpose)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        int total = Bonus;
        for (int die = 0; die < Dice; die++)
        {
            total += rolls.Roll($"{purpose}/{die}", 1, Sides);
        }

        return Math.Max(Floor, total);
    }

    /// <inheritdoc />
    public override string ToString() => Dice == 0
        ? Bonus.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Floor > 0
            ? $"{Dice}d{Sides}{Bonus:+#;-#;+0} (at least {Floor})"
            : $"{Dice}d{Sides}{Bonus:+#;-#;+0}";
}
