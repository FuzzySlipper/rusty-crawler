using System.Text;
using PartyRpg.Kit.Input;
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

/// <summary>The stops a player asked for, read from the admitted input of each update, in the order they arrived.</summary>
public sealed class RestInput
{
    private readonly (byte[] Intent, RestKind Kind)[] _intents;
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared stop controls.</summary>
    /// <param name="names">The stop controls the host declared.</param>
    public RestInput(RestIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _intents =
        [
            (Encoding.UTF8.GetBytes(names.Rest), RestKind.Rest),
            (Encoding.UTF8.GetBytes(names.Camp), RestKind.Camp),
            (Encoding.UTF8.GetBytes(names.WaitUntilDawn), RestKind.WaitUntilDawn),
            (Encoding.UTF8.GetBytes(names.WaitAnHour), RestKind.WaitAnHour),
            (Encoding.UTF8.GetBytes(names.WaitFiveMinutes), RestKind.WaitFiveMinutes),
        ];
        _actionContract = names.ActionContract;
    }

    /// <summary>The stops this update carried.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<RestKind> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<RestKind> stops = [];
        foreach (ProductInputEvent inputEvent in inbox.Digital)
        {
            if (!InputEvents.IsActivation(inputEvent)) continue;
            foreach ((byte[] intent, RestKind kind) in _intents)
            {
                if (!inputEvent.Intent.Span.SequenceEqual(intent)) continue;
                stops.Add(kind);
                break;
            }
        }

        foreach (UiAction action in inbox.Take(_actionContract, name => Kind(name) is not null)) stops.Add(Kind(action.Name)!.Value);
        return stops;
    }

    private static RestKind? Kind(string action) => action switch
    {
        RestActions.Rest => RestKind.Rest,
        RestActions.Camp => RestKind.Camp,
        RestActions.WaitUntilDawn => RestKind.WaitUntilDawn,
        RestActions.WaitAnHour => RestKind.WaitAnHour,
        RestActions.WaitFiveMinutes => RestKind.WaitFiveMinutes,
        _ => null,
    };
}
