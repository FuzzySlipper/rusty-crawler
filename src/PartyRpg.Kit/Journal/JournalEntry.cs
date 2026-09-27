using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Journal;

/// <summary>What kind of thing one journal entry records.</summary>
/// <remarks>
/// <para>
/// These are the events a party's own record is made of, and every one of them is written by the owner that
/// already reports it: a place the world put the party in, an errand the quest owner offered, took, or
/// finished, a rank the progression owner granted, a person the conversation opened onto, and a notable find
/// a search yielded. There is deliberately no kind for a state a reader can recompute — an objective's
/// progress, a party's standing, a place it is still standing in — because a journal records <em>when</em>
/// something happened, and what is still true is read from its owner.
/// </para>
/// <para>
/// The three errand kinds are separate rather than one kind with a stage word, because each is a different
/// sentence in a person's record and because the stage a party reached is exactly what the entry is about:
/// hearing an errand, agreeing to it, and finishing it are three moments, not one that moved.
/// </para>
/// </remarks>
public enum JournalEntryKind
{
    /// <summary>A place the party entered, written the first time it stood there.</summary>
    Place,

    /// <summary>An errand a person stated to the party.</summary>
    QuestOffered,

    /// <summary>An errand the party agreed to carry.</summary>
    QuestTaken,

    /// <summary>An errand the party finished and was paid for.</summary>
    QuestFinished,

    /// <summary>A rank a member of the party rose to.</summary>
    Rank,

    /// <summary>Somebody the party spoke with.</summary>
    Meeting,

    /// <summary>Something notable the party found.</summary>
    Find,
}

