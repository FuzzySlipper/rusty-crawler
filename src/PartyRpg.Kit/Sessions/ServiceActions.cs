using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PartyRpg.Kit.Services;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The service controls a product declares, by the names a player's commands arrive on.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, and use controls are:
/// the kit claims what a product declares and invents no key of its own. Two names are needed because a
/// product offers two ways to act at a counter — a digital intent for a key that leaves the service, and
/// the payload contract the screen sends its commands on — and both are read inside the one admitted
/// update, so a key and a button ask for exactly the same thing.
/// </para>
/// <para>
/// Entering a service is deliberately not one of these controls. A party enters by using the person the
/// interaction mechanism reached, on the use control the product already declares, so there is one way to
/// walk up to somebody rather than two.
/// </para>
/// </remarks>
public sealed record ServiceIntentNames
{
    /// <summary>Creates the declared service control names.</summary>
    /// <param name="leave">The intent a request to leave the counter arrives on.</param>
    /// <param name="actionContract">The payload contract the screen's service commands arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public ServiceIntentNames(string leave, string actionContract)
    {
        Leave = Require(leave, nameof(leave));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The intent that leaves the counter the party stands at.</summary>
    public string Leave { get; }

    /// <summary>The payload contract the screen's service commands arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The service control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// The actions a service screen sends, on the payload contract the product declares.
/// </summary>
/// <remarks>
/// These are wire names, not rules: the screen reports what the player asked the counter for, the session
/// reads it into a command, and the mechanism decides whether it is legal and what it costs. A name here
/// that the session never reads is a control that does nothing, which is why every one of them is exercised
/// by the suite.
/// </remarks>
public static class ServiceActions
{
    /// <summary>Takes a line of the shelves, carrying the lot and how many.</summary>
    public const string Buy = "service.buy";

    /// <summary>Gives the counter one of the party's items, carrying the instance.</summary>
    public const string Sell = "service.sell";

    /// <summary>Pays to learn what an item is, carrying the instance.</summary>
    public const string Identify = "service.identify";

    /// <summary>Pays to have an item repaired, carrying the instance.</summary>
    public const string Repair = "service.repair";

    /// <summary>Pays for a lesson, carrying its subject and the member it goes to.</summary>
    public const string Teach = "service.teach";

    /// <summary>Pays a counter to train one member a level, carrying the member it goes to.</summary>
    /// <remarks>
    /// A training step names a member rather than a target: what is bought is the member's next level at the
    /// counter the party stands at, and the hall's own ceiling and fee are the ruleset's answers about this
    /// member. Without this action the one mechanism could train nobody from the screen, which is what left
    /// the shipped halls unusable before it.
    /// </remarks>
    public const string Train = "service.train";

    /// <summary>Leaves the counter, ending the visit.</summary>
    public const string Leave = "service.leave";
}

/// <summary>
/// Reads admitted input into the service commands a screen asked for.
/// </summary>
/// <remarks>
/// <para>
/// This is the service screen's counterpart to the creation reader: the session consults it inside its one
/// admitted update while a visit is open, and not at all while the party is walking. Every command is
/// discrete — a purchase is made once — so nothing is remembered between updates, which is the opposite of
/// a held movement key and the same shape a creation choice has.
/// </para>
/// <para>
/// A digital event on the declared leave control carries its command; a payload on the declared contract
/// carries the action it names. Anything else, including a malformed payload, carries no command: an input
/// channel must not throw on hostile bytes, and a caller that receives nothing simply has nothing to apply.
/// </para>
/// </remarks>
public sealed class ServiceInput
{
    private readonly byte[] _leave;
    private readonly byte[] _actionContract;

    /// <summary>Creates the reader for one product's declared service controls.</summary>
    /// <param name="names">The intent and contract names the commands arrive on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public ServiceInput(ServiceIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _leave = Encoding.UTF8.GetBytes(names.Leave);
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into the service commands it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The commands the screen asked for, in the order they arrived.</returns>
    public IReadOnlyList<ServiceCommand> Read(ReadOnlySpan<ProductInputEvent> input)
    {
        List<ServiceCommand> commands = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (inputEvent.Intent.Span.SequenceEqual(_leave) && IsActivation(inputEvent))
                {
                    commands.Add(ServiceCommand.Of(ServiceCommandKind.Leave));
                }

                continue;
            }

            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            if (Command(inputEvent.PayloadData.Span) is { } command) commands.Add(command);
        }

        return commands;
    }

    /// <summary>
    /// Whether a digital event is an activation. A physical press carries an edge; a direct interface
    /// claim is admitted with no edge at all, so its own phase and provenance are what identify it.
    /// </summary>
    private static bool IsActivation(in ProductInputEvent inputEvent) =>
        inputEvent.Edge == InputEdge.Pressed
        || inputEvent.Phase == InputPhase.DirectUi
        || inputEvent.Provenance == InputProvenance.DirectUi;

    /// <summary>Reads one payload action into the command it names, or null when it names none of ours.</summary>
    private static ServiceCommand? Command(ReadOnlySpan<byte> utf8)
    {
        ServiceActionDto? action = Parse(utf8);
        if (action?.Action is not { Length: > 0 } name) return null;
        string target = Target(action.Target);
        // A command that names nothing is still a command: the mechanism refuses it by name, where dropping
        // it would show nothing, and a count below one is refused the same way rather than rounded up.
        return name switch
        {
            ServiceActions.Buy => new ServiceCommand(ServiceCommandKind.Buy, target, Count: action.Count ?? 1),
            ServiceActions.Sell => new ServiceCommand(ServiceCommandKind.Sell, target),
            ServiceActions.Identify => new ServiceCommand(ServiceCommandKind.Identify, target),
            ServiceActions.Repair => new ServiceCommand(ServiceCommandKind.Repair, target),
            ServiceActions.Teach => new ServiceCommand(ServiceCommandKind.Teach, target, action.Member ?? 0),
            ServiceActions.Train => new ServiceCommand(ServiceCommandKind.Train, Member: action.Member ?? 0),
            ServiceActions.Leave => ServiceCommand.Of(ServiceCommandKind.Leave),
            _ => null,
        };
    }

    /// <summary>
    /// Reads what a command names, whether the screen wrote it as a string or as a number.
    /// </summary>
    /// <remarks>
    /// An identity is an identity: a lot named "stock:sword" and an instance named 7 are both targets, and
    /// a screen that wrote one of them as a JSON number should not have its command dropped for it.
    /// </remarks>
    private static string Target(JsonElement? target) => target?.ValueKind switch
    {
        JsonValueKind.String => target.Value.GetString() ?? string.Empty,
        JsonValueKind.Number => target.Value.GetRawText(),
        _ => string.Empty,
    };

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input
    /// channel must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static ServiceActionDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, ServiceActionJsonContext.Default.ServiceActionDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The service action payload's wire record: an action name plus whatever it names.</summary>
internal sealed record ServiceActionDto(
    string? Action,
    JsonElement? Target,
    int? Member,
    int? Count);

/// <summary>Source-generated JSON for the service action payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServiceActionDto))]
internal sealed partial class ServiceActionJsonContext : JsonSerializerContext;
