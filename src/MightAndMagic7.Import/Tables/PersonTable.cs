using System.Globalization;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One row of the game's NPC table: somebody the world can hold.</summary>
/// <remarks>
/// Columns are the ones the donor reads (OpenEnroth <c>src/Engine/Tables/NPCTable.cpp:57-80</c>,
/// <c>InitializeNPCData</c>): the row's own number, the name as it is displayed, the portrait, the groups
/// the row belongs to, the building the row is placed in, the hireling profession, the greeting the row
/// uses, whether the person may join the party, and up to six dialogue event numbers. A zero in the
/// building column is the table's own way of saying the row is not placed in a house, and a zero
/// profession is its way of saying the row is not a hireling.
/// </remarks>
/// <param name="Id">The row's number, which is what an actor's own NPC identity names.</param>
/// <param name="Name">The name as the game displays it.</param>
/// <param name="Portrait">The portrait the row names, zero when it names none.</param>
/// <param name="Groups">The group columns, as the table states them.</param>
/// <param name="House">The building the row is placed in, zero when it is placed in none.</param>
/// <param name="Profession">The hireling profession, zero when the row is not one.</param>
/// <param name="GreetingIndex">The greeting row this person uses, zero when the row names none.</param>
/// <param name="CanJoin">Whether the table says this person may join the party.</param>
/// <param name="DialogueEvents">The dialogue event numbers the row states, in column order.</param>
/// <param name="Notes">The row's own notes column, kept as the table wrote it.</param>
public sealed record NpcRecord(
    int Id,
    string Name,
    int Portrait,
    IReadOnlyList<int> Groups,
    int House,
    int Profession,
    int GreetingIndex,
    bool CanJoin,
    IReadOnlyList<int> DialogueEvents,
    string Notes);

/// <summary>One row of the greeting table: what a person says when met, and when met again.</summary>
/// <remarks>
/// The table's own comment calls the two the first and the latest meeting, which is why they are kept
/// apart rather than joined into one line: a person who has met the party before says something else, and
/// that is state the party carries rather than a second person.
/// </remarks>
/// <param name="Index">The greeting's number, which an NPC row's greeting column names.</param>
/// <param name="First">What is said when the party meets this person.</param>
/// <param name="Again">What is said on later meetings.</param>
/// <param name="Owner">The table's own note about who the greeting belongs to, kept as written.</param>
public sealed record NpcGreeting(int Index, string First, string Again, string Owner);

/// <summary>One row of the topic table: something a person can be asked about.</summary>
/// <remarks>
/// The topic's text is not here: the row's text column names rows of the text table, and a row may name
/// several because the original chooses between them by the state of its event programs. The numbers are
/// kept as the row states them, in order, so a reader can see that a line has versions rather than being
/// handed one as if it were the whole answer.
/// </remarks>
/// <param name="Id">The topic's number.</param>
/// <param name="Label">The topic as a person reads it.</param>
/// <param name="Requires">The row's own requirement column; zero when it requires nothing.</param>
/// <param name="TextIds">The text rows the topic names, in the order the row writes them.</param>
/// <param name="OwnerIds">The NPC rows the topic belongs to; a zero is the table's own "nobody".</param>
/// <param name="Owners">The owner names the table writes beside the numbers.</param>
/// <param name="Notes">The row's own notes column, kept as the table wrote it.</param>
public sealed record NpcTopicRecord(
    int Id,
    string Label,
    int Requires,
    IReadOnlyList<int> TextIds,
    IReadOnlyList<int> OwnerIds,
    IReadOnlyList<string> Owners,
    string Notes);

/// <summary>One row of the text table: what a person says about a topic.</summary>
/// <param name="Id">The text's number, which a topic row names.</param>
/// <param name="Text">The text itself.</param>
/// <param name="Owner">The table's own note about who says it, kept as written.</param>
public sealed record NpcText(int Id, string Text, string Owner);

/// <summary>
/// The game's people: who exists, what they say when met, and what they can be asked about.
/// </summary>
/// <remarks>
/// <para>
/// Four tables together are what a person is. The NPC table names them, places them in buildings, and gives
/// them a portrait, a greeting row, and the dialogue event numbers the original runs when somebody speaks;
/// the greeting table says what they say on a first and a later meeting; the topic table says what they can
/// be asked about and who owns each topic; and the text table says what they answer with. The third and
/// fourth are separate because a topic's text is chosen by the original's event programs, so a topic may
/// name several texts and this reads all of them.
/// </para>
/// <para>
/// <b>The whole file is read, not the first five hundred rows.</b> The donor stops at five hundred
/// (<c>InitializeNPCData</c> takes five hundred rows), which loses the rows after that; the operator's file
/// carries nine hundred and ninety-nine, and a reader that quietly stopped would make the rest of the world
/// unreachable without saying so. Every row with a number is read, and the count is reported.
/// </para>
/// </remarks>
public sealed class PersonTable
{
    /// <summary>How many leading rows the NPC table's own header occupies.</summary>
    public const int NpcHeaderRowCount = 2;

    /// <summary>How many leading rows the greeting table's own header occupies.</summary>
    public const int GreetingHeaderRowCount = 1;

