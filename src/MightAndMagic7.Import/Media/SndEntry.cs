namespace MightAndMagic7.Import.Media;

/// <summary>One sample in a sound bank.</summary>
/// <param name="Index">Position in the bank's entry table, which is also its storage order.</param>
/// <param name="Name">The sample's name as the bank stores it.</param>
/// <param name="Offset">Absolute offset of the stored payload, past the per-payload size word.</param>
/// <param name="StoredSize">Stored payload size in bytes.</param>
/// <param name="DecompressedSize">
/// Declared inflated size, or zero for a payload stored as-is. A value equal to
/// <paramref name="StoredSize"/> also means stored, because the bank writes the stored size into both
/// fields for samples it did not deflate.
/// </param>
public readonly record struct SndEntry(int Index, string Name, long Offset, int StoredSize, int DecompressedSize)
{
    /// <inheritdoc />
    public override string ToString() => $"{Name} ({StoredSize} bytes)";
}
