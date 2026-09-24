using System.Text.Json;

namespace PartyRpg.Kit.Content;

/// <summary>One entry of a document: an id, and the fields the ruleset interprets.</summary>
/// <param name="Id">The entry's id, unique among entries of the same definition kind in the catalog.</param>
/// <param name="Payload">The entry exactly as the document wrote it.</param>
public readonly record struct ContentEntry(string Id, JsonElement Payload)
{
    /// <summary>Reads a property of the entry as a string, or an empty string when it is absent.</summary>
    public string GetString(string property) =>
        Payload.ValueKind == JsonValueKind.Object &&
        Payload.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>Reads a property of the entry as an integer, or null when it is absent or not one.</summary>
    public int? GetInt32(string property) =>
        Payload.ValueKind == JsonValueKind.Object &&
        Payload.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int number)
            ? number
            : null;
}
