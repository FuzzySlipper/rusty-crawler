using System.Numerics;
using System.Text.RegularExpressions;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What a transition's quoted cost lands on: the session's one clock and the party's one larder.
/// </summary>
/// <remarks>
/// <para>
/// The transition path states a cost and applies nothing; this is the suite for the receivers. Every
/// number a game would recognize here is the test's own policy — what a crossing costs, what a day eats,
/// where hunger starts and ends — because the kit holds none of them, and the ruleset's own answers are
/// proved beside it in the host's suite.
/// </para>
/// <para>
/// The clock is a real <see cref="GameClock"/> throughout, not a stand-in: the point of these facts is
/// that travel time and respawn are moved through the one clock, so a double that answered a day count
/// would prove nothing about the wiring.
/// </para>
/// </remarks>
public sealed class TravelCostWiringTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Home = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly ConditionId Weakness = new("weak");

    private const double StepSeconds = 1.0 / 60.0;

    [Fact]
    public void A_transition_charges_the_clock_and_the_larder_exactly_once()
    {
        GameClock clock = Clock();
        using PartyEntity party = Party(foodPortions: 6);
        using SessionWorld world = World(clock, Ledger(party), new TestCostRule());

        // Walking is free here, and a free journey charges nothing at all — not a zero-cost charge that
        // still spends a day's food.
        Assert.True(world.Travel(Link("edge"), TransitionKind.Walking).Arrived);
        Assert.Equal(GameDuration.None, clock.Elapsed);
        Assert.Equal(6, party.Food.Portions);

        // A quoted cost is applied once, on arrival: two days on the clock and two portions out of the
        // larder, and no part of it twice.
        Assert.True(world.Travel(Link("back"), TransitionKind.Entrance).Arrived);
        Assert.Equal(Calendar.Days(2), clock.Elapsed);
        Assert.Equal(new GameDate(1168, 1, 3, 9, 0, 0), clock.Now);
        Assert.Equal(4, party.Food.Portions);

        // The same journey again is a second journey, not a second charge for the first one.
        Assert.True(world.Travel(Link("edge"), TransitionKind.Entrance).Arrived);
        Assert.Equal(Calendar.Days(4), clock.Elapsed);
        Assert.Equal(2, party.Food.Portions);
    }

    [Fact]
    public void A_refused_transition_charges_neither_the_clock_nor_the_larder()
    {
        GameClock clock = Clock();
        using PartyEntity party = Party(foodPortions: 6);
        TestCostRule rule = new() { Refuse = true };
        using SessionWorld world = World(clock, Ledger(party), rule);
        PlacePose before = world.Party.PlacePose;

        TransitionResult refused = world.Travel(Link("edge"), TransitionKind.Entrance);

        // The rule refused the journey, so nothing moved between places and nothing was charged: a
        // refusal is an ordinary answer, and a party that never left must not pay for leaving.
        Assert.False(refused.Arrived);
        Assert.Equal("test-refused", refused.Refusal!.Code);
        Assert.Equal(GameDuration.None, clock.Elapsed);
        Assert.Equal(new GameDate(1168, 1, 1, 9, 0, 0), clock.Now);
        Assert.Equal(6, party.Food.Portions);
        Assert.Equal(before, world.Party.PlacePose);
        Assert.Equal(0, world.Places.ElapsedGameDays);
    }

    [Fact]
    public void A_party_that_arrives_short_is_weakened_and_a_day_it_covers_ends_it()
    {
        GameClock clock = Clock();
        using PartyEntity party = Party(foodPortions: 1, memberCount: 2);
        using SessionWorld world = World(clock, Ledger(party), new TestCostRule());

        // Two portions quoted, one in the larder: the day is spent down to empty and the party carries the
        // shortfall rather than the road being cancelled, which is what the donor's food store does.
        Assert.True(world.Travel(Link("edge"), TransitionKind.Entrance).Arrived);
        Assert.Equal(0, party.Food.Portions);
        Assert.All(party.Members, member => Assert.Equal(1, member.Conditions.SeverityOf(Weakness)));

        // Provisions arrive through the party's own ledger, and the next day the larder covers is what ends
        // the hunger: the rule states both ends of it, so a fed day is a recovery like any other.
        Ledger(party).Credit(PartyCost.OfFood(new Provisions(3, ProvisionUnit.Portions)));
        Assert.True(world.Travel(Link("back"), TransitionKind.Entrance).Arrived);
        Assert.Equal(1, party.Food.Portions);
        Assert.All(party.Members, member => Assert.False(member.Conditions.Has(Weakness)));
    }

    [Fact]
    public void The_clocks_day_boundary_reaches_place_respawn_through_the_session()
    {
        using RecordingUiProjectionChannel channel = new();
        GameClock clock = Clock();
        using SessionWorld world = World(clock, null, new TestCostRule());
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            world,
            clock: clock);
        session.Start();
        world.Places.MarkCleared(Home);

        // Eight and a third game hours of admitted time: the same day, so the place's population is not
        // due and its state stands.
        session.Update(Update(1, admitted: 1000, fixedDelta: 1.0));

        Assert.Equal(GameDuration.FromSeconds(30000), clock.Elapsed);
        Assert.Equal(0, clock.ElapsedGameDays);
        Assert.True(world.Places.StateOf(Home).Cleared);

        // Twenty-five game hours: the clock crossed a day boundary, and the same update that moved it
        // brings the world's places to the day it now stands on.
        session.Update(Update(1001, admitted: 2000, fixedDelta: 1.0));

        Assert.Equal(1, clock.ElapsedGameDays);
        PlaceState home = world.Places.StateOf(Home);
        Assert.Equal(1, world.Places.ElapsedGameDays);
        Assert.False(home.Cleared);
        Assert.Equal(1, home.RespawnCount);
        Assert.True(home.Visited);
        Assert.Equal("1168-01-02", channel.Latest().Field("clock").Field("date").AsString());
    }

    [Fact]
    public void A_journey_that_crosses_a_day_brings_a_cleared_place_back_on_arrival()
    {
        GameClock clock = Clock();
        using PartyEntity party = Party(foodPortions: 6);
        TestCostRule rule = new()
        {
            Crossing = new TravelCost(new TravelTime(3, TravelTimeUnit.Days), new Provisions(3, ProvisionUnit.Portions)),
        };
        using SessionWorld world = World(clock, Ledger(party), rule);

        // The place was cleared on the session's first day and its population is due after one.
        world.Places.MarkCleared(Home);

        Assert.True(world.Travel(Link("edge"), TransitionKind.Entrance).Arrived);

        // Three days of road: the crossing the clock reported is what brings the place back while the party
        // travels, so the arrival is the moment the schedule catches up rather than the next admitted update.
        Assert.Equal(Calendar.Days(3), clock.Elapsed);
        Assert.Equal(3, world.Places.ElapsedGameDays);
        PlaceState home = world.Places.StateOf(Home);
        Assert.False(home.Cleared);
        Assert.Equal(1, home.RespawnCount);
    }

    [Fact]
    public void The_session_advances_the_clock_by_the_interval_the_movement_step_covers()
    {
        using RecordingUiProjectionChannel channel = new();
        GameClock clock = Clock();
        PartyPoseOwner pose = Pose();
        RecordingMover mover = new(pose);
        using SessionWorld world = World(clock, null, new TestCostRule(), mover: mover, pose: pose);
        MovementInput input = new(Names, turnRatePerSecond: 512);
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            world,
            input,
            clock);
        session.Start();

        // One update of 120 admitted steps of a sixtieth of a second: the walk covers two seconds, and the
        // clock is moved by exactly those two seconds converted at its own scale — one interval, one owner.
        session.Update(Update(0, admitted: 120, fixedDelta: StepSeconds, input: [Forward()]));

        Assert.Equal([2.0], mover.Steps);
        Assert.Equal(GameDuration.FromSeconds(60), clock.Elapsed);

        // A held session admits no interval, so neither the walk nor the clock moves while it is held; the
        // input is still read, so a key released during the hold is not still held when it resumes.
        session.Hold();
        session.Update(Update(120, admitted: 120, fixedDelta: StepSeconds, input: []));
        Assert.Equal([2.0], mover.Steps);
        Assert.Equal(GameDuration.FromSeconds(60), clock.Elapsed);
    }

    [Fact]
    public void A_deadline_the_clock_brings_due_is_reported_because_no_owner_schedules_one_yet()
    {
        using RecordingUiProjectionChannel channel = new();
        RecordingDiagnosticsService diagnostics = new();
        GameClock clock = Clock();
        using SessionWorld world = World(clock, null, new TestCostRule());
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            world,
            clock: clock,
            diagnostics: diagnostics);
        session.Start();
        clock.ScheduleAfter(GameDuration.FromHours(1));

        // An hour of game time, admitted in one update: the clock brings the deadline due and hands it to
        // the session, which is its owner until something that keeps a schedule exists.
        session.Update(Update(0, admitted: 120, fixedDelta: 1.0));

        DiagnosticsPublishRequest published = Assert.Single(diagnostics.Published);
        Assert.Equal("deadline-due", published.Code);
        Assert.Contains("no owner schedules deadlines", published.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cost_with_no_account_to_land_in_is_reported_rather_than_dropped()
    {
        RecordingDiagnosticsService diagnostics = new();

        // A world with neither a clock nor a party: the cost rule still quotes a journey, and the quote has
        // no owner to reach, so the world says so instead of letting the journey look free.
        using SessionWorld world = World(clock: null, ledger: null, new TestCostRule(), diagnostics);

        Assert.True(world.Travel(Link("edge"), TransitionKind.Entrance).Arrived);
        Assert.Equal(
            ["travel-time-uncharged", "travel-provisions-uncharged"],
            diagnostics.Published.Select(published => published.Code));
    }

    [Fact]
    public void The_projection_publishes_the_clocks_date_and_the_partys_own_accounts()
    {
        using RecordingUiProjectionChannel channel = new();
        GameClock clock = Clock();
        using PartyEntity party = Party(foodPortions: 3, coins: 42, reputation: 5, fame: 2);
        using SessionWorld world = World(clock, Ledger(party), new TestCostRule());
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            world,
            clock: clock,
            party: party);

        ProjectedNode published = channel.Latest();
        Assert.Equal("1168-01-01", published.Field("clock").Field("date").AsString());
        Assert.Equal("09:00", published.Field("clock").Field("time").AsString());
        Assert.Equal("day", published.Field("clock").Field("daylight").AsString());
        Assert.Equal(0d, published.Field("clock").Field("elapsedDays").AsNumber());

        // The purse, the larder, and the standing are the party's own values, read from the party the
        // session holds rather than from a copy this layer keeps.
        Assert.Equal(42d, published.Field("party").Field("coins").AsNumber());
        Assert.Equal(3d, published.Field("party").Field("provisions").AsNumber());
        Assert.Equal("portions", published.Field("party").Field("unit").AsString());
        Assert.Equal(1d, published.Field("party").Field("members").AsNumber());
        Assert.Equal(5d, published.Field("party").Field("reputation").AsNumber());
        Assert.Equal(2d, published.Field("party").Field("fame").AsNumber());
        Assert.Equal(string.Empty, published.Field("party").Field("conditions").AsString());

        // What the party is charged shows up in the same place the panel reads from, rather than in a
        // projection-only number: two portions out of three leaves it fed.
        Assert.True(world.Travel(Link("edge"), TransitionKind.Entrance).Arrived);
        session.PublishWorld();
        Assert.Equal(1d, channel.Latest().Field("party").Field("provisions").AsNumber());
        Assert.Equal(string.Empty, channel.Latest().Field("party").Field("conditions").AsString());

        // A second journey spends the last portion short of the day it costs, and the weakness the larder
        // rule states is what the panel then shows.
        Assert.True(world.Travel(Link("back"), TransitionKind.Entrance).Arrived);
        session.PublishWorld();
        Assert.Equal(0d, channel.Latest().Field("party").Field("provisions").AsNumber());
        Assert.Equal("weak (1)", channel.Latest().Field("party").Field("conditions").AsString());
    }

    [Fact]
    public void The_kit_holds_one_food_store_and_no_second_larder()
    {
        // The larder's own file is the one place food is counted, so no other source may declare a store
        // for it: a second counter beside the party's would be a number that can disagree with the one the
        // larder reports, which is exactly what a party-scoped resource exists to prevent.
        string larder = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit", "Party", "PartyFood.cs");
        string[] sources =
        [
            .. Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit"), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && file != larder),
        ];
        Assert.NotEmpty(sources);

        // A store is a declaration a number can live in and move through: a field, or a property with a
        // setter. A read-only property of a food-ish name is how a value is handed on, not a second place
        // it is kept, so it is deliberately not what this looks for.
        Regex store = new(
            @"(private|internal|protected|public)\s+(int|long|Provisions)\s+_?\w*(Food|Portions|Provisions|Rations)\w*\s*(\{[^}]*\bset\b|=(?!=|>)|;)",
            RegexOptions.CultureInvariant);

        // The scan is not vacuous: the larder itself is exactly what it looks for.
        Assert.Matches(store, File.ReadAllText(larder));

        foreach (string source in sources)
        {
            Match declared = store.Match(File.ReadAllText(source));
            Assert.False(
                declared.Success,
                $"{Path.GetFileName(source)} declares a food store ('{declared.Value.Trim()}'): the party's larder is the one place food is counted, and a second counter beside it would let the two disagree.");
        }
    }

    private static readonly MovementIntentNames Names = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");

    /// <summary>The calendar these tests run on, which is the product's authored one.</summary>
    private static GameCalendar Calendar => GameCalendar.TwelveMonthsOfFourWeeks;

    /// <summary>The clock these tests run on: the authored calendar, a stated start, thirty times real time.</summary>
    private static GameClock Clock() => new(
        Calendar,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>The party a transition's provisions are charged to.</summary>
    private static PartyEntity Party(
        int foodPortions = 0,
        int coins = 0,
        int reputation = 0,
        int fame = 0,
        int memberCount = 1)
    {
        List<MemberCreation> members = [];
        for (int index = 0; index < memberCount; index++)
        {
            members.Add(new MemberCreation(new PartyMemberSeed(
                $"Member {index + 1}",
                new RaceId("testfolk"),
                new ClassId("fighter"),
                [new AttributeScore(new AttributeId("vigour"), 12)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(10),
                spellPoints: ResourcePool.Full(5))));
        }

        return new PartyEntityFactory().Create(new PartyCreation(
            members,
            coins,
            foodPortions,
            ProvisionUnit.Portions,
            reputation,
            fame));
    }

    /// <summary>The one path into a party's accounts, under a policy the test states.</summary>
    private static PartyResourceLedger Ledger(PartyEntity party) =>
        new(party, provisioning: new Rations(party, Weakness));

    /// <summary>A world over two places joined both ways, with a clock a journey charges time to.</summary>
    /// <param name="clock">The clock the world reads its days from and charges travel time to, or null.</param>
    /// <param name="ledger">The party's accounts, or null when the world holds no party.</param>
    /// <param name="rule">The rule every transition is quoted through.</param>
    /// <param name="diagnostics">Where a cost with no owner is reported, when the test reads reports.</param>
    /// <param name="mover">The party's movement, when the test drives a step through the session.</param>
    /// <param name="pose">The party's one pose owner, when the test needs the mover to share it.</param>
    private static SessionWorld World(
        GameClock? clock,
        PartyResourceLedger? ledger,
        ITravelCostRule rule,
        IDiagnosticsService? diagnostics = null,
        IPartyMover? mover = null,
        PartyPoseOwner? pose = null)
    {
        PlaceGraph graph = Graph();
        return new SessionWorld(
            graph,
            pose ?? Pose(),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            rule,
            clock,
            mover,
            diagnostics,
            entrances: null,
            clock: clock,
            resources: ledger);
    }

    /// <summary>The transition a link id names, which is the only way a caller reaches the one path.</summary>
    private static PlaceTransition Link(string id) =>
        Assert.Single(Graph().Transitions, transition => transition.Source == id);

    /// <summary>The party's one position, on the road out of the first place.</summary>
    private static PartyPoseOwner Pose() => new(
        new PartyPose(Home, new PlacePose(0, 0, 0, 512, 0)),
        new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));

    private static PlaceGraph Graph() => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document("places", "place",
                    """{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 1, "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ] }""",
                    """{ "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1, "entryPoints": [ { "id": "Party Start", "x": 5, "y": 6, "z": 7, "yaw": 0 } ] }"""))
                .Add("packs/world/links.json", Document("links", "travel-link",
                    """{ "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
                    """{ "id": "back", "fromPlace": "2", "toPlace": "1", "x": 10, "y": 20, "z": 0, "yaw": 512 }""")),
            Layout).RequireValid());

    private static ProductUpdate Update(
        ulong step,
        uint admitted,
        double fixedDelta = StepSeconds,
        ReadOnlySpan<ProductInputEvent> input = default)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            step,
            60,
            admitted,
            0,
            fixedDelta);
        return new ProductUpdate(facts, input);
    }

    /// <summary>A held forward control, which is what a player pressing the walk key sends.</summary>
    private static ProductInputEvent Forward() => new(
        InputEventKind.MappedDigital, InputEdge.Held, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, System.Text.Encoding.UTF8.GetBytes(Names.Forward),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "test content" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" },
            { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" }
          ]
        }
        """;

    private static string Document(string documentId, string definitionKind, params string[] entries) =>
        $$"""
        { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
        """;

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root above the test assembly.");
    }

    /// <summary>The cost contract as this suite states it: walking is free, anything else is a journey.</summary>
    private sealed class TestCostRule : ITravelCostRule
    {
        /// <summary>What a quoted journey costs; a test may state its own days and provisions.</summary>
        internal TravelCost Crossing { get; init; } =
            new(new TravelTime(2, TravelTimeUnit.Days), new Provisions(2, ProvisionUnit.Portions));

        /// <summary>Whether this rule refuses every journey, which is what a party that cannot travel sees.</summary>
        internal bool Refuse { get; init; }

        public TravelCostQuote Quote(TransitionRequest request) =>
            Refuse
                ? TravelCostQuote.Refused(new TravelRefusal("test-refused", "the test refused this journey"))
                : TravelCostQuote.Payable(request.Kind == TransitionKind.Walking ? TravelCost.Free : Crossing);
    }

    /// <summary>
    /// The larder policy this suite states: one portion a day, hunger when the larder is left empty, and a
    /// day the larder covers ends it. Every number is the test's, which is why the kit has none.
    /// </summary>
    private sealed class Rations(PartyEntity party, ConditionId weakness) : IProvisionDayRule
    {
        public Provisions DailyCharge(int members, int followers) => new(1, ProvisionUnit.Portions);

        public ActiveCondition? Consequence(int portionsAfter, int members, int followers)
        {
            if (portionsAfter >= 1)
            {
                // The rule is where a day's consequence is decided, so it is where hunger ends as well as
                // where it starts: a larder that still covers a day means the party ate.
                foreach (PartyMember member in party.Members) member.Conditions.Clear(weakness);
                return null;
            }

            return new ActiveCondition(weakness, 1);
        }
    }

    /// <summary>Movement as this suite drives it: it records the interval each step covered.</summary>
    private sealed class RecordingMover(PartyPoseOwner party) : IPartyMover
    {
        /// <summary>The admitted interval of every step the session asked for, in order.</summary>
        internal List<double> Steps { get; } = [];

        /// <summary>These movers hold no collision, so nothing occludes anything in them.</summary>
        public bool InSight(Vector3 from, Vector3 to) => true;

        public PlaceGeometryAdmission Enter(PlaceId place) => PlaceGeometryAdmission.Empty(place);

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
        {
            Steps.Add(elapsedSeconds);
            party.Move(1, 0, 0);
            return new MovementOutcome(
                party.Capture().Pose,
                new Vector3(1, 0, 0),
                Grounded: true,
                default,
                CharacterBlockFlags.None,
                default,
                SurfaceEffect.Ordinary,
                FallOutcome.None);
        }

        public void Dispose()
        {
        }
    }
}
