namespace PartyRpg.Kit.World;

/// <summary>
/// A whole place-state ledger as data: the game day it had reached, and one state per place it held.
/// </summary>
/// <remarks>
/// Capture is a value rather than a live view so it can be handed to a save without the save learning
/// anything about the world it came from, and so a test can build a ledger directly from one. The
/// recording itself — where the bytes go and what schema wraps them — belongs to the engine state store
/// and to a later stone; nothing here reads or writes storage.
/// </remarks>
/// <param name="ElapsedGameDays">The elapsed game day the ledger had reached when it was captured.</param>
/// <param name="States">One state per place the ledger held state for, in content order.</param>
public sealed record PlaceStateLedgerSnapshot(int ElapsedGameDays, IReadOnlyList<PlaceState> States);
