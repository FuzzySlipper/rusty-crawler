using System.Numerics;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Taking a transition by walking into it: the entrance a place issues, the entry that takes it through
/// the one transition path, and everything that must not happen instead.
/// </summary>
/// <remarks>
/// The content here is written by the test, in the shape the importer writes, so the trigger is exercised
/// without any of the operator's data. What a reach means is the same in both: a place's own coordinates,
/// a centre, and how far from it the party is inside the entrance.
/// </remarks>
public sealed class WalkTransitionTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    /// <summary>The pose the party starts in, which is outside every reach these places declare.</summary>
    private static readonly PlacePose Start = new(0, 0, 0, 512, 0);

    /// <summary>The reach the road into the cave is walked into: 200 along the first axis, 60 wide.</summary>
    private const double ReachX = 200;
    private const double ReachRadius = 60;

    [Fact]
    public void Walking_into_an_entrance_takes_its_transition_through_the_one_path_and_arrives_properly()
    {
        RecordingRule rule = new();
        using SessionWorld world = World([(100, 0, 0), (100, 0, 0), (100, 0, 0)], rule);
        RecordingMover mover = Mover(world);

        // Two steps put the party at x=200: the first ends outside the reach, the second inside it, and
        // it is that crossing — not standing in the entrance — that takes the transition.
        world.Step(MovementIntent.Still, 1);
        Assert.Equal(new PlaceId("1"), world.Place);

        world.Step(MovementIntent.Still, 1);
        Assert.Equal(new PlaceId("2"), world.Place);

        // The arrival is the destination's own: the pose its named point declares, in its coordinates.
        Assert.Equal(new PlacePose(5, 6, 7, 0, 0), world.Party.PlacePose);
        Assert.True(world.Places.StateOf(new PlaceId("2")).Visited);

        // Knowledge accrues the same way wherever the party goes: the place it started in and the one it
        // walked into are both known.
        Assert.Equal(2, world.Places.States.Count(state => state.Visited));

        // The destination's geometry was admitted and the old place's released, in that order.
        Assert.Equal([new PlaceId("1"), new PlaceId("2")], mover.Entered);

        // Walking in is a transition like any other: quoted once, as the kind of travel it is, with the
        // pose the party holds at the moment it enters the entrance and before it has left the place.
        TransitionRequest asked = Assert.Single(rule.Asked);
        Assert.Equal(TransitionKind.Entrance, asked.Kind);
        Assert.Equal("edge", asked.Transition.Source);
        Assert.Equal(new PlaceId("1"), asked.PartyPlace);
        Assert.Equal(new PlacePose(200, 0, 0, 512, 0), asked.PartyPose);
    }

    [Fact]
    public void Standing_in_an_entrance_is_not_entering_it_and_a_party_placed_inside_never_bounces_back()
    {
        RecordingRule rule = new();
        using SessionWorld world = World([(100, 0, 0), (100, 0, 0), (1, 0, 0), (500, 0, 0), (-500, 0, 0)], rule);

        world.Step(MovementIntent.Still, 1);
        world.Step(MovementIntent.Still, 1);
        Assert.Equal(new PlaceId("2"), world.Place);

        // The cave's own way out is centred on the point the party arrived at, so it stands inside that
        // reach. A step that stays inside it does not travel: a door must not throw the party back the
        // way it came.
        world.Step(MovementIntent.Still, 1);
        Assert.Equal(new PlaceId("2"), world.Place);

        // Walking out of the reach and back into it is an entry, so the way out works the same way the
        // way in did.
        world.Step(MovementIntent.Still, 1);
        Assert.Equal(new PlaceId("2"), world.Place);
        world.Step(MovementIntent.Still, 1);
        Assert.Equal(new PlaceId("1"), world.Place);
        Assert.Equal(new PlacePose(10, 20, 0, 512, 0), world.Party.PlacePose);
        Assert.Equal(2, rule.Asked.Count);
        Assert.Equal(TransitionKind.Entrance, rule.Asked[1].Kind);
        Assert.Equal("back", rule.Asked[1].Transition.Source);
    }

    [Fact]
    public void A_refused_walk_in_leaves_the_party_where_it_stands_and_is_reported_by_name()
    {
        RecordingRule rule = new() { Refuse = TransitionKind.Entrance };
        RecordingDiagnosticsService diagnostics = new();
        using SessionWorld world = World([(100, 0, 0), (100, 0, 0)], rule, diagnostics);

        world.Step(MovementIntent.Still, 1);
        world.Step(MovementIntent.Still, 1);

        // The rule refused the journey, so nothing moved between places and nothing was charged.
        Assert.Equal(new PlaceId("1"), world.Place);
        Assert.Equal(new PlacePose(200, 0, 0, 512, 0), world.Party.PlacePose);

        // A refusal nothing else could show is reported, and the report names the entrance and the way.
        DiagnosticsPublishRequest published = Assert.Single(diagnostics.Published);
        Assert.Equal("entrance-refused", published.Code);
        Assert.Contains("in-cave", published.Message, StringComparison.Ordinal);
        Assert.Contains("test-refused", published.Message, StringComparison.Ordinal);

        // The party is standing in the entrance it was refused at, which is not an entry: walking on
        // inside it does not ask again until it has left and come back.
        Assert.Single(rule.Asked);
    }

    [Fact]
    public void A_world_without_a_mover_or_entrances_moves_the_party_and_never_the_place()
    {
        RecordingRule rule = new();

        // No mover: there is no movement at all, so there is nothing to walk into anything.
        using SessionWorld still = World(null, rule);
        Assert.Null(still.Step(MovementIntent.Still, 1));
        Assert.Equal(new PlaceId("1"), still.Place);

        // A mover but no entrances: content declares no way to walk between places, and walking changes
        // the pose alone rather than teleporting the party through a door content never declared.
        using SessionWorld walkable = World([(400, 0, 0), (400, 0, 0)], rule, entrances: false);
        walkable.Step(MovementIntent.Still, 1);
        walkable.Step(MovementIntent.Still, 1);
        Assert.Equal(new PlaceId("1"), walkable.Place);
        Assert.Equal(new PlacePose(800, 0, 0, 512, 0), walkable.Party.PlacePose);
        Assert.Empty(rule.Asked);
    }

    [Fact]
    public void A_kind_of_travel_that_is_not_a_walk_in_cannot_be_written_as_one()
    {
        // A walk-in is walking or passing through an entrance. A fare or a portal declared as a reach
        // would be a journey nothing paid for, so content cannot declare one.
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => World(
            [],
            new RecordingRule(),
            entrances: true,
            entranceKind: "paidService"));

        Assert.Contains(error.Issues, issue => issue.Code == "entrance-kind-unknown");
    }

    [Fact]
    public void An_entrance_naming_a_transition_no_place_issues_is_refused_by_name()
    {
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => World(
            [],
            new RecordingRule(),
            entrances: true,
            entranceLink: "no-such-road"));

        Assert.Contains(error.Issues, issue => issue.Code == "entrance-transition-unknown");
    }

    [Fact]
    public void An_entrance_standing_in_a_place_its_transition_does_not_leave_is_refused_by_name()
    {
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => World(
            [],
            new RecordingRule(),
            entrances: true,
            entrancePlace: "2"));

        Assert.Contains(error.Issues, issue => issue.Code == "entrance-place-mismatch");
    }

    private static PartyPoseOwner Party() =>
        new(new PartyPose(new PlaceId("1"), Start), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));

    /// <summary>
    /// A world over two places joined both ways, with the roads' reaches content declares.
    /// </summary>
    /// <param name="mover">The party's movement, or null for a world with none.</param>
    /// <param name="rule">The rule every transition is quoted through.</param>
    /// <param name="diagnostics">Where a refusal is reported, when the test reads reports.</param>
    /// <param name="entrances">Whether content declares the reaches; a world without them cannot be walked between places.</param>
    /// <param name="entranceKind">The kind of travel the entrance declares, to exercise a kind that is not a walk-in.</param>
    /// <param name="entranceLink">The transition the entrance names, to exercise one the world does not hold.</param>
    /// <param name="entrancePlace">The place the entrance claims to stand in, to exercise one that is not where its road leaves.</param>
    private static SessionWorld World(
        IReadOnlyList<(double X, double Y, double Z)>? walk,
        ITravelCostRule rule,
        IDiagnosticsService? diagnostics = null,
        bool entrances = true,
        string entranceKind = "entrance",
        string entranceLink = "edge",
        string entrancePlace = "1")
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest(entrances))
                .Add("packs/world/places.json", Document("places", "place",
                    """{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ] }""",
                    """{ "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 5, "y": 6, "z": 7, "yaw": 0 } ] }"""))
                .Add("packs/world/links.json", Document("links", "travel-link",
                    """{ "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
                    """{ "id": "back", "fromPlace": "2", "toPlace": "1", "x": 10, "y": 20, "z": 0, "yaw": 512 }"""))
                .Add("packs/world/entrances.json", Document("entrances", "place-entrance",
                    $$"""{ "id": "in-cave", "link": "{{entranceLink}}", "fromPlace": "{{entrancePlace}}", "kind": "{{entranceKind}}", "x": {{ReachX}}, "y": 0, "z": 0, "radius": {{ReachRadius}} }""",
                    """{ "id": "out-cave", "link": "back", "fromPlace": "2", "kind": "entrance", "x": 5, "y": 6, "z": 7, "radius": 40 }""")),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PartyPoseOwner party = Party();
        return new SessionWorld(
            graph,
            party,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            rule,
            time: null,
            // The mover moves the world's own pose owner, so there is still exactly one party position and
            // the step the test queues is the step the world sees.
            walk is null ? null : new RecordingMover(party, walk),
            diagnostics,
            entrances ? PlaceEntranceLoader.Load(catalog, graph) : null);
    }

    /// <summary>The mover the world was built with, which is where a test reads what it entered.</summary>
    private static RecordingMover Mover(SessionWorld world) => Assert.IsType<RecordingMover>(world.Mover);

    private static string Manifest(bool entrances) =>
        $$"""
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "test content" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" },
            { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" }{{(entrances ? "," : string.Empty)}}
            {{(entrances ? """{ "path": "entrances.json", "documentId": "entrances", "definitionKind": "place-entrance" }""" : string.Empty)}}
          ]
        }
        """;

    private static string Document(string documentId, string definitionKind, params string[] entries) =>
        $$"""
        { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
        """;

    /// <summary>
    /// The party's movement as the test drives it: each step applies the next delta the test queued, which
    /// is what a resolved engine step would have done to the pose.
    /// </summary>
    private sealed class RecordingMover(PartyPoseOwner party, IReadOnlyList<(double X, double Y, double Z)> deltas) : IPartyMover
    {
        private int _steps;

        internal List<PlaceId> Entered { get; } = [];

        public PlaceGeometryAdmission Enter(PlaceId place)
        {
            Entered.Add(place);
            return PlaceGeometryAdmission.Empty(place);
        }

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
        {
            (double x, double y, double z) = deltas[Math.Min(_steps, deltas.Count - 1)];
            _steps++;
            party.Move(x, y, z);
            return new MovementOutcome(
                party.Capture().Pose,
                new Vector3((float)x, (float)y, (float)z),
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

    /// <summary>The cost contract as the test configures it: it records every question and can refuse one.</summary>
    private sealed class RecordingRule : ITravelCostRule
    {
        internal List<TransitionRequest> Asked { get; } = [];

        /// <summary>The kind of travel this rule refuses, or null when every kind is payable.</summary>
        internal TransitionKind? Refuse { get; set; }

        public TravelCostQuote Quote(TransitionRequest request)
        {
            Asked.Add(request);
            return Refuse == request.Kind
                ? TravelCostQuote.Refused(new TravelRefusal("test-refused", "the test refused this journey"))
                : TravelCostQuote.Payable(TravelCost.Free);
        }
    }
}
