using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One line of the history table: what the party's history book says about one event, and its page title.</summary>
/// <param name="Slot">
/// The history slot a map event writes, counting from zero: the event's variable names a slot and the donor reads
/// the table's row one past it (OpenEnroth <c>src/Engine/Objects/Character.cpp:3995-4002</c>, <c>historyLines[1 +
/// historyIndex(...)]</c>), so the slot is the row number less one.
/// </param>
/// <param name="Text">
/// The line as the table words it, with the table's own text codes written as named marks the ruleset fills
/// when the line is written: <c>{date}</c> for the day it happened and <c>{member:1}</c> to <c>{member:4}</c> for
/// the party's characters (see <see cref="HistoryTable"/>).
/// </param>
/// <param name="Title">The page title the table states beside it, or empty.</param>
public readonly record struct HistoryRecord(int Slot, string Text, string Title);

/// <summary>
/// The game's history table — the lines its history book holds — read from the rules archive.
/// </summary>
/// <remarks>
/// <para>
/// The table is a number, the line, a note column nothing reads and the page title (OpenEnroth
/// <c>src/Engine/Tables/HistoryTable.cpp:14-27</c>); the party keeps the time each slot was first written
/// (<c>src/Engine/Objects/Character.cpp:3995-4002</c>, <c>HistoryEventTimes</c>), which a map event sets
/// through the <c>history</c> variable. Row zero is the donor's own empty line, and the event variable's slot
/// reads the row one past it; this reader writes each line under the slot an event names, so that off-by-one
/// stays a fact of the source format here.
/// </para>
/// <para>
/// <b>What is not a line.</b> A numbered row whose text is empty is a slot the book shows nothing for (the
/// donor's book skips it, <c>src/GUI/UI/Books/JournalBook.cpp:44-46</c>); it is not read, and how many there
/// were is kept for the report.
/// </para>
/// <para>
/// <b>The text codes are the source format's.</b> The donor fills a history line through its dialogue string
/// builder with the time the slot was written (OpenEnroth <c>src/Engine/Objects/Character.cpp:3998</c>, read back in
/// <c>src/GUI/UI/Books/JournalBook.cpp:48</c>): <c>%30</c> is that day, and <c>%31</c> to <c>%34</c> are the
/// first to fourth character's names (<c>src/GUI/GUIWindow.cpp:953-965</c>). They are written here as
/// <c>{date}</c> and <c>{member:1}</c> to <c>{member:4}</c>, so no runtime reader knows the table's codes; any
/// other code is left as the table writes it, which is what the donor prints for an unknown one
/// (<c>src/GUI/GUIWindow.cpp:966-972</c>), and none occurs in the shipped table.
/// </para>
/// </remarks>
public sealed class HistoryTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 1;

    /// <summary>
    /// The last row the history book holds: the donor's array is 29 lines and row zero is its own empty one
    /// (OpenEnroth <c>src/Engine/Tables/HistoryTable.h:11</c>, <c>src/Engine/Tables/HistoryTable.cpp:17-18</c>).
    /// </summary>
    public const int LastRow = 28;

    private HistoryTable(TabularTable table, HistoryRecord[] rows, int skipped)
    {
        Table = table;
        Rows = rows;
        SkippedRows = skipped;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every line the table holds, in slot order.</summary>
    public IReadOnlyList<HistoryRecord> Rows { get; }

    /// <summary>How many numbered rows hold no line.</summary>
    public int SkippedRows { get; }

    /// <summary>Reads the table from an installation.</summary>
    /// <param name="install">The installation.</param>
    /// <returns>The table.</returns>
    /// <exception cref="LodFormatException">
    /// A row's number cannot be read, is not one of the slots an event can write, or two rows claim one number.
    /// </exception>
    public static HistoryTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.History, HeaderRowCount);
        List<HistoryRecord> rows = [];
        HashSet<int> numbers = [];
        int skipped = 0;
        foreach (TabularRow row in table.Rows)
        {
            if (row.Field(0).Trim().Length == 0) continue;
            int number = TableValue.Integer(table, row, 0, "Index");
            if (!numbers.Add(number))
            {
                throw new LodFormatException(LodFault.Ambiguous, $"{table.Source}: history line {number} has more than one row.");
            }

            if (number < 1 || number > LastRow)
            {
                throw new LodFormatException(LodFault.Value, $"{table.Source}: history line {number} is not one of the lines the book holds (1 to {LastRow}).");
            }

            string text = TableValue.Text(row, 1);
            if (text.Length == 0)
            {
                skipped++;
                continue;
            }

            rows.Add(new HistoryRecord(number - 1, Marks(text), TableValue.Text(row, 3)));
        }

        if (rows.Count == 0)
        {
            throw new LodFormatException(LodFault.Missing, $"{table.Source}: the table has no lines.");
        }

        return new HistoryTable(table, [.. rows.OrderBy(row => row.Slot)], skipped);
    }

    /// <summary>The line with the table's text codes written as the named marks a runtime reader fills.</summary>
    internal static string Marks(string text) => text
        .Replace("%30", "{date}", StringComparison.Ordinal)
        .Replace("%31", "{member:1}", StringComparison.Ordinal)
        .Replace("%32", "{member:2}", StringComparison.Ordinal)
        .Replace("%33", "{member:3}", StringComparison.Ordinal)
        .Replace("%34", "{member:4}", StringComparison.Ordinal);
}
