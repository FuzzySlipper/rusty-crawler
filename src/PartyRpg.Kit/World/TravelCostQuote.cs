namespace PartyRpg.Kit.World;

/// <summary>The cost rule's one answer about a transition: what it charges, or why the party cannot pay it.</summary>
/// <remarks>
/// The answer is a single value rather than a price plus a separate affordability question, so a caller
/// cannot obtain a price and forget to ask whether it can be paid. Affordability is the rule's to judge
/// because the purse and the food the party would pay from belong to the party owner: the rule reads
/// them, and the kit never has to hold a store it does not own.
/// </remarks>
public sealed record TravelCostQuote
{
    private readonly TravelCost _cost;

    private TravelCostQuote(TravelCost cost, TravelRefusal? refusal)
    {
        _cost = cost;
        Refusal = refusal;
    }

    /// <summary>The party can pay: this is what the transition charges.</summary>
    /// <param name="cost">What the transition costs.</param>
    public static TravelCostQuote Payable(TravelCost cost) => new(cost, null);

    /// <summary>The party cannot pay, or may not travel: the transition does not happen.</summary>
    /// <param name="refusal">Why the transition is refused.</param>
    /// <exception cref="ArgumentNullException">The refusal is null.</exception>
    public static TravelCostQuote Refused(TravelRefusal refusal) =>
        new(TravelCost.Free, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>The refusal, or null when the party can pay.</summary>
    public TravelRefusal? Refusal { get; }

    /// <summary>The cost to charge, which exists only on an answer that is not refused.</summary>
    /// <exception cref="InvalidOperationException">This answer is a refusal, which holds no cost; read <see cref="Refusal"/> instead.</exception>
    public TravelCost Cost => Refusal is null
        ? _cost
        : throw new InvalidOperationException("A refused travel quote holds no cost; read Refusal instead.");

    /// <inheritdoc />
    public override string ToString() => Refusal is null ? $"payable for {_cost}" : $"refused: {Refusal}";
}
