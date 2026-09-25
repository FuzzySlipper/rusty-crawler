namespace PartyRpg.Kit.Party;

/// <summary>One member a creation flow produced: the character it built, and what that character starts wearing.</summary>
/// <remarks>
/// The durable identity is not here. Creation describes a person; the party mints the identity when it
/// creates the member, which is what keeps identity minting in one place and lets the same shape be reused
/// by a scenario that does not care what numbers the identities happen to take.
/// </remarks>
public sealed record MemberCreation
{
    /// <summary>Creates a member for a party about to be built.</summary>
    /// <param name="seed">Everything the character is, as creation decided it.</param>
    /// <param name="startingEquipment">What the character starts wearing, which may be nothing.</param>
    public MemberCreation(PartyMemberSeed seed, IReadOnlyList<StartingEquipment>? startingEquipment = null)
    {
        ArgumentNullException.ThrowIfNull(seed);
        Seed = seed;
        StartingEquipment = startingEquipment ?? [];
    }

    /// <summary>Everything the character is, as creation decided it.</summary>
    public PartyMemberSeed Seed { get; }

    /// <summary>What the character starts wearing, in the order it is put on.</summary>
    public IReadOnlyList<StartingEquipment> StartingEquipment { get; }
}
