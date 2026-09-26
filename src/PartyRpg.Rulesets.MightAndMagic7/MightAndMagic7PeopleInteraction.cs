using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answers about using something, with the people its places hold added: whoever stands at a
/// placement is somebody the party talks to.
/// </summary>
/// <remarks>
/// <para>
/// <b>One way to reach a person.</b> A person is not entered by a mechanism of its own: the placement stands
/// in a place, this describes it as something the party talks to, and the conversation opens from the use.
/// Doors, chests, levers, signs, and people are all discovered and used by the one interaction mechanism,
/// and this adds no path beside it — it answers about one more kind of placement and delegates every other
/// question to the answers the doors-and-fixtures rule holds.
/// </para>
/// <para>
/// <b>Who is there is the dialogue policy's answer, not a second reading.</b> Whether a placement holds
/// somebody — a shopkeeper, a household, or a stranger a map's actor record places — and what they are
/// called come from the same policy the conversation itself asks, so the name the reticle shows and the
/// person who answers are one reading of one placement.
/// </para>
/// <para>
/// <b>What the use does is speak, not trade.</b> The outcome says the party spoke with whoever is here; what
/// that person has to say, what they offer, and what an offer hands the party to are the conversation's
/// business, and what a counter sells is the service mechanism's. A refusal there keeps its own vocabulary
/// — the counter is shut for the night — rather than being folded into an interaction refusal that could
/// not say what a shop needs.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7PeopleInteraction : IInteractionRule
{
    /// <summary>The state word a person the party has spoken with holds.</summary>
    internal const string SpokenState = "spoken";

    /// <summary>
    /// How far from somebody the party may stand and still address them, in place units.
    /// </summary>
    /// <remarks>
    /// The donor's keyboard interaction depth — "Maximum range for item pickup / opening chests / activating
    /// levers / etc with a keyboard" (OpenEnroth <c>src/Application/GameConfig.h:180</c>,
    /// <c>keyboard_interaction_depth</c>, default 512). A person is addressed from where a door is opened,
    /// which is the one range this game's interaction key has, and stating it here rather than borrowing
    /// another target's constant keeps how close a person is approached this game's own policy.
    /// </remarks>
    internal const double Reach = 512;

    private readonly MightAndMagic7Conversation _conversation;
    private readonly IInteractionRule _inner;

    /// <summary>Wraps this game's own interaction answers with the people its content places.</summary>
    /// <param name="conversation">
    /// This game's dialogue policy, which says who stands at a placement and what they are called. It is the
    /// concrete policy rather than the kit's seam because who a person is and what they say are this game's
    /// own answers about its own tables.
    /// </param>
    /// <param name="inner">The answers about everything else a place holds.</param>
    /// <exception cref="ArgumentNullException">Either answer is missing.</exception>
    internal MightAndMagic7PeopleInteraction(MightAndMagic7Conversation conversation, IInteractionRule inner)
    {
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public InteractionTargetDefinition? Describe(InteractionTargetRequest request)
    {
        if (_conversation.Describe(new ConversationTargetRequest(request.Place, request.Placement)) is not { } subject)
        {
            return _inner.Describe(request);
        }

        return new InteractionTargetDefinition(
            new InteractionTargetKind(MightAndMagic7Conversation.PersonTargetKind),
            subject.First.Name,
            InteractionVerb.Talk,
            Reach);
    }

    /// <inheritdoc />
    public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context) =>
        _inner.Judge(requirement, context);

    /// <inheritdoc />
    /// <remarks>Somebody the party can address guards themselves with nothing: what they have is behind them.</remarks>
    public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) =>
        _inner.Trap(target, context);

    /// <inheritdoc />
    public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context)
    {
        if (!string.Equals(target.Kind.Value, MightAndMagic7Conversation.PersonTargetKind, StringComparison.Ordinal))
        {
            return _inner.Apply(target, context);
        }

        return InteractionOutcome.Applied(SpokenState, $"The party speaks with {target.Name}.");
    }
}
