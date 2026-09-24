namespace MightAndMagic7.Import.Media;

/// <summary>One colour of a 256-entry palette.</summary>
/// <param name="R">Red channel.</param>
/// <param name="G">Green channel.</param>
/// <param name="B">Blue channel.</param>
public readonly record struct Rgb24(byte R, byte G, byte B);
