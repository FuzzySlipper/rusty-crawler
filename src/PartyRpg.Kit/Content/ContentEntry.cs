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

    /// <summary>Reads a property of the entry as a number, or null when it is absent or not one.</summary>
    public double? GetDouble(string property) =>
        Payload.ValueKind == JsonValueKind.Object &&
        Payload.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double number)
            ? number
            : null;

    /// <summary>
    /// Reads a property that names something, whether content wrote it as a string or as a number.
    /// </summary>
    /// <remarks>
    /// An identity is an identity: a place whose id is the number 12 and one whose id is the string
    /// "12" mean the same place, and a tool that writes one form should not be unable to feed a loader
    /// that expects the other.
    /// </remarks>
    public string GetId(string property)
    {
        if (Payload.ValueKind != JsonValueKind.Object || !Payload.TryGetProperty(property, out JsonElement value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        };
    }

    /// <summary>Whether the entry declares a property at all.</summary>
    public bool Has(string property) =>
        Payload.ValueKind == JsonValueKind.Object && Payload.TryGetProperty(property, out _);

    /// <summary>Reads a property of the entry as an array, or an empty list when it is absent or not one.</summary>
    public IReadOnlyList<JsonElement> GetArray(string property) =>
        Payload.ValueKind == JsonValueKind.Object &&
        Payload.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : [];

    /// <summary>Reads a string property of an element this entry's arrays carry.</summary>
    public static string ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>Reads a naming property of an element this entry's arrays carry, whether it is a string or a number.</summary>
    public static string ReadId(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out JsonElement value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        };
    }

    /// <summary>Reads a numeric property of an element this entry's arrays carry, or null when it is absent.</summary>
    public static double? ReadDouble(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double number)
            ? number
            : null;

    /// <summary>Reads a property of the entry as an integer, or null when it is absent or not one.</summary>
    public int? GetInt32(string property) =>
        Payload.ValueKind == JsonValueKind.Object &&
        Payload.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int number)
            ? number
            : null;
}
