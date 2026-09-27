namespace MightAndMagic7.Import.Maps;

/// <summary>
/// One of an indoor level's minimap outlines: the line its own automap is drawn from.
/// </summary>
/// <remarks>
/// <para>
/// The payload keeps these so the game can draw an interior's map without walking its geometry, and the donor
/// draws exactly this line: a segment between two of the level's own vertices
/// (<c>OpenEnroth src/GUI/UI/UIGame.cpp:1380-1400</c>, which projects
/// <c>pIndoor-&gt;vertices[pOutline-&gt;uVertex1ID]</c> and <c>uVertex2ID</c> to the minimap;
/// <c>src/Engine/Graphics/Indoor.h:69-76</c>, the record's own layout). The two faces the record names are the
/// sectors the line divides, and the height is the level's own reading of how high that line stands, which the
/// donor uses to shade the line by its distance from the party.
/// </para>
/// <para>
/// The importer reads them as data rather than as a picture: what the picture someone draws from them looks
/// like is the product's reading, and this type stays the level's own record.
/// </para>
/// </remarks>
/// <param name="Index">Which outline this is, in the payload's own order.</param>
/// <param name="Vertex1">The first vertex the line joins, as an index into the level's vertices.</param>
/// <param name="Vertex2">The second vertex the line joins, as an index into the level's vertices.</param>
/// <param name="Face1">One of the two faces the line divides.</param>
/// <param name="Face2">The other face the line divides.</param>
/// <param name="Z">The height the outline stands at, in engine units.</param>
/// <param name="Flags">The record's own flags, kept raw because nothing in this product reads them yet.</param>
public readonly record struct MapOutline(
    int Index,
    int Vertex1,
    int Vertex2,
    int Face1,
    int Face2,
    int Z,
    int Flags);
