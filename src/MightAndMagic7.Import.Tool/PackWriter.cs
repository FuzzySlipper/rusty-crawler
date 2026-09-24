using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Tool;

/// <summary>What one import wrote.</summary>
/// <param name="OutputRoot">The directory the packs were written to.</param>
/// <param name="Provenance">Where the content came from.</param>
/// <param name="Packs">The packs written, with their entry counts.</param>
internal sealed record PackWriteResult(string OutputRoot, InstallProvenance Provenance, IReadOnlyList<(string PackId, int Documents, int Entries)> Packs)
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

    private static readonly JsonWriterOptions Writer = new() { Indented = true, NewLine = "\n" };

    /// <summary>Writes every pack this importer produces for an installation.</summary>
    internal static PackWriteResult Write(LodInstall install, string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        InstallProvenance provenance = InstallProvenance.Read(install);
        Mm7Tables tables = Mm7Tables.Read(install);
        IReadOnlyList<EvtProgram> programs = EvtProgram.ReadAll(install);
        PlaceGraph graph = PlaceGraph.Build(programs, tables.Maps);

        Directory.CreateDirectory(outputRoot);
        List<(string, int, int)> packs =
        [
            WriteTables(tables, provenance, Path.Combine(outputRoot, "mm7-tables")),
            WriteWorld(tables, graph, provenance, Path.Combine(outputRoot, "mm7-world")),
        ];
        WriteBundleFragment(outputRoot, provenance, packs);
        return new PackWriteResult(outputRoot, provenance, packs);
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
        string packDirectory)
    {
        List<(string Path, string DocumentId, string Kind, int Entries)> documents =
        [
            ("places.json", "places", "place", WritePlaces(packDirectory, tables)),
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
        string packDirectory)
    {
        int links = WritePlaceGraph(packDirectory, graph, tables);
        IReadOnlyList<string> references = [.. tables.Maps.Maps.Select(map => $"place:{map.Id.ToString(CultureInfo.InvariantCulture)}")];
        WriteManifest(
            packDirectory,
            "mm7-world",
            "world",
            provenance,
            [("place-graph.json", "place-graph", "travel-link", references)]);
        return ("mm7-world", 1, links);
    }

    private static int WritePlaces(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (MapStatsRecord map in tables.Maps.Maps)
        {
            entries.Add((map.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("name", map.Name);
                writer.WriteString("mapFile", map.FileName);
                writer.WriteNumber("respawnDays", map.RespawnDays);
                writer.WriteNumber("alertDays", map.AlertDays);
                writer.WriteNumber("treasureLevel", map.TreasureLevel);
                writer.WriteNumber("encounterPercent", map.EncounterPercent);
                WriteOptionalString(writer, "track", map.Track);
                WriteOptionalString(writer, "environment", map.Environment);
            }));
        }

        return WriteDocument(packDirectory, "places.json", "places", "place", entries);
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

    private static int WritePlaceGraph(string packDirectory, PlaceGraph graph, Mm7Tables tables)
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
                writer.WriteNumber("toPlace", link.DestinationMapId!.Value);
                WriteOptionalString(writer, "toName", names.GetValueOrDefault(link.DestinationMapId.Value));
                writer.WriteNumber("x", link.X);
                writer.WriteNumber("y", link.Y);
                writer.WriteNumber("z", link.Z);
                writer.WriteNumber("yaw", link.Yaw);
                writer.WriteNumber("pitch", link.Pitch);
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
