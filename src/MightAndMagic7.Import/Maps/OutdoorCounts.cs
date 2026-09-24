namespace MightAndMagic7.Import.Maps;

/// <summary>The counts an outdoor payload declares.</summary>
/// <remarks>
/// These are the counts the file stores, not the sizes of the decoded arrays, so a payload whose
/// declared counts and arrays disagree is visible rather than rounded. The two normal blocks are
/// consumed but not kept — no donor reads them and their meaning is not established — so only the
/// normal table's size appears here.
/// </remarks>
/// <param name="NormalCount">How many per-cell normals the payload declares.</param>
/// <param name="ModelCount">How many placed models the payload declares.</param>
/// <param name="DecorationCount">How many decorations the payload declares.</param>
/// <param name="DecorationPidCount">How many decoration object references the payload declares.</param>
/// <param name="SpawnPointCount">How many spawn points the payload declares.</param>
/// <param name="FaceCount">How many faces were decoded across all models.</param>
/// <param name="VertexCount">How many vertices were decoded across all models.</param>
public readonly record struct OutdoorCounts(
    int NormalCount,
    int ModelCount,
    int DecorationCount,
    int DecorationPidCount,
    int SpawnPointCount,
    int FaceCount,
    int VertexCount);
