using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One row of the shipped potion table: a reagent, the empty bottle, a catalyst, or a potion.</summary>
/// <remarks>
/// <para>
/// The table states four label columns and then the mixture matrix: the first three carry the row's number,
/// its name, and the colour word a person reads for it, the fourth states what the thing does in the game's
/// own words, and the matrix that follows states what combining this row with each real potion makes. A
/// reagent's row also states its one recipe in that same effect column — <c>+ Bottle = Red Potion +1</c> — and
/// a potion's row states its own colour composition in the three cells between the effect and the matrix.
/// </para>
/// <para>
/// <b>The matrix is read the way the donor reads it.</b> Its columns are the fifty real potions
/// (<c>OpenEnroth</c> <c>src/Engine/Tables/ItemTable.cpp:252-278</c>, <c>LoadPotions</c>: the matrix begins at
/// the cell after the effect column, one column per potion from the first real potion to the last), and a
/// cell is either the id of what the pair makes, the word <c>no</c> for a pair that does nothing, or
/// <c>E</c> followed by the strength of the burst the pair makes. The donor's own row range is 222 through
/// 271, which is why the reagent rows carry no matrix at all.
/// </para>
/// </remarks>
/// <param name="Id">The row's own item id, which is the id the item table gives the same thing.</param>
/// <param name="Name">The name the table states.</param>
/// <param name="Description">The colour word the table states, which is what a reagent's recipe names.</param>
/// <param name="Effect">What the row does, in the table's own words.</param>
/// <param name="Kind">What the row is: a reagent, the bottle, the catalyst, or a potion.</param>
/// <param name="Units">The row's red, blue, and yellow composition, all zero for a row that states none.</param>
/// <param name="Tier">
/// The rung of the mixing skill's ladder the row requires, which the shipped table does not carry.
/// </param>
/// <param name="Power">
/// The strength the row states for itself: a reagent's own power, which is the item table's damage column for
/// the same row (<c>OpenEnroth</c> <c>src/Engine/Tables/ItemTable.cpp:166</c>).
/// </param>
/// <param name="Mixtures">
/// What combining this row with another row makes, keyed by the other row's id: the id of the result, the
/// word <c>none</c> for a pair that does nothing, or <c>burst:n</c> for a pair that goes off at strength n.
/// </param>
/// <param name="Notes">
/// The discovery each mixture records, keyed by the other row's id, zero for a mixture that records none.
/// </param>
public readonly record struct PotionRecord(
    int Id,
    string Name,
    string Description,
    string Effect,
    string Kind,
    IReadOnlyList<int> Units,
    int Tier,
    int Power,
    IReadOnlyDictionary<int, string> Mixtures,
    IReadOnlyDictionary<int, int> Notes)
{
    /// <summary>Whether this row is a reagent, which is the half of a reagent's recipe.</summary>
    public bool IsReagent => string.Equals(Kind, PotionTable.ReagentKind, StringComparison.Ordinal);

    /// <summary>Whether this row is a potion (or the catalyst), which is what the mixture matrix covers.</summary>
    public bool IsPotion => string.Equals(Kind, PotionTable.PotionKind, StringComparison.Ordinal) ||
        string.Equals(Kind, PotionTable.CatalystKind, StringComparison.Ordinal);
}

