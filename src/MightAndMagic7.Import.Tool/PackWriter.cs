using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MightAndMagic7.Import.Collision;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Tool;

/// <summary>What one import wrote.</summary>
/// <param name="OutputRoot">The directory the packs were written to.</param>
/// <param name="Provenance">Where the content came from.</param>
/// <param name="Packs">The packs written, with their entry counts.</param>
/// <param name="Geometry">What every place's collision emission produced.</param>
/// <param name="Entrances">What every place's walk-in entrance emission produced.</param>
/// <param name="Containers">What every place's container emission produced, which is what the places document carries.</param>
/// <param name="Services">
/// What the building table's emission produced: the counters, the households, the rows nothing was placed
/// for, and the passages the stables and docks sell.
/// </param>
/// <param name="People">
/// What the game's people tables and the maps' actor records produced: who exists, what they can be asked
/// about, where they stand, and everything nothing was placed for.
/// </param>
/// <param name="Encounters">
/// What the levels' spawn records produced: the encounter each actor spawn asks for, and every record nothing
/// was emitted for with its reason.
/// </param>
/// <param name="Creatures">
/// What the maps' own actor records that are not people produced: the creatures each level is built holding,
/// numbered as the level numbers them, and every record nothing was placed for with its reason.
/// </param>
/// <param name="Maps">What the places' automaps hold.</param>
/// <param name="Fixtures">
/// What the fixture emission produced: the things whose use raises one of a place's own events, the events
/// themselves, and the raised events another emitter answers for.
/// </param>
/// <param name="Globals">The global program's events, normalized as a place's are, and which of them a topic raises.</param>
internal sealed record PackWriteResult(
    string OutputRoot,
    InstallProvenance Provenance,
    IReadOnlyList<(string PackId, int Documents, int Entries)> Packs,
    CollisionSummary Geometry,
    PlaceEntranceSummary Entrances,
    PlaceContainerSummary Containers,
    PlaceServiceSummary Services,
    PlacePeopleSummary People,
    PlaceEncounterSummary Encounters,
    PlaceCreatureSummary Creatures,
    PlaceMapSummary Maps,
    PlaceFixtureSummary Fixtures,
    GlobalEventSummary Globals)
{
    /// <summary>The pack ids, in the order they were written.</summary>
    internal IReadOnlyList<string> PackIds => [.. Packs.Select(pack => pack.PackId)];
}

/// <summary>
/// Writes the normalized content packs a product loads.
/// </summary>
/// <remarks>
/// The output is meant to be reproducible: two runs over the same installation produce byte-identical
/// packs, so a difference in the product can be traced to a difference in the data rather than to the
/// importer. That rules out timestamps, ordering by anything but the data, and culture-dependent
/// formatting, all of which are easy to introduce and hard to notice.
/// </remarks>
internal static partial class PackWriter
{
    private const int SchemaVersion = 1;

    /// <summary>
    /// The placement kinds this importer emits, in the order a place writes them.
    /// </summary>
    /// <remarks>
    /// The list is what the counts object is written from, so a place always states a count for every
    /// kind that could be there — including the zeroes a region has for doors and lights — instead of
    /// leaving a checker to infer absence from a missing key.
    /// </remarks>
    private static readonly string[] PlacementKinds =
        ["spawn", "encounter", "actor", "decoration", "door", "light", "container", "sprite", "service", "residence", "person", "fixture"];

    /// <summary>How much of a place's map data an import reads.</summary>
    internal enum MapDetail
    {
        /// <summary>Places and their file links only; no map payload is decoded, so no geometry is emitted.</summary>
        None,

        /// <summary>
        /// Arrival points and collision geometry read from the maps, so a transition can name where the
        /// party lands and the party has something to stand on when it gets there.
        /// </summary>
        EntryPoints,
    }

    /// <summary>
    /// The geometry source names a place's counts are written under, in the order it writes them.
    /// </summary>
    /// <remarks>
    /// Like the placement counts, every place states a count for every source, including the zeroes a
    /// region has for interior faces, so a checker reads absence rather than inferring it.
    /// </remarks>
    private static readonly CollisionSource[] GeometrySources =
    [
        CollisionSource.InteriorFace,
        CollisionSource.Terrain,
        CollisionSource.ModelFace,
    ];

    private static readonly JsonWriterOptions Writer = new() { Indented = true, NewLine = "\n" };

