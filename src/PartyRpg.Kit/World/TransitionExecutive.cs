using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>Takes the party through one transition: verifies the departure, asks the cost rule, and arrives.</summary>
/// <remarks>
/// This is the one path between places. Walking off a region edge, passing an entrance or an exit, buying a
/// seat on a coach or a berth on a ship, stepping through a portal, and the world's own placement of the
/// party at the start of a game all go through <see cref="Take"/>, and every one of them asks the cost rule
/// first. There is no overload, shortcut, or convenience method that moves a party without it, and no
/// default rule: the executive cannot be constructed without one, so no caller can obtain a path that
/// skips the cost contract.
///
/// Two kinds of failure stay apart, because a caller must treat them differently. A party that cannot pay
/// is an ordinary condition, so the rule's refusal is returned in the result and the party does not move.
/// A request that contradicts the world — a transition the world does not issue, a kind of travel the
/// transition cannot be taken as, or a party that is not in the place the transition leaves from — is a
/// caller defect, so it throws: arriving anyway would be exactly the teleport this path exists to prevent,
/// and quietly refusing would hide the bug as if the player's journey had been declined.
///
/// One kind of travel is issued by its caller rather than by a place: a portal is opened where the party
/// stands and reaches a place the world holds, so it is taken as a transition the graph need not declare.
/// The same departure check applies to it — the party must be where the portal opens — and the destination
/// must be a place this world has, which is what keeps magical travel from being a way to arrive somewhere
/// nobody has ever been.
///
/// The executive stops at the cost. Charging it — advancing the session's clock, taking provisions from the
/// party's food, and the weakened state a party suffers when it arrives short of food — belongs to the
/// owners of the clock and the party, which the kit does not have. The result reports what was quoted so
/// those owners can be handed their share of it.
/// </remarks>
public sealed class TransitionExecutive
{
    private readonly ITravelCostRule _costRule;

    /// <summary>Creates the one transition path over the cost rule it must always consult.</summary>
    /// <param name="costRule">The rule that prices every transition, of every kind.</param>
    /// <exception cref="ArgumentNullException">The rule is null, which would leave transitions unpriced.</exception>
    public TransitionExecutive(ITravelCostRule costRule) =>
        _costRule = costRule ?? throw new ArgumentNullException(nameof(costRule));

    /// <summary>Takes one transition, or refuses it and leaves the party where it stands.</summary>
    /// <param name="request">The transition to take and where the party stands as it takes it.</param>
    /// <returns>
    /// An arrival holding the destination place, the pose resolved for that destination, and the cost that
    /// was charged; or a refusal holding the place and pose the party never left, with the rule's reason.
    /// </returns>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The request contradicts the world: the graph does not issue the transition, the kind of travel and
    /// the transition disagree about who issues it, the party is not in the place the transition leaves
    /// from, or the cost rule answered nothing at all.
    /// </exception>
    /// <exception cref="ContentValidationException">
    /// The transition's arrival cannot be resolved in its graph — a named point the destination does not
    /// have. That is a content defect, and a party that silently landed somewhere else would be far harder
    /// to diagnose than a refusal, so the graph's own failure is not softened here.
    /// </exception>
    public TransitionResult Take(TransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        VerifyDeparture(request);

        TravelCostQuote? quote = _costRule.Quote(request);
        if (quote is null)
        {
            // The path exists to guarantee the contract, so a rule that answers nothing stops the journey
            // rather than being read as free travel.
            throw new InvalidOperationException(
                $"The cost rule answered nothing for transition '{request.Transition.Source}' taken as {request.Kind}; an unpriced transition is not taken.");
        }

        if (quote.Refusal is { } refusal)
        {
            return TransitionResult.Refused(request.Kind, request.PartyPlace, request.PartyPose, refusal);
        }

        // The arrival is resolved through the graph rather than copied from the transition, so a named
        // arrival point is read from the destination place and stays correct when that place moves it.
        PlacePose arrival = request.Graph.ResolveArrival(request.Transition);
        return TransitionResult.Arrival(request.Kind, request.Transition.To, arrival, quote.Cost);
    }

    /// <summary>Fails when the request asks to move a party that is not where the transition leaves from.</summary>
    private static void VerifyDeparture(TransitionRequest request)
    {
        PlaceTransition transition = request.Transition;
        if (!request.Graph.Transitions.Contains(transition))
        {
            // Magical travel is issued by whoever opened the way rather than by a place: a portal stands where
            // the party is and reaches a place the world holds, so the transition it is taken as need not be
            // one content declared. What is still verified is everything that keeps a portal from being a way
            // to arrive somewhere the world has never heard of: the party must stand where the portal opens,
            // and the destination must be a place this world has.
            if (request.Kind == TransitionKind.Portal && !transition.IsWorldIssued)
            {
                if (transition.From != request.PartyPlace)
                {
                    throw new InvalidOperationException(
                        $"The party is in place '{request.PartyPlace}', but transition '{transition.Source}' leaves place '{transition.From}'.");
                }

                request.Graph.Require(transition.To);
                return;
            }

            throw new InvalidOperationException(
                $"The world does not issue the transition '{transition.Source}' from '{transition.From}' to '{transition.To}', so it cannot be taken.");
        }

        if (request.Kind == TransitionKind.Scripted)
        {
            // Scripted travel has no departure to verify: the world issues it, so there is no place the
            // party would have to be standing in. It is still checked against the transition's issuer, so
            // a place-issued road cannot be taken as scripted travel to reach somewhere the party could
            // not leave from.
            if (!transition.IsWorldIssued)
            {
                throw new InvalidOperationException(
                    $"Transition '{transition.Source}' leaves place '{transition.From}', so it is issued by a place and cannot be taken as scripted travel.");
            }

            return;
        }

        if (transition.IsWorldIssued)
        {
            throw new InvalidOperationException(
                $"Transition '{transition.Source}' is issued by the world rather than by a place, so '{request.Kind}' travel cannot take it.");
        }

        if (transition.From != request.PartyPlace)
        {
            throw new InvalidOperationException(
                $"The party is in place '{request.PartyPlace}', but transition '{transition.Source}' leaves place '{transition.From}'.");
        }
    }
}