/// <summary>
/// One event worth writing down, as the owner that reported it states it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reporter states what happened; the journal decides how it reads and when it was.</b> An owner
/// hands over the kind of event, who reported it, what it is about, what it is called in that owner's own
/// words, and where it happened — and nothing else. The date is the journal's to read from the one clock, so
/// no reporter can date an entry from a clock of its own, and the sentence is the ruleset's to word, so no
/// kit mechanism carries a game's vocabulary.
/// </para>
/// <para>
/// <b>A reporter reports every time the event happens and never checks whether it is news.</b> The offer of
/// an errand the party already heard, a place it is still standing in, a person it keeps speaking with: all
/// of them arrive here, and the journal is the one place that decides whether that is already written down.
/// That is what keeps deduplication in one owner rather than in every reporter.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of thing happened.</param>
/// <param name="Source">Which owner reported it, as the journal attributes the entry: <c>world</c>, <c>quest</c>, <c>progression</c>, <c>conversation</c>, or the ruleset's own word for what yielded a find.</param>
/// <param name="Subject">The identity of what it is about, which is what makes one event one entry.</param>
/// <param name="Name">What it is called, in the reporting owner's own words.</param>
/// <param name="Place">The place it happened in, or empty when it happened nowhere in particular.</param>
/// <exception cref="ArgumentException">The source, the subject, or the name is blank.</exception>
public readonly record struct JournalEvent(
    JournalEntryKind Kind,
    string Source,
    string Subject,
    string Name,
    string Place = "")
{
    /// <summary>Which owner reported it, which is what an entry is attributable to.</summary>
    public string Source { get; } = Require(Source, nameof(Source));

    /// <summary>The identity of what it is about.</summary>
    public string Subject { get; } = Require(Subject, nameof(Subject));

    /// <summary>What it is called, in the reporting owner's own words.</summary>
    public string Name { get; } = Require(Name, nameof(Name));

    /// <summary>The place it happened in, or empty when it happened nowhere in particular.</summary>
    public string Place { get; } = Place ?? string.Empty;

    /// <summary>Whether this reports the same event as another report.</summary>
    /// <remarks>
    /// The same kind of thing happening to the same subject in the same place is one event: a party that
    /// stands in a place for a hundred updates entered it once, and a person it speaks with twice was met
    /// once. A find is the exception the place is carried for — two of a thing found in two places are two
    /// findings — which is why the place is part of what makes an event the same one.
    /// </remarks>
    /// <param name="other">The other report.</param>
    /// <returns>Whether they are one event.</returns>
    public bool IsSameEvent(JournalEvent other) =>
        Kind == other.Kind
        && string.Equals(Subject, other.Subject, StringComparison.Ordinal)
        && string.Equals(Place, other.Place, StringComparison.Ordinal);

    private static string Require(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException(
                $"A journal event states no {parameterName}, so nothing would say what happened or what it happened to.",
                parameterName);
}

/// <summary>One dated line of a party's history: what happened, when, and who reported it.</summary>
/// <remarks>
/// <para>
/// <b>The line is written once and never recomputed.</b> What a person reads is composed when the event
/// happens, in the words the game had then, so a quest renamed, a place re-titled, or a person the world
/// stops carrying cannot rewrite what the party's own record says it did.
/// </para>
/// <para>
/// <b>The durable fact is the elapsed game time; the date is that fact read against the calendar.</b> A save
/// records the elapsed milliseconds since the session began and never a date, for the same reason the clock
/// itself is saved that way: a date stored beside a ruleset that composes a different starting date or
/// calendar would silently move every line of a party's history. The date is kept beside the elapsed time so
/// a panel renders a date rather than doing calendar arithmetic, and a load recomposes both from the save
/// and the clock it was resumed on — which is what makes a loaded entry read as the day it happened rather
/// than the day it was loaded.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of thing happened.</param>
/// <param name="Source">Which owner reported it.</param>
/// <param name="Subject">The identity of what it is about.</param>
/// <param name="Text">The line a person reads, as the game worded it when it happened.</param>
/// <param name="ElapsedMilliseconds">The game time since the session began at which it happened.</param>
/// <param name="Date">That game time as a point on the calendar.</param>
/// <param name="Place">The place it happened in, or empty when it happened nowhere in particular.</param>
/// <exception cref="ArgumentException">A word is blank.</exception>
/// <exception cref="ArgumentOutOfRangeException">The elapsed game time is negative, which game time never is.</exception>
public sealed record JournalEntry(
    JournalEntryKind Kind,
    string Source,
    string Subject,
    string Text,
    long ElapsedMilliseconds,
    GameDate Date,
    string Place = "")
{
    /// <summary>Which owner reported it.</summary>
    public string Source { get; } = !string.IsNullOrWhiteSpace(Source)
        ? Source
        : throw new ArgumentException("A journal entry names no source, so nothing attributes it.", nameof(Source));

    /// <summary>The identity of what it is about.</summary>
    public string Subject { get; } = !string.IsNullOrWhiteSpace(Subject)
        ? Subject
        : throw new ArgumentException("A journal entry names no subject, so nothing says what it records.", nameof(Subject));

    /// <summary>The line a person reads.</summary>
    public string Text { get; } = !string.IsNullOrWhiteSpace(Text)
        ? Text
        : throw new ArgumentException("A journal entry says nothing, so it would read as an empty line.", nameof(Text));

    /// <summary>The game time since the session began at which it happened.</summary>
    public long ElapsedMilliseconds { get; } = ElapsedMilliseconds >= 0
        ? ElapsedMilliseconds
        : throw new ArgumentOutOfRangeException(
            nameof(ElapsedMilliseconds),
            ElapsedMilliseconds,
            "Game time runs forward, so an entry happened at zero or more milliseconds into the session.");

    /// <summary>The place it happened in, or empty when it happened nowhere in particular.</summary>
    public string Place { get; } = Place ?? string.Empty;

    /// <summary>Whether this records the same event as a report.</summary>
    /// <remarks>
    /// The line the entry reads with is deliberately not compared: what makes two reports one event is the
    /// kind of thing, what it happened to, and where — never the wording, which is composed once and stays.
    /// </remarks>
    /// <param name="journalEvent">The report.</param>
    /// <returns>Whether they are one event.</returns>
    public bool Records(JournalEvent journalEvent) =>
        Kind == journalEvent.Kind
        && string.Equals(Subject, journalEvent.Subject, StringComparison.Ordinal)
        && string.Equals(Place, journalEvent.Place, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => $"{Date.Year:0000}-{Date.Month:00}-{Date.Day:00} {Text}";
}
