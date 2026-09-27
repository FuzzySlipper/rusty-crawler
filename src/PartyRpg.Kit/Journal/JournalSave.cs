using System.Globalization;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Journal;

/// <summary>One line of a party's history as a save records it, under the product's one current schema.</summary>
/// <remarks>
/// <para>
/// <b>The date is deliberately absent and the elapsed game time is here instead.</b> A save records how far
/// into the session an event happened and never which day it fell on: the calendar, the date a session
/// begins at, and the rate game time runs at are the ruleset's policy and are supplied again when a session
/// is composed, so a date stored beside them would move every line of a party's history the moment any of
/// them changed. The elapsed span is what the clock actually counted, and a load reads it back against the
/// calendar it was resumed on — which is what makes a loaded entry read as the day it happened rather than
/// the day it was loaded.
/// </para>
/// <para>
/// <b>The line is carried rather than recomposed.</b> What a person read when the event happened is what the
/// party's record says it did; re-deriving it from the owners a load happens to find would let a renamed
/// quest or a place the world no longer carries rewrite history.
/// </para>
/// </remarks>
/// <param name="Kind">The kind of thing that happened, as the wire spells it.</param>
/// <param name="Source">Which owner reported it.</param>
/// <param name="Subject">The identity of what it is about.</param>
/// <param name="Text">The line a person reads.</param>
/// <param name="ElapsedMilliseconds">The game time since the session began at which it happened.</param>
/// <param name="Place">The place it happened in, or empty when it happened nowhere in particular.</param>
public sealed record JournalEntrySave(
    string Kind,
    string Source,
    string Subject,
    string Text,
    long ElapsedMilliseconds,
    string Place = "")
{
    /// <summary>The place it happened in, or empty when it happened nowhere in particular.</summary>
    public string Place { get; init; } = Place ?? string.Empty;

    /// <summary>Reads one recorded line as a dated entry, over the clock the session was resumed on.</summary>
    /// <remarks>
    /// The date is composed exactly as the clock composes its own: the elapsed span is added to the date the
    /// session began at, on the calendar the ruleset supplied. A line therefore reads as the day it happened
    /// even when the party has lived for weeks since.
    /// </remarks>
    /// <param name="recorded">The line the save recorded.</param>
    /// <param name="clock">The session's one clock, which owns the calendar and the date the session began at.</param>
    /// <returns>The dated entry.</returns>
    /// <exception cref="ArgumentNullException">A recorded line or a clock is missing.</exception>
    /// <exception cref="ArgumentException">The kind word names no kind this build has.</exception>
    public static JournalEntry Read(JournalEntrySave recorded, GameClock clock)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(clock);
        return new JournalEntry(
            ReadKind(recorded.Kind),
            recorded.Source,
            recorded.Subject,
            recorded.Text,
            recorded.ElapsedMilliseconds,
            clock.Calendar.Add(clock.Start, GameDuration.FromMilliseconds(recorded.ElapsedMilliseconds)),
            recorded.Place);
    }

    /// <summary>Records one dated entry as a save writes it.</summary>
    /// <param name="entry">The entry to record.</param>
    /// <returns>The recorded line.</returns>
    /// <exception cref="ArgumentNullException">No entry was supplied.</exception>
    public static JournalEntrySave Record(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new JournalEntrySave(
            Word(entry.Kind),
            entry.Source,
            entry.Subject,
            entry.Text,
            entry.ElapsedMilliseconds,
            entry.Place);
    }

    /// <summary>The word the wire spells one kind of entry with.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The word.</returns>
    public static string Word(JournalEntryKind kind) => kind switch
    {
        JournalEntryKind.Place => "place",
        JournalEntryKind.QuestOffered => "quest-offered",
        JournalEntryKind.QuestTaken => "quest-taken",
        JournalEntryKind.QuestFinished => "quest-finished",
        JournalEntryKind.Rank => "rank",
        JournalEntryKind.Meeting => "meeting",
        _ => "find",
    };

    /// <summary>The kind one word names.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The kind.</returns>
    /// <exception cref="ArgumentException">The word names no kind this build has.</exception>
    public static JournalEntryKind ReadKind(string word) => word switch
    {
        "place" => JournalEntryKind.Place,
        "quest-offered" => JournalEntryKind.QuestOffered,
        "quest-taken" => JournalEntryKind.QuestTaken,
        "quest-finished" => JournalEntryKind.QuestFinished,
        "rank" => JournalEntryKind.Rank,
        "meeting" => JournalEntryKind.Meeting,
        "find" => JournalEntryKind.Find,
        _ => throw new ArgumentException(
            $"A journal entry is recorded as '{word}', which is not a kind this build has.",
            nameof(word)),
    };
}