    /// <summary>Writes every pack this importer produces for an installation.</summary>
    internal static PackWriteResult Write(LodInstall install, string outputRoot, MapDetail detail = MapDetail.EntryPoints)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        InstallProvenance provenance = InstallProvenance.Read(install);
        Mm7Tables tables = Mm7Tables.Read(install);
        IReadOnlyList<EvtProgram> programs = EvtProgram.ReadAll(install);
        PlaceGraph graph = PlaceGraph.Build(programs, tables.Maps);
        IReadOnlyDictionary<int, DecodedMap> maps = detail == MapDetail.EntryPoints ? DecodeMaps(install) : new Dictionary<int, DecodedMap>();
        IReadOnlyList<PlaceCollision> collisions = maps.Count == 0 ? [] : EmitCollisions(tables, maps, TerrainTileTable.Read(install));

        // The entrances are derived from the same decoded maps the collision is: a place's trigger faces
        // are map data, so an import that decoded no map has none to derive and says so per link.
        PlaceEntranceSummary entrances = PlaceEntranceEmitter.Emit(graph, maps, programs, tables.People);

        // The global program is what a person's topic runs, normalized into the same steps a place's events are; a
        // topic of an event's number is something its person says even when the topic table names no text for it.
        GlobalEventSummary globals = GlobalEventEmitter.Emit(programs, tables.People, graph, maps, tables.Classes);

        // A place's containers are derived from the map faces whose events open them, which is also where
        // its walk-in reaches come from, so an import that decoded no map has neither.
        PlaceContainerSummary containers = PlaceContainerEmitter.Emit(maps, programs, PlaceMapNumbersTable.Read(tables));

        // The counters are the building table's rows joined to the map faces that raise each house's own
        // event, which is the same reading of the maps the reaches and containers come from: an import that
        // decoded no map places no counter, and says so per row rather than emitting a shop nobody can walk
        // up to.
        PlaceServiceSummary services = PlaceServiceEmitter.Emit(tables.Services, tables, programs, maps);

        // The people the game's own tables carry, joined to the positions the maps give them: a person the
        // NPC table places in a building stands where that building's door is, and a person a map's actor
        // record places stands where the record says. An import that decoded no map has neither, and says
        // so per person rather than emitting somebody nobody can walk up to.
        PlacePeopleSummary people = PlacePeopleEmitter.Emit(tables.People, maps, services, globals.Numbers);

        // The encounters are read from the same decoded spawn records the places document carries, so a
        // spawn point and the encounter it asks for are one reading of one record rather than two.
        PlaceEncounterSummary encounters = PlaceEncounters.Emit(tables, maps);

        // The creatures a level is built holding are the same delta's actor records the people are read from,
        // every one that names no NPC row, placed under the index the level numbers it by.
        PlaceCreatureSummary creatures = PlaceCreatures.Emit(tables, maps);

        // The fixtures are the raised events no other emitter answers for, read from the same decoded faces
        // and the same programs the reaches, containers and counters are, with the text their steps print
        // resolved from each map's own string table.
        PlaceFixtureSummary fixtures = maps.Count == 0
            ? PlaceFixtureSummary.Empty
            : PlaceFixtureEmitter.Emit(maps, programs, MapStrings.ReadAll(install), graph, tables);

        // Each pack this importer owns is written into an empty directory, so a document an earlier importer
        // wrote and this one does not is not left beside the new ones for the loader to find. Other packs under
        // the same root — a scenario the operator staged — are not this importer's and are left alone.
        Directory.CreateDirectory(outputRoot);
        foreach (string pack in new[] { "mm7-world", "mm7-tables" })
        {
            string directory = Path.Combine(outputRoot, pack);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        ((string, int, int) world, PlaceMapSummary mapped) = WriteWorld(
            tables,
            graph,
            provenance,
            Path.Combine(outputRoot, "mm7-world"),
            maps,
            collisions,
            entrances,
            services,
            fixtures);
        List<(string, int, int)> packs =
        [
            WriteTables(tables, provenance, Path.Combine(outputRoot, "mm7-tables"), maps, containers, services, people, encounters, creatures, fixtures, globals),
            world,
        ];
        WriteBundleFragment(outputRoot, provenance, packs);
        return new PackWriteResult(outputRoot, provenance, packs, CollisionSummary.Of(collisions), entrances, containers, services, people, encounters, creatures, mapped, fixtures, globals);
    }

