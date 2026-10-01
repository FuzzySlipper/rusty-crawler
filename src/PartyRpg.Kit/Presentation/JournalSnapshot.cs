using System.Globalization;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Presentation;

/// <summary>One row of one book, as the panel shows it.</summary>
/// <param name="Id">The row's identity, which is the owner's own identity for what it names.</param>
/// <param name="Label">What it is called.</param>
/// <param name="Detail">What is true of it, empty when nothing is.</param>
/// <param name="State">Its state, as the owner words it, empty when it has none.</param>
/// <param name="Source">Which owner reported it, which is what the row is attributable to.</param>
/// <param name="Marked">Whether the owner marks it, which a screen shows as done rather than working out.</param>
public sealed record JournalRowSnapshot(
    string Id,
    string Label,
    string Detail,
    string State,
    string Source,
    bool Marked)
{
    /// <summary>Writes one row of a book.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("id", builder.String(Id)),
            ("label", builder.String(Label)),
            ("detail", builder.String(Detail)),
            ("state", builder.String(State)),
            ("source", builder.String(Source)),
            ("marked", builder.Boolean(Marked)));
}

/// <summary>One of the journal's books, as the panel shows it.</summary>
/// <remarks>
/// The book's words are the ruleset's, its rows are read from the owner that holds each fact at the moment
/// the projection is built, and its state says what it holds or why it holds nothing. A book whose owner the
/// session does not compose is published as unavailable with the game's own sentence for that, which is a
/// different fact from a book that is merely empty.
/// </remarks>
/// <param name="Kind">The book's kind, as the wire spells it.</param>
/// <param name="Title">What the game calls it.</param>
/// <param name="Available">Whether the session holds the owner that fills it.</param>
/// <param name="State">What the game says about it.</param>
/// <param name="Rows">What it holds, in the owner's own order.</param>
public sealed record JournalBookSnapshot(
    string Kind,
    string Title,
    bool Available,
    string State,
    IReadOnlyList<JournalRowSnapshot> Rows)
{
    /// <summary>Writes one book: its title, its state sentence, and its rows.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The book's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("kind", builder.String(Kind)),
            ("title", builder.String(Title)),
            ("available", builder.Boolean(Available)),
            ("state", builder.String(State)),
            ("rows", builder.Array([.. Rows.Select(row => row.Write(builder))])));
}

/// <summary>The party's journal as the panel needs it: its books, and what each holds.</summary>
/// <remarks>
/// <para>
/// <b>The books are readings of the owners that hold their facts, not copies kept here.</b> The quests book's
/// page is the errands the quest owner reports — the quests block the panel renders — and this block says
/// that book's name, whether the session can fill it, and how much it holds; the notes book reads what the
/// knowledge owner holds; the maps book reads the world's own knowledge of its places; the calendar reads the
/// one clock; and the history reads the journal's dated lines. A screen therefore renders what it is given,
/// computes no date, counts no place, and decides no quest.
/// </para>
/// <para>
/// <b>A session with no journal owner has no books at all.</b> Its ruleset stated no journal, so the block
/// says the mechanism is not there and carries no list: empty books would look exactly like a party
/// that has been nowhere and done nothing.
/// </para>
/// <para>
/// <b>A book whose own owner is missing is not an empty book.</b> A session may keep a journal and no
/// knowledge — its ruleset stated no discoveries — and that book then says so in the game's own words rather
/// than showing an empty list a player would read as "there is nothing to learn here".
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a journal owner at all.</param>
/// <param name="Books">The books the game keeps, in its own order.</param>
public sealed record JournalSnapshot(bool Available, IReadOnlyList<JournalBookSnapshot> Books)
{
    /// <summary>No journal owner: the session's ruleset stated no journal, so there are no books.</summary>
    public static JournalSnapshot None => new(false, []);

