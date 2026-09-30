namespace PartyRpg.Kit.Persistence;

/// <summary>
/// One part of a save that is missing or contradicts what it would be loaded into, named by what kind of
/// contradiction it is, what it is about, and the sentence a person reads.
/// </summary>
/// <remarks>
/// A code is what a caller and a test branch on and the text is what a person reads, so which problem a save
/// had is decided by <see cref="Code"/> and <see cref="Subject"/> rather than by matching an English sentence
/// that may be reworded. The text is still the whole of what a player is told, which is why the record reads
/// as its text wherever it is joined into a message.
/// </remarks>
/// <param name="Code">A stable code naming the kind of contradiction, one of <see cref="SaveCodes"/>.</param>
/// <param name="Subject">
/// What the problem is about — a place id, a quest id, a member, an item, a journal line, a note, a deadline,
/// a named entry, or a part of the session — or empty when it is about the whole document.
/// </param>
/// <param name="Text">The sentence a person reads.</param>
public sealed record SaveProblem(string Code, string Subject, string Text)
{
    /// <summary>The sentence a person reads, so a joined list of problems reads as the sentences do.</summary>
    /// <returns>The problem's text.</returns>
    public override string ToString() => Text;
}
