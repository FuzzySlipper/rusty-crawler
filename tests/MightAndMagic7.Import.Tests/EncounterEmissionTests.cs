using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The encounters a level's spawn records ask for, and the hostility matrix the data carries.
/// </summary>
/// <remarks>
/// The shipped spawn records name one of a map's encounter slots rather than a monster row, so what these
/// tests are about is the reading that turns one into an encounter: which slot an index names, whether the
/// record fixes the grade, the slot's kind, difficulty and count range, the monster rows the kind's graded
/// variants are, and what a record that cannot be read is refused with. Which grade a creature is and how
/// many stand on the field are the ruleset's draw, so no creature is written here. The matrix is the other
/// half: which kinds are each other's enemies is game data, and it is written into the pack rather than
/// compiled into a ruleset.
/// </remarks>
public sealed class EncounterEmissionTests
{
    [Fact]
    public void A_spawn_record_becomes_an_encounter_naming_its_slot_its_grade_and_its_variants_and_no_creature()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-encounters-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriter.Write(LodInstall.Open(installRoot), imports);

            // The fixture's one spawn record per map asks for an actor and names encounter five, which is the
            // second slot graded A: the second slot names the kind "Monster 2", and the monster fixture's own
            // third trio carries the internal names "Monster 2 A/B/C", so its variants are rows four to six.
            using JsonDocument places = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-tables", "places.json")));
            int encounters = 0;
            foreach (JsonElement entry in places.RootElement.GetProperty("entries").EnumerateArray())
            {
                foreach (JsonElement placement in entry.GetProperty("placements").EnumerateArray())
                {
                    // The importer chooses nothing: no creature is written, only the request it stands for.
                    Assert.NotEqual("monster", placement.GetProperty("kind").GetString());
                    if (placement.GetProperty("kind").GetString() != "encounter") continue;
                    encounters++;

                    Assert.Equal("encounter-0", placement.GetProperty("id").GetString());
                    Assert.Equal(0, placement.GetProperty("spawn").GetInt32());
                    Assert.Equal(5, placement.GetProperty("encounter").GetInt32());
                    Assert.Equal(2, placement.GetProperty("slot").GetInt32());
                    Assert.Equal("A", placement.GetProperty("grade").GetString());
                    Assert.Equal("Monster 2", placement.GetProperty("monsterKind").GetString());
                    Assert.Equal(1, placement.GetProperty("difficulty").GetInt32());
                    Assert.Equal("spawnPoints", placement.GetProperty("sourceField").GetString());
                    Assert.Equal(0, placement.GetProperty("sourceIndex").GetInt32());

                    // The slot's range travels as the record states it; whether it is read for a count is the
                    // ruleset's reading of the record, not the importer's.
                    Assert.Equal(1, placement.GetProperty("appearMin").GetInt32());
                    Assert.Equal(3, placement.GetProperty("appearMax").GetInt32());

                    JsonElement[] variants = [.. placement.GetProperty("variants").EnumerateArray()];
                    Assert.Equal(["A", "B", "C"], variants.Select(variant => variant.GetProperty("grade").GetString()));
                    Assert.Equal([4, 5, 6], variants.Select(variant => variant.GetProperty("monster").GetInt32()));
                    Assert.Equal("Monster 4", variants[0].GetProperty("monsterName").GetString());
                }
            }

            // One actor spawn per map, and seventy-six maps: the same number the decoder read.
            Assert.Equal(MapStatsTable.ExpectedMaps, encounters);

            // The encounter stands at the spawn record's own point, and the spawn record is still a placement of
            // its own: the mark and the encounter are two facts about one record.
            JsonElement first = places.RootElement.GetProperty("entries")[0];
            JsonElement spawn = Assert.Single(
                first.GetProperty("placements").EnumerateArray(),
                placement => placement.GetProperty("kind").GetString() == "spawn");
            JsonElement asked = Assert.Single(
                first.GetProperty("placements").EnumerateArray(),
                placement => placement.GetProperty("kind").GetString() == "encounter");
            Assert.Equal(spawn.GetProperty("x").GetDouble(), asked.GetProperty("x").GetDouble());
            Assert.Equal(spawn.GetProperty("y").GetDouble(), asked.GetProperty("y").GetDouble());
            Assert.Equal(spawn.GetProperty("z").GetDouble(), asked.GetProperty("z").GetDouble());
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void An_encounter_states_the_range_of_creatures_it_can_resolve_to_and_not_a_choice()
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

            PlaceEncounterSummary summary = PlaceEncounters.Emit(tables, maps);

            // Every fixture record fixes its grade, so every one puts exactly one creature on the field whatever
            // its slot's range: the floor and the ceiling are both one per encounter, and nothing is drawn.
            Assert.Equal(MapStatsTable.ExpectedMaps, summary.EncounterCount);
            Assert.Equal(0, summary.DrawnGrades);
            Assert.Equal(0, summary.DrawnCounts);
            Assert.Equal(summary.EncounterCount, summary.FewestCreatures);
            Assert.Equal(summary.EncounterCount, summary.MostCreatures);
            Assert.All(summary.Placements, placement => Assert.Equal("A", placement.FixedGrade));

            // A random slot, read by hand, states its range as the floor and the ceiling of what it can resolve to.
            PlaceEncounterPlacement random = summary.Placements[0] with { FixedGrade = null };
            Assert.True(random.CountDrawn);
            Assert.Equal(1, random.FewestCreatures);
            Assert.Equal(3, random.MostCreatures);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
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

            PlaceEncounterSummary summary = PlaceEncounters.Emit(tables, maps);

