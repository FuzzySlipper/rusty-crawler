using PartyRpg.Kit.Content;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
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
    internal static SessionWorld? Compose(
        ContentCatalog? catalog,
        RulesetSessionContext context,
        GameClock clock,
        PartyResourceLedger? resources)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);
        if (catalog is null) return null;
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        if (graph.Places.Count == 0) return null;

        (PlaceId Place, string? EntryPoint)? start = ReadStart(catalog);
        if (start is not { } begin) return null;

        PlaceDefinition place = graph.Require(begin.Place);
        PlacePose pose = begin.EntryPoint is { Length: > 0 } entryPointId
            ? graph.ResolveArrival(new PlaceTransition(null, begin.Place, PlaceArrival.AtEntryPoint(entryPointId), "scenario-start"))
            : PlacePose.Origin;

        PartyPoseOwner party = new(new PartyPose(place.Id, pose), MightAndMagic7Movement.Facing);
        PlaceStateLedger places = new(graph, PlaceRespawnRule.FromContent());
        return new SessionWorld(
            graph,
            party,
            places,
            new MightAndMagic7TravelCostRule(),
            clock,
            Mover(party, context),
            context.Engine?.Diagnostics,
            PlaceEntranceLoader.Load(catalog, graph),
            clock,
            resources);
    }

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
