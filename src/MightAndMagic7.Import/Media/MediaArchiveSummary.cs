namespace MightAndMagic7.Import.Media;

/// <summary>What one container contributed to an extraction run.</summary>
/// <param name="Name">The container's file name.</param>
/// <param name="Kind">The container family: <c>lod</c>, <c>snd</c>, or <c>vid</c>.</param>
/// <param name="Sha256">SHA-256 of the container file, so the run is pinned to exact inputs.</param>
/// <param name="Bytes">Container size in bytes.</param>
/// <param name="Entries">Entries the container holds.</param>
/// <param name="Artifacts">Artifacts the extractor recorded for it.</param>
/// <param name="Emitted">Artifacts that produced a file.</param>
public readonly record struct MediaArchiveSummary(
    string Name,
    string Kind,
    string Sha256,
    long Bytes,
    int Entries,
    int Artifacts,
    int Emitted);
