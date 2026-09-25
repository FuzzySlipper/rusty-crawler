using System.Numerics;
using System.Text;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The movement controls a product declares, read from admitted input into one step's intent.
/// </summary>
/// <remarks>
/// The names here are the test's own choice, which is the point: the kit claims whatever a product
/// declares and nothing else, so the same reader serves a product with other names for its keys.
/// </remarks>
public sealed class MovementInputTests
{
    private static readonly MovementIntentNames Names = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");

    private const double TurnRate = 512;

    private static MovementInput Input() => new(Names, TurnRate);

    [Fact]
    public void A_held_walk_key_asks_for_a_walk_and_its_release_stops_asking()
    {
        MovementInput input = Input();

        MovementIntent walking = input.Read([Digital(Names.Forward, InputEdge.Pressed)]);
        Assert.Equal(1, walking.Forward);
        Assert.Equal(0, walking.Strafe);
        Assert.False(walking.IsStill);

        // A key that is still down keeps asking on updates that carry no event at all, which is what
        // makes a held key hold rather than fire once.
        MovementIntent stillHeld = input.Read([]);
        Assert.Equal(1, stillHeld.Forward);

        MovementIntent stopped = input.Read([Digital(Names.Forward, InputEdge.Released)]);
        Assert.True(stopped.IsStill);
    }

    [Fact]
    public void Opposite_controls_cancel_and_strafe_right_is_the_positive_side()
    {
        MovementInput input = Input();

        MovementIntent both = input.Read([
            Digital(Names.Forward, InputEdge.Pressed),
            Digital(Names.Back, InputEdge.Pressed),
            Digital(Names.StrafeLeft, InputEdge.Pressed),
        ]);

        Assert.Equal(0, both.Forward);
        Assert.Equal(-1, both.Strafe);

        MovementIntent right = input.Read([
            Digital(Names.StrafeLeft, InputEdge.Released),
            Digital(Names.StrafeRight, InputEdge.Pressed),
        ]);
        Assert.Equal(1, right.Strafe);
    }

    [Fact]
    public void A_held_turn_control_asks_for_the_world_s_turn_rate_in_the_expected_direction()
    {
        MovementInput input = Input();

        // Turning left is the positive side: the world's facing grows when the party turns to its left.
        MovementIntent left = input.Read([Digital(Names.TurnLeft, InputEdge.Pressed)]);
        Assert.Equal(TurnRate, left.TurnRate, 6);

        MovementIntent right = input.Read([
            Digital(Names.TurnLeft, InputEdge.Released),
            Digital(Names.TurnRight, InputEdge.Pressed),
        ]);
        Assert.Equal(-TurnRate, right.TurnRate, 6);
    }

    [Fact]
    public void A_jump_is_an_edge_for_one_step_and_a_hold_for_as_long_as_it_is_held()
    {
        MovementInput input = Input();

        MovementIntent started = input.Read([Digital(Names.Jump, InputEdge.Pressed)]);
        Assert.True(started.JumpPressed);
        Assert.True(started.JumpHeld);

        // The engine buffers a jump, so the edge belongs to the step it arrived in and not to the ones
        // that follow; what follows is only the hold.
        MovementIntent held = input.Read([]);
        Assert.False(held.JumpPressed);
        Assert.True(held.JumpHeld);

        MovementIntent released = input.Read([Digital(Names.Jump, InputEdge.Released)]);
        Assert.False(released.JumpPressed);
        Assert.False(released.JumpHeld);
    }

    [Fact]
    public void An_intent_the_product_did_not_declare_asks_for_nothing()
    {
        MovementInput input = Input();

        MovementIntent intent = input.Read([
            Digital("test.fly", InputEdge.Pressed),
            Digital("crawler.ui", InputEdge.Pressed),
        ]);

        // A foreign intent is somebody else's event: it is not a movement control, so it moves nobody.
        Assert.True(intent.IsStill);
    }

    [Fact]
    public void Input_that_is_not_a_digital_control_asks_for_nothing()
    {
        MovementInput input = Input();

        // An axis or payload event on a declared name is still not a digital press: the reader believes
        // edges, and a value it does not read cannot set one.
        MovementIntent intent = input.Read([
            Digital(Names.Forward, InputEdge.None, InputPhase.DirectUi, InputProvenance.DirectUi, InputValueKind.Axis),
            Digital(Names.Back, InputEdge.None, InputPhase.DirectUi, InputProvenance.DirectUi, InputValueKind.ProductPayload),
        ]);

        Assert.True(intent.IsStill);
    }

