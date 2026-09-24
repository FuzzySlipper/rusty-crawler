using System.Text.Json;

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
          mm7import verify --install <directory>    checks the readers against the recorded inventory

        The importer reads the operator's own installation and writes nothing to it.
        """);
}
