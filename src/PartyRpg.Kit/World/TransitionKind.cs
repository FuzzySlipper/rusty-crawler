namespace PartyRpg.Kit.World;

/// <summary>What the party is doing to move between places, which is what the cost rule is asked about.</summary>
/// <remarks>
/// The kind is supplied by the caller rather than derived from the place graph, because it describes what
/// the party did, not what content declared: walking over a region edge and buying a seat on a coach can
/// share one edge between the same two places and still cost differently. Every kind is one member of one
/// enumeration so that no kind can grow a private path around the cost contract.
/// </remarks>
public enum TransitionKind
{
    /// <summary>Walking out of one region and into the next across a shared edge.</summary>
    Walking,

    /// <summary>Passing through an entrance or an exit between a region and an interior.</summary>
    Entrance,

    /// <summary>Travel bought from a service, such as a seat on a coach or a berth on a ship.</summary>
    PaidService,

    /// <summary>Magical travel that moves the party without crossing the ground between the two places.</summary>
    Portal,

    /// <summary>A transition the world itself issues rather than a place, such as placing the party at the start of a game.</summary>
    Scripted,
}
