namespace PartyRpg.Kit.Party;

/// <summary>
/// The tunable offsets a first-person view adds on top of the party's pose.
/// </summary>
/// <remarks>
/// These are the numbers presentation tuning arrives through, and they are handed to the derivation
/// rather than kept by it: a view is always the party's pose plus exactly the offsets passed with that
/// call, so no part of the view survives into the next update. Only pitch has a look offset — turning
/// sideways turns the party itself, which is what keeps one yaw the game's single source of facing —
/// and the stride offset is what lets the eye rise and fall as the party walks without the party's own
/// height changing.
/// </remarks>
public readonly record struct PartyViewOffsets
{
    /// <summary>Creates the offsets a view is derived with.</summary>
    /// <param name="lookPitch">Pitch the view adds to the party's facing, in the world's facing unit.</param>
    /// <param name="eyeHeight">Height of the eye above the party's own position, in the place's units.</param>
    /// <param name="bobOffset">
    /// Vertical offset the party's stride contributes to the eye right now, in the place's units. Whoever
    /// owns the stride phase computes it and passes it in; the view keeps no clock of its own to derive it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">An offset is not a number.</exception>
    public PartyViewOffsets(double lookPitch, double eyeHeight, double bobOffset)
    {
        RequireFinite(lookPitch, nameof(lookPitch), "A look pitch must be a number; a view that adds one that is not cannot be placed on a party.");
        RequireFinite(eyeHeight, nameof(eyeHeight), "An eye height must be a number; a view that adds one that is not cannot be placed on a party.");
        RequireFinite(bobOffset, nameof(bobOffset), "A stride offset must be a number; a view that adds one that is not cannot be placed on a party.");
        LookPitch = lookPitch;
        EyeHeight = eyeHeight;
        BobOffset = bobOffset;
    }

    /// <summary>Pitch the view adds to the party's facing, in the world's facing unit.</summary>
    public double LookPitch { get; }

    /// <summary>Height of the eye above the party's own position, in the place's units.</summary>
    public double EyeHeight { get; }

    /// <summary>Vertical offset the party's stride contributes to the eye right now, in the place's units.</summary>
    public double BobOffset { get; }

    private static void RequireFinite(double value, string parameterName, string message)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, value, message);
    }
}
