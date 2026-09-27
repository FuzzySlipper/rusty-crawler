using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Knowledge;

/// <summary>
/// The one owner of what a party has learned: the facts it can look up again, dated by the one clock, each
/// learned once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Knowledge is the party's and is never the world's.</b> Every note here belongs to the band that
/// learned it, so it is untouched by anything the world does to a place: a population the clock restores,
/// a container the party emptied, a fixture a use changed, and a place the party walks out of all live in
/// the world's own per-place state and none of them is a fact about what the party knows. That is why the
/// two are separate owners rather than one: a place's state is a reading of what the world currently is,
/// where knowledge is what the party carries out of it, and a design that retrofitted the split later
/// would have to guess which of a place's fields were really the party's.
/// </para>
/// <para>
/// <b>Learning the same fact twice is one fact.</b> Every reporter reports what it learned as it learned
/// it — a party that mixes a pair a second time, searches a chest again, or is told something it already
/// knows reports it the same way — so this is where that becomes one note: a report whose kind, subject,
/// and place are already known is not written again. Nothing beside the notes keeps a set of keys, because
/// a second copy of what is known would be the leak the bound below exists to prevent.
/// </para>
/// <para>
/// <b>The knowledge is bounded, and forgetting is what the bound means.</b> An unbounded set of notes is a
/// leak: it grows with every discovery, it is carried whole in every save, and it is rebuilt into every
/// projection. This keeps the most recent <see cref="MaxNotes"/> facts and lets the oldest fall off,
/// exactly as the journal beside it bounds its lines; a fact that has fallen off may be learned again,
/// which is what a record that no longer holds it means.
/// </para>
/// <para>
/// <b>A session without a clock has no knowledge.</b> The clock is what dates every note and what a loaded
/// note is read back against, so a session that holds none composes no owner at all: what its projection
/// publishes is that the mechanism is not there, which is the honest reading rather than a list of facts
/// learned at no particular time.
/// </para>
/// </remarks>
public sealed class PartyKnowledge
{
    /// <summary>How many facts one party's knowledge keeps.</summary>
    /// <remarks>
    /// A structural bound rather than a tuning value, and deliberately above what a game states there is to
    /// learn: forgetting a discovery the game itself offers would be worse than keeping a few rows it never
    /// mentions, so a game whose own discovery table is longer states its own bound in its ruleset. It is a
    /// count of facts and not a span of game time, because what makes the set expensive is its length — in
    /// the save, in the projection, and on the screen — rather than its age.
    /// </remarks>
    public const int MaxNotes = 256;

    private readonly IKnowledgeRule _rule;
    private readonly List<KnowledgeNote> _notes = [];
    private readonly GameClock _clock;

    /// <summary>Creates the owner over the session's clock and the game's own words.</summary>
    /// <remarks>
    /// The clock is what dates every note, so knowledge without one cannot exist: a note with no date would
    /// be a fact learned at no particular time, and knowing when the party learned something is part of
    /// what it knows. A save's notes arrive as elapsed game time and are read back against this clock,
    /// which is why a loaded note reads as the day it was learned.
    /// </remarks>
    /// <param name="rule">What this game calls its discoveries and what it counts as worth keeping.</param>
    /// <param name="clock">The session's one clock, which dates every note.</param>
    /// <param name="save">What a save recorded, or null for a party that has learned nothing.</param>
    /// <exception cref="ArgumentNullException">The rule or the clock is missing.</exception>
    public PartyKnowledge(IKnowledgeRule rule, GameClock clock, KnowledgeSave? save = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (save is null) return;
        foreach (KnowledgeNoteSave recorded in save.Notes) _notes.Add(KnowledgeNoteSave.Read(recorded, clock));
        Bound();
    }

    /// <summary>What this game calls its discoveries and what it counts as worth keeping.</summary>
    public IKnowledgeRule Rule => _rule;

    /// <summary>Every fact the party knows, oldest first, which is the order it learned them in.</summary>
    public IReadOnlyList<KnowledgeNote> Notes => _notes;

    /// <summary>Whether the party already knows one fact.</summary>
    /// <param name="report">The fact.</param>
    /// <returns>Whether a note already records it.</returns>
    public bool Knows(KnowledgeReport report)
    {
        foreach (KnowledgeNote note in _notes)
        {
            if (note.Records(report)) return true;
        }

        return false;
    }

    /// <summary>Writes one reported discovery down, unless it is not worth keeping or is already known.</summary>
    /// <remarks>
    /// Three answers decide one note, and each belongs to one owner: whether the discovery is worth keeping
    /// is the game's, whether the same fact is already known is this owner's, and how the note reads is
    /// this owner's too, composed from the game's own phrase for that kind of discovery and the name the
    /// reporting owner gave the thing. A report that is refused is not an error and not a contradiction: an
    /// ordinary sword found in a chest is a fact this game's knowledge does not keep.
    /// </remarks>
    /// <param name="report">What was learned, as the owner that learned it stated it.</param>
    /// <returns>Whether a note was written, which is false for a fact already known.</returns>
    public bool Record(KnowledgeReport report)
    {
        // Whether the discovery is worth keeping is the game's answer and whether the same fact is already
        // known is this owner's, and both are asked before anything is worded or written.
        if (!_rule.WorthLearning(report)) return false;
        if (Knows(report)) return false;

        // The name is the reporting owner's own words for the thing, so a note is the game's voice around
        // the owner's own name for what was learned rather than a second naming of it by this mechanism.
        string phrase = _rule.Phrase(report.Kind);
        string text = phrase.Length == 0 ? report.Name : $"{phrase} {report.Name}";
        _notes.Add(new KnowledgeNote(
            report.Kind,
            report.Source,
            report.Subject,
            text,
            _clock.Elapsed.Milliseconds,
            _clock.Now,
            report.Place));
        Bound();
        return true;
    }

    /// <summary>Reads the party's knowledge into the product's one current save schema.</summary>
    /// <remarks>
    /// What is captured is the facts and nothing else: what a fixture currently does and what the world
    /// currently holds are readings of their owners, so a save carries what the party learned rather than a
    /// copy of the place it learned it in.
    /// </remarks>
    /// <returns>The knowledge as a save records it.</returns>
    public KnowledgeSave Capture() => new([.. _notes.Select(KnowledgeNoteSave.Record)]);

    /// <summary>Drops the oldest notes until the knowledge is within its bound.</summary>
    private void Bound()
    {
        while (_notes.Count > MaxNotes) _notes.RemoveAt(0);
    }
}
