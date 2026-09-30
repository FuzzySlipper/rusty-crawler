using System.Globalization;
using System.Text.RegularExpressions;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>A count of dice, their sides, and what is added: <c>2D8+10</c>.</summary>
/// <param name="Count">How many dice are rolled.</param>
/// <param name="Sides">How many sides each has.</param>
/// <param name="Bonus">What is added to the roll.</param>
public readonly record struct MonsterDice(int Count, int Sides, int Bonus);

/// <summary>One of a monster's attacks, as its row states it.</summary>
/// <param name="Kind">The kind of harm, in the table's own word (<c>Phys</c>, <c>Fire</c>, <c>Ener</c>).</param>
/// <param name="Dice">The damage dice, or null when the cell states none.</param>
/// <param name="Missile">What the attack throws, in the table's own word, or empty when it is a blow.</param>
public readonly record struct MonsterAttackCell(string Kind, MonsterDice? Dice, string Missile);

/// <summary>A spell a monster casts, as its row states it.</summary>
/// <param name="Chance">How often it casts it, in percent.</param>
/// <param name="Name">The spell's name, as the spell table names it.</param>
/// <param name="Mastery">The rung it casts at, in the table's own letter (N, E, M, G).</param>
/// <param name="Skill">The skill level it casts at.</param>
public readonly record struct MonsterSpellCell(int Chance, string Name, string Mastery, int Skill);

/// <summary>What a monster's blow leaves besides harm, as its row states it.</summary>
/// <param name="Kind">The table's own word for it, with any strength digit and count removed.</param>
/// <param name="Strength">The strength the word carries (<c>Poison3</c> is 3), or zero when it carries none.</param>
/// <param name="Times">How many times it applies (<c>Stealx2</c> is 2), which is one unless the cell says more.</param>
public readonly record struct MonsterSpecialAttackCell(string Kind, int Strength, int Times);

/// <summary>Everything a monster's row states about how it fights, typed from the table's own cells.</summary>
/// <param name="Attack">Its first attack.</param>
/// <param name="SecondAttack">Its second attack.</param>
/// <param name="SecondAttackChance">How often it uses its second attack, in percent.</param>
/// <param name="FirstSpell">Its first spell, or null when it casts none.</param>
/// <param name="SecondSpell">Its second spell, or null when it casts none.</param>
/// <param name="Resistances">Its resistance to each kind of harm the table states one for, where it is not immune.</param>
/// <param name="Immunities">The kinds of harm it is immune to.</param>
/// <param name="SpecialAttack">What its blow leaves besides harm, or null when it leaves nothing.</param>
/// <param name="HostilityKind">The kind the hostility matrix places it in, which three rows share.</param>
public sealed record MonsterCombatRecord(
    MonsterAttackCell Attack,
    MonsterAttackCell SecondAttack,
    int SecondAttackChance,
    MonsterSpellCell? FirstSpell,
    MonsterSpellCell? SecondSpell,
    IReadOnlyDictionary<string, int> Resistances,
    IReadOnlyList<string> Immunities,
    MonsterSpecialAttackCell? SpecialAttack,
    int HostilityKind);

/// <summary>Reads how a monster fights out of the monster table's own cells.</summary>
/// <remarks>
/// <para>
/// The cells are at the positions the table's header names and the donor reads them at
/// (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:534-555</c>): 16 the special attack, 17 to 19 the first
/// attack's kind, dice and missile, 20 to 23 the second attack's chance, kind, dice and missile, 24 to 27 the
/// two spells' chances and cells, and 28 to 37 the ten resistances. Knowing those positions and the syntax of
/// each cell is what this importer is for, so a reader downstream gets named, typed fields and none of it.
/// </para>
/// <para>
/// <b>The special attack is read from the forms the shipped table carries.</b> Every non-empty cell in the
/// release is a word, optionally followed by a strength digit (<c>Poison3</c>, <c>Disease1</c>) and optionally
/// by a count (<c>Stealx2</c>, <c>Poison3x2</c>); that shape is what is read, and a cell of another shape is
/// refused by name rather than guessed at. What each word means is the ruleset's.
/// </para>
/// </remarks>
public static partial class MonsterCombat
{
    private const int SpecialAttackColumn = 16;
    private const int AttackKindColumn = 17;
    private const int AttackDiceColumn = 18;
    private const int AttackMissileColumn = 19;
    private const int SecondAttackChanceColumn = 20;
    private const int SecondAttackKindColumn = 21;
    private const int SecondAttackDiceColumn = 22;
    private const int SecondAttackMissileColumn = 23;
    private const int FirstSpellChanceColumn = 24;
    private const int FirstSpellColumn = 25;
    private const int SecondSpellChanceColumn = 26;
    private const int SecondSpellColumn = 27;

    /// <summary>The ten resistance columns and the kind of harm each is about, in the table's own words.</summary>
    private static readonly (string Kind, int Column)[] ResistanceColumns =
    [
        ("Fire", 28), ("Air", 29), ("Water", 30), ("Earth", 31), ("Mind", 32),
        ("Spirit", 33), ("Body", 34), ("Light", 35), ("Dark", 36), ("Phys", 37),
    ];

    /// <summary>How many cells a row must carry for every column read here to exist.</summary>
    public const int Columns = 38;

    /// <summary>The table's word for a resistance a creature cannot be harmed through at all.</summary>
    private const string Immune = "Imm";

