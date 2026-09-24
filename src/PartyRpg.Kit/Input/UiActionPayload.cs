using System.Text.Json;
using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Input;

/// <summary>
/// One semantic action the DOM companion asked for. The wire shape is deliberately small: an action
/// name plus whatever fields that action carries, so a screen can be extended without a new channel.
/// </summary>
/// <param name="Name">The action's wire name.</param>
public sealed record UiActionPayload(string Name)
{
    /// <summary>Hold the session, sent while it is running.</summary>
    public const string PauseSession = "session.pause";

    /// <summary>Release a held session, sent while it is paused.</summary>
    public const string ResumeSession = "session.resume";

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input
    /// channel must not throw on hostile bytes, and a caller that receives null simply has no action
    /// to apply.
    /// </summary>
    public static UiActionPayload? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        UiActionDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(utf8, UiActionJsonContext.Default.UiActionDto);
        }
        catch (JsonException)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(dto?.Action) ? null : new UiActionPayload(dto.Action);
    }
}

/// <summary>The action payload's wire record.</summary>
internal sealed record UiActionDto(string? Action);

/// <summary>Source-generated JSON for the action payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UiActionDto))]
internal sealed partial class UiActionJsonContext : JsonSerializerContext;
