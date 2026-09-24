namespace MightAndMagic7.Import.Media;

/// <summary>The media family a decoded unit belongs to.</summary>
/// <remarks>
/// This is the family of what came out of the wrapper, which is not always the wrapper that carried
/// it: a nested PCX or font arrives inside the container's non-image wrapper, so the wrapper says
/// <see cref="MediaWrapper.NonImage"/> while the kind says what the payload turned out to be.
/// </remarks>
public enum MediaKind
{
    /// <summary>A flat palette-indexed image from the image wrapper.</summary>
    Bitmap,

    /// <summary>A palette-only entry: 768 bytes and nothing else.</summary>
    Palette,

    /// <summary>One sprite frame image.</summary>
    Sprite,

    /// <summary>A PCX image nested inside the container's non-image wrapper.</summary>
    Pcx,

    /// <summary>A bitmap font nested inside the container's non-image wrapper.</summary>
    Font,

    /// <summary>A sound sample from the sound bank.</summary>
    Sound,

    /// <summary>A movie from a video container.</summary>
    Video,

    /// <summary>Something the extractor can classify but not decode as media.</summary>
    Other,
}
