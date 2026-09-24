using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Maps;

/// <summary>Decodes the shipped map payloads into normalized world data.</summary>
/// <remarks>
/// A map payload is one ordered walk whose later offsets derive from earlier counts, so the decoder
/// consumes every field in file order — including the parts no consumer needs — and fails when a walk
/// does not end exactly at the payload's end. A delta payload is decoded with its map as context and is
/// never decoded on its own.
/// </remarks>
public static class MapDecoder
{
    /// <summary>Decodes an outdoor payload without its delta.</summary>
    /// <param name="payload">The decoded <c>.odm</c> entry.</param>
    public static OutdoorMap DecodeOutdoor(LodPayload payload) => OutdoorMapReader.Read(payload, null);

    /// <summary>Decodes an outdoor payload together with its delta.</summary>
    /// <param name="payload">The decoded <c>.odm</c> entry.</param>
    /// <param name="delta">The decoded <c>.ddm</c> entry for the same map.</param>
    public static OutdoorMap DecodeOutdoor(LodPayload payload, LodPayload delta)
    {
        RequireSameMap(payload, delta);
        return OutdoorMapReader.Read(payload, delta);
    }

    /// <summary>Decodes an indoor payload without its delta, which leaves the map without doors.</summary>
    /// <param name="payload">The decoded <c>.blv</c> entry.</param>
    public static IndoorMap DecodeIndoor(LodPayload payload) => IndoorMapReader.Read(payload, null);

    /// <summary>Decodes an indoor payload together with its delta, which is where the doors are.</summary>
    /// <param name="payload">The decoded <c>.blv</c> entry.</param>
    /// <param name="delta">The decoded <c>.dlv</c> entry for the same map.</param>
    public static IndoorMap DecodeIndoor(LodPayload payload, LodPayload delta)
    {
        RequireSameMap(payload, delta);
        return IndoorMapReader.Read(payload, delta);
    }

    /// <summary>Decodes every map the per-map table names, from the container that holds map files.</summary>
    /// <remarks>
    /// A map that does not decode is recorded with its id, entry name, and the field and offset that
    /// stopped it, so a partially readable installation produces a report that names what is missing
    /// rather than a shorter list. Only a payload defect is recorded: any other exception is a defect
    /// in this decoder and is left to propagate.
    /// </remarks>
    /// <param name="install">The installation to read the per-map table and the maps from.</param>
    public static MapDecodeReport DecodeAll(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        MapStatsTable table = MapStatsTable.Read(install);
        LodArchive maps = install.Archive(Mm7TableSources.AssetsArchive);

        List<MapDecodeOutcome> outcomes = new(table.Maps.Count);
        foreach (MapStatsRecord map in table.Maps)
        {
            try
            {
                outcomes.Add(new MapDecodeOutcome(map, DecodeNamed(maps, map), null));
            }
            catch (LodFormatException failure)
            {
                outcomes.Add(new MapDecodeOutcome(map, null, new MapDecodeFailure(map.Id, map.FileName, failure.Message)));
            }
        }

        return MapDecodeReport.Create(outcomes);
    }

    private static DecodedMap DecodeNamed(LodArchive maps, MapStatsRecord map)
    {
        MapKind kind = Path.GetExtension(map.FileName).ToLowerInvariant() switch
        {
            ".odm" => MapKind.Outdoor,
            ".blv" => MapKind.Indoor,
            _ => throw new LodFormatException($"{map.FileName} is not a map file this decoder reads."),
        };

        // Every shipped map has its delta in the same container under the same stem, and a map decoded
        // without one would silently lose its doors or its runtime state.
        string deltaName = Path.ChangeExtension(map.FileName, kind == MapKind.Indoor ? ".dlv" : ".ddm");
        LodPayload payload = maps.Read(map.FileName);
        LodPayload delta = maps.Read(deltaName);
        return kind == MapKind.Indoor ? DecodeIndoor(payload, delta) : DecodeOutdoor(payload, delta);
    }

    private static void RequireSameMap(LodPayload payload, LodPayload delta)
    {
        string mapStem = Path.GetFileNameWithoutExtension(payload.Entry.Name);
        string deltaStem = Path.GetFileNameWithoutExtension(delta.Entry.Name);
        if (!string.Equals(mapStem, deltaStem, StringComparison.OrdinalIgnoreCase))
        {
            throw new LodFormatException(
                $"'{delta.Entry.Name}' belongs to another map, so it is not the delta of '{payload.Entry.Name}'.");
        }
    }
}
