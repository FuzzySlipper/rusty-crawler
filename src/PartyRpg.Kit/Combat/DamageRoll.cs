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
/// <para>
/// <b>A part may count again.</b> <see cref="WithMultiplier"/> adds a <see cref="DamageMultiplier"/>: a run of
/// the roll's dice and a share of its bonus that counts several times when a draw of its own lands. Each
/// multiplier draws once per roll under its own purpose, so two of them are two independent chances, and a
/// roll that states none draws exactly what it always drew.
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

    private readonly DamageMultiplier[]? _multipliers;

    /// <summary>The parts of this roll that may count again, in the order they draw.</summary>
    public IReadOnlyList<DamageMultiplier> Multipliers => _multipliers ?? [];

    /// <summary>The least this roll can produce.</summary>
    /// <remarks>A part that is certain to count again counts at its least; one that may not is left out when it would only lower the roll.</remarks>
    public int Minimum
    {
        get
        {
            int least = Dice + Bonus;
            foreach (DamageMultiplier multiplier in Multipliers)
            {
                int extra = (multiplier.Factor - 1) * (multiplier.Dice + multiplier.Bonus);
                least += Extreme(multiplier, extra, Math.Min);
            }

            return Math.Max(Floor, least);
        }
    }

    /// <summary>The most this roll can produce.</summary>
    public int Maximum
    {
        get
        {
            int most = (Dice * Sides) + Bonus;
            foreach (DamageMultiplier multiplier in Multipliers)
            {
                int extra = (multiplier.Factor - 1) * ((multiplier.Dice * Sides) + multiplier.Bonus);
                most += Extreme(multiplier, extra, Math.Max);
            }

            return Math.Max(Minimum, most);
        }
    }

    /// <summary>States this roll with one more part that may count again.</summary>
    /// <param name="multiplier">The part, whose dice must be dice this roll has.</param>
    /// <returns>The roll with the part added after any it already states.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The part names dice this roll does not roll.</exception>
    public DamageRoll WithMultiplier(DamageMultiplier multiplier)
    {
        if (multiplier.FirstDie + multiplier.Dice > Dice)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                multiplier,
                $"The part covers dice {multiplier.FirstDie} to {multiplier.FirstDie + multiplier.Dice - 1} of a roll of {Dice}, so it would count dice nobody rolled.");
        }

        DamageMultiplier[] parts = [.. Multipliers, multiplier];
        return new DamageRoll(this, parts);
    }

    private DamageRoll(DamageRoll roll, DamageMultiplier[] multipliers)
    {
        Dice = roll.Dice;
        Sides = roll.Sides;
        Bonus = roll.Bonus;
        Floor = roll.Floor;
        _multipliers = multipliers;
    }

    /// <summary>What a part may add at an extreme: all of it when certain, none when impossible, either otherwise.</summary>
    private static int Extreme(DamageMultiplier multiplier, int extra, Func<int, int, int> pick) =>
        multiplier.Chance.BasisPoints >= HitChance.Certain ? extra
        : multiplier.Chance.BasisPoints <= 0 ? 0
        : pick(0, extra);

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
        int[]? faces = _multipliers is null ? null : new int[Dice];
        for (int die = 0; die < Dice; die++)
        {
            int face = rolls.Roll($"{purpose}/{die}", 1, Sides);
            if (faces is not null) faces[die] = face;
            total += face;
        }

        if (faces is not null)
        {
            for (int index = 0; index < _multipliers!.Length; index++)
            {
                DamageMultiplier multiplier = _multipliers[index];
                int draw = rolls.Roll($"{purpose}/multiplier/{index}", 0, HitChance.Certain - 1);
                if (!multiplier.Chance.Hits(draw)) continue;
                int part = multiplier.Bonus;
                for (int die = multiplier.FirstDie; die < multiplier.FirstDie + multiplier.Dice; die++) part += faces[die];
                total += (multiplier.Factor - 1) * part;
            }
        }

        return Math.Max(Floor, total);
    }

    /// <summary>Whether another roll states the same dice, bonus, floor and parts.</summary>
    /// <param name="other">The other roll.</param>
    /// <returns>Whether the two are the same statement.</returns>
    public bool Equals(DamageRoll other) =>
        Dice == other.Dice && Sides == other.Sides && Bonus == other.Bonus && Floor == other.Floor &&
        Multipliers.SequenceEqual(other.Multipliers);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Dice);
        hash.Add(Sides);
        hash.Add(Bonus);
        hash.Add(Floor);
        foreach (DamageMultiplier multiplier in Multipliers) hash.Add(multiplier);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        string roll = Dice == 0
            ? Bonus.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Floor > 0
                ? $"{Dice}d{Sides}{Bonus:+#;-#;+0} (at least {Floor})"
                : $"{Dice}d{Sides}{Bonus:+#;-#;+0}";
        foreach (DamageMultiplier multiplier in Multipliers) roll += $", {multiplier}";
        return roll;
    }
}
