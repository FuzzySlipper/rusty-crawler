using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Maps;

/// <summary>One map's decode result: the map, or the reason it did not decode.</summary>
public sealed class MapDecodeOutcome
{
    internal MapDecodeOutcome(MapStatsRecord map, DecodedMap? decoded, MapDecodeFailure? failure)
    {
        if ((decoded is null) == (failure is null))
        {
            throw new ArgumentException("An outcome carries either a decoded map or a failure, not both and not neither.");
        }

        Map = map;
        Decoded = decoded;
        Failure = failure;
    }

    /// <summary>The per-map metadata row this outcome came from.</summary>
    public MapStatsRecord Map { get; }

    /// <summary>The decoded map, or null when it failed.</summary>
    public DecodedMap? Decoded { get; }

    /// <summary>The failure, or null when the map decoded.</summary>
    public MapDecodeFailure? Failure { get; }

    /// <summary>Whether the map decoded.</summary>
    public bool Succeeded => Decoded is not null;
}
