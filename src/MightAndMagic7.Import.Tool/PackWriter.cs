using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MightAndMagic7.Import.Collision;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Tool;

/// <summary>What one import wrote.</summary>
/// <param name="OutputRoot">The directory the packs were written to.</param>
/// <param name="Provenance">Where the content came from.</param>
/// <param name="Packs">The packs written, with their entry counts.</param>
/// <param name="Geometry">What every place's collision emission produced.</param>
internal sealed record PackWriteResult(string OutputRoot, InstallProvenance Provenance, IReadOnlyList<(string PackId, int Documents, int Entries)> Packs, CollisionSummary Geometry)
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
    private static readonly string[] PlacementKinds = ["spawn", "decoration", "door", "light"];

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

        Directory.CreateDirectory(outputRoot);
        List<(string, int, int)> packs =
        [
            WriteTables(tables, provenance, Path.Combine(outputRoot, "mm7-tables"), maps),
            WriteWorld(tables, graph, provenance, Path.Combine(outputRoot, "mm7-world"), maps, collisions),
        ];
        WriteBundleFragment(outputRoot, provenance, packs);
        return new PackWriteResult(outputRoot, provenance, packs, CollisionSummary.Of(collisions));
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
        IReadOnlyDictionary<int, DecodedMap> maps)
    {
        List<(string Path, string DocumentId, string Kind, int Entries)> documents =
        [
            ("places.json", "places", "place", WritePlaces(packDirectory, tables, maps)),
            ("classes.json", "classes", "class", WriteClasses(packDirectory, tables)),
            ("skills.json", "skills", "skill", WriteSkills(packDirectory, tables)),
            ("spells.json", "spells", "spell", WriteSpells(packDirectory, tables)),
            ("monsters.json", "monsters", "monster", WriteMonsters(packDirectory, tables)),
            ("items.json", "items", "item", WriteItems(packDirectory, tables)),
            ("quests.json", "quests", "quest", WriteQuests(packDirectory, tables)),
        ];
        WriteManifest(
            packDirectory,
            "mm7-tables",
            "definitions",
            provenance,
            documents.Select(document => (document.Path, document.DocumentId, document.Kind, (IReadOnlyList<string>)[])).ToArray());
        return ("mm7-tables", documents.Count, documents.Sum(document => document.Entries));
    }

    private static (string PackId, int Documents, int Entries) WriteWorld(
        Mm7Tables tables,
        PlaceGraph graph,
        InstallProvenance provenance,
        string packDirectory,
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<PlaceCollision> collisions)
    {
        int links = WritePlaceGraph(packDirectory, graph, tables, maps);
        int places = WritePlaceGeometry(packDirectory, collisions);
        IReadOnlyList<string> references = [.. tables.Maps.Maps.Select(map => $"place:{map.Id.ToString(CultureInfo.InvariantCulture)}")];
        IReadOnlyList<string> geometryReferences =
            [.. collisions.Where(place => place.Emitted).Select(place => $"place:{place.PlaceId.ToString(CultureInfo.InvariantCulture)}")];
        WriteManifest(
            packDirectory,
            "mm7-world",
            "world",
            provenance,
            [
                ("place-graph.json", "place-graph", "travel-link", references),

                // Only the places that produced an artifact are referenced: a reference to a place whose
                // geometry was refused would promise collision the pack does not carry.
                ("place-geometry.json", "place-geometry", "place-geometry", geometryReferences),
            ]);
        return ("mm7-world", 2, links + places);
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

    private static int WritePlaces(string packDirectory, Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps)
    {
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
                    WritePlacements(writer, decoded);
                }
            }));
        }

        return WriteDocument(packDirectory, "places.json", "places", "place", entries);
    }

    /// <summary>
    /// Writes what stands in a place: the spawn points, decorations, doors and lights its decoded map
    /// holds, each with the content identity a rule resolves and the position it stands at.
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
    /// </remarks>
    private static void WritePlacements(Utf8JsonWriter writer, DecodedMap map)
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
    private sealed record Placement(
        string Kind,
        int SourceIndex,
        string SourceField,
        MapPoint Position,
        int? Yaw,
        string? PositionSource,
        Action<Utf8JsonWriter> Fields)
    {
        /// <summary>The placement's identity within its place, which is what a rule resolves.</summary>
        public string Id { get; } = $"{Kind}-{SourceIndex}";
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
                WriteOptionalString(writer, "treasure", monster.Treasure);
                WriteColumns(writer, monster.Fields);
            }));
        }

        return WriteDocument(packDirectory, "monsters.json", "monsters", "monster", entries);
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
                writer.WriteString("damageDice", item.DamageDice);
                writer.WriteString("damageModifier", item.DamageModifier);
                writer.WriteString("material", item.Material);
                writer.WriteString("picture", item.Picture);
                WriteOptionalNumber(writer, "spriteIndex", item.SpriteIndex == 0 ? null : item.SpriteIndex);
                WriteColumns(writer, item.Fields);
            }));
        }

        return WriteDocument(packDirectory, "items.json", "items", "item", entries);
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
        IReadOnlyDictionary<int, DecodedMap> maps)
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

        return WriteDocument(packDirectory, "place-graph.json", "place-graph", "travel-link", entries);
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
