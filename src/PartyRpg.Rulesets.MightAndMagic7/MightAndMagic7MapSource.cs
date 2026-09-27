using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Reads each place's own map out of the content the product loaded.
/// </summary>
/// <remarks>
/// <para>
/// <b>The map is a raster the importer emitted from the operator's own data, and this reads it back without
/// interpreting it.</b> What each cell's number means is this game's reading and lives in
/// <see cref="MightAndMagic7Automap"/>; what is here is only the layout: a grid, and one number per square of
/// it. The format is recorded in <c>docs/research/mm7-map-formats.md</c> with the rest of the importer's
/// output, because the layout is the runtime contract between the two.
/// </para>
/// <para>
/// <b>A place content states no map for answers with none, and a map that cannot be read stops the read.</b>
/// The first is a place the automap cannot draw, which is worth saying plainly; the second is a content defect
/// whose cells would otherwise be drawn one square out of place, which is worse than a named failure while the
/// world is being composed.
/// </para>
/// <para>
/// Maps are read once and kept: a party asks for the place it stands in on every step, and re-reading a
/// document of tens of thousands of cells on each of them would make walking cost what the map's size is.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7MapSource : IPlaceMapSource
{
    /// <summary>The definition kind whose entries carry places' own maps.</summary>
    internal const string MapDefinitionKind = "place-map";

    private readonly ContentCatalog _catalog;
    private readonly Dictionary<string, PlaceMap?> _read = new(StringComparer.Ordinal);

    /// <summary>Creates the reader.</summary>
    /// <param name="catalog">The validated content the world was built from.</param>
    /// <exception cref="ArgumentNullException">No content catalog was supplied.</exception>
    internal MightAndMagic7MapSource(ContentCatalog catalog) =>
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <inheritdoc />
    public PlaceMap? For(PlaceId place)
    {
        if (_read.TryGetValue(place.Value, out PlaceMap? held)) return held;
        PlaceMap? map = Read(place);
        _read[place.Value] = map;
        return map;
    }

    /// <summary>Reads one place's map out of the documents that carry them.</summary>
    private PlaceMap? Read(PlaceId place)
    {
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in _catalog.Entries(MapDefinitionKind))
        {
            if (!string.Equals(entry.Id, place.Value, StringComparison.Ordinal)) continue;
            string where = $"{pack.PackId}/{document.DocumentId}#{entry.Id}";

            IReadOnlyList<JsonElement> origin = entry.GetArray("origin");
            if (origin.Count != 2 ||
                origin[0].ValueKind != JsonValueKind.Number || !origin[0].TryGetDouble(out double originX) ||
                origin[1].ValueKind != JsonValueKind.Number || !origin[1].TryGetDouble(out double originY))
            {
                throw new InvalidOperationException(
                    $"Place '{place}' declares its map in '{where}', but its 'origin' is not the two numbers a grid starts at, so its cells could not be placed.");
            }

            int? columns = entry.GetInt32("columns");
            int? rows = entry.GetInt32("rows");
            double? cellSize = entry.GetDouble("cellSize");
            if (columns is not > 0 || rows is not > 0 || cellSize is not > 0)
            {
                throw new InvalidOperationException(
                    $"Place '{place}' declares its map in '{where}' as {columns}x{rows} cells of {cellSize} units, which is not a grid.");
            }

            string cells = entry.GetString("kinds");
            int expected = columns.Value * rows.Value * 2;
            if (cells.Length != expected)
            {
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Place '{place}' declares its map in '{where}' as {columns}x{rows} cells and writes {cells.Length} digits where {columns * rows} cells need {expected}, so its squares would not line up with what it states."));
            }

            byte[] kinds = new byte[columns.Value * rows.Value];
            for (int index = 0; index < kinds.Length; index++)
            {
                kinds[index] = byte.Parse(
                    cells.AsSpan(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return new PlaceMap(
                place,
                new MapGrid(originX, originY, cellSize.Value, columns.Value, rows.Value),
                kinds);
        }

        return null;
    }
}
