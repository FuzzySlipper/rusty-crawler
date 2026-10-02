using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>Which way a creature is being moved relative to the actor it is moving about.</summary>
public enum CreatureMovePurpose
{
    /// <summary>Closing on the actor: the creature is trying to reach it.</summary>
    Toward,

    /// <summary>Backing away from the actor: a creature that has had enough is trying to leave.</summary>
    Away,
}

/// <summary>One creature's step, as whoever drives it asks for it.</summary>
/// <remarks>
/// <para>
/// A request is a value and not a promise: it says which creature moves, from where, about whom, and with
/// how much admitted time to do it. Whether the step happens is the mover's — a creature whose place holds
/// no collision, whose step is blocked by a wall, or whose session admits no time does not move, and says
/// so in its outcome rather than by arriving somewhere impossible.
/// </para>
/// <para>
/// <see cref="Speed"/> is the creature's own, in place units per second, because how fast a kind of
/// creature covers ground is a content fact and not a property of the mechanism: a creature that moves at
/// its own pace and one that moves at the party's would otherwise be paced by whoever asked. The mover
/// scales the controller profile it was composed with rather than inventing a second physics for it.
/// </para>
/// </remarks>
/// <param name="Creature">The combatant that moves.</param>
/// <param name="From">Where it stands now, as whoever drives it last read it.</param>
/// <param name="Target">The actor the move is about: the one being closed on, or the one being left.</param>
/// <param name="TargetPose">Where that actor stands now.</param>
/// <param name="Purpose">Which way the creature moves relative to it.</param>
/// <param name="Speed">How fast the creature covers ground, in place units per second.</param>
/// <param name="ElapsedSeconds">The admitted world time this step covers, which must be positive.</param>
public readonly record struct CreatureMoveRequest(
    CombatantId Creature,
    PlacePose From,
    CombatantId Target,
    PlacePose TargetPose,
    CreatureMovePurpose Purpose,
    double Speed,
    double ElapsedSeconds);

/// <summary>What one creature's step came to.</summary>
/// <remarks>
/// A step that moved nothing is a real outcome and not a failure: a creature pressed against a wall, one
/// standing in a place whose collision was never admitted, and a creature told to close on somebody who is
/// already at arm's length all end where they began, and the caller learns that from
/// <see cref="Moved"/> and <see cref="MovedBy"/> rather than by comparing positions itself.
/// <para>
/// A creature the engine cannot step at all — its body starts inside collision deeper than the engine resolves,
/// and no ground over its feet stands it clear — is held where it stands, and <see cref="Refusal"/> says why
/// with a code from <see cref="CreatureMoveCodes"/>. That is a creature that stays put by name, never a fault
/// that stops the session.
/// </para>
/// </remarks>
/// <param name="Moved">Whether the engine's step carried the creature anywhere.</param>
/// <param name="Pose">Where the creature stands after the step, which is where it began when nothing moved it.</param>
/// <param name="MovedBy">How far it moved, in place units.</param>
/// <param name="Grounded">Whether the engine found ground under it at the end of the step.</param>
/// <param name="Refusal">Why the creature is held rather than stepped; null when the engine took the step.</param>
public readonly record struct CreatureMoveOutcome(bool Moved, PlacePose Pose, double MovedBy, bool Grounded, Refusal? Refusal = null)
{
    /// <summary>Nothing moved: the creature stands where it stood.</summary>
    /// <param name="pose">Where it stands.</param>
    public static CreatureMoveOutcome Still(PlacePose pose) => new(false, pose, 0, false);

    /// <summary>The creature cannot be stepped, and stands where it stood for the stated reason.</summary>
    /// <param name="pose">Where it stands.</param>
    /// <param name="refusal">Why it is held.</param>
    /// <exception cref="ArgumentNullException">No reason was given.</exception>
    public static CreatureMoveOutcome Held(PlacePose pose, Refusal refusal) =>
        new(false, pose, 0, false, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>Whether the creature is held rather than stepped.</summary>
    public bool IsHeld => Refusal is not null;
}

/// <summary>The codes a creature's step is refused with.</summary>
public static class CreatureMoveCodes
{
    /// <summary>The place has no navigation or Engine found no usable pursuit route.</summary>
    public const string Navigation = "creature.navigation";
    /// <summary>
    /// The refusal code <c>creature-embedded</c>: the creature's body stands inside collision the engine cannot
    /// resolve it out of, and no ground over its feet stands it clear.
    /// </summary>
    public const string Embedded = "creature-embedded";
}

/// <summary>
/// Moves a creature through the engine's own collision, one step at a time.
/// </summary>
/// <remarks>
/// <para>
/// <b>One engine service, no C# collision.</b> An implementation walks a creature with the same spatial
/// service and the same collision scene the party walks in, exactly as
/// <c>PartyMovement</c> walks the party: the engine owns the sweep, the step-up, the slopes, and the
/// solver, and nothing here decides whether a body fits somewhere. A mover that could not reach the engine
/// answers that nothing moved, which is a world where creatures stand still rather than one where they
/// walk through walls.
/// </para>
/// <para>
/// <b>It owns no position.</b> Where a creature stands is the creature's own, attached when it was placed; a
/// step starts from that position and the driver writes back where the step ended. What a mover keeps between
/// steps is only the engine's continuation of a creature's motion.
/// </para>
/// <para>
/// <b>What it keeps is per visit.</b> The entities a population creates do not outlive the visit that made
/// them, so a mover forgets a creature when it leaves the field and forgets every creature when the place
/// changes.
/// </para>
/// </remarks>
public interface ICreatureMover : IDisposable
{
    /// <summary>Moves one creature by one step of admitted time.</summary>
    /// <param name="request">Which creature moves, about whom, and with how much time.</param>
    /// <returns>Where it ended up and whether it moved at all.</returns>
    CreatureMoveOutcome Move(CreatureMoveRequest request);

    /// <summary>Forgets what a mover kept of a creature's motion, which is what leaving the field does to one.</summary>
    /// <param name="creature">The creature to forget.</param>
    void Forget(CombatantId creature);

    /// <summary>
    /// Forgets what a mover kept of every creature's motion, which is what entering a place does: it belonged
    /// to the place the party has left.
    /// </summary>
    void ForgetAll();
}
