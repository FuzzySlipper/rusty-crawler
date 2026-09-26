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
/// <param name="Service">
/// The service controls the host declares, when it declares any, stated for the same reason and in the same
/// shape as the others: which intent leaves a counter, and which payload contract a service screen's
/// commands arrive on. Entering a service is deliberately not among them — the party enters by using the
/// person it is talking to — so a host declares the way out and the commands, and nothing else.
/// </param>
/// <param name="Rest">
/// The stop controls the host declares, when it declares any, stated for the same reason and in the same
/// shape as the others: which intent rests, which makes camp, which waits until dawn, which waits an hour,
/// which waits a short interval, and which payload contract a screen's own stop buttons arrive on. Every
/// stop is its own control because every stop is a different act, and without them a session never stops on
/// its own — which is what a product that offers no stop controls gets.
/// </param>
/// <param name="Conversation">
/// The conversation controls the host declares, when it declares any, stated for the same reason and in the
/// same shape as the others: which intent ends a conversation, and which payload contract a screen's own
/// choices arrive on. Entering a conversation is deliberately not among them — the party speaks with
/// somebody by using the person it faces — so a host declares the way out and the choices, and nothing else.
/// </param>
/// <param name="Combat">
/// The act control the host declares, when it declares any, stated for the same reason and in the same shape
/// as the others: which intent and which payload action order the party to attack. One control rather than
/// one per kind of attack, because what a member does with it is this game's answer about that member and
/// not something a player picks per press. Without it a session never attacks on its own — the fight is
/// still composed and still reads the world — which is what a product that declares no act control gets.
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
    SessionStart Start = SessionStart.Fresh,
    ServiceIntentNames? Service = null,
    RestIntentNames? Rest = null,
    ConversationIntentNames? Conversation = null,
    CombatIntentNames? Combat = null);
