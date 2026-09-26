using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The treasure rules the shipped tables carry, read where their format is known: a monster row's own cell,
/// the random-item weights, and the two vocabularies an item and a treasure request are compared by.
/// </summary>
/// <remarks>
/// These shapes are the source format's, and the donor documents every one of them: the monster cell's four
/// parts, the item row's equipment and skill words, and the random-item table's two sections. Reading them
/// here is what lets the packs carry numbers, so a case that disagrees with the donor fails while the data
/// is being read rather than in play.
/// </remarks>
public sealed class TreasureTableTests
{
    [Theory]
    [InlineData("0", 0, 0, 0, 0, "", "")]
    [InlineData("", 0, 0, 0, 0, "", "")]
    [InlineData("2D6", 100, 2, 6, 0, "", "")]
    [InlineData("10%10D20+L3Misc", 10, 10, 20, 3, "", "misc")]
    [InlineData("5%5D10+L2Cape", 5, 5, 10, 2, "cloak", "")]
    [InlineData("5D10+L1Ring", 100, 5, 10, 1, "ring", "")]
    [InlineData("400D10+L6", 100, 400, 10, 6, "", "")]
    [InlineData(" 300D10L5 ", 100, 300, 10, 5, "", "")]
    [InlineData("5%15D6+L2Sword", 5, 15, 6, 2, "", "sword")]
    [InlineData("2%L1Ring", 2, 0, 0, 1, "ring", "")]
    [InlineData("1%L4", 1, 0, 0, 4, "", "")]
    [InlineData("25%12D4", 25, 12, 4, 0, "", "")]
    public void A_monster_rows_treasure_cell_is_read_into_the_numbers_it_states(
        string cell,
        int chance,
        int rolls,
        int sides,
        int level,
        string kind,
        string skill)
    {
        MonsterTreasure treasure = MonsterTable.ReadTreasure(cell);
        Assert.Equal(chance, treasure.Chance);
        Assert.Equal(rolls, treasure.GoldRolls);
        Assert.Equal(sides, treasure.GoldSides);
        Assert.Equal(level, treasure.Level);
        Assert.Equal(kind, treasure.Kind);
        Assert.Equal(skill, treasure.Skill);
    }

    [Fact]
    public void A_cell_that_states_a_kind_the_data_never_names_asks_for_anything()
    {
        // The donor's own answer for a word outside its map is a request for any kind at all, and the level
        // it stated is kept: a word nothing knows is not a reason to drop the request.
        MonsterTreasure treasure = MonsterTable.ReadTreasure("5%5D10+L3Whatsit");
        Assert.Equal(3, treasure.Level);
        Assert.Equal(string.Empty, treasure.Kind);
        Assert.Equal(string.Empty, treasure.Skill);
    }

    [Theory]
    [InlineData("Weapon", "single-handed")]
    [InlineData("weapon2", "two-handed")]
    [InlineData("Missile", "bow")]
    [InlineData("Bow", "bow")]
    [InlineData("armor", "armour")]
    [InlineData("Helm", "helmet")]
    [InlineData("WeaponW", "wand")]
    [InlineData("Sscroll", "spell-scroll")]
    [InlineData("Mscroll", "message-scroll")]
    [InlineData("Bottle", "potion")]
    [InlineData("Reagent", "reagent")]
    [InlineData("gold", "gold")]
    [InlineData("N / A", "misc")]
    [InlineData("0", "misc")]
    public void An_items_equipment_word_becomes_the_kind_the_treasure_rules_compare(string equipStat, string kind) =>
        Assert.Equal(kind, ItemVocabulary.KindOf(equipStat));

    [Theory]
    [InlineData("Sword", "sword")]
    [InlineData("Chain", "chain")]
    [InlineData("Blaster", "blaster")]
    [InlineData("Misc", "misc")]
    [InlineData("0", "misc")]
    public void An_items_skill_word_becomes_the_skill_the_treasure_rules_compare(string skillGroup, string skill) =>
        Assert.Equal(skill, ItemVocabulary.SkillOf(skillGroup));

