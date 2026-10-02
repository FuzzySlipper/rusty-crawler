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

/// <summary>The world pack's documents: places, their maps, collision, entrances, placements, people, and the place graph.</summary>
internal static partial class PackWriter
{
    /// <summary>
    /// Writes each place's own automap raster, keyed by the place's own id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What is written is the map's own data read into the product's grid: a region's squares are its own
    /// terrain cells at the payload's own 512-unit pitch, one height band each, and an interior's are
    /// <see cref="PlaceMaps.InteriorCellSize"/> units each, saying whether one of the level's own minimap
    /// outlines passes through. The one number that is ours is that cell size, and the format spec records it
    /// beside the rest of the layout.
    /// </para>
    /// <para>
    /// The cells are written as two hexadecimal digits each rather than as an array of numbers, because a
    /// fully mapped region is 16,384 squares and a person reading the document wants its shape, not a page of
    /// zeroes. Nothing else about the document is compacted: the grid, its origin, and the file the map came
    /// from stay readable, because they are what makes the numbers below them mean something.
    /// </para>
    /// </remarks>
    private static PlaceMapSummary WritePlaceMaps(string packDirectory, IReadOnlyDictionary<int, DecodedMap> maps)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        int cells = 0;
        foreach ((int placeId, DecodedMap map) in maps.OrderBy(pair => pair.Key))
        {
            PlaceMapRaster raster = PlaceMaps.Of(placeId, map);
            cells += raster.Kinds.Length;
            entries.Add((placeId.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("mapFile", raster.FileName);
                writer.WriteString("kind", raster.Kind == MapKind.Outdoor ? "region" : "interior");
                writer.WriteNumber("cellSize", raster.CellSize);
                writer.WriteStartArray("origin");
                writer.WriteNumberValue(raster.OriginX);
                writer.WriteNumberValue(raster.OriginY);
                writer.WriteEndArray();
                writer.WriteNumber("columns", raster.Columns);
                writer.WriteNumber("rows", raster.Rows);
                writer.WritePropertyName("kinds");
                writer.WriteRawValue(Hex(raster.Kinds), skipInputValidation: true);
            }));
        }

        int places = WriteDocument(packDirectory, "place-map.json", "place-map", "place-map", entries);
        return new PlaceMapSummary(places, cells);
    }

    /// <summary>One square per two hexadecimal digits, in the document's own row-major order.</summary>
    private static string Hex(byte[] kinds)
    {
        StringBuilder text = new((kinds.Length * 2) + 2);
        text.Append('"');
        foreach (byte kind in kinds) text.Append(kind.ToString("x2", CultureInfo.InvariantCulture));
        text.Append('"');
        return text.ToString();
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
                if (place.WaterSquares > 0) writer.WriteNumber("waterSquares", place.WaterSquares);
                if (place.FluidFaces > 0) writer.WriteNumber("fluidFaces", place.FluidFaces);
                writer.WritePropertyName("artifact");
                writer.WriteRawValue(artifact);
                WriteSurfaces(writer, place.Surfaces);
            }));
        }

        return WriteDocument(packDirectory, "place-geometry.json", "place-geometry", "place-geometry", entries);
    }

    /// <summary>
    /// Writes the place's named ground beside its artifact: each surface's name and its triangles, over corners of its
    /// own in the engine's axes.
    /// </summary>
    /// <remarks>
    /// A surface is written whole or not at all, and a place with none writes an empty list rather than leaving the
    /// property out, so a reader can tell "this place has no water" from a pack written before water was marked. Each
    /// mesh is written compactly, one corner and one triangle per array, because its reader is the product's own
    /// classifier and a region's water is thousands of squares.
    /// </remarks>
    private static void WriteSurfaces(Utf8JsonWriter writer, IReadOnlyList<PlaceSurface> surfaces)
    {
        writer.WriteStartArray("surfaces");
        foreach (PlaceSurface surface in surfaces)
        {
            writer.WriteStartObject();
            writer.WriteString("surface", surface.Surface);
            writer.WriteStartArray("positions");
            for (int index = 0; index < surface.Mesh.VertexCount; index++)
            {
                (double x, double y, double z) = surface.Mesh.Position(index);
                writer.WriteRawValue(string.Create(CultureInfo.InvariantCulture, $"[{x},{y},{z}]"));
            }

            writer.WriteEndArray();
            writer.WriteStartArray("triangles");
            for (int index = 0; index < surface.Mesh.TriangleCount; index++)
            {
                IReadOnlyList<int> triangles = surface.Mesh.Triangles;
                writer.WriteRawValue(string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{triangles[index * 3]},{triangles[(index * 3) + 1]},{triangles[(index * 3) + 2]}]"));
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
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
            entries.Add((string.Create(CultureInfo.InvariantCulture, $"{entrance.FromPlace}.{entrance.EventId}.{entrance.SourceFaceIndex}"), writer =>
            {
                writer.WriteNumber("fromPlace", entrance.FromPlace);
                writer.WriteString("raises", entrance.Raises);
                writer.WriteString("raisesKind", PlaceFloorTrigger.PlacementKind);
                writer.WriteNumber("eventId", entrance.EventId);
                writer.WriteStartArray("links");
                foreach (int link in entrance.Links) writer.WriteStringValue(LinkId(link));
                writer.WriteEndArray();
                writer.WriteNumber("x", entrance.X);
                writer.WriteNumber("y", entrance.Y);
                writer.WriteNumber("z", entrance.Z);
                writer.WriteNumber("radius", entrance.Radius);
                writer.WriteNumber("faceIndex", entrance.SourceFaceIndex);
                writer.WriteNumber("modelIndex", entrance.SourceModelIndex);
                WriteOptionalString(writer, "modelName", entrance.SourceModelName);
                writer.WriteNumber("attributes", entrance.Attributes);
                writer.WriteString("trigger", "pressurePlate");
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
        PlaceEncounterSummary encounters,
        PlaceCreatureSummary creatures,
        PlaceFixtureSummary fixtures)
    {
        // A place's fixtures stand where the faces raising their event are, so they are grouped by place and
        // written into that place's own placements beside its containers.
        Dictionary<int, IReadOnlyList<PlaceFixturePlacement>> fixturesByPlace = fixtures.Fixtures
            .GroupBy(fixture => fixture.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceFixturePlacement>)[.. group]);
        Dictionary<int, IReadOnlyList<PlaceFloorTrigger>> triggersByPlace = fixtures.Triggers
            .GroupBy(trigger => trigger.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceFloorTrigger>)[.. group]);

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

        // An encounter stands where its spawn record is, so the encounters are grouped by the place their
        // map's records belong to and written into that place's own placements.
        Dictionary<int, IReadOnlyList<PlaceEncounterPlacement>> encountersByPlace = encounters.Placements
            .GroupBy(placement => placement.PlaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlaceEncounterPlacement>)[.. group]);

        // A level's own creatures stand where their actor records put them, grouped by place like the people
        // the same records are.
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
                // The base fine a crime here starts from, which this game's theft and crime rules read off the
                // place rather than off a table the runtime never sees.
                writer.WriteNumber("stealFine", map.StealFine);
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
                        encountersByPlace.GetValueOrDefault(map.Id, []),
                        creaturesByPlace.GetValueOrDefault(map.Id, []),
                        containersByPlace.GetValueOrDefault(map.Id, []),
                        objectsByPlace.GetValueOrDefault(map.Id, []),
                        countersByPlace.GetValueOrDefault(map.Id, []),
                        peopleByPlace.GetValueOrDefault(map.Id, []),
                        fixturesByPlace.GetValueOrDefault(map.Id, []),
                        triggersByPlace.GetValueOrDefault(map.Id, []),
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
        IReadOnlyList<PlaceEncounterPlacement> encounters,
        IReadOnlyList<PlaceCreaturePlacement> creatures,
        IReadOnlyList<PlaceChestPlacement> containers,
        IReadOnlyList<PlaceSpriteObjectPlacement> spriteObjects,
        IReadOnlyList<PlaceServicePlacement> counters,
        IReadOnlyList<PlacePersonPlacement> people,
        IReadOnlyList<PlaceFixturePlacement> fixtures,
        IReadOnlyList<PlaceFloorTrigger> triggers,
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

        // Every encounter stands on the spawn record that asked for it and states what the record asks for,
        // not what answers it: the slot, the grade when the record fixes one, the slot's kind, difficulty and
        // count range, and the monster rows the kind's graded variants are. Which grade and how many are the
        // ruleset's to decide when it populates the place, so no creature is written here.
        foreach (PlaceEncounterPlacement encounter in encounters)
        {
            placements.Add(new Placement("encounter", encounter.SourceSpawnIndex, "spawnPoints", new PlacementPoint(encounter.X, encounter.Y, encounter.Z), null, null, field =>
            {
                field.WriteNumber("spawn", encounter.SourceSpawnIndex);
                field.WriteNumber("encounter", encounter.EncounterIndex);
                field.WriteNumber("slot", encounter.Slot);
                if (encounter.FixedGrade is { } grade) field.WriteString("grade", grade);
                field.WriteString("monsterKind", encounter.MonsterKind);
                field.WriteNumber("difficulty", encounter.Difficulty);
                field.WriteNumber("appearMin", encounter.AppearMin);
                field.WriteNumber("appearMax", encounter.AppearMax);
                field.WriteNumber("group", encounter.Group);
                field.WriteNumber("attributes", encounter.Attributes);
                field.WriteNumber("radius", encounter.Radius);
                field.WriteStartArray("variants");
                foreach (PlaceEncounterVariant variant in encounter.Variants)
                {
                    field.WriteStartObject();
                    field.WriteString("grade", variant.Grade);
                    field.WriteNumber("monster", variant.MonsterId);
                    field.WriteString("monsterName", variant.MonsterName);
                    field.WriteEndObject();
                }

                field.WriteEndArray();
            }, encounter.PlacementId));
        }

        // A creature the level's own actor record stands is written as the record states it, under the actor
        // array's own field and index — the number a map event counting one creature's death names it by — and
        // nothing about it is chosen: whether a hidden one stands is the ruleset's reading.
        foreach (PlaceCreaturePlacement creature in creatures)
        {
            placements.Add(new Placement(PlaceCreatures.PlacementKind, creature.SourceActorIndex, "actors", new PlacementPoint(creature.X, creature.Y, creature.Z), creature.Yaw, "actor-record", field =>
            {
                field.WriteString("actorName", creature.SourceActorName);
                field.WriteNumber("monster", creature.MonsterId);
                field.WriteString("monsterName", creature.MonsterName);
                field.WriteNumber("group", creature.Group);
                field.WriteNumber("attributes", creature.Attributes);
                if (creature.HostilityGroup != 0) field.WriteNumber("hostilityGroup", creature.HostilityGroup);
                field.WriteNumber("aiState", creature.AiState);
                field.WriteNumber("hitPoints", creature.HitPoints);
                field.WriteNumber("sectorId", creature.SectorId);
                if (creature.UniqueNameIndex != 0) field.WriteNumber("uniqueNameIndex", creature.UniqueNameIndex);
                if (creature.Hidden) field.WriteBoolean("hidden", true);
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

        // A fixture names the event its use raises; the event itself is the place-event document's entry
        // under the same place and number, so what it does is written once rather than per face group.
        foreach (PlaceFixturePlacement fixture in fixtures)
        {
            placements.Add(new Placement("fixture", fixture.EventId, "events", new PlacementPoint(fixture.X, fixture.Y, fixture.Z), null, "event-face-centroid", field =>
            {
                field.WriteNumber("eventId", fixture.EventId);
                field.WriteString("heightSource", "event-faces-bottom");
                if (fixture.Label.Length > 0) field.WriteString("name", fixture.Label);
                field.WriteNumber("faceCount", fixture.FaceCount);
                if (fixture.ModelIndex is { } model) field.WriteNumber("sourceModel", model);
                if (fixture.ModelName.Length > 0) field.WriteString("sourceModelName", fixture.ModelName);
            }, fixture.PlacementId));
        }

        // A floor trigger is the event a place's pressure plates raise when the party walks onto one; the plates
        // are the reaches in the place-entrance document, each naming this placement, and the event is the
        // place-event entry under the same place and number.
        foreach (PlaceFloorTrigger trigger in triggers)
        {
            placements.Add(new Placement(PlaceFloorTrigger.PlacementKind, trigger.EventId, "events", new PlacementPoint(trigger.X, trigger.Y, trigger.Z), null, "event-face-centroid", field =>
            {
                field.WriteNumber("eventId", trigger.EventId);
                if (trigger.Label.Length > 0) field.WriteString("name", trigger.Label);
                field.WriteNumber("faceCount", trigger.FaceCount);
            }, trigger.PlacementId));
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

                    // The record's group, which a map event names when it turns a group hostile or counts its dead.
                    if (person.Group != 0) field.WriteNumber("group", person.Group);

                    // The record's standing toward the party, read as a level's own creature's is: its attribute bits,
                    // which carry the aggressor bit, and the kind it says it counts as when it names one.
                    field.WriteNumber("attributes", person.Attributes);
                    if (person.HostilityGroup != 0) field.WriteNumber("hostilityGroup", person.HostilityGroup);

                    // A person the level holds hidden is written as a creature's record is, and the ruleset decides
                    // that they do not stand.
                    if (person.Hidden) field.WriteBoolean("hidden", true);
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
                if (person.DialogueEvents > 0)
                {
                    writer.WriteNumber("dialogueEvents", person.DialogueEvents);

                    // The slots by position, an empty one as zero, because a map event changes a slot by where it is.
                    writer.WriteStartArray("topicSlots");
                    foreach (int slot in person.TopicSlots) writer.WriteNumberValue(slot);
                    writer.WriteEndArray();
                }

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
                    if (topic.Event is { } raised) writer.WriteNumber("event", raised);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }));
        }

        return WriteDocument(packDirectory, "people.json", "people", PlacePeopleEmitter.PersonDefinitionKind, entries);
    }

    /// <summary>The definition kind a row of the topic table is declared under, whoever it belongs to.</summary>
    internal const string TopicDefinitionKind = "person-topic";

    /// <summary>
    /// Writes the topic table: every row with something to say, by its number, which is what a map event that
    /// changes a person's slot names.
    /// </summary>
    private static int WriteTopics(string packDirectory, PlacePeopleSummary people)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PlacePersonTopic topic in people.Topics)
        {
            entries.Add((topic.Id, writer =>
            {
                writer.WriteString("label", topic.Label);
                writer.WriteString("text", topic.Text);
                writer.WriteNumber("textCount", topic.TextCount);
                if (topic.Requires != 0) writer.WriteNumber("requires", topic.Requires);
                if (topic.Event is { } raised) writer.WriteNumber("event", raised);
            }));
        }

        return WriteDocument(packDirectory, "topics.json", "topics", TopicDefinitionKind, entries);
    }

    /// <summary>The definition kind a row of the greeting table is declared under.</summary>
    internal const string GreetingDefinitionKind = "person-greeting";

    /// <summary>
    /// Writes the greeting table: every row by its number, with what is said on a first meeting and on a later one,
    /// which is what a map event that changes a person's greeting names.
    /// </summary>
    private static int WriteGreetings(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (NpcGreeting greeting in tables.People.Greetings)
        {
            entries.Add((greeting.Index.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("greeting", greeting.First);
                writer.WriteString("greetingAgain", greeting.Again);
            }));
        }

        return WriteDocument(packDirectory, "greetings.json", "greetings", GreetingDefinitionKind, entries);
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

    private static int WritePlaceGraph(
        string packDirectory,
        PlaceGraph graph,
        Mm7Tables tables,
        IReadOnlyDictionary<int, DecodedMap> maps,
        PlaceEntranceSummary entrances)
    {
        Dictionary<int, PlaceLinkAccount> accounts = entrances.Accounts.ToDictionary(account => account.LinkIndex);
        Dictionary<int, string> names = tables.Maps.Maps.ToDictionary(map => map.Id, map => map.Name);
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        int index = 0;
        foreach (PlaceLink link in graph.Links)
        {
            int linkIndex = index;
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

                // How a party takes the link is stated beside it, so a reader of the graph sees which links are a
                // use's, a plate's, a counter's or the world's, and under what condition, without a second document.
                if (accounts.TryGetValue(linkIndex, out PlaceLinkAccount? account))
                {
                    writer.WriteString("disposition", account.Disposition);
                    WriteOptionalString(writer, "trigger", account.Trigger);
                    WriteOptionalString(writer, "condition", account.Condition);
                }
            }));
            index++;
        }

        return WriteDocument(packDirectory, "place-graph.json", "place-graph", "travel-link", entries);
    }
}
