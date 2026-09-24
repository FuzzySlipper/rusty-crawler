namespace MightAndMagic7.Import.Media;

/// <summary>A media family the extractor deliberately does not emit, and why.</summary>
/// <param name="Family">The family's name.</param>
/// <param name="Reason">What is missing and what would have to exist to close it.</param>
public readonly record struct MediaBoundary(string Family, string Reason);
