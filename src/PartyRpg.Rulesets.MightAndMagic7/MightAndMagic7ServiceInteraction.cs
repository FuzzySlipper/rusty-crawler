using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Services;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answers about using something, with the counters its places hold added: a service placement
/// offers the use that talks to whoever keeps it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One way to reach a person.</b> A service is not entered by a second mechanism of its own: the counter
/// stands in a place as a placement, this describes it as something the party talks to, and the session
/// hands the use off to the service mechanism once it lands. Doors, chests, levers, signs, people, and
/// counters are all discovered and used by the one interaction mechanism, and this adds no path beside it —
/// it answers about one more kind of placement and delegates every other question to the answers #8486
/// wrote.
/// </para>
/// <para>
/// <b>What the use does is talk, not trade.</b> The outcome says the party spoke with whoever keeps the
/// counter; what the counter sells, whether it is open, and what it charges are the service mechanism's
/// business and are reported on the service surface, so a refusal there has the service's own vocabulary
/// — the counter is shut for the night, the party is not a member — rather than being folded into an
/// interaction refusal that could not say what a shop needs.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7ServiceInteraction : IInteractionRule
{
    /// <summary>The target kind a counter is.</summary>
    internal const string ServiceTargetKind = "service";

    /// <summary>The state word a counter the party has spoken at holds.</summary>
    internal const string SpokenState = "spoken";

    /// <summary>
    /// How far from a counter the party may stand and still address whoever keeps it, in place units.
    /// </summary>
    /// <remarks>
    /// The donor's keyboard interaction depth — "Maximum range for item pickup / opening chests / activating
    /// levers / etc with a keyboard" (OpenEnroth <c>src/Application/GameConfig.h:180</c>,
    /// <c>keyboard_interaction_depth</c>, default 512). A person is addressed from where a door is opened,
    /// which is the one range this game's interaction key has, and stating it here rather than borrowing
    /// another target's constant keeps how close a counter is approached this game's own policy.
    /// </remarks>
    internal const double Reach = 512;

    private readonly IServiceRule _services;
    private readonly IInteractionRule _inner;

    /// <summary>Wraps this game's own interaction answers with the counters its content places.</summary>
    /// <param name="services">This game's service policy, which says which placements keep a counter.</param>
    /// <param name="inner">The answers about everything else a place holds.</param>
    /// <exception cref="ArgumentNullException">Either answer is missing.</exception>
    internal MightAndMagic7ServiceInteraction(IServiceRule services, IInteractionRule inner)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public InteractionTargetDefinition? Describe(InteractionTargetRequest request)
    {
        if (!string.Equals(request.Placement.Content.Kind, MightAndMagic7Services.PlacementKind, StringComparison.Ordinal))
        {
            return _inner.Describe(request);
        }

        if (_services.Describe(new ServiceTargetRequest(request.Place, request.Placement)) is not { } service) return null;
        return new InteractionTargetDefinition(
            new InteractionTargetKind(ServiceTargetKind),
            service.Describe(),
            InteractionVerb.Talk,
            Reach);
    }

    /// <inheritdoc />
    public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context) =>
        _inner.Judge(requirement, context);

    /// <inheritdoc />
    /// <remarks>A counter guards itself with nothing: what a shop has is behind a proprietor rather than a lock.</remarks>
    public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) =>
        _inner.Trap(target, context);

    /// <inheritdoc />
    public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context)
    {
        if (!string.Equals(target.Kind.Value, ServiceTargetKind, StringComparison.Ordinal)) return _inner.Apply(target, context);
        string name = _services.Describe(new ServiceTargetRequest(context.Place, context.Placement)) is { } service
            ? service.Describe()
            : target.Name;
        return InteractionOutcome.Applied(SpokenState, $"The party speaks with {name}.");
    }
}
