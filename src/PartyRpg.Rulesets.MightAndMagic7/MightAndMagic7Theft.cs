using System.Globalization;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What this game makes of a theft: who may try, how a hand is measured against what being seen would cost, what
/// comes away, what the town fines the party and how far its opinion falls, and how long a counter stays shut.
/// </summary>
/// <remarks>
/// <para>
/// <b>Who may try is the donor's.</b> A character steals only with the Stealing skill learned and in a state to
/// act (OpenEnroth <c>src/Engine/Objects/Character.cpp:360-362</c>, <c>CanSteal</c>, and
/// <c>src/Io/Mouse.cpp:281-283</c>); from a counter's shelves only at the four shops whose screen offers the
/// shelf to a thief (<c>src/GUI/UI/Houses/Shops.cpp:1100-1137</c> — the weapon, armour, and magic shops and the
/// alchemist; a guild's books are sold by another screen that has no theft), and from a person standing in the
/// world.
/// </para>
/// <para>
/// <b>The measure is the donor's.</b> A thief's reach is one of the donor's five draws — two hundred under to two
/// hundred over — plus the skill's level times its rung's figure, a hundred, two hundred, three hundred, or five
/// hundred (<c>Character.cpp:110-118</c>). At a counter, what being caught would cost is a hundred for every point
/// of the party's standing in the donor's sign plus the place's own base fine, and the line's worth on top — three
/// times its worth for a weapon (<c>Character.cpp:1168-1174</c>). One theft in twenty is seen whatever the
/// measures say; otherwise a reach that covers the cost goes unseen, one short of it by less than five hundred is
/// seen with the goods already in hand, and one shorter still is seen empty-handed (<c>Character.cpp:1180-1192</c>).
/// From a person the cost is the person's own level plus a hundred for every point of the place's base fine and the
/// party's standing, and a thief who falls short is seen (<c>Character.cpp:1211-1214</c>); one who is not finds coin
/// three times in ten, something else three times in ten, and nothing the rest (<c>Character.cpp:1220-1279</c>).
/// Coin comes in the skill's level of dice whose sides the rung decides — two, four, six, or ten
/// (<c>Character.cpp:120-129</c>) — never more than the person carries.
/// </para>
/// <para>
/// <b>What it costs is the donor's, carried as a debt.</b> A thief seen at a counter adds the cost to the party's
/// fine — kept, as the donor keeps it, between nothing and four million (<c>Shops.cpp:1150</c>) — lowers the
/// world's opinion one point, two when the goods came away with the thief, and shuts the counter against the party
/// for a day (<c>Shops.cpp:1147-1166</c>); a theft nobody saw still lowers the opinion two points, because the town
/// notices what is missing (<c>Shops.cpp:1165-1171</c>). What a counter's shelf gives up carries the stolen mark
/// (<c>Shops.cpp:1123</c>), which a counter refuses to buy, identify, or repair (<c>src/Engine/Objects/Item.cpp:684-686</c>).
/// Every attempt on a person lowers the opinion one point whatever comes of it — the donor's own
/// <c>reputation++</c> beside the attempt (<c>src/Engine/Objects/Actor.cpp:1224-1241</c>). The fine is owed on
/// <see cref="FineAccount"/>, which a town hall collects (<c>src/GUI/UI/Houses/TownHall.cpp:30-45</c>).
/// </para>
/// <para>
/// <b>What is ours.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// A person who catches a thief fines the party what the donor reckons and never charges: the donor works the
/// figure out and only turns the peasants on the thief (<c>Actor::AggroSurroundingPeasants</c>,
/// <c>Character.cpp:1214-1218</c>). The turning is the donor's: the session puts the person robbed into the fight,
/// and the fight's provocation carries it to every actor of their faction within 4,096 units
/// (<see cref="MightAndMagic7Combat.ProvokedWith"/>).
/// </description></item>
/// <item><description>
/// What a person carries is what their row would leave if they fell — the row's coin dice and its treasure draw,
/// the same reading the donor's <c>SetRandomGoldIfTheresNoItem</c> makes (<c>Actor.cpp:192-212</c>) — drawn once
/// per person and remembered by the world's interaction ledger, including across saves. A place's clocked
/// restoration forgets the purse with its other memories, so the next visit draws it afresh.
/// </description></item>
/// <item><description>
/// A counter's line is worth the item table's own value; the donor reads the generated item's value, enchantment
/// included, and this game's shelves stock no enchantment.
/// </description></item>
/// <item><description>
/// The time a theft takes — the donor charges the thief an attack's recovery (<c>Actor.cpp:1237-1240</c>) — is
/// not charged: a theft is an act outside the fight here.
/// </description></item>
/// </list>
/// </remarks>
internal sealed class MightAndMagic7Theft
{
    /// <summary>The debt account a fine is owed on, which a town hall collects.</summary>
    internal const string FineAccount = "fine";

