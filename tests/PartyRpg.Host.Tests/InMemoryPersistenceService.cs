using Rusty.Engine;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The engine's persistence service in memory, so a product's saves can be exercised without a runtime.
/// </summary>
/// <remarks>
/// <para>
/// The engine's real service writes bytes under the persistence root the host selects before the product is
/// created, and it cannot be reached from a test assembly. This double keeps the same contract — open a
/// scoped store, save and load a key, report a revision, describe and copy a blob — and keeps payloads
/// exactly as they arrived, so a test can read back the bytes the product's own engine store wrote and can
/// hand the product a document of its own making.
/// </para>
/// <para>
/// It stores bytes and assigns them no meaning: nothing here decodes a save, so a test that wants a
/// malformed one seeds payloads directly and the failure it sees comes from the product.
/// </para>
/// </remarks>
internal sealed class InMemoryPersistenceService : IPersistenceService
{
    private sealed record Saved(ulong Revision, byte[] Payload);

    private readonly Dictionary<ulong, string> _scopes = [];
    private readonly Dictionary<ulong, Saved> _blobs = [];
    private readonly Dictionary<(string Scope, string Key), Saved> _saved = [];
    private ulong _nextHandle = 1;

    /// <summary>The payload a scope and key hold, or null when nothing was written there.</summary>
    internal byte[]? Payload(string scope, string key) =>
        _saved.TryGetValue((scope, key), out Saved? saved) ? saved.Payload : null;

    /// <summary>Puts bytes under a scope and key, which is how a test hands a product a save of its own.</summary>
    internal void Seed(string scope, string key, byte[] payload) => _saved[(scope, key)] = new Saved(1, payload);

    /// <inheritdoc />
    public PersistenceStore OpenStore(PersistenceOpenRequest request)
    {
        ulong handle = _nextHandle++;
        _scopes.Add(handle, request.Scope);
        return new PersistenceStore(new PersistenceStoreHandle(handle), () => _scopes.Remove(handle));
    }

    /// <inheritdoc />
    public PersistenceSaveReceipt Save(PersistenceSaveRequest request)
    {
        string scope = _scopes[request.Store.Handle.Value];
        (string Scope, string Key) key = (scope, request.Key);
        _saved.TryGetValue(key, out Saved? previous);
        ulong revision = (previous?.Revision ?? 0) + 1;
        _saved[key] = new Saved(revision, request.Payload.ToArray());
        return new PersistenceSaveReceipt(PersistenceSaveOutcome.Saved, revision);
    }

    /// <inheritdoc />
    public PersistenceDeleteReceipt Delete(PersistenceDeleteRequest request)
    {
        (string Scope, string Key) key = (_scopes[request.Store.Handle.Value], request.Key);
        _saved.TryGetValue(key, out Saved? previous);
        bool matches = request.RevisionGuard switch
        {
            PersistenceRevisionGuard.Any => true,
            PersistenceRevisionGuard.Exact => previous is not null && previous.Revision == request.ExpectedRevision,
            PersistenceRevisionGuard.Absent => previous is null,
            _ => false,
        };

        if (!matches) return new PersistenceDeleteReceipt(PersistenceDeleteOutcome.RevisionConflict, previous?.Revision ?? 0);
        if (previous is null) return new PersistenceDeleteReceipt(PersistenceDeleteOutcome.Missing, 0);
        _saved.Remove(key);
        return new PersistenceDeleteReceipt(PersistenceDeleteOutcome.Deleted, previous.Revision);
    }

    /// <inheritdoc />
    public PersistenceBlob Load(PersistenceLoadRequest request)
    {
        string scope = _scopes[request.Store.Handle.Value];
        _saved.TryGetValue((scope, request.Key), out Saved? saved);
        ulong handle = _nextHandle++;
        _blobs.Add(handle, saved ?? new Saved(0, []));
        return new PersistenceBlob(new PersistenceBlobHandle(handle), () => _blobs.Remove(handle));
    }

    /// <inheritdoc />
    public PersistenceBlobInfo DescribeBlob(PersistenceBlob blob)
    {
        Saved saved = _blobs[blob.Handle.Value];
        return new PersistenceBlobInfo(saved.Revision != 0, saved.Revision, (nuint)saved.Payload.Length);
    }

    /// <inheritdoc />
    public void CopyBlob(PersistenceCopyBlobRequest request) =>
        _blobs[request.Blob.Handle.Value].Payload.CopyTo(request.Destination.Span);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> ReadBlobBytes(PersistenceBlob blob) => _blobs[blob.Handle.Value].Payload;
}
