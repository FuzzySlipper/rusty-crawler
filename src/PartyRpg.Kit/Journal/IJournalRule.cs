namespace PartyRpg.Kit.Journal;

/// <summary>
/// What this game states about a party's journal: what its books are called, how its lines read, and what is
/// worth writing down at all.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to the journal, and it is deliberately three answers rather than
/// one. <see cref="Book"/> is the words: the books' names and what each says when it holds nothing or
/// cannot be filled, which is presentation meaning and belongs to the game whose manual names them.
/// <see cref="Phrase"/> is how a line about one kind of event begins — "entered", "took on", "met" — which is
/// the sentence a person reads in their own record. <see cref="WorthRecording"/> is the threshold: whether an
/// event is worth a line at all, which is the one judgement a game makes about its own journal.
/// </para>
/// <para>
/// <b>Nothing here changes anything and nothing here dates anything.</b> Whether a line is written, what it
/// says, and when it happened are the owner's own acts; a rule that decided a place had been entered or
/// stamped a day on an entry would be a second writer of one fact.
/// </para>
/// <para>
/// <b>A session whose ruleset states no journal has none.</b> A product composed without one holds no record
/// of where the party has been or what it did, and its projection says the mechanism is not there rather
/// than showing empty books.
/// </para>
/// </remarks>
public interface IJournalRule
{
    /// <summary>
    /// The books this game's journal keeps, in the order a panel shows them. Each is a kind of page the kit
    /// knows how to fill from its owner; which of them a game keeps, and in what order, is the game's.
    /// </summary>
    IReadOnlyList<JournalBookKind> Books { get; }

    /// <summary>What this game calls one of its books, and what it says when it holds nothing.</summary>
    /// <param name="book">Which book.</param>
    /// <returns>The book's words.</returns>
    JournalBookWords Book(JournalBookKind book);

    /// <summary>How a line about one kind of event begins, in this game's own words.</summary>
    /// <remarks>
    /// The phrase is composed with what the reporting owner called the thing, so the line reads in the game's
    /// voice around the owner's own name for it: the phrase never repeats the name and never states it.
    /// </remarks>
    /// <param name="kind">What kind of thing happened.</param>
    /// <returns>The words a line about it opens with.</returns>
    string Phrase(JournalEntryKind kind);

    /// <summary>Whether an event is worth writing down at all.</summary>
    /// <remarks>
    /// <para>
    /// This is the game's threshold over its own record: a journal that wrote a line for every sword found
    /// would bury what the party actually did, and what is worth a line is a decision about one game's world
    /// rather than a kit mechanism's.
    /// </para>
    /// <para>
    /// <b>A report that is not worth recording is still a report.</b> The owner reports every event and the
    /// journal asks here; answering no leaves the journal exactly as it was, and the reporter is never told
    /// to stop reporting — deduplication and notability stay in one place rather than in every reporter.
    /// </para>
    /// </remarks>
    /// <param name="journalEvent">The event as the owner that reported it stated it.</param>
    /// <returns>Whether it belongs in the party's record.</returns>
    bool WorthRecording(JournalEvent journalEvent);
}
