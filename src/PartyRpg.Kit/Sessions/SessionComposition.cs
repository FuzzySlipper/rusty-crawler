using PartyRpg.Kit.Rulesets;

namespace PartyRpg.Kit.Sessions;

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
    /// <summary>Reads the composition a ruleset declares.</summary>
    public static SessionComposition From(IGameRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return new SessionComposition(ruleset.Id, ruleset.Title);
    }
}
