namespace MightAndMagic7.Import.Maps;

/// <summary>Which family a map payload belongs to.</summary>
/// <remarks>
/// The two families are different formats, not two settings of one: an outdoor map is a height field
/// with separately placed models and its delta holds revelation state, while an indoor map is one
/// vertex/face/sector graph whose doors live in a separate delta payload.
/// </remarks>
public enum MapKind
{
    /// <summary>An outdoor region, read from an <c>.odm</c> payload.</summary>
    Outdoor,

    /// <summary>An interior, read from a <c>.blv</c> payload.</summary>
    Indoor,
}
