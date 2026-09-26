namespace PartyRpg.Kit.Magic;

/// <summary>How far a build expresses one spell's effect, as its coverage is reported.</summary>
/// <remarks>
/// <para>
/// The three states are the honest vocabulary of a build that applies effects by category first: a spell
/// whose category is applied through the owner that holds the state it changes is
/// <see cref="Implemented"/>; one that changes that state more coarsely than the game does — an area spell
/// aimed at one actor, a party-wide ward standing in for a per-character one, numbers this build states
/// rather than the donor's — is <see cref="Approximated"/> and says how; and one whose casting changes nothing
/// this build reads is <see cref="NotYet"/>, naming what is missing and who owns it.
/// </para>
/// <para>
/// <b>Not yet is a routed fact, not an excuse.</b> A spell that cannot be applied is still learned, still
/// paid for, and still delivered to the effect seam; what this adds is the name of the missing owner, so the
/// gap is a receiving task rather than a silence.
/// </para>
/// </remarks>
public enum SpellEffectCoverageState
{
    /// <summary>The category's own path changes the state this spell changes, through that state's owner.</summary>
    Implemented,

    /// <summary>The path applies, but more coarsely than the game does, which the note states.</summary>
    Approximated,

    /// <summary>Nothing this build reads changes; the note names what is missing and its receiver.</summary>
    NotYet,
}

/// <summary>The one vocabulary coverage is spelled with, on the wire and in the report.</summary>
public static class SpellEffectCoverageStates
{
    /// <summary>The wire name for one state.</summary>
    /// <param name="state">The state to spell.</param>
    /// <returns>The word the report and the wire spell it as.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state has no wire name.</exception>
    public static string WireName(SpellEffectCoverageState state) => state switch
    {
        SpellEffectCoverageState.Implemented => "implemented",
        SpellEffectCoverageState.Approximated => "approximated",
        SpellEffectCoverageState.NotYet => "not yet",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown spell effect coverage."),
    };
}

/// <summary>How far one spell's effect is expressed, and what that leaves out.</summary>
/// <remarks>
/// A note is written for a person and names the difference; a receiver names the owner that would close it,
/// and is empty only where nothing is missing. Both are published so the coverage report is generated from
/// the same answer the effect path applies by, and cannot drift from it.
/// </remarks>
/// <param name="State">How far this build expresses the spell's effect.</param>
/// <param name="Note">What the spell does here, and what is coarser or missing.</param>
/// <param name="Receiver">Who owns what is missing, empty when nothing is.</param>
public readonly record struct SpellEffectCoverage(SpellEffectCoverageState State, string Note, string Receiver)
{
    /// <summary>This spell's effect is expressed through its category's own owner.</summary>
    /// <param name="note">What it does, in a sentence.</param>
    /// <returns>The coverage.</returns>
    /// <exception cref="ArgumentException">The note is blank, which would report nothing about the spell.</exception>
    public static SpellEffectCoverage Implemented(string note) => new(SpellEffectCoverageState.Implemented, Require(note), string.Empty);

    /// <summary>This spell's effect is expressed more coarsely than the game does.</summary>
    /// <param name="note">How it is coarser, in a sentence.</param>
    /// <returns>The coverage.</returns>
    /// <exception cref="ArgumentException">The note is blank, which would report nothing about the difference.</exception>
    public static SpellEffectCoverage Approximated(string note) => new(SpellEffectCoverageState.Approximated, Require(note), string.Empty);

    /// <summary>Nothing this build reads changes, and this is the owner that would close the gap.</summary>
    /// <param name="note">What is missing, in a sentence.</param>
    /// <param name="receiver">Which owner would have to supply it, which must not be blank.</param>
    /// <returns>The coverage.</returns>
    /// <exception cref="ArgumentException">The note or the receiver is blank, which would leave the gap unnamed or unrouted.</exception>
    public static SpellEffectCoverage NotYet(string note, string receiver) =>
        new(SpellEffectCoverageState.NotYet, Require(note), Require(receiver));

    /// <inheritdoc />
    public override string ToString() =>
        Receiver.Length == 0 ? $"{SpellEffectCoverageStates.WireName(State)}: {Note}" : $"{SpellEffectCoverageStates.WireName(State)}: {Note} (receiver: {Receiver})";

    private static string Require(string text) =>
        !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new ArgumentException("A coverage row states what a spell does or what it leaves out, so its note cannot be blank.", nameof(text));
}
