namespace MightAndMagic7.Import.Maps;

/// <summary>The runtime state that travels beside a map in its own delta payload.</summary>
/// <remarks>
/// A delta is parsed with its geometry payload as context: its counts of faces and decorations come
/// from there, which is why a delta cannot be decoded on its own. The reveal state, per-face
/// attributes, per-decoration flags, actors, sprite objects, chests, map variables, last visit time
/// and weather are all consumed; the parts that are not surfaced here are either presentation state or
/// runtime objects whose record layouts belong to the event and object formats rather than to this one,
/// and only their counts are kept.
/// </remarks>
/// <param name="Header">The delta's header.</param>
/// <param name="FaceAttributeCount">How many faces the delta carries attributes for.</param>
/// <param name="DecorationFlagCount">How many decorations the delta carries flags for.</param>
/// <param name="ActorCount">How many actors the delta carries.</param>
/// <param name="SpriteObjectCount">How many sprite objects the delta carries.</param>
/// <param name="ChestCount">How many chests the delta carries.</param>
/// <param name="LastVisitTime">When the party last visited, as the game's own timestamp.</param>
/// <param name="Weather">The map's weather.</param>
/// <param name="Doors">The map's doors; empty outdoors, which has none.</param>
public sealed record MapDelta(
    MapDeltaHeader Header,
    int FaceAttributeCount,
    int DecorationFlagCount,
    int ActorCount,
    int SpriteObjectCount,
    int ChestCount,
    long LastVisitTime,
    MapWeather Weather,
    IReadOnlyList<MapDoor> Doors);
