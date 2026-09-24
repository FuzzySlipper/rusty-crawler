namespace MightAndMagic7.Import.Lod;

/// <summary>An entry's decoded payload and how it was stored.</summary>
/// <param name="Entry">The entry the payload came from.</param>
/// <param name="Bytes">The decoded bytes.</param>
/// <param name="Kind">How the container stored them.</param>
/// <param name="Palette">The palette the entry ships with, for image and palette entries.</param>
public readonly record struct LodPayload(LodEntry Entry, byte[] Bytes, LodPayloadKind Kind, byte[]? Palette = null)
{
    /// <summary>The payload as UTF-8 text, for the text tables the container marks as text.</summary>
    public string AsText()
    {
        string text = System.Text.Encoding.UTF8.GetString(Bytes);
        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }
}
