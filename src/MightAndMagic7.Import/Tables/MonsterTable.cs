using MightAndMagic7.Import.Lod;

using System.Globalization;

namespace MightAndMagic7.Import.Tables;

/// <summary>What one monster row drops, read out of its own treasure cell.</summary>
/// <remarks>
/// <para>
/// The shipped cell states four things in one string: how often in a hundred the creature drops anything, a
/// handful of coin dice, which treasure level its item comes from, and what kind of thing it asks that level
/// for. The donor reads all four out of the same cell (OpenEnroth
/// <c>src/Engine/Objects/Monsters.cpp:440-490</c>, the treasure-cell format and its parser), and this is that
/// reading done once, where the format is known, so the packs carry numbers rather than a string every
/// reader would have to spell again.
/// </para>
/// <para>
/// The four parts are independent and a cell may state only some of them: thirty-seven of the shipped rows
/// state a literal <c>0</c> and drop nothing, thirty-one state dice alone and drop coin, and the rest state
/// coin, a chance, a level, and often a kind. A cell that states a level without a chance drops its item
/// every time, which is the donor's own reading of a missing percent sign.
/// </para>
/// </remarks>
/// <param name="Chance">How often in a hundred the row drops anything, zero when it drops nothing.</param>
/// <param name="GoldRolls">How many dice of coin it drops, none when it drops no coin.</param>
/// <param name="GoldSides">How many sides each of those dice has.</param>
/// <param name="Level">Which treasure level its item comes from, zero when it drops no item.</param>
/// <param name="Kind">The tag of what it asks that level for, empty when it asks for anything.</param>
/// <param name="Skill">The skill tag of what it asks for, empty when it asks by kind alone.</param>
public readonly record struct MonsterTreasure(int Chance, int GoldRolls, int GoldSides, int Level, string Kind, string Skill)
{
    /// <summary>A creature that drops nothing at all, which is what a cell of <c>0</c> states.</summary>
    public static MonsterTreasure Nothing { get; } = new(0, 0, 0, 0, string.Empty, string.Empty);

    /// <summary>Whether this row drops anything: coin, an item, or both.</summary>
    public bool IsAnything => Chance > 0 || GoldRolls > 0;

    /// <summary>Whether the row asks a treasure level for an item.</summary>
    public bool WantsItem => Level > 0;
}

/// <summary>One monster row, with the combat columns this importer understands typed and the rest kept.</summary>
/// <param name="Id">The monster id.</param>
/// <param name="Name">The monster's name.</param>
/// <param name="Level">The monster's level.</param>
/// <param name="HitPoints">The monster's hit points.</param>
/// <param name="ArmorClass">The monster's armor class.</param>
/// <param name="Experience">Experience awarded for defeating it.</param>
/// <param name="Treasure">The treasure dice string the table carries, exactly as stored.</param>
/// <param name="TreasureRoll">What that string states, read into the numbers the product draws from.</param>
/// <param name="Fly">Whether the monster flies.</param>
/// <param name="Movement">The monster's movement class.</param>
/// <param name="AiType">The monster's AI class.</param>
/// <param name="Hostility">The monster's hostility value.</param>
/// <param name="Speed">The monster's speed.</param>
/// <param name="Recovery">The monster's recovery time.</param>
/// <param name="Fields">Every field of the row, for the columns this importer does not type yet.</param>
public readonly record struct MonsterRecord(
    int Id,
    string Name,
    int Level,
    int HitPoints,
    int ArmorClass,
    int Experience,
    string Treasure,
    MonsterTreasure TreasureRoll,
    string Fly,
    string Movement,
    string AiType,
    int Hostility,
    int Speed,
    int Recovery,
    IReadOnlyList<string> Fields);

