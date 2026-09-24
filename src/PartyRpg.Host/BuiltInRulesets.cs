using PartyRpg.Kit.Rulesets;
using PartyRpg.Rulesets.MightAndMagic7;

namespace PartyRpg.Host;

/// <summary>
/// The host's explicit catalog of compiled rulesets and its default selection. The host may choose a
/// built-in ruleset here and nowhere else, and it never interprets what the ruleset decides.
/// </summary>
internal static class BuiltInRulesets
{
    /// <summary>Every compiled ruleset this product can select.</summary>
    internal static IReadOnlyList<IGameRuleset> All { get; } = [MightAndMagic7Ruleset.Instance];

    /// <summary>The ruleset the product launches when nothing else selects one.</summary>
    internal static IGameRuleset Default => MightAndMagic7Ruleset.Instance;
}
