using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>One thing the party can use: what content placed, what the ruleset made of it, and its state.</summary>
/// <remarks>
/// <para>
/// A target is read from the live world each time the reticle is refreshed, never held in a list somebody
/// wrote down. The number is the identity the engine's selection addresses the target by: it is the
/// placement's own position in its place's content, so it is stable while the party stands in a place, two
/// targets of one place can never share it, and it is never durable state.
/// </para>
/// <para>
/// The definition is the ruleset's answer about the content and the state is what the party has already done
/// to it, and both travel with the target so a use, a projection, and a report agree about it without asking
/// again.
/// </para>
/// </remarks>
/// <param name="Id">The target's content identity: the place it stands in and the placement it is.</param>
/// <param name="Number">The identity the engine's selection addresses it by, which is its index in the place's content.</param>
/// <param name="Placement">The placement content declared, which is where it stands and what it carries.</param>
/// <param name="Definition">What the ruleset made of that placement.</param>
/// <param name="State">What the party has already done to it.</param>
public sealed record InteractionTarget(
    InteractionTargetId Id,
    ulong Number,
    PlacementDefinition Placement,
    InteractionTargetDefinition Definition,
    InteractionTargetState State)
{
    /// <summary>The placement's identity in content, which is the half of a target's identity a save could carry.</summary>
    public PlacementContentId Content => Id.Content;

    /// <summary>Which use applies to it.</summary>
    public InteractionVerb Verb => Definition.Verb;

    /// <inheritdoc />
    public override string ToString() => $"{Definition.Kind} '{Definition.Name}' at {Id}";
}
