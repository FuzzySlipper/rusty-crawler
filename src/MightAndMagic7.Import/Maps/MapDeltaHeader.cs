namespace MightAndMagic7.Import.Maps;

/// <summary>The header both delta payloads start with.</summary>
/// <remarks>
/// The header carries the map's runtime state at the moment it was written. Every shipped delta is
/// zeroed here, which is what a save file's first visit looks like, so the fields describe the map
/// rather than the level's geometry: the level's own counts are in the geometry payload.
/// </remarks>
/// <param name="RespawnCount">How many times the map's population has respawned.</param>
/// <param name="LastRespawnDay">The day the map last respawned; zero means the party has never been here.</param>
/// <param name="Reputation">The map's reputation.</param>
/// <param name="AlertStatus">How alerted the map's population is.</param>
/// <param name="TotalFacesCount">How many faces the map has.</param>
/// <param name="DecorationCount">How many decorations the map has.</param>
/// <param name="BmodelCount">How many placed models the map has; always zero indoors.</param>
/// <param name="Field1C">A field no donor reads; its width is known and its meaning is not.</param>
/// <param name="Field20">A field no donor reads; its width is known and its meaning is not.</param>
/// <param name="Field24">A field no donor reads; its width is known and its meaning is not.</param>
public readonly record struct MapDeltaHeader(
    int RespawnCount,
    int LastRespawnDay,
    int Reputation,
    int AlertStatus,
    uint TotalFacesCount,
    uint DecorationCount,
    uint BmodelCount,
    int Field1C,
    int Field20,
    int Field24);
