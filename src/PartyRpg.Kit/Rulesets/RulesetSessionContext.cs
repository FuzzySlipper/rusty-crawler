using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;

namespace PartyRpg.Kit.Rulesets;

/// <summary>
/// What the host hands a ruleset when it composes a session. It grows as stones land — engine
/// services, content, and tuning arrive with the mechanisms that consume them, and nothing is added
/// here before something needs it.
/// </summary>
/// <param name="Projection">Where the session publishes its presentation.</param>
/// <param name="Selection">The game bundle the host selected, when it selected one.</param>
/// <param name="Content">The validated content the session may build its world from, when a bundle supplied any.</param>
/// <param name="Time">Where elapsed game days come from, when a clock has been wired.</param>
/// <param name="Engine">
/// The engine's services, when the host is running inside one. A ruleset that composes movement needs
/// more than one of them — the spatial service owns collision and resolves the party's steps, and the
/// content owner retains the collision artifact a place provides — so the engine's own context is handed
/// over whole rather than growing this record a member per service family. It is null in a composition
/// that has no engine, and a ruleset that needs one says so by composing no movement.
/// </param>
/// <param name="Movement">
/// The movement controls the host declares, when it declares any. Names belong to the product identity
/// the host owns; what a rate a held turn control turns at is worth belongs to the ruleset's tuning, so
/// the host states which intents carry the controls and the ruleset composes the reader over them.
/// </param>
public sealed record RulesetSessionContext(
    IUiProjectionChannel Projection,
    BundleSelection Selection = default,
    ContentCatalog? Content = null,
    IWorldTimeSource? Time = null,
    IEngineContext? Engine = null,
    MovementIntentNames? Movement = null);
