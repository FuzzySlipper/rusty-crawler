namespace PartyRpg.Kit.Party;

/// <summary>One member a save records: the durable identity, and everything the character was.</summary>
/// <remarks>
/// The identity is recorded beside the seed rather than inside it, because identity is minted once by the
/// party and the seed describes a person. Recorded in roster order, these are also what tells a restore what
/// order the party stands in — nothing else in a save needs to say it.
/// </remarks>
/// <param name="Id">The member's durable identity.</param>
/// <param name="Seed">Everything the character is, apart from the items it held.</param>
public sealed record PartyMemberSave(PartyMemberId Id, PartyMemberSeed Seed);
