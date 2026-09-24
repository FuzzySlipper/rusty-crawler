namespace MightAndMagic7.Import.Media;

/// <summary>Which of the container's entry wrappers an entry was found in.</summary>
/// <remarks>
/// The names are the ones the format spec uses for the four sniffed wrappers plus the raw fallback.
/// <c>pcx</c> and <c>font</c> appear in that spec's list of wrapper names, but the bytes say they are
/// not wrappers at all: both are payloads inside <see cref="NonImage"/>, so this enum keeps the
/// wrapper at the container level and lets <see cref="MediaKind"/> name the nested family.
/// </remarks>
public enum MediaWrapper
{
    /// <summary>The <c>91969</c> + <c>"mvii"</c> compressed-data wrapper.</summary>
    Compressed,

    /// <summary>The 48-byte image wrapper.</summary>
    Image,

    /// <summary>The 48-byte image wrapper with every field zero: a palette and nothing else.</summary>
    Palette,

    /// <summary>The image wrapper flagged <c>0x100</c>: a payload that has to be sniffed.</summary>
    NonImage,

    /// <summary>The 32-byte sprite wrapper with its line table.</summary>
    Sprite,

    /// <summary>No wrapper the reader recognises.</summary>
    Raw,
}
