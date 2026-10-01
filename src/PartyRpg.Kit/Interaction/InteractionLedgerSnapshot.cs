using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>The interaction ledger as data: what each place the party has changed keeps.</summary>
/// <remarks>
/// It is a value rather than a live view so a save can carry it without learning anything about the world it
/// came from, and so a test can build a ledger directly from one. A place restored by the clock is absent,
/// because the ledger forgot it in the update that restored it: a load cannot bring back what a reset
/// discarded.
/// </remarks>
/// <param name="Places">One entry per place that keeps anything, in place identity order.</param>
public sealed record InteractionLedgerSnapshot(IReadOnlyList<PlaceInteractionSnapshot> Places)
{
    /// <summary>A ledger nothing has been written into.</summary>
    public static InteractionLedgerSnapshot None { get; } = new([]);
}

/// <summary>What one place keeps of what the party did there.</summary>
/// <param name="Place">The place.</param>
/// <param name="Values">The values the place keeps, by name in ordinal order.</param>
public sealed record PlaceInteractionSnapshot(PlaceId Place, IReadOnlyList<PlaceValue> Values);

/// <summary>One named whole number a place keeps.</summary>
/// <param name="Key">The ruleset's name for it.</param>
/// <param name="Value">Its value.</param>
public readonly record struct PlaceValue(string Key, long Value);
