using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Builds this game's world from the content the product loaded.
/// </summary>
/// <remarks>
/// The world exists when content declares both places and where the party starts. The starting place is
/// scenario data rather than something the ruleset knows by name, so a bundle without a scenario starts
/// with no world and the panel says so, instead of the product pretending to be somewhere.
/// </remarks>
internal static class MightAndMagic7World
{
    /// <summary>The definition kind a scenario's starting entry uses.</summary>
    internal const string StartDefinitionKind = "scenario-start";

    /// <summary>
    /// The definition kind a place's collision artifact is declared under, one entry per place, keyed by
    /// the place's own id.
    /// </summary>
    /// <remarks>
    /// The importer emits this document into the imported world pack, and emits one only for a place whose
    /// geometry it could close: a place it refused has no entry here, which the mover admits as an empty
    /// scene and reports rather than inventing ground. The document's own shape is the engine's, and the
    /// ruleset reads it without rewriting a field of it.
    /// </remarks>
    internal const string GeometryDefinitionKind = "place-geometry";

    /// <summary>The property of a geometry entry that holds the engine's canonical collision artifact.</summary>
    internal const string GeometryArtifactProperty = "artifact";

    /// <summary>
    /// The definition kind that carries the reaches a walking party takes this game's transitions
    /// through, one entry per trigger face, keyed by the link it takes and the face it came from.
    /// </summary>
    /// <remarks>
    /// The importer emits this document into the imported world pack from the event faces the maps carry.
    /// A place with no entry here is a place whose transitions cannot be walked into: the world then moves
    /// the party and never the place, and the write report names every such link rather than the product
    /// inventing a trigger position for it.
    /// </remarks>
    internal const string EntranceDefinitionKind = "place-entrance";

    /// <summary>Composes the world, or null when the content does not place the party anywhere.</summary>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="context">What the host handed the ruleset, which carries the engine the world moves in.</param>
    /// <param name="clock">
    /// This game's one clock, which the world reads its days from and charges a journey's time to. A session
    /// holds no second source of game time, so travel time and respawn are measured in one clock.
    /// </param>
    /// <param name="resources">
    /// The party's own accounts, which a journey charges its provisions to. It is null when content declared
    /// no party, and a quoted food cost is then reported rather than quietly dropped.
    /// </param>
    /// <param name="entity">
    /// The party itself, which an interaction reaches for what it requires and gives what it finds. It is
    /// null when content declared no party, and a requirement that needs one is then unmet rather than
    /// satisfied by an invented carrier.
    /// </param>
    /// <param name="resume">
    /// The save a session is resuming from, when this world is being composed for a load. A world composed
    /// from a save takes the party's place and pose and every place's remembered state from that save, and
    /// needs no scenario start: where the party stands is what the save says, and a scenario that has since
    /// changed cannot quietly move a resumed party elsewhere.
    /// </param>
    /// <param name="services">
    /// This game's answers about services, when its content declares any. A counter the party talks to is a
    /// service placement, and the interaction mechanism needs the same answers the service mechanism itself
    /// is composed over — the one policy instance, so what a use offers and what a transaction does can
    /// never be two readings of one placement. Without it a service placement is not a target at all and
    /// walking into a shop is not possible, which is what a session with no services gets.
    /// </param>
    internal static SessionWorld? Compose(
        ContentCatalog? catalog,
        RulesetSessionContext context,
        GameClock clock,
        PartyResourceLedger? resources,
        PartyEntity? entity = null,
        SessionSave? resume = null,
        MightAndMagic7Services? services = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);
        if (catalog is null) return null;
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        if (graph.Places.Count == 0) return null;

        // What content says about interaction is read once, here, with the world: a requirement that names a
        // kind this game does not know is a defect the load names, rather than a lock that quietly is not
        // there when somebody tries the door.
        MightAndMagic7Interaction.Validate(catalog);

