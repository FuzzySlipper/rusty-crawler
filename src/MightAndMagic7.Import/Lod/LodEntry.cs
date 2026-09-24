namespace MightAndMagic7.Import.Lod;

/// <summary>One named entry inside an archive.</summary>
/// <param name="Name">The entry name as the archive stores it.</param>
/// <param name="Offset">Absolute offset of the entry's payload in the archive file.</param>
/// <param name="Size">Declared payload size in bytes.</param>
public readonly record struct LodEntry(string Name, long Offset, int Size)
{
    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Size} bytes)";
}
