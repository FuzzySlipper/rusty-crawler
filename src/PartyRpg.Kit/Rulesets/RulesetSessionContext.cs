using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;

namespace PartyRpg.Kit.Rulesets;

/// <summary>
/// What the host hands a ruleset when it composes a session. It grows as stones land — engine
/// services, content, and tuning arrive with the mechanisms that consume them, and nothing is added
/// here before something needs it.
/// </summary>
/// <param name="Projection">Where the session publishes its presentation.</param>
/// <param name="Selection">The game bundle the host selected, when it selected one.</param>
public sealed record RulesetSessionContext(IUiProjectionChannel Projection, BundleSelection Selection = default);