    [Fact]
    public void A_direct_claim_asks_for_its_control_for_the_one_update_it_arrives_in()
    {
        MovementInput input = Input();

        // A direct interface claim carries no edge, so there is no release behind it to end the hold; it
        // is taken as asking for the control for the update it arrived in and no longer.
        MovementIntent claimed = input.Read([
            Digital(Names.Forward, InputEdge.None, InputPhase.DirectUi, InputProvenance.DirectUi),
            Digital(Names.Jump, InputEdge.None, InputPhase.DirectUi, InputProvenance.DirectUi),
        ]);

        Assert.Equal(1, claimed.Forward);
        Assert.True(claimed.JumpPressed);

        MovementIntent after = input.Read([]);
        Assert.True(after.IsStill);
    }

    [Fact]
    public void A_control_with_no_name_or_a_turn_rate_that_is_not_one_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new MovementIntentNames(
            "test.move-forward", "test.move-back", "test.strafe-left", "test.strafe-right", "test.turn-left", "test.turn-right", " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovementInput(Names, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovementInput(Names, double.NaN));
    }

    /// <summary>One digital engine event, in the shape the input service admits it.</summary>
    private static ProductInputEvent Digital(
        string intent,
        InputEdge edge,
        InputPhase phase = InputPhase.Pressed,
        InputProvenance provenance = InputProvenance.Physical,
        InputValueKind valueKind = InputValueKind.Digital) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        valueKind, phase, provenance, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
}

/// <summary>
/// Movement stepped inside the session's one admitted update: one step per update, with the intent the
/// player's input asked for, and the step's own length the time that update admitted.
/// </summary>
/// <remarks>
/// The mover here is the test's: it records what it was asked and moves the party through the world's own
/// pose owner. What it deliberately does not do is solve collision, because that is the engine's, and a
/// stand-in that solved a step would prove the stand-in rather than the engine. The engine's own step is
/// exercised against engine values in <see cref="PartyMotionTests"/> and in a live product run.
/// </remarks>
public sealed class MovementSteppingTests
{
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test Ruleset");
    private static readonly MovementIntentNames Names = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");

    private const double StepSeconds = 1.0 / 60.0;

    [Fact]
    public void The_admitted_update_steps_the_party_once_with_the_intent_the_input_asked_for()
    {
        PartyPoseOwner party = Party();
        RecordingMover mover = new(party);
        using SessionWorld world = World(party, mover);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            world,
            new MovementInput(Names, turnRatePerSecond: 512));
        session.Start();

        session.Update(Update(simulationStep: 60, admittedSteps: 2, [Digital(Names.Forward, InputEdge.Pressed)]));

        (MovementIntent intent, double seconds) = Assert.Single(mover.Steps);
        Assert.Equal(1, intent.Forward);
        Assert.Equal(2 * StepSeconds, seconds, 9);

