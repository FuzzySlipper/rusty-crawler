namespace PartyRpg.Kit.Party;

/// <summary>Where the party holds one item instance: nowhere yet, in the shared pack, or in a slot.</summary>
/// <remarks>
/// <para>
/// This is a closed set of three places, and that closure is what makes a per-character pack impossible to
/// express: there is no value here meaning "on a character but not worn", so an instance can never be
/// recorded as carried by a person rather than equipped by one. An equipped instance names the member by
/// durable identity, not by the engine's runtime entity, because a save has to read this without the store
/// that wrote it.
/// </para>
/// <para>
/// An instance records its own custody, and the party is the only code that writes it: the containers hold
/// the instances they hold and this value says the same thing about each of them, so a reader can ask
/// where one item is without walking the party, and a test can walk the party and show that every instance
/// is in exactly one place.
/// </para>
/// </remarks>
public readonly record struct ItemCustody
{
    private readonly Location _location;
    private readonly PartyMemberId _member;
    private readonly EquipmentSlot _slot;

    private ItemCustody(Location location, PartyMemberId member, EquipmentSlot slot)
    {
        _location = location;
        _member = member;
        _slot = slot;
    }

    /// <summary>Held by nobody in the party: on the ground, in a container, or in a shop's stock.</summary>
    public static ItemCustody Detached => default;

    /// <summary>In the party's one shared pack.</summary>
    public static ItemCustody InSharedInventory => new(Location.SharedInventory, default, default);

    /// <summary>Worn or wielded by one member, in one slot of that member's equipped figure.</summary>
    /// <param name="member">The member holding the instance, which must be a real identity.</param>
    /// <param name="slot">The slot of that member's figure the instance occupies.</param>
    /// <exception cref="ArgumentOutOfRangeException">The member identity is unset.</exception>
    /// <exception cref="ArgumentException">The slot has no name.</exception>
    public static ItemCustody EquippedBy(PartyMemberId member, EquipmentSlot slot)
    {
        if (member.Value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(member),
                member,
                "An equipped item has to name the member wearing it, and an unset identity names nobody.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(slot.Value);
        return new ItemCustody(Location.Equipped, member, slot);
    }

    /// <summary>Whether the party holds the instance nowhere at all.</summary>
    public bool IsDetached => _location == Location.Detached;

    /// <summary>Whether the instance lies in the party's shared pack.</summary>
    public bool IsInSharedInventory => _location == Location.SharedInventory;

    /// <summary>Whether the instance occupies a slot on a member's equipped figure.</summary>
    public bool IsEquipped => _location == Location.Equipped;

    /// <summary>The member wearing the instance.</summary>
    /// <exception cref="InvalidOperationException">The instance is not equipped, so no member wears it.</exception>
    public PartyMemberId Member => _location == Location.Equipped
        ? _member
        : throw new InvalidOperationException("Only an equipped item names a member; this custody names none.");

    /// <summary>The slot the instance occupies.</summary>
    /// <exception cref="InvalidOperationException">The instance is not equipped, so it occupies no slot.</exception>
    public EquipmentSlot Slot => _location == Location.Equipped
        ? _slot
        : throw new InvalidOperationException("Only an equipped item occupies a slot; this custody names none.");

    /// <inheritdoc />
    public override string ToString() => _location switch
    {
        Location.SharedInventory => "shared inventory",
        Location.Equipped => $"equipped by member {_member.Value} in '{_slot}'",
        _ => "detached",
    };

    private enum Location
    {
        Detached = 0,
        SharedInventory = 1,
        Equipped = 2,
    }
}
