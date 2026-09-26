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
/// <param name="Creatures">
/// What the levels' spawn records produced: the creatures standing in each place, and every record nothing
/// was emitted for with its reason.
/// </param>
internal sealed record PackWriteResult(
    string OutputRoot,
    InstallProvenance Provenance,
    IReadOnlyList<(string PackId, int Documents, int Entries)> Packs,
    CollisionSummary Geometry,
    PlaceEntranceSummary Entrances,
    PlaceContainerSummary Containers,
    PlaceServiceSummary Services,
    PlacePeopleSummary People,
    PlaceCreatureSummary Creatures)
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
internal static class PackWriter
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
        ["spawn", "monster", "decoration", "door", "light", "container", "sprite", "service", "residence", "person"];

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
        IReadOnlyList<PlaceCollision> collisions = maps.Count == 0 ? [] : EmitCollisions(tables, maps);

        // The entrances are derived from the same decoded maps the collision is: a place's trigger faces
        // are map data, so an import that decoded no map has none to derive and says so per link.
        PlaceEntranceSummary entrances = PlaceEntranceEmitter.Emit(graph, maps);

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
        PlacePeopleSummary people = PlacePeopleEmitter.Emit(tables.People, maps, services);

        // The creatures are emitted from the same decoded spawn records the places document carries, so a
        // spawn point and the creatures standing on it are one reading of one record rather than two.
        PlaceCreatureSummary creatures = PlaceCreatures.Emit(tables, maps);

        Directory.CreateDirectory(outputRoot);
        List<(string, int, int)> packs =
        [
            WriteTables(tables, provenance, Path.Combine(outputRoot, "mm7-tables"), maps, containers, services, people, creatures),
            WriteWorld(tables, graph, provenance, Path.Combine(outputRoot, "mm7-world"), maps, collisions, entrances, services),
        ];
        WriteBundleFragment(outputRoot, provenance, packs);
        return new PackWriteResult(outputRoot, provenance, packs, CollisionSummary.Of(collisions), entrances, containers, services, people, creatures);
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
    private static IReadOnlyList<PlaceCollision> EmitCollisions(Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps)
    {
        List<PlaceCollision> places = [];
        foreach (MapStatsRecord map in tables.Maps.Maps)
        {
            if (!maps.TryGetValue(map.Id, out DecodedMap? decoded)) continue;
            places.Add(PlaceCollisionEmitter.Emit(map.Id, map.FileName, decoded));
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
                $"{report.FailureCount} of {report.MapCount} maps did not decode, so their arrival points cannot be imported: {failures}");
        }

        Dictionary<int, DecodedMap> maps = [];
        foreach (MapDecodeOutcome outcome in report.Decoded)
        {
            if (outcome.Decoded is not null) maps[outcome.Map.Id] = outcome.Decoded;
        }

        return maps;
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
        PlaceCreatureSummary creatures)
    {
        List<(string Path, string DocumentId, string Kind, int Entries)> documents =
        [
            ("places.json", "places", "place", WritePlaces(packDirectory, tables, maps, containers, services, people, creatures)),
            ("people.json", "people", PlacePeopleEmitter.PersonDefinitionKind, WritePeople(packDirectory, people)),
            ("services.json", "services", "service", WriteServices(packDirectory, services)),
            ("classes.json", "classes", "class", WriteClasses(packDirectory, tables)),
            ("skills.json", "skills", "skill", WriteSkills(packDirectory, tables)),
            ("spells.json", "spells", "spell", WriteSpells(packDirectory, tables)),
            ("monsters.json", "monsters", "monster", WriteMonsters(packDirectory, tables)),
            ("hostility.json", "hostility", "hostility", WriteHostility(packDirectory, tables)),
            ("items.json", "items", "item", WriteItems(packDirectory, tables)),
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

            // Every creature names a monster row, so the place's own document refers to the row it stands
            // for: a creature whose row the pack does not carry is then a load defect rather than a
            // creature nothing can say the hit points of.
            .. creatures.Placements.Select(placement => $"monster:{placement.MonsterId.ToString(CultureInfo.InvariantCulture)}").Distinct().Order(StringComparer.Ordinal),
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
                (IReadOnlyList<string>)(string.Equals(document.DocumentId, "places", StringComparison.Ordinal) ? peopleReferences : [])))]);
        return ("mm7-tables", documents.Count, documents.Sum(document => document.Entries));
    }

    private static (string PackId, int Documents, int Entries) WriteWorld(
        Mm7Tables tables,
        PlaceGraph graph,
        InstallProvenance provenance,
        string packDirectory,
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<PlaceCollision> collisions,
        PlaceEntranceSummary entrances,
        PlaceServiceSummary services)
    {
        int links = WritePlaceGraph(packDirectory, graph, tables, maps, services);
        int places = WritePlaceGeometry(packDirectory, collisions);
        int reachCount = WritePlaceEntrances(packDirectory, entrances);
        // Every place is referenced by the graph, and a fare's link leaves the place its counter stands in,
        // so the references state both.
        IReadOnlyList<string> graphReferences =
        [
            .. tables.Maps.Maps.Select(map => $"place:{map.Id.ToString(CultureInfo.InvariantCulture)}"),
            .. services.Fares.Select(fare => $"service:{fare.ServiceId.ToString(CultureInfo.InvariantCulture)}").Distinct(),
        ];
        IReadOnlyList<string> geometryReferences =
            [.. collisions.Where(place => place.Emitted).Select(place => $"place:{place.PlaceId.ToString(CultureInfo.InvariantCulture)}")];

        // An entrance refers to the transition it takes and to both places that transition joins, so a
        // reader that resolves every reference is told the entrance belongs to a road the world holds
        // rather than to one it does not.
        IReadOnlyList<string> entranceReferences =
        [
            .. entrances.Entrances.Select(entrance => entrance.LinkIndex).Distinct().Order()
                .Select(index => $"travel-link:{LinkId(index)}"),
            .. entrances.Entrances.SelectMany(entrance => new[] { entrance.FromPlace, entrance.ToPlace }).Distinct().Order()
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
            ]);
        return ("mm7-world", 3, links + places + reachCount);
    }

    /// <summary>
    /// Writes the places' collision artifacts, one entry per place, keyed by the place's own id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document's shape is the engine's, not this importer's, and the artifact is embedded exactly as
    /// the engine will parse it: a reader hands the bytes on unchanged rather than re-serializing a copy
    /// that could disagree with them. It is embedded compactly because the artifact's only reader is that
    /// parser, while the document around it stays indented for a person.
    /// </para>
    /// <para>
    /// Counts are written beside the artifact so a report can state what a place's collision is made of
    /// without parsing geometry, and so a refusal is visible as a place that has no entry at all.
    /// </para>
    /// </remarks>
    private static int WritePlaceGeometry(string packDirectory, IReadOnlyList<PlaceCollision> collisions)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PlaceCollision place in collisions)
        {
            if (place.Artifact is not { } artifact) continue;
            entries.Add((place.PlaceId.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("mapFile", place.FileName);
                writer.WriteString("kind", place.Kind == MapKind.Outdoor ? "region" : "interior");
                writer.WriteNumber("vertices", place.Vertices);
                writer.WriteNumber("triangles", place.Triangles);
                writer.WriteStartObject("geometryCounts");
                foreach (CollisionSource source in GeometrySources)
                {
                    CollisionSourceCounts counts = place.CountOf(source);
                    writer.WriteStartObject(SourceName(source));
                    writer.WriteNumber("faces", counts.Faces);
                    writer.WriteNumber("triangles", counts.Triangles);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                if (place.DroppedFaces > 0) writer.WriteNumber("droppedFaces", place.DroppedFaces);
                if (place.DroppedTriangles > 0) writer.WriteNumber("droppedDegenerateTriangles", place.DroppedTriangles);
                writer.WritePropertyName("artifact");
                writer.WriteRawValue(artifact);
            }));
        }

        return WriteDocument(packDirectory, "place-geometry.json", "place-geometry", "place-geometry", entries);
    }

    /// <summary>The name a geometry source's counts are written under.</summary>
    private static string SourceName(CollisionSource source) => source switch
    {
        CollisionSource.InteriorFace => "interiorFace",
        CollisionSource.Terrain => "terrain",
        CollisionSource.ModelFace => "modelFace",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "A geometry source this importer does not write was asked for its name."),
    };

    /// <summary>The entry id one travel link is written under, which an entrance names to take it.</summary>
    private static string LinkId(int index) => index.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes where a party can walk into each place's transitions: the reach, which transition it takes,
    /// and the map face the reach was derived from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reach is written as a position and a radius because that is what the product tests a pose
    /// against, and both the derivation and its provenance are written beside it: a reader must be able
    /// to see that the position is a face's own centroid and the radius the face's own extent, not a
    /// number this importer chose. The face's attributes are written raw and named, so what the donor
    /// does with the face — raising the event when the party steps on it or clicks it — stays readable
    /// without this writer interpreting Might and Magic's bit values into the product's terms.
    /// </para>
    /// <para>
    /// A place with no reach contributes no entry. That is a fact about the data rather than a gap in the
    /// pack: the write report names every link that has none and why, which is where a reader looks for
    /// what cannot be walked into.
    /// </para>
    /// </remarks>
    private static int WritePlaceEntrances(string packDirectory, PlaceEntranceSummary summary)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PlaceEntrancePlacement entrance in summary.Entrances)
        {
            entries.Add(($"{LinkId(entrance.LinkIndex)}.{entrance.SourceFaceIndex.ToString(CultureInfo.InvariantCulture)}", writer =>
            {
                writer.WriteString("link", LinkId(entrance.LinkIndex));
                writer.WriteNumber("fromPlace", entrance.FromPlace);
                writer.WriteNumber("toPlace", entrance.ToPlace);
                writer.WriteString("kind", entrance.Kind == PlaceEntranceKind.Walking ? "walking" : "entrance");
                writer.WriteNumber("x", entrance.X);
                writer.WriteNumber("y", entrance.Y);
                writer.WriteNumber("z", entrance.Z);
                writer.WriteNumber("radius", entrance.Radius);
                writer.WriteNumber("eventId", entrance.EventId);
                writer.WriteNumber("faceIndex", entrance.SourceFaceIndex);
                writer.WriteNumber("modelIndex", entrance.SourceModelIndex);
                WriteOptionalString(writer, "modelName", entrance.SourceModelName);
                writer.WriteNumber("attributes", entrance.Attributes);
                writer.WriteString("trigger", entrance.IsPressurePlate ? "pressurePlate" : entrance.IsClickable ? "clickable" : "other");
                writer.WriteString("positionSource", "event-face-centroid");
                writer.WriteString("radiusSource", "event-face-extent");
            }));
        }

        return WriteDocument(packDirectory, "place-entrances.json", "place-entrances", "place-entrance", entries);
    }

    private static int WritePlaces(
        string packDirectory,
        Mm7Tables tables,
        IReadOnlyDictionary<int, DecodedMap> maps,
        PlaceContainerSummary containers,
        PlaceServiceSummary services,
        PlacePeopleSummary people,
        PlaceCreatureSummary creatures)
    {
        // A place's counters and households are emitted into its placements, which is where the interaction
        // mechanism reads them from: a service placement is a target the party talks to, and nothing about
        // the population's shape differs between a counter and a chest.
        Dictionary<int, IReadOnlyList<PlaceServicePlacement>> countersByPlace = services.Placements
            .GroupBy(placement => placement.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceServicePlacement>)[.. group]);
        Dictionary<int, IReadOnlyList<PlaceChestPlacement>> containersByPlace = containers.Chests
            .GroupBy(chest => chest.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceChestPlacement>)[.. group]);
        Dictionary<int, IReadOnlyList<PlaceSpriteObjectPlacement>> objectsByPlace = containers.SpriteObjects
            .GroupBy(placement => placement.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceSpriteObjectPlacement>)[.. group]);

        // A building's people are written into the building's own placement, so a counter and the people
        // behind it are one thing the party walks up to rather than two targets standing in one spot: the
        // party faces the building, and who is there is what the placement holds.
        Dictionary<int, IReadOnlyList<string>> residentsByBuilding = people.Households
            .Where(household => household.PersonIds.Count > 0)
            .ToDictionary(household => household.BuildingId, household => household.PersonIds);
        Dictionary<int, IReadOnlyList<PlacePersonPlacement>> peopleByPlace = people.Placements
            .GroupBy(placement => placement.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlacePersonPlacement>)[.. group]);

        // A creature stands where its spawn record put it, so the creatures are grouped by the place their
        // map's records belong to and written into that place's own placements.
        Dictionary<int, IReadOnlyList<PlaceCreaturePlacement>> creaturesByPlace = creatures.Placements
            .GroupBy(placement => placement.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceCreaturePlacement>)[.. group]);
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (MapStatsRecord map in tables.Maps.Maps)
        {
            entries.Add((map.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("name", map.Name);
                writer.WriteString("kind", string.Equals(Path.GetExtension(map.FileName), ".odm", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetExtension(map.FileName), ".ddm", StringComparison.OrdinalIgnoreCase)
                        ? "region"
                        : "interior");
                writer.WriteString("mapFile", map.FileName);
                writer.WriteStartArray("monsters");
                foreach (string monster in map.Slots.Select(slot => slot.Monster))
                {
                    // An unused slot is the table's own zero, which names no beast; only named ones are
                    // written, so a place that spawns nothing carries an empty list rather than a monster
                    // called nothing.
                    if (monster.Length == 0 || monster == "0") continue;
                    writer.WriteStringValue(monster);
                }

                writer.WriteEndArray();
                writer.WriteNumber("respawnDays", map.RespawnDays);
                writer.WriteNumber("alertDays", map.AlertDays);
                writer.WriteNumber("treasureLevel", map.TreasureLevel);
                writer.WriteNumber("encounterPercent", map.EncounterPercent);
                WriteOptionalString(writer, "track", map.Track);
                WriteOptionalString(writer, "environment", map.Environment);
                if (maps.TryGetValue(map.Id, out DecodedMap? decoded))
                {
                    writer.WriteStartArray("entryPoints");
                    foreach (MapEntryPoint point in decoded.EntryPoints)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("id", point.Name);
                        writer.WriteNumber("x", point.Position.X);
                        writer.WriteNumber("y", point.Position.Y);
                        writer.WriteNumber("z", point.Position.Z);
                        writer.WriteNumber("yaw", point.YawAngle);
                        writer.WriteNumber("pitch", 0);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    WritePlacements(
                        writer,
                        decoded,
                        creaturesByPlace.GetValueOrDefault(map.Id, []),
                        containersByPlace.GetValueOrDefault(map.Id, []),
                        objectsByPlace.GetValueOrDefault(map.Id, []),
                        countersByPlace.GetValueOrDefault(map.Id, []),
                        peopleByPlace.GetValueOrDefault(map.Id, []),
                        residentsByBuilding);
                }
            }));
        }

        return WriteDocument(packDirectory, "places.json", "places", "place", entries);
    }

    /// <summary>
    /// Writes what stands in a place: the spawn points, decorations, doors, lights, containers and loose
    /// objects its decoded map holds, each with the content identity a rule resolves and the position it
    /// stands at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A placement keeps the source's own field name and array index instead of being renumbered into a
    /// shape of this importer's own, so a reader can follow a placement back to the decoded field that
    /// produced it. The kind-specific fields stay beside that identity, and a place's counts are written
    /// with them, so the pack states what it holds rather than leaving a checker to count it.
    /// </para>
    /// <para>
    /// Only door slots that hold a door become placements: an interior stores two hundred door records
    /// whether or not they mean anything, and importing the empty slots would invent two hundred doors
    /// per place. A door stores no position of its own — only the geometry it moves when it opens — so
    /// its position is the middle of the vertices it names, and the record says where that point came
    /// from rather than passing a derived value off as authored data.
    /// </para>
    /// <para>
    /// A container is the same case: a delta stores the runtime's whole container array, so only the
    /// records a map's own event faces open become placements, and their position is the mean of those
    /// faces' centres, marked as derived. What a container holds is written as the record stores it —
    /// including the negative identifiers that ask for a random item of a treasure level — so the pack
    /// carries the request and not an answer this importer would have had to invent. A sprite object does
    /// store its own position, and every one is written, because a map's own state is not trimmed here;
    /// what holds nothing is answered for by nobody.
    /// </para>
    /// <para>
    /// A counter is the one placement whose position the map does not state for it: the building table says
    /// which map a shop is on and the map says which faces raise the house's own event, so the placement
    /// carries the point the party stands at and the face, model, and texture it was read from, and the rule
    /// that picked it. A placement of kind <c>service</c> names its service by the building's id, which is
    /// what the ruleset resolves; a placement of kind <c>residence</c> is a household, carrying the table's
    /// own type and name so a party that reaches it meets what the row says lives there.
    /// </para>
    /// </remarks>
    private static void WritePlacements(
        Utf8JsonWriter writer,
        DecodedMap map,
        IReadOnlyList<PlaceCreaturePlacement> creatures,
        IReadOnlyList<PlaceChestPlacement> containers,
        IReadOnlyList<PlaceSpriteObjectPlacement> spriteObjects,
        IReadOnlyList<PlaceServicePlacement> counters,
        IReadOnlyList<PlacePersonPlacement> people,
        IReadOnlyDictionary<int, IReadOnlyList<string>> residentsByBuilding)
    {
        List<Placement> placements = [];
        foreach (MapSpawnPoint spawn in map.SpawnPoints)
        {
            placements.Add(new Placement("spawn", spawn.Index, "spawnPoints", spawn.Position, null, null, field =>
            {
                field.WriteNumber("radius", spawn.Radius);
                field.WriteNumber("type", spawn.Type);
                field.WriteNumber("treasureLevelOrMonsterIndex", spawn.TreasureLevelOrMonsterIndex);
                field.WriteNumber("attributes", spawn.Attributes);
                field.WriteNumber("group", spawn.Group);
            }));
        }

        // Every creature stands on the spawn record that asked for it, and carries the row it is under the
        // field name the ruleset reads a creature from. Everything else on the placement is the reading
        // that produced it — the encounter slot, the grade, the count, and the record's own group and
        // radius — so an operator can follow a creature back to the record and see why it is there.
        foreach (PlaceCreaturePlacement creature in creatures)
        {
            placements.Add(new Placement("monster", creature.SourceSpawnIndex, "spawnPoints", new PlacementPoint(creature.X, creature.Y, creature.Z), (int)creature.Yaw, "spawn-record", field =>
            {
                field.WriteNumber("monster", creature.MonsterId);
                field.WriteString("monsterName", creature.MonsterName);
                field.WriteNumber("spawn", creature.SourceSpawnIndex);
                field.WriteNumber("encounter", creature.EncounterIndex);
                field.WriteString("grade", creature.Grade);
                field.WriteNumber("quantity", creature.Quantity);
                field.WriteNumber("unit", creature.Unit);
                field.WriteNumber("group", creature.Group);
                field.WriteNumber("attributes", creature.Attributes);
                field.WriteNumber("radius", creature.Radius);
                field.WriteNumber("appearMin", creature.AppearMin);
                field.WriteNumber("appearMax", creature.AppearMax);
                field.WriteString("gradeSource", creature.GradeDrawn ? "difficulty-odds" : "spawn-slot");
                field.WriteString("countSource", creature.CountDrawn ? "slot-range-floor" : "spawn-slot");
            }, creature.PlacementId));
        }

        foreach (MapDecoration decoration in map.Decorations)
        {
            placements.Add(new Placement("decoration", decoration.Index, "decorations", decoration.Position, decoration.YawAngle, null, field =>
            {
                field.WriteString("name", decoration.Name);
                field.WriteNumber("descriptionId", decoration.DescriptionId);
                field.WriteNumber("flags", decoration.Flags);
                field.WriteNumber("cog", decoration.Cog);
                field.WriteNumber("eventId", decoration.EventId);
                field.WriteNumber("triggerRange", decoration.TriggerRange);
                field.WriteNumber("eventVarId", decoration.EventVarId);
            }));
        }

        foreach (MapDoor door in map.Doors)
        {
            if (!door.InUse) continue;
            placements.Add(new Placement("door", door.Index, "doors", VertexMiddle(map, door.VertexIds), null, "vertexIds", field =>
            {
                field.WriteNumber("doorId", door.DoorId);
                field.WriteNumber("state", door.State);
                field.WriteNumber("attributes", door.Attributes);
                field.WriteNumber("moveLength", door.MoveLength);
                field.WriteNumber("openSpeed", door.OpenSpeed);
                field.WriteNumber("closeSpeed", door.CloseSpeed);
            }));
        }

        foreach (MapLight light in map.Lights)
        {
            placements.Add(new Placement("light", light.Index, "lights", light.Position, null, null, field =>
            {
                field.WriteNumber("radius", light.Radius);
                field.WriteNumber("red", light.Red);
                field.WriteNumber("green", light.Green);
                field.WriteNumber("blue", light.Blue);
                field.WriteNumber("type", light.Type);
                field.WriteNumber("attributes", light.Attributes);
                field.WriteNumber("brightness", light.Brightness);
            }));
        }

        foreach (PlaceChestPlacement container in containers)
        {
            placements.Add(new Placement("container", container.ChestIndex, "chests", new PlacementPoint(container.X, container.Y, container.Z), null, "event-face-centroid", field =>
            {
                // The trap numbers are the place's own row of the per-map table, copied onto the container
                // that answers for them: a target is asked what it requires from its own placement, and a
                // ruleset never sees a place's fields.
                field.WriteNumber("flags", container.Chest.Flags);
                field.WriteNumber("containerType", container.Chest.TypeId);
                field.WriteNumber("faceCount", container.SourceFaceCount);
                field.WriteNumber("faceSpread", container.FaceSpread);
                field.WriteNumber("trapDifficulty", container.TrapDifficulty);
                field.WriteNumber("trapDamageDice", container.TrapDamageDice);
                // The place's own danger level travels with the container for the same reason its trap
                // numbers do: every random reference the container holds is remapped through it, and a
                // ruleset never sees a place's fields.
                field.WriteNumber("mapTreasureLevel", container.MapTreasureLevel);
                field.WriteString("contentsSource", "chest-record");
                field.WriteStartArray("contents");
                foreach (MapChestItem item in container.Chest.Items)
                {
                    field.WriteStartObject();
                    field.WriteNumber("slot", item.Slot);
                    field.WriteNumber("item", item.ItemId);
                    field.WriteEndObject();
                }

                field.WriteEndArray();
            }));
        }

        foreach (PlaceSpriteObjectPlacement held in spriteObjects)
        {
            placements.Add(new Placement("sprite", held.Object.Index, "spriteObjects", held.Object.Position, held.Object.YawAngle, null, field =>
            {
                field.WriteNumber("spriteId", held.Object.SpriteId);
                field.WriteNumber("objectDescId", held.Object.ObjectDescId);
                field.WriteNumber("sectorId", held.Object.SectorId);
                field.WriteNumber("attributes", held.Object.Attributes);
                field.WriteNumber("containingItem", held.Object.ContainingItemId);
                field.WriteString("contentsSource", "containing-item");
            }));
        }

        foreach (PlaceServicePlacement counter in counters)
        {
            placements.Add(new Placement(
                counter.PlacementKind,
                counter.BuildingId,
                "houseTable",
                new PlacementPoint(counter.X, counter.Y, counter.Z),
                null,
                counter.PositionSource,
                field =>
                {
                    // The building id is the identity the ruleset resolves, and it is written under the
                    // field name the ruleset reads; the row's own type travels beside it so a residence
                    // says what the table called it without a second document being joined in.
                    field.WriteNumber("houseId", counter.BuildingId);
                    field.WriteString("fixture", counter.Fixture);
                    field.WriteString("name", counter.Name);
                    if (counter.Proprietor.Length > 0) field.WriteString("proprietor", counter.Proprietor);
                    field.WriteNumber("sourceEvent", counter.EventId);
                    field.WriteNumber("sourceFace", counter.SourceFaceIndex);
                    field.WriteNumber("sourceModel", counter.SourceModelIndex);
                    field.WriteString("sourceTexture", counter.SourceTexture);
                    if (counter.SourceModelName.Length > 0) field.WriteString("sourceModelName", counter.SourceModelName);
                    field.WriteNumber("eventFaces", counter.FaceCount);
                    field.WriteString("heightSource", counter.HeightSource);
                    WritePeople(field, residentsByBuilding.GetValueOrDefault(counter.BuildingId));
                }));
        }

        // Somebody a map's own actor record places stands where the record puts them, which is a position
        // the map states rather than one this importer derived from a face.
        foreach (PlacePersonPlacement person in people)
        {
            placements.Add(new Placement(
                PlacePeopleEmitter.PersonPlacementKind,
                person.SourceActorIndex,
                "actors",
                new PlacementPoint(person.X, person.Y, person.Z),
                (int)person.Yaw,
                null,
                field =>
                {
                    field.WriteString("actorName", person.SourceActorName);

                    // A person a map's own actor record places carries the monster row that record names,
                    // which is the row this person fights as: a peasant, a guard, a named adept. A person
                    // the NPC table places in a building states none, and the ruleset reads the row this
                    // game gives somebody whose own record says nothing.
                    if (person.MonsterId != 0) field.WriteNumber("monster", person.MonsterId);
                    WritePeople(field, [person.PersonId]);
                }));
        }

        writer.WriteStartObject("placementCounts");
        foreach (string kind in PlacementKinds)
        {
            writer.WriteNumber(kind, placements.Count(placement => string.Equals(placement.Kind, kind, StringComparison.Ordinal)));
        }

        writer.WriteNumber("total", placements.Count);
        writer.WriteEndObject();

        writer.WriteStartArray("placements");
        foreach (Placement placement in placements)
        {
            writer.WriteStartObject();
            writer.WriteString("id", placement.Id);
            writer.WriteString("kind", placement.Kind);
            writer.WriteString("sourceField", placement.SourceField);
            writer.WriteNumber("sourceIndex", placement.SourceIndex);
            writer.WriteNumber("x", placement.Position.X);
            writer.WriteNumber("y", placement.Position.Y);
            writer.WriteNumber("z", placement.Position.Z);
            if (placement.Yaw is { } yaw) writer.WriteNumber("yaw", yaw);
            if (placement.PositionSource is { } positionSource) writer.WriteString("positionSource", positionSource);
            placement.Fields(writer);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>Writes the people a placement holds, leaving the field out when it holds nobody.</summary>
    private static void WritePeople(Utf8JsonWriter writer, IReadOnlyList<string>? people)
    {
        if (people is not { Count: > 0 }) return;
        writer.WriteStartArray(PlacePeopleEmitter.PlacementPeopleField);
        foreach (string person in people) writer.WriteStringValue(person);
        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes the people the game's own tables carry: who they are, what they say when met, and everything
    /// they can be asked about with the first answer the topic table names.
    /// </summary>
    /// <remarks>
    /// A person's topics are written inside the person because that is what they are: the topic table owns
    /// each row to one NPC, and a separate document would make every reader join the two back together. The
    /// count of a topic's texts travels beside the first one so a reader can tell a plain line from a line
    /// the original chooses among.
    /// </remarks>
    private static int WritePeople(string packDirectory, PlacePeopleSummary people)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PlacePerson person in people.People)
        {
            entries.Add((person.Id, writer =>
            {
                writer.WriteNumber("npcId", person.NpcId);
                writer.WriteString("name", person.Name);
                if (person.Portrait.Length > 0) writer.WriteString("portrait", person.Portrait);
                if (person.Greeting.Length > 0) writer.WriteString("greeting", person.Greeting);
                if (person.GreetingAgain.Length > 0) writer.WriteString("greetingAgain", person.GreetingAgain);
                if (person.House != 0) writer.WriteNumber("house", person.House);
                if (person.DialogueEvents > 0) writer.WriteNumber("dialogueEvents", person.DialogueEvents);
                if (person.CanJoin) writer.WriteBoolean("canJoin", true);
                writer.WriteNumber("sourceRow", person.SourceRow);
                writer.WriteStartArray("topics");
                foreach (PlacePersonTopic topic in person.Topics)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", topic.Id);
                    writer.WriteString("label", topic.Label);
                    writer.WriteString("text", topic.Text);
                    writer.WriteNumber("textCount", topic.TextCount);
                    if (topic.Requires != 0) writer.WriteNumber("requires", topic.Requires);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }));
        }

        return WriteDocument(packDirectory, "people.json", "people", PlacePeopleEmitter.PersonDefinitionKind, entries);
    }

    /// <summary>
    /// The middle of the vertices a door moves, which is where the door stands.
    /// </summary>
    /// <remarks>
    /// A door that is in use always names at least one vertex — that is exactly what makes it in use —
    /// so there is always a middle to report. The division is integer division on purpose: the pack
    /// must not depend on a rounding mode to stay reproducible.
    /// </remarks>
    private static MapPoint VertexMiddle(DecodedMap map, IReadOnlyList<int> vertexIds)
    {
        long x = 0;
        long y = 0;
        long z = 0;
        foreach (int vertexId in vertexIds)
        {
            MapPoint vertex = map.Vertices[vertexId];
            x += vertex.X;
            y += vertex.Y;
            z += vertex.Z;
        }

        return new MapPoint((int)(x / vertexIds.Count), (int)(y / vertexIds.Count), (int)(z / vertexIds.Count));
    }

    /// <summary>One thing placed in a place, before it is written.</summary>
    /// <param name="Kind">The kind of thing placed.</param>
    /// <param name="SourceIndex">The index within the source field it was read from.</param>
    /// <param name="SourceField">The decoded map field it was read from.</param>
    /// <param name="Position">Where it stands.</param>
    /// <param name="Yaw">Its facing, or null when the source states none.</param>
    /// <param name="PositionSource">Where a derived position came from, or null when the source stores one.</param>
    /// <param name="Fields">The kind-specific fields to write beside the identity.</param>
    /// <param name="ExplicitId">
    /// The placement's identity, when one source record puts more than one placement in a place and
    /// <c>kind-index</c> would name them all the same thing. Null for the ordinary case, where the kind and
    /// the source index are the identity.
    /// </param>
    private sealed record Placement(
        string Kind,
        int SourceIndex,
        string SourceField,
        PlacementPoint Position,
        int? Yaw,
        string? PositionSource,
        Action<Utf8JsonWriter> Fields,
        string? ExplicitId = null)
    {
        /// <summary>The placement's identity within its place, which is what a rule resolves.</summary>
        public string Id { get; } = ExplicitId ?? $"{Kind}-{SourceIndex}";
    }

    /// <summary>
    /// Where a placement stands.
    /// </summary>
    /// <remarks>
    /// A position written from a decoded record is a whole number of place units and one derived from
    /// geometry is not, so placements carry real numbers: rounding a face's centre to a unit would make the
    /// pack's position disagree with the face it came from, and the difference would grow with the number
    /// of faces a container's position is averaged over.
    /// </remarks>
    private readonly record struct PlacementPoint(double X, double Y, double Z)
    {
        public static implicit operator PlacementPoint(MapPoint point) => new(point.X, point.Y, point.Z);
    }

    private static int WriteClasses(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (ClassRecord rank in tables.Classes.Ranks)
        {
            entries.Add((rank.Name, writer =>
            {
                writer.WriteString("description", rank.Description);
                writer.WriteString("baseClass", rank.BaseClass);
                writer.WriteNumber("rank", rank.Rank);
            }));
        }

        return WriteDocument(packDirectory, "classes.json", "classes", "class", entries);
    }

    private static int WriteSkills(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (SkillRecord skill in tables.Skills.Skills)
        {
            entries.Add((skill.Name, writer =>
            {
                writer.WriteString("description", skill.Description);
                writer.WriteString("normal", skill.Normal);
                writer.WriteString("expert", skill.Expert);
                writer.WriteString("master", skill.Master);
                writer.WriteString("grandMaster", skill.GrandMaster);
            }));
        }

        return WriteDocument(packDirectory, "skills.json", "skills", "skill", entries);
    }

    private static int WriteSpells(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (SpellRecord spell in tables.Spells.Spells)
        {
            entries.Add((spell.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("school", spell.School);
                writer.WriteNumber("level", spell.Level);
                writer.WriteString("name", spell.Name);
                writer.WriteString("resist", spell.Resist);
                writer.WriteString("shortName", spell.ShortName);
                writer.WriteString("description", spell.Description);
                writer.WriteString("normal", spell.Normal);
                writer.WriteString("expert", spell.Expert);
                writer.WriteString("master", spell.Master);
                writer.WriteString("grandMaster", spell.GrandMaster);
            }));
        }

        return WriteDocument(packDirectory, "spells.json", "spells", "spell", entries);
    }

    private static int WriteMonsters(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (MonsterRecord monster in tables.Monsters.Monsters)
        {
            entries.Add((monster.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("name", monster.Name);
                writer.WriteNumber("level", monster.Level);
                writer.WriteNumber("hitPoints", monster.HitPoints);
                writer.WriteNumber("armorClass", monster.ArmorClass);
                writer.WriteNumber("experience", monster.Experience);
                writer.WriteNumber("hostility", monster.Hostility);
                writer.WriteNumber("speed", monster.Speed);
                writer.WriteNumber("recovery", monster.Recovery);
                writer.WriteString("aiType", monster.AiType);
                writer.WriteString("movement", monster.Movement);
                writer.WriteString("fly", monster.Fly);
                WriteOptionalString(writer, "treasure", monster.Treasure);

                // The cell as it stands is kept beside what it states, so the numbers a fight draws from can
                // be checked against the bytes they came from; a creature that drops nothing states a cell of
                // zero and a roll of nothing rather than no roll at all.
                writer.WriteStartObject("treasureRoll");
                writer.WriteNumber("chance", monster.TreasureRoll.Chance);
                writer.WriteNumber("goldRolls", monster.TreasureRoll.GoldRolls);
                writer.WriteNumber("goldSides", monster.TreasureRoll.GoldSides);
                writer.WriteNumber("level", monster.TreasureRoll.Level);
                if (monster.TreasureRoll.Kind.Length > 0) writer.WriteString("kind", monster.TreasureRoll.Kind);
                if (monster.TreasureRoll.Skill.Length > 0) writer.WriteString("skill", monster.TreasureRoll.Skill);
                writer.WriteEndObject();
                WriteColumns(writer, monster.Fields);
            }));
        }

        return WriteDocument(packDirectory, "monsters.json", "monsters", "monster", entries);
    }

    /// <summary>
    /// Writes what every kind of monster thinks of every other kind and of the party, as the shipped
    /// matrix states it.
    /// </summary>
    /// <remarks>
    /// One entry per kind, carrying the bands it holds toward the kinds the header names, in the header's
    /// own column order. The party's own row and column are written with the rest: the party's row is what
    /// a creature fighting for the party reads its targets from, and the party's column is what a kind
    /// thinks of the party, so neither is an empty cell a reader would have to special-case.
    /// </remarks>
    private static int WriteHostility(string packDirectory, Mm7Tables tables)
    {
        HostilityTable matrix = tables.Hostility;
        int row = 0;
        // The header is an entry of its own so the pack names which kind every column index is: a band is
        // written against the column it was read from, and a reader that wanted to print one as a name
        // rather than as a number has the data's own order to print it from.
        List<(string Id, Action<Utf8JsonWriter> Write)> entries =
        [
            ("kinds", Kinds),
        ];

        void Kinds(Utf8JsonWriter writer)
        {
            writer.WriteStartArray("columns");
            foreach (string column in matrix.Columns) writer.WriteStringValue(column);
            writer.WriteEndArray();
        }

        foreach (HostilityRow feelings in matrix.Rows)
        {
            // The kind is the row's own position, which is how the donor reads the matrix: a row's number is
            // the monster type it is about, and the header is only names. Matching by name would leave every
            // row whose spelling the header does not repeat — twenty-four of them, in the shipped file —
            // without a kind at all.
            int kind = row++;
            entries.Add((feelings.Kind, writer =>
            {
                writer.WriteNumber("kind", kind);

                // Only the bands the data states are written, against the column index they were read from,
                // and a band the file leaves out is friendly: the donor's own reader fills every relation
                // with friendly before it reads a cell
                // (OpenEnroth src/Engine/Tables/HostilityTable.cpp:17-18), so an omitted cell and a stated
                // zero are one fact in the data's own terms.
                writer.WriteStartObject("hostility");
                for (int column = 0; column < matrix.Columns.Count; column++)
                {
                    if (feelings.BandAt(column) is not { } band || band == 0) continue;
                    writer.WriteNumber(column.ToString(CultureInfo.InvariantCulture), band);
                }

                writer.WriteEndObject();
            }));
        }

        return WriteDocument(packDirectory, "hostility.json", "hostility", "hostility", entries);
    }

    private static int WriteItems(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (ItemRecord item in tables.Items.Items)
        {
            entries.Add((item.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("name", item.Name);
                writer.WriteString("unidentifiedName", item.UnidentifiedName);
                writer.WriteNumber("value", item.Value);
                writer.WriteString("equipStat", item.EquipStat);
                writer.WriteString("skillGroup", item.SkillGroup);

                // The two tags the treasure rules compare, read from the table's own words here so nothing
                // downstream has to know how the shipped file spells an equipment column.
                writer.WriteString("type", ItemVocabulary.KindOf(item.EquipStat));
                writer.WriteString("skill", ItemVocabulary.SkillOf(item.SkillGroup));

                // What the item weighs at each treasure level, which is what a random item of that level
                // draws from. An item the shipped table does not weigh carries no weights at all rather than
                // six zeroes, so "never drawn" and "not stated" stay different facts.
                if (tables.RandomItems.Rows.FirstOrDefault(row => row.Id == item.Id) is { Id: > 0 } weighed)
                {
                    writer.WriteStartArray("lootWeights");
                    for (int level = 1; level <= RandomItemsTable.Levels; level++) writer.WriteNumberValue(weighed.ChanceAt(level));
                    writer.WriteEndArray();
                }

                writer.WriteString("damageDice", item.DamageDice);
                writer.WriteString("damageModifier", item.DamageModifier);

                // A book's, a scroll's, and a wand's row all state the spell they carry in the item table's
                // own damage column, as the letter S and the spell's global id: item 400 is the book of
                // "Torch Light" and carries S1, item 300 the scroll of the same spell, item 135 the Wand of
                // Fire with S2, and item 498 the book of "Souldrinker" with S99 (the shipped table's own
                // spelling, which the donor's three lookups turn into a spell id by position —
                // OpenEnroth src/Engine/Objects/ItemEnumFunctions.cpp:282-292, spellForSpellbook,
                // spellForScroll, and spellForWand, each over a table generated from the item table). That
                // spelling is a source-format quirk, so the join is written out here as a field of its own
                // rather than left for a reader to parse, and only for a row the spell table declares.
                if (ItemSpell(item, tables.Spells) is { } taught)
                {
                    writer.WriteString("spell", taught.ToString(CultureInfo.InvariantCulture));
                }

                writer.WriteString("material", item.Material);
                writer.WriteString("picture", item.Picture);
                WriteOptionalNumber(writer, "spriteIndex", item.SpriteIndex == 0 ? null : item.SpriteIndex);
                WriteColumns(writer, item.Fields);
            }));
        }

        return WriteDocument(packDirectory, "items.json", "items", "item", entries);
    }

    /// <summary>The spell an item's own reference column names, or null when the row carries none.</summary>
    /// <remarks>
    /// <para>
    /// The reference is the shipped table's spelling — the letter <c>S</c> and the spell's id — and it is read
    /// only for a row that is one of the three things the donor reads a spell for: a book, a spell scroll, or
    /// a wand. The three share the column and the spelling, which is why one join serves all of them and why
    /// the kind each is read as travels separately in the item's own <c>type</c> tag.
    /// </para>
    /// <para>
    /// A row whose reference names no spell the spell table declares is left without the field rather than
    /// given a number nothing answers.
    /// </para>
    /// </remarks>
    private static int? ItemSpell(ItemRecord item, SpellTable spells)
    {
        // The equipment words the shipped table uses for the three kinds that carry a spell, read through the
        // same vocabulary the pack's own kind tag comes from so the two cannot drift.
        string kind = ItemVocabulary.KindOf(item.EquipStat);
        if (!string.Equals(kind, ItemVocabulary.Book, StringComparison.Ordinal) &&
            !string.Equals(kind, ItemVocabulary.SpellScroll, StringComparison.Ordinal) &&
            !string.Equals(kind, ItemVocabulary.Wand, StringComparison.Ordinal))
        {
            return null;
        }

        string reference = item.DamageDice.Trim();
        if (reference.Length < 2 || !reference.StartsWith("S", StringComparison.OrdinalIgnoreCase)) return null;
        return int.TryParse(reference[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int id) &&
            spells.Spells.Any(spell => spell.Id == id)
            ? id
            : null;
    }

    private static int WriteQuests(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (QuestRecord quest in tables.Quests.Quests)
        {
            entries.Add((quest.Bit.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("text", quest.Text);
                WriteOptionalString(writer, "notes", quest.Notes);
                WriteOptionalString(writer, "owner", quest.Owner);
            }));
        }

        return WriteDocument(packDirectory, "quests.json", "quests", "quest", entries);
    }

    private static int WritePlaceGraph(
        string packDirectory,
        PlaceGraph graph,
        Mm7Tables tables,
        IReadOnlyDictionary<int, DecodedMap> maps,
        PlaceServiceSummary services)
    {
        Dictionary<int, string> names = tables.Maps.Maps.ToDictionary(map => map.Id, map => map.Name);
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        int index = 0;
        foreach (PlaceLink link in graph.Links)
        {
            entries.Add((index.ToString(CultureInfo.InvariantCulture), writer =>
            {
                WriteOptionalNumber(writer, "fromPlace", link.SourceMapId);
                if (link.SourceMapId is int source) WriteOptionalString(writer, "fromName", names.GetValueOrDefault(source));
                int destination = link.DestinationMapId!.Value;
                writer.WriteNumber("toPlace", destination);
                WriteOptionalString(writer, "toName", names.GetValueOrDefault(destination));

                // A move with no position means "arrive at the destination's own start point". That is a
                // named arrival only where the destination actually has such a point: five shipped
                // interiors have none, and for those the instruction's zeroed position is what the game
                // has, so the pack carries the position and records the point it wanted.
                bool namesTheStart = link is { X: 0, Y: 0, Z: 0 };
                string startPoint = "Party Start";
                bool destinationHasStart = maps.TryGetValue(destination, out DecodedMap? decoded)
                    && decoded.EntryPoints.Any(point => string.Equals(point.Name, startPoint, StringComparison.OrdinalIgnoreCase));
                if (namesTheStart && destinationHasStart)
                {
                    writer.WriteString("entryPoint", startPoint);
                }
                else
                {
                    writer.WriteNumber("x", link.X);
                    writer.WriteNumber("y", link.Y);
                    writer.WriteNumber("z", link.Z);
                    writer.WriteNumber("yaw", link.Yaw);
                    writer.WriteNumber("pitch", link.Pitch);
                    if (namesTheStart) writer.WriteString("entryPointMissing", startPoint);
                }

                writer.WriteNumber("houseId", link.HouseId);
                writer.WriteNumber("exitPicture", link.ExitPicture);
                writer.WriteNumber("eventId", link.EventId);
                writer.WriteNumber("step", link.Step);
                writer.WriteString("program", link.SourceEvtName);
            }));
            index++;
        }

        // A passage a stable or a dock sells is a transition like any other: the party that holds the fare
        // takes it, and the world prices it by the same path every other crossing goes through. Its link is
        // named for the counter that sells it rather than numbered with the map's own links, so a reader can
        // tell a bought journey from one the map's events issue, and it arrives at the destination's own
        // arrival point rather than at a coordinate this importer chose.
        foreach (PlaceFare fare in services.Fares)
        {
            entries.Add((fare.LinkId, writer =>
            {
                writer.WriteNumber("fromPlace", fare.FromPlace);
                WriteOptionalString(writer, "fromName", names.GetValueOrDefault(fare.FromPlace));
                writer.WriteNumber("toPlace", fare.ToPlace);
                WriteOptionalString(writer, "toName", names.GetValueOrDefault(fare.ToPlace));
                if (fare.ArrivalPoint.Length > 0)
                {
                    writer.WriteString("entryPoint", fare.ArrivalPoint);
                }
                else
                {
                    writer.WriteNumber("x", fare.X);
                    writer.WriteNumber("y", fare.Y);
                    writer.WriteNumber("z", fare.Z);
                    writer.WriteNumber("yaw", fare.Yaw);
                    writer.WriteNumber("pitch", fare.Pitch);
                }

                writer.WriteNumber("houseId", fare.ServiceId);
                writer.WriteNumber("exitPicture", 0);
                writer.WriteNumber("eventId", 0);
                writer.WriteNumber("step", 0);
                writer.WriteString("program", "2DEvents.txt");
                writer.WriteBoolean("fare", true);
                writer.WriteNumber("days", fare.Days);
            }));
        }

        return WriteDocument(packDirectory, "place-graph.json", "place-graph", "travel-link", entries);
    }

    /// <summary>
    /// Writes the counters the building table describes, keyed by the building's own id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every column written here is the table's, named after the column it came from, and the fields a row
    /// leaves empty are left out rather than filled with a number nobody stated: a temple has no stock
    /// interval and a house has no multiplier, and a reader that finds no field can say so.
    /// </para>
    /// <para>
    /// <b>What the definitions do not carry.</b> Which operations a counter offers, what its shelves hold,
    /// what it teaches, and what it charges are this game's answers about a kind, so the ruleset supplies
    /// them; the pack carries the operator's data and the provenance that says which row it came from.
    /// </para>
    /// </remarks>
    private static int WriteServices(string packDirectory, PlaceServiceSummary services)
    {
        Dictionary<int, IReadOnlyList<PlaceFare>> faresByService = services.Fares
            .GroupBy(fare => fare.ServiceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceFare>)[.. group]);
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PlaceServiceDefinition service in services.Services)
        {
            entries.Add((service.BuildingId.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("kind", service.Kind);
                writer.WriteString("name", service.Name);
                if (service.Proprietor.Length > 0) writer.WriteString("proprietor", service.Proprietor);
                if (service.Title.Length > 0) writer.WriteString("title", service.Title);
                writer.WriteNumber("mapId", service.MapId);
                writer.WriteString("place", service.PlaceId);
                WriteOptionalNumber(writer, "typeSequence", service.TypeSequence);
                WriteOptionalNumber(writer, "openHour", service.OpenHour);
                WriteOptionalNumber(writer, "closedHour", service.ClosedHour);
                WriteOptionalNumber(writer, "priceMultiplier", service.PriceMultiplier);
                WriteOptionalNumber(writer, "skillPriceMultiplier", service.SkillPriceMultiplier);
                WriteOptionalNumber(writer, "stockIntervalDays", service.StockIntervalDays);
                if (service.TrainingCap is int cap) writer.WriteNumber("trainingCap", cap);
                if (service.TrainingCapText.Length > 0) writer.WriteString("trainingCapText", service.TrainingCapText);
                if (faresByService.TryGetValue(service.BuildingId, out IReadOnlyList<PlaceFare>? fares))
                {
                    // The passages this counter sells, written with the very link that takes them: the
                    // counter's offer and the world's transition are one emission rather than two that
                    // could disagree about where a fare goes.
                    writer.WriteStartArray("fares");
                    foreach (PlaceFare fare in fares)
                    {
                        writer.WriteStartObject();
                        writer.WriteNumber("toPlace", fare.ToPlace);
                        writer.WriteString("place", fare.ToPlace.ToString(CultureInfo.InvariantCulture));
                        writer.WriteString("name", fare.DestinationName);
                        writer.WriteNumber("days", fare.Days);
                        writer.WriteString("link", fare.LinkId);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteString("source", "2DEvents.txt");
                writer.WriteNumber("sourceRow", service.SourceRow);
            }));
        }

        return WriteDocument(packDirectory, "services.json", "services", "service", entries);
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
        writer.WriteString("producer", "mm7import");
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

    /// <summary>
    /// Writes the row's remaining columns under a namespaced key, so a later stone can read a column
    /// this importer does not type yet without re-importing the game.
    /// </summary>
    private static void WriteColumns(Utf8JsonWriter writer, IReadOnlyList<string> fields)
    {
        writer.WriteStartArray("columns");
        foreach (string field in fields) writer.WriteStringValue(field);
        writer.WriteEndArray();
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
