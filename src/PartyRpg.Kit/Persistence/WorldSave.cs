using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// The world's durable state: where the party stands, and what every place it has touched remembers.
/// </summary>
/// <remarks>
/// <para>
/// The pose is recorded as the party's own place and pose — the one owner's capture — rather than as a
/// camera, a view, or a projection value, because none of those is state the world holds: a view is derived
/// from the pose on demand, and a projection is a copy published to a client.
/// </para>
/// <para>
/// The population is deliberately absent. A place's runtime entities are rebuilt from the content
/// placements the world was loaded with, so a save records which place's population is <em>due</em>
/// (visited, cleared, and the day it was last restored) and never the entities themselves — whose runtime
/// identities belong to the visit that created them.
/// </para>
/// </remarks>
public sealed record WorldSave
{
    /// <summary>Records the world's durable state.</summary>
    /// <param name="pose">The place the party is in and the pose it holds there.</param>
    /// <param name="places">What every place the party has touched remembers.</param>
    /// <exception cref="ArgumentNullException">The per-place state is null.</exception>
    public WorldSave(PartyPose pose, PlaceStateLedgerSnapshot places)
    {
        ArgumentNullException.ThrowIfNull(places);
        Pose = pose;
        Places = places;
    }

    /// <summary>The place the party is in and the pose it holds there.</summary>
    public PartyPose Pose { get; }

    /// <summary>What every place the party has touched remembers, and the game day the world had reached.</summary>
    public PlaceStateLedgerSnapshot Places { get; }
}
