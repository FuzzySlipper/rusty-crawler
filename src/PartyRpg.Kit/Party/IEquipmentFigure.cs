namespace PartyRpg.Kit.Party;

/// <summary>
/// The places a game's characters wear and wield things in, and which of them an item may go to.
/// </summary>
/// <remarks>
/// <para>
/// The kit names no slot (<see cref="EquipmentSlot"/>): how many places a figure has and what may go where are a
/// game's paper doll. This is that doll as the kit asks about it — the slots in the order a screen draws them, and
/// for one item the slots it could occupy, in the order a player would expect it to try them — so a session can
/// offer "wear this" without a slot of its own, and a screen can draw a figure without knowing its game.
/// </para>
/// <para>
/// <b>Where an item may go is not whether this member may put it there.</b> A skill, a mastery, or a second item
/// already in the other hand are the member's own facts and belong to <see cref="IEquipmentUseRule"/>, which the
/// party consults on every change; this answers only what the item is shaped for.
/// </para>
/// </remarks>
public interface IEquipmentFigure
{
    /// <summary>Every slot a figure has, in the order a screen draws them.</summary>
    IReadOnlyList<EquipmentSlot> Slots { get; }

    /// <summary>The slots an item is shaped for, most likely first, or none when it is not worn at all.</summary>
    /// <param name="item">The instance a player would put on.</param>
    IReadOnlyList<EquipmentSlot> SlotsFor(ItemInstance item);
}
