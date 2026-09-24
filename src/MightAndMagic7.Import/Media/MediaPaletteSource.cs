namespace MightAndMagic7.Import.Media;

/// <summary>Where a decoded unit's 256 colours came from.</summary>
public enum MediaPaletteSource
{
    /// <summary>The unit is not palette-indexed: its colours are stored per pixel.</summary>
    None,

    /// <summary>The 768 bytes that travel with the image wrapper.</summary>
    Embedded,

    /// <summary>A <c>pal%03d</c> entry named by the unit's own palette id.</summary>
    Named,

    /// <summary>The 768-byte tail of a single-plane PCX file.</summary>
    PcxTail,

    /// <summary>
    /// The unit names a palette the installation does not have. The emitted colours are an identity
    /// grey ramp, which preserves shape and is not claimed to be the original colour.
    /// </summary>
    Unresolved,
}