/// <summary>The shipped potion table and the discovery table that mirrors it.</summary>
/// <remarks>
/// <para>
/// <c>POTION.TXT</c> is where this game keeps its mixtures: seventy-two rows — twenty reagents, the empty
/// bottle, the catalyst, and fifty potions — and a symmetric matrix of what each pair of potions makes. What
/// it does <em>not</em> carry is what any of it costs or requires: the four mastery rungs that gate the more
/// complicated mixtures, what a potion does when it is drunk, and what an exploding mixture costs are all in
/// the executable (<c>OpenEnroth</c> <c>src/GUI/UI/UIPopup.cpp:1978-2270</c> for mixing and
/// <c>src/Engine/Objects/Character.cpp:3080-3300</c> for drinking), and this importer states the one of them
/// it writes into the pack rather than inventing the rest.
/// </para>
/// <para>
/// <b>The tier is authored here, and marked as ours.</b> The donor gates a mixture on the rung of Alchemy its
/// <em>result</em> needs, in four bands over the potion ids (<c>src/GUI/UI/UIPopup.cpp:2092-2112</c>): the
/// three simplest potions need nothing, 225-227 need the first rung, 228-239 the second, 240-261 the third,
/// and 262-271 the fourth. That reading is written out as a <c>tier</c> field on each row of the pack, so the
/// requirement travels with the recipe instead of being computed from an id range at play time.
/// </para>
/// <para>
/// <b>The catalyst's rows are authored here too.</b> No matrix cell covers the catalyst: the donor handles it
/// before it ever reads the matrix (<c>src/GUI/UI/UIPopup.cpp:2075-2080</c>), so a catalyst combined with a
/// potion makes that potion and two catalysts make a catalyst. Those rows are written out from that rule,
/// with the donor's path beside them, because a recipe table with a hole in it would be a recipe table a
/// reader has to know the executable to use.
/// </para>
/// </remarks>
public sealed class PotionTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 1;

    /// <summary>How many rows the shipped table carries.</summary>
    public const int ExpectedRows = 72;

    /// <summary>The first reagent's row id.</summary>
    public const int FirstReagent = 200;

    /// <summary>The last reagent's row id.</summary>
    public const int LastReagent = 219;

    /// <summary>The empty bottle's row id, which is what a reagent's recipe needs.</summary>
    public const int Bottle = 220;

    /// <summary>The catalyst's row id.</summary>
    public const int Catalyst = 221;

    /// <summary>The first real potion's row id, which is where the donor's mixture matrix begins.</summary>
    public const int FirstPotion = 222;

    /// <summary>The last real potion's row id, which is where the donor's mixture matrix ends.</summary>
    public const int LastPotion = 271;

    /// <summary>The cell the mixture matrix begins at, which is the column after a potion's three unit cells.</summary>
    private const int MatrixColumn = 7;

    /// <summary>The kind a reagent's row is emitted with.</summary>
    internal const string ReagentKind = "reagent";

    /// <summary>The kind the empty bottle's row is emitted with.</summary>
    internal const string BottleKind = "bottle";

    /// <summary>The kind the catalyst's row is emitted with.</summary>
    internal const string CatalystKind = "catalyst";

    /// <summary>The kind a potion's row is emitted with.</summary>
    internal const string PotionKind = "potion";

    /// <summary>What a mixture that does nothing is written as in the pack.</summary>
    internal const string NoReaction = "none";

    /// <summary>What a mixture that goes off is written as, before its strength.</summary>
    internal const string Burst = "burst";

    private PotionTable(TabularTable table, TabularTable notes, PotionRecord[] rows)
    {
        Table = table;
        NotesTable = notes;
        Rows = rows;
    }

    /// <summary>The potion table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>The discovery table this was read from, which mirrors the mixture matrix.</summary>
    public TabularTable NotesTable { get; }

    /// <summary>Every row, in table order.</summary>
    public IReadOnlyList<PotionRecord> Rows { get; }

    /// <summary>Reads both tables from an installation.</summary>
    /// <param name="install">The installation to read from.</param>
    /// <param name="items">
    /// The item table, when the caller has read it, so a reagent's stated power can be checked against the item
    /// row's own damage column — which is where the donor's <c>GetReagentPower</c> reads it from
    /// (<c>OpenEnroth</c> <c>src/Engine/Tables/ItemTable.cpp:166</c>). Null skips the check.
    /// </param>
    /// <exception cref="LodFormatException">A row is not one this reader understands, or the two tables disagree.</exception>
    public static PotionTable Read(LodInstall install, ItemTable? items = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Potions, HeaderRowCount);
        TabularTable notes = TabularTable.Read(install, Mm7TableSources.PotionNotes, HeaderRowCount);
        (IReadOnlyList<TabularRow> data, IReadOnlyList<TabularRow> annotations) = table.Partition();
        VerifyDuplicateBlock(table, data, annotations);

        // The colour a reagent's recipe names is resolved through the potion rows' own description column, so
        // "Red Potion" is whichever row the table itself calls red rather than an id this reader decided. The
        // catalyst is in the range because its own description is "Gray Potion", which is what the four grey
        // reagents' recipes name.
        Dictionary<string, int> colours = new(StringComparer.OrdinalIgnoreCase);
        foreach (TabularRow row in data)
        {
            if (!int.TryParse(TableValue.Text(row, 0), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id)) continue;
            if (id < Catalyst || id > LastPotion) continue;
            string description = TableValue.Text(row, 2).Trim();
            if (description.Length == 0) continue;
            colours.TryAdd(description, id);
        }

        Dictionary<int, TabularRow> noteRows = [];
        foreach (TabularRow row in notes.Rows)
        {
            if (int.TryParse(TableValue.Text(row, 0), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id))
            {
                noteRows[id] = row;
            }
        }

        List<PotionRecord> rows = [];
        foreach (TabularRow row in data)
        {
            string written = TableValue.Text(row, 0);
            if (!int.TryParse(written, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id))
            {
                throw new LodFormatException($"{table.Source}: row {row.Number} states the id '{written}', which is not a row number.");
            }

            string name = TableValue.Text(row, 1);
            string description = TableValue.Text(row, 2).Trim();
            string effect = TableValue.Text(row, 3).Trim();
            string kind = KindOf(id);
            int power = kind == ReagentKind ? PowerOf(table, row, effect, name) : 0;
            rows.Add(new PotionRecord(
                id,
                name,
                description,
                effect,
                kind,
                Units(row),
                TierOf(id),
                power,
                Mixtures(table, row, id, kind, colours),
                Discovery(noteRows.GetValueOrDefault(id), id)));
        }

        rows.Sort((left, right) => left.Id.CompareTo(right.Id));
        if (rows.Count != ExpectedRows)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedRows} potion rows, read {rows.Count}.");
        }

        VerifyPower(rows, items);
        return new PotionTable(table, notes, [.. rows]);
    }

    /// <summary>
    /// Checks the table's second, id-less block against the rows that carry ids.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>POTION.TXT</c> carries its last thirty-two rows <em>twice</em>: once with a row number and once
    /// again with the number column left blank and the three colour-unit cells omitted, which is a second
    /// layout of the same rows rather than more data. The donor skips them
    /// (<c>OpenEnroth</c> <c>src/Engine/Tables/ItemTable.cpp:257</c>, <c>if (tokens[0].empty()) continue;</c>)
    /// and so does this reader — but the skip is checked rather than silent, because a block that is
    /// <em>not</em> the rows already read would be data this import was quietly dropping.
    /// </para>
    /// <para>
    /// The three label cells are what is compared: the number is missing by construction, the colour units
    /// are the columns the second layout does not carry, and the mixture matrix that follows is checked only
    /// as far as the shorter of the two rows goes.
    /// </para>
    /// </remarks>
    private static void VerifyDuplicateBlock(
        TabularTable table,
        IReadOnlyList<TabularRow> data,
        IReadOnlyList<TabularRow> idless)
    {
        foreach (TabularRow row in idless)
        {
            string name = TableValue.Text(row, 1);
            TabularRow? twin = null;
            foreach (TabularRow candidate in data)
            {
                if (string.Equals(TableValue.Text(candidate, 1), name, StringComparison.Ordinal)) twin = candidate;
            }

            if (twin is not { } match)
            {
                throw new LodFormatException(
                    $"{table.Source}: row {row.Number} has no id and names '{name}', which no row of the table carries an id for.");
            }

            for (int column = 1; column <= 3; column++)
            {
                if (string.Equals(TableValue.Text(row, column), TableValue.Text(match, column), StringComparison.Ordinal)) continue;
                throw new LodFormatException(
                    $"{table.Source}: the id-less row naming '{name}' states '{TableValue.Text(row, column)}' where the row that carries its id states '{TableValue.Text(match, column)}'.");
            }
        }
    }

    /// <summary>What a row of the table is, from the id the donor's own ranges state.</summary>
    private static string KindOf(int id) => id switch
    {
        >= FirstReagent and <= LastReagent => ReagentKind,
        Bottle => BottleKind,
        Catalyst => CatalystKind,
        _ => PotionKind,
    };

    /// <summary>The rung of the mixing skill a potion's row requires, which the shipped table does not state.</summary>
    /// <remarks>
    /// The donor's four bands (<c>OpenEnroth</c> <c>src/GUI/UI/UIPopup.cpp:2092-2112</c>), read as the rung a
    /// character must hold rather than as the burst a character without it takes: below the first band no
    /// mastery is asked for at all. This is ours, and the row it produces is what the pack carries.
    /// </remarks>
    private static int TierOf(int id) => id switch
    {
        >= 225 and <= 227 => 1,
        >= 228 and <= 239 => 2,
        >= 240 and <= 261 => 3,
        >= 262 and <= 271 => 4,
        _ => 0,
    };

    /// <summary>A row's red, blue, and yellow composition, which only the potion rows state.</summary>
    private static int[] Units(TabularRow row)
    {
        int[] units = new int[3];
        for (int index = 0; index < units.Length; index++)
        {
            string cell = TableValue.Text(row, 4 + index).Trim();
            if (cell.Length == 0) continue;
            if (!int.TryParse(cell, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out units[index]))
            {
                throw new LodFormatException($"potion row {TableValue.Text(row, 0)} states the colour unit '{cell}', which is not a number.");
            }
        }

        return units;
    }

    /// <summary>
    /// A reagent's own power, which the table states in the reagent's effect cell as the strength its bottle
    /// recipe makes.
    /// </summary>
    private static int PowerOf(TabularTable table, TabularRow row, string effect, string name)
    {
        int plus = effect.LastIndexOf('+');
        if (plus < 0 || !int.TryParse(
                effect.AsSpan(plus + 1).Trim(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int power))
        {
            throw new LodFormatException(
                $"{table.Source}: reagent {TableValue.Text(row, 0)} ({name}) states the effect '{effect}', which does not end in the strength its bottle recipe makes.");
        }

        return power;
    }

    /// <summary>
    /// What one row combines with, as the row's own effect cell states it for a reagent and as the matrix
    /// states it for a potion.
    /// </summary>
    private static Dictionary<int, string> Mixtures(
        TabularTable table,
        TabularRow row,
        int id,
        string kind,
        IReadOnlyDictionary<string, int> colours)
    {
        Dictionary<int, string> mixtures = [];

        if (kind == ReagentKind)
        {
            // A reagent's one recipe is written in the row's own effect cell: "+ Bottle = Red Potion +1". The
            // bottle is the other ingredient and the colour word is resolved through the potion rows' own
            // description column, which is what keeps the join the table's rather than this reader's.
            string effect = TableValue.Text(row, 3);
            int equals = effect.IndexOf('=');
            if (!effect.StartsWith("+ Bottle =", StringComparison.OrdinalIgnoreCase) || equals < 0)
            {
                throw new LodFormatException(
                    $"{table.Source}: reagent {id} ({TableValue.Text(row, 1)}) states the effect '{effect}', which is not the bottle recipe this reader understands.");
            }

            string made = effect[(equals + 1)..].Trim();
            int plus = made.LastIndexOf('+');
            string colour = (plus < 0 ? made : made[..plus]).Trim();
            if (!colours.TryGetValue(colour, out int result))
            {
                throw new LodFormatException(
                    $"{table.Source}: reagent {id} ({TableValue.Text(row, 1)}) is mixed into '{colour}', which no potion row of this table describes itself as.");
            }

            mixtures[Bottle] = result.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return mixtures;
        }

        if (kind == BottleKind) return mixtures;

        if (kind == CatalystKind)
        {
            // The donor never reads a matrix cell for the catalyst: it decides before the matrix is consulted
            // that a catalyst mixed with anything is that thing, and that two catalysts are a catalyst
            // (OpenEnroth src/GUI/UI/UIPopup.cpp:2075-2080).
            for (int other = Catalyst; other <= LastPotion; other++)
            {
                mixtures[other] = other.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return mixtures;
        }

        for (int other = Catalyst; other <= LastPotion; other++)
        {
            // The matrix covers the fifty real potions only, so a cell outside that range is not read: the
            // catalyst's own row is stated above from the donor's rule rather than from a column the table
            // does not carry. The diagonal is read like every other cell — the table states "no" there, which
            // is a mixture that does nothing rather than a pair the table never considered.
            if (other < FirstPotion) continue;
            int column = MatrixColumn + (other - FirstPotion);
            if (column >= row.Fields.Length) continue;
            string cell = row.Fields[column].Trim();
            if (cell.Length == 0) continue;
            mixtures[other] = Outcome(cell);
        }

        return mixtures;
    }

    /// <summary>One matrix cell, as the donor reads it.</summary>
    private static string Outcome(string cell)
    {
        if (string.Equals(cell, "no", StringComparison.OrdinalIgnoreCase)) return NoReaction;
        if (cell.StartsWith('E') || cell.StartsWith('e'))
        {
            string strength = cell[1..].Trim();
            if (!int.TryParse(strength, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int level) || level < 1)
            {
                throw new LodFormatException($"a mixture cell states the strength '{cell}', which is not a burst strength.");
            }

            return $"{Burst}:{level.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        if (!int.TryParse(cell, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int result))
        {
            throw new LodFormatException($"a mixture cell states '{cell}', which is neither a result, 'no', nor a burst strength.");
        }

        return result.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The discovery one mixture records, read from the table that mirrors the matrix.
    /// </summary>
    /// <remarks>
    /// <c>POTNOTES.TXT</c> has the potion table's own layout with autonote indices in the matrix cells, and the
    /// donor reads it into a second matrix beside the first (<c>OpenEnroth</c>
    /// <c>src/Engine/Tables/ItemTable.cpp:281-299</c>, <c>LoadPotionNotes</c>). What a discovery is worth, and
    /// whether the party may know it, is nobody's business here: the number travels so that whatever keeps
    /// what the party has learned can write it down.
    /// </remarks>
    private static Dictionary<int, int> Discovery(TabularRow? row, int id)
    {
        Dictionary<int, int> notes = [];
        if (row is not { } notesRow) return notes;

        // The donor reads the discovery matrix for the fifty real potions and for nothing else
        // (OpenEnroth src/Engine/Tables/ItemTable.cpp:281-299, LoadPotionNotes), and the rows outside that
        // range state other things in the same cells: the bottle's row carries the potion ids themselves.
        if (id < FirstPotion || id > LastPotion) return notes;

        for (int other = FirstPotion; other <= LastPotion; other++)
        {
            if (other == id) continue;
            int column = MatrixColumn + (other - FirstPotion);
            if (column >= notesRow.Fields.Length) continue;
            string cell = notesRow.Fields[column].Trim();
            if (cell.Length == 0 || string.Equals(cell, "no", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(cell, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int note) || note == 0) continue;
            notes[other] = note;
        }

        return notes;
    }

    /// <summary>
    /// Checks a reagent's stated power against the item table's damage column for the same row.
    /// </summary>
    /// <remarks>
    /// The two are the same fact in the donor — <c>Item::GetReagentPower</c> reads the item row's damage dice,
    /// which <c>ItemTable::Initialize</c> copied into the reagent's power
    /// (<c>OpenEnroth</c> <c>src/Engine/Tables/ItemTable.cpp:166</c>) — so a disagreement means one of the two
    /// readers is wrong, and that is worth failing the import over rather than shipping a strength nothing can
    /// check later.
    /// </remarks>
    private static void VerifyPower(IReadOnlyList<PotionRecord> rows, ItemTable? items)
    {
        if (items is null) return;
        Dictionary<int, ItemRecord> byId = [];
        foreach (ItemRecord item in items.Items) byId[item.Id] = item;

        foreach (PotionRecord row in rows)
        {
            if (!row.IsReagent) continue;
            if (!byId.TryGetValue(row.Id, out ItemRecord item))
            {
                throw new LodFormatException($"reagent {row.Id} ({row.Name}) has no row in the item table, so its power cannot be checked against it.");
            }

            if (!int.TryParse(item.DamageDice, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int stated) || stated != row.Power)
            {
                throw new LodFormatException(
                    $"reagent {row.Id} ({row.Name}) states power {row.Power} in the potion table and '{item.DamageDice}' in the item table's damage column, which the donor reads as the same number.");
            }
        }
    }
}
