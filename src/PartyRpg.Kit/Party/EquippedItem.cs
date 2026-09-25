namespace PartyRpg.Kit.Party;

/// <summary>One instance occupying one slot of a member's equipped figure.</summary>
/// <remarks>
/// A pair rather than a map entry, so a reader walks a figure as an ordered list and a save records the
/// slot names it was written under. The instance is the same object the shared pack would hold: equipping
/// moves it, and never copies it.
/// </remarks>
/// <param name="Slot">The slot of the figure the instance occupies.</param>
/// <param name="Item">The instance in that slot.</param>
public readonly record struct EquippedItem(EquipmentSlot Slot, ItemInstance Item);