    /// <summary>Reads the journal and the owners its books are read from into the panel's own value.</summary>
    /// <param name="journal">The party's journal, or null when the session holds none.</param>
    /// <param name="quests">The quest owner, or null when the session holds none.</param>
    /// <param name="world">The world the party stands in, or null when the session holds none.</param>
    /// <param name="clock">The session's one clock, or null when it has none.</param>
    /// <param name="knowledge">The knowledge owner, or null when the session holds none.</param>
    /// <param name="maps">The map owner, or null when the session maps nothing.</param>
    /// <returns>The journal's books as a panel shows them.</returns>
    public static JournalSnapshot From(
        PartyJournal? journal,
        PartyQuests? quests,
        SessionWorld? world,
        GameClock? clock,
        PartyKnowledge? knowledge = null,
        PartyMaps? maps = null)
    {
        if (journal is null) return None;

        List<JournalBookSnapshot> books = [];
        foreach (JournalBookKind kind in journal.Rule.Books)
        {
            JournalBookWords words = journal.Rule.Book(kind);
            books.Add(kind switch
            {
                JournalBookKind.Quests => Quests(words, quests),
                JournalBookKind.Notes => Notes(words, knowledge, world),
                JournalBookKind.Maps => Maps(words, maps, world),
                JournalBookKind.Calendar => Calendar(words, clock),
                _ => History(words, journal, world),
            });
        }

        return new JournalSnapshot(true, books);
    }

    /// <summary>
    /// The quests book.
    /// </summary>
    /// <remarks>
    /// This book carries no rows on purpose. Its page is the journal the quests block publishes, read from
    /// the same owner at the same moment, and printing the errands a second time here would be two readings of
    /// one fact on one wire — which is exactly what the journal exists not to be.
    /// </remarks>
    private static JournalBookSnapshot Quests(JournalBookWords words, PartyQuests? quests)
    {
        if (quests is null) return new JournalBookSnapshot("quests", words.Title, false, words.Unavailable, []);

        int errands = quests.Journal.Count;
        string state = errands == 0
            ? words.Empty
            : string.Create(CultureInfo.InvariantCulture, $"{errands} errand{(errands == 1 ? string.Empty : "s")} in the journal");
        return new JournalBookSnapshot("quests", words.Title, true, state, []);
    }

    /// <summary>The notes book: what the party has learned, as the knowledge owner reports it.</summary>
    /// <remarks>
    /// <para>
    /// One row per fact, oldest first, with when it was learned and where. The words are the game's — the
    /// note itself was composed by the knowledge owner when the fact was learned, in the game's phrase
    /// around the reporting owner's own name for the thing — so a screen prints a row and decides nothing,
    /// exactly as it does for the history beside it.
    /// </para>
    /// <para>
    /// <b>The kind is part of the row's identity and not a thing this surface groups by.</b> A discovery's
    /// kind is what makes it one fact rather than another — the same thing found and learned are different
    /// notes — and a screen that wants tabs by kind (potions, fountains, obelisks) has the kind word
    /// in the row's identity to read without this surface deciding what belongs together.
    /// </para>
    /// </remarks>
    private static JournalBookSnapshot Notes(JournalBookWords words, PartyKnowledge? knowledge, SessionWorld? world)
    {
        if (knowledge is null) return new JournalBookSnapshot("notes", words.Title, false, words.Unavailable, []);

        List<JournalRowSnapshot> rows = [];
        foreach (KnowledgeNote note in knowledge.Notes)
        {
            rows.Add(new JournalRowSnapshot(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{KnowledgeNoteSave.Word(note.Kind)}|{note.Subject}|{note.Place}"),
                note.Text,
                string.Create(CultureInfo.InvariantCulture, $"{Date(note.Date)} {Time(note.Date)}"),
                PlaceName(note.Place, world),
                note.Source,
                false));
        }

        string state = rows.Count == 0
            ? words.Empty
            : string.Create(CultureInfo.InvariantCulture, $"{rows.Count} note{(rows.Count == 1 ? string.Empty : "s")}");
        return new JournalBookSnapshot("notes", words.Title, true, state, rows);
    }