    /// <summary>How many leading rows the topic table's own header occupies.</summary>
    public const int TopicHeaderRowCount = 1;

    /// <summary>How many leading rows the text table's own header occupies.</summary>
    public const int TextHeaderRowCount = 1;

    private PersonTable(
        TabularTable npcTable,
        TabularTable greetingTable,
        TabularTable topicTable,
        TabularTable textTable,
        NpcRecord[] npcs,
        NpcGreeting[] greetings,
        NpcTopicRecord[] topics,
        NpcText[] texts,
        IReadOnlyList<string> notes)
    {
        NpcTable = npcTable;
        GreetingTable = greetingTable;
        TopicTable = topicTable;
        TextTable = textTable;
        Npcs = npcs;
        Greetings = greetings;
        Topics = topics;
        Texts = texts;
        Notes = notes;
    }

    /// <summary>The NPC table this was read from.</summary>
    public TabularTable NpcTable { get; }

    /// <summary>The greeting table this was read from.</summary>
    public TabularTable GreetingTable { get; }

    /// <summary>The topic table this was read from.</summary>
    public TabularTable TopicTable { get; }

    /// <summary>The text table this was read from.</summary>
    public TabularTable TextTable { get; }

    /// <summary>Every person the table names, in table order.</summary>
    public IReadOnlyList<NpcRecord> Npcs { get; }

    /// <summary>Every greeting the table carries, in table order.</summary>
    public IReadOnlyList<NpcGreeting> Greetings { get; }

    /// <summary>Every topic the table carries, in table order.</summary>
    public IReadOnlyList<NpcTopicRecord> Topics { get; }

    /// <summary>Every answer text the table carries, in table order.</summary>
    public IReadOnlyList<NpcText> Texts { get; }

    /// <summary>What the read noticed about the tables, for a report.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>How many people are placed in a building.</summary>
    public int PlacedCount => Npcs.Count(npc => npc.House != 0);

    /// <summary>How many distinct buildings the table places people in.</summary>
    public int PlacedHouseCount => Npcs.Where(npc => npc.House != 0).Select(npc => npc.House).Distinct().Count();

    /// <summary>How many people name at least one dialogue event.</summary>
    public int ScriptedCount => Npcs.Count(npc => npc.DialogueEvents.Count > 0);

    /// <summary>One person by the identity an actor's own record names, or null when the table lacks it.</summary>
    /// <param name="id">The NPC table row number.</param>
    public NpcRecord? Npc(int id)
    {
        foreach (NpcRecord npc in Npcs)
        {
            if (npc.Id == id) return npc;
        }

        return null;
    }

    /// <summary>One greeting by the number an NPC row names, or null when the table lacks it.</summary>
    /// <param name="index">The greeting row number.</param>
    public NpcGreeting? Greeting(int index)
    {
        foreach (NpcGreeting greeting in Greetings)
        {
            if (greeting.Index == index) return greeting;
        }

        return null;
    }

    /// <summary>One answer text by the number a topic row names, or null when the table lacks it.</summary>
    /// <param name="id">The text row number.</param>
    public NpcText? Text(int id)
    {
        foreach (NpcText text in Texts)
        {
            if (text.Id == id) return text;
        }

        return null;
    }

    /// <summary>Every topic one person owns, in table order.</summary>
    /// <param name="npc">The person's row number.</param>
    public IReadOnlyList<NpcTopicRecord> TopicsOf(int npc) =>
        [.. Topics.Where(topic => topic.OwnerIds.Contains(npc))];

    /// <summary>Reads every people table from an installation.</summary>
    /// <param name="install">The installation to read from.</param>
    public static PersonTable Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        TabularTable npcTable = TabularTable.Read(install, Mm7TableSources.Npcs, NpcHeaderRowCount);
        TabularTable greetingTable = TabularTable.Read(install, Mm7TableSources.Greetings, GreetingHeaderRowCount);
        TabularTable topicTable = TabularTable.Read(install, Mm7TableSources.Topics, TopicHeaderRowCount);
        TabularTable textTable = TabularTable.Read(install, Mm7TableSources.TopicTexts, TextHeaderRowCount);

