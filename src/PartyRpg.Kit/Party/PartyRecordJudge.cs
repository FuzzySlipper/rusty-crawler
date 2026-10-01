namespace PartyRpg.Kit.Party;

/// <summary>
/// The ruleset's answer about one record a save says the party carries: why it is not one its rules could have
/// written, or null when it is or when the name is not one the ruleset judges.
/// </summary>
/// <remarks>
/// The kit keeps the party's records by name without knowing what any name means, and most names a ruleset
/// writes are a bare fact — a flag, a bit — that any count of at least one fits. A ruleset that keeps something
/// with a shape inside a record's name — a slot of somebody it names — says here whether the name still fits
/// the content the save is resumed over; a load asks before it composes anything, and every answer that is not
/// null is a problem named with the rest.
/// </remarks>
/// <param name="name">The record's name.</param>
/// <param name="count">How many times it is on record.</param>
/// <returns>Why the record contradicts the ruleset, in words that finish "…, and …"; or null when it fits.</returns>
public delegate string? PartyRecordJudge(string name, int count);
