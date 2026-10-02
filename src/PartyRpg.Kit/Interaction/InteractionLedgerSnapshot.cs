using PartyRpg.Kit.World;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;

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
public sealed record PlaceInteractionSnapshot(PlaceId Place, IReadOnlyList<PlaceValue> Values)
{
    /// <summary>What each used target became, by content identity.</summary>
    public IReadOnlyList<PlacementStateSnapshot> Targets { get; init; } = [];

    /// <summary>Placements defeated here, until the place is restored.</summary>
    public IReadOnlyList<PlacementContentId> Deaths { get; init; } = [];

    /// <summary>What a person still carries after a hand has reached for their purse.</summary>
    public IReadOnlyList<PlacementPurseSnapshot> Purses { get; init; } = [];
}

/// <summary>One target's durable state and incarnation.</summary>
public sealed record PlacementStateSnapshot(PlacementContentId Target, string State, int Revision);

/// <summary>One placement's remaining purse, distinct from the party's purse.</summary>
public sealed record PlacementPurseSnapshot(PlacementContentId Target, int Coins, IReadOnlyList<ItemDefinitionId> Items);

/// <summary>The game's answer about a target state a save records, or null when it is known.</summary>
public delegate string? PlacementStateJudge(PlaceId place, PlacementContentId target, string state);

/// <summary>One named whole number a place keeps.</summary>
/// <param name="Key">The ruleset's name for it.</param>
/// <param name="Value">Its value.</param>
public readonly record struct PlaceValue(string Key, long Value);
