using PartyRpg.Kit.Rulesets;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// How the party a session plays came into being: which of the starts its composition took.
/// </summary>
/// <remarks>
/// A new session takes exactly one of two starts — the player creates the party, or the scenario fixes it —
/// and a resumed one takes neither, because the party is the one the save holds. Which one a session is on is
/// a fact the panel and a live check read, so it is stated by the session from the start it was handed rather
/// than worked out from which blocks happen to be filled.
/// </remarks>
public enum SessionPartyStart
{
    /// <summary>The player creates the party: the session begins in creation.</summary>
    Creation,

    /// <summary>The scenario fixes the party: the session begins playing it, with no creation screen.</summary>
    Scenario,

    /// <summary>The party is the one a save held: the session resumed it.</summary>
    Resumed,
}

/// <summary>
/// The identity a session presents: which compiled ruleset is running, and the title to show for it.
/// The title is presentation; the identity is the value anything durable may depend on.
/// </summary>
/// <param name="Ruleset">The compiled ruleset this session was built from.</param>
/// <param name="Title">The ruleset's display title.</param>
/// <param name="Bundle">The game bundle the session was started from, when one was selected.</param>
/// <param name="ContentPacks">How many content packs the selected bundle resolved to.</param>
public readonly record struct SessionComposition(RulesetId Ruleset, string Title, string? Bundle = null, int ContentPacks = 0)
{
    /// <summary>
    /// Which start the session's party took. The session states it from the start it was composed with, so a
    /// value a caller set here is replaced by the one the session actually took.
    /// </summary>
    public SessionPartyStart PartyStart { get; init; }

    /// <summary>Reads the composition a ruleset declares.</summary>
    public static SessionComposition From(IGameRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return new SessionComposition(ruleset.Id, ruleset.Title);
    }
}
