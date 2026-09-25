using PartyRpg.Kit.Services;

namespace PartyRpg.Kit.Presentation;

/// <summary>One line of a service's shelves, as the panel shows it: what it is, how many, and what it costs.</summary>
/// <param name="Lot">The lot's identity, which a buy command names.</param>
/// <param name="Item">The item definition the line holds.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Count">How many are left on the shelves.</param>
/// <param name="Price">What one costs the party.</param>
/// <param name="IsSale">Whether the counter is reselling something it bought from the party.</param>
public readonly record struct ServiceStockSnapshot(
    string Lot,
    string Item,
    string Name,
    int Count,
    int Price,
    bool IsSale);

/// <summary>One lesson a service teaches, as the panel shows it.</summary>
/// <param name="Kind">Whether the lesson grants a skill or a party-wide effect, as the wire spells it.</param>
/// <param name="Subject">The skill's id, or the effect's id, which a teach command names.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Amount">The skill level the lesson reaches, or the effect's magnitude.</param>
/// <param name="Price">What the lesson costs the party.</param>
public readonly record struct ServiceLessonSnapshot(
    string Kind,
    string Subject,
    string Name,
    int Amount,
    int Price);

/// <summary>One of the party's own items, as a service that would buy it shows it.</summary>
/// <param name="Item">The instance's durable identity, which a sell command names.</param>
/// <param name="Definition">The definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Price">What the counter would pay the party for it.</param>
/// <param name="Damage">How damaged the instance is; zero is sound.</param>
/// <param name="Identified">Whether the party knows what it is.</param>
public readonly record struct ServiceSaleSnapshot(
    string Item,
    string Definition,
    string Name,
    int Price,
    int Damage,
    bool Identified);

/// <summary>One member a lesson could be taught to.</summary>
/// <param name="Index">The member's place in the party, counted from zero, which a teach command names.</param>
/// <param name="Name">What the member is called.</param>
public readonly record struct ServiceMemberSnapshot(int Index, string Name);

