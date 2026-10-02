namespace PartyRpg.Kit.Party;

/// <summary>The canonical owner's reason an item must remain with the party, if any.</summary>
public interface IItemRetentionRule
{
    /// <summary>Why an item of this definition may not be consumed, released, or have a charge spent.</summary>
    Refusal? Retains(ItemDefinitionId item);
}
