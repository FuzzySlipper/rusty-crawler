using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's service policy: which placement keeps a counter, what it sells, teaches, and offers, who it
/// serves, and what it charges.
/// </summary>
/// <remarks>
/// <para>
/// <b>The kinds come from content; the rules come from here.</b> The shipped building table recognizes
/// twenty-one kinds of service across a hundred and seventy-two buildings — weapon, armor, and magic shops,
/// alchemists, temples, taverns, banks, training halls, stables, docks, town halls, and the ten guilds — and
/// every one of them is served by the one kit mechanism over a definition this reads. Nothing here is a
/// class per kind: a kind is a word in the definition, what it offers is
/// <see cref="MightAndMagic7ServiceKinds"/>'s answer, and what differs between two counters of one kind is
/// the content they were read from.
/// </para>
/// <para>
/// <b>What content must state and what this supplies.</b> The pack carries the building table's own columns —
/// kind, name, proprietor, map, hours, multipliers, stock interval, training ceiling, and the passages a
/// stable or dock sells. Which operations a counter offers, what its shelves hold, what it teaches, what it
/// offers besides goods and lessons, and what it charges are this game's answers about a kind, so they are
/// supplied here when the entry states none of its own; an entry that states its own keeps them, which is
/// what authored content and the tests use.
/// </para>
/// <para>
/// <b>The prices are the donor's arithmetic with content's numbers.</b> Buying, selling, identifying,
/// repairing, teaching, healing, training, renting a room, filling the packs, and buying a passage are
/// priced the way the original prices them (OpenEnroth <c>src/Engine/PriceCalculator.cpp</c>), with the
/// multipliers the building table carries: the shop's price multiplier and its skill multiplier, the
/// party's best Merchant skill, and the party's reputation. The kit owns how a value and a multiplier
/// become coins; this owns which numbers apply.
/// </para>
/// <para>
/// <b>The shelves are the item table's goods.</b> The building table says nothing about what a shop sells,
/// so a shop's range is derived from the imported item table: the equipment words the kind trades, the
/// rarities the table marks as treasure rather than goods, and the shops of one kind dividing the catalogue
/// between them by their own order within the kind. Nothing is random, so the same installation produces
/// the same shops on every import.
/// </para>
/// <para>
/// <b>Membership is party-carried state.</b> A guild's definition names the party-wide effect that
/// membership is and its lessons sell that same effect, so "the party is a member" is one piece of state
/// the access check reads, the purchase writes, and a save already records — never a flag kept here. A
/// bank's holding and a bought passage are the same kind of state, which is why a deposit survives a save
/// and a fare cannot be spent twice.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Services : IServiceRule
{
    /// <summary>The definition kind a service entry uses.</summary>
    internal const string DefinitionKind = "service";

    /// <summary>The placement kind a place uses for the counter standing in it.</summary>
    internal const string PlacementKind = "service";

    /// <summary>The placement kind a place uses for a household rather than a counter.</summary>
    internal const string ResidencePlacementKind = "residence";

    /// <summary>The definition kind item values are read from, which is the imported item table.</summary>
    internal const string ItemDefinitionKind = "item";

    /// <summary>The definition kind a spell is declared under, which is how a guild's books are gated.</summary>
    internal const string SpellDefinitionKind = "spell";

    /// <summary>The definition kind a skill is declared under, which is what a counter may teach.</summary>
    internal const string SkillDefinitionKind = "skill";

    /// <summary>The definition kind a monster is declared under, which is what a town hall's bounty names.</summary>
    internal const string MonsterDefinitionKind = "monster";

    /// <summary>The definition kind a place's road is declared under, which is what a rumour reads.</summary>
    internal const string TravelDefinitionKind = "travel-link";

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

    /// <summary>
    /// The name the party's bank balance is held under, which every bank in the world keeps the same one of.
    /// </summary>
    /// <remarks>
    /// The donor keeps one balance on the party rather than one per bank (OpenEnroth
    /// <c>src/Engine/Party.h</c>, <c>uNumGoldInBank</c>, and <c>GUI/UI/Houses/Bank.cpp</c>, which reads and
    /// writes that one field), so a party that deposits in one town withdraws in the next. The name is
    /// content's for the state and this game's for the balance.
    /// </remarks>
    internal const string BankHolding = "bank";

    /// <summary>The ceiling a training hall without a numeric cap uses, which is the donor's own no-limit.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/GUI/UI/Houses/Training.cpp:18-29</c>, <c>trainingHallMaxLevels</c>: the hall with no
    /// limit is stated as the largest number the donor's type can hold, and this table writes the same row as
    /// the free text "No Max".
    /// </remarks>
    internal const int UncappedTraining = int.MaxValue;

    private readonly Dictionary<ServiceId, ServiceDefinition> _services;
    private readonly Dictionary<ServiceId, ServiceFacts> _facts;
    private readonly Dictionary<ItemDefinitionId, ItemFacts> _items;
    private readonly Dictionary<string, SpellFacts> _spells;
    private readonly Dictionary<int, IReadOnlyList<string>> _placeMonsters;
    private readonly Dictionary<int, IReadOnlyList<string>> _placeRoads;
    private readonly Dictionary<string, int> _monsterLevels;
    private readonly Dictionary<(string Place, string Placement), ServiceHousehold> _households;
    private readonly IReadOnlyList<ItemFacts> _catalogue;
    private readonly MightAndMagic7Skills? _skills;
    private readonly MightAndMagic7Spells? _magic;

    private MightAndMagic7Services(
        Dictionary<ServiceId, ServiceDefinition> services,
        Dictionary<ServiceId, ServiceFacts> facts,
        Dictionary<ItemDefinitionId, ItemFacts> items,
        Dictionary<string, SpellFacts> spells,
        Dictionary<int, IReadOnlyList<string>> placeMonsters,
        Dictionary<int, IReadOnlyList<string>> placeRoads,
        Dictionary<string, int> monsterLevels,
        Dictionary<(string Place, string Placement), ServiceHousehold> households,
        IReadOnlyList<ItemFacts> catalogue,
        MightAndMagic7Skills? skills,
        MightAndMagic7Spells? magic)
    {
        _services = services;
        _facts = facts;
        _items = items;
        _spells = spells;
        _magic = magic;
        _placeMonsters = placeMonsters;
        _placeRoads = placeRoads;
        _monsterLevels = monsterLevels;
        _households = households;
        _catalogue = catalogue;
        _skills = skills;
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
    internal static MightAndMagic7Services? Read(
        ContentCatalog? catalog,
        MightAndMagic7Skills? skillPolicy = null,
        MightAndMagic7Spells? spellPolicy = null)
    {
        if (catalog is null) return null;
        List<ContentValidationIssue> issues = [];
        Dictionary<ItemDefinitionId, ItemFacts> items = ReadItems(catalog);
        Dictionary<string, SpellFacts> spells = ReadSpells(catalog);
        Dictionary<int, IReadOnlyList<string>> placeMonsters = ReadPlaceMonsters(catalog);
        Dictionary<int, IReadOnlyList<string>> placeRoads = ReadPlaceRoads(catalog);
        Dictionary<string, int> monsterLevels = ReadMonsterLevels(catalog);
        Dictionary<ServiceId, ServiceDefinition> services = [];
        Dictionary<ServiceId, ServiceFacts> facts = [];
        HashSet<string> skills = [.. catalog.Entries(SkillDefinitionKind).Select(entry => entry.Entry.Id)];
        IReadOnlyList<ItemFacts> catalogue = [.. items.Values.OrderBy(item => item.Id)];

        // A guild's rung is its position among the guilds of its own school in the order the table states
        // them, which is what the shipped names spell out (initiate, adept, master, paramount) and what this
        // reads without depending on the words.
        Dictionary<string, int> guildRungs = [];
        foreach ((_, _, ContentEntry guild) in catalog.Entries(DefinitionKind)
            .Where(entry => MightAndMagic7ServiceKinds.GuildSchool(entry.Entry.GetString("kind")) is not null)
            .OrderBy(entry => ParseOrder(entry.Entry.Id)))
        {
            string school = MightAndMagic7ServiceKinds.GuildSchool(guild.GetString("kind"))!;
            guildRungs[school] = guildRungs.GetValueOrDefault(school) + 1;
            guildRungs[$"{school}:{guild.Id}"] = guildRungs[school];
        }

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

            if (Definition(entry, id, items, catalogue, skills, guildRungs, facts, Defect) is { } service) services[id] = service;
        }

        Dictionary<(string Place, string Placement), ServiceHousehold> households = ValidatePlacements(catalog, services, issues);

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's services cannot be served: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Services(
            services,
            facts,
            items,
            spells,
            placeMonsters,
            placeRoads,
            monsterLevels,
            households,
            catalogue,
            // The mastery lessons a counter offers are this game's skill policy's answer about its own
            // table, so the session composes one reading of it and hands it over; a caller that composed
            // none gets one read here, so a counter still teaches what its kind states.
            skillPolicy ?? MightAndMagic7Skills.Read(catalog),
            // A guild's spell books are this game's magic's answer about its own table: which spell each book
            // teaches and which rung of a school's ladder the guild stands at. A caller that composed none
            // gets one read here, so a guild still sells its school's spells.
            spellPolicy ?? MightAndMagic7Spells.Read(catalog, skillPolicy));
    }

    /// <inheritdoc />
    public ServiceDefinition? Describe(ServiceTargetRequest request)
    {
        // A placement is a counter only when content says it is one and a definition describes it; every
        // other placement is not a service at all, which is how a person talked to for something else — a
        // quest giver, a master teacher — stays out of this mechanism.
        if (!string.Equals(request.Placement.Content.Kind, PlacementKind, StringComparison.Ordinal)) return null;

        // The imported packs name the building under the field the table's own id came from, because a
        // placement's identity has to be unique among its place's placements; an authored pack may simply
        // name the service as the placement's id, which is what the tests and the earliest content do.
        string named = ContentEntry.ReadId(request.Placement.Source.Payload, "houseId");
        if (named.Length == 0) named = request.Placement.Content.Id;
        return _services.GetValueOrDefault(new ServiceId(named));
    }

    /// <summary>
    /// Whether a table's own name is the word it uses for a row it reserves rather than for somebody.
    /// </summary>
    /// <remarks>
    /// Several building and NPC rows are named "Placeholder", which is the table's own marker for a row
    /// whose name was never written. Naming a person that would put the word on the screen as though it
    /// were somebody's name, so a reader that finds it falls back to what the row does state.
    /// </remarks>
    /// <param name="name">The name as the table wrote it.</param>
    internal static bool IsPlaceholderName(string name) =>
        name.Contains("Placeholder", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Who lives in a placement that is a household rather than a counter, or null when it is not one.
    /// </summary>
    /// <remarks>
    /// A row of the building table that is not a service is a house, a castle, or a dungeon mouth. A house
    /// whose door the map hangs an event on is a household: the party can walk up to it and speak to whoever
    /// the table names there, which is what the table's own name, proprietor, and title state. What a
    /// household offers — an odd job, a guild member to recruit, a master teacher — belongs to the quest,
    /// follower, and teaching owners, so this names who answers the door rather than pretending to a counter
    /// with nothing behind it.
    /// </remarks>
    /// <param name="place">The place the placement stands in.</param>
    /// <param name="placement">The placement's identity within its place.</param>
    internal ServiceHousehold? Household(PlaceId place, string placement) =>
        _households.TryGetValue((place.Value, placement), out ServiceHousehold? who) ? who : null;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// What content declares, unless a line names an item the item table prices: a line may state its own
    /// worth, and one that does not is worth what the imported table says the item is worth rather than
    /// nothing.
    /// </para>
    /// <para>
    /// A definition the importer wrote states no lines at all, because the building table states none: the
    /// shelves are then this game's answer about the kind — the goods the item table holds for what that kind
    /// trades, or the spell books a guild's school and rung allow — laid out deterministically so two imports
    /// of one installation produce the same shops.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ServiceDefinition service = request.Service;
        if (service.Stock.Count > 0)
        {
            List<ServiceStockLine> declared = [];
            foreach (ServiceStockLine line in service.Stock)
            {
                declared.Add(line.Value > 0 || !_items.TryGetValue(line.Definition, out ItemFacts facts)
                    ? line
                    : line with { Value = facts.Value });
            }

            return declared;
        }

        return _facts.TryGetValue(service.Id, out ServiceFacts? facts2)
            ? StockFor(service, facts2)
            : [];
    }

    /// <inheritdoc />
    /// <remarks>
    /// What content declares, or — when it declares nothing, which is what the importer writes — the skills
    /// this game's kind teaches: the trades of the goods it stocks, or the list the donor gives that kind,
    /// plus a guild's own school and its second skill.
    /// </remarks>
    public IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ServiceDefinition service = request.Service;
        if (service.Lessons.Count > 0) return service.Lessons;
        if (!_facts.TryGetValue(service.Id, out ServiceFacts? facts)) return [];

        // The rung a counter can take a member to is the rung it stands at itself: a guild's own depth in
        // its school (see MightAndMagic7ServiceKinds.MasteryDepth), and one for every other kind of counter.
        // The lessons follow: the first rung every counter that teaches a skill already offers, and — where
        // the counter stands deeper — the mastery lessons above it, each priced at the donor's own flat fee
        // for that rung and labelled with the rung's own word.
        int depth = MightAndMagic7ServiceKinds.MasteryDepth(
            facts.GuildRung,
            MightAndMagic7ServiceKinds.IsPairedGuild(service.Kind.Value));
        List<ServiceLesson> lessons = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string skill in facts.TaughtSkills)
        {
            if (!seen.Add(skill)) continue;
            lessons.Add(new ServiceLesson(ServiceLessonKind.Skill, skill, 1, LessonValue(service), skill));
            if (_skills is null) continue;
            for (int tier = 2; tier <= depth; tier++)
            {
                lessons.Add(new ServiceLesson(
                    ServiceLessonKind.Skill,
                    skill,
                    amount: 1,
                    MightAndMagic7Skills.MasteryFee(new SkillId(skill), tier),
                    _skills.LessonName(new SkillId(skill), tier),
                    tier));
            }
        }

        if (facts.Membership.Length > 0)
        {
            lessons.Insert(0, new ServiceLesson(ServiceLessonKind.Effect, facts.Membership, 1, LessonValue(service), facts.MembershipName));
        }

        return [.. lessons, .. SpellBooks(service, facts)];
    }

    /// <summary>
    /// The spell books a guild's rung of its own school may sell, as lessons one book each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A book bought at a guild is a lesson, and the book is consumed by it.</b> The shipped item table
    /// carries one book per spell — 99 rows whose reference column names the spell and whose value is the
    /// book's price ([`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md),
    /// <i>Items and monsters</i>: 99 of the 800 rows are books) — and the donor's own guild screen stocks a
    /// random sample of the books its school and rung allow
    /// (<c>src/GUI/UI/Houses/MagicGuild.cpp:317-345</c>, <c>generateSpellBooksForGuild</c> over
    /// <c>spellsForSchool</c>). The price here is the book's own value, charged through the same lesson price
    /// the counter's other lessons go through, and what the party takes away is the spell: a purchased book
    /// that landed in the pack would be a second thing to carry, a second way to learn, and an item with no
    /// other use.
    /// </para>
    /// <para>
    /// The order is the catalogue's, which is the item table's own, so two imports of one installation offer
    /// the same books in the same order rather than a random sample a second run would draw differently. What
    /// the guild may sell is <see cref="MightAndMagic7Spells.Sells"/>: every spell of its school whose tier is
    /// at most the rung the guild stands at.
    /// </para>
    /// </remarks>
    private IReadOnlyList<ServiceLesson> SpellBooks(ServiceDefinition service, ServiceFacts facts)
    {
        if (_magic is not { } magic || facts.School.Length == 0) return [];
        int rung = MightAndMagic7ServiceKinds.MasteryDepth(
            facts.GuildRung,
            MightAndMagic7ServiceKinds.IsPairedGuild(service.Kind.Value));

        List<ServiceLesson> books = [];
        foreach (ItemFacts item in _catalogue)
        {
            if (!string.Equals(item.EquipStat, MightAndMagic7Spells.BookEquipStat, StringComparison.OrdinalIgnoreCase)) continue;
            if (magic.TaughtBy(item.Definition) is not { } taught) continue;
            if (magic.Spell(taught.Value) is not { } spell) continue;
            if (!string.Equals(spell.School, facts.School, StringComparison.OrdinalIgnoreCase)) continue;
            if (!MightAndMagic7Spells.Sells(rung, spell)) continue;

            // The lesson's own rung is the first, because a book teaches one spell rather than a rung of a
            // ladder: the rung a lesson carries is what a mastery lesson raises a skill to, and the price it
            // implies is the flat mastery fee rather than the book's own value.
            books.Add(new ServiceLesson(ServiceLessonKind.Spell, spell.Id.Value, amount: 1, item.Value, spell.Name));
        }

        return books;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The membership a service names, as the party's own party-wide effects carry it. The word shown is the
    /// lesson's own name where one sells it, so a guild reads as what a player bought rather than as an
    /// internal effect id.
    /// </para>
    /// <para>
    /// A counter may also require nothing and still publish what the party carries: a temple shows the
    /// conditions it can end, and a bank shows the balance it keeps, because a player checks those before
    /// deciding. They are published here rather than in the offers because they are what the party already
    /// has, which is the same thing a membership is.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Access(ServiceAccessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<string> carried = [];
        if (request.Service.Membership.Length > 0 && request.Party.Effects.Has(new EffectId(request.Service.Membership)))
        {
            carried.Add(MembershipName(request.Service));
        }

        if (_facts.TryGetValue(request.Service.Id, out ServiceFacts? facts) && facts.HoldsCoins)
        {
            carried.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"the counter holds {ServiceHolding.Coins(request.Party, BankHolding)} coin(s)"));
        }

        return carried;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// What the mechanism cannot know and this game decides: whether the party carries the access the
    /// counter requires, whether the thing being asked for is already the party's, whether a member has
    /// anything to cure or anything to train with, and how much a bank may be asked for.
    /// </para>
    /// <para>
    /// A guild serves its members only — except at the one lesson that sells the membership itself, because
    /// a counter that required the membership before it would sell it could never be joined. A temple does
    /// not sell a cure to somebody who is well, and a training hall does not take a fee from a member who
    /// has not earned the level or has reached the hall's own ceiling.
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
                $"{service.Describe()} serves members only, and the party carries no membership of {MembershipName(service)}.");
        }

        return request.Operation switch
        {
            ServiceOperationKind.Teach => JudgeLesson(request),
            ServiceOperationKind.Cure => JudgeCure(request),
            ServiceOperationKind.Train => JudgeTraining(request),
            ServiceOperationKind.Provision => JudgeProvision(request),
            ServiceOperationKind.Deposit => JudgeDeposit(request),
            ServiceOperationKind.Withdraw => JudgeWithdrawal(request),
            ServiceOperationKind.Fare => JudgeFare(request),
            _ => ServiceEligibility.Allowed,
        };
    }


    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// What a counter offers beyond goods and lessons, as this game answers it for the kind: a temple's
    /// cures, a training hall's step and its ceiling, a tavern's provisions, its room, and what travellers
    /// say there, a bank's account with the party, and a stable's or a dock's passages. Which of them appear
    /// depends on the party's own state — a cure for a condition nobody suffers is not offered, and a
    /// training step for a member who has reached the hall's ceiling is not either — so a counter's surface
    /// answers what the party could actually do there.
    /// </para>
    /// <para>
    /// <b>A notice is a surface, not a transaction.</b> A town hall's bounty and a tavern's rumour are lines
    /// the counter posts: they cost nothing and change nothing, and they are published here rather than being
    /// a command, because a player reads them. What the bounty would pay is the donor's own hundred times
    /// the beast's level (OpenEnroth <c>src/GUI/UI/Houses/TownHall.cpp:143-176</c>), and which beast it is
    /// comes from the place's own encounter row rather than from a list this ruleset keeps.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ServiceOffer> Offers(ServiceOfferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ServiceDefinition service = request.Service;
        if (!_facts.TryGetValue(service.Id, out ServiceFacts? facts)) return [];

        List<ServiceOffer> offers = [];
        if (string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.Temple, StringComparison.Ordinal))
        {
            offers.AddRange(MightAndMagic7Conditions.Cures(Conditions(request.Party)));
        }
        else if (string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.Training, StringComparison.Ordinal))
        {
            offers.Add(new ServiceOffer(
                ServiceOfferKind.Training,
                "Training",
                Value: 0,
                Amount: 1,
                Limit: facts.TrainingCap > 0 ? facts.TrainingCap : UncappedTraining));
        }
        else if (string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.Tavern, StringComparison.Ordinal))
        {
            offers.Add(new ServiceOffer(
                ServiceOfferKind.Provision,
                "Food and drink",
                Value: ProvisionPrice(service),
                Amount: facts.ProvisionPortions > 0 ? facts.ProvisionPortions : 1));
            offers.Add(new ServiceOffer(
                ServiceOfferKind.Stay,
                "A room for the night",
                Value: RoomPrice(service),
                Amount: MightAndMagic7ServiceKinds.RoomHours(Now(request), MightAndMagic7ServiceKinds.IsUndergroundTown(facts.MapId)),
                Clears: MightAndMagic7Conditions.RestClears));
        }
        else if (string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.Bank, StringComparison.Ordinal))
        {
            offers.Add(new ServiceOffer(
                ServiceOfferKind.Holding,
                "The counter's keeping",
                Subject: BankHolding,
                Value: 0,
                Amount: ServiceHolding.Coins(request.Party, BankHolding)));
        }
        else if (string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.Stables, StringComparison.Ordinal)
            || string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.Boats, StringComparison.Ordinal))
        {
            foreach (ServiceFare fare in facts.Fares)
            {
                offers.Add(new ServiceOffer(
                    ServiceOfferKind.Fare,
                    $"A passage to {fare.Name}",
                    Subject: fare.Place,
                    Value: MightAndMagic7ServiceKinds.FareBase(service.Kind.Value),
                    Amount: fare.Days));
            }
        }

        if (string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.TownHall, StringComparison.Ordinal))
        {
            if (Bounty(facts.MapId, Now(request)) is { } bounty) offers.Add(bounty);
        }

        // What travellers say belongs to the tavern, which is where a party asks; a town hall posts its own
        // business instead.
        if (string.Equals(service.Kind.Value, MightAndMagic7ServiceKinds.Tavern, StringComparison.Ordinal)
            && Rumour(facts.MapId) is { } rumour)
        {
            offers.Add(rumour);
        }

        return offers;
    }

    /// <summary>Every condition every member of the party is suffering, which is what a temple offers to end.</summary>
    private static IReadOnlyList<ActiveCondition> Conditions(PartyEntity party)
    {
        List<ActiveCondition> conditions = [];
        foreach (PartyMember member in party.Members)
        {
            foreach (ActiveCondition condition in member.Conditions.Active)
            {
                if (!conditions.Any(active => active.Condition == condition.Condition))
                {
                    conditions.Add(condition);
                }
            }
        }

        return conditions;
    }

    /// <summary>The hour the party stands at, or the first hour of the day when the session keeps no clock.</summary>
    private static GameDate Now(ServiceOfferRequest request) => request.Clock?.Now ?? default;

    /// <summary>
    /// The bounty a town hall posts this month, from the place's own encounter row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor draws a huntable beast and pays a hundred times its level, refreshing the bounty monthly
    /// (OpenEnroth <c>src/GUI/UI/Houses/TownHall.cpp:135-176</c>). Nothing here draws: the beast is chosen
    /// from the place's own encounter row by the month the clock stands in, so the same month at the same
    /// hall is the same bounty in every session, and a party that reads the notice twice reads one notice.
    /// </para>
    /// <para>
    /// Claiming it needs the kill, and this build has no combat, so the notice is published and the claim is
    /// not: a hall that paid a bounty for a beast still breathing would be paying for nothing.
    /// </para>
    /// </remarks>
    private ServiceOffer? Bounty(int mapId, GameDate now)
    {
        if (!_placeMonsters.TryGetValue(mapId, out IReadOnlyList<string>? monsters) || monsters.Count == 0) return null;
        string beast = monsters[(Math.Max(1, now.Month) - 1) % monsters.Count];
        int level = _monsterLevels.GetValueOrDefault(beast, 1);
        int reward = 100 * Math.Max(1, level);
        return new ServiceOffer(
            ServiceOfferKind.Notice,
            string.Create(
                CultureInfo.InvariantCulture,
                $"This month's bounty is on a {beast}: the hall pays {reward} coin(s) for proof of the kill."),
            Subject: beast,
            Value: 0,
            Amount: reward);
    }

    /// <summary>
    /// What travellers say at a tavern, from the roads the place's own map issues.
    /// </summary>
    /// <remarks>
    /// The tables carry no rumour text — the original's gossip is in its own dialogue strings — so the
    /// counter says what the imported world actually holds: where the roads out of this place lead, coach and
    /// boat routes included, because those are the journeys a party at a tavern would ask about. It is a
    /// surface built from the data rather than a line invented for each of the twenty-one taverns.
    /// </remarks>
    private ServiceOffer? Rumour(int mapId)
    {
        if (!_placeRoads.TryGetValue(mapId, out IReadOnlyList<string>? roads) || roads.Count == 0) return null;
        IReadOnlyList<string> named = [.. roads.Take(3)];
        string list = named.Count == 1
            ? named[0]
            : string.Join(", ", named.Take(named.Count - 1)) + " and " + named[^1];
        return new ServiceOffer(
            ServiceOfferKind.Notice,
            $"Travellers here speak of the roads to {list}.",
            Value: 0);
    }

    /// <summary>Whether the party may be taught the lesson it named.</summary>
    private ServiceEligibility JudgeLesson(ServiceEligibilityRequest request)
    {
        if (request.Subject.Lesson is not { } teaching) return ServiceEligibility.Allowed;

        // A spell lesson is this game's magic's own answer about who may learn it: the school's skill, the
        // rung the spell asks for, and a spellbook that does not already hold it. The counter judges nothing
        // of it itself, so a book refused at a guild and a book refused anywhere else are one refusal.
        if (teaching.Kind == ServiceLessonKind.Spell)
        {
            if (_magic is not { } magic) return ServiceEligibility.Allowed;
            PartyMember learner = request.Party.Member(request.Member);
            SpellDefinition spell = magic.Catalog.Read(new SpellId(teaching.Subject));
            return magic.MayLearn(learner, spell) is { } refused
                ? ServiceEligibility.Refused(refused.Code, refused.Message)
                : ServiceEligibility.Allowed;
        }

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
        if (level >= teaching.Amount && tier >= teaching.Tier)
        {
            return ServiceEligibility.Refused(
                "service-nothing-to-learn",
                $"{recipient.Profile.Name} already has {teaching.Label} at level {level} and rung {tier}, which is what the lesson teaches.");
        }

        // Whether this member's class and rank may hold the rung at all — and, when they may not, which
        // promotion would open it — is this game's skill policy's answer rather than the mechanism's: the
        // ceiling, the rung below, the skill level a teacher wants, and the donor's own extra conditions all
        // live beside the table they are read from.
        return _skills?.Lesson(recipient, skill, teaching.Tier, teaching.Amount) is { } refusal
            ? ServiceEligibility.Refused(refusal.Code, refusal.Message)
            : ServiceEligibility.Allowed;
    }

    /// <summary>
    /// Whether a temple may heal the member, which it may only when the cure claims something they suffer.
    /// </summary>
    /// <remarks>
    /// The donor's own refusal (OpenEnroth <c>src/GUI/UI/Houses/Temple.cpp:165-178</c>,
    /// <c>isPlayerHealableByTemple</c>): a character who is whole is not healed, and so is not charged.
    /// </remarks>
    private static ServiceEligibility JudgeCure(ServiceEligibilityRequest request)
    {
        if (request.Subject.Offer is not { } cure) return ServiceEligibility.Allowed;
        PartyMember patient = request.Party.Member(request.Member);
        bool suffers = patient.Conditions.Active.Any(condition => cure.ClearsCondition(condition.Condition));
        return suffers
            ? ServiceEligibility.Allowed
            : ServiceEligibility.Refused(
                "service-nothing-to-heal",
                $"{patient.Profile.Name} suffers nothing {cure.Name} treats, so there is nothing to heal.");
    }

    /// <summary>
    /// Whether a member may train here: the hall has not taken them to its ceiling, and they have earned
    /// the level.
    /// </summary>
    /// <remarks>
    /// The donor's two conditions (OpenEnroth <c>src/GUI/UI/Houses/Training.cpp:36-49</c>): a member at the
    /// hall's ceiling cannot train, and a member short of the experience a level takes is told how much more
    /// is wanted. The curve is asked of <see cref="MightAndMagic7Progression"/>, which owns it, so the
    /// figure a hall refuses over and the figure the progression owner trains by are one formula.
    /// </remarks>
    private static ServiceEligibility JudgeTraining(ServiceEligibilityRequest request)
    {
        if (request.Subject.Offer is not { } training) return ServiceEligibility.Allowed;
        PartyMember member = request.Party.Member(request.Member);
        int level = member.Progression.Level;
        if (level >= training.Limit)
        {
            return ServiceEligibility.Refused(
                "service-training-capped",
                $"{member.Profile.Name} stands at level {level} and {request.Service.Describe()} trains no further than level {training.Limit}.");
        }

        long wanted = MightAndMagic7Progression.Instance.ExperienceForLevel(level);
        return member.Progression.Experience >= wanted
            ? ServiceEligibility.Allowed
            : ServiceEligibility.Refused(
                "service-experience-short",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{member.Profile.Name} needs {wanted - member.Progression.Experience} more experience to train to level {level + 1}."));
    }

    /// <summary>Whether a tavern may fill the party's packs, which it may not once they are full.</summary>
    /// <remarks>
    /// The donor refuses a purchase when the party already carries as much as the tavern sells
    /// (OpenEnroth <c>src/GUI/UI/Houses/Tavern.cpp:99-104</c>). Our larder has no stated capacity, so the
    /// tavern's own number of days is what "full" means here — a divergence from a game whose packs hold a
    /// fixed number of days, and one that keeps the refusal rather than selling food nobody needs.
    /// </remarks>
    private static ServiceEligibility JudgeProvision(ServiceEligibilityRequest request)
    {
        if (request.Subject.Offer is not { } provision) return ServiceEligibility.Allowed;
        return request.Party.Food.Portions >= provision.Amount
            ? ServiceEligibility.Refused(
                "service-packs-full",
                $"The party already carries {request.Party.Food.Portions} provisions and {request.Service.Describe()} fills packs to {provision.Amount}.")
            : ServiceEligibility.Allowed;
    }

    /// <summary>Whether a bank may take the coins the party offers.</summary>
    /// <remarks>
    /// A deposit of nothing is not a deposit, and a party cannot leave coin it does not have: the second is
    /// judged here rather than left to the settlement path so a bank says the party is short rather than the
    /// purse refusing on its behalf.
    /// </remarks>
    private static ServiceEligibility JudgeDeposit(ServiceEligibilityRequest request)
    {
        if (request.Subject.Offer is not { } holding) return ServiceEligibility.Allowed;
        int coins = request.Subject.Count;
        if (coins < 1)
        {
            return ServiceEligibility.Refused("service-deposit-empty", "A deposit of nothing is not a deposit.");
        }

        return request.Party.Purse.Coins >= coins
            ? ServiceEligibility.Allowed
            : ServiceEligibility.Refused(
                "purse-short",
                $"The party holds {request.Party.Purse.Coins} coin(s) and asked to leave {coins} with {request.Service.Describe()}.");
    }

    /// <summary>Whether a bank holds as much as the party asks to take back.</summary>
    private static ServiceEligibility JudgeWithdrawal(ServiceEligibilityRequest request)
    {
        if (request.Subject.Offer is not { } holding) return ServiceEligibility.Allowed;
        int held = ServiceHolding.Coins(request.Party, holding.Subject.Length > 0 ? holding.Subject : holding.Name);
        return request.Subject.Count <= held
            ? ServiceEligibility.Allowed
            : ServiceEligibility.Refused(
                "service-holding-short",
                $"{request.Service.Describe()} holds {held} coin(s) for the party and the party asked for {request.Subject.Count}.");
    }

    /// <summary>
    /// Whether a counter may sell the passage the party named.
    /// </summary>
    /// <remarks>
    /// A party that already holds a passage to a place is not sold a second one: the ticket is the party's
    /// and a counter that sold another would be charging twice for one journey. What the destination is, and
    /// whether the world has a road there, are content's and the world's business rather than this counter's.
    /// </remarks>
    private static ServiceEligibility JudgeFare(ServiceEligibilityRequest request)
    {
        if (request.Subject.Offer is not { } fare) return ServiceEligibility.Allowed;
        return ServicePassage.DaysTo(request.Party, new PlaceId(fare.Subject)) > 0
            ? ServiceEligibility.Refused(
                "service-passage-held",
                $"The party already holds a passage to {fare.Name}, so there is no second one to sell.")
            : ServiceEligibility.Allowed;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The original's prices, over the multipliers content carries. A purchase is never below what the item
    /// is worth, an identification and a repair never below a third of their base, and a shop never pays
    /// more for an item than it is worth or less than one coin (OpenEnroth
    /// <c>src/Engine/PriceCalculator.cpp:26-95</c>). A cure, a training step, a room, a fill of the packs,
    /// and a fare are the donor's own formulas for those acts (<c>PriceCalculator.cpp:129-215</c>).
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
            ServiceOperationKind.Teach => TeachPrice(request, merchant),
            ServiceOperationKind.Cure => Cure(request, merchant),
            ServiceOperationKind.Train => Training(request, merchant),
            ServiceOperationKind.Provision => Charge(merchant, request.Subject.Value),
            ServiceOperationKind.Stay => Charge(merchant, request.Subject.Value),
            ServiceOperationKind.Deposit => ServiceQuote.Charging(request.Subject.Count, request.Subject.Count),
            ServiceOperationKind.Withdraw => ServiceQuote.Paying(request.Subject.Count, request.Subject.Count),
            _ => FarePrice(service, request.Subject, merchant),
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

    /// <summary>What a lesson costs: a counter's own price for the first rung, the donor's flat fee for a mastery.</summary>
    /// <remarks>
    /// A first-rung lesson is priced as it always was — the donor's five hundred over the counter's own
    /// skill multiplier, with the party's merchant standing taking its share. A mastery lesson is not: the
    /// donor's teacher states a fee and the party pays exactly it (OpenEnroth
    /// <c>src/GUI/UI/NPCTopics.cpp:481-527</c>, where the cost is read from the skill's own table and no
    /// multiplier is applied), so what a mastery costs is the same in every house that teaches it and is
    /// not discounted. Two lessons of one skill are two prices, which is why the rung decides.
    /// </remarks>
    private static ServiceQuote TeachPrice(ServiceQuoteRequest request, int merchant) =>
        request.Subject.Lesson is { Tier: > 1 } mastery
            ? ServiceQuote.Charging(mastery.Value, mastery.Value)
            : Charge(merchant, ServicePricing.Coins(request.Subject.Value, request.Service.SkillPriceMultiplier));

    /// <summary>What a temple charges to end the conditions its cure claims.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:83-95</c>: the worst condition's multiplier — one for
    /// an ordinary affliction, five for death or petrification, ten for eradication — times how long it has
    /// been suffered, times the temple's own multiplier, held between one coin and ten thousand. This party
    /// model records how severe a condition is rather than when it began, so the severity stands in for the
    /// span (<see cref="MightAndMagic7Conditions"/>).
    /// </remarks>
    private ServiceQuote Cure(ServiceQuoteRequest request, int merchant)
    {
        ServiceOffer offer = request.Subject.Offer!;
        IReadOnlyList<ActiveCondition> conditions = request.Party.Member(request.Member).Conditions.Active;
        int basePrice = MightAndMagic7Conditions.HealingPrice(offer, conditions, request.Service.PriceMultiplier);
        return Charge(merchant, basePrice);
    }

    /// <summary>What a training step costs, as the donor prices a level.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:200-215</c>, <c>trainingCostForPlayer</c>: the member's
    /// level times the hall's multiplier times the class's tier — the rank the member has reached in its
    /// class ladder — discounted by the merchant and never below a third of that; and nothing at all until
    /// the member has earned the experience a level takes, which is a thousand times the level times the
    /// level plus one, halved (<c>1000 * level * (level + 1) / 2</c>). The member trains one level per step,
    /// which is what the donor's own screen offers.
    /// </remarks>
    private ServiceQuote Training(ServiceQuoteRequest request, int merchant)
    {
        PartyMember member = request.Party.Member(request.Member);
        int level = member.Progression.Level;
        int classTier = Math.Max(1, member.Progression.ClassRank);
        int basePrice = (int)Math.Min(int.MaxValue, (double)level * request.Service.PriceMultiplier * classTier);
        return Charge(merchant, basePrice);
    }

    /// <summary>What filling the party's packs costs, as the donor prices a tavern's food.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:188-198</c>, <c>tavernFoodCostForPlayer</c>: the
    /// tavern's multiplier cubed over a hundred, never below a third of that and never below one coin. The
    /// amount the tavern fills is its own multiplier, in days
    /// (<c>src/GUI/UI/Houses/Tavern.cpp:99-118</c>, <c>SetFood(fPriceMultiplier)</c>).
    /// </remarks>
    private static int ProvisionPrice(ServiceDefinition service) =>
        ServicePricing.AtLeast(ServicePricing.Coins(1, Math.Pow(service.PriceMultiplier, 3) / 100), 1);

    /// <summary>What a room for the night costs, as the donor prices one.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:176-186</c>, <c>tavernRoomCostForPlayer</c>: the
    /// tavern's multiplier squared over ten, never below a third of that and never below one coin.
    /// </remarks>
    private static int RoomPrice(ServiceDefinition service) =>
        ServicePricing.AtLeast(ServicePricing.Coins(1, service.PriceMultiplier * service.PriceMultiplier / 10), 1);

    /// <summary>What a passage costs, as the donor prices a seat or a berth.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:162-174</c>, <c>transportCostForPlayer</c>: the
    /// network's own base — twenty-five for a coach and fifty for a boat — times the counter's multiplier,
    /// discounted by the merchant and never below a third of the base. The days the journey takes are the
    /// route's own and travel in the offer, not in the price.
    /// </remarks>
    private static ServiceQuote FarePrice(ServiceDefinition service, ServiceSubject subject, int merchant)
    {
        int basePrice = subject.Value > 0 ? subject.Value : MightAndMagic7ServiceKinds.FareBase(service.Kind.Value);
        int price = ServicePricing.Percent(ServicePricing.Coins(basePrice, service.PriceMultiplier), merchant);
        int floor = Math.Max(1, basePrice / 3);
        return ServiceQuote.Charging(ServicePricing.AtLeast(price, floor), basePrice);
    }

    /// <summary>What an item is worth, as the imported item table states it.</summary>
    private int ValueOf(ItemInstance? item) =>
        item is { } instance && _items.TryGetValue(instance.Definition, out ItemFacts facts) ? facts.Value : 0;

    /// <summary>How a service's membership reads to a person: its lesson's name, or its own effect id.</summary>
    private static string MembershipName(ServiceDefinition service)
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

    /// <summary>The base a kind-supplied lesson's fee is computed from, so the one quote formula prices it.</summary>
    /// <remarks>
    /// The donor prices a guild's teaching at the guild's own multiplier and everything else's at the
    /// counter's skill multiplier (OpenEnroth <c>src/Engine/PriceCalculator.cpp:148-160</c>). The quote
    /// formula the kit owns multiplies a lesson's own base by the skill multiplier, so a guild's lessons
    /// carry the base that produces the donor's price through it — the guild's multiplier over its skill
    /// multiplier, times the donor's five hundred — and every kind keeps one arithmetic.
    /// </remarks>
    private static int LessonValue(ServiceDefinition service) =>
        MightAndMagic7ServiceKinds.IsGuild(service.Kind.Value)
            ? ServicePricing.Coins(1, MightAndMagic7ServiceKinds.LessonBasePrice * service.PriceMultiplier / service.SkillPriceMultiplier)
            : MightAndMagic7ServiceKinds.LessonBasePrice;

    /// <summary>Reads one service entry into the definition the mechanism serves.</summary>
    private static ServiceDefinition? Definition(
        ContentEntry entry,
        ServiceId id,
        Dictionary<ItemDefinitionId, ItemFacts> items,
        IReadOnlyList<ItemFacts> catalogue,
        HashSet<string> skills,
        Dictionary<string, int> guildRungs,
        Dictionary<ServiceId, ServiceFacts> facts,
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
                    $"service '{id}' offers '{word}', which this build has no operation for.");
                continue;
            }

            operations.Add(operation);
        }

        // An entry that states no operation of its own is a counter whose kind this game knows, and the
        // kind's own operations are what it offers; a kind with none is a surface rather than a counter — a
        // town hall posts a bounty and takes a fine, and the bounty is read rather than transacted.
        if (operations.Count == 0) operations.AddRange(MightAndMagic7ServiceKinds.Operations(kind));

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

        // The donor's repair price divides by six minus the shop's multiplier, so a shop that repairs may
        // not state a multiplier of six or more: its repair price would be a division by zero rather than a
        // price. The guard is on the counters that repair and not on the number, because the table's other
        // kinds carry larger multipliers for what they charge — a training hall's fee, a tavern's days of
        // food — and refusing those would refuse the operator's own data.
        if (operations.Contains(ServiceOperationKind.Repair) && priceMultiplier >= 6)
        {
            defect(
                "service-multiplier-invalid",
                $"service '{id}' repairs and states a price multiplier of {priceMultiplier}, and this game's repair price divides by six minus that multiplier.");
            return null;
        }

        List<ServiceStockLine> stock = Stock(entry, id, items, defect);
        List<ServiceLesson> lessons = Lessons(entry, id, defect);
        string membership = entry.GetString("membership");
        int mapId = entry.GetInt32("mapId") ?? 0;
        List<ServiceFare> fares = Fares(entry, id, defect);

        // A guild's membership and its rung are this game's answers about the kind, so they are supplied
        // when content states neither: the effect the access check reads and the lessons sell, and the rung
        // that decides how many spell levels of the school its shelves may hold.
        ServiceFacts? kindFacts = KindFacts(kind, entry, skills, guildRungs, membership, catalogue);
        if (kindFacts is { } supplied)
        {
            membership = supplied.Membership;
            facts[id] = supplied with { Fares = fares, MapId = mapId };
        }
        else
        {
            facts[id] = new ServiceFacts(
                MapId: mapId,
                TaughtSkills: [],
                GuildRung: 0,
                Membership: membership,
                MembershipName: membership,
                School: string.Empty,
                TrainingCap: 0,
                FareDays: 0,
                ProvisionPortions: 0,
                StayHours: 0,
                HoldsCoins: false,
                Fares: fares);
        }

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
                membership);
        }
        catch (ArgumentException error)
        {
            defect("service-invalid", $"service '{id}' cannot be served: {error.Message}");
            return null;
        }
    }

    /// <summary>
    /// This game's answers about a kind, as the facts the offers, lessons, and shelves are built from.
    /// </summary>
    /// <remarks>
    /// A guild's membership is the effect its access check reads; its school and rung decide the spell books
    /// on its shelves. A training hall's ceiling and a tavern's portions and hours come from the columns the
    /// table fills, read here so the offers can be built without reading content again.
    /// </remarks>
    private static ServiceFacts? KindFacts(
        string kind,
        ContentEntry entry,
        HashSet<string> skills,
        Dictionary<string, int> guildRungs,
        string membership,
        IReadOnlyList<ItemFacts> catalogue)
    {
        string? school = MightAndMagic7ServiceKinds.GuildSchool(kind);
        int rung = school is not null && guildRungs.TryGetValue($"{school}:{entry.Id}", out int found) ? found : 0;
        int cap = entry.GetInt32("trainingCap") ?? (string.Equals(kind, MightAndMagic7ServiceKinds.Training, StringComparison.Ordinal) ? UncappedTraining : 0);
        int portions = string.Equals(kind, MightAndMagic7ServiceKinds.Tavern, StringComparison.Ordinal)
            ? (int)Math.Round(entry.GetDouble("priceMultiplier") ?? 1)
            : 0;

        List<string> taught = [];
        foreach (string skill in MightAndMagic7ServiceKinds.FixedLessons(kind))
        {
            if (skills.Contains(skill)) taught.Add(skill);
        }

        // A weapon or an armor shop teaches the trades of the goods it stocks, which is the donor's own
        // reading of a shop's learnable skills (OpenEnroth <c>src/GUI/UI/Houses/Shops.cpp:697-731</c>: the
        // skills come from the item classes the counter sells). The item table's own skill column is that
        // list, filtered by the skills this game actually has — the table files a club under a skill the
        // skill table does not carry, and a counter cannot teach what the game has no ladder for.
        IReadOnlyList<string> trades = MightAndMagic7ServiceKinds.StockEquipStats(kind);
        if (trades.Count > 0)
        {
            foreach (ItemFacts item in catalogue)
            {
                if (!trades.Contains(item.EquipStat, StringComparer.OrdinalIgnoreCase)) continue;
                if (MightAndMagic7ServiceKinds.RareMaterials.Contains(item.Material, StringComparer.OrdinalIgnoreCase)) continue;
                if (!skills.Contains(item.SkillGroup)) continue;
                if (!taught.Contains(item.SkillGroup, StringComparer.Ordinal)) taught.Add(item.SkillGroup);
            }
        }

        if (school is not null && skills.Contains(school)) taught.Insert(0, school);
        string extra = MightAndMagic7ServiceKinds.GuildExtraSkill(kind);
        if (extra.Length > 0 && skills.Contains(extra)) taught.Add(extra);

        // A guild's membership is content's own effect when it states one, and this game's otherwise: the
        // school is the identity a save carries and a second guild of the same school is the same membership.
        string effect = membership;
        if (school is not null && effect.Length == 0) effect = $"guild.{school.ToLowerInvariant()}";

        return new ServiceFacts(
            MapId: 0,
            TaughtSkills: taught,
            GuildRung: rung,
            Membership: effect,
            MembershipName: school is null ? effect : $"{school} Guild membership",
            School: school ?? string.Empty,
            TrainingCap: cap,
            FareDays: 0,
            ProvisionPortions: portions,
            StayHours: 0,
            HoldsCoins: string.Equals(kind, MightAndMagic7ServiceKinds.Bank, StringComparison.Ordinal),
            Fares: []);
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

    /// <summary>Reads the passages a counter sells, which the import wrote from the routes it derived.</summary>
    private static List<ServiceFare> Fares(ContentEntry entry, ServiceId id, Action<string, string> defect)
    {
        List<ServiceFare> fares = [];
        foreach (JsonElement element in entry.GetArray("fares"))
        {
            string place = ContentEntry.ReadId(element, "place");
            string name = ContentEntry.ReadString(element, "name");
            double? days = ContentEntry.ReadDouble(element, "days");
            if (place.Length == 0)
            {
                defect("service-fare-destination-missing", $"a passage of service '{id}' names no destination place, so there is nowhere it could take the party.");
                continue;
            }

            fares.Add(new ServiceFare(place, name.Length == 0 ? place : name, days is { } length && length >= 1 ? (int)length : 1));
        }

        return fares;
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
            items[new ItemDefinitionId(entry.Id)] = new ItemFacts(
                ParseOrder(entry.Id),
                entry.GetString("name"),
                worth,
                entry.GetString("equipStat"),
                entry.GetString("skillGroup"),
                entry.GetString("material"));
        }

        return items;
    }

    /// <summary>The spells this game sells, by the school and level a guild's shelves are gated by.</summary>
    private static Dictionary<string, SpellFacts> ReadSpells(ContentCatalog catalog)
    {
        Dictionary<string, SpellFacts> spells = new(StringComparer.Ordinal);
        foreach ((_, _, ContentEntry entry) in catalog.Entries(SpellDefinitionKind))
        {
            string name = entry.GetString("name");
            string school = entry.GetString("school");
            if (name.Length == 0 || school.Length == 0) continue;
            spells[name] = new SpellFacts(school, entry.GetInt32("level") ?? 1);
        }

        return spells;
    }

    /// <summary>
    /// The shelves a kind's counter holds: a shop's goods from the item table, a guild's spell books from its
    /// school and rung.
    /// </summary>
    /// <remarks>
    /// The catalogue is catalogued once, in the order of the item table, and the shops of one kind divide it
    /// by their own position among their kind: the k-th of n shops takes every n-th line, so the shops of a
    /// town differ from each other and together cover what the kind trades. Nothing is random and nothing
    /// depends on a hash, so a shop's shelves are a fact of the installation rather than of a run.
    /// </remarks>
    private IReadOnlyList<ServiceStockLine> StockFor(ServiceDefinition service, ServiceFacts facts)
    {
        List<ItemFacts> candidates = [];

        // A guild's shelves hold no goods at all: what it sells is its school's spell books, which are
        // lessons rather than stock, because a book is consumed by the learning and a line of stock would be
        // a second way to buy the same spell.
        if (facts.School.Length > 0) return [];

        IReadOnlyList<string> trades = MightAndMagic7ServiceKinds.StockEquipStats(service.Kind.Value);
        if (trades.Count == 0) return [];
        foreach (ItemFacts item in _catalogue)
        {
            if (!trades.Contains(item.EquipStat, StringComparer.OrdinalIgnoreCase)) continue;
            if (MightAndMagic7ServiceKinds.RareMaterials.Contains(item.Material, StringComparer.OrdinalIgnoreCase)) continue;
            candidates.Add(item);
        }

        // The shops of one kind divide the catalogue between them by their own order, which the table states
        // in its type-sequence column.
        IReadOnlyList<ServiceDefinition> peers = [.. _facts
            .Where(pair => _services[pair.Key].Kind == service.Kind)
            .OrderBy(pair => ParseOrder(pair.Key.Value))
            .Select(pair => _services[pair.Key])];
        int stride = Math.Max(1, peers.Count);
        int position = 0;
        for (int index = 0; index < peers.Count; index++)
        {
            if (peers[index].Id == service.Id)
            {
                position = index;
                break;
            }
        }

        List<ServiceStockLine> lines = [];
        for (int index = position; index < candidates.Count && lines.Count < MightAndMagic7ServiceKinds.ShopStockLines; index += stride)
        {
            ItemFacts item = candidates[index];
            lines.Add(new ServiceStockLine(item.Definition, 1, item.Value, item.Name));
        }

        return lines;
    }

    /// <summary>
    /// Judges every place's service placements against the definitions that describe them.
    /// </summary>
    /// <remarks>
    /// A placement content calls a counter and no definition describes is a shop that cannot be entered —
    /// and nothing at runtime could tell it from a person who is talked to for something else. It is named
    /// here, while the session is composed, rather than discovered by a player walking into a dead door.
    /// A household is not judged: a residence is a person the party can speak to, and the table's own row is
    /// all that describes it.
    /// </remarks>
    private static Dictionary<(string Place, string Placement), ServiceHousehold> ValidatePlacements(
        ContentCatalog catalog,
        Dictionary<ServiceId, ServiceDefinition> services,
        List<ContentValidationIssue> issues)
    {
        Dictionary<(string, string), ServiceHousehold> households = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in entry.GetArray(PlacePopulationContent.PlacementsField))
            {
                string kind = ContentEntry.ReadString(placement, PlacePopulationContent.KindField);
                string named = ContentEntry.ReadId(placement, "houseId");
                if (named.Length == 0) named = ContentEntry.ReadId(placement, PlacePopulationContent.IdField);
                if (string.Equals(kind, ResidencePlacementKind, StringComparison.Ordinal))
                {
                    // A household is described by the table's own row, which the placement carries: what the
                    // building is called, who the table names in it, and what it called them.
                    string name = ContentEntry.ReadString(placement, "name");
                    households[(entry.Id, ContentEntry.ReadId(placement, PlacePopulationContent.IdField))] = new ServiceHousehold(
                        new ServiceId(named.Length > 0 ? named : entry.Id),
                        name.Length > 0 ? name : named,
                        ContentEntry.ReadString(placement, "proprietor"),
                        ContentEntry.ReadString(placement, "fixture"));
                    continue;
                }

                if (!string.Equals(kind, PlacementKind, StringComparison.Ordinal)) continue;
                if (named.Length > 0 && services.ContainsKey(new ServiceId(named))) continue;
                issues.Add(new ContentValidationIssue(
                    "service-placement-unknown",
                    $"place '{entry.Id}' places a service counter named '{named}', which no service entry describes.",
                    pack.PackId,
                    document.DocumentId));
            }
        }

        return households;
    }

    /// <summary>The encounter rows the places carry, keyed by the place a counter stands in.</summary>
    /// <remarks>
    /// A region's own row of the per-map table names the monsters that live there, which is what a town
    /// hall's bounty is drawn from. A place that names none is a place with no bounty to post, rather than a
    /// hall that invents a beast.
    /// </remarks>
    private static Dictionary<int, IReadOnlyList<string>> ReadPlaceMonsters(ContentCatalog catalog)
    {
        Dictionary<int, IReadOnlyList<string>> places = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            List<string> monsters = [];
            foreach (JsonElement element in entry.GetArray("monsters"))
            {
                if (element.ValueKind == JsonValueKind.String && element.GetString() is { Length: > 0 } name) monsters.Add(name);
            }

            if (monsters.Count > 0 && int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int placeId))
            {
                places[placeId] = monsters;
            }
        }

        return places;
    }

    /// <summary>The named destinations of the roads that leave each place, for what a tavern tells.</summary>
    private static Dictionary<int, IReadOnlyList<string>> ReadPlaceRoads(ContentCatalog catalog)
    {
        Dictionary<int, List<string>> places = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(TravelDefinitionKind))
        {
            string from = entry.GetId("fromPlace");
            string to = entry.GetString("toName");
            if (from.Length == 0 || to.Length == 0) continue;
            if (!int.TryParse(from, NumberStyles.None, CultureInfo.InvariantCulture, out int placeId)) continue;
            if (!places.TryGetValue(placeId, out List<string>? roads)) places[placeId] = roads = [];
            if (!roads.Contains(to, StringComparer.Ordinal)) roads.Add(to);
        }

        return places.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }

    /// <summary>The monster table's levels, which a bounty's reward is computed from.</summary>
    private static Dictionary<string, int> ReadMonsterLevels(ContentCatalog catalog)
    {
        Dictionary<string, int> levels = new(StringComparer.Ordinal);
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MonsterDefinitionKind))
        {
            string name = entry.GetString("name");
            if (name.Length == 0) continue;
            levels[name] = entry.GetInt32("level") ?? 1;
        }

        return levels;
    }

    /// <summary>The order an identity states, which is how the table's numeric ids are walked.</summary>
    private static int ParseOrder(string id) =>
        int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int order) ? order : int.MaxValue;

    /// <summary>The operation a content word names, or null when this build has none for it.</summary>
    private static ServiceOperationKind? Operation(string word) => word switch
    {
        "buy" => ServiceOperationKind.Buy,
        "sell" => ServiceOperationKind.Sell,
        "identify" => ServiceOperationKind.Identify,
        "repair" => ServiceOperationKind.Repair,
        "teach" => ServiceOperationKind.Teach,
        "cure" => ServiceOperationKind.Cure,
        "train" => ServiceOperationKind.Train,
        "provision" => ServiceOperationKind.Provision,
        "stay" => ServiceOperationKind.Stay,
        "deposit" => ServiceOperationKind.Deposit,
        "withdraw" => ServiceOperationKind.Withdraw,
        "fare" => ServiceOperationKind.Fare,
        _ => null,
    };

    /// <summary>What the imported item table says one item is worth.</summary>
    /// <param name="Id">The item's own id, in table order.</param>
    /// <param name="Name">What the table calls it.</param>
    /// <param name="Value">The item's value in the table's own units.</param>
    /// <param name="EquipStat">The equipment word the table files it under.</param>
    /// <param name="SkillGroup">The skill the table files it under.</param>
    /// <param name="Material">The material or rarity the table states.</param>
    private readonly record struct ItemFacts(int Id, string Name, int Value, string EquipStat, string SkillGroup, string Material)
    {
        /// <summary>The item's definition identity, which is its id as content spells it.</summary>
        public ItemDefinitionId Definition => new(Id.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>What the imported spell table says about one spell.</summary>
    private readonly record struct SpellFacts(string School, int Level);

    /// <summary>What a passage a counter sells reaches, and how long it takes.</summary>
    /// <param name="Place">The destination place's identity.</param>
    /// <param name="Name">What the destination is called.</param>
    /// <param name="Days">How many game days the journey takes.</param>
    internal readonly record struct ServiceFare(string Place, string Name, int Days);

    /// <summary>What this game knows about one counter besides the definition the mechanism serves.</summary>
    private sealed record ServiceFacts(
        int MapId,
        IReadOnlyList<string> TaughtSkills,
        int GuildRung,
        string Membership,
        string MembershipName,
        string School,
        int TrainingCap,
        int FareDays,
        int ProvisionPortions,
        int StayHours,
        bool HoldsCoins,
        IReadOnlyList<ServiceFare> Fares);
}

/// <summary>Who lives in a placement that is a household rather than a counter.</summary>
/// <remarks>
/// The building table's own row describes it: what the table called the building, who it names as living
/// there, and what it called them. What a household offers the party — an odd job, a guild member to
/// recruit, a master teacher — belongs to the quest, follower, and teaching owners, and this states who
/// answers the door so the one interaction mechanism has something true to say.
/// </remarks>
/// <param name="Id">The household's identity, which is the building row's own id.</param>
/// <param name="Name">What the table calls the building.</param>
/// <param name="Proprietor">Who the table names, or empty when it names nobody.</param>
/// <param name="Fixture">The table's own type word, which says what sort of building it is.</param>
internal sealed record ServiceHousehold(ServiceId Id, string Name, string Proprietor, string Fixture)
{
    /// <summary>How the household reads to a person: its name, and who the table names in it.</summary>
    public string Describe() => Proprietor.Length > 0 ? $"{Name}, where {Proprietor} lives" : Name;
}
