using PartyRpg.Kit;
using System.Text.RegularExpressions;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's own answers to the kit's cost and larder seams, and the party its content describes.
/// </summary>
/// <remarks>
/// <para>
/// The ruleset's policy types are internal because nothing outside the product composes them, so this
/// suite reaches them through the ruleset's own friend declaration. What it proves is what no kit test
/// can: the donor-cited numbers this game states — a day on the road, a ration a day, the condition
/// hunger puts on a member — and that the party a scenario declares is the party the product leads.
/// </para>
/// <para>
/// Every claim about the donor in the code under test is checked here as a shape rather than as a copy of
/// its source: a day is one ration, a walk quotes what the manual's overland crossing quotes, and a
/// crossing that cannot be walked into one of the game's kinds is refused by name.
/// </para>
/// </remarks>
public sealed class TravelPolicyTests
{
    [Fact]
    public void A_fall_takes_the_donors_share_of_each_members_own_health()
    {
        // The donor's arithmetic: the whole distance, times a tenth of the member's maximum health, over 256.
        // A member who can take 40 and falls 600 loses (600 × 4) / 256 = 9; one who can take 95 loses 21.
        using PartyEntity party = new PartyEntityFactory().Create(new PartyCreation(
            [Member("Roderick", 40), Member("Ysolde", 95)],
            coins: 0,
            foodPortions: 0,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));
        PartyRpg.Kit.Movement.FallOutcome fall = new(Distance: 600, Excess: 600 - 512, Damage: 0);

        IFallRule falls = MightAndMagic7Movement.Falls(party);
        Assert.Equal(9, falls.DamageTo(party.Members[0], fall));
        Assert.Equal(21, falls.DamageTo(party.Members[1], fall));

        // A feather fall the party carries spares every member the landing (OpenEnroth
        // src/Engine/Graphics/Outdoor.cpp:1426, !partyHasFeatherFall).
        new PartyRpg.Kit.Magic.RunningSpellEffects(party).Start(SpellEffectIds.FeatherFall, magnitude: 1, lasts: null);
        Assert.Equal(0, falls.DamageTo(party.Members[0], fall));
        Assert.Equal(0, falls.DamageTo(party.Members[1], fall));

        static MemberCreation Member(string name, int hitPoints) => new(new PartyMemberSeed(
            name,
            new RaceId("human"),
            new ClassId("knight"),
            [new AttributeScore(new AttributeId("Might"), 13)],
            skills: [],
            spells: [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(hitPoints),
            spellPoints: ResourcePool.Full(0)));
    }

    private static readonly PlaceId Home = new("1");

    [Fact]
    public void Walking_a_road_costs_a_day_on_it_and_the_rations_that_day_eats()
    {
        ContentCatalog catalog = Catalog(World());
        PlaceGraph graph = PlaceGraphLoader.Load(catalog, MightAndMagic7FareDays.Read(catalog));
        PlaceTransition road = Assert.Single(graph.TransitionsFrom(Home));
        MightAndMagic7TravelCostRule rule = new();

        // Both ways of walking a road are the same journey, and both are quoted as one: the manual's
        // overland crossing lasts days and eats a food unit a day, so the quote is a day and that day's
        // rations rather than nothing at all.
        foreach (TransitionKind kind in new[] { TransitionKind.Walking, TransitionKind.Entrance })
        {
            TravelCostQuote quote = rule.Quote(new TransitionRequest(graph, road, kind, Home, PlacePose.Origin));

            Assert.Null(quote.Refusal);
            Assert.Equal(new TravelTime(MightAndMagic7TravelCostRule.DaysPerCrossing, TravelTimeUnit.Days), quote.Cost.Time);
            Assert.Equal(
                MightAndMagic7Provisions.RationsPerDay * MightAndMagic7TravelCostRule.DaysPerCrossing,
                quote.Cost.Food.Amount);
            Assert.Equal(ProvisionUnit.Portions, quote.Cost.Food.Unit);
        }

        // A scripted move is the world placing the party, not a journey it made, so it quotes nothing.
        TravelCostQuote scripted = rule.Quote(new TransitionRequest(graph, road, TransitionKind.Scripted, Home, PlacePose.Origin));
        Assert.True(scripted.Cost.IsFree);
    }

    [Fact]
    public void A_fare_is_bought_at_a_counter_and_the_road_honours_exactly_what_it_reaches()
    {
        ContentCatalog catalog = Catalog(Stabled(PartyDocument(food: 6)));
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlaceTransition road = Assert.Single(graph.TransitionsFrom(Home), transition => transition.IsFare);
        using PartyEntity party = MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The scenario declares a party, so composing it must produce one.");
        MightAndMagic7TravelCostRule rule = new(party);

        // No passage: a paid transition is refused by name rather than taken free, and nothing about the
        // party changes.
        Refusal unpaid = rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin)).Refusal!;
        Assert.Equal(MightAndMagic7Codes.TravelFareUnpaid, unpaid.Code);
        Assert.Contains($"{road.To}", unpaid.Message, StringComparison.Ordinal);

