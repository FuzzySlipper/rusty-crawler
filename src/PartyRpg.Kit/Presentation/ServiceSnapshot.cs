using PartyRpg.Kit.Services;

namespace PartyRpg.Kit.Presentation;

/// <summary>One line of a service's shelves, as the panel shows it: what it is, how many, and what it costs.</summary>
/// <param name="Lot">The lot's identity, which a buy command names.</param>
/// <param name="Item">The item definition the line holds.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Count">How many are left on the shelves.</param>
/// <param name="Price">What one costs the party.</param>
/// <param name="IsSale">Whether the counter is reselling something it bought from the party.</param>
public sealed record ServiceStockSnapshot(
    string Lot,
    string Item,
    string Name,
    int Count,
    int Price,
    bool IsSale)
{
    /// <summary>Writes one lot on the shelves.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <param name="buys">Whether the counter the party stands at takes a purchase at all.</param>
    /// <param name="steals">Whether the counter keeps a shelf a member of the party could try without paying.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder, bool buys, bool steals) =>
        builder.Object(
            ("lot", builder.String(Lot)),
            ("item", builder.String(Item)),
            ("name", builder.String(Name)),
            ("count", builder.Number(Count)),
            ("price", builder.Number(Price)),
            ("sale", builder.Boolean(IsSale)),
            // A lot the counter has sold out of is still a row, and one a purchase would be refused on.
            ("canBuy", builder.Boolean(buys && Count > 0)),
            // A line nothing is left of is no more to be stolen than bought.
            ("canSteal", builder.Boolean(steals && Count > 0)));
}

/// <summary>One lesson a service teaches, as the panel shows it.</summary>
/// <param name="Kind">Whether the lesson grants a skill or a party-wide effect, as the wire spells it.</param>
/// <param name="Subject">The skill's id, or the effect's id, which a teach command names.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Amount">The skill level the lesson reaches, or the effect's magnitude.</param>
/// <param name="Price">What the lesson costs the party.</param>
/// <param name="Tier">The rung of a skill's ladder the lesson leaves a member at, where one is the first.</param>
public sealed record ServiceLessonSnapshot(
    string Kind,
    string Subject,
    string Name,
    int Amount,
    int Price,
    int Tier)
{
    /// <summary>Writes one lesson the counter teaches.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("kind", builder.String(Kind)),
            ("subject", builder.String(Subject)),
            ("name", builder.String(Name)),
            ("amount", builder.Number(Amount)),
            ("price", builder.Number(Price)),
            // The rung is published because two lessons of one skill are two rows on the screen: a
            // teach command names the subject and the rung together, and the row a player pressed is
            // the row it sends back.
            ("tier", builder.Number(Tier)));
}

/// <summary>One of the party's own items, as a service that would buy it shows it.</summary>
/// <param name="Item">The instance's durable identity, which a sell command names.</param>
/// <param name="Definition">The definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Price">What the counter would pay the party for it.</param>
/// <param name="Damage">How damaged the instance is; zero is sound.</param>
/// <param name="Identified">Whether the party knows what it is.</param>
public sealed record ServiceSaleSnapshot(
    string Item,
    string Definition,
    string Name,
    int Price,
    int Damage,
    bool Identified)
{
    /// <summary>Whether the instance carries the stolen mark, which a counter may refuse to deal in.</summary>
    public bool Stolen { get; init; }

    /// <summary>Writes one of the party's items the counter would buy.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("item", builder.String(Item)),
            ("definition", builder.String(Definition)),
            ("name", builder.String(Name)),
            ("price", builder.Number(Price)),
            ("damage", builder.Number(Damage)),
            ("identified", builder.Boolean(Identified)),
            ("stolen", builder.Boolean(Stolen)));

    /// <summary>Writes this item as one a counter would work on: which instance, and what it is called.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint WriteHeld(UiValueBuilder builder) =>
        builder.Object(
            ("item", builder.String(Item)),
            ("name", builder.String(Name)));
}

