using System.Text;

namespace MightAndMagic7.Import.Lod;

/// <summary>
/// The one reading of a fixed-width name field: single-byte characters up to the first NUL, or the whole
/// field when it has none.
/// </summary>
/// <remarks>
/// Container directories, sound and video tables, map records and event operands all store names this way,
/// padded with NUL bytes, and they are names rather than text a player reads, so Latin-1 — one byte, one
/// character, nothing refused — is what keeps every name round-trippable.
/// </remarks>
internal static class FixedText
{
    /// <summary>Reads a NUL-padded name field.</summary>
    /// <param name="raw">The field's bytes.</param>
    internal static string Read(ReadOnlySpan<byte> raw)
    {
        int terminator = raw.IndexOf((byte)0);
        return Encoding.Latin1.GetString(terminator >= 0 ? raw[..terminator] : raw);
    }
}
