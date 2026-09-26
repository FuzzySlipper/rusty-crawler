using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The controls a product declares for pacing a fight: the toggle and the two turn actions.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, act, service, rest, and conversation
/// controls are: the kit claims what a product declares and invents no key of its own. The toggle is its own
/// control because switching the pacing is its own act — the donor keeps one flag for it and the game's
/// manual gives it a key of its own (the manual's own account of the toggle, p.33: "Enter toggles real-time
/// and turn-based at any moment").
/// </para>
/// <para>
/// <b>Skipping and waiting are separate controls</b> because they have different consequences: a skipped turn
/// forfeits the round and owes the action that was not taken, and a waited turn is deferred to the end of the
/// round and owes nothing. One "pass" control would have to guess which of the two a player meant.
/// </para>
/// <para>
/// Attacking is deliberately not declared here: the act control is the fight's own, and in a paced fight it
/// means the same act for the actor whose turn it is. What this record adds is only what a paced fight needs
/// beyond acting.
/// </para>
/// </remarks>
public sealed record TurnIntentNames
{
    /// <summary>Creates the declared pace control names.</summary>
    /// <param name="toggle">The intent that switches between real-time and turn-based pacing.</param>
    /// <param name="skip">The intent that forfeits the current actor's turn for the rest of the round.</param>
    /// <param name="wait">The intent that defers the current actor's turn to the end of the round.</param>
    /// <param name="actionContract">The payload contract a screen's own pace controls arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public TurnIntentNames(string toggle, string skip, string wait, string actionContract)
    {
        Toggle = Require(toggle, nameof(toggle));
        Skip = Require(skip, nameof(skip));
        Wait = Require(wait, nameof(wait));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The intent that switches the pacing.</summary>
    public string Toggle { get; }

    /// <summary>The intent that forfeits the current actor's turn for the rest of the round.</summary>
    public string Skip { get; }

    /// <summary>The intent that defers the current actor's turn to the end of the round.</summary>
    public string Wait { get; }

    /// <summary>The payload contract a screen's own pace controls arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The pace control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The pace actions a screen sends, on the payload contract the product declares.</summary>
/// <remarks>
/// These are wire names, not rules: the screen reports what the player asked for, the session reads it, and
/// the fight decides what it means for the actor whose turn it is. Every one of them is also the name of a
/// declared intent, so a key and a button ask for exactly the same act, and a name here that the session
/// never reads is a control that does nothing — which is why the suite exercises each of them.
/// </remarks>
public static class TurnActions
{
    /// <summary>Switches between real-time and turn-based pacing.</summary>
    public const string Toggle = "combat.turn-based";

    /// <summary>Forfeits the current actor's turn for the rest of the round.</summary>
    public const string Skip = "combat.turn-skip";

    /// <summary>Defers the current actor's turn to the end of the round.</summary>
    public const string Wait = "combat.turn-wait";
}

/// <summary>What one update's admitted input asks of a paced fight.</summary>
/// <remarks>
/// This is what the player committed, not what the fight did with it: the toggle is applied where the mode
/// is resolved, and a skipped or waited turn is applied to whoever holds the turn when the update gets
/// there. An action that arrives when nobody holds a turn is therefore not an error — it names a turn the
/// session is not taking — and the session reports it as the refusal it is.
/// </remarks>
/// <param name="Toggle">Whether the player asked to switch the pacing.</param>
/// <param name="Skip">Whether the player asked to forfeit the current turn for the rest of the round.</param>
/// <param name="Wait">Whether the player asked to defer the current turn to the end of the round.</param>
public readonly record struct TurnControls(bool Toggle, bool Skip, bool Wait)
{
    /// <summary>Nothing was asked of the pacing.</summary>
    public static TurnControls None => default;

    /// <summary>Whether anything at all was asked.</summary>
    public bool Any => Toggle || Skip || Wait;
}

/// <summary>
/// Reads admitted input into what the player asked of a paced fight.
/// </summary>
/// <remarks>
/// <para>
/// This is the pace controls' counterpart to the movement, act, and stop readers, and it keeps the same
/// discipline: one call per admitted update, one name claimed per control, and anything else — including a
/// malformed payload — carries nothing, because an input channel must not throw on hostile bytes.
/// </para>
/// <para>
/// <b>These controls are pressed, never held.</b> A hold edge reports that a key is still down, and a toggle
/// that repeated while its key stayed down would switch the pacing back and forth every update; so would a
/// skip that repeated. A physical press carries an edge and is read once, and a direct interface claim is
/// read for the one update it arrives in, exactly as the act control's own button is.
/// </para>
/// </remarks>
public sealed class TurnInput
{
    private readonly byte[] _toggle;
    private readonly byte[] _skip;
    private readonly byte[] _wait;
    private readonly byte[] _actionContract;

    /// <summary>Creates the reader for one product's declared pace controls.</summary>
    /// <param name="names">The intent and contract names the pace controls arrive on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public TurnInput(TurnIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _toggle = Encoding.UTF8.GetBytes(names.Toggle);
        _skip = Encoding.UTF8.GetBytes(names.Skip);
        _wait = Encoding.UTF8.GetBytes(names.Wait);
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into what the player asked of the pacing.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>What was asked, none of it when nothing was.</returns>
    public TurnControls Read(ReadOnlySpan<ProductInputEvent> input)
    {
        bool toggle = false;
        bool skip = false;
        bool wait = false;
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (!IsActivation(inputEvent)) continue;
                ReadOnlySpan<byte> intent = inputEvent.Intent.Span;
                if (intent.SequenceEqual(_toggle)) toggle = true;
                else if (intent.SequenceEqual(_skip)) skip = true;
                else if (intent.SequenceEqual(_wait)) wait = true;
                continue;
            }

            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            switch (Parse(inputEvent.PayloadData.Span)?.Action)
            {
                case TurnActions.Toggle:
                    toggle = true;
                    break;
                case TurnActions.Skip:
                    skip = true;
                    break;
                case TurnActions.Wait:
                    wait = true;
                    break;
                default:
                    break;
            }
        }

        return new TurnControls(toggle, skip, wait);
    }

    /// <summary>
    /// Whether a digital event is an activation. A physical press carries an edge and a direct interface
    /// claim carries no edge at all, so its own phase and provenance are what identify it; a held edge is a
    /// key still being down rather than a new decision, and none of these controls repeats.
    /// </summary>
    private static bool IsActivation(in ProductInputEvent inputEvent) =>
        inputEvent.Edge == InputEdge.Pressed
        || inputEvent.Phase == InputPhase.DirectUi
        || inputEvent.Provenance == InputProvenance.DirectUi;

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input channel
    /// must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static TurnActionDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, TurnActionJsonContext.Default.TurnActionDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The pace action payload's wire record: an action name, and nothing else.</summary>
internal sealed record TurnActionDto(string? Action);

/// <summary>Source-generated JSON for the pace action payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TurnActionDto))]
internal sealed partial class TurnActionJsonContext : JsonSerializerContext;
