using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>What one downed creature left behind, as whoever keeps the bodies holds it.</summary>
/// <remarks>
/// <para>
/// A body is the creature's own placement lying where it fell, plus the one thing that distinguishes one
/// death from the next: its serial. Two visits to a place can bring the same creature down twice, and the
/// state the party left on the first body — searched — must not be read as the state of the second, so a
/// death is named rather than only its placement being named.
/// </para>
/// <para>
/// The corpus is not durable state and is not meant to be: the entities a population creates live for the
/// visit that made them, and a body is one of those creatures. Nothing here is written into a save, and a
/// resumed session finds the place as content says it was.
/// </para>
/// </remarks>
/// <param name="Place">The place the creature fell in.</param>
/// <param name="Body">The creature's placement, lying at the place it fell.</param>
/// <param name="Name">What the creature was called, which is what the body reads as.</param>
/// <param name="Serial">Which death this body is, counted across the session so two deaths cannot share one.</param>
public sealed record Corpse(PlaceId Place, PlacementDefinition Body, string Name, long Serial)
{
    /// <summary>The placement identity the body answers for, which is the creature's own.</summary>
    /// <remarks>
    /// A body stands where a creature stood, so it is named by the same content identity: a ruleset asked
    /// what a placement offers reads one identity and answers about the creature or about its body from the
    /// state, which is the same reading a door's open and closed positions take.
    /// </remarks>
    public PlacementContentId Content => Body.Content;

    /// <summary>Where the body lies.</summary>
    public PlacePose Pose => Body.Pose;

    /// <inheritdoc />
    public override string ToString() => $"{Name} in {Place} at {Pose} (death {Serial})";
}
