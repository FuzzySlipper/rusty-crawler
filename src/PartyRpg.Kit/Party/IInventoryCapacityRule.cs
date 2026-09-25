namespace PartyRpg.Kit.Party;

/// <summary>Whether the party's one shared pack admits more items, and why not when it does not.</summary>
/// <remarks>
/// Capacity is policy over content and tuning, never a constant in this mechanism: the shipped data carries
/// no weight column and the original enforces no carry limit, so a rule that invents one would invent a
/// limit the game does not have. When encumbrance does arrive it is party-wide — one allowance over the
/// single shared pack — which is why the rule is asked about the pack as a whole rather than about a
/// character.
/// </remarks>
public interface IInventoryCapacityRule
{
    /// <summary>Answers whether the shared pack takes the items, or refuses them.</summary>
    /// <param name="held">The instances the pack holds right now, in the order they were taken.</param>
    /// <param name="definition">The definition the incoming items are copies of.</param>
    /// <param name="count">How many items are being taken.</param>
    /// <returns>A refusal when the pack will not take them, or null when it will.</returns>
    PartyRefusal? Judge(IReadOnlyList<ItemInstance> held, ItemDefinitionId definition, int count);
}
