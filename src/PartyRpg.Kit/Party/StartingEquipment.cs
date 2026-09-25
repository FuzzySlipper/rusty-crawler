namespace PartyRpg.Kit.Party;

/// <summary>One piece of equipment a character is created wearing: a slot and a definition to fill it with.</summary>
/// <remarks>
/// Creation names definitions rather than instances, because the instance does not exist until the party
/// mints its durable identity. The factory creates the instance and equips it through the same gated path
/// every later equip takes, so a creation blueprint the rules would refuse fails while the party is being
/// built rather than quietly wearing something it may not use.
/// </remarks>
/// <param name="Slot">The slot of the equipped figure to fill.</param>
/// <param name="Definition">The item definition the character starts with.</param>
public readonly record struct StartingEquipment(EquipmentSlot Slot, ItemDefinitionId Definition);
