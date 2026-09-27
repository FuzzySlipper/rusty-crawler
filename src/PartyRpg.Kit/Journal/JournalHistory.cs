using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Journal;

/// <summary>
/// The party's own history: every dated line it has written down, in the order it lived them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The history belongs to the party and to nothing else.</b> No line is keyed by where the party stood, so
/// a place whose population the world restores touches none of it, a party that walks out of the place an
/// entry happened in keeps the entry, and the whole list survives a save because it is the party's own state
/// rather than a reading of the world's.
/// </para>
/// <para>
/// <b>The same event is one entry.</b> Every reporter reports what happened as it happens — a party standing
/// in a place for a hundred updates reports it a hundred times — so this is where that becomes one line:
/// a report whose kind, subject, and place are already written down is not written again. What an event
/// <em>is</em> is therefore decided in one owner, exactly as what a quest's stage is is decided in one owner.
/// </para>
/// <para>
/// <b>The history is bounded, and forgetting is what the bound means.</b> An unbounded journal is a leak: it
/// grows with every place entered and every person met, it is carried whole in every save, and it is rebuilt
/// into every projection. This keeps the most recent <see cref="MaxEntries"/> lines and lets the oldest fall
/// off the back, exactly as a paper journal runs out of pages. A line that has fallen off is forgotten — the
/// party may enter that place again and a new line is written, which is what a record that no longer holds
/// the old one means — and the dedupe above holds over what the history actually holds rather than over a
/// set of keys kept beside it, because a second set would be the leak the bound exists to prevent.
/// </para>
/// </remarks>
public sealed class JournalHistory
{
    /// <summary>How many dated lines one party's history keeps.</summary>
    /// <remarks>
    /// A structural bound rather than a tuning value: the operator's world holds a few hundred places, people,
    /// and errands, so a party that has written more than this has had a very long game, and the oldest lines
    /// are the ones a person would have torn out. It is deliberately a count of lines and not a span of game
    /// time, because what makes the list expensive is its length — in the save, in the projection, and on the
    /// screen — and not its age.
    /// </remarks>
    public const int MaxEntries = 256;

    private readonly List<JournalEntry> _entries = [];
    private readonly GameClock _clock;

    /// <summary>Creates the history over the session's one clock, optionally holding what a save recorded.</summary>
    /// <remarks>
    /// The clock is what dates every line, so a history without one cannot exist: a journal whose entries had
    /// no dates would be a list of things that happened at no particular time, and the whole point of the
    /// record is when the party was where. A save's lines arrive as elapsed game time and are read back
    /// against this clock, which is why a loaded entry reads as the day it happened.
    /// </remarks>
    /// <param name="clock">The session's one clock.</param>
    /// <param name="recorded">The lines a save recorded, or null for a party that has written nothing down.</param>
    /// <exception cref="ArgumentNullException">No clock was supplied.</exception>
    public JournalHistory(GameClock clock, IReadOnlyList<JournalEntrySave>? recorded = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (recorded is null) return;
        foreach (JournalEntrySave entry in recorded) _entries.Add(JournalEntrySave.Read(entry, clock));
        Bound();
    }

    /// <summary>Every line the history holds, oldest first, which is the order the party lived them.</summary>
    public IReadOnlyList<JournalEntry> Entries => _entries;

    /// <summary>Whether one event is already written down.</summary>
    /// <param name="journalEvent">The report.</param>
    /// <returns>Whether a line already records it.</returns>
    public bool Holds(JournalEvent journalEvent)
    {
        foreach (JournalEntry entry in _entries)
        {
            if (entry.Records(journalEvent)) return true;
        }

        return false;
    }

    /// <summary>Writes one event down, unless the same event is already there.</summary>
    /// <remarks>
    /// The line is composed by the caller rather than here because the words are the ruleset's and the act of
    /// wording an entry is the journal owner's; what this owner decides is whether the event is one the
    /// history already holds and which lines the bound drops.
    /// </remarks>
    /// <param name="journalEvent">What happened.</param>
    /// <param name="text">The line a person reads.</param>
    /// <returns>Whether a line was written.</returns>
    public bool Write(JournalEvent journalEvent, string text)
    {
        if (Holds(journalEvent)) return false;
        _entries.Add(new JournalEntry(
            journalEvent.Kind,
            journalEvent.Source,
            journalEvent.Subject,
            text,
            _clock.Elapsed.Milliseconds,
            _clock.Now,
            journalEvent.Place));
        Bound();
        return true;
    }

    /// <summary>Reads every line into the product's one current save schema.</summary>
    /// <returns>The history as a save records it.</returns>
    public JournalSave Capture() => new([.. _entries.Select(JournalEntrySave.Record)]);

    /// <summary>Drops the oldest lines until the history is within its bound.</summary>
    private void Bound()
    {
        while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
    }
}
