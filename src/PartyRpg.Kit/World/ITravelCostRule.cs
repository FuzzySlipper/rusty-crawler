namespace PartyRpg.Kit.World;

/// <summary>What a transition costs the party, and whether the party can pay it.</summary>
/// <remarks>
/// This is the cost contract the one transition path is built on: the path holds a rule and asks it about
/// every transition, of every kind, in both directions. The kit ships no implementation, because a price
/// is policy — the ruleset supplies the rule, the content and tuning it reads supply the numbers, and the
/// party owner supplies the purse and the food the rule judges affordability against.
/// </remarks>
public interface ITravelCostRule
{
    /// <summary>Reads what one transition charges the party, or refuses, without spending anything.</summary>
    /// <param name="request">The transition to price and where the party stands.</param>
    /// <returns>The cost to charge, or a named refusal that stops the transition.</returns>
    TravelCostQuote Quote(TransitionRequest request);

    /// <summary>Settles a rule's own cost after the destination and its ground admit the party.</summary>
    /// <remarks>
    /// The session still charges the quoted time and provisions to their owners. A rule with another
    /// cost, such as a bought passage, spends it here. Rules with only time and provisions need no further settlement.
    /// A refused arrival never reaches this method.
    /// </remarks>
    /// <param name="request">The journey that was admitted, including its departure.</param>
    void Arrived(TransitionRequest request) { }
}
