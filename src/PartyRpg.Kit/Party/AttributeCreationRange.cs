namespace PartyRpg.Kit.Party;

/// <summary>
/// How one attribute may be bought at creation: where it starts, how far it may move, and what a move costs.
/// </summary>
/// <remarks>
/// <para>
/// A pool of points is spent across a character's attributes, and a point of attribute is not always worth
/// one pool point: a game may charge two pool points for one point of a race's weaker attribute and give two
/// points of its strong one for a single pool point. Both numbers are therefore data.
/// </para>
/// <para>
/// The rule that keeps the pool honest is that an adjustment <em>below</em> the starting value moves and
/// refunds exactly what an adjustment above it costs, with the distance and the price exchanged. Without
/// that exchange a player could lower an expensive attribute, bank the refund and raise it again more
/// cheaply, which turns the pool into a faucet: the raise and lowering sizes are two readings of the same
/// pair of numbers rather than four independent settings.
/// </para>
/// <para>
/// The floor is data rather than "the starting value minus two" because how far below its own average a race
/// may be pushed is that game's statement, not this layer's.
/// </para>
/// </remarks>
public sealed record AttributeCreationRange
{
    /// <summary>States how one attribute is bought for one race.</summary>
    /// <param name="attribute">The attribute this range is about.</param>
    /// <param name="name">What the attribute is called, for the messages a refused choice carries.</param>
    /// <param name="start">The value the race starts the attribute at.</param>
    /// <param name="minimum">The lowest the attribute may be lowered to at creation.</param>
    /// <param name="maximum">The highest the attribute may be raised to at creation.</param>
    /// <param name="stepSize">How many attribute points one adjustment moves above the starting value.</param>
    /// <param name="stepCost">How many pool points one adjustment costs above the starting value.</param>
    /// <exception cref="ArgumentException">The name is blank, so a refusal could not say what it is about.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The floor lies above the start, the ceiling below it, or a step is below one — a range no value could
    /// be bought inside.
    /// </exception>
    public AttributeCreationRange(
        AttributeId attribute,
        string name,
        int start,
        int minimum,
        int maximum,
        int stepSize,
        int stepCost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (minimum > start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                minimum,
                $"'{name}' cannot be lowered below {minimum} while it starts at {start}; a floor above the start would refund points for a value creation never granted.");
        }

        if (maximum < start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                maximum,
                $"'{name}' cannot be raised to {maximum} while it starts at {start}; a ceiling below the start would leave nothing to buy.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(stepSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(stepCost, 1);
        Attribute = attribute;
        Name = name;
        Start = start;
        Minimum = minimum;
        Maximum = maximum;
        StepSize = stepSize;
        StepCost = stepCost;
    }

    /// <summary>The attribute this range is about.</summary>
    public AttributeId Attribute { get; }

    /// <summary>What the attribute is called.</summary>
    public string Name { get; }

    /// <summary>The value the race starts the attribute at.</summary>
    public int Start { get; }

    /// <summary>The lowest the attribute may be lowered to at creation.</summary>
    public int Minimum { get; }

    /// <summary>The highest the attribute may be raised to at creation.</summary>
    public int Maximum { get; }

    /// <summary>How many attribute points one adjustment moves above the starting value.</summary>
    public int StepSize { get; }

    /// <summary>How many pool points one adjustment costs above the starting value.</summary>
    public int StepCost { get; }

    /// <summary>How far one raise moves the score from the value it currently stands at.</summary>
    /// <remarks>
    /// At or above the starting value a raise moves <see cref="StepSize"/>; below it, it moves
    /// <see cref="StepCost"/>, so a race that was lowered by one price can be raised by the other and land
    /// exactly back on its starting value rather than stepping past it.
    /// </remarks>
    /// <param name="value">The score before the raise.</param>
    public int RaiseSize(int value) => value < Start ? StepCost : StepSize;

    /// <summary>How many pool points one raise costs from the value the score currently stands at.</summary>
    /// <param name="value">The score before the raise.</param>
    public int RaiseCost(int value) => value < Start ? StepSize : StepCost;

    /// <summary>How far one lowering moves the score from the value it currently stands at.</summary>
    /// <remarks>
    /// The starting value itself counts as the lower side: dropping from it moves <see cref="StepCost"/>,
    /// which is how far below the start a refund reaches — and the refund is <see cref="StepSize"/>, exactly
    /// what raising that same distance costs.
    /// </remarks>
    /// <param name="value">The score before the lowering.</param>
    public int LowerSize(int value) => value <= Start ? StepCost : StepSize;

    /// <summary>How many pool points one lowering refunds from the value the score currently stands at.</summary>
    /// <param name="value">The score before the lowering.</param>
    public int LowerRefund(int value) => value <= Start ? StepSize : StepCost;

    /// <summary>Whether the score can be raised one raise without passing its ceiling.</summary>
    /// <param name="value">The score now.</param>
    public bool CanRaise(int value) => value + RaiseSize(value) <= Maximum;

    /// <summary>Whether the score can be lowered one lowering without passing its floor.</summary>
    /// <param name="value">The score now.</param>
    public bool CanLower(int value) => value - LowerSize(value) >= Minimum;

    /// <summary>The score one raise higher.</summary>
    /// <param name="value">The score now, which <see cref="CanRaise"/> must admit.</param>
    public int Raised(int value) => value + RaiseSize(value);

    /// <summary>The score one lowering lower.</summary>
    /// <param name="value">The score now, which <see cref="CanLower"/> must admit.</param>
    public int Lowered(int value) => value - LowerSize(value);

    /// <summary>
    /// What a score accounts for in the pool: positive when it was bought above the starting value, negative
    /// when it was lowered below it and refunded points.
    /// </summary>
    /// <param name="value">The score, which must lie on the range's own steps.</param>
    /// <exception cref="ArgumentException">The score is not a value the range's steps reach.</exception>
    public int PoolPoints(int value)
    {
        if (value >= Start) return ((value - Start) / StepSize) * StepCost;
        return -(((Start - value) / StepCost) * StepSize);
    }

    /// <summary>Whether a score lies inside the range on whole adjustments from the starting value.</summary>
    /// <param name="value">The score to judge.</param>
    public bool Reaches(int value)
    {
        if (value < Minimum || value > Maximum) return false;
        int distance = Math.Abs(value - Start);
        return distance % (value >= Start ? StepSize : StepCost) == 0;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Name} {Minimum}..{Maximum} (start {Start}, {StepSize} per {StepCost})";
}
