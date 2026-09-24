using System.Text.Json;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Tool;

/// <summary>
/// The recorded inventory of the operator's installation, used as the acceptance oracle for the
/// readers. Every number here is written down in <c>docs/research/mm7-data-inventory.md</c>; a reader
/// that drifts from the data fails this check instead of failing quietly.
/// </summary>
internal static class InventoryCheck
{
    internal static int Run(string installRoot)
    {
        LodInstall install = LodInstall.Open(installRoot);
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

        foreach (string failure in failures) Console.Error.WriteLine($"inventory mismatch: {failure}");
        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                install = install.Root,
                result = failures.Count == 0 ? "pass" : "fail",
                checks = 20 + sharedNames.Length,
                failures,
            },
            new JsonSerializerOptions { WriteIndented = true }));
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Check(List<string> failures, string what, int expected, int actual)
    {
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