        List<string> notes = [];
        NpcRecord[] npcs = ReadNpcs(npcTable, notes);
        NpcGreeting[] greetings = ReadGreetings(greetingTable, notes);
        NpcTopicRecord[] topics = ReadTopics(topicTable, notes);
        NpcText[] texts = ReadTexts(textTable, notes);
        return new PersonTable(npcTable, greetingTable, topicTable, textTable, npcs, greetings, topics, texts, notes);
    }

    private static NpcRecord[] ReadNpcs(TabularTable table, List<string> notes)
    {
        List<NpcRecord> npcs = [];
        HashSet<int> seen = [];
        int unnumbered = 0;
        foreach (TabularRow row in table.Rows)
        {
            if (!Number(row.Field(0), out int id))
            {
                unnumbered++;
                continue;
            }

            if (!seen.Add(id))
            {
                notes.Add($"npcdata row {id} is declared more than once, so the second one is not read: which person an actor's identity names would be ambiguous.");
                continue;
            }

            if (TableValue.Text(row, 1).Length == 0)
            {
                notes.Add($"npcdata row {id} names nobody, so no person is placed for it.");
                continue;
            }

            npcs.Add(new NpcRecord(
                id,
                TableValue.Text(row, 1),
                Number(row.Field(2), out int portrait) ? portrait : 0,
                [.. new[] { 3, 4, 5 }.Select(column => Number(row.Field(column), out int group) ? group : 0)],
                Number(row.Field(6), out int house) ? house : 0,
                Number(row.Field(7), out int profession) ? profession : 0,
                Number(row.Field(8), out int greeting) ? greeting : 0,
                string.Equals(row.Field(9).Trim(), "y", StringComparison.OrdinalIgnoreCase),
                [.. Enumerable.Range(10, 6).Select(column => Number(row.Field(column), out int evt) ? evt : 0).Where(evt => evt != 0)],
                TableValue.Text(row, 16)));
        }

        if (unnumbered > 0)
        {
            notes.Add($"{unnumbered} npcdata rows carry no number, so they are annotation rather than people.");
        }

        return [.. npcs];
    }

    private static NpcGreeting[] ReadGreetings(TabularTable table, List<string> notes)
    {
        List<NpcGreeting> greetings = [];
        HashSet<int> seen = [];
        foreach (TabularRow row in table.Rows)
        {
            if (!Number(row.Field(0), out int index)) continue;
            if (!seen.Add(index))
            {
                notes.Add($"npcgreet row {index} is declared more than once, so the second one is not read.");
                continue;
            }

            greetings.Add(new NpcGreeting(
                index,
                TableValue.Text(row, 1),
                TableValue.Text(row, 2),
                TableValue.Text(row, 4)));
        }

        return [.. greetings];
    }

    private static NpcTopicRecord[] ReadTopics(TabularTable table, List<string> notes)
    {
        List<NpcTopicRecord> topics = [];
        HashSet<int> seen = [];
        int unplaced = 0;
        foreach (TabularRow row in table.Rows)
        {
            if (!Number(row.Field(0), out int id)) continue;
            if (!seen.Add(id))
            {
                notes.Add($"npctopic row {id} is declared more than once, so the second one is not read.");
                continue;
            }

            string label = TableValue.Text(row, 1);
            if (label.Length == 0)
            {
                notes.Add($"npctopic row {id} names no topic, so it is not something anybody can be asked about.");
                continue;
            }

            IReadOnlyList<int> ownerIds = [.. TableField.Numbers(row.Field(6))];
            IReadOnlyList<string> owners = [.. new[] { 5 }.Select(column => TableValue.Text(row, column)).Where(name => name.Length > 0)];
            if (ownerIds.Count == 0) unplaced++;
            topics.Add(new NpcTopicRecord(
                id,
                label,
                Number(row.Field(2), out int requires) ? requires : 0,
                [.. TextIds(row.Field(4))],
                ownerIds,
                owners,
                TableValue.Text(row, 3)));
        }

        if (unplaced > 0)
        {
            notes.Add($"{unplaced} npctopic rows name no owner number, so they belong to nobody this table can place.");
        }

        return [.. topics];
    }

    private static NpcText[] ReadTexts(TabularTable table, List<string> notes)
    {
        List<NpcText> texts = [];
        HashSet<int> seen = [];
        foreach (TabularRow row in table.Rows)
        {
            if (!Number(row.Field(0), out int id)) continue;
            if (!seen.Add(id))
            {
                notes.Add($"npctext row {id} is declared more than once, so the second one is not read.");
                continue;
            }

            texts.Add(new NpcText(id, TableValue.Text(row, 1), TableValue.Text(row, 3)));
        }

        return [.. texts];
    }

    /// <summary>
    /// The text rows a topic names, which the table may write as one number, a comma-separated list, or an
    /// inclusive range.
    /// </summary>
    /// <remarks>
    /// The ranges are the original's own: a topic whose answer depends on the party's state names every
    /// text that state could select, and it writes them as a span (<c>26-33</c>). Reading only the first
    /// number would silently drop seven answers the data carries, so the whole span is kept and the count
    /// travels with the topic.
    /// </remarks>
    private static IEnumerable<int> TextIds(string field)
    {
        foreach (string part in field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int dash = part.IndexOf('-', StringComparison.Ordinal);
            if (dash > 0 &&
                Number(part[..dash], out int from) &&
                Number(part[(dash + 1)..], out int to) &&
                to >= from)
            {
                for (int id = from; id <= to; id++) yield return id;
                continue;
            }

            if (Number(part, out int single)) yield return single;
        }
    }

    private static bool Number(string field, out int value) =>
        int.TryParse(field.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
}

/// <summary>Reads numbers out of one table field, which the tables sometimes write as a list.</summary>
internal static class TableField
{
    /// <summary>Every number a field states, in the order it states them.</summary>
    /// <param name="field">The field as the table wrote it.</param>
    internal static IEnumerable<int> Numbers(string field)
    {
        foreach (string part in field.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)) yield return value;
        }
    }
}
