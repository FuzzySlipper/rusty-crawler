using System.Text;

namespace MightAndMagic7.Import.Lod;

/// <summary>An entry's decoded payload and how it was stored.</summary>
/// <param name="Entry">The entry the payload came from.</param>
/// <param name="Bytes">The decoded bytes.</param>
/// <param name="Kind">How the container stored them.</param>
/// <param name="Palette">The palette the entry ships with, for image and palette entries.</param>
public readonly record struct LodPayload(LodEntry Entry, byte[] Bytes, LodPayloadKind Kind, byte[]? Palette = null)
{
    private static readonly Encoding WesternText = WesternEncoding();

    /// <summary>The payload as text, for the text tables the container marks as text.</summary>
    /// <remarks>
    /// The shipped tables are single-byte Western text: the English release writes its curly quotes, dashes and
    /// accented names in the Windows-1252 code page, which a UTF-8 reading turns into replacement characters in
    /// what a player reads. A table that begins with a UTF-8 byte-order mark says it is UTF-8 and is read that
    /// way. A byte the code page leaves undefined is refused with the entry and the offset named, rather than
    /// guessed at.
    /// </remarks>
    /// <exception cref="LodFormatException">The payload holds a byte that is not Western text.</exception>
    public string AsText()
    {
        if (Bytes.Length >= 3 && Bytes[0] == 0xEF && Bytes[1] == 0xBB && Bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(Bytes, 3, Bytes.Length - 3);
        }

        // The code page leaves five bytes undefined, and the platform's decoder passes them through as control
        // characters rather than refusing them, so they are refused here by name.
        int undefined = Bytes.AsSpan().IndexOfAny(UndefinedWesternBytes);
        if (undefined >= 0)
        {
            throw new LodFormatException(
                LodFault.Value,
                $"{Entry.Name} holds byte 0x{Bytes[undefined]:X2} at offset {undefined}, which the Western code page its text is written in does not define.");
        }

        return WesternText.GetString(Bytes);
    }

    /// <summary>The bytes the Windows-1252 code page assigns no character.</summary>
    private static readonly byte[] UndefinedWesternBytes = [0x81, 0x8D, 0x8F, 0x90, 0x9D];

    private static Encoding WesternEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
