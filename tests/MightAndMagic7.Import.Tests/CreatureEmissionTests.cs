using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The creatures a level's spawn records put on the field, and the hostility matrix the data carries.
/// </summary>
/// <remarks>
/// The shipped spawn records name one of a map's encounter slots rather than a monster row, so what these
/// tests are about is the reading that turns one into the other: which slot an index names, which graded
/// variant the map's own difficulty odds favour, how many creatures the slot spawns, and what a record that
/// cannot be read is refused with. The matrix is the other half: which kinds are each other's enemies is
/// game data, and it is written into the pack rather than compiled into a ruleset.
/// </remarks>
public sealed class CreatureEmissionTests
{
    [Fact]
    public void A_spawn_record_becomes_a_creature_naming_the_row_its_slot_and_grade_resolve_to()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-creatures-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriter.Write(LodInstall.Open(installRoot), imports);

            // The fixture's one spawn record per map asks for an actor and names encounter five, which is
            // the second slot graded A: the second slot names the kind "Monster 2", and the monster table's
            // own third trio is "Monster 2 A/B/C", so the row is the fourth.
            using JsonDocument places = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-tables", "places.json")));
            int creatures = 0;
            foreach (JsonElement entry in places.RootElement.GetProperty("entries").EnumerateArray())
            {
                foreach (JsonElement placement in entry.GetProperty("placements").EnumerateArray())
                {
                    if (placement.GetProperty("kind").GetString() != "monster") continue;
                    creatures++;

                    // The row is named under the field a ruleset reads a creature from, and everything else
                    // on the placement is the reading that produced it rather than a claim about the data.
                    // The row is the fourth: the monster fixture's third trio carries the internal names
                    // "Monster 2 A/B/C", so grade A of the kind named "Monster 2" is the fourth row.
                    Assert.Equal(4, placement.GetProperty("monster").GetInt32());
                    Assert.Equal("Monster 4", placement.GetProperty("monsterName").GetString());
                    Assert.Equal(0, placement.GetProperty("spawn").GetInt32());
                    Assert.Equal(5, placement.GetProperty("encounter").GetInt32());
                    Assert.Equal("A", placement.GetProperty("grade").GetString());
                    Assert.Equal("spawn-slot", placement.GetProperty("gradeSource").GetString());
                    Assert.Equal(1, placement.GetProperty("quantity").GetInt32());
                    Assert.Equal("spawn-slot", placement.GetProperty("countSource").GetString());
                    Assert.Equal("spawnPoints", placement.GetProperty("sourceField").GetString());
                    Assert.Equal(0, placement.GetProperty("sourceIndex").GetInt32());

                    // The grade is fixed by the record's own index rather than drawn, so the slot's range
                    // travels as provenance and is not read for a count: a graded slot spawns exactly one
                    // creature, which is what the donor's own fixed count is.
                    Assert.Equal(1, placement.GetProperty("appearMin").GetInt32());
                    Assert.Equal(3, placement.GetProperty("appearMax").GetInt32());
                }
            }

            // One actor spawn per map, and seventy-six maps: the same number the decoder read.
            Assert.Equal(MapStatsTable.ExpectedMaps, creatures);

