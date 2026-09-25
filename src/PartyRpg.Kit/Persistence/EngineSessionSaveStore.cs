using System.Text.Json;
using Rusty.Engine;
using Rusty.Engine.Persistence;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// The engine's own product state store, behind the kit's save seam.
/// </summary>
/// <remarks>
/// <para>
/// The engine stores current bytes and gives them no schema meaning; the meaning is this product's document,
/// encoded with the engine's own JSON codec over the metadata <see cref="SessionSaveJson"/> hands it. That is
/// the whole save path: no schema number, no migration, no reader for an older shape, and no
/// reflection-based serialization anywhere on it.
/// </para>
/// <para>
/// <b>The store is opened the first time a save is written or read, not when the session is composed.</b>
/// The host selects the absolute persistence root before the product is created, so a product running
/// without one still plays; it simply cannot save, and its first attempt says so by name instead of the
/// product refusing to start. Opening once and keeping the handle is what makes a session's saves share one
/// store, and the handle is released with the session that owns this seam.
/// </para>
/// </remarks>
public sealed class EngineSessionSaveStore : ISessionSaveStore
{
    private readonly IEngineContext _engine;
    private readonly string _scope;
    private ProductStateStore<SessionSave>? _store;
    private bool _disposed;

    /// <summary>Creates the store a session's saves are read and written through.</summary>
    /// <param name="engine">The engine whose persistence service owns the bytes.</param>
    /// <param name="scope">The product's storage scope, which the host's persistence root is resolved against.</param>
    /// <exception cref="ArgumentNullException">The engine is null.</exception>
    /// <exception cref="ArgumentException">The scope is blank, so no store could be opened.</exception>
    public EngineSessionSaveStore(IEngineContext engine, string scope)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        _engine = engine;
        _scope = scope;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The store has been released.</exception>
    /// <exception cref="SessionSaveException">The engine could not open the product's persistence store.</exception>
    public void Write(string slot, SessionSave save)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        ArgumentNullException.ThrowIfNull(save);
        Opened().Save(slot, save);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The store has been released.</exception>
    /// <exception cref="SessionSaveException">The engine could not open the product's persistence store, or the slot does not hold a save of the current shape.</exception>
    public SessionSave? Read(string slot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        ProductStateLoad<SessionSave> load;
        try
        {
            load = Opened().Load(slot);
        }
        catch (Exception error) when (IsUndecodableSave(error))
        {
            // The engine reports a malformed document by throwing out of the codec, and what a caller needs
            // to know is which slot could not be read and why — not which serializer type refused it.
            throw new SessionSaveException(
                $"The save in slot '{slot}' cannot be read under the current schema: {error.Message}",
                [error.Message]);
        }

        return load.Present ? load.State : null;
    }

    /// <summary>Releases the engine's store handle, when one was ever opened.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _store?.Dispose();
        _store = null;
    }

    /// <summary>The engine's store, opened once, or the reason it could not be opened.</summary>
    private ProductStateStore<SessionSave> Opened()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_store is not null) return _store;

        try
        {
            _store = new ProductStateStore<SessionSave>(
                _engine,
                _scope,
                new JsonProductStateCodec<SessionSave>(SessionSaveJson.TypeInfo));
        }
        catch (EngineCallException error)
        {
            // The engine refuses to open a store when the host selected no persistence root. That is a host
            // decision rather than a save defect, and it is named with the loss it prevents: the session can
            // be played, but nothing it does can be written down.
            throw new SessionSaveException(
                $"The engine could not open the persistence store '{_scope}' that session saves are written to: {error.Message} The host selects the absolute persistence root before the product is created, so a product running without one plays but cannot save.",
                [error.Message]);
        }

        return _store;
    }

    /// <summary>
    /// Whether a failure out of the engine's load is the save's bytes rather than the store itself.
    /// </summary>
    /// <remarks>
    /// Decoding a document fails through the value types it is made of: the serializer reports malformed JSON
    /// and missing members as <see cref="JsonException"/>, and a document whose values are not legal — an
    /// identity of zero, a stack count below one, a negative purse — fails in the constructor that states
    /// that rule. A storage failure is none of these and is not reported as a save defect.
    /// </remarks>
    private static bool IsUndecodableSave(Exception error) =>
        error is JsonException or FormatException or OverflowException or ArgumentException or InvalidOperationException or NotSupportedException;
}
