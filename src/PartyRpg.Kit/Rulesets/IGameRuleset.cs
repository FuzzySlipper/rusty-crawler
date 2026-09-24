using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.Rulesets;

/// <summary>
/// A compiled ruleset: the concrete policy a product builds its session from. Rulesets are compiled
/// into the product composition, so this seam is answered at build time and never discovered at runtime.
/// </summary>
public interface IGameRuleset
{
    /// <summary>The ruleset's stable identity.</summary>
    RulesetId Id { get; }

    /// <summary>The ruleset's display title, used by a projection and never by a save or an identity check.</summary>
    string Title { get; }

    /// <summary>Builds this ruleset's session over the mechanisms the kit supplies.</summary>
    IGameSession CreateSession(RulesetSessionContext context);
}