    /// <summary>
    /// Emits every place's collision geometry from the maps that were decoded.
    /// </summary>
    /// <remarks>
    /// A place whose geometry cannot be closed enough is not emitted and not faked: the outcome carries
    /// the reason, the pack carries no entry for it, and the product says on entry that the place has
    /// nothing to stand on. That is worse for one place than a mesh with a hole in it, and better than a
    /// party that falls through a floor.
    /// </remarks>
    private static IReadOnlyList<PlaceCollision> EmitCollisions(Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps, TerrainTileTable tiles)
    {
        List<PlaceCollision> places = [];
        foreach (MapStatsRecord map in tables.Maps.Maps)
        {
            if (!maps.TryGetValue(map.Id, out DecodedMap? decoded)) continue;
            places.Add(PlaceCollisionEmitter.Emit(map.Id, map.FileName, decoded, tiles));
        }

        return places;
    }

    /// <summary>
    /// Decodes every map, failing when one does not decode.
    /// </summary>
    /// <remarks>
    /// A place whose arrival points are missing would still load, and a transition naming an arrival
    /// point in it would then fail at load time with a message about the transition rather than about
    /// the map that could not be read. Failing here keeps the defect where it is.
    /// </remarks>
    private static IReadOnlyDictionary<int, DecodedMap> DecodeMaps(LodInstall install)
    {
        MapDecodeReport report = MapDecoder.DecodeAll(install);
        if (report.FailureCount > 0)
        {
            string failures = string.Join("; ", report.Failures.Take(5).Select(failure => $"{failure.MapId} {failure.FileName}: {failure.Reason}"));
            throw new LodFormatException(
                LodFault.Count,
                $"{report.FailureCount} of {report.MapCount} maps did not decode, so their arrival points cannot be imported: {failures}");
        }

        Dictionary<int, DecodedMap> maps = [];
        foreach (MapDecodeOutcome outcome in report.Decoded)
        {
            if (outcome.Decoded is not null) maps[outcome.Map.Id] = outcome.Decoded;
        }

        return maps;
    }