/// <summary>What the party is doing at a service, as the panel needs it.</summary>
/// <remarks>
/// <para>
/// These are the mechanism's own facts copied into one presentation value, never a second opinion about
/// them: which counter the party stands at, whether it is serving, what the party carries of what it
/// requires, what is on the shelves and at what price, what the counter teaches, what of the party's own it
/// would buy, and what the last command did with the coins that moved. Every price and every word here was
/// the ruleset's or the mechanism's; the panel spells none of them.
/// </para>
/// <para>
/// A session whose ruleset answered no service policy has no facts at all, and <see cref="None"/> is that
/// state, so the panel says the mechanism is not there instead of showing an empty counter that looks like
/// a shop with nothing in it. A session that holds the mechanism but stands at no counter publishes
/// <c>available</c> with <c>open</c> false, which is a different fact again.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a service mechanism at all.</param>
/// <param name="Open">Whether a visit is open.</param>
/// <param name="Id">The counter's identity in content, empty when the party stands at none.</param>
/// <param name="Kind">What sort of service it is, as content names it.</param>
/// <param name="Name">What a person reads on the sign.</param>
/// <param name="Proprietor">Whoever keeps it, or empty when content names nobody.</param>
/// <param name="State">Whether the counter is serving: <c>open</c>, <c>closed</c>, or empty when the party stands at none.</param>
/// <param name="Hours">The hours content states for it, or empty when it serves always.</param>
/// <param name="Operations">The operations it offers, as words.</param>
/// <param name="Memberships">What the party carries of what the service requires, as words.</param>
/// <param name="Stock">What is on the shelves.</param>
/// <param name="Lessons">What the counter teaches.</param>
/// <param name="Sales">What of the party's own the counter would buy.</param>
/// <param name="Members">The members a lesson could be taught to.</param>
/// <param name="Action">What the last command asked for: <c>open</c>, <c>buy</c>, <c>sell</c>, and the rest, or empty before any.</param>
/// <param name="Outcome">What the last command did: <c>none</c>, <c>applied</c>, or <c>refused</c>.</param>
/// <param name="Code">The last refusal's code, empty when the last command applied or none has happened.</param>
/// <param name="Message">What the last command reported, empty before the party has asked for anything.</param>
/// <param name="Paid">How many coins the last command took from the party's purse.</param>
/// <param name="Earned">How many coins the last command put into it.</param>
/// <param name="Coins">What the party's one purse holds, as the service mechanism reads it.</param>
public readonly record struct ServiceSnapshot(
    bool Available,
    bool Open,
    string Id,
    string Kind,
    string Name,
    string Proprietor,
    string State,
    string Hours,
    IReadOnlyList<string> Operations,
    IReadOnlyList<string> Memberships,
    IReadOnlyList<ServiceStockSnapshot> Stock,
    IReadOnlyList<ServiceLessonSnapshot> Lessons,
    IReadOnlyList<ServiceSaleSnapshot> Sales,
    IReadOnlyList<ServiceMemberSnapshot> Members,
    string Action,
    string Outcome,
    string Code,
    string Message,
    int Paid,
    int Earned,
    int Coins)
{
    /// <summary>No service mechanism: there is no counter to stand at and nothing to browse.</summary>
    public static ServiceSnapshot None => new(
        Available: false,
        Open: false,
        Id: string.Empty,
        Kind: string.Empty,
        Name: string.Empty,
        Proprietor: string.Empty,
        State: string.Empty,
        Hours: string.Empty,
        Operations: [],
        Memberships: [],
        Stock: [],
        Lessons: [],
        Sales: [],
        Members: [],
        Action: string.Empty,
        Outcome: "none",
        Code: string.Empty,
        Message: string.Empty,
        Paid: 0,
        Earned: 0,
        Coins: 0);

    /// <summary>Reads the service facts out of the session's mechanism.</summary>
    /// <param name="services">The session's service mechanism, or null when it holds none.</param>
    /// <returns>The facts the panel shows, or <see cref="None"/> when there is no mechanism.</returns>
    public static ServiceSnapshot From(PartyServices? services)
    {
        if (services is null) return None;

        ServiceDefinition? current = services.Current;
        ServiceBrowse? browse = services.Browse();
        ServiceResult? last = services.Last;
        IReadOnlyList<ServiceStockOffer> offers = browse?.Stock ?? [];
        IReadOnlyList<ServiceLessonOffer> teaching = browse?.Lessons ?? [];
        IReadOnlyList<ServiceSaleOffer> purchases = browse?.Sales ?? [];
        IReadOnlyList<ServiceMemberOffer> roster = browse?.Members ?? [];

        List<ServiceStockSnapshot> stock = [];
        foreach (ServiceStockOffer offer in offers)
        {
            stock.Add(new ServiceStockSnapshot(offer.Lot.Value, offer.Definition.Value, offer.Name, offer.Count, offer.Price, offer.IsSale));
        }

        List<ServiceLessonSnapshot> lessons = [];
        foreach (ServiceLessonOffer offer in teaching)
        {
            lessons.Add(new ServiceLessonSnapshot(WireName(offer.Kind), offer.Subject, offer.Name, offer.Amount, offer.Price));
        }

        List<ServiceSaleSnapshot> sales = [];
        foreach (ServiceSaleOffer offer in purchases)
        {
            sales.Add(new ServiceSaleSnapshot(
                offer.Item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                offer.Definition.Value,
                offer.Name,
                offer.Price,
                offer.Damage,
                offer.Identified));
        }

        List<ServiceMemberSnapshot> members = [];
        foreach (ServiceMemberOffer offer in roster)
        {
            members.Add(new ServiceMemberSnapshot(offer.Index, offer.Name));
        }

        return new ServiceSnapshot(
            Available: true,
            Open: services.IsOpen,
            Id: current?.Id.Value ?? string.Empty,
            Kind: current?.Kind.Value ?? string.Empty,
            Name: current?.Name ?? string.Empty,
            Proprietor: current?.Proprietor ?? string.Empty,
            State: services.State,
            Hours: current?.Hours?.ToString() ?? string.Empty,
            Operations: browse?.Operations ?? [],
            Memberships: browse?.Memberships ?? [],
            Stock: stock,
            Lessons: lessons,
            Sales: sales,
            Members: members,
            Action: last?.Action ?? string.Empty,
            Outcome: last is null ? "none" : last.IsApplied ? "applied" : "refused",
            Code: last?.Code ?? string.Empty,
            Message: last?.Message ?? string.Empty,
            Paid: last?.Paid ?? 0,
            Earned: last?.Earned ?? 0,
            Coins: services.Coins);
    }

    /// <summary>The wire name for what a lesson grants.</summary>
    /// <remarks>
    /// A kind with no word is refused rather than published as an empty string: a screen that could not tell
    /// "this lesson teaches a skill" from a lesson this wire has no name for would offer the wrong command.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no wire name.</exception>
    public static string WireName(ServiceLessonKind kind) => kind switch
    {
        ServiceLessonKind.Skill => "skill",
        ServiceLessonKind.Effect => "effect",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown lesson kind."),
    };
}
