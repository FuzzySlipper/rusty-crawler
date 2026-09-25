using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;

namespace PartyRpg.Kit.Rulesets;

/// <summary>
/// How a session begins, as the host decided.
/// </summary>
/// <remarks>
/// This is a composition input rather than something a session works out for itself: a slot with nothing in
/// it and a session playing on from one are the same session to the shell, so only the host knows whether
/// the operator asked to continue. It travels with the context so the ruleset's one composition entry can
/// answer it, and a run that resumes is what the operator asked for rather than a fallback a missing save
/// silently became.
/// </remarks>
public enum SessionStart
{
    /// <summary>Build a new session: its party comes from creation or from the content's scenario.</summary>
    Fresh,

    /// <summary>Resume the session the save slot holds, failing by name when it holds nothing.</summary>
    Resume,
}

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
/// <param name="Creation">
/// The creation controls the host declares, when it declares any, stated for the same reason and in the
/// same shape as the movement controls: which intents carry a confirmation and an acceptance, and which
/// payload contract a screen's choices arrive on. The ruleset composes the reader over those names and
/// this game's own flow.
/// </param>
/// <param name="Save">
/// The save controls the host declares, when it declares any, stated for the same reason as the others:
/// which intent and which payload action ask a session to save at the moment the player asks. Without
/// them a session never saves on its own — a save happens only where a request reaches the boundary —
/// which is what a product that offers no save control yet gets.
/// </param>
/// <param name="Use">
/// The use controls the host declares, when it declares any, stated for the same reason and in the same
/// shape as the others: which intent and which payload action ask the session to use what the party faces.
/// Without them nothing is ever used by itself, which is what a product that offers no use control gets.
/// </param>
/// <param name="Start">
/// How this session begins, as the host decided: a new one, or the save the slot already holds. It is a
/// host decision because only the host knows what the operator asked for, and it is carried here rather
/// than left to the ruleset's own guess so the one composition entry can answer it.
/// </param>
public sealed record RulesetSessionContext(
    IUiProjectionChannel Projection,
    BundleSelection Selection = default,
    ContentCatalog? Content = null,
    IWorldTimeSource? Time = null,
    IEngineContext? Engine = null,
    MovementIntentNames? Movement = null,
    CreationIntentNames? Creation = null,
    SaveIntentNames? Save = null,
    UseIntentNames? Use = null,
    SessionStart Start = SessionStart.Fresh);