/// <summary>One thing a counter offers besides goods and lessons, as the panel shows it.</summary>
/// <remarks>
/// A cure, a passage, a provision, a room, or a line the counter posts is one offer of one kind, and what a
/// command naming it names is its subject: the place a passage reaches, the state a holding is kept in, the
/// condition a cure removes. The kind travels as the wire's own word so a screen offers the command that
/// belongs to it rather than guessing from the name a person reads.
/// </remarks>
/// <param name="Kind">What sort of offer it is, as the wire spells it.</param>
/// <param name="Subject">What the offer acts on, which a command naming it names.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Amount">How much of the subject there is: the days a passage takes, the portions a provision fills, the hours a room lasts.</param>
/// <param name="Price">What the counter charges the party for it.</param>
public sealed record ServiceOfferSnapshot(
    string Kind,
    string Subject,
    string Name,
    int Amount,
    int Price)
{
    /// <summary>Writes one other offer the counter makes.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("kind", builder.String(Kind)),
            // The subject is what a command naming the offer names — the place a passage reaches, the
            // condition a cure removes — so a row a player presses sends back the thing it was about
            // rather than a position in a list that the next browse could reorder.
            ("subject", builder.String(Subject)),
            ("name", builder.String(Name)),
            ("amount", builder.Number(Amount)),
            ("price", builder.Number(Price)));

    /// <summary>Writes this offer as a debt a screen has a repayment command for.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint WriteDebt(UiValueBuilder builder) =>
        builder.Object(
            ("subject", builder.String(Subject)),
            ("name", builder.String(Name)),
            // What is owed on the account, and what a repayment of all of it would take from the purse now: the
            // counter takes no more than the purse holds, which is the price rule's answer rather than the screen's.
            ("owed", builder.Number(Amount)),
            ("price", builder.Number(Price)),
            // A purse with nothing in it pays nothing toward the debt, which the price rule answered as nothing.
            ("canRepay", builder.Boolean(Price > 0)));

    /// <summary>Writes this offer as a passage a screen has a command for.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint WriteFare(UiValueBuilder builder) =>
        builder.Object(
            ("subject", builder.String(Subject)),
            ("name", builder.String(Name)),
            ("price", builder.Number(Price)));
}

