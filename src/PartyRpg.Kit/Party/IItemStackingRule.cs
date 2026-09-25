namespace PartyRpg.Kit.Party;

/// <summary>How far a game lets copies of one item definition share a single instance.</summary>
/// <remarks>
/// Stacking is policy, not a constant: how many arrows make a bundle, and whether anything bundles at all,
/// is a rule over item definitions, and the numbers live in content or tuning. The kit asks this rule when
/// the shared pack takes items, and a product that supplies no rule stacks nothing, which is the honest
/// state of a game that has not said what bundles.
/// </remarks>
public interface IItemStackingRule
{
    /// <summary>How many of one definition may share one instance.</summary>
    /// <param name="definition">The definition about to be taken into the pack.</param>
    /// <returns>A maximum of one or less when the definition does not stack, and the largest stack otherwise.</returns>
    int MaximumStack(ItemDefinitionId definition);
}
