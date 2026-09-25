namespace MightAndMagic7.Import.Events;

/// <summary>
/// Opcodes this importer reads. The event language has around seventy of them; the ones that matter to a
/// world are decoded here — the two that move the party and the one that opens a container — and the rest
/// are recorded as unread rather than guessed at.
/// </summary>
public static class EvtOpcodes
{
    /// <summary>Leaves the current map through an entrance.</summary>
    public const byte Exit = 1;

    /// <summary>Moves the party to a position, optionally on another map.</summary>
    public const byte MoveToMap = 6;

    /// <summary>
    /// Opens one of the map's containers, by the index the map's own container array gives it
    /// (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:17</c>, <c>EVENT_OpenChest = 7</c>).
    /// </summary>
    public const byte OpenChest = 7;
}
