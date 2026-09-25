namespace PartyRpg.Kit.Persistence;

/// <summary>
/// Where a session's save lives: the seam the explicit save boundary writes and reads through.
/// </summary>
/// <remarks>
/// <para>
/// The kit states the operation and the engine's own product state store performs it, so the bytes are
/// stored by the owner of durable storage rather than by a file path this layer invented. A store keeps
/// current bytes and assigns them no schema meaning: there is no schema number, no migration, and no reader
/// for an older shape behind this interface, and a slot that does not decode is a failure to report rather
/// than an older save to interpret.
/// </para>
/// <para>
/// A store holds a resource — the engine's persistence handle — so it is disposable, and the session it was
/// handed to releases it with itself.
/// </para>
/// </remarks>
public interface ISessionSaveStore : IDisposable
{
    /// <summary>Writes a session's save under a slot, replacing whatever that slot held.</summary>
    /// <param name="slot">The slot to write; a slot names one save within the product's storage.</param>
    /// <param name="save">The document to write.</param>
    void Write(string slot, SessionSave save);

    /// <summary>Reads the save a slot holds.</summary>
    /// <param name="slot">The slot to read.</param>
    /// <returns>The save, or null when the slot holds nothing.</returns>
    /// <exception cref="SessionSaveException">The slot holds bytes that the current schema cannot decode.</exception>
    SessionSave? Read(string slot);
}
