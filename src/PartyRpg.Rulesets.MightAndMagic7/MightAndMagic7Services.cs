using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's service policy: which placement keeps a counter, what it sells and teaches, who it serves,
/// and what it charges.
/// </summary>
/// <remarks>
/// <para>
/// <b>The kinds come from content; the rules come from here.</b> The shipped building table recognizes
/// twenty-one kinds of service across a hundred and seventy-two buildings — weapon, armor, and magic shops,
/// alchemists, temples, taverns, banks, training halls, stables, docks, town halls, and the ten guilds — and
/// every one of them is served by the one kit mechanism over a definition this reads. Nothing here is a
/// class per kind: a kind is a word in the definition, and what differs between kinds is which operations
/// the definition offers, what its shelves hold, what it teaches, when it opens, and what access it
/// requires.
/// </para>
/// <para>
/// <b>The prices are the donor's arithmetic with content's numbers.</b> Buying, selling, identifying,
/// repairing, and teaching are priced the way the original prices them (OpenEnroth
/// <c>src/Engine/PriceCalculator.cpp</c>), with the multipliers the building table carries: the shop's price
/// multiplier and its skill multiplier, the party's best Merchant skill, and the party's reputation. The
/// kit owns how a value and a multiplier become coins; this owns which numbers apply.
/// </para>
/// <para>
/// <b>Membership is party-carried state.</b> A guild's definition names the party-wide effect that
/// membership is and its lessons sell that same effect, so "the party is a member" is one piece of state
/// the access check reads, the purchase writes, and a save already records — never a flag kept here.
/// </para>
/// <para>
/// <b>What content must state.</b> A service definition carries its id, its kind, its name, the operations
/// it offers, its shelves, its lessons, its hours, and its multipliers; a place's placement of kind
/// <c>service</c> names that id. Definitions and placements are read and judged once, where the session is
/// composed, so a counter that names no service, a shelf line with no item, or a lesson with no subject
/// fails by name at load rather than becoming a shop that silently sells nothing.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Services : IServiceRule
{
    /// <summary>The definition kind a service entry uses.</summary>
    internal const string DefinitionKind = "service";

    /// <summary>The placement kind a place uses for the counter standing in it.</summary>
    internal const string PlacementKind = "service";

    /// <summary>The definition kind item values are read from, which is the imported item table.</summary>
    internal const string ItemDefinitionKind = "item";

    /// <summary>
    /// The skill whose level and tier improve what the party pays and is paid, as the donor names it.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:97-113</c> <c>playerMerchant</c>: the merchant skill's
    /// multiplier by mastery is 1, 2, 3, or 5, a grand master always trades at the best rate, and the party's
    /// reputation adjusts the rest. The skill's own name is the shipped skill table's, which the ruleset
    /// reads as content rather than the kit knowing it.
    /// </remarks>
    internal static readonly SkillId MerchantSkill = new("Merchant");

    /// <summary>The multipliers the merchant skill's mastery applies, indexed by the skill's rung.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:99</c> —
    /// <c>GetMultiplierForSkillLevel(SKILL_MERCHANT, 1, 2, 3, 5)</c> — where the rungs are normal, expert,
    /// master, and grand master. A rung beyond the ladder keeps the last multiplier rather than inventing
    /// one, exactly as the donor's own table lookup would.
    /// </remarks>
    private static readonly int[] MerchantMultipliers = [0, 1, 2, 3, 5];

    /// <summary>The rung at which a merchant always trades at the best rate.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/PriceCalculator.cpp:100-102</c>: a grand master merchant is 100.</remarks>
    private const int GrandMasterRung = 4;

    /// <summary>
    /// The base price of identifying one item, before the shop's multiplier and the party's standing.
    /// </summary>
    /// <remarks>OpenEnroth <c>src/Engine/PriceCalculator.cpp:11-19</c> — <c>baseItemIdentifyPrice</c>: fifty.</remarks>
    private const int IdentificationBase = 50;

    private readonly Dictionary<ServiceId, ServiceDefinition> _services;
    private readonly Dictionary<ItemDefinitionId, ItemFacts> _items;

    private MightAndMagic7Services(Dictionary<ServiceId, ServiceDefinition> services, Dictionary<ItemDefinitionId, ItemFacts> items)
    {
        _services = services;
        _items = items;
    }

    /// <summary>
    /// Reads every service the content declares, and judges it against the places that name one.
    /// </summary>
    /// <remarks>
    /// Everything is read and judged before anything is served, so a pack that names a counter nothing
    /// describes, prices a shelf line with no item, or states hours that make no window fails with every
    /// problem at once while the session is being composed rather than at the moment a player walks in.
    /// </remarks>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <returns>This game's service policy over that content, or null when no content was loaded.</returns>
    /// <exception cref="ContentValidationException">Content declares a service that cannot be served; every problem is named.</exception>
    internal static MightAndMagic7Services? Read(ContentCatalog? catalog)
    {
        if (catalog is null) return null;
        List<ContentValidationIssue> issues = [];
        Dictionary<ItemDefinitionId, ItemFacts> items = ReadItems(catalog);
        Dictionary<ServiceId, ServiceDefinition> services = [];

        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(DefinitionKind))
        {
            void Defect(string code, string message) =>
                issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

            if (entry.Id.Length == 0)
            {
                Defect("service-identity-missing", "a service entry declares no id, so no placement could name it.");
                continue;
            }

            ServiceId id = new(entry.Id);
            if (services.ContainsKey(id))
            {
                Defect("service-identity-reused", $"service '{id}' is declared more than once, so which counter a placement names would be ambiguous.");
                continue;
            }

            if (Definition(entry, id, items, Defect) is { } service) services[id] = service;
        }

        ValidatePlacements(catalog, services, issues);

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's services cannot be served: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Services(services, items);
    }

    /// <inheritdoc />
    public ServiceDefinition? Describe(ServiceTargetRequest request)
    {
        // A placement is a counter only when content says it is one and a definition describes it; every
        // other placement is not a service at all, which is how a person talked to for something else — a
        // quest giver, a master teacher — stays out of this mechanism.
        if (!string.Equals(request.Placement.Content.Kind, PlacementKind, StringComparison.Ordinal)) return null;
        return _services.GetValueOrDefault(new ServiceId(request.Placement.Content.Id));
    }

    /// <inheritdoc />
    /// <remarks>
    /// What content declares, unless a line names an item the item table prices: a line may state its own
    /// worth, and one that does not is worth what the imported table says the item is worth rather than
    /// nothing.
    /// </remarks>
    public IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<ServiceStockLine> lines = [];
        foreach (ServiceStockLine line in request.Service.Stock)
        {
            lines.Add(line.Value > 0 || !_items.TryGetValue(line.Definition, out ItemFacts facts)
                ? line
                : line with { Value = facts.Value });
        }

        return lines;
    }

    /// <inheritdoc />
    public IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Service.Lessons;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The membership a service names, as the party's own party-wide effects carry it. The word shown is the
    /// lesson's own name where one sells it, so a guild reads as what a player bought rather than as an
    /// internal effect id.
    /// </remarks>
    public IReadOnlyList<string> Access(ServiceAccessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Service.Membership.Length == 0) return [];
        if (!request.Party.Effects.Has(new EffectId(request.Service.Membership))) return [];
        foreach (ServiceLesson lesson in request.Service.Lessons)
        {
            if (lesson.Kind == ServiceLessonKind.Effect && string.Equals(lesson.Subject, request.Service.Membership, StringComparison.Ordinal))
            {
                return [lesson.Label];
            }
        }

        return [request.Service.Membership];
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// What the mechanism cannot know and this game decides: whether the party carries the access the
    /// counter requires, and whether the thing being asked for is already the party's. A guild serves its
    /// members only — except at the one lesson that sells the membership itself, because a counter that
    /// required the membership before it would sell it could never be joined.
    /// </para>
    /// <para>
    /// Whether the counter is open is deliberately not judged here: content's hours are read against the
    /// session's one clock by the mechanism, so a shop that closed while the party browsed is refused the
    /// same way in every kind of service.
    /// </para>
    /// </remarks>
    public ServiceEligibility Judge(ServiceEligibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ServiceDefinition service = request.Service;
        bool sellsTheMembership = request.Operation == ServiceOperationKind.Teach &&
            request.Subject.Lesson is { Kind: ServiceLessonKind.Effect } lesson &&
            string.Equals(lesson.Subject, service.Membership, StringComparison.Ordinal);

        if (service.Membership.Length > 0 &&
            !sellsTheMembership &&
            !request.Party.Effects.Has(new EffectId(service.Membership)))
        {
            return ServiceEligibility.Refused(
                "service-membership-required",
                $"{service.Describe()} serves members only, and the party carries no membership of {Membership(service)}.");
        }

        if (request.Operation != ServiceOperationKind.Teach || request.Subject.Lesson is not { } teaching) return ServiceEligibility.Allowed;

        if (teaching.Kind == ServiceLessonKind.Effect)
        {
            return request.Party.Effects.Has(new EffectId(teaching.Subject))
                ? ServiceEligibility.Refused("service-membership-held", $"The party already carries {teaching.Label}.")
                : ServiceEligibility.Allowed;
        }

        // A lesson a member has already taken is not sold twice: teaching a skill to a level the member
        // already stands at would be charging for a change that never happened. How far a class may take a
        // skill is the progression owner's ceiling, which does not exist yet, so nothing here pretends to
        // enforce one.
        PartyMember recipient = request.Party.Member(request.Member);
        SkillId skill = new(teaching.Subject);
        int level = recipient.Skills.LevelOf(skill);
        int tier = recipient.Skills.TierOf(skill).Value;
        return level >= teaching.Amount && tier >= teaching.Tier
            ? ServiceEligibility.Refused(
                "service-nothing-to-learn",
                $"{recipient.Profile.Name} already has {teaching.Label} at level {level} and rung {tier}, which is what the lesson teaches.")
            : ServiceEligibility.Allowed;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The original's prices, over the multipliers content carries. A purchase is never below what the item
    /// is worth, an identification and a repair never below a third of their base, and a shop never pays
    /// more for an item than it is worth or less than one coin (OpenEnroth
    /// <c>src/Engine/PriceCalculator.cpp:26-95</c>).
    /// </para>
    /// <para>
    /// The party's own standing enters through <see cref="MerchantValue"/>, which is the donor's own
    /// combination of the best Merchant skill in the party and the party's reputation. The party's best
    /// member answers rather than a chosen one for the same reason a locked door asks the band whether
    /// anybody can pick it: the party acts as one band.
    /// </para>
    /// </remarks>
    public ServiceQuote Quote(ServiceQuoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ServiceDefinition service = request.Service;
        int merchant = MerchantValue(request.Party);
        return request.Operation switch
        {
            ServiceOperationKind.Buy => Buy(service, request.Subject, merchant),
            ServiceOperationKind.Sell => Sell(service, request.Subject, merchant),
            ServiceOperationKind.Identify => Charge(merchant, ServicePricing.Coins(IdentificationBase, service.PriceMultiplier)),
            ServiceOperationKind.Repair => Charge(merchant, ServicePricing.Coins(ValueOf(request.Subject.Item), 1 / (6 - service.PriceMultiplier))),
            _ => Charge(merchant, ServicePricing.Coins(request.Subject.Value, service.SkillPriceMultiplier)),
        };
    }

    /// <summary>The value the party's best merchant and its reputation earn it, as the donor computes it.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:97-113</c>: a grand master always trades at the best
    /// rate; otherwise the skill's mastery multiplier times its level, minus the party's reputation, plus
    /// seven, capped at a hundred — and a party with no merchant skill at all trades on its reputation
    /// alone. The sign convention is the donor's: a higher reputation number reduces the merchant value and
    /// so raises what the party pays. Our parties begin at zero reputation, and what reputation means is
    /// content's to set.
    /// </remarks>
    internal static int MerchantValue(PartyEntity party)
    {
        int level = 0;
        SkillTier tier = SkillTier.None;
        foreach (PartyMember member in party.Members)
        {
            int candidate = member.Skills.LevelOf(MerchantSkill);
            if (candidate > level)
            {
                level = candidate;
                tier = member.Skills.TierOf(MerchantSkill);
            }
        }

        if (tier.Value >= GrandMasterRung) return 100;

        int bonus = MerchantMultipliers[Math.Min(tier.Value, MerchantMultipliers.Length - 1)] * level;
        int reputation = party.Reputation.Reputation;
        return bonus == 0 ? -reputation : Math.Min(bonus - reputation + 7, 100);
    }

    /// <summary>What buying the goods the party named costs, with the merchant's adjustment and the item's floor.</summary>
    /// <remarks>
    /// The donor prices one item and never below what it is worth (OpenEnroth
    /// <c>src/Engine/PriceCalculator.cpp:52-58</c>, <c>itemBuyingPriceForPlayer</c>), so a purchase of
    /// several is that price per item over the count the shelves are handing over.
    /// </remarks>
    private static ServiceQuote Buy(ServiceDefinition service, ServiceSubject subject, int merchant)
    {
        int unit = ServicePricing.AtLeast(
            ServicePricing.Percent(ServicePricing.Coins(subject.Value, service.PriceMultiplier), merchant),
            subject.Value);
        return ServiceQuote.Charging(unit * subject.Count, subject.Value);
    }

    /// <summary>What a shop pays for one of the party's items, with the shares and bounds the donor states.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:60-81</c> — <c>itemSellingPriceForPlayer</c>: the item's
    /// value divided by the shop's multiplier plus two, plus the merchant's share of the value, held between
    /// one coin and the item's own worth; an item with no worth, and a broken one, fetch a single coin.
    /// </remarks>
    private ServiceQuote Sell(ServiceDefinition service, ServiceSubject subject, int merchant)
    {
        ItemInstance? item = subject.Item;
        int value = ValueOf(item);
        int paid = ServicePricing.Coins(value, 1 / (service.PriceMultiplier + 2)) + ServicePricing.Share(value, merchant);
        paid = value > 0 ? ServicePricing.AtLeast(ServicePricing.AtMost(paid, value), 1) : 1;
        if (item is { State.Damage: > 0 }) paid = 1;
        return ServiceQuote.Paying(paid, value);
    }

    /// <summary>
    /// What a fee costs: a base the donor states, with the merchant's adjustment and the donor's floor of a
    /// third of the base.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:39-51</c> and <c>:116-127</c>: identification and
    /// learning take the merchant's discount but never fall below a third of their base price, and a price
    /// is at least one coin.
    /// </remarks>
    private static ServiceQuote Charge(int merchant, int basePrice)
    {
        int price = ServicePricing.Percent(basePrice, merchant);
        return ServiceQuote.Charging(ServicePricing.AtLeast(price, Math.Max(1, basePrice / 3)), basePrice);
    }

    /// <summary>What an item is worth, as the imported item table states it.</summary>
    private int ValueOf(ItemInstance? item) =>
        item is { } instance && _items.TryGetValue(instance.Definition, out ItemFacts facts) ? facts.Value : 0;

    /// <summary>How a service's membership reads to a person: its lesson's name, or its own effect id.</summary>
    private static string Membership(ServiceDefinition service)
    {
        foreach (ServiceLesson lesson in service.Lessons)
        {
            if (lesson.Kind == ServiceLessonKind.Effect && string.Equals(lesson.Subject, service.Membership, StringComparison.Ordinal))
            {
                return lesson.Label;
            }
        }

        return service.Membership;
    }

    /// <summary>Reads one service entry into the definition the mechanism serves.</summary>
    private static ServiceDefinition? Definition(
        ContentEntry entry,
        ServiceId id,
        Dictionary<ItemDefinitionId, ItemFacts> items,
        Action<string, string> defect)
    {
        string kind = entry.GetString("kind");
        string name = entry.GetString("name");
        if (kind.Length == 0 || name.Length == 0)
        {
            defect(
                "service-incomplete",
                $"service '{id}' does not state a kind and a name, which are the two things a counter is shown as.");
            return null;
        }

        List<ServiceOperationKind> operations = [];
        foreach (JsonElement element in entry.GetArray("operations"))
        {
            string word = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : string.Empty;
            if (Operation(word) is not { } operation)
            {
                defect(
                    "service-operation-unknown",
                    $"service '{id}' offers '{word}', which is not buy, sell, identify, repair, or teach in this build.");
                continue;
            }

            operations.Add(operation);
        }

        if (operations.Count == 0)
        {
            defect(
                "service-operations-missing",
                $"service '{id}' offers no operation, so there would be nothing a party could do at its counter.");
        }

        ServiceHours? hours = null;
        int? openHour = entry.GetInt32("openHour");
        int? closedHour = entry.GetInt32("closedHour");
        if (openHour is not null || closedHour is not null)
        {
            if (openHour is not { } open || closedHour is not { } closed)
            {
                defect("service-hours-incomplete", $"service '{id}' states one of its opening and closing hours and not the other.");
            }
            else
            {
                try
                {
                    hours = new ServiceHours(open, closed);
                }
                catch (ArgumentOutOfRangeException error)
                {
                    defect("service-hours-invalid", $"service '{id}' states hours it cannot keep: {error.Message}");
                }
            }
        }

        GameDuration? refresh = null;
        if (entry.GetInt32("stockIntervalDays") is { } days)
        {
            if (days <= 0)
            {
                defect("service-stock-interval-invalid", $"service '{id}' restocks every {days} day(s), which is not a schedule.");
            }
            else
            {
                refresh = GameDuration.FromHours((long)days * 24);
            }
        }

        double priceMultiplier = entry.GetDouble("priceMultiplier") ?? 1;
        double skillMultiplier = entry.GetDouble("skillPriceMultiplier") ?? priceMultiplier;
        if (!double.IsFinite(priceMultiplier) || priceMultiplier <= 0 || !double.IsFinite(skillMultiplier) || skillMultiplier <= 0)
        {
            defect("service-multiplier-invalid", $"service '{id}' states a price or skill multiplier that is not a finite, positive number.");
            return null;
        }

        // The donor's repair price divides by six minus the shop's multiplier, so a multiplier of six or more
        // has no repair price at all rather than a negative or infinite one.
        if (priceMultiplier >= 6)
        {
            defect(
                "service-multiplier-invalid",
                $"service '{id}' states a price multiplier of {priceMultiplier}, and this game's repair price divides by six minus that multiplier.");
            return null;
        }

        List<ServiceStockLine> stock = Stock(entry, id, items, defect);
        List<ServiceLesson> lessons = Lessons(entry, id, defect);
        try
        {
            return new ServiceDefinition(
                id,
                new ServiceKind(kind),
                name,
                operations,
                stock,
                lessons,
                entry.GetString("proprietor"),
                hours,
                refresh,
                priceMultiplier,
                skillMultiplier,
                entry.GetString("membership"));
        }
        catch (ArgumentException error)
        {
            defect("service-invalid", $"service '{id}' cannot be served: {error.Message}");
            return null;
        }
    }

    /// <summary>Reads a service's shelves.</summary>
    private static List<ServiceStockLine> Stock(
        ContentEntry entry,
        ServiceId id,
        Dictionary<ItemDefinitionId, ItemFacts> items,
        Action<string, string> defect)
    {
        List<ServiceStockLine> lines = [];
        foreach (JsonElement element in entry.GetArray("stock"))
        {
            string item = ContentEntry.ReadId(element, "item");
            if (item.Length == 0)
            {
                defect("service-stock-item-missing", $"a line of service '{id}' names no item, so its shelves would hold nothing anybody could buy.");
                continue;
            }

            double? count = ContentEntry.ReadDouble(element, "count");
            double? value = ContentEntry.ReadDouble(element, "value");
            int holding = count is { } amount && amount is >= 1 and <= int.MaxValue ? (int)amount : 1;
            int worth = value is { } price && price >= 0 && price <= int.MaxValue
                ? (int)price
                : items.TryGetValue(new ItemDefinitionId(item), out ItemFacts facts) ? facts.Value : 0;
            try
            {
                lines.Add(new ServiceStockLine(new ItemDefinitionId(item), holding, worth, ContentEntry.ReadString(element, "name")));
            }
            catch (ArgumentOutOfRangeException error)
            {
                defect("service-stock-line-invalid", $"a line of service '{id}' cannot be held: {error.Message}");
            }
        }

        return lines;
    }

    /// <summary>Reads what a service teaches.</summary>
    private static List<ServiceLesson> Lessons(ContentEntry entry, ServiceId id, Action<string, string> defect)
    {
        List<ServiceLesson> lessons = [];
        foreach (JsonElement element in entry.GetArray("lessons"))
        {
            string word = ContentEntry.ReadString(element, "kind");
            string subject = ContentEntry.ReadId(element, "subject");
            ServiceLessonKind? lessonKind = word switch
            {
                "skill" => ServiceLessonKind.Skill,
                "effect" => ServiceLessonKind.Effect,
                _ => null,
            };

            if (lessonKind is not { } kind || subject.Length == 0)
            {
                defect(
                    "service-lesson-incomplete",
                    $"a lesson of service '{id}' states kind '{word}' and subject '{subject}', and a lesson names a skill or an effect to teach.");
                continue;
            }

            double? value = ContentEntry.ReadDouble(element, "value");
            double? amount = ContentEntry.ReadDouble(element, "amount");
            double? tier = ContentEntry.ReadDouble(element, "tier");
            try
            {
                lessons.Add(new ServiceLesson(
                    kind,
                    subject,
                    amount is { } levels && levels is >= 1 and <= int.MaxValue ? (int)levels : 1,
                    value is { } fee && fee >= 0 && fee <= int.MaxValue ? (int)fee : 0,
                    ContentEntry.ReadString(element, "name"),
                    tier is { } rung && rung is >= 1 and <= int.MaxValue ? (int)rung : 1));
            }
            catch (ArgumentOutOfRangeException error)
            {
                defect("service-lesson-invalid", $"a lesson of service '{id}' cannot be taught: {error.Message}");
            }
        }

        return lessons;
    }

    /// <summary>Reads the item table's values, which a shelf line's worth and a sale are priced from.</summary>
    private static Dictionary<ItemDefinitionId, ItemFacts> ReadItems(ContentCatalog catalog)
    {
        Dictionary<ItemDefinitionId, ItemFacts> items = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(ItemDefinitionKind))
        {
            if (entry.Id.Length == 0) continue;
            double? value = entry.GetDouble("value");
            int worth = value is { } price && price >= 0 && price <= int.MaxValue ? (int)price : 0;
            items[new ItemDefinitionId(entry.Id)] = new ItemFacts(worth);
        }

        return items;
    }

    /// <summary>
    /// Judges every place's service placements against the definitions that describe them.
    /// </summary>
    /// <remarks>
    /// A placement content calls a counter and no definition describes is a shop that cannot be entered —
    /// and nothing at runtime could tell it from a person who is talked to for something else. It is named
    /// here, while the session is composed, rather than discovered by a player walking into a dead door.
    /// </remarks>
    private static void ValidatePlacements(
        ContentCatalog catalog,
        Dictionary<ServiceId, ServiceDefinition> services,
        List<ContentValidationIssue> issues)
    {
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in entry.GetArray(PlacePopulationContent.PlacementsField))
            {
                if (!string.Equals(ContentEntry.ReadString(placement, PlacePopulationContent.KindField), PlacementKind, StringComparison.Ordinal)) continue;
                string id = ContentEntry.ReadId(placement, PlacePopulationContent.IdField);
                if (id.Length > 0 && services.ContainsKey(new ServiceId(id))) continue;
                issues.Add(new ContentValidationIssue(
                    "service-placement-unknown",
                    $"place '{entry.Id}' places a service counter named '{id}', which no service entry describes.",
                    pack.PackId,
                    document.DocumentId));
            }
        }
    }

    /// <summary>The operation a content word names, or null when this build has none for it.</summary>
    private static ServiceOperationKind? Operation(string word) => word switch
    {
        "buy" => ServiceOperationKind.Buy,
        "sell" => ServiceOperationKind.Sell,
        "identify" => ServiceOperationKind.Identify,
        "repair" => ServiceOperationKind.Repair,
        "teach" => ServiceOperationKind.Teach,
        _ => null,
    };

    /// <summary>What the imported item table says one item is worth.</summary>
    /// <param name="Value">The item's value in the table's own units.</param>
    private readonly record struct ItemFacts(int Value);
}
