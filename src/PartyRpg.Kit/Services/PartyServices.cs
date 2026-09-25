using System.Globalization;
using PartyRpg.Kit.Party;
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
    /// <exception cref="ArgumentNullException">The rule or the party is missing.</exception>
    public PartyServices(IServiceRule rule, PartyEntity party, PartyResourceLedger? accounts = null, GameClock? clock = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _accounts = accounts;
        _clock = clock;
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

        return new ServiceBrowse(service, operations, memberships, stock, lessons, sales, members);
    }

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
            ServiceOperationKind.Teach when subject.Lesson!.Kind == ServiceLessonKind.Skill =>
                $"{_party.Member(member).Profile.Name} is taught {subject.Lesson.Label} to level {subject.Lesson.Amount} for {quote.Charge.Coins} coin(s).",
            ServiceOperationKind.Teach =>
                $"The party pays {quote.Charge.Coins} coin(s) for {subject.Lesson!.Label}.",
            _ => $"The party's transaction at {visit.Service.Name} is done.",
        };

    /// <summary>Prices one operation through the ruleset's price rule.</summary>
    private ServiceQuote Price(ServiceDefinition service, ServiceOperationKind operation, ServiceSubject subject, PartyMemberId member) =>
        _rule.Quote(new ServiceQuoteRequest(service, operation, subject, member, _party, _clock));

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
        _ => "leave",
    };

    /// <summary>The word for an operation, as the projection and the messages spell it.</summary>
    private static string Word(ServiceOperationKind operation) => operation switch
    {
        ServiceOperationKind.Buy => "buy",
        ServiceOperationKind.Sell => "sell",
        ServiceOperationKind.Identify => "identify",
        ServiceOperationKind.Repair => "repair",
        _ => "teach",
    };
}
