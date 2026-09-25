using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// The document's JSON metadata: the generated context, plus the one converter the schema needs.
/// </summary>
/// <remarks>
/// <para>
/// Every type in the document is described by metadata the build generated from the types themselves, so the
/// save path performs no reflection discovery, consults no runtime type registry, and has no resolver that
/// could quietly widen the schema. This is the <c>JsonTypeInfo&lt;SessionSave&gt;</c> the engine's JSON codec
/// is handed, which is what keeps a product saving the same way under CoreCLR and under NativeAOT.
/// </para>
/// <para>
/// The context's own options are copied rather than replaced, so the metadata stays the generated one and
/// the only thing this adds is <see cref="ItemCustodyJsonConverter"/> — the shape that says where an item
/// was held. Property names are camel-cased, so the bytes a person inspects read as the document's sections
/// do, and those names are part of the one current schema.
/// </para>
/// </remarks>
public static class SessionSaveJson
{
    /// <summary>The generated metadata for the whole document.</summary>
    public static JsonTypeInfo<SessionSave> TypeInfo { get; } = Resolve();

    private static JsonTypeInfo<SessionSave> Resolve()
    {
        JsonSerializerOptions options = new(SessionSaveJsonContext.Default.Options);
        options.Converters.Add(new ItemCustodyJsonConverter());
        return (JsonTypeInfo<SessionSave>)options.GetTypeInfo(typeof(SessionSave));
    }
}