    [Fact]
    public void The_two_vocabularies_agree_so_a_request_finds_what_it_asked_for()
    {
        // A request for a cloak and an item the table calls a cloak must be one tag, or the request would
        // find nothing: stating the two maps in one place is what keeps them from drifting apart.
        foreach (string word in new[] { "Cape", "Helm", "Shield", "Ring", "Amulet", "Boots", "Belt", "Gauntlets", "Wand", "Gem" })
        {
            (string kind, string skill) = ItemVocabulary.FilterOf(word);
            Assert.NotEqual(string.Empty, kind);
            Assert.Equal(string.Empty, skill);
            Assert.Contains(word, new[] { "Cape", "Helm", "Shield", "Ring", "Amulet", "Boots", "Belt", "Gauntlets", "Wand", "Gem" });
        }

        // A request by skill is answered by the item's own skill word, which is the other half of the same
        // table: a sword asks by sword, and the item table spells a sword's skill the same way.
        Assert.Equal(("", "sword"), ItemVocabulary.FilterOf("SWORD"));
        Assert.Equal("sword", ItemVocabulary.SkillOf("Sword"));
        Assert.Equal(("single-handed", ""), ItemVocabulary.FilterOf("weapon"));
        Assert.Equal("single-handed", ItemVocabulary.KindOf("Weapon"));
        Assert.Equal(("", "misc"), ItemVocabulary.FilterOf("Misc"));
        Assert.Equal("misc", ItemVocabulary.SkillOf("Misc"));
    }

