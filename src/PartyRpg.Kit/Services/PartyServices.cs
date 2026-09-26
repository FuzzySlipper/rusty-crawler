using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Services;

/// <summary>
/// The one service mechanism: a party walks up to a counter, browses what it offers, transacts, and leaves.
/// </summary>
/// <remarks>
/// <para>
/// <b>One mechanism serves every kind.</b> A weapon shop, an armor shop, a magic shop, an alchemist, a
/// temple, a guild, a tavern, a bank, a training hall, a stable, and a town hall are all this class over a
/// different definition: which operations a counter offers, what its shelves hold, what it teaches, when it
/// opens, what it requires, and what its prices are multiplied by all come from content interpreted by the
/// ruleset. Adding a kind is adding content; the only thing that ever needs code is a new <em>operation</em>
/// — a cure, a rest, a deposit, a fare — which is a new step of this workflow and not a new class per
/// building.
/// </para>
/// <para>
/// <b>The party's own accounts are the only accounts.</b> A purchase is charged through
/// <see cref="PartyResourceLedger.Settle"/>, the same path a fare and a road's provisions take, and a sale
/// is paid through <see cref="PartyResourceLedger.Credit"/>. There is no per-character purse and no
/// service-held money: the party's one purse and one shared pack are everything a transaction touches, so a
/// price the party cannot pay refuses whole and names the shortfall.
/// </para>
/// <para>
/// <b>Nothing is a silent no-op.</b> Every command that cannot happen comes back as a refusal with a code
/// and a sentence: the counter is shut for the night, the party is not a member, the shelves are out of it,
/// the party holds no such item, the item is already identified or sound, the purse is short. What a
/// command did carries the coins that moved, because a player checks the purse.
/// </para>
/// <para>
/// <b>Stock and hours are game time.</b> A counter's hours are content's window read against the session's
/// one clock, and its shelves refresh on a repeating deadline of that same clock whose interval content
/// states — never on a counter kept in a frame loop. Every advance of the clock is handed here, by the
/// admitted update and by a journey alike, so a shelf that came due while the party travelled is full when
/// it comes back.
/// </para>
/// <para>
/// A visit is open or it is not, and entering and leaving are both explicit: the party enters by using the
/// person the interaction mechanism reached (a service target's Talk verb hands off here), and leaves when
/// it asks to. The mechanism holds no state of its own beyond the open visit, the shelves the party has
/// laid eyes on, and the last thing that happened, so two sessions that play the same commands see the same
/// counters.
/// </para>
/// </remarks>
public sealed class PartyServices : IGameTimeObserver
{
    private readonly IServiceRule _rule;
    private readonly PartyEntity _party;
    private readonly PartyResourceLedger? _accounts;
    private readonly GameClock? _clock;
    private readonly PartyProgression? _progression;
    private readonly Dictionary<ServiceId, ServiceShelf> _shelves = [];
    private ServiceDefinition? _current;
    private ServiceVisit? _visit;
    private ServiceResult? _last;

    /// <summary>Creates the service mechanism over the party it serves and the ruleset that answers for it.</summary>
    /// <param name="rule">This game's answers about services: what a counter is, what it offers, who it serves, and what it charges.</param>
    /// <param name="party">The party whose one purse and one shared pack are the only accounts a transaction touches.</param>
    /// <param name="accounts">
    /// The party's own accounts as the one settlement path. It is composed over the same party the session
    /// plays, so a shop and a road charge one purse; without one a transaction that moves coin is refused
    /// by name rather than settling against an account no one owns.
    /// </param>
    /// <param name="clock">
    /// The session's one clock, which opening hours are judged against and which a shelf's refresh deadline
    /// is registered on. Without one a service that states hours cannot be known to be open, and a shelf
    /// that states a schedule keeps what it was laid out with.
    /// </param>
    /// <param name="progression">
    /// The party's progression owner, which is what a training step settles through: the level rise, the
    /// growth of the pools, and the skill points a level grants all happen there, and this mechanism only
    /// charges the fee and reports the step. Without one a hall's fee is still quoted and the step is
    /// refused by name rather than levelling a member nothing owns.
    /// </param>
    /// <exception cref="ArgumentNullException">The rule or the party is missing.</exception>
    public PartyServices(
        IServiceRule rule,
        PartyEntity party,
        PartyResourceLedger? accounts = null,
        GameClock? clock = null,
        PartyProgression? progression = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _accounts = accounts;
        _clock = clock;
        _progression = progression;
    }

    /// <summary>This game's answers about services.</summary>
    public IServiceRule Rule => _rule;

    /// <summary>The service being visited, or null when the party stands at no counter.</summary>
    public ServiceVisit? Visit => _visit;

    /// <summary>Whether a visit is open.</summary>
    public bool IsOpen => _visit is not null;

    /// <summary>
    /// The service the party last stood at: the open visit's, or the counter a walk-in was refused at, so a
    /// panel can name the shop that turned the party away instead of showing nothing at all.
    /// </summary>
    public ServiceDefinition? Current => _current;

    /// <summary>The last command's result, or null before the party has dealt with a service.</summary>
    public ServiceResult? Last => _last;

    /// <summary>
    /// Whether the counter the party last stood at is serving now: <c>open</c>, <c>closed</c> when content's
    /// hours say so, or empty when the party has stood at none. It is recomputed from the clock rather than
    /// remembered, so a shop whose closing hour passed while the party browsed reads closed.
    /// </summary>
    public string State
    {
        get
        {
            if (_current is not { } service) return string.Empty;
            return Closed(service) is null ? "open" : "closed";
        }
    }

