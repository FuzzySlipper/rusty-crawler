using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>This game's session composes over the packs written from the operator's own installation.</summary>
/// <remarks>
/// The importer's own suite proves the same over its synthetic installation; this proves it over every row the
/// release actually ships, which is where a table's shape the synthetic data does not carry would surface as a
/// content defect.
/// </remarks>
public sealed class ImportedSessionTests
{
    [ImportedFact("monsters.json")]
    public void The_operators_imported_packs_compose_this_games_session_with_no_content_defect()
    {
        ContentCatalog catalog = ImportedContent.Load();
        Assert.True(catalog.IsValid, string.Join("; ", catalog.Issues.Select(issue => issue.ToString())));

        // The session reads what a bundle naming the import would select, not the whole root: an operator's root
        // also holds the scenario packs their live checks staged, and no bundle selects all of those at once.
        ContentCatalog written = ImportedContent.Written(catalog);
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create();
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(new RulesetSessionContext(
            new EngineUiProjectionChannel(ui, new UiStreamRequest(Declared.UiStream, Declared.UiContract)),
            new BundleSelection("imported", written.Packs.Count),
            written,
            Engine: context.Engine));
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        Assert.NotEmpty(ui.Projections);
    }

    [ImportedFact("services.json")]
    public void This_games_fare_network_over_the_imported_counters_sells_every_stop_to_every_other()
    {
        // The import states seven stables and seven docks and where each stands, and no destination: the network
        // is this game's policy over them — each of the seven coach towns reaches the six others and each of the
        // seven ports the six others, eighty-four sold crossings in all, every one timed by its route's tuning.
        ContentCatalog catalog = ImportedContent.Load();
        Assert.True(catalog.IsValid, string.Join("; ", catalog.Issues.Select(issue => issue.ToString())));
        PartyRpg.Kit.World.PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        MightAndMagic7FareNetwork network = MightAndMagic7FareNetwork.Read(catalog);

        Assert.Equal(7, network.Stops(MightAndMagic7FareDays.CoachRoute).Count);
        Assert.Equal(7, network.Stops(MightAndMagic7FareDays.BoatRoute).Count);
        List<PartyRpg.Kit.World.PlaceTransition> sold = [.. graph.Transitions.Where(transition => transition.IsFare)];
        Assert.Equal(84, sold.Count);
        Assert.All(sold, transition => Assert.True(transition.FareDays >= 1));
        Assert.Equal(42, sold.Count(transition => transition.FareRoute == MightAndMagic7FareDays.CoachRoute));
    }

    [ImportedFact("places.json")]
    public void The_imported_places_hold_each_arrival_point_once_and_every_named_arrival_resolves()
    {
        // The graph refuses a place declaring one arrival-point id twice (entry-point-id-reused); the release's
        // own maps carry seventy-six places and eighty-three arrival points, every one under its own name, so the
        // import loads unchanged and every transition naming a point lands at exactly one.
        ContentCatalog catalog = ImportedContent.Load();
        Assert.True(catalog.IsValid, string.Join("; ", catalog.Issues.Select(issue => issue.ToString())));
        PartyRpg.Kit.World.PlaceGraph graph = MightAndMagic7World.Graph(catalog);

        Assert.Equal(76, graph.Places.Count);
        Assert.Equal(83, graph.Places.Sum(place => place.EntryPoints.Count));
        Assert.All(graph.Places, place => Assert.Equal(
            place.EntryPoints.Count,
            place.EntryPoints.Select(point => point.Id).Distinct(PartyRpg.Kit.World.PlaceDefinition.EntryPointIds).Count()));
        Assert.All(graph.Transitions.Where(transition => transition.Arrival.IsEntryPoint), transition => graph.ResolveArrival(transition));
    }
}
