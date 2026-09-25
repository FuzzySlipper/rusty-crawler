namespace PartyRpg.Kit.Party;

/// <summary>The settlement rule's one answer about a charge: what the party pays, or why it is not served.</summary>
/// <remarks>
/// One value rather than a price beside a separate permission, so a caller cannot obtain a price and forget
/// to ask whether the party is served at all — the same shape the world's travel cost uses. What a charge
/// becomes is entirely the rule's answer over its own tables: a discount a reputation has earned, a
/// surcharge fame attracts, or a flat refusal to deal with the party. The kit holds none of those
/// thresholds and never guesses one.
/// </remarks>
public sealed record SettlementQuote
{
    private readonly PartyCost _price;

    private SettlementQuote(PartyCost price, PartyRefusal? refusal)
    {
        _price = price;
        Refusal = refusal;
    }

    /// <summary>The party is served: this is what the quoted charge becomes.</summary>
    /// <param name="price">What the party actually pays, which may differ from what was quoted.</param>
    public static SettlementQuote Payable(PartyCost price) => new(price, null);

    /// <summary>The party is not served, so nothing is charged.</summary>
    /// <param name="refusal">Why the charge is refused.</param>
    /// <exception cref="ArgumentNullException">The refusal is null.</exception>
    public static SettlementQuote Refused(PartyRefusal refusal) =>
        new(PartyCost.Free, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>The refusal, or null when the party is served.</summary>
    public PartyRefusal? Refusal { get; }

    /// <summary>The price to charge, which exists only on an answer that is not refused.</summary>
    /// <exception cref="InvalidOperationException">This answer is a refusal, which holds no price; read <see cref="Refusal"/> instead.</exception>
    public PartyCost Cost => Refusal is null
        ? _price
        : throw new InvalidOperationException("A refused settlement quote holds no price; read Refusal instead.");

    /// <inheritdoc />
    public override string ToString() => Refusal is null
        ? $"payable at {_price.Coins} coin(s) and {_price.Food.Amount} portion(s)"
        : $"refused: {Refusal}";
}