/// <summary>One member a lesson could be taught to.</summary>
/// <param name="Index">The member's place in the party, counted from zero, which a teach command names.</param>
/// <param name="Name">What the member is called.</param>
public sealed record ServiceMemberSnapshot(int Index, string Name)
{
    /// <summary>Writes one member a lesson could go to.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("index", builder.Number(Index)),
            ("name", builder.String(Name)));
}

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
/// <param name="Offers">What else the counter offers: its cures, its passages, its provisions, its rooms, and its lines.</param>
/// <param name="Sales">What of the party's own the counter would buy.</param>
/// <param name="Members">The members a lesson could be taught to.</param>
/// <param name="Action">What the last command asked for: <c>open</c>, <c>buy</c>, <c>sell</c>, and the rest, or empty before any.</param>
/// <param name="Outcome">What the last command did: <c>none</c>, <c>applied</c>, or <c>refused</c>.</param>
/// <param name="Code">The last refusal's code, empty when the last command applied or none has happened.</param>
/// <param name="Message">What the last command reported, empty before the party has asked for anything.</param>
/// <param name="Paid">How many coins the last command took from the party's purse.</param>
/// <param name="Earned">How many coins the last command put into it.</param>
/// <param name="Coins">What the party's one purse holds, as the service mechanism reads it.</param>
public sealed record ServiceSnapshot(
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
    IReadOnlyList<ServiceOfferSnapshot> Offers,
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
    /// <summary>
    /// The members who could try to take a line off the counter's shelves without paying, as the theft rule
    /// answered for each; empty where nobody could, or where the counter keeps nothing to steal.
    /// </summary>
    public IReadOnlyList<ServiceMemberSnapshot> Thieves { get; init; } = [];

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
        Offers: [],
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
        IReadOnlyList<ServiceOfferLine> quoted = browse?.Offers ?? [];
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
            lessons.Add(new ServiceLessonSnapshot(WireName(offer.Kind), offer.Subject, offer.Name, offer.Amount, offer.Price, offer.Tier));
        }

        List<ServiceOfferSnapshot> lines = [];
        foreach (ServiceOfferLine line in quoted)
        {
            lines.Add(new ServiceOfferSnapshot(
                WireName(line.Offer.Kind),
                line.Offer.Target,
                line.Offer.Name,
                line.Offer.Amount,
                line.Price));
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
                offer.Identified) { Stolen = offer.Stolen });
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
            Offers: lines,
            Sales: sales,
            Members: members,
            Action: last?.Action ?? string.Empty,
            Outcome: last is null ? "none" : last.IsApplied ? "applied" : "refused",
            Code: last?.Code ?? string.Empty,
            Message: last?.Message ?? string.Empty,
            Paid: last?.Paid ?? 0,
            Earned: last?.Earned ?? 0,
            Coins: services.Coins)
        {
            Thieves = [.. (browse?.Thieves ?? []).Select(offer => new ServiceMemberSnapshot(offer.Index, offer.Name))],
        };
    }

    /// <summary>The wire name for what a counter offers besides goods and lessons.</summary>
    /// <remarks>
    /// A kind with no word is refused rather than published as an empty string, for the same reason a lesson's
    /// is: a screen that could not tell a passage from a room would offer the wrong command for the row.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no wire name.</exception>
    public static string WireName(ServiceOfferKind kind) => kind switch
    {
        ServiceOfferKind.Cure => "cure",
        ServiceOfferKind.Training => "training",
        ServiceOfferKind.Provision => "provision",
        ServiceOfferKind.Stay => "stay",
        ServiceOfferKind.Holding => "holding",
        ServiceOfferKind.Fare => "fare",
        ServiceOfferKind.Notice => "notice",
        ServiceOfferKind.Debt => "debt",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown offer kind."),
    };

    /// <summary>The wire name for what a lesson grants.</summary>
    /// <remarks>
    /// A kind with no word is refused rather than published as an empty string: a screen that could not tell
    /// "this lesson teaches a skill" from a lesson this wire has no name for would offer the wrong command.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no wire name.</exception>
    public static string WireName(ServiceLessonKind kind) => kind switch
    {
        ServiceLessonKind.Skill => "skill",
        ServiceLessonKind.Spell => "spell",
        ServiceLessonKind.Membership => "membership",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown lesson kind."),
    };

    /// <summary>Writes the service block: which counter the party stands at, what it offers, and what happened.</summary>
    /// <remarks>
    /// Every list is sent whole so the screen decides nothing: the shelves with their prices, the lessons
    /// with their fees, what the counter would buy from the party, and which members a lesson could go to.
    /// </remarks>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder)
    {
        // Which commands the counter takes is its own list of operations, read once here: a screen offers a
        // row's command when the product says the counter takes it, and never decides that from a word itself.
        bool buys = Takes(ServiceOperationKind.Buy);
        bool sells = Takes(ServiceOperationKind.Sell);
        bool identifies = Takes(ServiceOperationKind.Identify);
        bool repairs = Takes(ServiceOperationKind.Repair);
        bool teaches = Takes(ServiceOperationKind.Teach);
        bool steals = Takes(ServiceOperationKind.Steal) && Thieves.Count > 0;
        bool repays = Takes(ServiceOperationKind.Repay);

        // What the counter would identify and what it would mend are the party's own items it has a use for:
        // an item already known is not one to identify, and one that is whole is not one to repair.
        IEnumerable<ServiceSaleSnapshot> identify = identifies ? Sales.Where(offer => !offer.Identified) : [];
        IEnumerable<ServiceSaleSnapshot> repair = repairs ? Sales.Where(offer => offer.Damage > 0) : [];

        // A passage is the one offer besides goods and lessons a screen has a command for, so the passages are
        // published as their own list: which rows a player can press is the product's reading of its own kinds.
        string fare = WireName(ServiceOfferKind.Fare);
        IEnumerable<ServiceOfferSnapshot> fares = Open ? Offers.Where(offer => string.Equals(offer.Kind, fare, StringComparison.Ordinal)) : [];

        // What the party owes on an account this counter collects is the other offer a screen has a command for:
        // each row is the account, what is owed on it, and what a repayment would take from the purse now.
        string debt = WireName(ServiceOfferKind.Debt);
        IEnumerable<ServiceOfferSnapshot> debts = repays ? Offers.Where(offer => string.Equals(offer.Kind, debt, StringComparison.Ordinal)) : [];

        return builder.Object(
            ("available", builder.Boolean(Available)),
            ("open", builder.Boolean(Open)),
            ("id", builder.String(Id)),
            ("kind", builder.String(Kind)),
            ("name", builder.String(Name)),
            ("proprietor", builder.String(Proprietor)),
            ("state", builder.String(State)),
            ("hours", builder.String(Hours)),
            ("operations", builder.Array([.. Operations.Select(builder.String)])),
            ("memberships", builder.Array([.. Memberships.Select(builder.String)])),
            ("stock", builder.Array([.. Stock.Select(offer => offer.Write(builder, buys, steals))])),
            ("lessons", builder.Array([.. Lessons.Select(offer => offer.Write(builder))])),
            ("offers", builder.Array([.. Offers.Select(offer => offer.Write(builder))])),
            ("sales", builder.Array([.. Sales.Select(offer => offer.Write(builder))])),
            ("members", builder.Array([.. Members.Select(member => member.Write(builder))])),
            ("identify", builder.Array([.. identify.Select(offer => offer.WriteHeld(builder))])),
            ("repair", builder.Array([.. repair.Select(offer => offer.WriteHeld(builder))])),
            ("fares", builder.Array([.. fares.Select(offer => offer.WriteFare(builder))])),
            ("debts", builder.Array([.. debts.Select(offer => offer.WriteDebt(builder))])),
            ("thieves", builder.Array([.. (steals ? Thieves : Array.Empty<ServiceMemberSnapshot>()).Select(member => member.Write(builder))])),
            ("canSteal", builder.Boolean(steals)),
            ("canBuy", builder.Boolean(buys)),
            ("canSell", builder.Boolean(sells)),
            ("canTeach", builder.Boolean(teaches)),
            ("action", builder.String(Action)),
            ("outcome", builder.String(Outcome)),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)),
            ("paid", builder.Number(Paid)),
            ("earned", builder.Number(Earned)),
            ("coins", builder.Number(Coins)));
    }

    /// <summary>Whether the counter a visit has open carries out one operation.</summary>
    private bool Takes(ServiceOperationKind operation) =>
        Open && Operations.Contains(PartyServices.WireName(operation), StringComparer.Ordinal);
}
