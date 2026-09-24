namespace PartyRpg.Kit.World;

/// <summary>
/// A position and facing inside one place, in that place's own coordinates.
/// </summary>
/// <remarks>
/// A pose is only meaningful together with the place it is in: regions and interiors keep their own
/// coordinate spaces, and the translation between them happens at the transition boundary rather than
/// by pretending one space covers the world.
/// </remarks>
/// <param name="X">Position along the place's first axis.</param>
/// <param name="Y">Position along the place's second axis.</param>
/// <param name="Z">Height in the place.</param>
/// <param name="Yaw">Facing around the vertical axis.</param>
/// <param name="Pitch">Facing above or below the horizon.</param>
public readonly record struct PlacePose(double X, double Y, double Z, double Yaw, double Pitch)
{
    /// <summary>A pose at the origin, facing along the place's first axis.</summary>
    public static PlacePose Origin => default;
}

/// <summary>A named spot in a place the party can arrive at.</summary>
/// <param name="Id">The arrival point's identity, as content names it.</param>
/// <param name="Pose">Where arriving there puts the party.</param>
public sealed record PlaceEntryPoint(string Id, PlacePose Pose);