    /// <summary>Reads one row's combat cells.</summary>
    /// <param name="source">Which table the row came from, for the message a refusal carries.</param>
    /// <param name="id">The row's own monster id.</param>
    /// <param name="name">The row's own monster name.</param>
    /// <param name="cells">The row's cells, as the table stored them.</param>
    /// <returns>What the row states about how the creature fights.</returns>
    /// <exception cref="LodFormatException">A cell does not have the shape its column holds.</exception>
    public static MonsterCombatRecord Read(string source, int id, string name, IReadOnlyList<string> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        string row = $"{source}: monster {id} '{name}'";
        if (cells.Count < Columns)
        {
            throw new LodFormatException(LodFault.Count, $"{row} carries {cells.Count} cells where its combat columns need {Columns}.");
        }

        string Cell(int column) => cells[column].Trim();

        Dictionary<string, int> resistances = new(StringComparer.Ordinal);
        List<string> immunities = [];
        foreach ((string kind, int column) in ResistanceColumns)
        {
            string cell = Cell(column);
            if (string.Equals(cell, Immune, StringComparison.OrdinalIgnoreCase)) immunities.Add(kind);
            else resistances[kind] = Integer(row, cell, $"its {kind} resistance");
        }

        return new MonsterCombatRecord(
            new MonsterAttackCell(Cell(AttackKindColumn), Dice(row, Cell(AttackDiceColumn), "its attack's damage"), Missile(Cell(AttackMissileColumn))),
            new MonsterAttackCell(Cell(SecondAttackKindColumn) is "0" ? string.Empty : Cell(SecondAttackKindColumn), Dice(row, Cell(SecondAttackDiceColumn), "its second attack's damage"), Missile(Cell(SecondAttackMissileColumn))),
            Integer(row, Cell(SecondAttackChanceColumn), "its second attack's chance"),
            Spell(row, Cell(FirstSpellColumn), Cell(FirstSpellChanceColumn)),
            Spell(row, Cell(SecondSpellColumn), Cell(SecondSpellChanceColumn)),
            resistances,
            immunities,
            Special(row, Cell(SpecialAttackColumn)),
            // Three rows — a creature's three grades — share one column of the hostility matrix, counted from
            // one after the party's own (OpenEnroth src/Engine/Objects/MonsterEnumFunctions.h:38-40).
            id <= 0 ? 0 : ((id - 1) / 3) + 1);
    }

    private static int Integer(string row, string cell, string what) =>
        int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new LodFormatException(LodFault.Value, $"{row} states '{cell}' for {what}, which is not a number.");

    private static string Missile(string cell) => cell is "" or "0" ? string.Empty : cell;

    private static MonsterDice? Dice(string row, string cell, string what)
    {
        if (cell is "" or "0") return null;
        Match match = DicePattern().Match(cell);
        if (!match.Success)
        {
            throw new LodFormatException(LodFault.Value, $"{row} states '{cell}' for {what}, which is not dice (a count, D, sides, and an optional +bonus).");
        }

        return new MonsterDice(
            int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["sides"].Value, CultureInfo.InvariantCulture),
            match.Groups["bonus"].Success ? int.Parse(match.Groups["bonus"].Value, CultureInfo.InvariantCulture) : 0);
    }

    private static MonsterSpellCell? Spell(string row, string cell, string chance)
    {
        if (cell is "" or "0") return null;
        string[] parts = cell.Split(',');

        // Ninety-two of the release's spell cells are a name, a rung letter and a skill; one writes the letter
        // and the skill together without the comma between them (monster 69, 'Lightning Bolt,M10'), and is read
        // as the same two things.
        if (parts.Length == 2 && RungAndSkillPattern().Match(parts[1].Trim()) is { Success: true } joined)
        {
            parts = [parts[0], joined.Groups["rung"].Value, joined.Groups["skill"].Value];
        }

        if (parts.Length != 3 || parts[0].Trim().Length == 0)
        {
            throw new LodFormatException(LodFault.Value, $"{row} states '{cell}' for a spell, which is not a name, a rung letter, and a skill.");
        }

        return new MonsterSpellCell(
            Integer(row, chance, "a spell's chance"),
            parts[0].Trim(),
            parts[1].Trim(),
            Integer(row, parts[2].Trim(), "a spell's skill"));
    }

    private static MonsterSpecialAttackCell? Special(string row, string cell)
    {
        if (cell is "" or "0") return null;
        Match match = SpecialPattern().Match(cell);
        if (!match.Success)
        {
            throw new LodFormatException(LodFault.Value, $"{row} states '{cell}' for its special attack, which is not a word with an optional strength and count.");
        }

        return new MonsterSpecialAttackCell(
            match.Groups["word"].Value,
            match.Groups["strength"].Success ? int.Parse(match.Groups["strength"].Value, CultureInfo.InvariantCulture) : 0,
            match.Groups["times"].Success ? int.Parse(match.Groups["times"].Value, CultureInfo.InvariantCulture) : 1);
    }

    [GeneratedRegex(@"^(?<count>\d+)[dD](?<sides>\d+)(?:\+(?<bonus>\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex DicePattern();

    [GeneratedRegex(@"^(?<word>[A-Za-z]+?)(?<strength>\d)?(?:x(?<times>\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex SpecialPattern();

    [GeneratedRegex(@"^(?<rung>[NnEeMmGg])(?<skill>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex RungAndSkillPattern();
}