    /// <summary>The words a person reads for the account a fine is owed on.</summary>
    internal const string FineLabel = "the party's fine";

    /// <summary>The place field the importer carries the map table's base fine on.</summary>
    internal const string StealFineField = "stealFine";

    /// <summary>The kind of definition a place is.</summary>
    internal const string PlaceDefinitionKind = "place";

    /// <summary>The deed a thief seen empty-handed at a counter is credited as.</summary>
    internal const string CaughtSource = "theft-caught";

    /// <summary>The deed a thief seen with the goods in hand is credited as.</summary>
    internal const string CaughtWithGoodsSource = "theft-caught-with-goods";

    /// <summary>The deed a theft nobody saw is credited as.</summary>
    internal const string UnseenSource = "theft-unseen";

    /// <summary>The deed any attempt on a person's purse is credited as.</summary>
    internal const string PickpocketSource = "pickpocket";

    /// <summary>How far being seen empty-handed at a counter moves the world's opinion: the donor's one point.</summary>
    internal const int CaughtReputation = -1;

    /// <summary>How far being seen with the goods, or a theft nobody saw, moves it: the donor's two points.</summary>
    internal const int TakenReputation = -2;

    /// <summary>How far any attempt on a person's purse moves it: the donor's one point.</summary>
    internal const int PickpocketReputation = -1;

    /// <summary>The skill a theft is judged by.</summary>
    internal static readonly SkillId Stealing = new("Stealing");

    /// <summary>The scope a theft's draws are keyed under.</summary>
    internal const string RollScope = "mm7.theft";

    /// <summary>The seed a theft's draws are keyed under.</summary>
    internal const ulong RollSeed = 0x7EF7_0000_5EE9_0005;

    /// <summary>A thief's reach per level of the skill at each rung, none to grand master (the donor's figures).</summary>
    private static readonly int[] RungReach = [0, 100, 200, 300, 500];

    /// <summary>The donor's five draws of a thief's luck, one of which every theft adds to its reach.</summary>
    private static readonly int[] LuckReach = [-200, -100, 0, 100, 200];

    /// <summary>The sides of the dice a pickpocket's coin is counted in at each rung, none to grand master.</summary>
    private static readonly int[] CoinSides = [0, 2, 4, 6, 10];

    /// <summary>How often a thief is seen whatever the measures say, in percent (the donor's one in twenty).</summary>
    private const int SeenAnyway = 5;

    /// <summary>How far short of the cost a counter's thief may fall and still come away with the goods.</summary>
    private const int WithGoodsMargin = 500;

    /// <summary>The draw, out of a hundred, from which a pickpocket finds coin (the donor's seventy).</summary>
    private const int CoinFrom = 70;

    /// <summary>The draw, out of a hundred, from which a pickpocket finds something besides coin (the donor's forty).</summary>
    private const int ThingFrom = 40;

    private readonly Dictionary<string, int> _baseFines;
    private readonly IRandomService? _random;
    private readonly MightAndMagic7Loot? _loot;
    private readonly Func<PlacementDefinition, int?> _personLevel;
    private readonly TuningProfile _tuning;
    private readonly Func<InteractionLedger> _states;
    private long _draws;

    private MightAndMagic7Theft(
        Dictionary<string, int> baseFines,
        IRandomService? random,
        MightAndMagic7Loot? loot,
        Func<PlacementDefinition, int?> personLevel,
        TuningProfile tuning,
        Func<InteractionLedger> states)
    {
        _baseFines = baseFines;
        _random = random;
        _loot = loot;
        _personLevel = personLevel;
        _tuning = tuning;
        _states = states;
    }

