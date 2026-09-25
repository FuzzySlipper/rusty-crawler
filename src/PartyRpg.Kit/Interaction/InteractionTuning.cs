namespace PartyRpg.Kit.Interaction;

/// <summary>How the reticle acquires and releases the thing the party faces, in radians.</summary>
/// <remarks>
/// <para>
/// A player using something stands in front of it and looks at it, so the aim is a cone rather than a point:
/// acquisition is the half-angle within which a target is picked up and release the wider one within which
/// it is kept, which is what stops a target flickering away the moment the party's facing drifts a degree.
/// The two bounds are policy, not arithmetic — how forgiving an aim is belongs to the game — which is why
/// they are stated by the ruleset rather than guessed here.
/// </para>
/// <para>
/// <b>Reach is not here.</b> How close the party must stand is a fact about the kind of thing being used —
/// a lever is reached for, a person is spoken to across a room — so it travels on each target's definition
/// and the query is bounded by the furthest target the place actually holds.
/// </para>
/// </remarks>
public readonly record struct InteractionTuning
{
    /// <summary>Creates the aim one world's reticle uses.</summary>
    /// <param name="acquisitionAngleRadians">The half-angle within which a target is picked up.</param>
    /// <param name="releaseAngleRadians">The half-angle within which a picked-up target is kept, at least the acquisition angle.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An angle is not a finite angle, is not positive, is wider than a half turn, or the release bound is
    /// narrower than the acquisition bound.
    /// </exception>
    public InteractionTuning(double acquisitionAngleRadians, double releaseAngleRadians)
    {
        if (!double.IsFinite(acquisitionAngleRadians) || acquisitionAngleRadians <= 0 || acquisitionAngleRadians > Math.PI)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acquisitionAngleRadians),
                acquisitionAngleRadians,
                "A target is acquired within a finite, positive half-angle no wider than a half turn; a cone that is not one acquires nothing.");
        }

        if (!double.IsFinite(releaseAngleRadians) || releaseAngleRadians < acquisitionAngleRadians || releaseAngleRadians > Math.PI)
        {
            throw new ArgumentOutOfRangeException(
                nameof(releaseAngleRadians),
                releaseAngleRadians,
                "A target is released within a half-angle at least as wide as the one it was acquired within, and no wider than a half turn.");
        }

        AcquisitionAngleRadians = acquisitionAngleRadians;
        ReleaseAngleRadians = releaseAngleRadians;
    }

    /// <summary>The half-angle within which a target is picked up.</summary>
    public double AcquisitionAngleRadians { get; }

    /// <summary>The half-angle within which a picked-up target is kept.</summary>
    public double ReleaseAngleRadians { get; }
}
