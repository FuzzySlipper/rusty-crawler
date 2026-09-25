using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Services;

/// <summary>What a service's policy answers about one operation: what the party pays, and what it is paid.</summary>
/// <remarks>
/// <para>
/// A price and a payment are one value because one operation can be either: a purchase asks the party for
/// coins, a sale gives them, and an identification, a repair, or a lesson asks for a fee. Both parts are
/// ordinary party costs, so a service that asked for provisions as well as coin would settle through the
/// same path without the mechanism changing.
/// </para>
/// <para>
/// The value is the base the policy priced from, carried so the mechanism can record it with what it took
/// in: a shop that buys an item holds it at the value it paid, and the shop's own price for selling it back
/// is worked from that same number. It is content's or the ruleset's figure, never the mechanism's.
/// </para>
/// </remarks>
/// <param name="Charge">What the operation asks the party for, free when it asks nothing.</param>
/// <param name="Payment">What the operation gives the party, free when it gives nothing.</param>
/// <param name="Value">The base value the policy priced from, or zero when it priced from no stated value.</param>
public readonly record struct ServiceQuote(PartyCost Charge, PartyCost Payment, int Value = 0)
{
    /// <summary>A quote for nothing: the operation costs the party nothing and pays it nothing.</summary>
    public static ServiceQuote Free => new(PartyCost.Free, PartyCost.Free);

    /// <summary>A quote that asks the party for coins.</summary>
    /// <param name="coins">How many coins, which cannot be negative.</param>
    /// <param name="value">The base value the price was worked from.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public static ServiceQuote Charging(int coins, int value = 0) =>
        new(PartyCost.OfGold(coins), PartyCost.Free, value);

    /// <summary>A quote that pays the party coins.</summary>
    /// <param name="coins">How many coins, which cannot be negative.</param>
    /// <param name="value">The base value the payment was worked from.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public static ServiceQuote Paying(int coins, int value = 0) =>
        new(PartyCost.Free, PartyCost.OfGold(coins), value);
}

/// <summary>Whether a service's policy lets the party do one operation, and why not when it does not.</summary>
/// <remarks>
/// The eligibility half of a service's policy: whether the counter is open, whether the party carries the
/// membership it requires, whether a member may learn the lesson, whether an item is in a state the
/// operation makes sense in. The refusal is the party's ordinary refusal value, so a service that will not
/// serve the party says why in the same shape as a lock that will not open and a purse that cannot pay.
/// </remarks>
public readonly record struct ServiceEligibility
{
    private ServiceEligibility(PartyRefusal? refusal) => Refusal = refusal;

    /// <summary>The party may do this.</summary>
    public static ServiceEligibility Allowed => default;

    /// <summary>The party may not, and this is why.</summary>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why the party is not served, in terms a person can act on.</param>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    public static ServiceEligibility Refused(string code, string message) => new(new PartyRefusal(code, message));

    /// <summary>Whether the party may do it.</summary>
    public bool IsAllowed => Refusal is null;

    /// <summary>Why the party may not do it, or null when it may.</summary>
    public PartyRefusal? Refusal { get; }
}