    /// <summary>
    /// What the party's one purse holds, as every result reports it. It is the party's own balance read
    /// live, never a copy kept beside it, so a service can never show a purse the party does not have.
    /// </summary>
    public int Coins => _party.Purse.Coins;

    /// <summary>
    /// Opens the service a placement keeps, if it keeps one, and reports what came of it.
    /// </summary>
    /// <remarks>
    /// This is the handoff the interaction mechanism reaches: a person the party talked to offers the
    /// counter behind them, and the service mechanism is what decides whether the party may walk in. A
    /// placement that keeps no service answers null, which is how a ruleset says "this person is talked to
    /// for something else" without a second way to reach a person existing.
    /// </remarks>
    /// <param name="place">The place the placement stands in.</param>
    /// <param name="placement">The placement content declared.</param>
    /// <returns>What opening the counter did, or null when the placement keeps no service.</returns>
    /// <exception cref="ArgumentNullException">The placement is null.</exception>
    public ServiceResult? OpenTarget(PlaceId place, PlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return _rule.Describe(new ServiceTargetRequest(place, placement)) is { } service ? Open(service) : null;
    }

    /// <summary>Opens a service the party has walked in to, or refuses by name when it is shut.</summary>
    /// <remarks>
    /// The counter the party is standing at is recorded whether or not it is serving, so a refusal names
    /// the shop and the hours it keeps rather than leaving the party looking at nothing. A visit opened
    /// while another was open replaces it: a party is at one counter at a time.
    /// </remarks>
    /// <param name="service">The service being entered.</param>
    /// <returns>What opening it did.</returns>
    /// <exception cref="ArgumentNullException">The service is null.</exception>
    public ServiceResult Open(ServiceDefinition service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _visit = null;
        _current = service;
        if (Closed(service) is { } refusal)
        {
            return Record(ServiceResult.Refused("open", refusal.Code, refusal.Message, Coins));
        }

        _visit = new ServiceVisit(service, ShelfFor(service));
        return Record(ServiceResult.Applied("open", $"The party steps up to the counter of {service.Describe()}.", coins: Coins));
    }

    /// <summary>Leaves the counter the party stands at.</summary>
    /// <returns>What leaving did, or a refusal when no visit was open.</returns>
    public ServiceResult Close()
    {
        if (_visit is not { } visit)
        {
            return Record(ServiceResult.Refused("leave", "service-not-open", "The party is not standing at a service counter.", Coins));
        }

        string name = visit.Service.Name;
        _visit = null;
        _current = null;
        return Record(ServiceResult.Applied("leave", $"The party leaves {name}.", coins: Coins));
    }

    /// <summary>Runs one command at the counter the party stands at, and reports what it did.</summary>
    /// <remarks>
    /// Every operation goes through this one path and in this one order: resolve what the command names,
    /// judge whether the party may do it, quote what it costs, judge what the party must be able to hold,
    /// settle the charge whole through the party's one ledger, apply the change, credit what the service
    /// pays, and report the coins that moved. A refusal at any step leaves the party and the shelves exactly
    /// as they were.
    /// </remarks>
    /// <param name="command">What the screen asked the service to do.</param>
    /// <returns>What the command did, or why it did nothing.</returns>
    /// <exception cref="ArgumentNullException">The command is null.</exception>
    public ServiceResult Transact(ServiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Kind == ServiceCommandKind.Leave) return Close();
        if (_visit is not { } visit)
        {
            return Refuse(command.Kind, "service-not-open", "The party is not standing at a service counter.");
        }

        ServiceDefinition service = visit.Service;
        if (command.Operation is not { } operation)
        {
            return Refuse(command.Kind, "service-operation-unknown", $"Nothing in this build knows what '{command.Kind}' asks a service for.");
        }

        if (!service.Offers(operation))
        {
            return Refuse(command.Kind, "service-operation-unavailable", $"{service.Describe()} does not offer to {Word(command.Kind)}.");
        }

        // The hours are re-judged on every command, not only when the party walked in: a shop that closed
        // while the party browsed stops serving, and says so, rather than selling on because a screen is
        // open. Locking the door outside hours belongs to the schedule owner that closes doors.
        if (Closed(service) is { } shut) return Refuse(command.Kind, shut.Code, shut.Message);

        if (Resolve(visit, command, out ServiceSubject subject, out PartyMemberId member) is { } missing) return missing;

        ServiceEligibility eligibility = _rule.Judge(new ServiceEligibilityRequest(service, operation, subject, member, _party, _clock));
        if (eligibility.Refusal is { } refused) return Refuse(command.Kind, refused.Code, refused.Message);

        ServiceQuote quote = _rule.Quote(new ServiceQuoteRequest(service, operation, subject, member, _party, _clock));

        // What the party must be able to hold is judged before anything is settled, so a purchase the pack
        // cannot take refuses whole rather than charging for goods that would have to be dropped.
        if (operation == ServiceOperationKind.Buy &&
            subject.Definition is { } definition &&
            _party.Inventory.Judge(definition, subject.Count) is { } noRoom)
        {
            return Refuse(command.Kind, noRoom.Code, noRoom.Message);
        }

        if ((!quote.Charge.IsFree || !quote.Payment.IsFree) && _accounts is null)
        {
            return Refuse(
                command.Kind,
                "service-no-accounts",
                $"{service.Describe()} settles in coin and this session holds no party accounts to settle against; the party's purse is the only purse a service may touch.");
        }

