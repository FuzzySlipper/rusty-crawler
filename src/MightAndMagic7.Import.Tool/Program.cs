using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Media;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Tool;

/// <summary>The operator-facing command line over the importer.</summary>
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static int Main(string[] arguments)
    {
        if (arguments.Length == 0 || arguments[0] is "-h" or "--help" or "help")
        {
            Usage();
            return arguments.Length == 0 ? 2 : 0;
        }

        try
        {
            return arguments[0] switch
            {
                "info" => Info(Require(arguments, 1, "info <archive>")),
                "list" => List(Require(arguments, 1, "list <archive>")),
                "report" => Report(RequireInstall(arguments)),
                "verify" => InventoryCheck.Run(RequireInstall(arguments)),
                "write" => Write(RequireInstall(arguments), RequireOption(arguments, "--output"), arguments.Contains("--check-determinism")),
                "maps" => Maps(RequireInstall(arguments)),
                "creatures" => Creatures(RequireInstall(arguments)),
                "people" => PeopleDetail(RequireInstall(arguments)),
                "media" => Media(RequireInstall(arguments), RequireOption(arguments, "--output")),
                _ => Unknown(arguments[0]),
            };
        }
        catch (Exception error) when (error is Lod.LodFormatException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static int Info(string archivePath)
    {
        Lod.LodArchive archive = Lod.LodArchive.Open(archivePath);
        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                archive = archive.Name,
                versionDeclaredByHeader = archive.VersionString,
                entryRecordSize = archive.FileEntrySize,
                description = archive.Description,
                entries = archive.Entries.Count,
                duplicateNames = archive.DuplicateEntryNames,
            },
            Json));
        return 0;
    }

    private static int List(string archivePath)
    {
        Lod.LodArchive archive = Lod.LodArchive.Open(archivePath);
        foreach (Lod.LodEntry entry in archive.Entries)
        {
            Console.WriteLine($"{entry.Size,10}  {entry.Name}");
        }

        return 0;
    }

    private static int Report(string installRoot)
    {
        Lod.LodInstall install = Lod.LodInstall.Open(installRoot);
        List<object> archives = [];
        long decoded = 0;
        List<string> undecoded = [];
        foreach (string name in install.ArchiveNames())
        {
            Lod.LodArchive archive = install.Archive(name);
            long decodedHere = 0;
            foreach (Lod.LodEntry entry in archive.Entries)
            {
                try
                {
                    archive.Read(entry);
                    decodedHere++;
                }
                catch (Lod.LodFormatException error)
                {
                    undecoded.Add($"{name}:{entry.Name}: {error.Message}");
                }
            }

            decoded += decodedHere;
            archives.Add(new
            {
                name,
                versionDeclaredByHeader = archive.VersionString,
                entries = archive.Entries.Count,
                decodedHere,
                duplicateNames = archive.DuplicateEntryNames.Count,
            });
        }

        Tables.TableInventory inventory = Tables.TableInventory.Read(install);
        Tables.Mm7Tables tables = Tables.Mm7Tables.Read(install);
        IReadOnlyList<Events.EvtProgram> programs = Events.EvtProgram.ReadAll(install);
        World.PlaceGraph graph = World.PlaceGraph.Build(programs, tables.Maps);

        HashSet<byte> decodedOpcodes = [Events.EvtOpcodes.Exit, Events.EvtOpcodes.MoveToMap];
        SortedDictionary<byte, int> unreadOpcodes = [];
        foreach (Events.EvtProgram program in programs)
        {
            foreach ((byte opcode, int count) in program.OpcodeCounts)
            {
                if (decodedOpcodes.Contains(opcode)) continue;
                unreadOpcodes[opcode] = unreadOpcodes.GetValueOrDefault(opcode) + count;
            }
        }

        var report = new
        {
            install = install.Root,
            archives,
            entriesDecoded = decoded,
            entriesNotDecoded = undecoded.Count,
            textTables = inventory.TextTables.Count,
            textTableNames = inventory.TextTables.Select(table => table.Name).ToArray(),
            ambiguousTableNames = inventory.AmbiguousTables.Select(table => new { table.Name, table.Archives }).ToArray(),
            typedTables = new
            {
                classes = tables.Classes.Ranks.Count,
                skills = tables.Skills.Skills.Count,
                maps = tables.Maps.Maps.Count,
                buildings = tables.Services.Buildings.Count,
                services = tables.Services.Services.Count(),
                monsters = tables.Monsters.Monsters.Count,
                hostilityKinds = tables.Hostility.Rows.Count,
                hostilityNameMismatches = tables.Hostility.MismatchedNames,
                spells = tables.Spells.Spells.Count,
                spellSchools = tables.Spells.Schools.Count,
                quests = tables.Quests.Quests.Count,
                itemRows = tables.Items.Items.Count,
            },
            eventPrograms = programs.Count,
            mapMoves = graph.MoveInstructionCount,
            mapLinks = graph.Links.Count,
            withinMapMoves = graph.WithinMapMoves.Count,
            linksFromMapPrograms = graph.LinksFromMapPrograms,
            linksFromGlobalProgram = graph.LinksFromGlobalProgram,
            unorderedMapPairs = graph.Links
                .Where(link => link.SourceMapId is not null)
                .Select(link => (Low: Math.Min(link.SourceMapId!.Value, link.DestinationMapId!.Value), High: Math.Max(link.SourceMapId!.Value, link.DestinationMapId!.Value)))
                .Distinct()
                .Count(),
            exitInstructions = graph.ExitInstructionCount,
            programsWithoutAMap = graph.ProgramsWithoutAMap,
            opcodesNotDecoded = unreadOpcodes.Count,
            ruleFamiliesTheDataDoesNotCarry = Tables.TableInventory.KnownGaps.Select(gap => new { gap.Name, gap.Why, gap.Owner }).ToArray(),
        };
        Console.WriteLine(JsonSerializer.Serialize(report, Json));
        return 0;
    }

    private static string Require(string[] arguments, int index, string usage)
    {
        if (arguments.Length <= index)
        {
            throw new Lod.LodFormatException($"Missing argument. Usage: mm7import {usage}");
        }

        return arguments[index];
    }

    /// <summary>
    /// Reports the creatures the operator's own spawn records put on the field, per place, with everything
    /// nothing was emitted for and why.
    /// </summary>
    /// <remarks>
    /// This is the read-only half of the creature import and the same reading the writer makes: it decodes
    /// the maps, resolves every actor spawn through its map's encounter slots and the monster table, and
    /// prints what a write would emit without writing anything. It exists because "does the world hold
    /// monsters, and where" is a question an operator asks before generating packs rather than after
    /// playing them, and because a remainder has to be a named record rather than a smaller total.
    /// </remarks>
    private static int Creatures(string installRoot)
    {
        LodInstall install = LodInstall.Open(installRoot);
        Mm7Tables tables = Mm7Tables.Read(install);
        MapDecodeReport report = MapDecoder.DecodeAll(install);
        Dictionary<int, DecodedMap> maps = report.Decoded
            .Where(outcome => outcome.Decoded is not null)
            .ToDictionary(outcome => outcome.Map.Id, outcome => outcome.Decoded!);
        PlaceCreatureSummary summary = PlaceCreatures.Emit(tables, maps);

        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                install = install.Root,
                placesHoldingCreatures = summary.PopulatedPlaces,
                creatures = summary.CreatureCount,
                distinctMonsters = summary.DistinctMonsters,
                spawnRecords = summary.SpawnRecords,
                actorSpawns = summary.ActorSpawns,
                treasureSpawns = summary.TreasureSpawns,
                perPlace = summary.PerPlace.Select(count => new
                {
                    place = count.Key,
                    name = tables.Maps.Maps.First(map => map.Id == count.Key).Name,
                    creatures = count.Value,
                }),
                refusalsByReason = summary.RefusalCodes,
                refusals = summary.Refusals.Select(refusal => new
                {
                    reason = refusal.Code,
                    subject = refusal.Subject,
                    detail = refusal.Reason,
                }),
                notes = summary.Notes,
            },
            Json));
        return 0;
    }

    private static int Maps(string installRoot)
    {
        MapDecodeReport report = MapDecoder.DecodeAll(Lod.LodInstall.Open(installRoot));
        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                maps = report.MapCount,
                decoded = report.DecodedCount,
                failed = report.FailureCount,
                outdoor = Describe(report.Outdoor),
                indoor = Describe(report.Indoor),
                total = Describe(report.Total),
                failures = report.Failures.Select(failure => new { failure.MapId, failure.FileName, failure.Reason }),
            },
            Json));
        return report.FailureCount == 0 ? 0 : 1;
    }

    private static object Describe(MapFamilyCounts counts) => new
    {
        counts.Maps,
        counts.Faces,
        counts.Vertices,
        counts.Doors,
        counts.Lights,
        counts.EntryPoints,
        counts.Decorations,
        counts.SpawnPoints,
    };

    private static int Media(string installRoot, string outputRoot)
    {
        MediaManifest manifest = MediaExtractor.Extract(Lod.LodInstall.Open(installRoot), outputRoot);
        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                output = outputRoot,
                decoder = manifest.DecoderVersion,
                emitted = manifest.EmittedCount,
                bytes = manifest.EmittedBytes,
                archives = manifest.Archives.Select(archive => new { archive.Name, archive.Kind, archive.Entries, archive.Artifacts, archive.Emitted }),
                boundaries = manifest.Boundaries.Select(boundary => new { boundary.Family, boundary.Reason }),
            },
            Json));
        return 0;
    }

    private static int Write(string installRoot, string outputRoot, bool checkDeterminism)
    {
        Lod.LodInstall install = Lod.LodInstall.Open(installRoot);
        PackWriteResult result = PackWriter.Write(install, outputRoot);
        if (checkDeterminism)
        {
            string second = outputRoot.TrimEnd('/') + ".second";
            if (Directory.Exists(second)) Directory.Delete(second, recursive: true);
            PackWriter.Write(install, second);
            bool identical = PackWriter.AreIdentical(outputRoot, second);
            Directory.Delete(second, recursive: true);
            Console.WriteLine(JsonSerializer.Serialize(new { determinism = identical ? "identical" : "differs", secondRun = second }, Json));
            if (!identical) return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                result.OutputRoot,
                provenance = new { result.Provenance.Game, build = result.Provenance.BuildString },
                packs = result.Packs.Select(pack => new { pack.PackId, pack.Documents, pack.Entries }),
                geometry = Describe(result.Geometry),
                entrances = Describe(result.Entrances),
                services = Describe(result.Services),
                people = Describe(result.People),
                creatures = Describe(result.Creatures),
                use = "add these pack ids to a bundle under content/partyrpg/bundles to load them",
            },
            Json));
        return 0;
    }

    /// <summary>
    /// What the creature derivation produced: how many records asked for a creature, how many creatures were
    /// emitted where, and every record nothing was emitted for with its reason.
    /// </summary>
    /// <remarks>
    /// The counts are stated because "the world holds monsters" is a claim about the operator's own data:
    /// the actor spawns are the records a creature can come from, the treasure spawns are the ones that ask
    /// for loot instead, and a refusal is listed rather than only counted so an operator can see which
    /// record asked for something the import could not read.
    /// </remarks>
    private static object Describe(Packs.PlaceCreatureSummary creatures) => new
    {
        spawnRecords = creatures.SpawnRecords,
        actorSpawns = creatures.ActorSpawns,
        treasureSpawns = creatures.TreasureSpawns,
        emitted = creatures.CreatureCount,
        places = creatures.PopulatedPlaces,
        distinctMonsters = creatures.DistinctMonsters,
        refusals = creatures.Refusals.Select(refusal => new
        {
            subject = refusal.Subject,
            reason = refusal.Code,
            detail = refusal.Reason,
        }),
        notes = creatures.Notes,
    };

    /// <summary>
    /// What the people derivation produced: who the tables carry, where they stand, and everything nothing
    /// was placed for.
    /// </summary>
    /// <remarks>
    /// The counts are stated per source because the two sources are different facts about the data: the NPC
    /// table's own placement column is who lives in a building, and a map's actor records are who is
    /// standing in the open. A person nothing was placed for is listed with its reason rather than only
    /// counted, because an unreachable person is a remainder the operator has to see to judge the import.
    /// </remarks>
    private static object Describe(Packs.PlacePeopleSummary people) => new
    {
        persons = people.PersonCount,
        topics = people.TopicCount,
        gatedTopics = people.GatedTopicCount,
        branchedTopics = people.BranchedTopicCount,
        greeted = people.GreetedCount,
        scripted = people.ScriptedCount,
        standingInTheOpen = people.PlacementCount,
        distinctPeopleInTheOpen = people.PlacedPersonCount,
        inBuildings = people.ResidentCount,
        buildingsWithPeople = people.HouseholdCount,
        reachedBuildings = people.ReachableHouseholdCount,
        unreachableResidents = people.UnreachableResidentCount,
        refusals = people.Refusals.Select(refusal => new
        {
            subject = refusal.Subject,
            reason = refusal.Code,
            detail = refusal.Reason,
        }),
    };

    /// <summary>
    /// Reports the people the operator's own data carries, per source, with every remainder named.
    /// </summary>
    /// <remarks>
    /// This is the read-only half of the people import: it decodes the maps and reads the tables the same
    /// way the writer does, so what it reports is what a write would emit, without writing anything. It
    /// exists because the counts are the answer to "is there anybody in the world", which is a question an
    /// operator asks before generating packs rather than after playing them.
    /// </remarks>
    private static int PeopleDetail(string installRoot)
    {
        Lod.LodInstall install = Lod.LodInstall.Open(installRoot);
        Tables.Mm7Tables tables = Tables.Mm7Tables.Read(install);
        IReadOnlyList<Events.EvtProgram> programs = Events.EvtProgram.ReadAll(install);
        MapDecodeReport report = MapDecoder.DecodeAll(install);
        Dictionary<int, DecodedMap> maps = [];
        foreach (MapDecodeOutcome outcome in report.Decoded)
        {
            if (outcome.Decoded is not null) maps[outcome.Map.Id] = outcome.Decoded;
        }

        Packs.PlaceServiceSummary services = Packs.PlaceServiceEmitter.Emit(tables.Services, tables, programs, maps);
        Packs.PlacePeopleSummary people = Packs.PlacePeopleEmitter.Emit(tables.People, maps, services);

        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                install = install.Root,
                maps = maps.Count,
                actors = maps.Values.Sum(map => map.Delta?.ActorCount ?? 0),
                peopleWithAnIdentity = maps.Values.Sum(map => map.Delta?.PersonCount ?? 0),
                npcRows = tables.People.Npcs.Count,
                npcRowsInABuilding = tables.People.PlacedCount,
                buildingsNamed = tables.People.PlacedHouseCount,
                greetings = tables.People.Greetings.Count,
                topics = tables.People.Topics.Count,
                texts = tables.People.Texts.Count,
                derived = Describe(people),
                notes = people.Notes,
                topicsByOwner = tables.People.Topics
                    .SelectMany(topic => topic.OwnerIds.Select(owner => (Owner: owner, Topic: topic)))
                    .GroupBy(entry => entry.Owner)
                    .OrderBy(group => group.Key)
                    .Select(group => new { npc = group.Key, topics = group.Count() }),
            },
            Json));
        return 0;
    }

    /// <summary>
    /// What the entrance derivation produced: the reaches a walking party can take, and every link it
    /// cannot.
    /// </summary>
    /// <remarks>
    /// The untriggerable links are reported one by one with their reason rather than only counted: a link
    /// nothing can walk into is a journey the product cannot make, and the operator needs to see which
    /// ones those are — and why — without reading the pack or the maps back.
    /// </remarks>
    private static object Describe(Packs.PlaceEntranceSummary entrances) => new
    {
        places = entrances.PlaceCount,
        links = entrances.LinkCount,
        reaches = entrances.ReachCount,
        pressurePlates = entrances.PressurePlateCount,
        clickable = entrances.ClickableCount,
        untriggerable = entrances.UntriggerableCount,
        refusals = entrances.Refusals.Select(refusal => new
        {
            link = refusal.LinkIndex,
            from = refusal.FromPlace,
            to = refusal.ToPlace,
            refusal.EventId,
            refusal.Step,
            reason = refusal.Code,
            detail = refusal.Reason,
        }),
    };

    /// <summary>
    /// What the building table's emission produced: the counters and households a party can walk up to, and
    /// every row nothing was placed for.
    /// </summary>
    /// <remarks>
    /// A row with no placement is a building the product cannot enter, and the operator needs to see which
    /// ones those are, and why, without reading the table back. The refusals are listed with their rows so a
    /// remainder is a fact about the data rather than a number that came up short.
    /// </remarks>
    private static object Describe(Packs.PlaceServiceSummary services) => new
    {
        counters = services.ServiceCount,
        placed = services.CounterCount,
        residences = services.ResidenceCount,
        places = services.PlaceCount,
        fares = services.FareCount,
        unplaced = services.RefusalCount,
        byKind = services.Services
            .GroupBy(service => service.Kind)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new { kind = group.Key, rows = group.Count() }),
        refusals = services.Refusals.Select(refusal => new
        {
            building = refusal.BuildingId,
            type = refusal.Type,
            name = refusal.Name,
            map = refusal.MapId,
            reason = refusal.Code,
            detail = refusal.Reason,
        }),
    };

    /// <summary>The geometry sources a report states counts for, in the order the pack writes them.</summary>
    private static readonly Collision.CollisionSource[] GeometrySources =
    [
        Collision.CollisionSource.InteriorFace,
        Collision.CollisionSource.Terrain,
        Collision.CollisionSource.ModelFace,
    ];

    /// <summary>
    /// What the collision emission did, per place family and per geometry source.
    /// </summary>
    /// <remarks>
    /// A refused place is reported with its reason rather than only counted: no artifact means the party
    /// has nothing to stand on in that place, which the product reports on entry, and the operator needs
    /// to see which places those are without reading the pack.
    /// </remarks>
    private static object Describe(Collision.CollisionSummary geometry) => new
    {
        places = geometry.PlaceCount,
        emitted = geometry.EmittedCount,
        refused = geometry.RefusedCount,
        regions = geometry.EmittedOf(MightAndMagic7.Import.Maps.MapKind.Outdoor),
        interiors = geometry.EmittedOf(MightAndMagic7.Import.Maps.MapKind.Indoor),
        geometry.Vertices,
        geometry.Triangles,
        sources = GeometrySources.Select(source => new
        {
            source = source.ToString(),
            geometry.CountOf(source).Faces,
            geometry.CountOf(source).Triangles,
        }),
        refusals = geometry.Refused.Select(place => new
        {
            place = place.PlaceId,
            place.FileName,
            reason = place.Refusal?.Code,
            detail = place.Refusal?.Detail,
        }),
    };

    private static string RequireOption(string[] arguments, string name)
    {
        for (int index = 1; index < arguments.Length - 1; index++)
        {
            if (arguments[index] == name) return arguments[index + 1];
        }

        throw new Lod.LodFormatException($"Missing {name}. Usage: mm7import write --install <game-directory> --output <directory> [--check-determinism]");
    }

    private static string RequireInstall(string[] arguments)
    {
        for (int index = 1; index < arguments.Length - 1; index++)
        {
            if (arguments[index] == "--install") return arguments[index + 1];
        }

        return Require(arguments, 1, "report --install <game-directory>");
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Usage();
        return 2;
    }

    private static void Usage() => Console.Error.WriteLine(
        """
        mm7import - offline importer for Might and Magic VII data

          mm7import info <archive.lod>              header, entry count, duplicate names
          mm7import list <archive.lod>              every entry with its size
          mm7import report --install <directory>    full extraction report for a game installation
          mm7import maps --install <directory>      decodes every map and reports counts and failures
          mm7import media --install <directory> --output <directory>
                                                    extracts images, palettes, sprites, and sound with a manifest
          mm7import verify --install <directory>    checks the readers against the recorded inventory
          mm7import write --install <directory> --output <directory> [--check-determinism]
                                                    writes normalized content packs and, on request,
                                                    proves two runs produce identical bytes

        The importer reads the operator's own installation and writes nothing to it.
        """);
}
