namespace PartyRpg.Kit.World;

/// <summary>The unit a provision charge is stated in.</summary>
/// <remarks>
/// The kit holds no food store and no notion of what food weighs, so the unit is deliberately the
/// abstract one: a portion is one share of whatever the party eats on the road. What a portion is worth
/// — a meal, a day, a measure — is the food owner's policy, and stating the unit lets a ruleset that
/// measures food differently say so instead of being silently reinterpreted by this layer.
/// </remarks>
public enum ProvisionUnit
{
    /// <summary>One portion of the party's food.</summary>
    Portions,
}