/// <summary>The monster table.</summary>
public sealed class MonsterTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 3;

    /// <summary>How many monsters the shipped table carries.</summary>
    public const int ExpectedRows = 276;

    private MonsterTable(TabularTable table, MonsterRecord[] monsters, TabularRow[] annotations)
    {
        Table = table;
        Monsters = monsters;
        AnnotationRows = annotations;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every monster row, in table order.</summary>
    public IReadOnlyList<MonsterRecord> Monsters { get; }

    /// <summary>Rows the table writes without an id, which are legend text rather than monsters.</summary>
    public IReadOnlyList<TabularRow> AnnotationRows { get; }

    /// <summary>Reads the table from an installation.</summary>
    public static MonsterTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Monsters, HeaderRowCount);
        (IReadOnlyList<TabularRow> data, IReadOnlyList<TabularRow> annotations) = table.Partition();
        MonsterRecord[] monsters = [.. data.Select(row => new MonsterRecord(
            TableValue.Integer(table, row, 0, "#"),
            TableValue.Text(row, 1),
            TableValue.Integer(table, row, 3, "LVL"),
            TableValue.ThousandsInteger(table, row, 4, "HP"),
            TableValue.Integer(table, row, 5, "AC"),
            TableValue.ThousandsInteger(table, row, 6, "EXP"),
            TableValue.Text(row, 7),
            ReadTreasure(TableValue.Text(row, 7)),
            TableValue.Text(row, 9),
            TableValue.Text(row, 10),
            TableValue.Text(row, 11),
            TableValue.Integer(table, row, 12, "Hst"),
            TableValue.Integer(table, row, 13, "Spd"),
            TableValue.Integer(table, row, 14, "Rec"),
            row.Fields))];

        if (monsters.Length != ExpectedRows)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedRows} monsters, read {monsters.Length}.");
        }

        return new MonsterTable(table, monsters, [.. annotations]);
    }

    /// <summary>
    /// Reads what one treasure cell states: <c>[NN%][MdS][+][Llvl[ItemType]]</c>.
    /// </summary>
    /// <remarks>
    /// The donor's own readings, kept exactly: a cell of <c>0</c> is a creature that drops nothing; a cell
    /// with no percent sign drops whatever it states every time; dice are stated as a count, a <c>D</c>, and
    /// a side count, with an optional <c>+</c> before the level; and the level is one digit followed by the
    /// kind of thing wanted, which the vocabulary turns into the tags the product compares. A cell whose
    /// level digit is missing asks for no item, and a kind word the vocabulary does not know asks for
    /// anything at all — which is the donor's own answer for a word outside its map
    /// (<c>src/Engine/Objects/Monsters.cpp:492-495</c>).
    /// </remarks>
    /// <param name="cell">The cell exactly as the table stored it.</param>
    /// <returns>What it states.</returns>
    public static MonsterTreasure ReadTreasure(string cell)
    {
        string text = cell.Trim();
        if (text.Length == 0 || text == "0") return MonsterTreasure.Nothing;

        // The chance is everything before the percent sign; without one the cell states a certainty.
        int chance = 100;
        string rest = text;
        int percent = text.IndexOf('%', StringComparison.Ordinal);
        if (percent >= 0)
        {
            chance = int.TryParse(text[..percent], NumberStyles.None, CultureInfo.InvariantCulture, out int stated) ? stated : 0;
            rest = text[(percent + 1)..];
        }

        // The level is everything from the first 'L'; what precedes it is the coin dice.
        string dice = rest;
        string level = string.Empty;
        int at = rest.IndexOfAny(['l', 'L']);
        if (at >= 0)
        {
            dice = rest[..at];
            level = rest[(at + 1)..];
        }

        if (dice.EndsWith('+')) dice = dice[..^1];
        int rolls = 0;
        int sides = 0;
        int d = dice.IndexOfAny(['d', 'D']);
        if (d >= 0)
        {
            rolls = int.TryParse(dice[..d], NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : 0;
            sides = int.TryParse(dice[(d + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int faces) ? faces : 0;
        }

        int treasureLevel = 0;
        string kind = string.Empty;
        string skill = string.Empty;
        if (level.Length > 0 && char.IsAsciiDigit(level[0]))
        {
            treasureLevel = level[0] - '0';
            (kind, skill) = ItemVocabulary.FilterOf(level[1..]);
        }

        return new MonsterTreasure(chance, rolls, sides, treasureLevel, kind, skill);
    }
}
