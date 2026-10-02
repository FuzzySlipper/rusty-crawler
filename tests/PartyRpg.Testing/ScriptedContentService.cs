using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>
/// An engine content service that admits whatever it is handed and records it.
/// </summary>
/// <remarks>
/// A place's collision artifact reaches the spatial service through a content reference, so a test of an
/// admission needs the reference and nothing else. Every other operation throws: the product does not use
/// it, and a double that answered it would let the product start depending on it unproven.
/// </remarks>
public sealed class ScriptedContentService : IContentService
{
    private ulong _next;

    /// <summary>Every document admitted, in order.</summary>
    public List<ContentAdmissionRequest> Admitted { get; } = [];

    /// <summary>How many admitted references were released.</summary>
    public int ReleasedReferences { get; private set; }

    /// <inheritdoc />
    public ContentReference AdmitReference(ContentAdmissionRequest request)
    {
        Admitted.Add(request);
        return new ContentReference(new ContentReferenceHandle(++_next), () => ReleasedReferences++);
    }

    /// <inheritdoc />
    public PortableAsset LoadPortableAsset(PortableAssetLoadRequest request) => throw Unsupported();

    /// <inheritdoc />
    public PortableAssetReadoutResult ReadPortableAsset(PortableAsset request) => throw Unsupported();

    /// <inheritdoc />
    public ContentReference OpenPortableAssetMember(PortableAssetMemberRequest request) => throw Unsupported();

    /// <inheritdoc />
    public ReadOnlyMemory<ContentBundleInfo> ListBundles() => throw Unsupported();

    /// <inheritdoc />
    public ContentBundle OpenBundle(ContentBundleOpenRequest request) => throw Unsupported();

    /// <inheritdoc />
    public ContentBundle OpenContainer(ContentContainerOpenRequest request) => throw Unsupported();

    /// <inheritdoc />
    public ContentSha256 ReadBundleIdentity(ContentBundle request) => throw Unsupported();

    /// <inheritdoc />
    public ReadOnlyMemory<ContentReferenceInfo> ReadBundleFiles(ContentBundle request) => throw Unsupported();

    /// <inheritdoc />
    public ContentReference OpenBundleReference(ContentBundleReferenceRequest request) => throw Unsupported();

    /// <inheritdoc />
    public ContentReference OpenReference(ContentOpenRequest request) => throw Unsupported();

    /// <inheritdoc />
    public ContentReference ResolveReference(ContentResolveRequest request) => throw Unsupported();

    /// <inheritdoc />
    public ReadOnlyMemory<ContentReferenceInfo> ReadReferenceInfo(ContentReference request) => throw Unsupported();

    /// <inheritdoc />
    public ReadOnlyMemory<byte> ReadBytes(ContentReadBytesRequest request) => throw Unsupported();

    private static NotSupportedException Unsupported() =>
        new("The product admits place artifacts only, so the scripted content service answers that one operation.");
}
