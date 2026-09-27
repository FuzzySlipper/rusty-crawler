namespace PartyRpg.Kit.Journal;

/// <summary>One of the books a party's journal is kept in.</summary>
/// <remarks>
/// The five are the design's own set of books rather than a screen's tabs: quests, notes, maps, calendar, and
/// history. What each holds is a different owner's reading — the quest owner's errands, the knowledge owner's
/// discoveries, the world's places, the clock's date, this journal's own dated lines — so no book is a copy of
/// another and none of them is computed by a screen.
/// </remarks>
public enum JournalBookKind
{
    /// <summary>The errands the party stands with, read from the quest owner.</summary>
    Quests,

    /// <summary>What the party has learned, read from the knowledge owner.</summary>
    Notes,

    /// <summary>Where the party has been, read from the world's own knowledge of its places.</summary>
    Maps,

    /// <summary>What day it is, read from the one clock.</summary>
    Calendar,

    /// <summary>What happened and when, which is this journal's own record.</summary>
    History,
}

/// <summary>What a game calls one of the five books, and what it says when the book holds nothing.</summary>
/// <remarks>
/// The words belong to the ruleset rather than to the kit, exactly as every other piece of presentation
/// meaning does: a game's books are named in its own manual, and a kit that spelled them itself would be
/// carrying one game's vocabulary into every other one.
/// </remarks>
/// <param name="Title">What this game calls the book.</param>
/// <param name="Empty">What it says when the session can fill the book and the book holds nothing.</param>
/// <param name="Unavailable">
/// What it says when the session cannot fill the book at all — the owner that would is not composed — which
/// is a different fact from a book that is merely empty.
/// </param>
public readonly record struct JournalBookWords(string Title, string Empty, string Unavailable);

/// <summary>One row of one book: an identity, what it reads as, and the state it carries.</summary>
/// <remarks>
/// A row is deliberately flat. What a book shows is a list a person reads, and every value here is read from
/// the owner that holds it before the row is built, so a screen renders a row rather than comparing two
/// facts to decide what it says.
/// </remarks>
/// <param name="Id">The row's identity, which is the owner's own identity for what it names.</param>
/// <param name="Label">The row's first line: what it is called.</param>
/// <param name="Detail">The row's second line: what is true of it, empty when nothing is.</param>
/// <param name="State">The row's state, as the owner words it, empty when it has none.</param>
/// <param name="Marked">Whether the owner marks this row, which a screen shows as done rather than working out.</param>
public readonly record struct JournalBookRow(string Id, string Label, string Detail = "", string State = "", bool Marked = false);

/// <summary>One of the five books as a session can fill it.</summary>
/// <remarks>
/// <para>
/// <b>A book that cannot be filled is not an empty book.</b> "This session holds no owner that keeps notes"
/// and "the party has written nothing down" are two different facts, and a screen that showed both as an
/// empty list would leave a player unable to tell a build that records no discoveries from a party that has
/// made none. Which of the two it is, is <see cref="Available"/>; what it says is the game's own words.
/// </para>
/// <para>
/// The rows are copied from their owners at the moment the projection is built and never kept: a book is a
/// reading, so a party that finishes an errand, finds a place, or spends a day sees the book change without
/// anything having had to notice.
/// </para>
/// </remarks>
/// <param name="Kind">Which of the five books this is.</param>
/// <param name="Title">What the game calls it.</param>
/// <param name="Available">Whether the session holds the owner that fills it.</param>
/// <param name="State">What the game says about it: what it holds, why it is empty, or what is missing.</param>
/// <param name="Rows">What it holds, in the owner's own order.</param>
public sealed record JournalBook(
    JournalBookKind Kind,
    string Title,
    bool Available,
    string State,
    IReadOnlyList<JournalBookRow> Rows)
{
    /// <summary>The five books, in the order a journal keeps them.</summary>
    /// <remarks>
    /// The order is stated here rather than left to whichever enumeration a caller happens to walk, so the
    /// books a panel shows are the same books in the same order on every projection.
    /// </remarks>
    public static IReadOnlyList<JournalBookKind> All { get; } =
    [
        JournalBookKind.Quests,
        JournalBookKind.Notes,
        JournalBookKind.Maps,
        JournalBookKind.Calendar,
        JournalBookKind.History,
    ];

    /// <summary>The word the projection spells one book's kind with.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The word.</returns>
    public static string Word(JournalBookKind kind) => kind switch
    {
        JournalBookKind.Quests => "quests",
        JournalBookKind.Notes => "notes",
        JournalBookKind.Maps => "maps",
        JournalBookKind.Calendar => "calendar",
        _ => "history",
    };
}
