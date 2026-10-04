using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using Rusty.Engine;
using Rusty.Engine.Interaction;
using KitInteractionTarget = PartyRpg.Kit.Interaction.InteractionTarget;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// What the party is facing and what using it did, as the panel needs it.
/// </summary>
/// <remarks>
/// <para>
/// These are the interaction mechanism's own facts copied into one presentation value, never a second
/// opinion about them: the target, its disposition, verb and state, how far it stands, why the reticle holds or refuses
/// it, what it requires, and the outcome of the last use while that result still addresses this context. Nothing here
/// re-derives a target or judges a requirement, which is what lets a person tell "there is nothing here"
/// from "that door needs a key the party does not carry".
/// </para>
/// <para>
/// A session with no world, or one whose ruleset composed no interaction policy, has no facts at all;
/// <see cref="None"/> is that state, so the panel says the mechanism is not there instead of showing an
/// empty reticle that looks like an empty room.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds an interaction mechanism at all.</param>
/// <param name="Target">The focused target's kind, empty when nothing is focused.</param>
/// <param name="Label">What the focused target is called, empty when nothing is focused.</param>
/// <param name="Disposition">The ruleset's disposition for a being, empty for targets such as doors or containers.</param>
/// <param name="Verb">The use that applies to it, empty when nothing is focused.</param>
/// <param name="State">What the party has already done to it, empty when nothing has.</param>
/// <param name="Distance">How far it stands from the party, zero when nothing is focused.</param>
/// <param name="Reason">Why the reticle holds or refuses what it does, as the selection's own reason.</param>
/// <param name="Requires">What the focused target requires, in the order the checks happen.</param>
/// <param name="Bodies">
/// How many bodies lie in the place the party stands in, which is what the mechanism found beside the place's
/// own content. It is published because a body is something a player has to be able to see is there: a place
/// whose only usable thing is what the party killed would otherwise read exactly like an empty one.
/// </param>
/// <param name="Outcome">What the current context's last use did: <c>none</c>, <c>applied</c>, or <c>refused</c>.</param>
/// <param name="Code">The last refusal's code, empty when the last use applied or none has happened.</param>
/// <param name="Message">What the last use reported, empty before the party has used anything.</param>
/// <param name="Residue">What the last use could not deliver, empty when it delivered all of it.</param>
public sealed record InteractionSnapshot(
    bool Available,
    string Target,
    string Label,
    string Verb,
    string State,
    double Distance,
    string Reason,
    IReadOnlyList<string> Requires,
    string Outcome,
    string Code,
    string Message,
    string Residue,
    int Bodies = 0,
    string Disposition = "")
{
    /// <summary>No interaction mechanism: there is nothing to focus and nothing to use.</summary>
    public static InteractionSnapshot None => new(
        Available: false,
        Target: string.Empty,
        Label: string.Empty,
        Verb: string.Empty,
        State: string.Empty,
        Distance: 0,
        Reason: string.Empty,
        Requires: [],
        Outcome: "none",
        Code: string.Empty,
        Message: string.Empty,
        Residue: string.Empty,
        Bodies: 0,
        Disposition: string.Empty);

    /// <summary>Reads the interaction facts out of the world's mechanism.</summary>
    /// <param name="interaction">The session's interaction mechanism, or null when it holds none.</param>
    /// <returns>The facts the panel shows, or <see cref="None"/> when there is no mechanism.</returns>
    public static InteractionSnapshot From(PartyInteraction? interaction)
    {
        if (interaction is null) return None;

        KitInteractionTarget? context = interaction.FocusedTarget ?? interaction.ContextTarget;
        List<string> requires = [];
        if (context is { } focused)
        {
            foreach (InteractionRequirement requirement in focused.Definition.Requires) requires.Add(requirement.Describe());
        }

        // LastResult is retained by the interaction owner so ActionFeedback can identify the latest answer, but it
        // must not travel under a different target after the Engine selection moves. A null target is the deliberate
        // no-target refusal and remains current while the reticle still has no candidate; a result with a target is
        // current only for that same content identity and state incarnation. The state revision is the product's
        // existing incarnation carried into the Engine candidate revision, so a restored target with the same
        // placement identity cannot inherit a result from the visit that was forgotten.
        InteractionResult? result = interaction.LastResult;
        if (result is not null && !Addresses(result, context)) result = null;
        return new InteractionSnapshot(
            Available: true,
            Target: context?.Definition.Kind.Value ?? string.Empty,
            Label: context?.Definition.Name ?? string.Empty,
            Verb: context is { } verb ? SessionProjection.WireName(verb.Definition.Verb) : string.Empty,
            State: context?.Definition.State ?? string.Empty,
            Distance: interaction.FocusedTarget is not null ? interaction.FocusedDistance : interaction.ContextDistance,
            Reason: SessionProjection.WireName(interaction.FocusReason),
            Requires: requires,
            Outcome: result is null ? "none" : result.IsApplied ? "applied" : "refused",
            Code: result?.Code ?? string.Empty,
            Message: result?.Message ?? string.Empty,
            Residue: result?.Residue ?? string.Empty,
            Bodies: interaction.Bodies.Count,
            Disposition: context?.Definition.Disposition ?? string.Empty);
    }

    /// <summary>Whether an owner's last result still addresses the current Engine context.</summary>
    private static bool Addresses(InteractionResult result, KitInteractionTarget? context) =>
        result.Target is null
            ? context is null
            : context is not null
                && result.Target.Id == context.Id
                && result.Target.State.Revision == context.State.Revision;

    /// <summary>Writes the interaction block: what is faced, what it requires, and what the last use did.</summary>
    /// <remarks>
    /// The requirements are sent as the sentences the ruleset gave them, so a locked door announces what it
    /// needs before anybody tries it, and the panel spells none of them itself.
    /// </remarks>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("available", builder.Boolean(Available)),
            ("target", builder.String(Target)),
            ("label", builder.String(Label)),
            ("disposition", builder.String(Disposition)),
            ("verb", builder.String(Verb)),
            ("state", builder.String(State)),
            ("distance", builder.Number(Distance)),
            ("reason", builder.String(Reason)),
            ("requires", builder.Array([.. Requires.Select(builder.String)])),
            ("bodies", builder.Number(Bodies)),
            ("outcome", builder.String(Outcome)),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)),
            ("residue", builder.String(Residue)));
}