    [Fact]
    public void The_random_item_table_is_read_as_its_first_section_and_no_further()
    {
        // The shipped file weighs 618 items and then holds the enchantment chances below them. A reader that
        // walked past the section boundary would read a table name as an item id, so the boundary is what this
        // case is about as much as the weights are.
        string installRoot = SyntheticInstallation.Create();
        try
        {
            RandomItemsTable table = RandomItemsTable.Read(LodInstall.Open(installRoot));

            Assert.Equal(RandomItemsTable.ExpectedRows, table.Rows.Count);
            Assert.Equal(RandomItemsTable.ExpectedRows, table.DrawnCount);
            Assert.Equal(1, table.Rows[0].Id);
            Assert.Equal(618, table.Rows[^1].Id);

            // The fixture's first item weighs at every level, which is what a level's pool is drawn from.
            RandomItemRow first = table.Rows[0];
            for (int level = 1; level <= RandomItemsTable.Levels; level++) Assert.Equal(5, first.ChanceAt(level));
            Assert.Equal(0, first.ChanceAt(0));
            Assert.Equal(0, first.ChanceAt(RandomItemsTable.Levels + 1));

            // The second section's rows are kept as rows the table carries rather than read as items.
            Assert.Contains(table.AnnotationRows, row => row.Field(0).StartsWith("Bonus chance", StringComparison.Ordinal));
            Assert.Contains(table.AnnotationRows, row => row.Field(0) == "Weapons");
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void A_written_pack_carries_the_treasure_rules_the_product_draws_from()
    {
        // The contract this suite exists for: what the importer read is what the pack states, field for field,
        // so the ruleset's reading is checked against the data rather than against a summary of it.
        string installRoot = SyntheticInstallation.Create();
        string root = Path.Combine(Path.GetTempPath(), $"mm7-treasure-{Guid.NewGuid():N}");
        try
        {
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), root, PackWriter.MapDetail.None);
            Assert.Contains("mm7-tables", written.PackIds);

            using JsonDocument monsters = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mm7-tables", "monsters.json")));
            JsonElement rows = monsters.RootElement.GetProperty("entries");
            Assert.Equal(276, rows.GetArrayLength());

            // Every row states a parsed cell beside the cell it came from, and the four shapes the fixture
            // writes are all there: nothing, coin alone, a chance with coin and a kind, and a certainty.
            JsonElement nothing = Row(rows, 4);
            Assert.Equal("0", nothing.GetProperty("treasure").GetString());
            Assert.Equal(0, nothing.GetProperty("treasureRoll").GetProperty("chance").GetInt32());
            Assert.Equal(0, nothing.GetProperty("treasureRoll").GetProperty("level").GetInt32());

            JsonElement coin = Row(rows, 5);
            Assert.Equal(2, coin.GetProperty("treasureRoll").GetProperty("goldRolls").GetInt32());
            Assert.Equal(6, coin.GetProperty("treasureRoll").GetProperty("goldSides").GetInt32());
            Assert.False(coin.GetProperty("treasureRoll").TryGetProperty("kind", out _));

            JsonElement item = Row(rows, 6);
            Assert.Equal(10, item.GetProperty("treasureRoll").GetProperty("chance").GetInt32());
            Assert.Equal(3, item.GetProperty("treasureRoll").GetProperty("level").GetInt32());
            Assert.Equal("sword", item.GetProperty("treasureRoll").GetProperty("skill").GetString());

            JsonElement certain = Row(rows, 7);
            Assert.Equal(100, certain.GetProperty("treasureRoll").GetProperty("chance").GetInt32());
            Assert.Equal(1, certain.GetProperty("treasureRoll").GetProperty("level").GetInt32());
            Assert.Equal("cloak", certain.GetProperty("treasureRoll").GetProperty("kind").GetString());

            using JsonDocument items = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mm7-tables", "items.json")));
            JsonElement itemRows = items.RootElement.GetProperty("entries");
            Assert.Equal(800, itemRows.GetArrayLength());

            // The first weighed item carries the six weights its row of the random-item table states, and the
            // tags its equipment and skill words become; the placeholder item zero weighs nothing and says so
            // by carrying no weights at all.
            JsonElement sword = itemRows[1];
            Assert.Equal("sword", sword.GetProperty("skill").GetString());
            Assert.Equal("single-handed", sword.GetProperty("type").GetString());

            // A spell book's own reference column names the spell it teaches, and the importer writes that
            // join out as a field rather than leaving the shipped spelling for a reader to parse:
            // OpenEnroth src/Engine/Objects/ItemEnumFunctions.cpp:282, spellForSpellbook.
            JsonElement book = itemRows[400];
            Assert.Equal("Book", book.GetProperty("equipStat").GetString());
            Assert.Equal("book", book.GetProperty("type").GetString());
            Assert.Equal("S2", book.GetProperty("damageDice").GetString());
            Assert.Equal("2", book.GetProperty("spell").GetString());

            // A row that is not a book of a spell the table declares carries no such field at all.
            Assert.False(sword.TryGetProperty("spell", out _));
            Assert.Equal([5, 5, 5, 5, 5, 5], [.. sword.GetProperty("lootWeights").EnumerateArray().Select(weight => weight.GetInt32())]);
            Assert.False(itemRows[0].TryGetProperty("lootWeights", out _));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_written_container_carries_the_places_own_danger_level()
    {
        // A container's random references are remapped through the place's own treasure level, and a ruleset
        // never sees a place's fields: the number travels with the container that needs it.
        string installRoot = SyntheticInstallation.Create(withMaps: true, withContainers: true);
        try
        {
            IReadOnlyDictionary<int, PlaceMapNumbers> places = PlaceMapNumbersTable.Read(Mm7Tables.Read(LodInstall.Open(installRoot)));
            Assert.All(places.Values, place => Assert.InRange(place.TreasureLevel, 0, 6));
            Assert.Contains(places.Values, place => place.TreasureLevel > 0);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    private static JsonElement Row(JsonElement entries, int id) =>
        entries.EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == id.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