        // The step's motion lands on the party's own pose, which is the only place a position is held:
        // the party started at four and the step moved it the six units the intent asked for.
        Assert.Equal(10, world.Party.PlacePose.X, 6);
        Assert.Same(party, world.Party);
    }

    [Fact]
    public void A_session_that_admitted_no_steps_moves_nobody()
    {
        PartyPoseOwner party = Party();
        RecordingMover mover = new(party);
        using SessionWorld world = World(party, mover);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel, world, new MovementInput(Names, 512));
        session.Start();

        session.Update(Update(simulationStep: 1, admittedSteps: 0, [Digital(Names.Forward, InputEdge.Pressed)]));

        // No admitted time means no step: an intent still has to be solved for, but there is no interval
        // for the engine to solve it over.
        Assert.Empty(mover.Steps);
        Assert.Equal(4, world.Party.PlacePose.X, 6);
    }

    [Fact]
    public void A_held_session_reads_what_the_player_holds_but_takes_no_step()
    {
        PartyPoseOwner party = Party();
        RecordingMover mover = new(party);
        using SessionWorld world = World(party, mover);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel, world, new MovementInput(Names, 512));
        session.Start();
        session.Hold();

        session.Update(Update(simulationStep: 1, admittedSteps: 1, [Digital(Names.Forward, InputEdge.Pressed)]));
        Assert.Empty(mover.Steps);

        // The key that went down while the session was held is still held when it resumes, so the party
        // walks on the next running update without the player pressing anything again.
        session.ReleaseHold();
        session.Update(Update(simulationStep: 2, admittedSteps: 1, []));

        (MovementIntent intent, _) = Assert.Single(mover.Steps);
        Assert.Equal(1, intent.Forward);
    }

    [Fact]
    public void A_party_that_asked_for_nothing_is_still_solved_for()
    {
        PartyPoseOwner party = Party();
        RecordingMover mover = new(party);
        using SessionWorld world = World(party, mover);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel, world, new MovementInput(Names, 512));
        session.Start();

        session.Update(Update(simulationStep: 1, admittedSteps: 1, [Digital("test.unclaimed", InputEdge.Pressed)]));

        // Gravity, a ledge walked off, and a moving platform all act on a party that asked for nothing,
        // so a still intent is not a reason to skip the engine's step.
        (MovementIntent intent, double seconds) = Assert.Single(mover.Steps);
        Assert.True(intent.IsStill);
        Assert.Equal(StepSeconds, seconds, 9);
        Assert.Equal(4, world.Party.PlacePose.X, 6);
    }

    [Fact]
    public void A_world_without_movement_says_so_rather_than_moving_the_party_itself()
    {
        PartyPoseOwner party = Party();
        using SessionWorld world = World(party, mover: null);

        Assert.Null(world.Mover);
        Assert.Null(world.Step(MovementIntent.Still, StepSeconds));
        Assert.Equal(4, world.Party.PlacePose.X, 6);
        Assert.Equal(MovementDiagnostics.None, world.Movement);
    }

    [Fact]
    public void A_session_with_no_world_never_asks_for_a_step()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel, world: null, new MovementInput(Names, 512));
        session.Start();

        session.Update(Update(simulationStep: 1, admittedSteps: 1, [Digital(Names.Forward, InputEdge.Pressed)]));

        Assert.Equal(MovementDiagnostics.None, session.Movement);
    }

    [Fact]
    public void The_world_admits_the_place_it_starts_in_and_every_place_the_party_travels_to()
    {
        PartyPoseOwner party = Party();
        RecordingMover mover = new(party);
        using SessionWorld world = World(party, mover);

        // The place the party starts in is entered like any other, so the scene it walks in is filled
        // before the first step rather than one arrival late.
        Assert.Equal([new PlaceId("1")], mover.Entered);

        PlaceTransition outbound = Assert.Single(world.Graph.TransitionsFrom(new PlaceId("1")));
        Assert.True(world.Travel(outbound, TransitionKind.Walking).Arrived);

        // Collision belongs to the place, so a change of place is where the scene changes with it.
        Assert.Equal([new PlaceId("1"), new PlaceId("2")], mover.Entered);
    }

    private static PartyPoseOwner Party() => new(
        new PartyPose(new PlaceId("1"), new PlacePose(4, 0, 0, Yaw: 0, Pitch: 0)),
        new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));

    private static SessionWorld World(PartyPoseOwner party, RecordingMover? mover)
    {
        PlaceGraph graph = Graph();
        return new SessionWorld(
            graph,
            party,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new TestCostRule(),
            time: null,
            mover);
    }

    private static PlaceGraph Graph() => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json",
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
                    """)
                .Add("packs/world/places.json",
                    """
                    {
                      "documentId": "places",
                      "definitionKind": "place",
                      "entries": [
                        { "id": "1", "kind": "region", "name": "Home", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 4, "y": 0, "z": 0, "yaw": 0 } ] },
                        { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
                      ]
                    }
                    """)
                .Add("packs/world/links.json",
                    """
                    {
                      "documentId": "links",
                      "definitionKind": "travel-link",
                      "entries": [ { "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ]
                    }
                    """),
            new ContentLayout("packs", "imports", "bundles")).RequireValid());

    private static ProductUpdate Update(ulong simulationStep, uint admittedSteps, ProductInputEvent[] input)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            simulationStep,
            60,
            admittedSteps,
            0,
            StepSeconds);
        return new ProductUpdate(facts, input);
    }

    private static ProductInputEvent Digital(string intent, InputEdge edge) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>
    /// The party's movement as the test drives it: it records what it was asked, moves the party through
    /// the world's own pose owner the way a resolved engine step would, and reports the falls it is told
    /// to report.
    /// </summary>
    private sealed class RecordingMover(PartyPoseOwner party) : IPartyMover
    {
        internal List<(MovementIntent Intent, double Seconds)> Steps { get; } = [];

        internal List<PlaceId> Entered { get; } = [];

        internal FallOutcome Fall { get; set; }

        internal bool Grounded { get; set; } = true;

        /// <summary>These movers hold no collision, so nothing occludes anything in them.</summary>
        public bool InSight(Vector3 from, Vector3 to) => true;

        public PlaceGeometryAdmission Enter(PlaceId place)
        {
            Entered.Add(place);
            return PlaceGeometryAdmission.Empty(place);
        }

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
        {
            Steps.Add((intent, elapsedSeconds));

            // What the engine resolves, in miniature: the party is moved through its pose owner and
            // nowhere else, so the test holds the same single-writer rule the product does.
            double forward = intent.Forward * elapsedSeconds * 180;
            party.Turn(intent.TurnRate * elapsedSeconds, 0);
            party.Move(forward, 0, 0);

            return new MovementOutcome(
                party.Capture().Pose,
                new Vector3((float)forward, 0, 0),
                Grounded,
                default,
                CharacterBlockFlags.None,
                default,
                SurfaceEffect.Ordinary,
                Fall);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>Walking is free in these tests, and nothing is ever paid for.</summary>
    private sealed class TestCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}

/// <summary>
/// Where a place's collision geometry is read from, and what an entry that promised one but carried none
/// does.
/// </summary>
public sealed class ContentPlaceGeometryTests
{
    private const string Artifact =
        """{"schemaVersion":1,"staticMeshArtifactId":"place-1","bounds":{"min":[0,0,0],"max":[10,10,10]},"collision":{"positions":[[0,0,0],[1,0,0],[0,0,1]],"triangles":[[0,1,2]]},"navigation":{"id":"nav","config":{"schemaVersion":1,"cellSize":64,"levelQuantum":64,"maximumSlopeDegrees":45,"requiredHeadroom":192,"supportProbeDrop":64},"cells":[{"column":0,"row":0,"level":0,"supportHeight":0,"walkable":true}]}}""";

    [Fact]
    public void A_place_whose_content_carries_an_artifact_hands_on_the_document_byte_for_byte()
    {
        PlaceGeometry? geometry = Source(withArtifact: true).For(new PlaceId("1"));

        Assert.NotNull(geometry);

        // The engine parses this document itself, so what content wrote is what it must receive: a
        // re-serialized copy would be a second copy of the engine's schema, and the first thing to
        // disagree with it.
        Assert.Equal(Artifact, Encoding.UTF8.GetString(geometry.Artifact.Span));

        // And it arrives with its own path, so the engine's content owner reports which pack, document,
        // and entry it came from rather than an opaque handle.
        Assert.Equal("world/geometry/1.json", geometry.Path);
    }

    [Fact]
    public void A_place_whose_content_declares_no_geometry_provides_none()
    {
        Assert.Null(Source(withArtifact: false).For(new PlaceId("1")));
        Assert.Null(Source(withArtifact: true).For(new PlaceId("2")));
    }

    [Fact]
    public void An_entry_that_promised_geometry_and_carried_none_stops_the_read_by_name()
    {
        // A place whose geometry silently went missing is a party walking through the floor, so the read
        // fails where the defect is attributable rather than yielding nothing.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Source(withArtifact: false, emptyArtifact: true).For(new PlaceId("1")));

        Assert.Contains("place '1'", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_artifact_with_no_bytes_is_refused_by_name()
    {
        Assert.Throws<ArgumentException>(() => new PlaceGeometry("world/geometry/1.json", ReadOnlyMemory<byte>.Empty));
        Assert.Throws<ArgumentException>(() => new PlaceGeometry(" ", Encoding.UTF8.GetBytes(Artifact)));
    }

    private static ContentPlaceGeometry Source(bool withArtifact, bool emptyArtifact = false)
    {
        string geometry = withArtifact
            ? $$"""{ "id": "1", "artifact": {{Artifact}} }"""
            : emptyArtifact
                ? """{ "id": "1", "artifact": 7 }"""
                : """{ "id": "2", "artifact": {} }""";

        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "world",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [ { "path": "geometry.json", "documentId": "geometry", "definitionKind": "place-geometry" } ]
                    }
                    """)
                .Add("packs/world/geometry.json",
                    $$"""{ "documentId": "geometry", "definitionKind": "place-geometry", "entries": [ {{geometry}} ] }"""),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();

        return new ContentPlaceGeometry(catalog, "place-geometry", "artifact");
    }
}

