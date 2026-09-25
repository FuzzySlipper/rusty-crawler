using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>Which thing in which place a use is about: a place, and the content placement standing in it.</summary>
/// <remarks>
/// <para>
/// This is content identity and the only half of a target that could be written down. A runtime entity's
/// identity belongs to the visit that created it, so state that outlives a visit — a door the party opened,
/// a container it emptied — is keyed by this pair rather than by anything the engine mints.
/// </para>
/// <para>
/// A target is only meaningful together with its place for the same reason a pose is: two places keep
/// separate coordinate spaces and separate content, and a placement id alone would name a door in whichever
/// place happened to be asked about.
/// </para>
/// </remarks>
/// <param name="Place">The place the target stands in.</param>
/// <param name="Content">The placement's identity in that place's content.</param>
public readonly record struct InteractionTargetId(PlaceId Place, PlacementContentId Content)
{
    /// <inheritdoc />
    public override string ToString() => $"{Place}/{Content}";
}