            // The creature is placed at the spawn record's own point when it is the only one there, and the
            // spawn record the placed point came from is still a placement of its own: the mark and the
            // creature are two facts about one record.
            using JsonDocument again = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-tables", "places.json")));
            JsonElement first = again.RootElement.GetProperty("entries")[0];
            JsonElement spawn = Assert.Single(
                first.GetProperty("placements").EnumerateArray(),
                placement => placement.GetProperty("kind").GetString() == "spawn");
            JsonElement beast = Assert.Single(
                first.GetProperty("placements").EnumerateArray(),
                placement => placement.GetProperty("kind").GetString() == "monster");
            Assert.Equal(spawn.GetProperty("x").GetDouble(), beast.GetProperty("x").GetDouble());
            Assert.Equal(spawn.GetProperty("y").GetDouble(), beast.GetProperty("y").GetDouble());
            Assert.Equal(spawn.GetProperty("z").GetDouble(), beast.GetProperty("z").GetDouble());
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_spawn_naming_an_empty_encounter_slot_is_refused_with_its_reason_and_its_place()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, emptyEncounterSlots: true);
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            Mm7Tables tables = Mm7Tables.Read(install);
            MapDecodeReport report = MapDecoder.DecodeAll(install);
            Dictionary<int, DecodedMap> maps = report.Decoded
                .Where(outcome => outcome.Decoded is not null)
                .ToDictionary(outcome => outcome.Map.Id, outcome => outcome.Decoded!);

            PlaceCreatureSummary summary = PlaceCreatures.Emit(tables, maps);

            // Every record states an actor spawn and every one of them names the second slot, which these
            // maps leave empty: nothing is emitted and every record is named with the reason it was not.
            Assert.Equal(MapStatsTable.ExpectedMaps, summary.ActorSpawns);
            Assert.Empty(summary.Placements);
            Assert.Equal(MapStatsTable.ExpectedMaps, summary.Refusals.Count);
            Assert.Equal(MapStatsTable.ExpectedMaps, summary.RefusalCodes["spawn-encounter-empty"]);
            PlaceCreatureRefusal refusal = summary.Refusals[0];
            Assert.Contains("place 1", refusal.Subject, StringComparison.Ordinal);
            Assert.Contains("spawn 0", refusal.Subject, StringComparison.Ordinal);
            Assert.Contains("states no monster", refusal.Reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void The_same_installation_emits_the_same_creatures_in_the_same_order()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            Mm7Tables tables = Mm7Tables.Read(install);
            MapDecodeReport report = MapDecoder.DecodeAll(install);
            Dictionary<int, DecodedMap> maps = report.Decoded
                .Where(outcome => outcome.Decoded is not null)
                .ToDictionary(outcome => outcome.Map.Id, outcome => outcome.Decoded!);

            // Two readings of one installation, record for record: nothing about the emission depends on
            // the order a dictionary happened to enumerate or on a number drawn from anywhere.
            PlaceCreatureSummary first = PlaceCreatures.Emit(tables, maps);
            PlaceCreatureSummary second = PlaceCreatures.Emit(tables, maps);
            Assert.Equal(first.Placements, second.Placements);
            Assert.Equal(first.Refusals, second.Refusals);
            Assert.Equal(
                first.Placements.Select(placement => $"{placement.PlaceId}|{placement.PlacementId}|{placement.MonsterId}|{placement.X}|{placement.Y}"),
                second.Placements.Select(placement => $"{placement.PlaceId}|{placement.PlacementId}|{placement.MonsterId}|{placement.X}|{placement.Y}"));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void The_hostility_matrix_the_data_carries_is_read_as_kinds_and_write_as_content()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-hostility-{Guid.NewGuid():N}");
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            HostilityTable matrix = HostilityTable.Read(install);

            // The file's own shape: a header naming every kind and one row per kind, each carrying a band
            // per column. A band of zero is friendly, which is the donor's own reading of the table's
            // initial fill, and the fixture states one feud.
            Assert.Equal(["Party", "Monster 1", "Monster 2", "Monster 3"], matrix.Columns);
            Assert.Equal(4, matrix.Rows.Count);
            Assert.Equal(4, matrix.Band("Monster 2", "Monster 3"));
            Assert.Equal(3, matrix.Band("Monster 3", "Monster 2"));
            Assert.Equal(0, matrix.Band("Monster 1", "Monster 2"));
            Assert.Null(matrix.Band("Monster 9", "Monster 2"));

            // The reader maps a row to a column without assuming the file is square: a kind the matrix does
            // not name is nothing rather than column zero.
            Assert.Equal(2, matrix.ColumnOf("Monster 2"));
            Assert.Null(matrix.ColumnOf("Nobody"));
            Assert.NotNull(matrix.RowOf("Monster 3"));
            Assert.Null(matrix.RowOf("Nobody"));

            // What the pack carries: the header as one entry and each kind's non-zero bands against the
            // column they were read from, so a reader that only knows a kind's number can read it.
            string imports = Path.Combine(root, "imports");
            PackWriter.Write(install, imports);
            using JsonDocument hostility = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-tables", "hostility.json")));
            JsonElement entries = hostility.RootElement.GetProperty("entries");
            JsonElement kinds = entries[0];
            Assert.Equal("kinds", kinds.GetProperty("id").GetString());
            Assert.Equal(4, kinds.GetProperty("columns").GetArrayLength());

            JsonElement beast = Assert.Single(
                entries.EnumerateArray(),
                entry => entry.GetProperty("id").GetString() == "Monster 2");
            Assert.Equal(2, beast.GetProperty("kind").GetInt32());
            Assert.Equal(4, beast.GetProperty("hostility").GetProperty("3").GetInt32());
            Assert.False(beast.GetProperty("hostility").TryGetProperty("0", out _));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
