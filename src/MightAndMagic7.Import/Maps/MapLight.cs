namespace MightAndMagic7.Import.Maps;

/// <summary>One light of an indoor level.</summary>
/// <remarks>
/// The map format specification does not give this record's field layout, only its 16-byte width, so
/// the layout decoded here is the donor's <c>BLVLight_MM7</c>: a 16-bit position, radius, three colour
/// channels, a type, an attribute word, and a brightness. Indoor lights are 16-bit where the rest of
/// the level's geometry is 32-bit, which is why the position is widened at this boundary.
/// </remarks>
/// <param name="Index">The light's index in the level's light array.</param>
/// <param name="Position">The light's position.</param>
/// <param name="Radius">The radius the light reaches.</param>
/// <param name="Red">Red channel.</param>
/// <param name="Green">Green channel.</param>
/// <param name="Blue">Blue channel.</param>
/// <param name="Type">The light's type.</param>
/// <param name="Attributes">The light's attribute word, kept raw.</param>
/// <param name="Brightness">The light's brightness.</param>
public sealed record MapLight(
    int Index,
    MapPoint Position,
    int Radius,
    int Red,
    int Green,
    int Blue,
    int Type,
    int Attributes,
    int Brightness);
