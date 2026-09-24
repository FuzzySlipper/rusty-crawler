namespace PartyRpg.Kit.World;

/// <summary>
/// Where a transition puts the party when it arrives.
/// </summary>
/// <remarks>
/// Two forms exist because the data has two. A travel instruction that names no position means "enter
/// at the place's own start point", which is a named arrival the place supplies; an instruction that
/// carries a position means exactly that position. Collapsing the first into the second would invent
/// a coordinate the game never stored, and would break the moment a place moves its start point.
/// </remarks>
/// <param name="EntryPointId">The arrival point's id, when the transition arrives at a named point.</param>
/// <param name="Pose">The exact pose, when the transition carries one.</param>
public readonly record struct PlaceArrival(string? EntryPointId, PlacePose? Pose)
{
    /// <summary>Arriving at a named point in the destination place.</summary>
    public static PlaceArrival AtEntryPoint(string entryPointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPointId);
        return new PlaceArrival(entryPointId, null);
    }

    /// <summary>Arriving at an exact pose.</summary>
    public static PlaceArrival AtPose(PlacePose pose) => new(null, pose);

    /// <summary>Whether the arrival names a point rather than carrying a pose.</summary>
    public bool IsEntryPoint => EntryPointId is not null;
}
