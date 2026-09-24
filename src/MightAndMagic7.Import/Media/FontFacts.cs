using System.Buffers.Binary;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// The header and atlas measurements of a bitmap font.
/// </summary>
/// <remarks>
/// The extractor reads these and emits nothing: the product renders its own interface text, so a
/// glyph atlas has no consumer yet, and half-decoding a font into a PNG nobody reads would be worse
/// than recording what the file says. The measured numbers are kept because they prove the entry
/// unwrapped correctly and they cost nothing to obtain.
/// </remarks>
/// <param name="FirstChar">First character code the font carries.</param>
/// <param name="LastChar">Last character code the font carries.</param>
/// <param name="Depth">Bits per glyph pixel; the shipped fonts all say 8.</param>
/// <param name="Height">Glyph height in pixels.</param>
/// <param name="GlyphBytes">Sum of <c>height * width</c> over the glyphs the atlas describes.</param>
/// <param name="AtlasSize">Atlas byte count that reproduced <see cref="GlyphBytes"/>: 4096 or 1280.</param>
/// <param name="PayloadBytes">Total unwrapped payload size.</param>
public readonly record struct FontFacts(
    int FirstChar,
    int LastChar,
    int Depth,
    int Height,
    int GlyphBytes,
    int AtlasSize,
    int PayloadBytes)
{
    /// <summary>True when the atlas describes exactly the bytes the payload holds.</summary>
    public bool GlyphBytesMatchPayload => 32 + AtlasSize + GlyphBytes == PayloadBytes;

    /// <summary>Reads a font header and atlas, or reports why the bytes are not a font.</summary>
    /// <param name="payload">An already-unwrapped font payload.</param>
    /// <param name="facts">The measurements, when the bytes are a font.</param>
    /// <param name="reason">Why the bytes are not a font, when they are not.</param>
    public static bool TryRead(byte[] payload, out FontFacts facts, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(payload);
        facts = default;
        reason = null;

        if (payload.Length < 32 + 1280)
        {
            reason = $"a font payload starts with a 32-byte header and an atlas, but only {payload.Length} bytes are present";
            return false;
        }

        int firstChar = payload[0x00];
        int lastChar = payload[0x01];
        int depth = payload[0x02];
        int height = payload[0x05];
        uint paletteCount = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0x08));
        if (firstChar >= lastChar || depth != 8 || height is < 4 or > 63 || paletteCount != 0)
        {
            reason = $"the font predicate needs firstChar < lastChar, depth 8, height 4..63, and no palettes; the header says {firstChar}..{lastChar}, depth {depth}, height {height}, {paletteCount} palettes";
            return false;
        }

        // Two atlas layouts exist: the MM7 one carries a bearing and an i32 width per character, the
        // other only a u8 width. Which one a file uses is decided by which one accounts for the glyph
        // bytes, because guessing from the file name would break on the next font. `calig.fnt` in the
        // shipped archive is the narrow one.
        int mm7Glyphs = SumWideGlyphBytes(payload, firstChar, lastChar, height);
        int narrowGlyphs = SumNarrowGlyphBytes(payload, firstChar, lastChar, height);
        int mm7Atlas = 4096;
        int narrowAtlas = 1280;
        bool mm7Matches = 32 + mm7Atlas + mm7Glyphs == payload.Length;
        bool narrowMatches = 32 + narrowAtlas + narrowGlyphs == payload.Length;

        (int glyphs, int atlas) = mm7Matches || !narrowMatches ? (mm7Glyphs, mm7Atlas) : (narrowGlyphs, narrowAtlas);
        facts = new FontFacts(firstChar, lastChar, depth, height, glyphs, atlas, payload.Length);
        return true;
    }

    private static int SumWideGlyphBytes(byte[] payload, int firstChar, int lastChar, int height)
    {
        int total = 0;
        for (int character = firstChar; character <= lastChar && character < 256; character++)
        {
            int width = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0x20 + (character * 12) + 4));
            if (width is > 0 and <= 63) total += height * width;
        }

        return total;
    }

    private static int SumNarrowGlyphBytes(byte[] payload, int firstChar, int lastChar, int height)
    {
        int total = 0;
        for (int character = firstChar; character <= lastChar && character < 256; character++)
        {
            int width = payload[0x20 + character];
            if (width is > 0 and <= 63) total += height * width;
        }

        return total;
    }
}
