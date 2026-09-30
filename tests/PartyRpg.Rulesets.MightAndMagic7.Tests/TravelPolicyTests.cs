using PartyRpg.Kit;
using System.Text.RegularExpressions;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
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

        Assert.Equal(9, MightAndMagic7Movement.Falls.DamageTo(party.Members[0], fall));
        Assert.Equal(21, MightAndMagic7Movement.Falls.DamageTo(party.Members[1], fall));

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
        ContentCatalog catalog = Catalog(World(PartyDocument(food: 6)));
        PlaceGraph graph = PlaceGraphLoader.Load(catalog, MightAndMagic7FareDays.Read(catalog));
        PlaceTransition road = Assert.Single(graph.TransitionsFrom(Home));
        using PartyEntity party = MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The scenario declares a party, so composing it must produce one.");
        MightAndMagic7TravelCostRule rule = new(party);

        // No passage: a paid transition is refused by name rather than taken free, and nothing about the
        // party changes.
        Refusal unpaid = rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin)).Refusal!;
        Assert.Equal(MightAndMagic7Codes.TravelFareUnpaid, unpaid.Code);
        Assert.Contains($"{road.To}", unpaid.Message, StringComparison.Ordinal);

        // A passage to somewhere else does not pay for this journey: a ticket names the place it reaches.
        party.Passages.Hold(new PlaceId("99"), 2);
        Assert.Equal(
            "travel-fare-unpaid",
            rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin)).Refusal!.Code);

        // The passage to the place the road reaches is what pays, and boarding spends it: the journey quotes
        // the days the counter sold, eats no provisions because the fare included them, and leaves the party
        // holding no ticket for the journey back.
        party.Passages.Hold(road.To, 2);
        TravelCostQuote boarded = rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin));
        Assert.Null(boarded.Refusal);
        Assert.Equal(new TravelTime(2, TravelTimeUnit.Days), boarded.Cost.Time);
        Assert.True(boarded.Cost.Food.IsNone);
        Assert.Equal(0, party.Passages.DaysTo(road.To));
    }

    [Fact]
    public void A_passage_is_boarded_at_the_world_and_charges_the_days_the_ticket_names_exactly_once()
    {
        // The world's own roads, restated with a crossing a stable sells beside the one the party walks: a
        // root holds one document per id, so the fare replaces the links the world declares rather than
        // sitting beside them.
        (string Path, string Text)[] staged = [.. World(PartyDocument(food: 6))];
        ContentCatalog catalog = Catalog(
            [.. staged.Where(file => !file.Path.EndsWith("links.json", StringComparison.Ordinal)), Fare()]);
        PlaceGraph graph = PlaceGraphLoader.Load(catalog, MightAndMagic7FareDays.Read(catalog));
        PlaceTransition coach = graph.Transitions.Single(transition => transition.Source == "coach");
        Assert.True(coach.IsFare);

        using PartyEntity party = MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The scenario declares a party, so composing it must produce one.");
        PartyResourceLedger accounts = new(party, provisioning: new MightAndMagic7Provisions(party));
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

        // The counter sells a journey of two days; boarding it moves the party to the town the passage
        // reaches, charges the ticket's own days on the one clock, and tears the ticket.
        party.Passages.Hold(new PlaceId("2"), 2);
        TransitionResult boarded = world.Board(new PlaceId("2"));

        Assert.True(boarded.Arrived);
        Assert.Equal(new PlaceId("2"), world.Place);
        Assert.Equal(new PlacePose(1, 2, 3, 0, 0), world.Party.PlacePose);
        Assert.Equal(2, clock.ElapsedGameDays);
        Assert.Equal(0, party.Passages.DaysTo(new PlaceId("2")));

        // The larder is untouched: a fare includes the journey's board, which is the donor's own reading of
        // a coach journey.
        Assert.Equal(6, party.Food.Portions);

        // Back at the counter, boarding again has no ticket behind it: the journey is refused by name, the
        // party stays where it stands, and the clock does not move a second time.
        world.ArriveAt(Home, PlacePose.Origin);
        TransitionResult again = world.Board(new PlaceId("2"));

        Assert.False(again.Arrived);
        Assert.Equal("travel-fare-unpaid", again.Refusal!.Code);
        Assert.Equal(2, clock.ElapsedGameDays);
        Assert.Equal(Home, world.Place);
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
    public void A_day_eats_one_ration_and_an_empty_larder_weakens_the_party_until_it_is_fed()
    {
        ContentCatalog catalog = Catalog(World(PartyDocument(food: 2)));
        using PartyEntity party = MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The scenario declares a party, so composing it must produce one.");
        PartyResourceLedger ledger = new(party, provisioning: new MightAndMagic7Provisions(party));

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

        // Provisions arriving through the party's own path, and the next day the larder covers, is what
        // ends the hunger: the rule states both ends of it.
        ledger.Credit(PartyCost.OfFood(new Provisions(2, ProvisionUnit.Portions)));
        ProvisionDay fed = ledger.SpendDay();

        Assert.Null(fed.Shortage);
        Assert.Equal(1, party.Food.Portions);
        Assert.All(party.Members, member => Assert.False(member.Conditions.Has(MightAndMagic7Provisions.Weakness)));
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
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json", Manifest(extra.Length > 0)),
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

    /// <summary>
    /// The world's own roads, restated with a crossing a stable sells beside the one the party can walk.
    /// </summary>
    /// <remarks>
    /// It replaces the world's links document rather than adding a second one, because a root has one
    /// document per id: the two crossings from Home to Cave — one walked and one bought — are what the test
    /// needs to tell a fare from a road between the same places.
    /// </remarks>
    private static (string Path, string Text) Fare() =>
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

    private static string Manifest(bool party) =>
        $$"""
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "authored for a test" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" },
            { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" },
            { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" }{{(party ? "," : string.Empty)}}
            {{(party ? """{ "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }""" : string.Empty)}}
          ]
        }
        """;
}
