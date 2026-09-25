namespace PartyRpg.Kit.Persistence;

/// <summary>
/// A session could not be captured, written, read, or rebuilt, with every missing or contradictory part of
/// the save named.
/// </summary>
/// <remarks>
/// The problems are a list rather than one message because a save that cannot be loaded is usually wrong in
/// more than one place: reporting all of them lets a defective save be fixed in one pass, and lets a player
/// be told what is actually wrong instead of the first thing a reader happened to check. A failure with no
/// problems is a failure of the surrounding store rather than of the document — bytes that could not be
/// read at all, or a slot that holds nothing.
/// </remarks>
public sealed class SessionSaveException : Exception
{
    /// <summary>Creates the failure.</summary>
    /// <param name="message">What happened, in the terms of the save that could not be used.</param>
    /// <param name="problems">Every part of the save that is missing or contradictory; empty when the document itself is not the problem.</param>
    public SessionSaveException(string message, IReadOnlyList<string>? problems = null)
        : base(message)
    {
        Problems = problems ?? [];
    }

    /// <summary>Every part of the save that is missing or contradictory.</summary>
    public IReadOnlyList<string> Problems { get; }
}
