using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// The build-time metadata every session save is read and written with.
/// </summary>
/// <remarks>
/// <para>
/// The metadata is generated from the document's own types, so the save path performs no reflection
/// discovery, consults no runtime type registry, and has no resolver that could quietly widen the schema.
/// It is the <c>JsonTypeInfo&lt;SessionSave&gt;</c> the engine's JSON codec asks for, which is what keeps a
/// product saving the same way under CoreCLR and under NativeAOT.
/// </para>
/// <para>
/// Property names are camel-cased so the bytes a person inspects read as the document's sections do. The
/// names are part of the one current schema: changing one changes what the product writes, which is
/// allowed during development and is exactly why no compatibility reader exists.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionSave))]
public sealed partial class SessionSaveJsonContext : JsonSerializerContext;
