using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Services;

/// <summary>One line of stock as it is browsed: what it is, how many are left, and what it would cost.</summary>
/// <remarks>
/// The price travels with the line because a browse is what a panel shows before anything is decided, and
/// the number must be the price the operation would actually charge rather than one the screen works out:
/// the ruleset prices it, the mechanism carries it, and the panel spells nothing.
/// </remarks>
/// <param name="Lot">The lot's identity, which a buy command names.</param>
/// <param name="Definition">The item definition the lot's goods are copies of.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Count">How many are left.</param>
/// <param name="Price">What one of them costs the party.</param>
/// <param name="Value">What one of them is worth as a base price, before the policy's adjustments.</param>
/// <param name="IsSale">Whether the shop is reselling something it bought from the party rather than its own stock.</param>
public readonly record struct ServiceStockOffer(
    ServiceLotId Lot,
    ItemDefinitionId Definition,
    string Name,
    int Count,
    int Price,
    int Value,
    bool IsSale);

/// <summary>One lesson as it is browsed: what it teaches and what it would cost.</summary>
/// <param name="Kind">Whether the lesson grants a skill or a party-wide effect.</param>
/// <param name="Subject">The skill's id, or the effect's id.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Amount">The skill level the lesson reaches, or the effect's magnitude.</param>
/// <param name="Price">What the lesson costs the party.</param>
/// <param name="Tier">
/// The rung of a skill's ladder the lesson leaves a member at, where one is the first rung. A counter can
/// teach the same skill at more than one rung — a guild sells its school's first lesson and its own deeper
/// ones — so a lesson is named by its subject and its rung together, and a command that names only the
/// subject means the first rung.
/// </param>
public readonly record struct ServiceLessonOffer(
    ServiceLessonKind Kind,
    string Subject,
    string Name,
    int Amount,
    int Price,
    int Tier = 1);

/// <summary>One of the party's own items as a service sees it when it would buy it.</summary>
/// <remarks>
/// The state travels with the offer because what a shop pays depends on it, and because a player deciding
/// what to sell needs to see that an item is damaged or unidentified before it goes. The damage and the
/// identification are the instance's own state read back, never a second copy kept here.
/// </remarks>
/// <param name="Item">The instance's durable identity, which a sell command names.</param>
/// <param name="Definition">The definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Price">What the service would pay the party for it.</param>
/// <param name="Damage">How damaged the instance is; zero is sound.</param>
/// <param name="Identified">Whether the party knows what it is.</param>
public readonly record struct ServiceSaleOffer(
    ItemInstanceId Item,
    ItemDefinitionId Definition,
    string Name,
    int Price,
    int Damage,
    bool Identified);

/// <summary>One member a lesson could be taught to, as the panel offers them.</summary>
/// <param name="Index">The member's place in the party, counted from zero, which a teach command names.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
public readonly record struct ServiceMemberOffer(int Index, PartyMemberId Member, string Name);

/// <summary>What one open service offers the party, as one browsable value.</summary>
/// <remarks>
/// <para>
/// This is the whole of what a service screen shows before anything is decided: which operations the
/// counter offers, what the party already carries of what it requires, what is on the shelves and what it
/// costs, what the counter teaches, what of the party's own it would buy, and which members a lesson could
/// go to. Every price here was quoted by the ruleset's price rule and every list came from the stock rule,
/// so a screen that renders this decides nothing.
/// </para>
/// <para>
/// It is recomputed whenever it is read rather than cached, because the shelf, the party's purse, and the
/// clock all move underneath it: a browse that remembered a price would show one the counter no longer
/// charges.
/// </para>
/// </remarks>
/// <param name="Service">The service being browsed.</param>
/// <param name="Operations">The operations it offers, as words.</param>
/// <param name="Memberships">What the party carries of what the service requires, as words.</param>
/// <param name="Stock">What is on the shelves, in the order a person reads them.</param>
/// <param name="Lessons">What the counter teaches.</param>
/// <param name="Offers">
/// What else the counter offers: its cures, its passages, its provisions, its rooms, what it holds, its
/// training ceiling, and the lines it posts.
/// </param>
/// <param name="Sales">What of the party's own the counter would buy.</param>
/// <param name="Members">The members a lesson, a cure, or a training step could go to.</param>
public sealed record ServiceBrowse(
    ServiceDefinition Service,
    IReadOnlyList<string> Operations,
    IReadOnlyList<string> Memberships,
    IReadOnlyList<ServiceStockOffer> Stock,
    IReadOnlyList<ServiceLessonOffer> Lessons,
    IReadOnlyList<ServiceOfferLine> Offers,
    IReadOnlyList<ServiceSaleOffer> Sales,
    IReadOnlyList<ServiceMemberOffer> Members);
