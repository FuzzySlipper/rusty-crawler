using PartyRpg.Kit;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.World;

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
    private readonly MightAndMagic7Fixtures? _events;

    /// <summary>Wraps this game's own interaction answers with the people its content places.</summary>
    /// <param name="conversation">
    /// This game's dialogue policy, which says who stands at a placement and what they are called. It is the
    /// concrete policy rather than the kit's seam because who a person is and what they say are this game's
    /// own answers about its own tables.
    /// </param>
    /// <param name="inner">The answers about everything else a place holds.</param>
    /// <param name="events">
    /// This game's one interpretation of event steps, which runs a house's own event when the party uses the house; without
    /// one a house only opens.
    /// </param>
    /// <exception cref="ArgumentNullException">Either answer is missing.</exception>
    internal MightAndMagic7PeopleInteraction(MightAndMagic7Conversation conversation, IInteractionRule inner, MightAndMagic7Fixtures? events = null)
    {
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _events = events;
    }

    /// <inheritdoc />
    public InteractionTargetDefinition? Describe(InteractionTargetRequest request)
    {
        // What a person's word raised is run by the answers about events, wherever the person stands.
        if (request.Raised.Length > 0) return _inner.Describe(request);

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
    public Verdict Judge(InteractionRequirement requirement, InteractionContext context) =>
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

        // A house whose own event does more than open it runs that event: the event decides whether the party is let in,
        // and a branch of it may lead the party elsewhere instead.
        if (_events?.House(target, context, house => House(context, house)) is { } housed) return housed;
        return InteractionOutcome.Applied(SpokenState, $"The party speaks with {target.Name}.");
    }

    /// <summary>Who stands at the placement of a house of the use's place, by the house's number, or null when it holds none.</summary>
    private ConversationSubject? House(InteractionContext context, int house)
    {
        foreach (PlacementDefinition placement in context.PlaceTargets)
        {
            if (placement.Source.GetInt32(MightAndMagic7Conversation.HouseField) != house) continue;
            if (_conversation.Describe(new ConversationTargetRequest(context.Place, placement)) is { } subject) return subject;
        }

        return null;
    }
}
