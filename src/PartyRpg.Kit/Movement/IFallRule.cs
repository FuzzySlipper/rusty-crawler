using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Movement;

/// <summary>What a fall past the tuning's threshold does to each member of the party, as a game answers it.</summary>
/// <remarks>
/// A landing is measured by movement and priced here, one member at a time, because a game may price a fall
/// from each character's own state — how much they can take — rather than at one rate for everybody. What the
/// answer is lands through the member's own damage entry, the same one a trap and a blow arrive at.
/// </remarks>
public interface IFallRule
{
    /// <summary>How much harm a landing does to one member.</summary>
    /// <param name="member">The member who landed.</param>
    /// <param name="fall">How far the party fell and how far past the threshold that was.</param>
    /// <returns>The harm, never below zero.</returns>
    int DamageTo(PartyMember member, FallOutcome fall);
}