        // Which places are clocked is read once here, from the counters the places keep and the hours a place
        // states for itself, and the one reading is handed to the interaction rule and to the world: the door
        // a use is refused at and the hours the panel shows are the same schedule, so they cannot disagree.
        MightAndMagic7Schedules schedules = MightAndMagic7Schedules.Read(catalog, graph, services);
        foreach (string note in schedules.Notes)
        {
            context.Engine?.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Info,
                DiagnosticsDisposition.Accepted,
                Source: "schedule",
                Code: "place-unclocked",
                Message: note,
                Correlation: string.Empty));
        }

        PlaceRespawnRule respawn = PlaceRespawnRule.FromContent();
        PartyPoseOwner party;
        PlaceStateLedger places;
        if (resume is { } save)
        {
            // Everything the save names is judged against this world before any of it is rebuilt, so a
            // document that does not fit leaves no half-composed world behind.
            MightAndMagic7Persistence.RequireLoadable(save, graph);

            // The pose owner is created at the pose the party resumes at, which goes through the same
            // admission an entered pose goes through: a save can never put the party where its place would
            // refuse it, and the refusal would have been named above rather than thrown here.
            party = new PartyPoseOwner(save.World.Pose, MightAndMagic7Movement.Facing);
            places = PlaceStateLedger.Restore(graph, respawn, save.World.Places);
        }
        else
        {
            (PlaceId Place, string? EntryPoint)? start = ReadStart(catalog);
            if (start is not { } begin) return null;

            PlaceDefinition place = graph.Require(begin.Place);
            PlacePose pose = begin.EntryPoint is { Length: > 0 } entryPointId
                ? graph.ResolveArrival(new PlaceTransition(null, begin.Place, PlaceArrival.AtEntryPoint(entryPointId), "scenario-start"))
                : PlacePose.Origin;

            party = new PartyPoseOwner(new PartyPose(place.Id, pose), MightAndMagic7Movement.Facing);
            places = new PlaceStateLedger(graph, respawn);
        }

        return new SessionWorld(
            graph,
            party,
            places,

            // The cost rule is composed over the party itself, because a fare is the party's own passage:
            // the counter that sells one writes it on the party and the road that honours it reads and
            // tears the same state, so a seat bought in one town cannot be spent in another's name.
            new MightAndMagic7TravelCostRule(entity),
            clock,
            Mover(party, context),
            context.Engine?.Diagnostics,
            PlaceEntranceLoader.Load(catalog, graph),
            clock,
            resources,
            entity,
            new InteractionPolicy(Interaction(services, schedules.Schedule), MightAndMagic7Movement.Space, MightAndMagic7Interaction.Aim),
            schedules.Schedule);
    }

    /// <summary>
    /// This game's answers about using what a place holds, with its counters added when it has any.
    /// </summary>
    /// <remarks>
    /// The service answers wrap the doors-and-fixtures answers rather than replacing them, which is what
    /// keeps one interaction mechanism: a placement this game's service content calls a counter is a person
    /// to talk to, and everything else is answered exactly as it was. The doors-and-fixtures answers carry
    /// this game's schedule, which is what locks a door outside the hours its place keeps.
    /// </remarks>
    private static IInteractionRule Interaction(MightAndMagic7Services? services, PlaceSchedule schedule) =>
        services is null
            ? new MightAndMagic7Interaction(schedule)
            : new MightAndMagic7ServiceInteraction(services, new MightAndMagic7Interaction(schedule));

    /// <summary>
    /// The party's movement, when the host handed this ruleset an engine to move in.
    /// </summary>
    /// <remarks>
    /// Movement needs both halves: places to walk between, which the world supplies, and an engine whose
    /// spatial service owns collision and resolves the step. Without the engine there is no movement at
    /// all, and the session says so by stepping nothing rather than by standing in for a spatial service
    /// the product does not have.
    /// </remarks>
    private static IPartyMover? Mover(PartyPoseOwner party, RulesetSessionContext context)
    {
        if (context.Engine is not { } engine) return null;

        // A context that carries no engine, or an engine that answers with no spatial service, means the
        // product has no collision to move through: there is no movement then, rather than movement that
        // walks through walls.
        if (engine.Spatial is not { } spatial) return null;

        PartyMovement movement = new(
            spatial,
            party,
            MightAndMagic7Movement.Space,
            MightAndMagic7Movement.Session,
            MightAndMagic7Movement.Tuning(spatial));

        // A place's geometry comes from the catalog when content carries any. Without a catalog there is no
        // world either, but the mover is composed here where both are still in hand.
        IPlaceGeometrySource? geometry = context.Content is { } catalog
            ? new ContentPlaceGeometry(catalog, GeometryDefinitionKind, GeometryArtifactProperty)
            : null;

        return new EnginePartyMover(spatial, movement, engine.Content, geometry);
    }

    private static (PlaceId Place, string? EntryPoint)? ReadStart(ContentCatalog catalog)
    {
        foreach ((_, _, ContentEntry entry) in catalog.Entries(StartDefinitionKind))
        {
            string place = entry.GetId("place");
            if (place.Length == 0) continue;
            string entryPoint = entry.GetId("entryPoint");
            return (new PlaceId(place), entryPoint.Length == 0 ? null : entryPoint);
        }

        return null;
    }
}
