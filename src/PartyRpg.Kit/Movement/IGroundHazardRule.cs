using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Movement;

/// <summary>
/// The game's answer to what standing on a kind of ground does to the party while time passes: how often it harms
/// the party there, and how much each time.
/// </summary>
/// <remarks>
/// <para>
/// The kit holds the mechanism: the world asks the party's mover what the party stands on whenever the clock moves,
/// counts the intervals the advance crossed on the calendar's own boundaries, and lands each interval's harm through
/// each member's own damage entry, the way a fall's harm lands. What makes ground dangerous — water a party drowns in
/// unless something keeps it up, ground that burns — and who is spared is the game's, answered here.
/// </para>
/// <para>
/// Ground the party is not standing on harms nobody: a party in the air, flying over water, or before its first step
/// stands on nothing, so a hazard is asked about only while the mover reports footing.
/// </para>
/// </remarks>
public interface IGroundHazardRule
{
    /// <summary>How often standing on this ground harms the party now, or null when it does not.</summary>
    /// <param name="ground">The ground the party stands on.</param>
    GameDuration? IntervalOn(SurfaceEffect ground);

    /// <summary>What one interval on this ground does to one member, in the unit their health is measured in.</summary>
    /// <param name="member">The member.</param>
    /// <param name="ground">The ground the party stands on.</param>
    int DamageTo(PartyMember member, SurfaceEffect ground);
}