    /// <summary>
    /// Whether two write roots hold the same bytes for what one write produced: each written pack and the
    /// bundle fragment. Anything else in the root — the operator's own scenario packs, a README — is not
    /// the writer's output, so it neither passes nor fails the check.
    /// </summary>
    internal static bool AreIdentical(string left, string right, PackWriteResult written)
    {
        string fragment = "imported-bundle.json";
        if (!File.ReadAllBytes(Path.Combine(left, fragment)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(right, fragment)))) return false;
        return written.PackIds.All(packId => AreIdentical(Path.Combine(left, packId), Path.Combine(right, packId)));
    }

    /// <summary>Whether two directories hold exactly the same files with the same bytes.</summary>
    internal static bool AreIdentical(string left, string right)
    {
        string[] leftFiles = [.. Directory.EnumerateFiles(left, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(left, path)).Order(StringComparer.Ordinal)];
        string[] rightFiles = [.. Directory.EnumerateFiles(right, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(right, path)).Order(StringComparer.Ordinal)];
        if (!leftFiles.SequenceEqual(rightFiles, StringComparer.Ordinal)) return false;
        foreach (string file in leftFiles)
        {
            if (!File.ReadAllBytes(Path.Combine(left, file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(right, file)))) return false;
        }

        return true;
    }

    private static (string PackId, int Documents, int Entries) WriteTables(
        Mm7Tables tables,
        InstallProvenance provenance,
        string packDirectory,
        IReadOnlyDictionary<int, DecodedMap> maps,
        PlaceContainerSummary containers,
        PlaceServiceSummary services,
        PlacePeopleSummary people,
        PlaceEncounterSummary encounters,
        PlaceCreatureSummary creatures,
        PlaceFixtureSummary fixtures,
        GlobalEventSummary globals)
    {
        List<(string Path, string DocumentId, string Kind, int Entries)> documents =
        [
            ("places.json", "places", "place", WritePlaces(packDirectory, tables, maps, containers, services, people, encounters, creatures, fixtures)),
            ("place-events.json", "place-events", PlaceEventDefinitionKind, WritePlaceEvents(packDirectory, fixtures)),
            ("global-events.json", "global-events", GlobalEventDefinitionKind, WriteGlobalEvents(packDirectory, globals)),
            ("discoveries.json", "discoveries", DiscoveryDefinitionKind, WriteDiscoveries(packDirectory, tables)),
            ("history.json", "history", HistoryDefinitionKind, WriteHistory(packDirectory, tables)),
            ("people.json", "people", PlacePeopleEmitter.PersonDefinitionKind, WritePeople(packDirectory, people)),
            ("topics.json", "topics", TopicDefinitionKind, WriteTopics(packDirectory, people)),
            ("greetings.json", "greetings", GreetingDefinitionKind, WriteGreetings(packDirectory, tables)),
            ("services.json", "services", "service", WriteServices(packDirectory, services)),
            ("classes.json", "classes", "class", WriteClasses(packDirectory, tables)),
            ("skills.json", "skills", "skill", WriteSkills(packDirectory, tables)),
            ("spells.json", "spells", "spell", WriteSpells(packDirectory, tables)),
            ("monsters.json", "monsters", "monster", WriteMonsters(packDirectory, tables)),
            ("hostility.json", "hostility", "hostility", WriteHostility(packDirectory, tables)),
            ("items.json", "items", "item", WriteItems(packDirectory, tables)),
            ("potions.json", "potions", "potion", WritePotions(packDirectory, tables)),
            ("quests.json", "quests", "quest", WriteQuests(packDirectory, tables)),
        ];
        // A place's own document names the people standing in it, so every one of them is declared as a
        // reference: a placement naming a person no entry describes is then a load defect rather than
        // somebody who silently is not there.
        IReadOnlyList<string> peopleReferences =
        [
            .. people.Placements.Select(placement => $"{PlacePeopleEmitter.PersonDefinitionKind}:{placement.PersonId}").Distinct().Order(StringComparer.Ordinal),
            .. people.Households.SelectMany(household => household.PersonIds)
                .Select(id => $"{PlacePeopleEmitter.PersonDefinitionKind}:{id}").Distinct().Order(StringComparer.Ordinal),

            // Every encounter names the monster rows its graded variants are, so the place's own document
            // refers to each row an encounter could resolve to: a variant whose row the pack does not carry is
            // then a load defect rather than a creature nothing can say the hit points of.
            .. encounters.Placements
                .SelectMany(placement => placement.Variants)
                .Select(variant => $"monster:{variant.MonsterId.ToString(CultureInfo.InvariantCulture)}").Distinct().Order(StringComparer.Ordinal),
        ];
        // A place's own events name the place whose program holds them, so the document refers to each: an
        // event for a place the pack does not carry is then a load defect rather than a program nothing runs.
        IReadOnlyList<string> eventReferences =
        [
            .. fixtures.Events.Select(placeEvent => placeEvent.PlaceId).Distinct().Order()
                .Select(place => $"place:{place.ToString(CultureInfo.InvariantCulture)}"),
        ];
        WriteManifest(
            packDirectory,
            "mm7-tables",
            "definitions",
            provenance,
            [.. documents.Select(document => (
                document.Path,
                document.DocumentId,
                document.Kind,
                (IReadOnlyList<string>)(document.DocumentId switch
                {
                    "places" => peopleReferences,
                    "place-events" => eventReferences,
                    _ => [],
                })))]);
        return ("mm7-tables", documents.Count, documents.Sum(document => document.Entries));
    }

    private static ((string PackId, int Documents, int Entries) Pack, PlaceMapSummary Maps) WriteWorld(
        Mm7Tables tables,
        PlaceGraph graph,
        InstallProvenance provenance,
        string packDirectory,
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<PlaceCollision> collisions,
        PlaceEntranceSummary entrances,
        PlaceServiceSummary services,
        PlaceFixtureSummary fixtures)
    {
        int links = WritePlaceGraph(packDirectory, graph, tables, maps, entrances);
        int places = WritePlaceGeometry(packDirectory, collisions, fixtures);
        int reachCount = WritePlaceEntrances(packDirectory, entrances);
        PlaceMapSummary mapped = WritePlaceMaps(packDirectory, maps);
        // Every place is referenced by the graph.
        IReadOnlyList<string> graphReferences =
            [.. tables.Maps.Maps.Select(map => $"place:{map.Id.ToString(CultureInfo.InvariantCulture)}")];
        IReadOnlyList<string> geometryReferences =
            [.. collisions.Where(place => place.Emitted).Select(place => $"place:{place.PlaceId.ToString(CultureInfo.InvariantCulture)}")];

        // Every place the per-map table names is walked for its own map, so a place that produced no entry is a
        // place the payload held nothing to draw rather than a place this writer forgot.
        IReadOnlyList<string> mapReferences =
            [.. maps.Keys.Order().Select(place => $"place:{place.ToString(CultureInfo.InvariantCulture)}")];

        // A plate refers to the travel links its event's moves take and to the place it lies in, so a reader
        // that resolves every reference is told the plate belongs to roads the world holds.
        IReadOnlyList<string> entranceReferences =
        [
            .. entrances.Entrances.SelectMany(entrance => entrance.Links).Distinct().Order()
                .Select(index => $"travel-link:{LinkId(index)}"),
            .. entrances.Entrances.Select(entrance => entrance.FromPlace).Distinct().Order()
                .Select(place => $"place:{place.ToString(CultureInfo.InvariantCulture)}"),
        ];
        WriteManifest(
            packDirectory,
            "mm7-world",
            "world",
            provenance,
            [
                ("place-graph.json", "place-graph", "travel-link", graphReferences),

                // Only the places that produced an artifact are referenced: a reference to a place whose
                // geometry was refused would promise collision the pack does not carry.
                ("place-geometry.json", "place-geometry", "place-geometry", geometryReferences),
                ("place-entrances.json", "place-entrances", "place-entrance", entranceReferences),

                // Every place with a map is referenced by it: the entry is what the automap is drawn from, so
                // a reader that resolves references is told which places the pack can draw at all.
                ("place-map.json", "place-map", "place-map", mapReferences),
            ]);
        return (("mm7-world", 4, links + places + reachCount + mapped.Places), mapped);
    }

    private static int WriteDocument(
        string packDirectory,
        string fileName,
        string documentId,
        string definitionKind,
        IReadOnlyList<(string Id, Action<Utf8JsonWriter> Write)> entries)
    {
        Directory.CreateDirectory(packDirectory);
        using FileStream stream = File.Create(Path.Combine(packDirectory, fileName));
        using Utf8JsonWriter writer = new(stream, Writer);
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString("documentId", documentId);
        writer.WriteString("definitionKind", definitionKind);
        writer.WriteStartArray("entries");
        int count = 0;
        foreach ((string id, Action<Utf8JsonWriter> write) in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            write(writer);
            writer.WriteEndObject();
            count++;
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        return count;
    }

    private static void WriteManifest(
        string packDirectory,
        string packId,
        string kind,
        InstallProvenance provenance,
        IReadOnlyList<(string Path, string DocumentId, string DefinitionKind, IReadOnlyList<string> References)> documents)
    {
        Directory.CreateDirectory(packDirectory);
        using FileStream stream = File.Create(Path.Combine(packDirectory, "pack.json"));
        using Utf8JsonWriter writer = new(stream, Writer);
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString("packId", packId);
        writer.WriteString("kind", kind);
        writer.WriteStartObject("provenance");
        writer.WriteString("description", provenance.Description);
        writer.WriteString("game", provenance.Game);
        writer.WriteString("build", provenance.BuildString);
        writer.WriteString("producer", InstallProvenance.Producer);
        writer.WriteEndObject();
        writer.WriteStartArray("documents");
        foreach ((string path, string documentId, string definitionKind, IReadOnlyList<string> references) in documents)
        {
            writer.WriteStartObject();
            writer.WriteString("path", path);
            writer.WriteString("documentId", documentId);
            writer.WriteString("definitionKind", definitionKind);
            writer.WriteStartArray("references");
            foreach (string reference in references) writer.WriteStringValue(reference);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value)) writer.WriteString(name, value);
    }

    private static void WriteOptionalNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is not null) writer.WriteNumber(name, value.Value);
    }

    private static void WriteOptionalNumber(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is not null) writer.WriteNumber(name, value.Value);
    }

    private static void WriteBundleFragment(string outputRoot, InstallProvenance provenance, IReadOnlyList<(string PackId, int Documents, int Entries)> packs)
    {
        using FileStream stream = File.Create(Path.Combine(outputRoot, "imported-bundle.json"));
        using Utf8JsonWriter writer = new(stream, Writer);
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString("bundleId", "mm7-imported");
        writer.WriteString("ruleset", provenance.Game);
        writer.WriteStartArray("contentPacks");
        foreach ((string packId, _, _) in packs) writer.WriteStringValue(packId);
        writer.WriteEndArray();
        writer.WriteString("description", $"The packs this import wrote, from {provenance.BuildString}. Copy this file to a bundle directory to load them.");
        writer.WriteEndObject();
    }

    /// <summary>The digest of one file, used by the determinism check's message.</summary>
    internal static string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>Reads a written pack's entry count, so the tool can report what it produced.</summary>
    internal static int EntryCount(string documentPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(documentPath, Encoding.UTF8));
        return document.RootElement.GetProperty("entries").GetArrayLength();
    }
}

/// <summary>What an import wrote of the places' own automaps.</summary>
/// <remarks>
/// A summary rather than the rasters themselves: a report needs to say how many places a product can draw and
/// how many squares that is, and the squares live in the pack where the product reads them.
/// </remarks>
/// <param name="Places">How many places carry a map.</param>
/// <param name="Cells">How many squares those maps hold together.</param>
internal readonly record struct PlaceMapSummary(int Places, int Cells);
