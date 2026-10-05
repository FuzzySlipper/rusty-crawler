using System.Security.Cryptography;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>Structural coverage of every imported place, with an optional operator reachability report.</summary>
public sealed class WorldContentInventoryTests
{
    [ImportedFact("places.json")]
    public void Every_imported_place_has_geometry_arrivals_and_resolved_placement_references()
    {
        ContentCatalog catalog = ImportedContent.Playable();
        Assert.True(catalog.IsValid, string.Join("; ", catalog.Issues));
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlacePopulationContent population = PlacePopulationContent.Read(graph);
        var entrances = PlaceEntranceLoader.Load(catalog, graph);
        MightAndMagic7Interaction.Validate(catalog);
        _ = new MightAndMagic7TravelCostRule(catalog: catalog);
        _ = new MightAndMagic7ItemMagic(catalog, null, null, null, () => null);
        Assert.Equal(76, graph.Places.Count);
        Assert.Equal(13, graph.Places.Count(place => place.Kind == PlaceKind.Region));
        Assert.Equal(63, graph.Places.Count(place => place.Kind == PlaceKind.Interior));

        HashSet<string> Ids(string kind) => [.. catalog.Entries(kind).Select(row => row.Entry.Id)];
        foreach (string kind in new[] { "place-geometry", "place-map", "place-render" })
            Assert.Equal(Ids("place").Order(), Ids(kind).Order());
        HashSet<string> people = Ids("person"), services = Ids("service"), monsters = Ids("monster"), events = Ids("place-event");
        foreach (PlaceDefinition place in graph.Places)
        {
            Assert.NotEmpty(place.EntryPoints);
            foreach (PlacementDefinition placement in population.PlacementsOf(place.Id))
            {
                ContentEntry source = placement.Source;
                foreach (JsonElement person in source.GetArray("people")) Assert.Contains(person.GetString()!, people);
                if (placement.Content.Kind == "service") Assert.Contains(source.GetId("houseId"), services);
                if (placement.Content.Kind is "actor" or "monster") Assert.Contains(source.GetId("monster"), monsters);
                if (placement.Content.Kind is "fixture" or "floor-trigger")
                    Assert.Contains($"{place.Id}.{source.GetId("eventId")}", events);
                foreach (JsonElement variant in source.GetArray("variants"))
                    Assert.Contains(ContentEntry.ReadId(variant, "monster"), monsters);
            }
        }
        Assert.All(graph.Transitions, transition => graph.ResolveArrival(transition));
        foreach (PlaceEntrance entrance in entrances)
            if (entrance.Raises is { } raises)
                Assert.Contains(population.PlacementsOf(entrance.Place), placement => placement.Content == raises);

        // This is an optimistic bound, not a proof of ordinary play: admit every world-issued arrival and
        // ignore event gates. A place outside even this set cannot be reached through the loaded graph.
        var links = catalog.Entries("travel-link").ToDictionary(row => row.Entry.Id, row => row.Entry);
        var usable = graph.Transitions.Where(link => link.IsFare || links[link.Source].GetString("disposition") != "unreachable").ToArray();
        HashSet<PlaceId> reachable = [new("1")];
        Dictionary<PlaceId, string> via = [];
        bool changed;
        do
        {
            changed = false;
            foreach (PlaceTransition link in usable)
                if ((link.From is null || reachable.Contains(link.From.Value)) && reachable.Add(link.To))
                {
                    via[link.To] = link.Source;
                    changed = true;
                }
        } while (changed);
        Assert.Equal(graph.Places.Count, reachable.Count);

        if (Environment.GetEnvironmentVariable("CRAWLER_WORLD_REPORT") is not { Length: > 0 } report) return;
        var documents = catalog.Packs.SelectMany(pack => pack.Documents.Select(document => new
        {
            pack = pack.PackId,
            document = document.DocumentId,
        })).ToArray();
        var result = new
        {
            structuralValidation = "passed",
            reachabilityMethod = "Optimistic upper bound: canonical ruleset graph including fare network; all world-issued arrivals admitted; conditional gates ignored; unreachable source events excluded. Does not prove physical reach or successful event execution.",
            allPlacesReachableEvenOptimistically = reachable.Count == graph.Places.Count,
            places = graph.Places.Count,
            transitions = graph.Transitions.Count,
            fareTransitions = graph.Transitions.Count(link => link.IsFare),
            entrances = entrances.Count,
            entryPoints = graph.Places.Sum(place => place.EntryPoints.Count),
            optimisticReachable = reachable.Count,
            unresolvedPlacementReferences = 0,
            danglingTransitions = 0,
            placesWithoutEntryPoints = 0,
            documents,
            tablesSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ImportedContent.Table("places.json")))),
            inventory = graph.Places.Select(place => new
            {
                id = place.Id.Value, place.Name, kind = place.Kind.ToString(),
                entryPoints = place.EntryPoints.Select(point => point.Id),
                placements = population.PlacementsOf(place.Id).Count,
                optimisticReachable = reachable.Contains(place.Id),
                via = via.GetValueOrDefault(place.Id),
            }),
        };
        File.WriteAllText(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