    /// <summary>Reads this game's theft rule over the places content carries.</summary>
    /// <param name="catalog">The validated content, or null for a product that loaded none.</param>
    /// <param name="random">The engine's random service, or null for a product that cannot draw: no theft is then tried.</param>
    /// <param name="loot">What a person carries is read from, or null when this product reads no loot.</param>
    /// <param name="personLevel">The level of the row a person fights as, or null when nothing reads one.</param>
    /// <param name="states">The world ledger; standalone policy callers may omit it and retain one local ledger.</param>
    /// <returns>The rule.</returns>
    internal static MightAndMagic7Theft Read(
        ContentCatalog? catalog,
        IRandomService? random = null,
        MightAndMagic7Loot? loot = null,
        Func<PlacementDefinition, int?>? personLevel = null,
        Func<InteractionLedger>? states = null)
    {
        Dictionary<string, int> fines = new(StringComparer.Ordinal);
        if (catalog is not null)
        {
            foreach ((_, _, ContentEntry place) in catalog.Entries(PlaceDefinitionKind))
            {
                if (place.GetInt32(StealFineField) is { } fine && fine > 0) fines[place.Id] = fine;
            }
        }

        // Standalone policy callers use the same ledger owner as a session, rather than a second purse model.
        InteractionLedger? standalone = states is null ? new InteractionLedger() : null;
        return new MightAndMagic7Theft(fines, random, loot, personLevel ?? (_ => null), MightAndMagic7Tuning.Read(catalog), states ?? (() => standalone!));
    }

    /// <summary>The base fine of a place: the map table's own column, which the importer carries onto it, or nothing.</summary>
    /// <param name="place">The place.</param>
    internal int BaseFine(PlaceId place) => _baseFines.GetValueOrDefault(place.Value);

    /// <summary>How much more a fine adds to what is owed, kept as the donor keeps the whole between nothing and its ceiling.</summary>
    /// <param name="owed">What the party owes on the fine account now.</param>
    /// <param name="fine">What the deed is fined.</param>
    /// <returns>What the fine adds, which is never negative and never takes the whole past the ceiling.</returns>
    internal static int Added(int owed, int fine)
    {
        long total = Math.Clamp((long)owed + Math.Max(0, fine), 0, MightAndMagic7Crimes.FineCeiling);
        return (int)Math.Max(0, total - owed);
    }

    /// <summary>Whether a member could try a theft from what the request names, drawing nothing.</summary>
    /// <param name="request">The counter or the person, and the member.</param>
    /// <returns>Why not, or null when they could.</returns>
    internal Refusal? Judge(ServiceTheftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Service is { } counter && !counter.Offers(ServiceOperationKind.Steal))
        {
            return new Refusal(MightAndMagic7Codes.TheftNotAShop, $"{counter.Describe()} keeps nothing on a shelf a thief could reach.");
        }

        if (request.Service is null && (request.Person is not { } person || !MightAndMagic7Combat.IsPerson(person)))
        {
            return new Refusal(MightAndMagic7Codes.TheftNobodyToRob, "Nobody the party stands with carries a purse a hand could reach.");
        }

        PartyMember thief = request.Party.Member(request.Member);
        if (!thief.Skills.Knows(Stealing) || thief.Skills.LevelOf(Stealing) < 1)
        {
            return new Refusal(MightAndMagic7Codes.TheftNoSkill, $"{thief.Profile.Name} has not learned to steal.");
        }

        if (!MightAndMagic7Conditions.CanAct(thief))
        {
            return new Refusal(MightAndMagic7Codes.TheftCannotAct, $"{thief.Profile.Name} is in no state to reach for anything.");
        }

