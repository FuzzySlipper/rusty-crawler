namespace PartyRpg.Kit.Party;

/// <summary>What one settlement did: what the party paid and where its accounts stand, or why nothing was paid.</summary>
/// <remarks>
/// The outcome counterpart of <see cref="SettlementQuote"/>, kept apart for the same reason the world keeps
/// a cost quote apart from a transition result: one is a policy's answer about a charge, the other is what
/// actually left the party's accounts. The balances are carried because a service screen shows them, and
/// because a settlement that reported a price without reporting the accounts it came from would invite a
/// caller to recompute what the mechanism already knows.
/// </remarks>
public sealed record ResourceSettlement
{
    private readonly PartyCost _cost;
    private readonly int _purseAfter;
    private readonly int _provisionsAfter;

    private ResourceSettlement(PartyCost cost, int purseAfter, int provisionsAfter, PartyRefusal? refusal)
    {
        _cost = cost;
        _purseAfter = purseAfter;
        _provisionsAfter = provisionsAfter;
        Refusal = refusal;
    }

    /// <summary>The party paid the charge.</summary>
    /// <param name="cost">What the party paid, which is what the rule priced rather than what was quoted.</param>
    /// <param name="purseAfter">What the purse holds once the charge is paid.</param>
    /// <param name="provisionsAfter">What the larder holds once the charge is paid.</param>
    public static ResourceSettlement Paid(PartyCost cost, int purseAfter, int provisionsAfter) =>
        new(cost, purseAfter, provisionsAfter, null);

    /// <summary>Nothing was paid, and both accounts are untouched.</summary>
    /// <param name="refusal">Why the charge could not be settled.</param>
    /// <exception cref="ArgumentNullException">The refusal is null.</exception>
    public static ResourceSettlement Refused(PartyRefusal refusal) =>
        new(PartyCost.Free, 0, 0, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>The refusal, or null when the charge was paid.</summary>
    public PartyRefusal? Refusal { get; }

    /// <summary>Whether the charge was paid.</summary>
    public bool Admitted => Refusal is null;

    /// <summary>What the party paid, which exists only on a settlement that was paid.</summary>
    /// <exception cref="InvalidOperationException">This settlement is a refusal; read <see cref="Refusal"/> instead.</exception>
    public PartyCost Cost
    {
        get
        {
            RequirePaid();
            return _cost;
        }
    }

    /// <summary>What the purse holds afterwards, which exists only on a settlement that was paid.</summary>
    /// <exception cref="InvalidOperationException">This settlement is a refusal; read <see cref="Refusal"/> instead.</exception>
    public int PurseAfter
    {
        get
        {
            RequirePaid();
            return _purseAfter;
        }
    }

    /// <summary>What the larder holds afterwards, which exists only on a settlement that was paid.</summary>
    /// <exception cref="InvalidOperationException">This settlement is a refusal; read <see cref="Refusal"/> instead.</exception>
    public int ProvisionsAfter
    {
        get
        {
            RequirePaid();
            return _provisionsAfter;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Refusal is null
        ? $"paid {_cost.Coins} coin(s) and {_cost.Food.Amount} portion(s), leaving {_purseAfter} and {_provisionsAfter}"
        : $"refused: {Refusal}";

    private void RequirePaid()
    {
        if (Refusal is not null)
        {
            throw new InvalidOperationException("A refused settlement paid nothing; read Refusal instead.");
        }
    }
}
