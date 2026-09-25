using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Services;

/// <summary>
/// The sort of thing a counter offers besides goods and lessons, as the mechanism's own vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// <b>A kind here is a capability, not a building.</b> A temple, a tavern, a bank, a training hall, and a
/// stable are all served by the one mechanism; what each of them needs is one of these — a cure, a stay, a
/// holding, a level, a passage, a notice — and the mechanism applies it without knowing which building
/// asked. That is why this list is short and why adding a building that needs none of them adds no code:
/// content names the kind, the ruleset answers what it offers, and the workflow below moves the party's own
/// state.
/// </para>
/// <para>
/// A provision is the one kind that is not a capability of its own: it fills the larder the party already
/// has, exactly as a purchased item fills the pack, and it is here rather than in the stock because the
/// party's food is a party-wide account rather than an instance in the pack.
/// </para>
/// </remarks>
public enum ServiceOfferKind
{
    /// <summary>Removes one or more conditions acting on a member, which is what a temple's healing is.</summary>
    Cure,

    /// <summary>Converts a member's banked experience into a level, up to the ceiling the offer states.</summary>
    Training,

    /// <summary>Fills the party's larder with provisions, which is what a tavern's food and drink is.</summary>
    Provision,

    /// <summary>Gives the party a night somewhere safe: game time passes and the party rests.</summary>
    Stay,

    /// <summary>Names the state a counter keeps the party's coins in, which is what a bank holds.</summary>
    Holding,

    /// <summary>A passage to a place, which is what a stable or a dock sells.</summary>
    Fare,

    /// <summary>A line the counter posts or tells, which is a rumour or a bounty notice. It costs nothing.</summary>
    Notice,
}

/// <summary>One thing a counter offers, as content and policy answer for it.</summary>
/// <remarks>
/// <para>
/// This is the same shape a lesson has — a kind, a subject, a name, a base value, and how much of it —
/// because it answers the same question about a different sort of thing: what this counter offers that is
/// not a line of goods. What each field means is the kind's: a cure's <see cref="Clears"/> are the
/// conditions it removes, a training's <see cref="Limit"/> is the ceiling it will take a member to, a
/// provision's <see cref="Amount"/> is how many portions it fills, a stay's is how many hours the room
/// lasts, a fare's <see cref="Subject"/> names the place it reaches, and a holding's names the state the
/// coins are kept in.
/// </para>
/// <para>
/// <b>The numbers are content's and policy's; the arithmetic is the kit's.</b> <see cref="Value"/> is the
/// base the price rule works from, exactly as a stock line's and a lesson's are, and the price the party
/// actually pays is quoted by the ruleset through <see cref="IServiceRule.Quote"/>.
/// </para>
/// </remarks>
/// <param name="Kind">Which sort of thing this offer is.</param>
/// <param name="Subject">
/// What the offer acts on: the condition a cure removes when it removes only one, the place a fare reaches,
/// the state a holding is kept in, or empty when the offer names nothing a command could name.
/// </param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Value">What it is worth as a base price, which cannot be negative.</param>
/// <param name="Amount">
/// How much of the subject there is: the portions a provision fills, the hours a stay lasts, the coins a
/// holding was opened with, or one when the kind states no quantity.
/// </param>
/// <param name="Limit">
/// The ceiling the offer will take the party to — a training hall's cap, or zero when the offer states
/// none.
/// </param>
/// <param name="Clears">The conditions a cure or a stay ends, in the order they are cleared.</param>
/// <exception cref="ArgumentException">The name or a subject is blank, which offers nothing.</exception>
/// <exception cref="ArgumentOutOfRangeException">The value or the amount is negative.</exception>
public sealed record ServiceOffer(
    ServiceOfferKind Kind,
    string Name,
    string Subject = "",
    int Value = 0,
    int Amount = 1,
    int Limit = 0,
    IReadOnlyList<ConditionId>? Clears = null)
{
    /// <summary>The conditions this offer ends, which is empty when it ends none.</summary>
    public IReadOnlyList<ConditionId> Conditions => Clears ?? [];

    /// <summary>Whether this offer ends a particular condition.</summary>
    /// <param name="condition">The condition to look for.</param>
    public bool ClearsCondition(ConditionId condition) => Conditions.Contains(condition);

    /// <summary>What a command names this offer by, which is its own name when it names nothing else.</summary>
    public string Target => Subject.Length > 0 ? Subject : Name;

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        ServiceOfferKind.Training => $"{Name} (to level {Limit})",
        ServiceOfferKind.Provision => $"{Name} ({Amount})",
        ServiceOfferKind.Stay => $"{Name} ({Amount}h)",
        _ => Subject.Length > 0 ? $"{Name} ({Subject})" : Name,
    };
}

/// <summary>Which counter's offers are being read, and by whom.</summary>
/// <remarks>
/// The party and the clock travel whole for the same reason they do in the stock and lesson requests: what a
/// counter offers can depend on the party's own state — the membership a guild's shelves require, the coins
/// a bank already holds, the hour a room may be taken — and those are this game's answers about its own
/// content, not questions the kit can answer.
/// </remarks>
/// <param name="Service">The service whose offers are being read.</param>
/// <param name="Party">The party that would take them.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record ServiceOfferRequest(ServiceDefinition Service, PartyEntity Party, GameClock? Clock);

/// <summary>One offer as it is browsed: the offer itself and what taking it would cost.</summary>
/// <remarks>
/// The price travels with the offer for the same reason a stock line's does: a browse is what a panel shows
/// before anything is decided, and the number must be the price the operation would actually charge.
/// </remarks>
/// <param name="Offer">The offer.</param>
/// <param name="Price">What taking it costs the party.</param>
public readonly record struct ServiceOfferLine(ServiceOffer Offer, int Price);
