namespace MightAndMagic7.Import.Media;

/// <summary>One movie inside a video container.</summary>
/// <param name="Index">Position in the container's entry table.</param>
/// <param name="Name">The movie's name as the container stores it.</param>
/// <param name="Offset">Absolute offset of the movie's first byte.</param>
/// <param name="Size">
/// Slice length. The container stores no size, so this is the distance to the next entry's offset, or
/// to the end of the file for the last entry.
/// </param>
public readonly record struct VidEntry(int Index, string Name, long Offset, long Size)
{
    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Size} bytes)";
}
