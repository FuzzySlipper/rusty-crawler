using System.Globalization;
using System.Text;
using System.Text.Json;
using Rusty.Engine;

namespace PartyRpg.Kit.Input;

/// <summary>One semantic action a screen sent: its name, the contract it arrived on, and the fields it carries.</summary>
/// <remarks>
/// A field is read by name from the payload that was parsed once, when the update's input was read, so every
/// reader that looks at the same action reads the same parse. A field of the wrong shape reads as absent, which
/// is what each reader already does with a field that is not there.
/// </remarks>
public sealed class UiAction
{
    private readonly JsonElement _payload;

    internal UiAction(string contract, string name, JsonElement payload)
    {
        Contract = contract;
        Name = name;
        _payload = payload;
    }

    /// <summary>The payload contract the action arrived on.</summary>
    public string Contract { get; }

    /// <summary>The action's name.</summary>
    public string Name { get; }

    /// <summary>Whether a reader has taken this action.</summary>
    public bool Claimed { get; internal set; }

    /// <summary>A field as text: a string as written, a number as its own digits, or empty when there is neither.</summary>
    /// <param name="field">The field's name on the wire.</param>
    public string Text(string field) =>
        _payload.TryGetProperty(field, out JsonElement value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                _ => string.Empty,
            }
            : string.Empty;

    /// <summary>A field as a whole number, or null when it is not one.</summary>
    /// <param name="field">The field's name on the wire.</param>
    public int? Int(string field) =>
        _payload.TryGetProperty(field, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number
            : null;

    /// <summary>A field as a positive identity number, or null when it does not name one.</summary>
    /// <param name="field">The field's name on the wire.</param>
    public ulong? Identity(string field) =>
        ulong.TryParse(Text(field), NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) && value > 0 ? value : null;
}

/// <summary>
/// One admitted update's input as its readers see it: the digital events, and every semantic action parsed once.
/// </summary>
/// <remarks>
/// <para>
/// Every control a session reads arrives either as a digital intent — a key, or a direct claim from the interface —
/// or as a payload on a semantic-action contract. The payloads are parsed here, once per update, rather than once
/// per reader that scans for its own; a payload that is not JSON or names no action is nothing any reader could
/// act on, and is dropped where it is read rather than thrown out of the update.
/// </para>
/// <para>
/// <b>A reader takes what it acts on, and what nobody took is visible.</b> Each reader claims the actions it
/// reads, by contract and name, so an action that arrived and that nothing in the session took — a purchase with
/// no counter open, a mixture with no mixing, a name no mechanism knows — is left unclaimed rather than silently
/// dropped, and the session reports it.
/// </para>
/// </remarks>
public sealed class ActionInbox
{
    private readonly ProductInputEvent[] _digital;
    private readonly List<UiAction> _actions;

    /// <summary>Reads one update's admitted input.</summary>
    /// <param name="input">The update's admitted input.</param>
    public ActionInbox(ReadOnlySpan<ProductInputEvent> input)
    {
        List<ProductInputEvent> digital = [];
        _actions = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                digital.Add(inputEvent);
                continue;
            }

            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (Parse(inputEvent.PayloadData.Span) is not { } payload) continue;
            if (!payload.TryGetProperty("action", out JsonElement name) || name.ValueKind != JsonValueKind.String) continue;
            string action = name.GetString() ?? string.Empty;
            if (action.Trim().Length == 0) continue;
            _actions.Add(new UiAction(Encoding.UTF8.GetString(inputEvent.PayloadContract.Span), action, payload));
        }

        _digital = [.. digital];
    }

    /// <summary>An update that carried nothing.</summary>
    public static ActionInbox Empty => new([]);

    /// <summary>The digital events the update carried, in the order they arrived.</summary>
    public ReadOnlySpan<ProductInputEvent> Digital => _digital;

    /// <summary>Every action the update carried, in the order it arrived.</summary>
    public IReadOnlyList<UiAction> Actions => _actions;

    /// <summary>Whether a digital intent was activated in this update, by a press or a direct claim.</summary>
    /// <param name="intent">The intent's name, as the engine carries it.</param>
    public bool Activated(ReadOnlySpan<byte> intent)
    {
        foreach (ProductInputEvent inputEvent in _digital)
        {
            if (inputEvent.Intent.Span.SequenceEqual(intent) && InputEvents.IsActivation(inputEvent)) return true;
        }

        return false;
    }

    /// <summary>Takes every action on a contract whose name a reader acts on, in the order they arrived.</summary>
    /// <param name="contract">The contract the reader claims its actions on.</param>
    /// <param name="names">Whether a name is one the reader acts on.</param>
    /// <returns>The actions taken, each now claimed.</returns>
    public IReadOnlyList<UiAction> Take(string contract, Func<string, bool> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        List<UiAction> taken = [];
        foreach (UiAction action in _actions)
        {
            if (!string.Equals(action.Contract, contract, StringComparison.Ordinal) || !names(action.Name)) continue;
            action.Claimed = true;
            taken.Add(action);
        }

        return taken;
    }

    /// <summary>Takes every action on a contract with one name.</summary>
    /// <param name="contract">The contract the reader claims its actions on.</param>
    /// <param name="name">The action's name.</param>
    public IReadOnlyList<UiAction> Take(string contract, string name) =>
        Take(contract, candidate => string.Equals(candidate, name, StringComparison.Ordinal));

    /// <summary>The actions nobody took, on the contracts given.</summary>
    /// <param name="contracts">The contracts a session claims its actions on; others belong to somebody else.</param>
    public IEnumerable<UiAction> Unclaimed(IReadOnlySet<string> contracts) =>
        _actions.Where(action => !action.Claimed && contracts.Contains(action.Contract));

    private static JsonElement? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            Utf8JsonReader reader = new(utf8);
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
