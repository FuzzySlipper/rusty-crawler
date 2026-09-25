using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The controls a product declares for stopping: a rest, a camp, and the waits.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, use, and service controls
/// are: the kit claims what a product declares and invents no key of its own. Every stop has its own name
/// because every stop is a different act — one key cannot mean "rest until healed" and "wait without
/// healing" — and the payload contract carries the same names for a screen's own buttons, so a key and a
/// button ask for exactly the same stop.
/// </para>
/// <para>
/// The waits are declared apart from the rest deliberately: what a player asks for is what the session
/// applies, and a product that offered one "rest" control would have to guess which of the four the player
/// meant.
/// </para>
/// </remarks>
public sealed record RestIntentNames
{
    /// <summary>Creates the declared stop control names.</summary>
    /// <param name="rest">The intent that rests and heals where the party stands.</param>
    /// <param name="camp">The intent that makes camp in the open.</param>
    /// <param name="waitUntilDawn">The intent that waits until the next dawn.</param>
    /// <param name="waitAnHour">The intent that waits an hour.</param>
    /// <param name="waitFiveMinutes">The intent that waits a short interval.</param>
    /// <param name="actionContract">The payload contract a screen's stop commands arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public RestIntentNames(
        string rest,
        string camp,
        string waitUntilDawn,
        string waitAnHour,
        string waitFiveMinutes,
        string actionContract)
    {
        Rest = Require(rest, nameof(rest));
        Camp = Require(camp, nameof(camp));
        WaitUntilDawn = Require(waitUntilDawn, nameof(waitUntilDawn));
        WaitAnHour = Require(waitAnHour, nameof(waitAnHour));
        WaitFiveMinutes = Require(waitFiveMinutes, nameof(waitFiveMinutes));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The intent that rests and heals where the party stands.</summary>
    public string Rest { get; }

    /// <summary>The intent that makes camp in the open.</summary>
    public string Camp { get; }

    /// <summary>The intent that waits until the next dawn.</summary>
    public string WaitUntilDawn { get; }

    /// <summary>The intent that waits an hour.</summary>
    public string WaitAnHour { get; }

    /// <summary>The intent that waits a short interval.</summary>
    public string WaitFiveMinutes { get; }

    /// <summary>The payload contract a screen's stop commands arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The stop control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// The stop actions a screen sends, on the payload contract the product declares.
/// </summary>
/// <remarks>
/// These are wire names, not rules: the screen reports what the player asked for, the session reads it into
/// a kind of stop, and the mechanism decides whether the party may take it and what it costs. A name here
/// that the session never reads is a control that does nothing, which is why every one of them is exercised
/// by the suite.
/// </remarks>
public static class RestActions
{
    /// <summary>Rests and heals where the party stands, under a roof.</summary>
    public const string Rest = "rest.rest";

    /// <summary>Makes camp in the open, where the ground and the night have their say.</summary>
    public const string Camp = "rest.camp";

    /// <summary>Waits until the next dawn, without healing.</summary>
    public const string WaitUntilDawn = "rest.wait-dawn";

    /// <summary>Waits an hour, without healing.</summary>
    public const string WaitAnHour = "rest.wait-hour";

    /// <summary>Waits five minutes, without healing.</summary>
    public const string WaitFiveMinutes = "rest.wait-five-minutes";
}

/// <summary>
/// Reads admitted input into the stops a player asked for, in the order they arrived.
/// </summary>
/// <remarks>
/// <para>
/// This is the stop controls' counterpart to the creation and service readers: the session consults it
/// inside its one admitted update, and every command it reads is discrete — a rest is asked for once — so
/// nothing is remembered between updates. A digital event on a declared control carries its stop; a payload
/// on the declared contract carries the action it names. Anything else, including a malformed payload,
/// carries no stop: an input channel must not throw on hostile bytes, and a caller that receives nothing
/// simply has nothing to apply.
/// </para>
/// <para>
/// Several stops in one update are several stops, applied in the order they arrived: a party that asks to
/// wait an hour twice waits two hours, and one that rests and then camps does both, exactly as the update's
/// own sequence of commands says.
/// </para>
/// </remarks>
public sealed class RestInput
{
    private readonly byte[] _rest;
    private readonly byte[] _camp;
    private readonly byte[] _waitUntilDawn;
    private readonly byte[] _waitAnHour;
    private readonly byte[] _waitFiveMinutes;
    private readonly byte[] _actionContract;

    /// <summary>Creates the reader for one product's declared stop controls.</summary>
    /// <param name="names">The intent and contract names the commands arrive on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public RestInput(RestIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _rest = Encoding.UTF8.GetBytes(names.Rest);
        _camp = Encoding.UTF8.GetBytes(names.Camp);
        _waitUntilDawn = Encoding.UTF8.GetBytes(names.WaitUntilDawn);
        _waitAnHour = Encoding.UTF8.GetBytes(names.WaitAnHour);
        _waitFiveMinutes = Encoding.UTF8.GetBytes(names.WaitFiveMinutes);
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into the stops it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The stops the player asked for, in the order they arrived.</returns>
    public IReadOnlyList<RestKind> Read(ReadOnlySpan<ProductInputEvent> input)
    {
        List<RestKind> stops = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (!IsActivation(inputEvent)) continue;
                if (inputEvent.Intent.Span.SequenceEqual(_rest)) stops.Add(RestKind.Rest);
                else if (inputEvent.Intent.Span.SequenceEqual(_camp)) stops.Add(RestKind.Camp);
                else if (inputEvent.Intent.Span.SequenceEqual(_waitUntilDawn)) stops.Add(RestKind.WaitUntilDawn);
                else if (inputEvent.Intent.Span.SequenceEqual(_waitAnHour)) stops.Add(RestKind.WaitAnHour);
                else if (inputEvent.Intent.Span.SequenceEqual(_waitFiveMinutes)) stops.Add(RestKind.WaitFiveMinutes);
                continue;
            }

            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            if (Kind(inputEvent.PayloadData.Span) is { } kind) stops.Add(kind);
        }

        return stops;
    }

    /// <summary>
    /// Whether a digital event is an activation. A physical press carries an edge; a direct interface
    /// claim is admitted with no edge at all, so its own phase and provenance are what identify it.
    /// </summary>
    private static bool IsActivation(in ProductInputEvent inputEvent) =>
        inputEvent.Edge == InputEdge.Pressed
        || inputEvent.Phase == InputPhase.DirectUi
        || inputEvent.Provenance == InputProvenance.DirectUi;

    /// <summary>Reads one payload action into the stop it names, or null when it names none of ours.</summary>
    private static RestKind? Kind(ReadOnlySpan<byte> utf8)
    {
        RestActionDto? action = Parse(utf8);
        return action?.Action switch
        {
            RestActions.Rest => RestKind.Rest,
            RestActions.Camp => RestKind.Camp,
            RestActions.WaitUntilDawn => RestKind.WaitUntilDawn,
            RestActions.WaitAnHour => RestKind.WaitAnHour,
            RestActions.WaitFiveMinutes => RestKind.WaitFiveMinutes,
            _ => null,
        };
    }

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input
    /// channel must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static RestActionDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, RestActionJsonContext.Default.RestActionDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The stop action payload's wire record: an action name, and nothing else.</summary>
internal sealed record RestActionDto(string? Action);

/// <summary>Source-generated JSON for the stop action payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RestActionDto))]
internal sealed partial class RestActionJsonContext : JsonSerializerContext;
