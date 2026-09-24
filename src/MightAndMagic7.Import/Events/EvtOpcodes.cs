namespace MightAndMagic7.Import.Events;

/// <summary>
/// Opcodes this importer reads. The event language has around seventy of them; only the two that move
/// the party are decoded here, and the rest are recorded as unread rather than guessed at.
/// </summary>
public static class EvtOpcodes
{
    /// <summary>Leaves the current map through an entrance.</summary>
    public const byte Exit = 1;

    /// <summary>Moves the party to a position, optionally on another map.</summary>
    public const byte MoveToMap = 6;
}
