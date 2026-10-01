using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Journal;

/// <summary>
/// The one owner of what a party has written down: its dated history, and the words its ruleset gives it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A journal is a record of when things happened, never a second copy of what is true.</b> It holds dated
/// lines and nothing else: the errands a party stands with are read from the quest owner, the places it
/// knows from the world, the day from the clock, and its own lines from here. That is what lets a quest's
/// stage move, a place's population be restored, or a day pass without the journal being told, and it is why
/// nothing in this file names a quest, a place, a person, or a rank.
/// </para>
/// <para>
/// <b>Every line is written by whoever owns the event.</b> The session reports the places the world puts the
/// party in, the errands its quest owner offered, took, and finished, the ranks its progression owner
/// granted, and the people its conversation opened onto; a ruleset reports what its own searches yielded.
/// They all reach the same entry point, and this owner is the one place that decides whether the event is
/// news, how it reads, and which day it happened on — so no reporter dates a line, words it, or checks
/// whether it is already written down.
/// </para>
/// <para>
/// <b>A session without a clock has no journal.</b> The clock is what dates every line and what a loaded line
/// is read back against, so a session that holds none composes no journal at all: what its projection
/// publishes is that the mechanism is not there, which is the honest reading rather than a list of things
/// that happened at no particular time.
/// </para>
/// </remarks>
public sealed class PartyJournal
{
    private readonly IJournalRule _rule;
    private readonly JournalHistory _history;

    /// <summary>Creates the owner over the session's clock and the game's own words.</summary>
    /// <param name="rule">What this game calls its books and what it counts as worth writing down.</param>
    /// <param name="clock">The session's one clock, which dates every line.</param>
    /// <param name="save">What a save recorded, or null for a party that has written nothing down.</param>
    /// <exception cref="ArgumentNullException">The rule or the clock is missing.</exception>
    public PartyJournal(IJournalRule rule, GameClock clock, JournalSave? save = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _history = new JournalHistory(clock ?? throw new ArgumentNullException(nameof(clock)), save?.Entries);
    }

    /// <summary>What this game calls its books and what it counts as worth writing down.</summary>
    public IJournalRule Rule => _rule;

    /// <summary>
    /// The change stamp this owner took when the history it keeps last changed, or when it was made: a reader that
    /// kept what it built beside it reads the owner again only when it has moved (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp => _history.Stamp;

    /// <summary>Every line the party has written down, oldest first.</summary>
    public IReadOnlyList<JournalEntry> Entries => _history.Entries;

    /// <summary>Writes one reported event down, unless it is not worth a line or is already there.</summary>
    /// <remarks>
    /// Three answers decide one line, and each belongs to one owner: whether the event is worth recording is
    /// the game's, whether the same event is already written down is the history's, and how the line reads is
    /// this owner's, composed from the game's own phrase for that kind of event and the name the reporting
    /// owner gave the thing. A report that is refused is not an error and not a contradiction: an ordinary
    /// sword found in a chest is an event this game's journal does not keep.
    /// </remarks>
    /// <param name="journalEvent">What happened, as the owner that reported it stated it.</param>
    /// <returns>Whether a line was written.</returns>
    public bool Record(JournalEvent journalEvent)
    {
        if (!_rule.WorthRecording(journalEvent)) return false;

        // The name is the reporting owner's own words for the thing, so a line is the game's voice around the
        // owner's own name for what happened rather than a second naming of it by this mechanism.
        string phrase = _rule.Phrase(journalEvent.Kind);
        string text = phrase.Length == 0 ? journalEvent.Name : $"{phrase} {journalEvent.Name}";
        return _history.Write(journalEvent, text);
    }

    /// <summary>Reads the party's history into the product's one current save schema.</summary>
    /// <remarks>
    /// What is captured is the dated lines and nothing else: the books a screen shows are readings of the
    /// owners that hold them, so a save carries the party's own record rather than a copy of the world, the
    /// quests, or the day it was written on.
    /// </remarks>
    /// <returns>The history as a save records it.</returns>
    public JournalSave Capture() => _history.Capture();
}
