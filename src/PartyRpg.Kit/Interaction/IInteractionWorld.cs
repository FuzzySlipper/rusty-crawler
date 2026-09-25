using System.Numerics;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>
/// The live state an interaction is resolved against: where the party is, what stands in the place it is in,
/// and the owners a use's requirements and outcome settle against.
/// </summary>
/// <remarks>
/// <para>
/// A use is resolved against the world as it stands at the moment of the use, never against a list of
/// targets someone wrote down: the placements here are the place's own content, the party is the one the
/// session holds, and the state is what the party has already done to those placements. This is the seam the
/// world answers, so the mechanism depends on what it reads rather than on the world's own type.
/// </para>
/// <para>
/// Every optional member is optional for a reason that is a fact about the world: a session with no scenario
/// party has no party, a ruleset that composed no clock has no time of day, and a world with no engine has no
/// collision to see through. A requirement that needs an absent owner is unmet and says so, which is honest
/// where an invented fact would not be.
/// </para>
/// </remarks>
public interface IInteractionWorld
{
    /// <summary>The place the party is in, which is where targets are read from.</summary>
    PlaceId Place { get; }

    /// <summary>Where the party stands and faces in that place.</summary>
    PlacePose Pose { get; }

    /// <summary>What the place holds, in content order, whether or not the party is standing in it.</summary>
    IReadOnlyList<PlacementDefinition> Placements { get; }

    /// <summary>The party a use is made by, or null when the world holds none.</summary>
    PartyEntity? Party { get; }

    /// <summary>The party's own accounts, which a use's price settles against, or null when there are none.</summary>
    PartyResourceLedger? Accounts { get; }

    /// <summary>The session's one clock, which a time-of-day requirement is judged against, or null when none was composed.</summary>
    GameClock? Clock { get; }

    /// <summary>What the party has already done to the targets of every place it has been in.</summary>
    InteractionLedger States { get; }

    /// <summary>
    /// Whether nothing solid stands between two points of the place the party is in, in the engine's world
    /// axes.
    /// </summary>
    /// <remarks>
    /// This is line of sight and not a route: a target the party can see is one it may aim at, and whether it
    /// can walk there is a different question the world does not answer here. A world with no collision scene
    /// answers that nothing occludes anything, because it holds nothing that could — the same honesty with
    /// which it reports a party standing on no geometry.
    /// </remarks>
    /// <param name="from">Where the sight line starts, in the engine's world axes.</param>
    /// <param name="to">What the party is looking at, in the engine's world axes.</param>
    /// <returns>Whether the target is in sight.</returns>
    bool InSight(Vector3 from, Vector3 to);
}
