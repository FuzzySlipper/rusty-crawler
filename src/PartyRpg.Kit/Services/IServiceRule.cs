using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Services;

/// <summary>What one placement asks a service rule about: whether it is a service at all, and which one.</summary>
/// <remarks>
/// The placement travels whole, exactly as it does for interaction, so a ruleset reads whatever fields its
/// own content carries rather than a vocabulary the kit invented for them. A placement that names no
/// service this game knows is not a counter: it is simply not described, and the party can still talk to
/// whatever else stands there.
/// </remarks>
/// <param name="Place">The place the placement stands in.</param>
/// <param name="Placement">The placement content declared, which is where the service is named.</param>
public readonly record struct ServiceTargetRequest(PlaceId Place, PlacementDefinition Placement);

/// <summary>What the shelves hold when they are laid out: the rule's answer over content's lines.</summary>
/// <remarks>
/// A shelf is laid out from content's declaration, and asking the rule rather than reading the definition
/// is what leaves room for a game's own policy — a guild that stocks only what its tier may sell, a shop
/// whose range depends on the party's standing — without the mechanism growing a branch per kind.
/// </remarks>
/// <param name="Service">The service whose shelves are being laid out.</param>
/// <param name="Party">The party that will browse them.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record ServiceStockRequest(ServiceDefinition Service, PartyEntity Party, GameClock? Clock);

/// <summary>What the counter teaches: the rule's answer over the lessons content declares.</summary>
/// <param name="Service">The service whose lessons are being laid out.</param>
/// <param name="Party">The party that will browse them.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record ServiceLessonRequest(ServiceDefinition Service, PartyEntity Party, GameClock? Clock);

/// <summary>Which of a service's access requirements the party already carries.</summary>
/// <param name="Service">The service being browsed.</param>
/// <param name="Party">The party that carries the state the requirement is read from.</param>
public sealed record ServiceAccessRequest(ServiceDefinition Service, PartyEntity Party);

/// <summary>One operation a party asks for, as the eligibility rule judges it.</summary>
/// <remarks>
/// The party and the clock travel whole rather than as pre-digested answers, because what each requirement
/// means is this game's policy: whether a membership is the party's, which member's skill counts, how far a
/// class may take a skill, and what time of day the counter keeps are not questions the kit can answer.
/// </remarks>
/// <param name="Service">The service being asked.</param>
/// <param name="Operation">Which operation the party asked for.</param>
/// <param name="Subject">What the operation acts on, resolved from what the command named.</param>
/// <param name="Member">Which member the operation is for, or an unset identity when it is for nobody in particular.</param>
/// <param name="Party">The party asking.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record ServiceEligibilityRequest(
    ServiceDefinition Service,
    ServiceOperationKind Operation,
    ServiceSubject Subject,
    PartyMemberId Member,
    PartyEntity Party,
    GameClock? Clock);

/// <summary>One operation a party asks for, as the price rule prices it.</summary>
/// <param name="Service">The service being asked.</param>
/// <param name="Operation">Which operation the party asked for.</param>
/// <param name="Subject">What the operation acts on.</param>
/// <param name="Member">Which member the operation is for, or an unset identity when it is for nobody in particular.</param>
/// <param name="Party">The party asking, whose skill and standing the price may be adjusted by.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record ServiceQuoteRequest(
    ServiceDefinition Service,
    ServiceOperationKind Operation,
    ServiceSubject Subject,
    PartyMemberId Member,
    PartyEntity Party,
    GameClock? Clock);

/// <summary>
/// What this game answers about services: which placement is one, what it offers, who may be served, and
/// what it charges.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to the service mechanism, and it is deliberately six answers
/// rather than one. <see cref="Describe"/> is content interpretation: a placement names a service, and this
/// turns it into the definition the mechanism serves. <see cref="Stock"/> and <see cref="Lessons"/> are the
/// stock rule: what the shelves hold and what the counter teaches, which content declares and policy may
/// narrow. <see cref="Judge"/> is the eligibility rule: whether the party may do this at all, here, now.
/// <see cref="Quote"/> is the price rule: what the party pays and what it is paid. <see cref="Access"/> is
/// what the party already carries of what the service requires, so a panel can show it and the same
/// membership can be checked by both.
/// </para>
/// <para>
/// <b>The numbers are the ruleset's; the arithmetic is the kit's.</b> A rule states the multipliers and the
/// values it wants applied and composes them through <see cref="ServicePricing"/>, so how a value and a
/// multiplier become whole coins, and how a percentage is taken off, is one answer in the kit rather than
/// one per rule.
/// </para>
/// <para>
/// Every answer is a value about content and policy, never about one visit: two parties that walk into the
/// same shop are answered by the same rule, and what has already happened — a shelf sold down, a membership
/// bought — travels in the service's own state and the party's, not in the rule.
/// </para>
/// </remarks>
public interface IServiceRule
{
    /// <summary>
    /// Which service a placement is, or null when it is not one. A placement nothing describes is not a
    /// counter at all, exactly as a placement the interaction rule describes nothing about is not usable.
    /// </summary>
    /// <param name="request">The placement and the place it stands in.</param>
    /// <returns>The service definition, or null when the placement keeps no counter.</returns>
    ServiceDefinition? Describe(ServiceTargetRequest request);

    /// <summary>What the service's shelves hold when they are laid out, which is what a visit restocks to.</summary>
    /// <param name="request">The service, the party, and the clock.</param>
    /// <returns>The lines the shelves hold, in the order a person reads them.</returns>
    IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request);

    /// <summary>What the service teaches.</summary>
    /// <param name="request">The service, the party, and the clock.</param>
    /// <returns>The lessons on offer, in the order a person reads them.</returns>
    IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request);

    /// <summary>
    /// What the service offers besides goods and lessons: its cures, its passages, its provisions, its rooms,
    /// what it holds for the party, its ceiling on training, and the lines it posts.
    /// </summary>
    /// <remarks>
    /// This is the one answer covering every capability a kind needs and the stock rule cannot express: a
    /// temple's cures, a stable's fares, a tavern's food, rooms, and rumours, a training hall's cap, and a
    /// bank's account with the party are all this list, and what each offer means is its kind. The party and
    /// the clock travel whole for the same reason they do in the stock rule: a guild's membership, the coins
    /// a bank already holds, and the hour a room may be taken are this game's answers about its own content.
    /// </remarks>
    /// <param name="request">The service, the party, and the clock.</param>
    /// <returns>The offers, in the order a person reads them.</returns>
    IReadOnlyList<ServiceOffer> Offers(ServiceOfferRequest request);

    /// <summary>
    /// Which of the service's access requirements the party carries, as the words a panel shows. An empty
    /// list means the party carries none of what this service recognises, which is not the same as the
    /// service requiring nothing.
    /// </summary>
    /// <param name="request">The service and the party that carries the state.</param>
    /// <returns>What the party carries, in the order the service states it.</returns>
    IReadOnlyList<string> Access(ServiceAccessRequest request);

    /// <summary>
    /// Whether the party may do this operation here and now, and the sentence that says why not.
    /// </summary>
    /// <param name="request">The service, the operation, what it acts on, and the party asking.</param>
    /// <returns>The verdict.</returns>
    ServiceEligibility Judge(ServiceEligibilityRequest request);

    /// <summary>What the operation costs the party and what it pays it.</summary>
    /// <param name="request">The service, the operation, what it acts on, and the party asking.</param>
    /// <returns>The quote the mechanism settles.</returns>
    ServiceQuote Quote(ServiceQuoteRequest request);
}