        return _random is null
            ? new Refusal(MightAndMagic7Codes.TheftChanceUnavailable, "Whether a thief is seen is chance, and this product has no random service to draw it.")
            : null;
    }

    /// <summary>Draws what one theft from a counter's shelf comes to.</summary>
    /// <param name="request">The counter, the line, the member, and the party.</param>
    /// <param name="worth">What the line is worth to a thief: its value, three times over for a weapon.</param>
    /// <param name="place">The place the counter stands in, whose base fine the cost includes.</param>
    /// <returns>The outcome, or why it could not be tried.</returns>
    internal ServiceTheft AtCounter(ServiceTheftRequest request, int worth, PlaceId place)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Judge(request) is { } refused) return ServiceTheft.Refused(refused);
        if (request.Subject?.Lot is not { } lot)
        {
            return ServiceTheft.Refused(new Refusal(MightAndMagic7Codes.TheftNotAShop, $"{request.Service!.Describe()} was not asked for a line of its shelves."));
        }

        PartyMember thief = request.Party.Member(request.Member);
        KeyedRolls rolls = Rolls($"counter/{request.Service!.Id}/{thief.Id}", request.Clock);
        int reach = Reach(thief, rolls);
        long cost = Math.Max(0, (100L * (Standing(request.Party) + BaseFine(place))) + worth);
        int fine = (int)Math.Min(cost, MightAndMagic7Crimes.FineCeiling);
        int owed = request.Party.Debts.OwedOn(FineAccount);
        GameDuration ban = GameDuration.FromHours(_tuning.Whole(MightAndMagic7Tuning.TheftBanHours));
        string name = thief.Profile.Name;

        bool seenAnyway = rolls.Roll("seen", 1, 100) <= SeenAnyway;
        if (!seenAnyway && cost <= reach)
        {
            return ServiceTheft.Tried(
                caught: false,
                taken: true,
                coins: 0,
                items: null,
                marked: true,
                fine: 0,
                account: FineAccount,
                deed: UnseenSource,
                ban: GameDuration.None,
                message: $"{name} takes {lot.Label} from the shelf unseen.");
        }

        if (!seenAnyway && cost - reach < WithGoodsMargin)
        {
            return ServiceTheft.Tried(
                caught: true,
                taken: true,
                coins: 0,
                items: null,
                marked: true,
                fine: Added(owed, fine),
                account: FineAccount,
                deed: CaughtWithGoodsSource,
                ban: ban,
                message: $"{name} gets away with {lot.Label}, but is seen doing it.");
        }

        return ServiceTheft.Tried(
            caught: true,
            taken: false,
            coins: 0,
            items: null,
            marked: false,
            fine: Added(owed, fine),
            account: FineAccount,
            deed: CaughtSource,
            ban: ban,
            message: $"{name} is caught reaching for {lot.Label}.");
    }

    /// <summary>Draws what one attempt on a person's purse comes to.</summary>
    /// <param name="request">The person, the place they stand in, the member, and the party.</param>
    /// <returns>The outcome, or why it could not be tried.</returns>
    internal ServiceTheft FromPerson(ServiceTheftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Judge(request) is { } refused) return ServiceTheft.Refused(refused);
        PlacementDefinition person = request.Person!;
        PartyMember thief = request.Party.Member(request.Member);
        string name = thief.Profile.Name;
        string key = $"{request.Place}/{person.Content}";
        KeyedRolls rolls = Rolls($"person/{key}/{thief.Id}", request.Clock);
        int reach = Reach(thief, rolls);
        long cost = Math.Max(0, (_personLevel(person) ?? 0) + (100L * (BaseFine(request.Place) + Standing(request.Party))));

        bool seen = rolls.Roll("seen", 1, 100) <= SeenAnyway || cost > reach;
        if (seen)
        {
            int fine = (int)Math.Min(cost, MightAndMagic7Crimes.FineCeiling);
            return ServiceTheft.Tried(
                caught: true,
                taken: false,
                coins: 0,
                items: null,
                marked: false,
                fine: Added(request.Party.Debts.OwedOn(FineAccount), fine),
                account: FineAccount,
                deed: PickpocketSource,
                ban: GameDuration.None,
                message: $"{name} is caught with a hand in somebody's purse.");
        }

        InteractionLedger states = _states();
        PlacementPurseSnapshot purse = PurseOf(request.Place, key, person, states);
        int found = rolls.Roll("find", 0, 99);
        if (found >= CoinFrom && purse.Coins > 0)
        {
            int rung = Math.Min(thief.Skills.TierOf(Stealing).Value, CoinSides.Length - 1);
            int lifted = Math.Min(purse.Coins, rolls.Dice(thief.Skills.LevelOf(Stealing), CoinSides[rung]));
            if (lifted > 0)
            {
                states.KeepPurse(request.Place, purse with { Coins = purse.Coins - lifted });
                return Lifted(lifted, null, $"{name} lifts {lifted.ToString(CultureInfo.InvariantCulture)} coin(s) unseen.");
            }
        }
        else if (found >= ThingFrom && found < CoinFrom && Handed(request.Party, person) is { } handed)
        {
            // What the person carries — the item their own map record starts them with, or one an event gave them — is
            // what a hand finds first (OpenEnroth src/Engine/Objects/Character.cpp:1254-1260), and it is theirs no longer.
            MightAndMagic7PersonState.Take(request.Party.Records, handed.Person, handed.Item, MightAndMagic7PersonState.Starting(person));
            ItemDefinitionId thing = new(handed.Item.ToString(CultureInfo.InvariantCulture));
            string label = _loot?.NameOf(thing) ?? thing.Value;
            return Lifted(0, [thing], $"{name} lifts {label} unseen.");
        }
        else if (found >= ThingFrom && found < CoinFrom && purse.Items.Count > 0)
        {
            ItemDefinitionId thing = purse.Items[0];
            states.KeepPurse(request.Place, purse with { Items = [.. purse.Items.Skip(1)] });
            string label = _loot?.NameOf(thing) ?? thing.Value;
            return Lifted(0, [thing], $"{name} lifts {label} unseen.");
        }

        return Lifted(0, null, $"{name} goes unseen and finds nothing worth taking.");
    }

    /// <summary>An attempt on a person nobody saw, with what came of it.</summary>
    private static ServiceTheft Lifted(int coins, IReadOnlyList<ItemDefinitionId>? items, string message) =>
        ServiceTheft.Tried(
            caught: false,
            taken: false,
            coins: coins,
            items: items,
            marked: false,
            fine: 0,
            account: FineAccount,
            deed: PickpocketSource,
            ban: GameDuration.None,
            message: message);

    /// <summary>A thief's reach: one of the donor's five draws of luck, and the skill's level times its rung's figure.</summary>
    private static int Reach(PartyMember thief, KeyedRolls rolls)
    {
        int rung = Math.Min(thief.Skills.TierOf(Stealing).Value, RungReach.Length - 1);
        return LuckReach[rolls.Pick(LuckReach.Length)] + (thief.Skills.LevelOf(Stealing) * RungReach[rung]);
    }

    /// <summary>The party's standing in the donor's own sign, in which a higher number is a worse one.</summary>
    private static int Standing(PartyEntity party) => -party.Reputation.Reputation;

    /// <summary>The draws of one attempt, keyed so no two attempts share them.</summary>
    private KeyedRolls Rolls(string what, GameClock? clock)
    {
        _draws++;
        long now = clock?.Elapsed.Milliseconds ?? 0;
        return new KeyedRolls(
            _random!,
            RollSeed,
            RollScope,
            string.Create(CultureInfo.InvariantCulture, $"{what}/{now}/{_draws}"));
    }

    /// <summary>
    /// The first item one of the people a placement stands for carries — the one the placement's own record starts them
    /// with, then one an event gave them — with whose it is, or null.
    /// </summary>
    /// <param name="party">The party, whose records keep what events gave whom and took from whom (<see cref="MightAndMagic7PersonState"/>).</param>
    /// <param name="person">The person's placement.</param>
    internal static (string Person, int Item)? Handed(PartyEntity party, PlacementDefinition person)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(person);
        foreach (string id in MightAndMagic7Conversation.PeopleOf(person))
        {
            if (MightAndMagic7PersonState.Carried(party.Records, id, MightAndMagic7PersonState.Starting(person)) is [var first, ..]) return (id, first);
        }

        return null;
    }

    /// <summary>What a person carries, drawn the first time a hand reaches for it and remembered after.</summary>
    private PlacementPurseSnapshot PurseOf(PlaceId place, string key, PlacementDefinition person, InteractionLedger states)
    {
        if (states.PurseOf(place, person.Content) is { } held) return held;
        LootYield carried = _loot?.RollsFor($"purse/{key}") is { } rolls ? _loot.Death(person, rolls) : LootYield.Nothing;
        List<ItemDefinitionId> items = [];
        foreach (LootItem item in carried.Items)
        {
            for (int count = 0; count < item.Count; count++) items.Add(item.Definition);
        }
        PlacementPurseSnapshot purse = new(person.Content, carried.Coins, items);
        states.KeepPurse(place, purse);
        return purse;
    }
}