            // Every record states an actor spawn and every one of them names the second slot, which these
            // maps leave empty: nothing is emitted and every record is named with the reason it was not.
            Assert.Equal(MapStatsTable.ExpectedMaps, summary.ActorSpawns);
            Assert.Empty(summary.Placements);
            Assert.Equal(MapStatsTable.ExpectedMaps, summary.Refusals.Count);
            Assert.Equal(MapStatsTable.ExpectedMaps, summary.RefusalCodes["spawn-encounter-empty"]);
            PlaceEncounterRefusal refusal = summary.Refusals[0];
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
    public void The_same_installation_emits_the_same_encounters_in_the_same_order()
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

            // Two readings of one installation, record for record: nothing about the emission depends on the
            // order a dictionary happened to enumerate or on a number drawn from anywhere.
            PlaceEncounterSummary first = PlaceEncounters.Emit(tables, maps);
            PlaceEncounterSummary second = PlaceEncounters.Emit(tables, maps);
            Assert.Equal(first.Refusals, second.Refusals);
            Assert.Equal(
                first.Placements.Select(placement => $"{placement.PlaceId}|{placement.PlacementId}|{placement.FixedGrade}|{string.Join(',', placement.Variants)}|{placement.X}|{placement.Y}"),
                second.Placements.Select(placement => $"{placement.PlaceId}|{placement.PlacementId}|{placement.FixedGrade}|{string.Join(',', placement.Variants)}|{placement.X}|{placement.Y}"));
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

            // The file's own shape at the shipped size: a header naming the party and every kind, and one
            // row per kind, each carrying a band per column. A band of zero is friendly, which is the donor's
            // own reading of the table's initial fill, and the fixture states one feud among its first kinds.
            int kinds = HostilityTable.ExpectedKinds;
            Assert.Equal(kinds + 1, matrix.Columns.Count);
            Assert.Equal("Party", matrix.Columns[0]);
            Assert.Equal($"Monster {kinds}", matrix.Columns[kinds]);
            Assert.Equal(kinds + 1, matrix.Rows.Count);
            Assert.All(matrix.Rows, row => Assert.Equal(kinds + 1, row.Bands.Count));
            Assert.Equal(4, matrix.Band("Monster 2", "Monster 3"));
            Assert.Equal(3, matrix.Band("Monster 3", "Monster 2"));
            Assert.Equal(0, matrix.Band("Monster 1", "Monster 2"));

            // Every cell is the fixture's own function of its position, so any cell can be checked; these are
            // the corners, the last row and column, and a spread between them.
            (int Row, int Column)[] probes = [(0, 0), (0, kinds), (kinds, 0), (kinds, kinds), (kinds, 1), (1, kinds), (kinds - 1, kinds), (17, 42), (42, 17), (60, 61), (87, 4)];
            foreach ((int row, int column) in probes)
            {
                Assert.Equal(SyntheticInstallation.HostilityBand(row, column), matrix.Rows[row].BandAt(column));
            }

            Assert.Equal(SyntheticInstallation.HostilityBand(kinds, 1), matrix.Band($"Monster {kinds}", "Monster 1"));
            Assert.Null(matrix.Rows[kinds].BandAt(kinds + 1));

            // The monster table carries more kinds than the matrix names, which the shipped data does too: a
            // kind past the matrix is nothing rather than a band read from somewhere else.
            Assert.Null(matrix.Band($"Monster {kinds + 1}", "Monster 2"));
            Assert.Null(matrix.Band("Monster 2", $"Monster {kinds + 1}"));

            // One row spells its kind with spacing the header does not repeat. Looked up by name it is not
            // found, and it is still the row at its own position, which is how the donor reads the matrix.
            int misnamed = SyntheticInstallation.MisnamedHostilityRow;
            Assert.Equal(1, matrix.MismatchedNames);
            Assert.Null(matrix.RowOf($"Monster {misnamed}"));
            Assert.Equal($"Monster  {misnamed}", matrix.Rows[misnamed].Kind);

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
            JsonElement header = entries[0];
            Assert.Equal("kinds", header.GetProperty("id").GetString());
            Assert.Equal(kinds + 1, header.GetProperty("columns").GetArrayLength());
            Assert.Equal($"Monster {kinds}", header.GetProperty("columns")[kinds].GetString());
            Assert.Equal(kinds + 2, entries.GetArrayLength());

            JsonElement beast = Assert.Single(
                entries.EnumerateArray(),
                entry => entry.GetProperty("id").GetString() == "Monster 2");
            Assert.Equal(2, beast.GetProperty("kind").GetInt32());
            Assert.Equal(4, beast.GetProperty("hostility").GetProperty("3").GetInt32());
            Assert.False(beast.GetProperty("hostility").TryGetProperty("0", out _));

            // The last kind's row is written whole: every non-zero band of its own against the column it was
            // read from, the last column included, and no zero band at all.
            JsonElement last = entries[kinds + 1];
            Assert.Equal($"Monster {kinds}", last.GetProperty("id").GetString());
            Assert.Equal(kinds, last.GetProperty("kind").GetInt32());
            Dictionary<string, int> written = last.GetProperty("hostility").EnumerateObject().ToDictionary(band => band.Name, band => band.Value.GetInt32());
            Dictionary<string, int> stated = Enumerable.Range(0, kinds + 1)
                .Where(column => SyntheticInstallation.HostilityBand(kinds, column) != 0)
                .ToDictionary(column => column.ToString(System.Globalization.CultureInfo.InvariantCulture), column => SyntheticInstallation.HostilityBand(kinds, column));
            Assert.Equal(stated, written);

            // The misnamed row keeps its position as its kind in the pack, under the name the file spells.
            JsonElement misnamedEntry = Assert.Single(
                entries.EnumerateArray(),
                entry => entry.GetProperty("id").GetString() == $"Monster  {misnamed}");
            Assert.Equal(misnamed, misnamedEntry.GetProperty("kind").GetInt32());
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
