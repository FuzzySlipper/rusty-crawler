namespace MightAndMagic7.Import.Maps;

/// <summary>One position in the level's own coordinate system.</summary>
/// <remarks>
/// Both families store positions as three signed integers, but at different widths: outdoor model
/// vertices and decorations are 32-bit, indoor level vertices and light positions are 16-bit. The
/// decoder widens the 16-bit form here so a consumer never has to know which payload it came from.
/// </remarks>
/// <param name="X">East-west coordinate, in engine units.</param>
/// <param name="Y">North-south coordinate, in engine units.</param>
/// <param name="Z">Height, in engine units.</param>
public readonly record struct MapPoint(int X, int Y, int Z);
