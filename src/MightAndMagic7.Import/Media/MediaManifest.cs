namespace MightAndMagic7.Import.Media;

/// <summary>Everything one extraction run found, in the order it was found and then sorted by identity.</summary>
public sealed class MediaManifest
{
    /// <summary>The decoder generation that produced the artifacts.</summary>
    public required string DecoderVersion { get; init; }

    /// <summary>The installation the run read, and how it was identified.</summary>
    public required InstallIdentity Source { get; init; }

    /// <summary>The families this run does not emit, and the reason for each.</summary>
    public required IReadOnlyList<MediaBoundary> Boundaries { get; init; }

    /// <summary>What each container contributed.</summary>
    public required IReadOnlyList<MediaArchiveSummary> Archives { get; init; }

    /// <summary>Every artifact, ordered by identity.</summary>
    public required IReadOnlyList<MediaArtifact> Artifacts { get; init; }

    /// <summary>Artifacts that produced a file.</summary>
    public int EmittedCount => Artifacts.Count(artifact => artifact.OutputPath is not null);

    /// <summary>Total bytes of the emitted files.</summary>
    public long EmittedBytes => Artifacts.Sum(artifact => artifact.OutputBytes ?? 0);
}
