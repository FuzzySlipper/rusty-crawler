using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;

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

    /// <summary>The facing rule this game's data uses: 2048 units to a turn, as the maps store it.</summary>
    private static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);

    /// <summary>Composes the world, or null when the content does not place the party anywhere.</summary>
    internal static SessionWorld? Compose(ContentCatalog? catalog, IWorldTimeSource? time = null)
    {
        if (catalog is null) return null;
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        if (graph.Places.Count == 0) return null;

        (PlaceId Place, string? EntryPoint)? start = ReadStart(catalog);
        if (start is not { } begin) return null;

        PlaceDefinition place = graph.Require(begin.Place);
        PlacePose pose = begin.EntryPoint is { Length: > 0 } entryPointId
            ? graph.ResolveArrival(new PlaceTransition(null, begin.Place, PlaceArrival.AtEntryPoint(entryPointId), "scenario-start"))
            : PlacePose.Origin;

        PlaceStateLedger places = new(graph, PlaceRespawnRule.FromContent());
        SessionWorld world = new(graph, new PartyPoseOwner(new PartyPose(place.Id, pose), Facing), places, new MightAndMagic7TravelCostRule(), time);
        return world;
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
