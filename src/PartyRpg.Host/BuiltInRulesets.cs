using PartyRpg.Kit.Rulesets;
using PartyRpg.Rulesets.MightAndMagic7;

namespace PartyRpg.Host;

/// <summary>
/// The host's compiled ruleset selection. The host may choose a built-in ruleset here and nowhere
/// else, and it never interprets what the ruleset decides.
/// </summary>
internal static class BuiltInRulesets
{
    /// <summary>The ruleset the product launches.</summary>
    internal static IGameRuleset Default => MightAndMagic7Ruleset.Instance;
}
