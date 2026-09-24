namespace MightAndMagic7.Import.Lod;

/// <summary>
/// How an entry's bytes were stored, as the container sniffing decided.
/// </summary>
public enum LodPayloadKind
{
    /// <summary>Stored verbatim.</summary>
    Verbatim,

    /// <summary>Wrapped in the compressed-data header and zlib-deflated.</summary>
    Compressed,

    /// <summary>Wrapped in the compressed-data header and stored without deflating.</summary>
    CompressedStored,

    /// <summary>Wrapped in the image header, marked as text, stored verbatim.</summary>
    Text,

    /// <summary>Wrapped in the image header, marked as text, and deflated.</summary>
    DeflatedText,

    /// <summary>Wrapped in the image header as an image payload; the palette travels with it.</summary>
    Image,

    /// <summary>Wrapped in the image header as a palette-only entry.</summary>
    Palette,
}