/// <summary>Every dated line of one party's history, as a save records them.</summary>
/// <remarks>
/// <para>
/// <b>This is party state and not place state.</b> The history belongs to the party that lived it, so nothing
/// here is keyed by where the party stood, a place whose population is restored touches none of it, and a
/// party that walks out of the place an entry happened in keeps the entry.
/// </para>
/// <para>
/// <b>The section is the whole history and never the quest state again.</b> A line about an errand records
/// that the errand was offered, taken, or finished on a day; the stage, the progress, and the objectives stay
/// in the quest section where their owner keeps them, so a journal is a record of when things happened
/// rather than a second copy of what is true.
/// </para>
/// </remarks>
/// <param name="Entries">The lines, in the order the party lived them.</param>
public sealed record JournalSave(IReadOnlyList<JournalEntrySave>? Entries = null)
{
    /// <summary>The lines, in the order the party lived them.</summary>
    public IReadOnlyList<JournalEntrySave> Entries { get; init; } = Entries ?? [];

    /// <summary>A party that has written nothing down.</summary>
    public static JournalSave None { get; } = new();

    /// <summary>Whether this records nothing, which is what a party that has just begun has.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Every contradiction between this history and the session it would be resumed into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The problems are contradictions rather than rules the product might refuse, because a load must not
    /// re-judge a history the product itself wrote: a kind word this build does not have came from something
    /// else, a line with no subject or no words says nothing, a line dated after the game time the save had
    /// reached happened in a future the session never lived, and two lines about one event mean the dedupe
    /// this owner exists for did not hold.
    /// </para>
    /// <para>
    /// <b>A line whose place the world no longer carries is deliberately not one of them.</b> A journal
    /// outlives the places it happened in: what the party did in a place is a fact about the party, and a
    /// world that stopped carrying a region is a reason to leave the record standing rather than to refuse
    /// the expedition that holds it.
    /// </para>
    /// </remarks>
    /// <param name="elapsedMilliseconds">The game time the save's clock had reached.</param>
    /// <param name="limit">How many lines a history this build keeps may hold.</param>
    /// <returns>Every problem found, in the order the lines are recorded.</returns>
    public IReadOnlyList<string> Problems(long elapsedMilliseconds, int limit)
    {
        List<string> problems = [];
        if (Entries.Count > limit)
        {
            problems.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the journal holds {Entries.Count} entries and this build keeps at most {limit}, so the document was written by something that does not bound a history"));
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JournalEntrySave entry in Entries)
        {
            if (entry.Kind is not ("place" or "quest-offered" or "quest-taken" or "quest-finished" or "rank" or "meeting" or "find"))
            {
                problems.Add($"a journal entry is recorded as '{entry.Kind}', which is not a kind this build has");
            }

            if (string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Subject) || string.IsNullOrWhiteSpace(entry.Text))
            {
                problems.Add($"a journal entry of kind '{entry.Kind}' records no source, subject, or words, so it would read as an empty line");
            }

            if (entry.ElapsedMilliseconds < 0 || entry.ElapsedMilliseconds > elapsedMilliseconds)
            {
                problems.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"a journal entry happened {entry.ElapsedMilliseconds} ms into the session and the save had reached {elapsedMilliseconds} ms, so it is dated in a future the party never lived"));
            }

            string identity = $"{entry.Kind}|{entry.Subject}|{entry.Place}";
            if (!seen.Add(identity))
            {
                problems.Add($"the journal records '{entry.Text}' twice, so the same event would be two entries");
            }
        }

        return problems;
    }
}