/// <summary>
/// What a fall becomes in the session: an observation, a diagnostic, and nothing applied.
/// </summary>
public sealed class MovementObservationTests
{
    private static readonly MovementIntentNames Names = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");

    [Fact]
    public void A_landing_past_the_threshold_is_recorded_and_reported_to_the_engine_diagnostics()
    {
        PartyPoseOwner party = new(
            new PartyPose(new PlaceId("home"), new PlacePose(0, 0, 0, 0, 0)),
            new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        RecordingMover mover = new(party) { Fall = new FallOutcome(Distance: 600, Excess: 88, Damage: 0) };
        RecordingDiagnosticsService diagnostics = new();
        using SessionWorld world = World(party, mover, diagnostics);

        world.Step(MovementIntent.Still, 1.0 / 60.0);

        MovementDiagnostics observed = world.Movement;
        Assert.Equal(1, observed.Falls);
        Assert.Equal(600, observed.LastFall.Distance, 6);
        Assert.True(observed.LastFall.PastThreshold);

        // The fall is reported and not applied: the party's health owner does not exist yet, which is why
        // the number is published for whoever will own it rather than charged here.
        DiagnosticsPublishRequest published = Assert.Single(diagnostics.Published);
        Assert.Equal("fall-past-threshold", published.Code);
        Assert.Equal("movement", published.Source);
        Assert.Contains("600", published.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_landing_within_the_threshold_is_recorded_and_reports_nothing()
    {
        PartyPoseOwner party = new(
            new PartyPose(new PlaceId("home"), new PlacePose(0, 0, 0, 0, 0)),
            new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        RecordingMover mover = new(party) { Fall = new FallOutcome(Distance: 12, Excess: 0, Damage: 0) };
        RecordingDiagnosticsService diagnostics = new();
        using SessionWorld world = World(party, mover, diagnostics);

        world.Step(MovementIntent.Still, 1.0 / 60.0);

        Assert.Equal(0, world.Movement.Falls);
        Assert.NotNull(world.Movement.Last);
        Assert.False(world.Movement.LastFall.PastThreshold);
        Assert.Empty(diagnostics.Published);
    }

    private static SessionWorld World(PartyPoseOwner party, RecordingMover mover, RecordingDiagnosticsService diagnostics)
    {
        PlaceGraph graph = PlaceGraphLoader.Load(
            ContentCatalogLoader.Load(
                new InMemoryContentSource()
                    .Add("packs/world/pack.json",
                        """
                        {
                          "schemaVersion": 1,
                          "packId": "world",
                          "kind": "definitions",
                          "provenance": { "description": "test content" },
                          "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
                        }
                        """)
                    .Add("packs/world/places.json",
                        """
                        {
                          "documentId": "places",
                          "definitionKind": "place",
                          "entries": [ { "id": "home", "kind": "region", "name": "Home", "respawnDays": 7,
                                         "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] } ]
                        }
                        """),
                new ContentLayout("packs", "imports", "bundles")).RequireValid());

        return new SessionWorld(
            graph,
            party,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeCostRule(),
            time: null,
            mover,
            diagnostics);
    }

    private sealed class RecordingMover(PartyPoseOwner party) : IPartyMover
    {
        internal FallOutcome Fall { get; init; }

        /// <summary>These movers hold no collision, so nothing occludes anything in them.</summary>
        public bool InSight(Vector3 from, Vector3 to) => true;

        public PlaceGeometryAdmission Enter(PlaceId place) => PlaceGeometryAdmission.Empty(place);

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds) => new(
            party.Capture().Pose,
            Vector3.Zero,
            Grounded: true,
            default,
            CharacterBlockFlags.None,
            default,
            SurfaceEffect.Ordinary,
            Fall);

        public void Dispose()
        {
        }
    }

    private sealed class FreeCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}

/// <summary>An engine diagnostics service that records what a session reports to it.</summary>
internal sealed class RecordingDiagnosticsService : IDiagnosticsService
{
    private readonly List<DiagnosticsPublishRequest> _published = [];

    internal IReadOnlyList<DiagnosticsPublishRequest> Published => _published;

    public ReadOnlyMemory<byte> ReadRenderer() => ReadOnlyMemory<byte>.Empty;

    public void Publish(DiagnosticsPublishRequest request) => _published.Add(request);
}
