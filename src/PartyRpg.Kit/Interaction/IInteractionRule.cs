namespace PartyRpg.Kit.Interaction;

/// <summary>
/// What this game answers about using what the world holds: what a placement offers, what a requirement
/// means, and what a granted use produces.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to the interaction mechanism, and it is deliberately three
/// answers rather than one. <see cref="Describe"/> says what a placement is — its kind, its name, its verb,
/// its reach, what it requires, and what it costs — which is content interpretation. <see cref="Judge"/>
/// says what one requirement means for this game, which is policy the kit cannot know. <see cref="Apply"/>
/// says what a use the party has been allowed to make produces, which is where a door's new state, a
/// search's findings, and a sign's words come from.
/// </para>
/// <para>
/// The kit never asks a rule what to do about a failure it can already name, and never applies an outcome
/// itself beyond the owners it holds: prices settle through the party's one settlement path, items enter
/// through the party's one acquisition path, and the state a use records is the ruleset's own word.
/// </para>
/// </remarks>
public interface IInteractionRule
{
    /// <summary>
    /// What a placement offers the party, or null when it offers nothing: a spawn point, a light, and a
    /// decoration that raises no event are not things anybody uses.
    /// </summary>
    /// <param name="request">The placement, its place, and what has already happened to it.</param>
    /// <returns>The target's definition, or null when the placement is not a target.</returns>
    InteractionTargetDefinition? Describe(InteractionTargetRequest request);

    /// <summary>
    /// Whether the party meets one requirement, and the sentence that says why it does not. The sentence is
    /// what a refusal shows, so a requirement answered without one would be a lock that says nothing.
    /// </summary>
    /// <param name="requirement">The requirement to judge.</param>
    /// <param name="context">The target, its state, the party, and the clock the requirement is judged against.</param>
    /// <returns>The verdict.</returns>
    InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context);

    /// <summary>
    /// What a use the party is allowed to make produces: what the target becomes, what the use gives, or why
    /// nothing happened after all.
    /// </summary>
    /// <param name="target">The definition the target was given.</param>
    /// <param name="context">The target, its state, the party, and the clock.</param>
    /// <returns>The outcome the kit applies and reports.</returns>
    InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context);
}
