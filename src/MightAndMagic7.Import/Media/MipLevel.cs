namespace MightAndMagic7.Import.Media;

/// <summary>One level of a decoded image's mip chain.</summary>
/// <param name="Width">Level width in pixels.</param>
/// <param name="Height">Level height in pixels.</param>
/// <param name="Offset">Byte offset of the level's first index inside the pixel block.</param>
public readonly record struct MipLevel(int Width, int Height, int Offset);
