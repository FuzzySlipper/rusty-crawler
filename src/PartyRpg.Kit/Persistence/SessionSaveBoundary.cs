using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// The explicit save boundary: the one place a live session is read into a save and written, and the one
/// place a save is read back.
/// </summary>
/// <remarks>
/// <para>
/// Nothing in a session captures itself. An admitted update steps the world, the clock, and the party and
/// publishes a projection; it never writes a save, and neither does pausing, holding, restarting, or ending
/// a session. A save happens when a caller asks this boundary for one — at a point the product decides is
/// meaningful — and it is written to the slot the boundary was composed with. That is what makes "save at
/// meaningful boundaries" a property of the code rather than an intention about it.
/// </para>
/// <para>
/// Reading is deliberately separate from writing: <see cref="SessionSave.Capture"/> is what a caller uses to
/// inspect or hand on a session's document without touching storage.
/// </para>
/// </remarks>
public sealed class SessionSaveBoundary
{
    /// <summary>The slot a session is saved under when the product names none.</summary>
    public const string DefaultSlot = "session";

    private readonly ISessionSaveStore _store;

    /// <summary>Creates the boundary a session is saved and loaded through.</summary>
    /// <param name="store">Where the save bytes live.</param>
    /// <param name="slot">The slot this boundary reads and writes.</param>
    /// <exception cref="ArgumentNullException">The store is null.</exception>
    /// <exception cref="ArgumentException">The slot is blank, so a save could never be found again.</exception>
    public SessionSaveBoundary(ISessionSaveStore store, string slot = DefaultSlot)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        _store = store;
        Slot = slot;
    }

    /// <summary>The slot this boundary reads and writes.</summary>
    public string Slot { get; }

    /// <summary>
    /// Reads a session into the current schema and writes it to this boundary's slot.
    /// </summary>
    /// <param name="session">The live session to save.</param>
    /// <returns>The document that was written, which is what a later load rebuilds from.</returns>
    /// <exception cref="ArgumentNullException">The session is null.</exception>
    /// <exception cref="SessionSaveException">The session holds nothing saveable, or the save could not be written.</exception>
    public SessionSave Save(PartyRpgSession session)
    {
        SessionSave save = SessionSave.Capture(session);
        _store.Write(Slot, save);
        return save;
    }

    /// <summary>Reads this boundary's slot.</summary>
    /// <returns>The save the slot holds, or null when nothing was ever written there.</returns>
    /// <exception cref="SessionSaveException">The slot holds bytes the current schema cannot decode.</exception>
    public SessionSave? Load() => _store.Read(Slot);
}
