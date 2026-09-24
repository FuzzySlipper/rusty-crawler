namespace MightAndMagic7.Import.Maps;

/// <summary>One texture coordinate of a face vertex.</summary>
/// <param name="U">Horizontal texture coordinate.</param>
/// <param name="V">Vertical texture coordinate.</param>
public readonly record struct MapTextureCoordinate(int U, int V);