        if (!quote.Charge.IsFree)
        {
            ResourceSettlement settlement = _accounts!.Settle(quote.Charge);
            if (!settlement.Admitted) return Refuse(command.Kind, settlement.Refusal!.Code, settlement.Refusal.Message);
        }

        if (Apply(visit, command.Kind, operation, subject, member, quote) is { } failed)
        {
            // The change itself refused after the charge had settled, which the judgements above exist to
            // prevent. The coin goes back rather than the party paying for nothing, and the refusal is
            // reported with both facts.
            if (!quote.Charge.IsFree) _accounts!.Credit(quote.Charge);
            return failed;
        }

        if (!quote.Payment.IsFree) _accounts!.Credit(quote.Payment);
        string message = Message(visit, operation, subject, member, quote);
        return Record(ServiceResult.Applied(Word(command.Kind), message, quote.Charge.Coins, quote.Payment.Coins, Coins));
    }

    /// <summary>What the open service offers the party, or null when no visit is open.</summary>
    /// <remarks>
    /// A browse is rebuilt from the shelf, the party, and the clock every time it is read, so a price it
    /// shows is the price the counter would charge now. Prices come from the ruleset's price rule and the
    /// lists from its stock and lesson rules, which is what keeps a screen from deciding anything.
    /// </remarks>
    /// <returns>What is browsable, or null when the party stands at no counter.</returns>
    public ServiceBrowse? Browse()
    {
        if (_visit is not { } visit) return null;
        ServiceDefinition service = visit.Service;

        List<string> operations = [.. service.Operations.Select(operation => Word(operation))];
        List<string> memberships = [.. _rule.Access(new ServiceAccessRequest(service, _party))];

        List<ServiceStockOffer> stock = [];
        foreach (ServiceStockLot lot in visit.Shelf.Lots)
        {
            // A line the shop has sold out of is published at zero rather than hidden: "this shop has none
            // left" is an answer a player acts on, and a line that vanished would look like a shop that
            // never sold the thing at all.
            //
            // A lot that holds one item the party sold is sold back whole, because an item instance is
            // indivisible here: the party that wanted to keep part of a stack would have sold part of it.
            int count = lot.IsSale ? lot.Count : 1;
            int price = Price(service, ServiceOperationKind.Buy, ServiceSubject.OfLot(lot, count), default).Charge.Coins;
            stock.Add(new ServiceStockOffer(lot.Id, lot.Definition, lot.Label, lot.Count, price, lot.Value, lot.IsSale));
        }

        List<ServiceLessonOffer> lessons = [];
        foreach (ServiceLesson lesson in _rule.Lessons(new ServiceLessonRequest(service, _party, _clock)))
        {
            int price = Price(service, ServiceOperationKind.Teach, ServiceSubject.OfLesson(lesson), default).Charge.Coins;
            lessons.Add(new ServiceLessonOffer(lesson.Kind, lesson.Subject, lesson.Label, lesson.Amount, price));
        }

        // What else the counter offers is priced the same way its lessons are: the operation the offer is
        // taken as decides which price the policy quotes, so a panel shows what a command would charge
        // rather than a second number worked out beside it.
        List<ServiceOfferLine> offers = [];
        foreach (ServiceOffer offer in _rule.Offers(new ServiceOfferRequest(service, _party, _clock)))
        {
            // A notice is read rather than taken, so it is published at no price: quoting one through the
            // price rule would ask what a rumour costs, which is a question no counter answers.
            int price = offer.Kind == ServiceOfferKind.Notice
                ? 0
                : Price(service, OperationOf(offer.Kind), ServiceSubject.OfOffer(offer, offer.Amount), Subject(offer)).Charge.Coins;
            offers.Add(new ServiceOfferLine(offer, price));
        }

        List<ServiceSaleOffer> sales = [];
        if (service.Offers(ServiceOperationKind.Sell))
        {
            // Only the shared pack is for sale: what a member wears is that member's figure, and a shop
            // that bought it would be taking the boots off a character's feet from a menu.
            foreach (ItemInstance item in _party.Inventory.Items)
            {
                int price = Price(service, ServiceOperationKind.Sell, ServiceSubject.OfItem(item), default).Payment.Coins;
                sales.Add(new ServiceSaleOffer(
                    item.Id,
                    item.Definition,
                    item.Definition.Value,
                    price,
                    item.State.Damage,
                    item.State.IsIdentified));
            }
        }

        List<ServiceMemberOffer> members = [];
        for (int index = 0; index < _party.Members.Count; index++)
        {
            PartyMember candidate = _party.Members[index];
            members.Add(new ServiceMemberOffer(index, candidate.Id, candidate.Profile.Name));
        }

        return new ServiceBrowse(service, operations, memberships, stock, lessons, offers, sales, members);
    }

    /// <summary>
    /// What the counter the party stands at offers to train one member for, priced, or null when nobody
    /// there trains.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A training step is priced for a member rather than for the party — the level and the class rank are
    /// the member's — so this is the one read that answers with the fee a named member would be charged. It
    /// is what a panel publishes as the price of the next level, and it is the same quote
    /// <see cref="Transact"/> settles, so a number shown and a number charged cannot be two numbers.
    /// </para>
    /// <para>
    /// It is a reading: nothing is judged, nothing is charged, and a member who has not earned the level
    /// still gets the price the step would cost.
    /// </para>
    /// </remarks>
    /// <param name="memberIndex">The member's place in the party, counted from zero.</param>
    /// <returns>The priced offer, or null when the party stands at no counter or that counter trains nobody.</returns>
    public ServiceOfferLine? TrainingOffer(int memberIndex)
    {
        if (_visit is not { } visit) return null;
        if (memberIndex < 0 || memberIndex >= _party.Members.Count) return null;
        if (!visit.Service.Offers(ServiceOperationKind.Train)) return null;

        foreach (ServiceOffer offer in _rule.Offers(new ServiceOfferRequest(visit.Service, _party, _clock)))
        {
            if (offer.Kind != ServiceOfferKind.Training) continue;
            PartyMember trainee = _party.Members[memberIndex];
            ServiceQuote quote = Price(
                visit.Service,
                ServiceOperationKind.Train,
                ServiceSubject.OfOffer(offer, 1),
                trainee.Id);
            return new ServiceOfferLine(offer, quote.Charge.Coins);
        }

        return null;
    }

    /// <summary>
    /// The member an offer's price is read for, or an unset identity when it acts on nobody in particular.
    /// </summary>
    /// <remarks>
    /// A cure and a training step are priced from one member's own state, and a browse has no member: it
    /// lists what a counter offers to the party. The party's own first member stands in for the price shown
    /// on that list — the number is a real member's — and the command that acts is priced for the member it
    /// names, which is why the two answers are asked separately. A party with no members prices nothing,
    /// and the policy's own quote is what refuses it.
    /// </remarks>
    private PartyMemberId Subject(ServiceOffer offer) =>
        offer.Kind is ServiceOfferKind.Cure or ServiceOfferKind.Training && _party.Members.Count > 0
            ? _party.Members[0].Id
            : default;

    /// <inheritdoc />
    /// <remarks>
    /// A shelf whose refresh deadline came due is laid out again from the ruleset's answer over content, so
    /// what a shop holds when full is policy and the schedule that fills it is game time. Everything the
    /// party sold the shop stays where it is: only content's own lines are restored.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The advance is null.</exception>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        if (advance.Due.Count == 0) return;

        foreach (DeadlineDue due in advance.Due)
        {
            foreach (ServiceShelf shelf in _shelves.Values)
            {
                if (shelf.Refresh != due.Deadline) continue;
                shelf.Restock(_rule.Stock(new ServiceStockRequest(shelf.Service, _party, _clock)));
            }
        }
    }

    /// <summary>
    /// Whether the mechanism holds this deadline, which is what tells a report of a due deadline something
    /// acted on from one nobody owns.
    /// </summary>
    /// <param name="deadline">The deadline handle a clock reported.</param>
    public bool Holds(DeadlineId deadline)
    {
        foreach (ServiceShelf shelf in _shelves.Values)
        {
            if (shelf.Refresh == deadline) return true;
        }

        return false;
    }

    /// <summary>The shelves of one service, laid out the first time the party sees them.</summary>
    /// <remarks>
    /// Laying a shelf out is also what registers its refresh deadline, so a service nobody ever walks into
    /// costs the clock nothing. The deadline is repeating and re-arms from the instant the clock actually
    /// reached, which is why a long journey fills the shelves once rather than once per missed interval.
    /// </remarks>
    private ServiceShelf ShelfFor(ServiceDefinition service)
    {
        if (_shelves.TryGetValue(service.Id, out ServiceShelf? existing)) return existing;

        ServiceShelf shelf = ServiceShelf.From(service, _rule.Stock(new ServiceStockRequest(service, _party, _clock)));
        if (service.RefreshInterval is { } interval && _clock is { } clock) shelf.Refresh = clock.ScheduleEvery(interval);
        _shelves[service.Id] = shelf;
        return shelf;
    }

    /// <summary>
    /// Resolves what a command names into the thing the operation acts on, or the refusal that says it is
    /// not there.
    /// </summary>
    /// <remarks>
    /// Resolving before policy is asked is what separates "the shelves are out of it" from "the party holds
    /// no such item" and both from "the counter teaches no such lesson": three answers a player acts on
    /// differently, and three the mechanism can only tell apart by resolving first.
    /// </remarks>
    private ServiceResult? Resolve(ServiceVisit visit, ServiceCommand command, out ServiceSubject subject, out PartyMemberId member)
    {
        subject = null!;
        member = default;
        ServiceDefinition service = visit.Service;

        switch (command.Kind)
        {
            case ServiceCommandKind.Buy:
            {
                if (visit.Shelf.Lot(new ServiceLotId(command.Target)) is not { } lot)
                {
                    return Refuse(command.Kind, "service-no-such-lot", $"{service.Describe()} has no line '{command.Target}' on its shelves.");
                }

                if (lot.IsEmpty)
                {
                    return Refuse(command.Kind, "service-out-of-stock", $"{service.Describe()} has sold out of {lot.Label}.");
                }

                int count = lot.IsSale ? lot.Count : command.Count;
                if (count < 1)
                {
                    return Refuse(command.Kind, "service-count-invalid", $"The party asked for {command.Count} × {lot.Label}, and a purchase is of at least one.");
                }

                if (count > lot.Count)
                {
                    return Refuse(
                        command.Kind,
                        "service-not-enough-stock",
                        $"{service.Describe()} holds {lot.Count} × {lot.Label} and the party asked for {count}.");
                }

                subject = ServiceSubject.OfLot(lot, count);
                return null;
            }

            case ServiceCommandKind.Sell:
            case ServiceCommandKind.Identify:
            case ServiceCommandKind.Repair:
            {
                if (!ulong.TryParse(command.Target, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) || value == 0)
                {
                    return Refuse(command.Kind, "service-no-such-item", $"The party holds no item '{command.Target}', so there is nothing to {Word(command.Kind)}.");
                }

                if (_party.FindItem(new ItemInstanceId(value)) is not { } item)
                {
                    return Refuse(command.Kind, "service-no-such-item", $"The party holds no item {value}, so there is nothing to {Word(command.Kind)}.");
                }

                // A member's figure is not the party's stock: selling it would take a worn item off a
                // character from a shop screen, so a shop buys only what lies in the shared pack.
                if (command.Kind == ServiceCommandKind.Sell && !item.Custody.IsInSharedInventory)
                {
                    return Refuse(command.Kind, "service-item-worn", $"{item.Definition} is worn by a member, and a shop buys what lies in the party's pack.");
                }

                // The state an operation changes is the instance's own, so the mechanism knows when there
                // is nothing to do: charging a fee to identify what is identified would be taking coin for
                // a change that never happened.
                if (command.Kind == ServiceCommandKind.Identify && item.State.IsIdentified)
                {
                    return Refuse(command.Kind, "service-already-identified", $"{item.Definition} is identified already, so there is nothing to learn about it.");
                }

                if (command.Kind == ServiceCommandKind.Repair && item.State.Damage == 0)
                {
                    return Refuse(command.Kind, "service-not-damaged", $"{item.Definition} is sound, so there is nothing to repair.");
                }

                subject = ServiceSubject.OfItem(item);
                return null;
            }

            case ServiceCommandKind.Cure:
            case ServiceCommandKind.Train:
            case ServiceCommandKind.Provision:
            case ServiceCommandKind.Stay:
            case ServiceCommandKind.Deposit:
            case ServiceCommandKind.Withdraw:
            case ServiceCommandKind.Fare:
            {
                ServiceOfferKind want = OfferKindOf(command.Kind);
                List<ServiceOffer> offers = [.. _rule.Offers(new ServiceOfferRequest(service, _party, _clock))
                    .Where(offer => offer.Kind == want)];
                if (offers.Count == 0)
                {
                    return Refuse(command.Kind, "service-no-such-offer", $"{service.Describe()} offers no {Word(command.Kind)}.");
                }

                // A command that names nothing takes the counter's one offer of that kind — a hall has one
                // training step, a bank one account, a tavern one room — and a command that names something
                // takes the offer that subject identifies. A counter with several offers of a kind and
                // nothing named is refused rather than guessed at, because taking the wrong room or the wrong
                // passage would charge the party for a journey it did not ask for.
                ServiceOffer? chosen = null;
                if (command.Target.Length == 0)
                {
                    if (offers.Count > 1)
                    {
                        return Refuse(
                            command.Kind,
                            "service-offer-ambiguous",
                            $"{service.Describe()} offers {offers.Count} things to {Word(command.Kind)} and the command named none of them.");
                    }

                    chosen = offers[0];
                }
                else
                {
                    foreach (ServiceOffer candidate in offers)
                    {
                        if (string.Equals(candidate.Target, command.Target, StringComparison.Ordinal))
                        {
                            chosen = candidate;
                            break;
                        }
                    }
                }

                if (chosen is null)
                {
                    return Refuse(command.Kind, "service-no-such-offer", $"{service.Describe()} offers nothing called '{command.Target}' to {Word(command.Kind)}.");
                }

                // Which member an offer goes to is asked only of the offers that act on one: a room, a
                // provision, and an account are the party's, and a member index for them would be a number
                // nothing reads.
                if (want is ServiceOfferKind.Cure or ServiceOfferKind.Training)
                {
                    if (command.Member < 0 || command.Member >= _party.Members.Count)
                    {
                        return Refuse(command.Kind, "service-no-such-member", $"The party has no member {command.Member + 1}, so there is nobody for {chosen.Name} to act on.");
                    }

                    member = _party.Members[command.Member].Id;
                }

                // What a deposit or a withdrawal moves is coin, which the command counts; every other offer
                // acts on as many of itself as the offer states.
                int count = command.Kind is ServiceCommandKind.Deposit or ServiceCommandKind.Withdraw
                    ? command.Count
                    : Math.Max(1, command.Count);

                if (count < 1)
                {
                    return Refuse(command.Kind, "service-count-invalid", $"The party asked for {count} of {chosen.Name}, and an operation acts on at least one.");
                }

                subject = ServiceSubject.OfOffer(chosen, count);
                return null;
            }

            case ServiceCommandKind.Teach:
            {
                ServiceLesson? lesson = null;
                foreach (ServiceLesson candidate in _rule.Lessons(new ServiceLessonRequest(service, _party, _clock)))
                {
                    if (string.Equals(candidate.Subject, command.Target, StringComparison.Ordinal))
                    {
                        lesson = candidate;
                        break;
                    }
                }

                if (lesson is null)
                {
                    return Refuse(command.Kind, "service-no-such-lesson", $"{service.Describe()} teaches no lesson called '{command.Target}'.");
                }

                if (command.Member < 0 || command.Member >= _party.Members.Count)
                {
                    return Refuse(command.Kind, "service-no-such-member", $"The party has no member {command.Member + 1}, so a lesson has nobody to go to.");
                }

                member = _party.Members[command.Member].Id;
                subject = ServiceSubject.OfLesson(lesson);
                return null;
            }

            default:
                return Refuse(command.Kind, "service-operation-unknown", $"Nothing in this build knows what '{command.Kind}' asks a service for.");
        }
    }

    /// <summary>Applies what an operation changes, or returns the refusal that says why it changed nothing.</summary>
    /// <remarks>
    /// By the time this runs the eligibility, the price, the pack's room, and the charge have all been
    /// judged, so a refusal here is a rule that changed its mind between two calls. The caller gives the
    /// coin back and reports it rather than leaving the party charged for nothing.
    /// </remarks>
    private ServiceResult? Apply(
        ServiceVisit visit,
        ServiceCommandKind kind,
        ServiceOperationKind operation,
        ServiceSubject subject,
        PartyMemberId member,
        ServiceQuote quote)
    {
        switch (operation)
        {
            case ServiceOperationKind.Buy:
            {
                ServiceStockLot lot = subject.Lot!;
                ItemInstance instance = lot.IsSale
                    ? lot.Instance!
                    : _party.CreateItem(lot.Definition, subject.Count);
                ItemAcquisition acquisition = _party.AcquireItem(instance);
                if (!acquisition.Admitted) return Refuse(kind, acquisition.Refusal!.Code, acquisition.Refusal.Message);
                visit.Shelf.Take(lot, subject.Count);
                return null;
            }

            case ServiceOperationKind.Sell:
            {
                ItemInstance? released = _party.ReleaseItem(subject.Item!.Id);
                if (released is null)
                {
                    return Refuse(kind, "service-no-such-item", $"The party holds no item {subject.Item.Id}, so there is nothing to sell.");
                }

                // What the shop paid is the base it prices the item back from, so a buy-back is priced from
                // the same number the shop's own margin was taken from rather than from a fresh guess.
                visit.Shelf.Accept(released, quote.Value > 0 ? quote.Value : subject.Value);
                return null;
            }

            case ServiceOperationKind.Identify:
                subject.Item!.Identify();
                return null;

            case ServiceOperationKind.Repair:
                subject.Item!.Repair(subject.Item.State.Damage);
                return null;

            case ServiceOperationKind.Cure:
            {
                // What a cure ends is the offer's own list rather than a condition the mechanism was told:
                // a temple that claims to remove death and eradication states both, and a stay that clears
                // what a night clears states that instead. Restoring the body is the same act — the donor's
                // temple healing resets the conditions and fills both pools together — so it happens here
                // rather than being a second operation nothing would offer.
                ServiceOffer cure = subject.Offer!;
                PartyMember patient = _party.Member(member);
                foreach (ConditionId condition in cure.Conditions) patient.Conditions.Clear(condition);
                if (cure.Amount > 0) patient.Resources.RestoreAll();
                return null;
            }

            case ServiceOperationKind.Train:
            {
                // A training step is one level, and the level is the progression owner's to grant: the fee
                // has been settled through the party's one ledger above, the hall's ceiling comes from the
                // offer content states, and the rise, the growth of the pools, and the skill points the new
                // level grants all happen in the owner. This mechanism therefore reports the step rather
                // than performing it, which is what keeps one owner of a level and one owner of the curve.
                //
                // Without an owner nothing may rise, and the step is refused by name: charging a fee for a
                // level no one could grant would be the worst of both.
                if (_progression is not { } progression)
                {
                    return Refuse(
                        kind,
                        "service-no-progression",
                        $"{visit.Service.Describe()} trains by the level and this session holds no progression owner to grant one.");
                }

                ServiceOffer training = subject.Offer!;
                ProgressionTrainingResult step = progression.Train(
                    member,
                    new ProgressionTrainingTerms(visit.Service.Name, quote.Charge.Coins, training.Limit));
                if (step.Refusal is { } refused)
                {
                    return Refuse(kind, refused.Code, refused.Message);
                }

                return null;
            }

            case ServiceOperationKind.Provision:
            {
                // Provisions are the party's own account rather than an instance in its pack, so they are
                // credited where a purchase would mint items: one purchase fills the amount the offer
                // states, and the count is how many purchases the command asked for.
                _party.Food.Credit(checked(subject.Offer!.Amount * subject.Count));
                return null;
            }

            case ServiceOperationKind.Stay:
            {
                // A room buys a night. Game time passes on the session's one clock to the hour the room
                // gives up at, and the party rests: what a rest clears is the offer's own list, which is how
                // a night that ends a weakness differs from a cure that ends a disease. Without a clock the
                // time cannot pass, and the charge is refused before this runs rather than the party paying
                // for a night that never came.
                if (_clock is not { } clock)
                {
                    return Refuse(kind, "service-no-clock", $"{visit.Service.Describe()} rents rooms by the night and this session keeps no clock, so no night could pass.");
                }

                ServiceOffer room = subject.Offer!;
                int hours = room.Amount < 1 ? 1 : room.Amount;
                clock.Advance(GameDuration.FromHours(hours));
                foreach (PartyMember sleeper in _party.Members)
                {
                    foreach (ConditionId condition in room.Conditions) sleeper.Conditions.Clear(condition);
                    sleeper.Resources.RestoreAll();
                }

                return null;
            }

            case ServiceOperationKind.Deposit:
            {
                // The coins leave the purse through the party's one ledger and are recorded as what the
                // counter keeps for the party, under the name the offer states. Nothing is minted and nothing
                // is lost: the purse and the holding are two places the same coins can be, and both are
                // party state a save carries.
                string account = Holding(subject);
                ServiceHolding.Set(_party, account, ServiceHolding.Coins(_party, account) + subject.Count);
                return null;
            }

            case ServiceOperationKind.Withdraw:
            {
                int held = ServiceHolding.Coins(_party, Holding(subject));
                if (held < subject.Count)
                {
                    return Refuse(kind, "service-holding-short", $"{visit.Service.Describe()} holds {held} coin(s) for the party and the party asked for {subject.Count}.");
                }

                ServiceHolding.Set(_party, Holding(subject), held - subject.Count);
                return null;
            }

            case ServiceOperationKind.Fare:
            {
                // A fare is party-carried state: the party holds the ticket and the road honours it. The days
                // the journey takes are the offer's own amount, which is what the travel policy reads to
                // price the boarding, so the counter that sold the ticket and the road that takes it agree
                // about the journey without either asking the other.
                ServicePassage.Grant(_party, new PlaceId(subject.Offer!.Subject), subject.Offer.Amount < 1 ? 1 : subject.Offer.Amount);
                return null;
            }

            case ServiceOperationKind.Teach:
            {
                ServiceLesson lesson = subject.Lesson!;
                PartyMember recipient = _party.Member(member);
                if (lesson.Kind == ServiceLessonKind.Skill)
                {
                    // A lesson bought with coin teaches the skill or raises it to the rung and level the
                    // lesson states, and spends no skill points: the fee is what it cost. How far a class
                    // may take a skill is the eligibility rule's answer, which judged this already.
                    SkillId skill = new(lesson.Subject);
                    if (!recipient.Skills.Knows(skill))
                    {
                        recipient.Skills.Learn(skill, new SkillTier(lesson.Tier));
                    }
                    else if (recipient.Skills.TierOf(skill).Value < lesson.Tier)
                    {
                        recipient.Skills.SetTier(skill, new SkillTier(lesson.Tier));
                    }

                    int level = recipient.Skills.LevelOf(skill);
                    if (level < lesson.Amount) recipient.Skills.RaiseLevel(skill, lesson.Amount - level, 0);
                    return null;
                }

                // A membership is party-carried state: the effect is applied to the band, where the access
                // requirement reads it back and the party's own save already records it.
                _party.Effects.Apply(new PartyEffect(new EffectId(lesson.Subject), lesson.Amount));
                return null;
            }

            default:
                return Refuse(kind, "service-operation-unknown", $"Nothing in this build knows how to carry out {operation}.");
        }
    }

    /// <summary>What a transaction did, in the words a person reads.</summary>
    private string Message(
        ServiceVisit visit,
        ServiceOperationKind operation,
        ServiceSubject subject,
        PartyMemberId member,
        ServiceQuote quote) => operation switch
        {
            ServiceOperationKind.Buy =>
                $"The party buys {subject.Count} × {subject.Lot!.Label} for {quote.Charge.Coins} coin(s), and {subject.Lot.Count} are left.",
            ServiceOperationKind.Sell =>
                $"The party sells {subject.Item!.StackCount} × {subject.Item.Definition} for {quote.Payment.Coins} coin(s), and the counter will sell it back.",
            ServiceOperationKind.Identify =>
                $"The party pays {quote.Charge.Coins} coin(s) to identify {subject.Item!.Definition}.",
            ServiceOperationKind.Repair =>
                $"The party pays {quote.Charge.Coins} coin(s) to repair {subject.Item!.Definition}.",
            ServiceOperationKind.Cure =>
                $"The party pays {quote.Charge.Coins} coin(s) and {_party.Member(member).Profile.Name} is healed of {string.Join(", ", subject.Offer!.Conditions)}.",
            ServiceOperationKind.Train => TrainMessage(member, quote),
            ServiceOperationKind.Provision =>
                $"The party pays {quote.Charge.Coins} coin(s) for {subject.Offer!.Amount * subject.Count} provisions.",
            ServiceOperationKind.Stay =>
                $"The party pays {quote.Charge.Coins} coin(s) for {subject.Offer!.Name} and rests for {subject.Offer.Amount} hour(s).",
            ServiceOperationKind.Deposit =>
                $"The party leaves {subject.Count} coin(s) with {visit.Service.Name} and it now holds {ServiceHolding.Coins(_party, Holding(subject))}.",
            ServiceOperationKind.Withdraw =>
                $"The party takes {subject.Count} coin(s) back from {visit.Service.Name} and it holds {ServiceHolding.Coins(_party, Holding(subject))}.",
            ServiceOperationKind.Fare =>
                $"The party pays {quote.Charge.Coins} coin(s) for a passage to {subject.Offer!.Subject}, which takes {subject.Offer.Amount} day(s).",
            ServiceOperationKind.Teach when subject.Lesson!.Kind == ServiceLessonKind.Skill =>
                $"{_party.Member(member).Profile.Name} is taught {subject.Lesson.Label} to level {subject.Lesson.Amount} for {quote.Charge.Coins} coin(s).",
            ServiceOperationKind.Teach =>
                $"The party pays {quote.Charge.Coins} coin(s) for {subject.Lesson!.Label}.",
            _ => $"The party's transaction at {visit.Service.Name} is done.",
        };

    /// <summary>Prices one operation through the ruleset's price rule.</summary>
    private ServiceQuote Price(ServiceDefinition service, ServiceOperationKind operation, ServiceSubject subject, PartyMemberId member) =>
        _rule.Quote(new ServiceQuoteRequest(service, operation, subject, member, _party, _clock));

    /// <summary>
    /// What a completed training step reports: the level the member now stands at, what it cost, and the
    /// skill points the level granted.
    /// </summary>
    /// <remarks>
    /// The level is read from the member rather than from the offer: the offer states the hall's ceiling,
    /// and a sentence that reported the ceiling as the level reached would tell a player they had risen to
    /// a level they had not. The points come from the owner's own record of the step it granted.
    /// </remarks>
    private string TrainMessage(PartyMemberId member, ServiceQuote quote)
    {
        PartyMember trainee = _party.Member(member);
        string points = _progression?.LastTraining is { IsTrained: true } step && step.Member == member
            ? string.Create(CultureInfo.InvariantCulture, $" and {step.Growth.SkillPoints} skill point(s)")
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{trainee.Profile.Name} trains to level {trainee.Progression.Level} for {quote.Charge.Coins} coin(s){points}.");
    }

    /// <summary>Records a result as the last thing that happened and hands it back.</summary>
    private ServiceResult Record(ServiceResult result)
    {
        _last = result;
        return result;
    }

    /// <summary>States a refusal, with the purse as the refusal left it.</summary>
    private ServiceResult Refuse(ServiceCommandKind kind, string code, string message) =>
        Record(ServiceResult.Refused(Word(kind), code, message, Coins));

    /// <summary>
    /// Why the counter is not serving now, or null when it is: content's hours read against the one clock.
    /// </summary>
    /// <remarks>
    /// A service that states hours and a session with no clock cannot be known to be open, so that is
    /// refused by name rather than assumed open — the same honesty with which a requirement that needs a
    /// clock is unmet when a ruleset composed none.
    /// </remarks>
    private PartyRefusal? Closed(ServiceDefinition service)
    {
        if (service.Hours is not { } hours) return null;
        if (_clock is not { } clock)
        {
            return new PartyRefusal(
                "service-no-clock",
                $"{service.Describe()} keeps {hours} and this session keeps no clock, so whether it is open cannot be known.");
        }

        if (hours.IsOpenAt(clock.Now)) return null;
        return new PartyRefusal(
            "service-closed",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{service.Describe()} is shut: it keeps {hours} and the clock stands at {clock.Now.Hour:00}:00."));
    }

    /// <summary>The word for an operation or command, as the projection and the messages spell it.</summary>
    private static string Word(ServiceCommandKind kind) => kind switch
    {
        ServiceCommandKind.Buy => "buy",
        ServiceCommandKind.Sell => "sell",
        ServiceCommandKind.Identify => "identify",
        ServiceCommandKind.Repair => "repair",
        ServiceCommandKind.Teach => "teach",
        ServiceCommandKind.Cure => "cure",
        ServiceCommandKind.Train => "train",
        ServiceCommandKind.Provision => "provision",
        ServiceCommandKind.Stay => "stay",
        ServiceCommandKind.Deposit => "deposit",
        ServiceCommandKind.Withdraw => "withdraw",
        ServiceCommandKind.Fare => "fare",
        _ => "leave",
    };

    /// <summary>The word for an operation, as the projection and the messages spell it.</summary>
    private static string Word(ServiceOperationKind operation) => operation switch
    {
        ServiceOperationKind.Buy => "buy",
        ServiceOperationKind.Sell => "sell",
        ServiceOperationKind.Identify => "identify",
        ServiceOperationKind.Repair => "repair",
        ServiceOperationKind.Teach => "teach",
        ServiceOperationKind.Cure => "cure",
        ServiceOperationKind.Train => "train",
        ServiceOperationKind.Provision => "provision",
        ServiceOperationKind.Stay => "stay",
        ServiceOperationKind.Deposit => "deposit",
        ServiceOperationKind.Withdraw => "withdraw",
        _ => "fare",
    };

    /// <summary>The operation a command is taken as, which is the offer kind it names.</summary>
    private static ServiceOperationKind OperationOf(ServiceOfferKind kind) => kind switch
    {
        ServiceOfferKind.Cure => ServiceOperationKind.Cure,
        ServiceOfferKind.Training => ServiceOperationKind.Train,
        ServiceOfferKind.Provision => ServiceOperationKind.Provision,
        ServiceOfferKind.Stay => ServiceOperationKind.Stay,
        ServiceOfferKind.Holding => ServiceOperationKind.Deposit,
        ServiceOfferKind.Fare => ServiceOperationKind.Fare,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "A notice is read rather than taken, so it is not the subject of an operation and has no price."),
    };

    /// <summary>The offer kind a command asks for.</summary>
    private static ServiceOfferKind OfferKindOf(ServiceCommandKind kind) => kind switch
    {
        ServiceCommandKind.Cure => ServiceOfferKind.Cure,
        ServiceCommandKind.Train => ServiceOfferKind.Training,
        ServiceCommandKind.Provision => ServiceOfferKind.Provision,
        ServiceCommandKind.Stay => ServiceOfferKind.Stay,
        ServiceCommandKind.Deposit or ServiceCommandKind.Withdraw => ServiceOfferKind.Holding,
        _ => ServiceOfferKind.Fare,
    };

    /// <summary>The name a counter keeps the party's coins under, which is its offer's subject or its name.</summary>
    private static string Holding(ServiceSubject subject) =>
        subject.Offer!.Subject.Length > 0 ? subject.Offer.Subject : subject.Offer.Name;
}
