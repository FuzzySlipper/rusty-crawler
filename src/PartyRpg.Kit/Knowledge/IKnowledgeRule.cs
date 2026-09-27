namespace PartyRpg.Kit.Knowledge;

/// <summary>
/// What this game states about what its party learns: how a note about each kind of discovery reads, and
/// what it counts as worth keeping at all.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to the knowledge owner, and it is deliberately two answers
/// rather than one. <see cref="Phrase"/> is how a note about one kind of discovery begins — "Learned",
/// "Read", "Found" — which is the sentence a person reads in the book their discoveries are kept in.
/// <see cref="WorthLearning"/> is the threshold: whether a reported fact is one this game keeps at all,
/// which is the judgement a game makes about its own knowledge rather than a kit mechanism's.
/// </para>
/// <para>
/// <b>Nothing here changes anything and nothing here dates anything.</b> Whether a note is written, how it
/// reads, and when it was learned are the owner's own acts; a rule that decided a mixture had been made or
/// stamped a day on a note would be a second writer of one fact.
/// </para>
/// <para>
/// <b>A session whose ruleset states no knowledge keeps none.</b> A product composed without one learns
/// nothing it can look up again, and what it publishes says the mechanism is not there rather than showing
/// an empty book a player would read as "there is nothing here to learn".
/// </para>
/// </remarks>
public interface IKnowledgeRule
{
    /// <summary>How a note about one kind of discovery begins, in this game's own words.</summary>
    /// <remarks>
    /// The phrase is composed with what the reporting owner called the thing, so the note reads in the
    /// game's voice around the owner's own name for it: the phrase never repeats the name and never states
    /// it.
    /// </remarks>
    /// <param name="kind">What kind of thing was learned.</param>
    /// <returns>The words a note about it opens with.</returns>
    string Phrase(KnowledgeKind kind);

    /// <summary>Whether a reported fact is worth keeping at all.</summary>
    /// <remarks>
    /// <para>
    /// This is the game's threshold over its own knowledge: a party that kept a note of every sword it
    /// found would bury what it actually knows, and what is worth knowing is a decision about one game's
    /// world rather than a kit mechanism's.
    /// </para>
    /// <para>
    /// <b>A report that is not worth keeping is still a report.</b> The owner reports every discovery and
    /// the knowledge owner asks here; answering no leaves the notes exactly as they were, and the reporter
    /// is never told to stop reporting — deduplication and notability stay in one place rather than in
    /// every reporter.
    /// </para>
    /// </remarks>
    /// <param name="report">The fact as the owner that reported it stated it.</param>
    /// <returns>Whether it belongs in what the party knows.</returns>
    bool WorthLearning(KnowledgeReport report);
}