        // A passage to somewhere else does not pay for this journey: a ticket names the place it reaches.
        party.Passages.Hold(new PlaceId("99"), MightAndMagic7FareDays.CoachRoute);
        Assert.Equal(
            "travel-fare-unpaid",
            rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin)).Refusal!.Code);

        // Nor does a boat ticket to the right place pay for the coach: a ticket names the route it was sold on.
        party.Passages.Hold(road.To, MightAndMagic7FareDays.BoatRoute);
        Assert.Equal(
            "travel-fare-unpaid",
            rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin)).Refusal!.Code);

        // The passage on the coach to the place the road reaches is what pays. Quoting preserves it: the
        // journey quotes the coach's days under the tuning loaded, eats no provisions because the fare included
        // them, and leaves the party holding no ticket for the journey back.
        party.Passages.Hold(road.To, MightAndMagic7FareDays.CoachRoute);
        TravelCostQuote boarded = rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin));
        Assert.Null(boarded.Refusal);
        Assert.Equal(new TravelTime((int)MightAndMagic7Tuning.CoachDays.Default, TravelTimeUnit.Days), boarded.Cost.Time);
        Assert.True(boarded.Cost.Food.IsNone);
        Assert.True(party.Passages.Holds(road.To));

        // A crossing no counter sells is not a bought journey at all, whatever kind of travel names it.
        PlaceTransition walked = Assert.Single(graph.TransitionsFrom(Home), transition => !transition.IsFare);
        Assert.Equal(
            TravelCodes.TravelFareUnstated,
            rule.Quote(new TransitionRequest(graph, walked, TransitionKind.PaidService, Home, PlacePose.Origin)).Refusal!.Code);
    }

    [Fact]
    public void A_passage_is_boarded_at_the_world_and_charges_the_routes_days_exactly_once()
    {
        ContentCatalog catalog = Catalog(Stabled(PartyDocument(food: 6)));
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlaceTransition coach = graph.Transitions.Single(transition => transition.Source == "fare-coach-1-2");
        Assert.True(coach.IsFare);

        using PartyEntity party = MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The scenario declares a party, so composing it must produce one.");
        PartyResourceLedger accounts = new(party, provisioning: new MightAndMagic7Provisions());
        GameClock clock = TestClock.Create();
        PartyPoseOwner owner = new(
            new PartyPose(Home, PlacePose.Origin),
            new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        using SessionWorld world = new(
            graph,
            owner,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new MightAndMagic7TravelCostRule(party),
            time: clock,
            clock: clock,
            resources: accounts,
            partyEntity: party);

        // The stable sells the coach; boarding it moves the party to the place the passage reaches, charges
        // the coach's days on the one clock, and tears the ticket.
        party.Passages.Hold(new PlaceId("2"), MightAndMagic7FareDays.CoachRoute);
        TransitionResult boarded = world.Board(new PlaceId("2"));

        int days = (int)MightAndMagic7Tuning.CoachDays.Default;
        Assert.True(boarded.Arrived);
        Assert.Equal(new PlaceId("2"), world.Place);
        Assert.Equal(new PlacePose(1, 2, 3, 0, 0), world.Party.PlacePose);
        Assert.Equal(days, clock.ElapsedGameDays);
        Assert.False(party.Passages.Holds(new PlaceId("2")));

        // The larder is untouched: a fare includes the journey's board, which is the donor's own reading of
        // a coach journey.
        Assert.Equal(6, party.Food.Portions);

        // Back at the counter, boarding again has no ticket behind it: the journey is refused by name, the
        // party stays where it stands, and the clock does not move a second time.
        world.ArriveAt(Home, PlacePose.Origin);
        TransitionResult again = world.Board(new PlaceId("2"));

        Assert.False(again.Arrived);
        Assert.Equal("travel-fare-unpaid", again.Refusal!.Code);
        Assert.Equal(days, clock.ElapsedGameDays);
        Assert.Equal(Home, world.Place);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_refused_paid_arrival_preserves_the_ticket_and_accounts_and_can_be_retried(bool groundRefuses)
    {
        ContentCatalog catalog = Catalog(Stabled(PartyDocument(food: 6)));
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlaceId destination = new("2");
        using PartyEntity party = MightAndMagic7Party.Compose(catalog)!;
        party.Passages.Hold(destination, MightAndMagic7FareDays.CoachRoute);
        PartyResourceLedger accounts = new(party, provisioning: new MightAndMagic7Provisions());
        GameClock clock = TestClock.Create();
        bool poseRefuses = !groundRefuses;
        PartyPoseOwner owner = new(
            new PartyPose(Home, PlacePose.Origin),
            new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512),
            (PlaceId place, PlacePose pose, out PlacePose adjusted) =>
            {
                adjusted = pose;
                return place != destination || !poseRefuses;
            });
        RecordingMover mover = RecordingMover.Standing(owner);
        mover.Refuses = groundRefuses ? destination : null;
        using SessionWorld world = new(
            graph, owner, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new MightAndMagic7TravelCostRule(party), mover: mover, clock: clock,
            resources: accounts, partyEntity: party);
        PartyPose before = owner.Capture();
        long coins = party.Purse.Coins;

        TransitionResult refused = world.Board(destination);

        Assert.False(refused.Arrived);
        Assert.Equal(groundRefuses ? TravelCodes.PlaceGroundRefused : TravelCodes.PlaceRefusedArrival, refused.Refusal!.Code);
        Assert.Equal(before, owner.Capture());
        Assert.Equal(MightAndMagic7FareDays.CoachRoute, party.Passages.RouteTo(destination));
        Assert.Equal(0, clock.ElapsedGameDays);
        Assert.Equal(6, party.Food.Portions);
        Assert.Equal(coins, party.Purse.Coins);

        poseRefuses = false;
        mover.Refuses = null;
        Assert.True(world.Board(destination).Arrived);
        Assert.Equal(destination, owner.Place);
        Assert.False(party.Passages.Holds(destination));
        Assert.Equal((int)MightAndMagic7Tuning.CoachDays.Default, clock.ElapsedGameDays);
        Assert.Equal(6, party.Food.Portions);
        Assert.Equal(coins, party.Purse.Coins);

        world.ArriveAt(Home, PlacePose.Origin);
        Assert.Equal(MightAndMagic7Codes.TravelFareUnpaid, world.Board(destination).Refusal!.Code);
        Assert.Equal((int)MightAndMagic7Tuning.CoachDays.Default, clock.ElapsedGameDays);
    }

    [Fact]
    public void A_passage_saved_under_one_tuning_is_boarded_under_another_at_the_new_length()
    {
        // A ticket is bought and saved while the coach takes this game's default two days, and the session is
        // resumed over the same content under a tuning pack that makes it five. The ticket carries the route,
        // not the days, so the resumed world still sells the journey it names and charges today's five.
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, Stabled(PartyDocument(food: 6)));
        using (IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui)))
        {
            session.Start();
            PartyEntity party = ((MightAndMagic7Session)session).Party!;
            party.Passages.Hold(new PlaceId("2"), MightAndMagic7FareDays.CoachRoute);
            SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
            Assert.Equal(new PartyPassage(new PlaceId("2"), MightAndMagic7FareDays.CoachRoute), Assert.Single(saved.Party.Passages));
        }

        const int Retuned = 5;
        Assert.NotEqual(Retuned, (int)MightAndMagic7Tuning.CoachDays.Default);
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = RulesetTestContext.Create(
            persistence,
            [.. Stabled(PartyDocument(food: 6)).Where(file => !file.Path.EndsWith("bundle.json", StringComparison.Ordinal)), .. CoachTuning(Retuned)]);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume });
        resumed.Start();

        SessionWorld world = ((MightAndMagic7Session)resumed).World!;
        PartyEntity carried = ((MightAndMagic7Session)resumed).Party!;
        Assert.Equal(MightAndMagic7FareDays.CoachRoute, carried.Passages.RouteTo(new PlaceId("2")));
        TransitionResult boarded = world.Board(new PlaceId("2"));

        Assert.True(boarded.Arrived, boarded.Refusal?.ToString());
        Assert.Equal(new TravelTime(Retuned, TravelTimeUnit.Days), boarded.ChargedCost.Time);
        Assert.Equal(new PlaceId("2"), world.Place);
        Assert.False(carried.Passages.Holds(new PlaceId("2")));
    }

    [Fact]
    public void The_fare_network_is_the_rulesets_over_the_counters_content_places()
    {
        // Content places a stable in each of three towns, a dock in two of them, and a stable a place does not
        // stand; it states no destination anywhere. The network is this game's policy over those counters: each
        // stable reaches every other town that keeps a stable and each dock every other port, one crossing per
        // pair of stops on each route, landing at the destination's own start.
        ContentCatalog catalog = Catalog(Network());
        MightAndMagic7FareNetwork network = MightAndMagic7FareNetwork.Read(catalog);
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);

        string[] sold = [.. graph.Transitions.Where(transition => transition.IsFare).Select(transition => $"{transition.FareRoute}:{transition.From}->{transition.To}")];
        Assert.Equal(
            new[]
            {
                "coach:1->2", "coach:1->3", "coach:2->1", "coach:2->3", "coach:3->1", "coach:3->2",
                "boat:1->3", "boat:3->1",
            },
            sold);
        Assert.All(graph.Transitions.Where(transition => transition.IsFare), transition => Assert.Equal(
            transition.FareRoute == MightAndMagic7FareDays.CoachRoute ? (int)MightAndMagic7Tuning.CoachDays.Default : (int)MightAndMagic7Tuning.BoatDays.Default,
            transition.FareDays));

        // Where a passage lands is the destination's own data: its start when it states one, its first arrival
        // point when it states no start.
        Assert.Equal(PlaceArrival.AtEntryPoint("Party Start"), graph.Transitions.Single(transition => transition.Source == "fare-coach-2-1").Arrival);
        Assert.Equal(PlaceArrival.AtEntryPoint("North Start"), graph.Transitions.Single(transition => transition.Source == "fare-coach-1-3").Arrival);

        // A counter's offers are the same reading: the stable in town 1 sells the coach to towns 2 and 3, the
        // dock there the boat to town 3, and the stable nobody placed sells nothing.
        Assert.Equal(new[] { "2:Town:coach", "3:Port:coach" }, network.SoldBy("54").Select(Describe));
        Assert.Equal(new[] { "3:Port:boat" }, network.SoldBy("63").Select(Describe));
        Assert.Empty(network.SoldBy("57"));

        static string Describe(MightAndMagic7FareNetwork.Passage passage) => $"{passage.Place}:{passage.Name}:{passage.Route}";
    }

    [Fact]
    public void Content_that_authors_its_own_sold_crossing_is_refused_by_name()
    {
        // This game's passages are its network's: a pack that still states a sold crossing — one written when the
        // importer derived the network — would sell every journey twice, so it is refused where it is read.
        ContentCatalog catalog = Catalog(
        [
            .. World(PartyDocument(food: 6)).Where(file => !file.Path.EndsWith("links.json", StringComparison.Ordinal)),
            AuthoredFare(),
        ]);
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => MightAndMagic7World.Graph(catalog));
        Assert.Contains(error.Issues, issue => issue.Code == "fare-link-authored" && issue.Message.Contains("'coach'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_passage_counter_standing_in_two_places_is_refused_and_one_with_two_doors_is_one_counter()
    {
        // The counter is what a party asks for its passages, and those leave from where it stands: one counter
        // in two towns would sell from whichever was read first, so it is refused by name, both places named.
        (string Path, string Text)[] twoTowns = Rewrite(
            """{ "id": "service-55", "kind": "service", "houseId": 55, "x": 100, "y": 0, "z": 0 }""",
            """{ "id": "service-54", "kind": "service", "houseId": 54, "x": 50, "y": 0, "z": 0 }, { "id": "service-55", "kind": "service", "houseId": 55, "x": 100, "y": 0, "z": 0 }""");
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => MightAndMagic7FareNetwork.Read(Catalog(twoTowns)));
        ContentValidationIssue issue = Assert.Single(error.Issues);
        Assert.Equal("fare-counter-placed-twice", issue.Code);
        Assert.Contains("'54'", issue.Message, StringComparison.Ordinal);
        Assert.Contains("place '1'", issue.Message, StringComparison.Ordinal);
        Assert.Contains("place '2'", issue.Message, StringComparison.Ordinal);

        // The same counter placed twice where it stands is one counter with two doors, and sells what it sold.
        (string Path, string Text)[] twoDoors = Rewrite(
            """{ "id": "service-63", "kind": "service", "houseId": 63, "x": 200, "y": 0, "z": 0 }""",
            """{ "id": "service-63", "kind": "service", "houseId": 63, "x": 200, "y": 0, "z": 0 }, { "id": "service-54-back", "kind": "service", "houseId": 54, "x": 300, "y": 0, "z": 0 }""");
        Assert.Equal(
            new[] { "2", "3" },
            MightAndMagic7FareNetwork.Read(Catalog(twoDoors)).SoldBy("54").Select(passage => passage.Place.Value));

        static (string Path, string Text)[] Rewrite(string from, string to)
        {
            (string Path, string Text)[] files = Network();
            Assert.Single(files, file => file.Text.Contains(from, StringComparison.Ordinal));
            return [.. files.Select(file => (file.Path, file.Text.Replace(from, to, StringComparison.Ordinal)))];
        }
    }

    [Fact]
    public void Magical_travel_costs_no_road_because_the_spell_already_paid_for_it()
    {
        ContentCatalog catalog = Catalog(World());
        PlaceGraph graph = PlaceGraphLoader.Load(catalog, MightAndMagic7FareDays.Read(catalog));
        PlaceTransition road = Assert.Single(graph.TransitionsFrom(Home));
        MightAndMagic7TravelCostRule rule = new();

        // A portal crosses no ground: the spell that opened it was paid for in spell points, so the crossing
        // itself quotes no day on the road and eats nothing out of the party's larder. What a portal may
        // reach is the casting's own judgement, which the effect path makes before a point is spent.
        TravelCostQuote portal = rule.Quote(new TransitionRequest(graph, road, TransitionKind.Portal, Home, PlacePose.Origin));

        Assert.Null(portal.Refusal);
        Assert.True(portal.Cost.IsFree);
    }

    [Fact]
    public void A_day_eats_one_ration_and_an_empty_larder_weakens_the_party_until_it_rests()
    {
        ContentCatalog catalog = Catalog(World(PartyDocument(food: 2)));
        using PartyEntity party = MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The scenario declares a party, so composing it must produce one.");
        PartyResourceLedger ledger = new(party, provisioning: new MightAndMagic7Provisions());

        // The donor's day takes one unit, and one unit only: the charge does not scale with the two members
        // the scenario declares, because the donor's food store is the party's own single number.
        Assert.Equal(2, party.Members.Count);
        ProvisionDay day = ledger.SpendDay();

        Assert.Equal(MightAndMagic7Provisions.RationsPerDay, day.Charged.Amount);
        Assert.Equal(1, party.Food.Portions);
        Assert.All(party.Members, member => Assert.False(member.Conditions.Has(MightAndMagic7Provisions.Weakness)));

        // The next day spends the larder empty: the party is weakened on the road, exactly as arriving
        // short of food weakens it.
        ProvisionDay hungry = ledger.SpendDay();

        Assert.Equal(1, hungry.Covered);
        Assert.Equal(0, party.Food.Portions);
        Assert.Equal(MightAndMagic7Provisions.Weakness, hungry.Shortage!.Value.Condition);
        Assert.All(party.Members, member => Assert.Equal(1, member.Conditions.SeverityOf(MightAndMagic7Provisions.Weakness)));

        // A fed day is no new shortage, but it does not end the weakness: the donor clears the weak condition
        // only on a full rest (OpenEnroth src/Engine/Party.cpp:698-721), and the same condition may be the
        // fatigue rule's, which a meal must not end. A completed sleep clears it (RestAndScheduleTests).
        ledger.Credit(PartyCost.OfFood(new Provisions(2, ProvisionUnit.Portions)));
        ProvisionDay fed = ledger.SpendDay();

        Assert.Null(fed.Shortage);
        Assert.Equal(1, party.Food.Portions);
        Assert.All(party.Members, member => Assert.Equal(1, member.Conditions.SeverityOf(MightAndMagic7Provisions.Weakness)));
    }

    [Fact]
    public void A_scenario_that_declares_no_party_composes_none_and_one_it_cannot_build_is_refused_by_name()
    {
        // No party content: the product runs with no party rather than with four adventurers the ruleset
        // invented, which is what the world does when content declares no places.
        Assert.Null(MightAndMagic7Party.Compose(Catalog(World())));

        // Content that declares a party but no members is a defect of the scenario, named while the
        // session is composed rather than started as a band one character short.
        ContentValidationException empty = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Party.Compose(Catalog(World(PartyDocument(food: 3, members: 0)))));
        Assert.Contains(empty.Issues, issue => issue.Code == "party-members-missing");

        // A member without the identities creation needs is the same kind of defect.
        ContentValidationException nameless = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Party.Compose(Catalog(World(PartyDocument(food: 3, members: 1, name: string.Empty)))));
        Assert.Contains(nameless.Issues, issue => issue.Code == "party-member-incomplete");
    }

    [Fact]
    public void A_host_without_a_creation_screen_publishes_the_clock_and_the_party_its_scenario_declares()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(World(PartyDocument(food: 6)));

        // A host that declared no creation controls plays the party its scenario fixes — the scripted path.
        // The product itself declares them, so a new product game is created; this is the composition a live
        // check, a test, or a product without creation takes, and it is what exercises the scenario's party.
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        session.Start();

        ProjectedNode published = ProjectedNode.Of(ui.Latest().Value);
        ProjectedNode clock = published.Field("clock");
        Assert.True(clock.Field("present").AsBoolean());
        Assert.Equal("1168-01-01", clock.Field("date").AsString());
        Assert.Equal("09:00", clock.Field("time").AsString());
        Assert.Equal("day", clock.Field("daylight").AsString());
        Assert.Equal(0d, clock.Field("elapsedDays").AsNumber());

        ProjectedNode party = published.Field("party");
        Assert.True(party.Field("present").AsBoolean());
        Assert.Equal(2d, party.Field("members").AsNumber());
        Assert.Equal(200d, party.Field("coins").AsNumber());
        Assert.Equal(6d, party.Field("provisions").AsNumber());
        Assert.Equal("portions", party.Field("unit").AsString());
        Assert.Equal(4d, party.Field("reputation").AsNumber());
        Assert.Equal(2d, party.Field("fame").AsNumber());
        Assert.Equal(string.Empty, party.Field("conditions").AsString());

        // The one admitted update moves the clock: ten admitted seconds are five game minutes at this
        // game's thirty-to-one rate, so the panel's time is the clock's rather than a value kept here.
        session.Update(RulesetTestContext.Update(simulationStep: 0, admittedSteps: 600));
        Assert.Equal("09:05", ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("time").AsString());
    }

    [Fact]
    public void A_host_without_a_creation_screen_runs_without_a_party_when_content_declares_none_but_keeps_the_clock()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(World());

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        session.Start();

        ProjectedNode published = ProjectedNode.Of(ui.Latest().Value);

        // The clock is the ruleset's own policy, so a session keeps time whether or not a scenario declares
        // a party to spend it.
        Assert.True(published.Field("clock").Field("present").AsBoolean());

        // And no party is invented for content that declares none: the panel is told there is none rather
        // than shown an empty purse the product made up.
        Assert.False(published.Field("party").Field("present").AsBoolean());
        Assert.Equal(0d, published.Field("party").Field("coins").AsNumber());
        Assert.Equal(string.Empty, published.Field("party").Field("conditions").AsString());
    }

    [Fact]
    public void A_scenario_whose_party_cannot_be_created_stops_a_host_that_plays_it_by_name()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(World(PartyDocument(food: 6, members: 0)));

        // A party that cannot be built is a defect of the scenario, and a host that plays that party refuses
        // to compose the session rather than leading a band that quietly lost a member. A creating product
        // never reads this document: its party comes from the player, not from the scenario.
        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui)));
        Assert.Contains(error.Issues, issue => issue.Code == "party-members-missing");
    }

    /// <summary>The catalog the product would load from the files a test stages.</summary>
    private static ContentCatalog Catalog(params (string Path, string Text)[] files)
    {
        (ProductCreateContext context, _) = RulesetTestContext.Create(files);
        return ContentCatalogLoader.Load(
            RulesetTestContext.Content(context),
            ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
    }

    /// <summary>A world of two places joined one way, with no party unless a test stages one.</summary>
    private static (string Path, string Text)[] World(params (string Path, string Text)[] extra) =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json", Manifest(
            party: extra.Any(file => file.Path.EndsWith("party.json", StringComparison.Ordinal)),
            services: extra.Any(file => file.Path.EndsWith("services.json", StringComparison.Ordinal)))),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/links.json",
            """
            {
              "documentId": "links",
              "definitionKind": "travel-link",
              "entries": [ { "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        .. extra,
    ];

    /// <summary>A crossing content authors as sold, which this game refuses because its passages are its network's.</summary>
    private static (string Path, string Text) AuthoredFare() =>
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/links.json",
            """
            {
              "documentId": "links",
              "definitionKind": "travel-link",
              "entries": [
                { "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" },
                { "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true, "route": "coach" }
              ]
            }
            """);

    /// <summary>
    /// The world of two places with a stable standing in each, written the way the importer writes counters:
    /// the building's kind and where it stands, and nothing about where it sells passages to.
    /// </summary>
    /// <remarks>
    /// It replaces the world's places document rather than adding a second one, because a root has one
    /// document per id: the places are the same two, now each carrying its stable's placement.
    /// </remarks>
    private static (string Path, string Text)[] Stabled(params (string Path, string Text)[] extra) =>
    [
        .. World(
        [
            .. extra,
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/services.json",
                """
                {
                  "documentId": "services",
                  "definitionKind": "service",
                  "entries": [
                    { "id": "54", "kind": "Stables", "name": "Home Corral", "mapId": 1, "place": "1", "priceMultiplier": 2 },
                    { "id": "55", "kind": "Stables", "name": "Cave Corral", "mapId": 2, "place": "2", "priceMultiplier": 2 }
                  ]
                }
                """),
        ]).Where(file => !file.Path.EndsWith("places.json", StringComparison.Ordinal)),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ],
                  "placements": [ { "id": "service-54", "kind": "service", "houseId": 54, "x": 100, "y": 0, "z": 0 } ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ],
                  "placements": [ { "id": "service-55", "kind": "service", "houseId": 55, "x": 100, "y": 0, "z": 0 } ] }
              ]
            }
            """),
    ];

    /// <summary>Three towns' counters and no destinations: what the fare network is read over.</summary>
    private static (string Path, string Text)[] Network() =>
    [
        .. World(
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/services.json",
                """
                {
                  "documentId": "services",
                  "definitionKind": "service",
                  "entries": [
                    { "id": "54", "kind": "Stables", "name": "Corral", "mapId": 1, "place": "1" },
                    { "id": "55", "kind": "Stables", "name": "Corral", "mapId": 2, "place": "2" },
                    { "id": "56", "kind": "Stables", "name": "Corral", "mapId": 3, "place": "3" },
                    { "id": "57", "kind": "Stables", "name": "Unplaced Corral", "mapId": 3, "place": "3" },
                    { "id": "63", "kind": "Boats", "name": "Dock", "mapId": 1, "place": "1" },
                    { "id": "64", "kind": "Boats", "name": "Dock", "mapId": 3, "place": "3" }
                  ]
                }
                """))
            .Where(file => !file.Path.EndsWith("places.json", StringComparison.Ordinal)),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ],
                  "placements": [
                    { "id": "service-54", "kind": "service", "houseId": 54, "x": 100, "y": 0, "z": 0 },
                    { "id": "service-63", "kind": "service", "houseId": 63, "x": 200, "y": 0, "z": 0 } ] },
                { "id": "2", "kind": "region", "name": "Town", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ],
                  "placements": [ { "id": "service-55", "kind": "service", "houseId": 55, "x": 100, "y": 0, "z": 0 } ] },
                { "id": "3", "kind": "region", "name": "Port", "respawnDays": 1,
                  "entryPoints": [ { "id": "North Start", "x": 4, "y": 5, "z": 6, "yaw": 0 }, { "id": "South Start", "x": 7, "y": 8, "z": 9, "yaw": 0 } ],
                  "placements": [
                    { "id": "service-56", "kind": "service", "houseId": 56, "x": 100, "y": 0, "z": 0 },
                    { "id": "service-64", "kind": "service", "houseId": 64, "x": 200, "y": 0, "z": 0 } ] }
              ]
            }
            """),
    ];

    /// <summary>A bundle naming a tuning pack that states how many days a coach journey takes.</summary>
    private static (string Path, string Text)[] CoachTuning(int days) =>
    [
        ($"{RulesetTestContext.ContentDirectory}/bundles/{RulesetTestContext.BundleId}/bundle.json",
            $$"""
            {
              "schemaVersion": 1,
              "bundleId": "{{RulesetTestContext.BundleId}}",
              "ruleset": "mightandmagic7",
              "contentPacks": [ "world" ],
              "tuningPack": "tuning",
              "description": "test bundle"
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/tuning/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "tuning",
              "kind": "tuning",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "tuning.json", "documentId": "tuning", "definitionKind": "tuning" } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/tuning/tuning.json",
            $$"""{ "documentId": "tuning", "definitionKind": "tuning", "entries": [ { "id": "{{MightAndMagic7Tuning.CoachDays.Id}}", "value": {{days}} } ] }"""),
    ];

    /// <summary>A scenario's party: as many members as a test asks for, and what the purse and larder start with.</summary>
    private static (string Path, string Text) PartyDocument(int food, int members = 2, string name = "Roderick") =>
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party",
                  "coins": 200,
                  "food": {{food}},
                  "reputation": 4,
                  "fame": 2,
                  "members": [
                    {{string.Join(",", Enumerable.Range(0, members).Select(index => Member(index == 0 ? name : $"{name} {index + 1}")))}} 
                  ]
                }
              ]
            }
            """);

    private static string Member(string name) =>
        $$"""
        {
          "name": "{{name}}",
          "race": "Human",
          "class": "Knight",
          "level": 1,
          "hitPoints": 40,
          "spellPoints": 0,
          "attributes": [ { "id": "Might", "value": 13 } ],
          "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
          "spells": [ "Fireball" ],
          "conditions": []
        }
        """;

    private static string Manifest(bool party, bool services)
    {
        List<string> documents =
        [
            """{ "path": "places.json", "documentId": "places", "definitionKind": "place" }""",
            """{ "path": "links.json", "documentId": "links", "definitionKind": "travel-link" }""",
            """{ "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" }""",
        ];
        if (party) documents.Add("""{ "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }""");
        if (services) documents.Add("""{ "path": "services.json", "documentId": "services", "definitionKind": "service" }""");
        return $$"""
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [ {{string.Join(", ", documents)}} ]
            }
            """;
    }
}