    /// <summary>
    /// The maps book: one page per place, read from the map owner the automap is drawn from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The book and the automap are one owner's answers.</b> Every row is a place the party holds a map of,
    /// and what it says about it is the same owner's count of the cells it has seen of that place's own map —
    /// so a book and a drawing cannot disagree about where the party has been, and there is no second list of
    /// places for either of them to drift from.
    /// </para>
    /// <para>
    /// <b>A place the party has been to but holds no map of is not a page.</b> The world's own record of where
    /// the party has been is a different fact — it is what the world remembers of a place rather than what the
    /// party drew — and a book of maps shows maps. A session whose content carries no maps for the places it
    /// has places in says so in the game's own words rather than listing rooms it cannot draw.
    /// </para>
    /// </remarks>
    private static JournalBookSnapshot Maps(JournalBookWords words, PartyMaps? maps, SessionWorld? world)
    {
        if (maps is null) return new JournalBookSnapshot("maps", words.Title, false, words.Unavailable, []);

        MapWords mapWords = maps.Rule.Words;
        List<JournalRowSnapshot> rows = [];
        string here = world?.Place.Value ?? string.Empty;
        foreach (MapTerritory territory in maps.Territories)
        {
            PlaceDefinition? place = world?.Graph.Find(territory.Place);
            bool current = string.Equals(territory.Place.Value, here, StringComparison.Ordinal);
            rows.Add(new JournalRowSnapshot(
                territory.Place.Value,
                place?.Name ?? territory.Place.Value,
                place is null ? string.Empty : SessionProjection.WireName(place.Kind),
                mapWords.Seen(territory.SeenCount, territory.Grid.Cells),
                "map",
                current));
        }

        string state = rows.Count == 0 ? mapWords.Empty : mapWords.Mapped(rows.Count);
        return new JournalBookSnapshot("maps", words.Title, true, state, rows);
    }

    /// <summary>The calendar book: what day it is, read from the one clock.</summary>
    private static JournalBookSnapshot Calendar(JournalBookWords words, GameClock? clock)
    {
        if (clock is null) return new JournalBookSnapshot("calendar", words.Title, false, words.Unavailable, []);

        GameDate now = clock.Now;
        List<JournalRowSnapshot> rows =
        [
            new JournalRowSnapshot("date", "Today", Date(now), string.Empty, "clock", false),
            new JournalRowSnapshot("time", "Time", Time(now), clock.IsDaylight ? "day" : "night", "clock", false),
            new JournalRowSnapshot(
                "elapsed",
                "Days since the expedition began",
                clock.ElapsedGameDays.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                "clock",
                false),
        ];

        return new JournalBookSnapshot("calendar", words.Title, true, Date(now), rows);
    }

    /// <summary>The history book: what the party wrote down, oldest first, with where each line happened.</summary>
    private static JournalBookSnapshot History(JournalBookWords words, PartyJournal journal, SessionWorld? world)
    {
        List<JournalRowSnapshot> rows = [];
        foreach (JournalEntry entry in journal.Entries)
        {
            rows.Add(new JournalRowSnapshot(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{JournalEntrySave.Word(entry.Kind)}|{entry.Subject}|{entry.Place}"),
                entry.Text,
                string.Create(CultureInfo.InvariantCulture, $"{Date(entry.Date)} {Time(entry.Date)}"),
                PlaceName(entry.Place, world),
                entry.Source,
                false));
        }

        string state = rows.Count == 0 ? words.Empty : string.Create(CultureInfo.InvariantCulture, $"{rows.Count} entries");
        return new JournalBookSnapshot("history", words.Title, true, state, rows);
    }

    /// <summary>What a place is called, or the recorded identity when the world no longer carries it.</summary>
    /// <remarks>
    /// A journal line outlives the place it happened in, so a place the world has stopped carrying reads as
    /// the identity the entry recorded rather than as an empty where.
    /// </remarks>
    private static string PlaceName(string place, SessionWorld? world)
    {
        if (place.Length == 0) return string.Empty;
        return world?.Graph.Find(new PlaceId(place))?.Name ?? place;
    }

    /// <summary>The calendar's own numbers for a day, in the form every other block publishes dates in.</summary>
    private static string Date(GameDate date) =>
        date.DayText;

    /// <summary>The time of day, to the minute, in the form every other block publishes times in.</summary>
    private static string Time(GameDate date) =>
        string.Create(CultureInfo.InvariantCulture, $"{date.Hour:00}:{date.Minute:00}");

    /// <summary>Writes the journal block: the five books and what each of them holds.</summary>
    /// <remarks>
    /// Every row is sent whole, with the words the owner gave it and the state the owner reports, so a screen
    /// that shows a book decides nothing about it: it prints a title, a state sentence, and rows. The quests
    /// book carries no rows because its page is the quests block, read from the same owner at the same
    /// moment: publishing the errands twice would be two readings of one fact on one wire.
    /// </remarks>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("available", builder.Boolean(Available)),
            ("books", builder.Array([.. Books.Select(book => book.Write(builder))])));
}
