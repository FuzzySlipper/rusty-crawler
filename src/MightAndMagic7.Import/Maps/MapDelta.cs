namespace MightAndMagic7.Import.Maps;

/// <summary>The runtime state that travels beside a map in its own delta payload.</summary>
/// <remarks>
/// A delta is parsed with its geometry payload as context: its counts of faces and decorations come
/// from there, which is why a delta cannot be decoded on its own. The reveal state, per-face
/// attributes, per-decoration flags, actors, map variables, last visit time and weather are consumed and
/// not surfaced: they are presentation state, and the record layouts of the objects they hold belong to
/// the event and object formats rather than to this one. Its actors are surfaced because an actor whose
/// record names an NPC is somebody standing in the world; its sprite objects and chests are surfaced
/// because a shipped delta's initial ones are the items and containers a place holds, and nothing else in
/// the data carries them.
/// </remarks>
/// <param name="Header">The delta's header.</param>
/// <param name="FaceAttributeCount">How many faces the delta carries attributes for.</param>
/// <param name="DecorationFlagCount">How many decorations the delta carries flags for.</param>
/// <param name="ActorCount">How many actors the delta carries.</param>
/// <param name="Actors">The actors the delta carries, in payload order.</param>
/// <param name="SpriteObjects">The sprite objects the delta carries, in payload order.</param>
/// <param name="Chests">The chests the delta carries, in payload order.</param>
/// <param name="LastVisitTime">When the party last visited, as the game's own timestamp.</param>
/// <param name="Weather">The map's weather.</param>
/// <param name="Doors">The map's doors; empty outdoors, which has none.</param>
public sealed record MapDelta(
    MapDeltaHeader Header,
    int FaceAttributeCount,
    int DecorationFlagCount,
    int ActorCount,
    IReadOnlyList<MapActor> Actors,
    IReadOnlyList<MapSpriteObject> SpriteObjects,
    IReadOnlyList<MapChest> Chests,
    long LastVisitTime,
    MapWeather Weather,
    IReadOnlyList<MapDoor> Doors)
{
    /// <summary>How many of the delta's actors are people rather than monsters.</summary>
    public int PersonCount => Actors.Count(actor => actor.IsPerson);

    /// <summary>How many sprite objects the delta carries.</summary>
    public int SpriteObjectCount => SpriteObjects.Count;

    /// <summary>How many chest records the delta carries, which is the runtime array's size and not a count of placed containers.</summary>
    public int ChestCount => Chests.Count;
}
