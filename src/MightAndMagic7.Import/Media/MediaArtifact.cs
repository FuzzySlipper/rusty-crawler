namespace MightAndMagic7.Import.Media;

/// <summary>One media artifact the extractor found, emitted or deliberately not emitted.</summary>
/// <remarks>
/// The identity is <c>mm7:&lt;archive&gt;/&lt;entryName&gt;</c> with the entry name in the case the archive
/// stores, because archive lookup is case-insensitive and two archives can hold the same name. Every
/// field that does not apply to a family is null rather than zero, so a reader cannot mistake "no
/// palette" for "palette zero".
/// </remarks>
public sealed record MediaArtifact
{
    /// <summary>The artifact's identity, <c>mm7:&lt;archive&gt;/&lt;entryName&gt;</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The installation release the bytes came from.</summary>
    public required string SourceRelease { get; init; }

    /// <summary>The installation build the bytes came from.</summary>
    public required string SourceBuild { get; init; }

    /// <summary>The container's file name.</summary>
    public required string Archive { get; init; }

    /// <summary>The entry name as the container stores it.</summary>
    public required string EntryName { get; init; }

    /// <summary>Absolute offset of the entry's bytes inside its container.</summary>
    public required long EntryOffset { get; init; }

    /// <summary>Entry size in bytes, wrappers included.</summary>
    public required int EntrySize { get; init; }

    /// <summary>SHA-256 of the entry's raw bytes, wrappers included.</summary>
    public required string EntrySha256 { get; init; }

    /// <summary>Which container wrapper the entry was found in.</summary>
    public required MediaWrapper Wrapper { get; init; }

    /// <summary>Whether the container stored the payload deflated.</summary>
    public required bool PayloadDeflated { get; init; }

    /// <summary>Whether the pixel or glyph block inside the wrapper was stored deflated.</summary>
    public required bool PixelDeflated { get; init; }

    /// <summary>The media family the entry turned out to be.</summary>
    public required MediaKind Kind { get; init; }

    /// <summary>Base level width, for the families that have pixels.</summary>
    public int? Width { get; init; }

    /// <summary>Base level height, for the families that have pixels.</summary>
    public int? Height { get; init; }

    /// <summary>Levels in the mip chain, base level included.</summary>
    public int? MipLevels { get; init; }

    /// <summary>The palette id the unit names, for the families that name one.</summary>
    public int? PaletteId { get; init; }

    /// <summary>Where the colours came from.</summary>
    public MediaPaletteSource? PaletteSource { get; init; }

    /// <summary>Whether palette index 0 is drawn transparent.</summary>
    public bool? ZeroIsTransparent { get; init; }

    /// <summary>Blank lines the sprite header reports at the bottom of the canvas.</summary>
    public int? EmptyBottomLines { get; init; }

    /// <summary>The image or sprite header's flag word.</summary>
    public long? Flags { get; init; }

    /// <summary>What a sound sample's RIFF header says, for sound artifacts.</summary>
    public WaveFacts? Audio { get; init; }

    /// <summary>The emitted file's path, relative to the output root, or null when nothing was emitted.</summary>
    public string? OutputPath { get; init; }

    /// <summary>SHA-256 of the emitted file.</summary>
    public string? OutputSha256 { get; init; }

    /// <summary>Size of the emitted file in bytes.</summary>
    public long? OutputBytes { get; init; }

    /// <summary>Why no file was emitted, when none was.</summary>
    public string? ExcludedReason { get; init; }

    /// <summary>Measured facts and boundary statements this artifact needs to be read correctly.</summary>
    public string? Notes { get; init; }
}
