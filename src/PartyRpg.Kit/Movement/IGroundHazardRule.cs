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

    /// <summary>
    /// What spares the party, or some of it, the harm of this game's ground now, wherever it stands: each effect the
    /// party carries that does, who carries it, and whether it spares everybody or only its carrier.
    /// </summary>
    /// <remarks>
    /// This is the game's own reading of what its <see cref="IntervalOn"/> and <see cref="DamageTo"/> already answer,
    /// published so a person can see why the ground does or does not harm whom; it decides nothing the two do not.
    /// A game whose ground spares nobody answers nothing.
    /// </remarks>
    IReadOnlyList<GroundShelter> Shelters => [];
}

/// <summary>One effect that spares the party some ground's harm, as the game names it.</summary>
/// <param name="Effect">The effect's identity.</param>
/// <param name="Name">What the game calls it, which is what a person reads.</param>
/// <param name="Carrier">The member who carries it.</param>
/// <param name="Everybody">Whether it spares the whole party; otherwise it spares its carrier alone.</param>
public sealed record GroundShelter(EffectId Effect, string Name, PartyMemberId Carrier, bool Everybody);

/// <summary>What the ground under the party does to it now, as the world reads it for the panel.</summary>
/// <param name="Footing">The ground the party stands on, or null when it stands on nothing.</param>
/// <param name="Interval">How often that ground harms the party now, or null when it does not.</param>
/// <param name="NextHarm">The calendar boundary at which it next does, or null when it does not.</param>
/// <param name="UntilNextHarm">How much game time lies before then; none when it does not.</param>
/// <param name="Shelters">What spares the party, or some of it, the harm of the game's ground now.</param>
public sealed record GroundReading(
    SurfaceEffect? Footing,
    GameDuration? Interval,
    GameDate? NextHarm,
    GameDuration UntilNextHarm,
    IReadOnlyList<GroundShelter> Shelters)
{
    /// <summary>A world with no mover or no clock: nothing underfoot, nothing harming, nobody spared.</summary>
    public static GroundReading None { get; } = new(null, null, null, GameDuration.None, []);
}
