using System.Text.Json;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Media;

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
                use = "add these pack ids to a bundle under content/partyrpg/bundles to load them",
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
