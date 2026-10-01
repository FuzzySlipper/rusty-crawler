using System.Text.Json;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Media;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Tool;

/// <summary>
/// The recorded inventory of the operator's installation, used as the acceptance oracle for the
/// readers. Every number here is written down in <c>docs/research/mm7-data-inventory.md</c>; a reader
/// that drifts from the data fails this check instead of failing quietly.
/// </summary>
/// <remarks>
/// The tables and the place graph are read directly. The figures the repository's documents state about
/// what an import yields — maps, places, arrivals, doors, containers, people, encounters, reaches, geometry,
/// media — are read from the same decoding and the same writers an operator runs, into a scratch directory
/// this check deletes, so a figure stated in prose is a figure something asserts.
/// </remarks>
internal static class InventoryCheck
{
    /// <summary>The release the recorded inventory was taken from, as the installation's own files name it.</summary>
    /// <remarks>
    /// Every count here is that release's. Another edition — another language, another patch — is a different
    /// set of files, so a mismatch there is reported as the difference between the two releases rather than as
    /// a reader that drifted.
    /// </remarks>
    internal const string RecordedRelease = "Update v. 1.1 (Might and Magic 7: For Blood and Honor build 1207658916 English)";

    internal static int Run(string installRoot)
    {
        LodInstall install = LodInstall.Open(installRoot);
        string release = InstallProvenance.Read(install).Build;
        Mm7Tables tables = Mm7Tables.Read(install);
        TableInventory inventory = TableInventory.Read(install);
        IReadOnlyList<EvtProgram> programs = EvtProgram.ReadAll(install);
        PlaceGraph graph = PlaceGraph.Build(programs, tables.Maps);

        List<string> failures = [];
        Check(failures, "archives", 5, install.ArchiveNames().Count);
        Check(failures, "entries decoded", 15548, Sum(install, out int undecoded));
        Check(failures, "entries not decoded", 0, undecoded);
        Check(failures, "text tables", 34, inventory.TextTables.Count);
        Check(failures, "classes", 36, tables.Classes.Ranks.Count);
        Check(failures, "skills", 37, tables.Skills.Skills.Count);
        Check(failures, "maps", 76, tables.Maps.Maps.Count);
        Check(failures, "buildings", 525, tables.Services.Buildings.Count);
        Check(failures, "services", 172, tables.Services.Services.Count());
        Check(failures, "monsters", 276, tables.Monsters.Monsters.Count);
        Check(failures, "spells", 99, tables.Spells.Spells.Count);
        Check(failures, "spell schools", 9, tables.Spells.Schools.Count);
        Check(failures, "quests", 512, tables.Quests.Quests.Count);
        Check(failures, "item rows", 800, tables.Items.Items.Count);
        Check(failures, "event programs", 77, programs.Count);
        Check(failures, "map moves", 271, graph.MoveInstructionCount);
        Check(failures, "map links", 193, graph.Links.Count);
        Check(failures, "within-map moves", 78, graph.WithinMapMoves.Count);
        Check(failures, "exit instructions", 1552, graph.ExitInstructionCount);
        Check(failures, "programs without a map", 1, graph.ProgramsWithoutAMap.Count);

        CheckImport(failures, install, tables);

        // The older table set from the previous game in the family shares these names with the rules
        // archive; the check exists so the trap cannot disappear unnoticed.
        string[] sharedNames = ["CLASS.TXT", "MAPSTATS.TXT", "SPELLS.TXT"];
        string[] ambiguous = [.. inventory.AmbiguousTables.Select(table => table.Name)];
        foreach (string name in sharedNames)
        {
            if (!ambiguous.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add($"expected '{name}' to be present in more than one archive, but only one holds it");
            }
        }

        if (failures.Count > 0 && !string.Equals(release, RecordedRelease, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"This installation is '{release}' and the recorded inventory is of '{RecordedRelease}': the counts below differ between the two releases, which is not in itself a reader that drifted.");
        }

        foreach (string failure in failures) Console.Error.WriteLine($"inventory mismatch: {failure}");
        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                install = install.Root,
                release,
                recordedRelease = RecordedRelease,
                result = failures.Count == 0 ? "pass" : "fail",
                checks = _checks + sharedNames.Length,
                failures,
            },
            new JsonSerializerOptions { WriteIndented = true }));
        return failures.Count == 0 ? 0 : 1;
    }

    private static int _checks;

    /// <summary>What decoding every map and writing every pack yields, against the figures the documents state.</summary>
    private static void CheckImport(List<string> failures, LodInstall install, Mm7Tables tables)
    {
        MapDecodeReport maps = MapDecoder.DecodeAll(install);
        List<DecodedMap> decoded = [.. maps.Decoded.Select(outcome => outcome.Decoded).OfType<DecodedMap>()];
        Check(failures, "maps decoded", 76, maps.DecodedCount);
        Check(failures, "maps not decoded", 0, maps.FailureCount);
        Check(failures, "arrival points", 83, maps.Total.EntryPoints);
        Check(failures, "chest records", 1520, maps.Total.Chests);
        Check(failures, "sprite objects", 722, maps.Total.SpriteObjects);
        Check(failures, "doors in use", 786, decoded.Sum(map => map.Doors.Count(door => door.InUse)));
        Check(failures, "interiors with a door in use", 54, decoded.Count(map => map.Doors.Any(door => door.InUse)));
        Check(failures, "decorations raising an event", 60, decoded.Sum(map => map.Decorations.Count(decoration => decoration.EventId != 0)));
        Check(failures, "actors", 826, decoded.Sum(map => map.Delta?.Actors.Count ?? 0));
        Check(failures, "actors naming a person", 123, decoded.Sum(map => map.Delta?.Actors.Count(actor => actor.IsPerson) ?? 0));

        string scratch = Directory.CreateTempSubdirectory("mm7import-verify-").FullName;
        try
        {
            PackWriteResult written = PackWriter.Write(install, Path.Combine(scratch, "packs"));
            Check(failures, "places with geometry", 76, written.Geometry.EmittedCount);
            Check(failures, "regions", 13, written.Geometry.EmittedOf(MapKind.Outdoor));
            Check(failures, "interiors", 63, written.Geometry.EmittedOf(MapKind.Indoor));
            Check(failures, "collision triangles", 824320, written.Geometry.Triangles);
            Check(failures, "transition reaches", 532, written.Entrances.ReachCount);
            Check(failures, "links with a reach", 155, written.Entrances.LinkCount);
            Check(failures, "containers", 357, written.Containers.ContainerCount);
            Check(failures, "places with a container", 57, written.Containers.PlaceCount);
            Check(failures, "enterable services", 136, written.Services.CounterCount);
            Check(failures, "service placements", 358, written.Services.PlacementCount);
            Check(failures, "stables and docks", 14, written.Services.FareCounterCount);
            Check(failures, "people inside buildings", 247, written.People.ResidentCount);
            Check(failures, "buildings with people", 195, written.People.HouseholdCount);
            Check(failures, "unreachable residents", 2, written.People.UnreachableResidentCount);
            // The importer emits encounters, not creatures: which grade and how many are the ruleset's draw when
            // a place is populated. The creature figures are therefore the range the slots' own counts allow —
            // the floor, which is every random slot at its fewest, and the ceiling, every one at its most.
            Check(failures, "encounters", 1800, written.Encounters.EncounterCount);
            Check(failures, "places with encounters", 72, written.Encounters.PopulatedPlaces);
            Check(failures, "spawn records refused", 43, written.Encounters.Refusals.Count);
            Check(failures, "encounters with a drawn grade", 1775, written.Encounters.DrawnGrades);
            Check(failures, "fewest creatures the encounters resolve to", 1900, written.Encounters.FewestCreatures);
            Check(failures, "most creatures the encounters resolve to", 5458, written.Encounters.MostCreatures);

            // A fixture is a clicked face group or decoration whose event no other emitter answers for; its event
            // and the timers that keep what it gives are carried as normalized steps, and the notes those steps
            // write are the discovery table's rows.
            Check(failures, "fixtures", 1097, written.Fixtures.Fixtures.Count);
            Check(failures, "places with a fixture", 66, written.Fixtures.PlaceCount);
            Check(failures, "fixture events", 495, written.Fixtures.FixtureEventCount);
            Check(failures, "timed events carried", 56, written.Fixtures.TriggeredEventCount);
            Check(failures, "place events carried", 525, written.Fixtures.Events.Count);
            Check(failures, "fixture event steps", 3271, written.Fixtures.Events.Where(placeEvent => placeEvent.Raised).Sum(placeEvent => placeEvent.Steps.Count));
            Check(failures, "raised events a door answers for", 382, written.Fixtures.OwnedElsewhere.GetValueOrDefault("change-door-state"));
            Check(failures, "raised events without instructions", 56, written.Fixtures.RaisedWithoutInstructions);
            Check(failures, "discovery notes", 186, tables.Discoveries.Rows.Count);
            Check(failures, "discovery rows without a note", 69, tables.Discoveries.SkippedRows);

            MediaManifest media = MediaExtractor.Extract(install, Path.Combine(scratch, "media"));
            Check(failures, "media emitted", 17681, media.EmittedCount);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static void Check(List<string> failures, string what, int expected, int actual)
    {
        _checks++;
        if (expected != actual) failures.Add($"{what}: expected {expected}, read {actual}");
    }

    private static int Sum(LodInstall install, out int undecoded)
    {
        int total = 0;
        undecoded = 0;
        foreach (string name in install.ArchiveNames())
        {
            LodArchive archive = install.Archive(name);
            foreach (LodEntry entry in archive.Entries)
            {
                try
                {
                    archive.Read(entry);
                    total++;
                }
                catch (LodFormatException)
                {
                    undecoded++;
                }
            }
        }

        return total;
    }
}
