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
public sealed class PartyServices : IGameTimeObserver, IDeadlineOwner
{
    private readonly IServiceRule _rule;
    private readonly PartyEntity _party;
    private readonly PartyResourceLedger? _accounts;
    private readonly GameClock? _clock;
    private readonly PartyRest? _rest;
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
    /// <param name="rest">
    /// The rest mechanism a rented room hands its night to, so a night at an inn is the same sleep a rest is.
    /// Without one a room is refused by name rather than slept in some second way.
    /// </param>
    /// <exception cref="ArgumentNullException">The rule or the party is missing.</exception>
    public PartyServices(
        IServiceRule rule,
        PartyEntity party,
        PartyResourceLedger? accounts = null,
        GameClock? clock = null,
        PartyProgression? progression = null,
        PartyRest? rest = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _accounts = accounts;
        _clock = clock;
        _progression = progression;
        _rest = rest;
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

    /// <summary>
    /// Ends an open visit because the party it was serving is no longer standing there, reporting nothing.
    /// </summary>
    /// <remarks>
    /// This is deliberately not <see cref="Close"/>: nothing was asked for, so no answer belongs to the
    /// player and the last result stays whatever command carried the party away — a passage bought at a
    /// counter and boarded in the same update is the fare, not a leave nobody pressed. A visit that was not
    /// open ends as quietly as one that was, because the fact is where the party stands rather than what the
    /// counter did.
    /// </remarks>
    public void Abandon()
    {
        _visit = null;
        _current = null;
    }

    /// <summary>Runs one command at the counter the party stands at, and reports what it did.</summary>
    /// <remarks>
    /// <para>
    /// <b>Everything is judged before anything moves.</b> The command is resolved into what it acts on, the
    /// counter's hours and eligibility are read, the price is quoted, and the owner the operation reaches —
    /// progression for a level, rest for a room, the counter's own holding for a withdrawal — is asked whether
    /// the change can happen. Only then is the charge settled, and the change applied cannot be refused, so a
    /// party never pays for something it did not receive and nothing is handed back.
    /// </para>
    /// <para>
    /// Which operation a command is, is one entry of <see cref="Operations"/>: its word, the subject it takes,
    /// what it judges, what it changes, and how it reads.
    /// </para>
    /// </remarks>
    /// <param name="command">What the screen asked for.</param>
    /// <returns>What happened, or why nothing did.</returns>
    public ServiceResult Transact(ServiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Kind == ServiceOperationKind.Leave) return Close();
        if (_visit is not { } visit)
        {
            return Refuse(command.Kind, "service-not-open", "The party is not standing at a service counter.");
        }

        ServiceDefinition service = visit.Service;
        ServiceOperationKind kind = command.Kind;
        Operation operation = Operations[kind];
        if (!service.Offers(kind))
        {
            return Refuse(kind, "service-operation-unavailable", $"{service.Describe()} does not offer to {operation.Word}.");
        }

        // The hours are re-judged on every command, not only when the party walked in: a shop that closed
        // while the party browsed stops serving, and says so, rather than selling on because a screen is
        // open. Locking the door outside hours belongs to the schedule owner that closes doors.
        if (Closed(service) is { } shut) return Refuse(kind, shut.Code, shut.Message);

        if (Resolve(visit, command, operation, out ServiceSubject subject, out PartyMemberId member) is { } missing) return missing;
        if (operation.Admit?.Invoke(subject) is { } unfit) return Refuse(kind, unfit.Code, unfit.Message);

        ServiceEligibility eligibility = _rule.Judge(new ServiceEligibilityRequest(service, kind, subject, member, _party, _clock));
        if (eligibility.Refusal is { } refused) return Refuse(kind, refused.Code, refused.Message);

        ServiceQuote quote = _rule.Quote(new ServiceQuoteRequest(service, kind, subject, member, _party, _clock));
        if ((!quote.Charge.IsFree || !quote.Payment.IsFree || operation.NeedsAccounts) && _accounts is null)
        {
            return Refuse(
                kind,
                "service-no-accounts",
                $"{service.Describe()} settles in coin and this session holds no party accounts to settle against; the party's purse is the only purse a service may touch.");
        }

        Transaction transaction = new(visit, kind, subject, member, quote);
        if (operation.Judge?.Invoke(this, transaction) is { } blocked) return Refuse(kind, blocked.Code, blocked.Message);

        if (!quote.Charge.IsFree)
        {
            ResourceSettlement settlement = _accounts!.Settle(quote.Charge);
            if (!settlement.Admitted) return Refuse(kind, settlement.Refusal!.Code, settlement.Refusal.Message);
        }

        operation.Apply(this, transaction);
        if (!quote.Payment.IsFree) _accounts!.Credit(quote.Payment);

        // The result names what the command acted on, so a caller that owns what comes next — the world, which
        // honours a passage the counter has just sold — reads it from the transaction rather than from the
        // screen's own copy of the request.
        return Record(ServiceResult.Applied(operation.Word, operation.Describe(this, transaction), subject.Target, quote.Charge.Coins, quote.Payment.Coins, Coins));
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
            lessons.Add(new ServiceLessonOffer(lesson.Kind, lesson.Subject, lesson.Label, lesson.Amount, price, lesson.Tier));
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

    /// <inheritdoc />
    /// <remarks>
    /// A shelf's restock is rebuilt on load: a counter stocks its shelf from the ruleset's own stock the first
    /// time it is visited, and its refresh runs from that visit. What the party sold to a counter and the
    /// counts its purchases drew down are not carried — a resumed session finds each shelf as a restock would
    /// leave it — which is a stated loss rather than a refused save.
    /// </remarks>
    public bool RebuildsOnLoad(DeadlineId deadline) => Holds(deadline);

    /// <inheritdoc />
    public string Describe(DeadlineId deadline)
    {
        foreach (ServiceShelf shelf in _shelves.Values)
        {
            if (shelf.Refresh == deadline) return $"the restock of {shelf.Service.Describe()}";
        }

        return $"deadline {deadline}";
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
    /// Every operation this mechanism carries out, one entry per kind: adding a kind of operation is adding one
    /// entry here.
    /// </summary>
    /// <remarks>
    /// An entry states the operation's word, which shape of subject it acts on (a shelf's lot, an item the party
    /// holds, one of the counter's offers of a kind, or a lesson), what it asks of that subject before anything
    /// else is judged, what it asks of the owner it reaches before the charge is settled, what it changes —
    /// which cannot be refused, because everything that could refuse it was asked first — and how it reads.
    /// </remarks>
    private static readonly IReadOnlyDictionary<ServiceOperationKind, Operation> Operations = new Dictionary<ServiceOperationKind, Operation>
    {
        [ServiceOperationKind.Buy] = new(
            "buy",
            SubjectShape.Lot,
            Apply: static (services, t) => services.ApplyBuy(t),
            Describe: static (_, t) => $"The party buys {t.Subject.Count} × {t.Subject.Lot!.Label} for {t.Quote.Charge.Coins} coin(s), and {t.Subject.Lot.Count} are left."),
        [ServiceOperationKind.Sell] = new(
            "sell",
            SubjectShape.Item,
            Admit: static subject => subject.Item!.Custody.IsInSharedInventory
                ? null
                // A member's figure is not the party's stock: selling it would take a worn item off a character.
                : new PartyRefusal("service-item-worn", $"{subject.Item.Definition} is worn by a member, and a shop buys what lies in the party's pack."),
            Apply: static (services, t) => services.ApplySell(t),
            Describe: static (_, t) => $"The party sells {t.Subject.Item!.StackCount} × {t.Subject.Item.Definition} for {t.Quote.Payment.Coins} coin(s), and the counter will sell it back."),
        [ServiceOperationKind.Identify] = new(
            "identify",
            SubjectShape.Item,
            // Charging to identify what is identified would be taking coin for a change that never happened.
            Admit: static subject => subject.Item!.State.IsIdentified
                ? new PartyRefusal("service-already-identified", $"{subject.Item.Definition} is identified already, so there is nothing to learn about it.")
                : null,
            Apply: static (_, t) => t.Subject.Item!.Identify(),
            Describe: static (_, t) => $"The party pays {t.Quote.Charge.Coins} coin(s) to identify {t.Subject.Item!.Definition}."),
        [ServiceOperationKind.Repair] = new(
            "repair",
            SubjectShape.Item,
            Admit: static subject => subject.Item!.State.Damage == 0
                ? new PartyRefusal("service-not-damaged", $"{subject.Item.Definition} is sound, so there is nothing to repair.")
                : null,
            Apply: static (_, t) => t.Subject.Item!.Repair(t.Subject.Item.State.Damage),
            Describe: static (_, t) => $"The party pays {t.Quote.Charge.Coins} coin(s) to repair {t.Subject.Item!.Definition}."),
        [ServiceOperationKind.Teach] = new(
            "teach",
            SubjectShape.Lesson,
            Judge: static (services, t) => t.Subject.Lesson!.Kind == ServiceLessonKind.Skill && services._progression is null
                ? new PartyRefusal("service-no-progression", $"{t.Visit.Service.Describe()} teaches skills and this session holds no progression owner to raise one.")
                : null,
            Apply: static (services, t) => services.ApplyTeach(t),
            Describe: static (services, t) => t.Subject.Lesson!.Kind switch
            {
                ServiceLessonKind.Skill => $"{services._party.Member(t.Member).Profile.Name} is taught {t.Subject.Lesson.Label} to level {t.Subject.Lesson.Amount} for {t.Quote.Charge.Coins} coin(s).",
                ServiceLessonKind.Spell => $"{services._party.Member(t.Member).Profile.Name} learns {t.Subject.Lesson.Label} from the book for {t.Quote.Charge.Coins} coin(s), and the book is used up.",
                _ => $"The party pays {t.Quote.Charge.Coins} coin(s) for {t.Subject.Lesson.Label}.",
            }),
        [ServiceOperationKind.Cure] = new(
            "cure",
            SubjectShape.Offer,
            ServiceOfferKind.Cure,
            ForMember: true,
            Apply: static (services, t) => services.ApplyCure(t),
            Describe: static (services, t) => $"The party pays {t.Quote.Charge.Coins} coin(s) and {services._party.Member(t.Member).Profile.Name} is healed of {string.Join(", ", t.Subject.Offer!.Conditions)}."),
        [ServiceOperationKind.Train] = new(
            "train",
            SubjectShape.Offer,
            ServiceOfferKind.Training,
            ForMember: true,
            // A training step is one level, and the level is the progression owner's to grant; it is asked before
            // the fee is taken, so no party pays for a level it could not have.
            Judge: static (services, t) => services._progression is not { } progression
                ? new PartyRefusal("service-no-progression", $"{t.Visit.Service.Describe()} trains by the level and this session holds no progression owner to grant one.")
                : progression.JudgeTraining(t.Member, services.Terms(t)),
            Apply: static (services, t) => Require(services._progression!.Train(t.Member, services.Terms(t)).Refusal, "training"),
            Describe: static (services, t) => services.TrainMessage(t.Member, t.Quote)),
        [ServiceOperationKind.Provision] = new(
            "provision",
            SubjectShape.Offer,
            ServiceOfferKind.Provision,
            NeedsAccounts: true,
            // Provisions are the party's own account: one purchase fills what the offer states, credited through
            // the party's one ledger as every other food is.
            Apply: static (services, t) => services._accounts!.Credit(PartyCost.OfFood(new Provisions(
                checked(t.Subject.Offer!.Amount * t.Subject.Count),
                services._party.Food.Unit))),
            Describe: static (_, t) => $"The party pays {t.Quote.Charge.Coins} coin(s) for {t.Subject.Offer!.Amount * t.Subject.Count} provisions."),
        [ServiceOperationKind.Stay] = new(
            "stay",
            SubjectShape.Offer,
            ServiceOfferKind.Stay,
            // A room buys a night, and the night is the rest mechanism's; whether one could be slept is asked of it
            // before the price is taken.
            Judge: static (services, t) => services._rest is not { } rest
                ? new PartyRefusal("service-no-rest", $"{t.Visit.Service.Describe()} rents rooms by the night and this session composes no rest, so no night could be slept.")
                : rest.JudgeRoom(),
            Apply: static (services, t) => services.ApplyStay(t),
            Describe: static (_, t) => $"The party pays {t.Quote.Charge.Coins} coin(s) for {t.Subject.Offer!.Name} and rests for {t.Subject.Offer.Amount} hour(s)."),
        [ServiceOperationKind.Deposit] = new(
            "deposit",
            SubjectShape.Offer,
            ServiceOfferKind.Holding,
            Apply: static (services, t) => services._party.Holdings.Hold(Holding(t.Subject), services._party.Holdings.BalanceOf(Holding(t.Subject)) + t.Subject.Count),
            Describe: static (services, t) => $"The party leaves {t.Subject.Count} coin(s) with {t.Visit.Service.Name} and it now holds {services._party.Holdings.BalanceOf(Holding(t.Subject))}."),
        [ServiceOperationKind.Withdraw] = new(
            "withdraw",
            SubjectShape.Offer,
            ServiceOfferKind.Holding,
            Judge: static (services, t) => services._party.Holdings.BalanceOf(Holding(t.Subject)) is var held && held < t.Subject.Count
                ? new PartyRefusal("service-holding-short", $"{t.Visit.Service.Describe()} holds {held} coin(s) for the party and the party asked for {t.Subject.Count}.")
                : null,
            Apply: static (services, t) => services._party.Holdings.Hold(Holding(t.Subject), services._party.Holdings.BalanceOf(Holding(t.Subject)) - t.Subject.Count),
            Describe: static (services, t) => $"The party takes {t.Subject.Count} coin(s) back from {t.Visit.Service.Name} and it holds {services._party.Holdings.BalanceOf(Holding(t.Subject))}."),
        [ServiceOperationKind.Fare] = new(
            "fare",
            SubjectShape.Offer,
            ServiceOfferKind.Fare,
            // The days the journey takes are the offer's own amount, which the travel policy reads to price the
            // boarding, so the counter that sold the ticket and the road that takes it agree about the journey.
            Apply: static (services, t) => services._party.Passages.Hold(new PlaceId(t.Subject.Offer!.Subject), t.Subject.Offer.Amount < 1 ? 1 : t.Subject.Offer.Amount),
            Describe: static (_, t) => $"The party pays {t.Quote.Charge.Coins} coin(s) for a passage to {t.Subject.Offer!.Subject}, which takes {t.Subject.Offer.Amount} day(s)."),
        [ServiceOperationKind.Leave] = new(
            "leave",
            SubjectShape.None,
            Apply: static (_, _) => throw new InvalidOperationException("Leaving ends the visit; it is not carried out as a transaction."),
            Describe: static (_, _) => string.Empty),
    };

    /// <summary>What an operation of a table entry acts on.</summary>
    private enum SubjectShape
    {
        /// <summary>Nothing: leaving names no subject.</summary>
        None,

        /// <summary>A line on the counter's shelves.</summary>
        Lot,

        /// <summary>An item the party holds.</summary>
        Item,

        /// <summary>One of the counter's offers of the operation's own kind.</summary>
        Offer,

        /// <summary>One of the counter's lessons.</summary>
        Lesson,
    }

    /// <summary>One operation of this mechanism, as its table entry states it.</summary>
    /// <param name="Word">What the projection and the messages call it.</param>
    /// <param name="Shape">What it acts on.</param>
    /// <param name="Offer">The kind of offer it takes, when it takes one.</param>
    /// <param name="ForMember">Whether it acts on one member, which the command names.</param>
    /// <param name="NeedsAccounts">Whether it moves the party's accounts even when nothing is charged.</param>
    /// <param name="Admit">What it asks of the subject before anything else is judged.</param>
    /// <param name="Judge">What it asks of the owner it reaches before the charge is settled.</param>
    /// <param name="Apply">What it changes, which nothing can refuse by then.</param>
    /// <param name="Describe">How it reads once done.</param>
    private sealed record Operation(
        string Word,
        SubjectShape Shape,
        ServiceOfferKind? Offer = null,
        bool ForMember = false,
        bool NeedsAccounts = false,
        Func<ServiceSubject, PartyRefusal?>? Admit = null,
        Func<PartyServices, Transaction, PartyRefusal?>? Judge = null,
        Action<PartyServices, Transaction> Apply = null!,
        Func<PartyServices, Transaction, string> Describe = null!);

    /// <summary>One command judged and priced, which is what an operation applies and describes.</summary>
    private sealed record Transaction(ServiceVisit Visit, ServiceOperationKind Kind, ServiceSubject Subject, PartyMemberId Member, ServiceQuote Quote);

    /// <summary>
    /// Resolves what a command names into the thing the operation acts on, or the refusal that says it is not
    /// there — which separates "the shelves are out of it" from "the party holds no such item" from "the counter
    /// teaches no such lesson", three answers a player acts on differently.
    /// </summary>
    private ServiceResult? Resolve(ServiceVisit visit, ServiceCommand command, Operation operation, out ServiceSubject subject, out PartyMemberId member)
    {
        subject = null!;
        member = default;
        ServiceDefinition service = visit.Service;
        ServiceOperationKind kind = command.Kind;

        switch (operation.Shape)
        {
            case SubjectShape.Lot:
            {
                if (visit.Shelf.Lot(new ServiceLotId(command.Target)) is not { } lot)
                {
                    return Refuse(kind, "service-no-such-lot", $"{service.Describe()} has no line '{command.Target}' on its shelves.");
                }

                if (lot.IsEmpty) return Refuse(kind, "service-out-of-stock", $"{service.Describe()} has sold out of {lot.Label}.");

                int count = lot.IsSale ? lot.Count : command.Count;
                if (count < 1)
                {
                    return Refuse(kind, "service-count-invalid", $"The party asked for {command.Count} × {lot.Label}, and a purchase is of at least one.");
                }

                if (count > lot.Count)
                {
                    return Refuse(kind, "service-not-enough-stock", $"{service.Describe()} holds {lot.Count} × {lot.Label} and the party asked for {count}.");
                }

                subject = ServiceSubject.OfLot(lot, count);
                return null;
            }

            case SubjectShape.Item:
            {
                if (!ulong.TryParse(command.Target, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) || value == 0
                    || _party.FindItem(new ItemInstanceId(value)) is not { } item)
                {
                    return Refuse(kind, "service-no-such-item", $"The party holds no item '{command.Target}', so there is nothing to {operation.Word}.");
                }

                subject = ServiceSubject.OfItem(item);
                return null;
            }

            case SubjectShape.Offer:
            {
                ServiceOfferKind want = operation.Offer!.Value;
                List<ServiceOffer> offers = [.. _rule.Offers(new ServiceOfferRequest(service, _party, _clock)).Where(offer => offer.Kind == want)];
                if (offers.Count == 0) return Refuse(kind, "service-no-such-offer", $"{service.Describe()} offers no {operation.Word}.");

                // A command that names nothing takes the counter's one offer of that kind — a hall has one training
                // step, a bank one account, a tavern one room — and one that names something takes the offer that
                // subject identifies. Several offers and nothing named is refused rather than guessed at, because
                // taking the wrong room or the wrong passage would charge the party for a journey it did not ask for.
                ServiceOffer? chosen;
                if (command.Target.Length == 0)
                {
                    if (offers.Count > 1)
                    {
                        return Refuse(kind, "service-offer-ambiguous", $"{service.Describe()} offers {offers.Count} things to {operation.Word} and the command named none of them.");
                    }

                    chosen = offers[0];
                }
                else
                {
                    chosen = offers.FirstOrDefault(candidate => string.Equals(candidate.Target, command.Target, StringComparison.Ordinal));
                }

                if (chosen is null)
                {
                    return Refuse(kind, "service-no-such-offer", $"{service.Describe()} offers nothing called '{command.Target}' to {operation.Word}.");
                }

                if (operation.ForMember)
                {
                    if (command.Member < 0 || command.Member >= _party.Members.Count)
                    {
                        return Refuse(kind, "service-no-such-member", $"The party has no member {command.Member + 1}, so there is nobody for {chosen.Name} to act on.");
                    }

                    member = _party.Members[command.Member].Id;
                }

                // What a deposit or a withdrawal moves is coin, which the command counts; every other offer acts on
                // as many of itself as the command asks, at least one.
                int count = want == ServiceOfferKind.Holding ? command.Count : Math.Max(1, command.Count);
                if (count < 1)
                {
                    return Refuse(kind, "service-count-invalid", $"The party asked for {count} of {chosen.Name}, and an operation acts on at least one.");
                }

                subject = ServiceSubject.OfOffer(chosen, count);
                return null;
            }

            default:
            {
                // A counter can teach one skill at more than one rung, so a lesson is resolved by its subject and its
                // rung together; a command that names no rung means the first.
                ServiceLesson? lesson = _rule.Lessons(new ServiceLessonRequest(service, _party, _clock))
                    .FirstOrDefault(candidate => string.Equals(candidate.Subject, command.Target, StringComparison.Ordinal) && candidate.Tier == command.Tier);
                if (lesson is null) return Refuse(kind, "service-no-such-lesson", $"{service.Describe()} teaches no lesson called '{command.Target}'.");
                if (command.Member < 0 || command.Member >= _party.Members.Count)
                {
                    return Refuse(kind, "service-no-such-member", $"The party has no member {command.Member + 1}, so a lesson has nobody to go to.");
                }

                member = _party.Members[command.Member].Id;
                subject = ServiceSubject.OfLesson(lesson);
                return null;
            }
        }
    }

    /// <summary>Puts a bought line in the party's pack and takes it off the shelf.</summary>
    private void ApplyBuy(Transaction t)
    {
        ServiceStockLot lot = t.Subject.Lot!;
        ItemInstance instance = lot.IsSale ? lot.Instance! : _party.CreateItem(lot.Definition, t.Subject.Count);
        Require(_party.AcquireItem(instance).Refusal, "a purchase");
        t.Visit.Shelf.Take(lot, t.Subject.Count);
    }

    /// <summary>Takes a sold item out of the party and onto the shelf, priced back from what the shop paid.</summary>
    private void ApplySell(Transaction t)
    {
        ItemInstance released = _party.ReleaseItem(t.Subject.Item!.Id)
            ?? throw new InvalidOperationException($"Item {t.Subject.Item.Id} was resolved and is no longer the party's.");
        t.Visit.Shelf.Accept(released, t.Quote.Value > 0 ? t.Quote.Value : t.Subject.Value);
    }

    /// <summary>
    /// Ends what a cure's own list names and, when the offer restores, fills the patient's pools: a temple's
    /// healing is one act on the member it was bought for.
    /// </summary>
    private void ApplyCure(Transaction t)
    {
        ServiceOffer cure = t.Subject.Offer!;
        PartyMember patient = _party.Member(t.Member);
        foreach (ConditionId condition in cure.Conditions) patient.Conditions.Clear(condition);
        if (cure.Amount > 0) patient.Resources.RestoreAll();
    }

    /// <summary>Sleeps the night a room gives, through the rest mechanism, which already judged it could.</summary>
    private void ApplyStay(Transaction t)
    {
        ServiceOffer room = t.Subject.Offer!;
        RestResult night = _rest!.SleepInRoom(GameDuration.FromHours(room.Amount < 1 ? 1 : room.Amount), room.Conditions);
        if (!night.IsApplied) Require(new PartyRefusal(night.Code, night.Message), "a room");
    }

    /// <summary>
    /// Teaches what a lesson states: a skill through the progression owner, a spell into the member's own
    /// spellbook (the book is consumed by the learning), or a membership granted to the band.
    /// </summary>
    private void ApplyTeach(Transaction t)
    {
        ServiceLesson lesson = t.Subject.Lesson!;
        switch (lesson.Kind)
        {
            case ServiceLessonKind.Skill:
                _progression!.Teach(t.Member, new SkillId(lesson.Subject), new SkillTier(lesson.Tier), lesson.Amount);
                break;
            case ServiceLessonKind.Spell:
                _party.Member(t.Member).Spells.Learn(new SpellId(lesson.Subject));
                break;
            default:
                _party.Memberships.Grant(lesson.Subject);
                break;
        }
    }

    /// <summary>The terms a counter trains under: its name, its fee, and the ceiling its offer states.</summary>
    private ProgressionTrainingTerms Terms(Transaction t) => new(t.Visit.Service.Name, t.Quote.Charge.Coins, t.Subject.Offer!.Limit);

    /// <summary>
    /// Fails loudly when an owner refuses a change it was asked about before the charge: that is a broken
    /// promise between the judgement and the change, not a refusal a player could meet.
    /// </summary>
    private static void Require(PartyRefusal? refusal, string what)
    {
        if (refusal is not null)
        {
            throw new InvalidOperationException($"The owner refused {what} it had already judged possible: {refusal.Code}: {refusal.Message}");
        }
    }

    /// <summary>Prices one operation through the ruleset's price rule.</summary>
    private ServiceQuote Price(ServiceDefinition service, ServiceOperationKind operation, ServiceSubject subject, PartyMemberId member) =>
        _rule.Quote(new ServiceQuoteRequest(service, operation, subject, member, _party, _clock));

    /// <summary>
    /// What a completed training step reports: the level the member now stands at, what it cost, and the skill
    /// points the level granted, read from the member and the owner rather than from the offer's ceiling.
    /// </summary>
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
    private ServiceResult Refuse(ServiceOperationKind kind, string code, string message) =>
        Record(ServiceResult.Refused(Word(kind), code, message, Coins));

    /// <summary>
    /// Why the counter is not serving now, or null when it is: content's hours read against the one clock. A
    /// service that states hours and a session with no clock cannot be known to be open, so that is refused by
    /// name rather than assumed open.
    /// </summary>
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

    /// <summary>The word for an operation, as the projection and the messages spell it.</summary>
    private static string Word(ServiceOperationKind kind) => Operations[kind].Word;

    /// <summary>
    /// The operation an offer is taken as: the first kind, in the enumeration's own order, whose entry takes it —
    /// so a counter's holding is priced as a deposit.
    /// </summary>
    private static ServiceOperationKind OperationOf(ServiceOfferKind kind) =>
        Operations.Where(entry => entry.Value.Offer == kind).Min(entry => entry.Key);

    /// <summary>The name a counter keeps the party's coins under, which is its offer's subject or its name.</summary>
    private static string Holding(ServiceSubject subject) =>
        subject.Offer!.Subject.Length > 0 ? subject.Offer.Subject : subject.Offer.Name;
}
