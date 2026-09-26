using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The conversation controls a product declares, by the names a player's commands arrive on.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, use, service, and stop
/// controls are: the kit claims what a product declares and invents no key of its own. Two names are needed
/// because a product offers two ways to act in a conversation — a digital intent for a key that ends it, and
/// the payload contract the screen sends its choices on — and both are read inside the one admitted update,
/// so a key and a button ask for exactly the same thing.
/// </para>
/// <para>
/// Entering a conversation is deliberately not one of these controls. A party speaks with somebody by using
/// the person the interaction mechanism reached, on the use control the product already declares, so there
/// is one way to walk up to somebody rather than two.
/// </para>
/// </remarks>
public sealed record ConversationIntentNames
{
    /// <summary>Creates the declared conversation control names.</summary>
    /// <param name="leave">The intent a request to end the conversation arrives on.</param>
    /// <param name="actionContract">The payload contract the screen's conversation choices arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public ConversationIntentNames(string leave, string actionContract)
    {
        Leave = Require(leave, nameof(leave));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The intent that ends the conversation the party is in.</summary>
    public string Leave { get; }

    /// <summary>The payload contract the screen's conversation choices arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The conversation control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>What a screen asked a conversation for.</summary>
/// <remarks>
/// These are wire names, not rules: the screen reports what the player chose, the session reads it into a
/// command, and the mechanism decides whether the topic is on offer and what the person says. A name here
/// that the session never reads is a control that does nothing, which is why every one of them is exercised
/// by the suite.
/// </remarks>
public static class ConversationActions
{
    /// <summary>Takes a topic, carrying the topic's identity.</summary>
    public const string Topic = "conversation.topic";

    /// <summary>Turns to another of the people present, carrying the person's identity.</summary>
    public const string Person = "conversation.person";

    /// <summary>Ends the conversation.</summary>
    public const string Leave = "conversation.leave";
}

/// <summary>What one conversation command asks for.</summary>
/// <param name="Kind">Whether the command takes a topic, turns to a person, or leaves.</param>
/// <param name="Target">The topic's or the person's identity, or empty for a command that names neither.</param>
public readonly record struct ConversationCommand(ConversationCommandKind Kind, string Target = "")
{
    /// <summary>Leaves the conversation, which is the one command that names nothing.</summary>
    public static ConversationCommand Leave { get; } = new(ConversationCommandKind.Leave);
}

/// <summary>What kind of thing a conversation command asks the mechanism for.</summary>
public enum ConversationCommandKind
{
    /// <summary>Take the topic the command names.</summary>
    Topic,

    /// <summary>Turn to the person the command names.</summary>
    Person,

    /// <summary>End the conversation.</summary>
    Leave,
}

/// <summary>
/// Reads admitted input into the conversation commands a screen asked for.
/// </summary>
/// <remarks>
/// <para>
/// This is the conversation screen's counterpart to the service and creation readers: the session consults
/// it inside its one admitted update while a conversation is open, and not at all while the party is
/// walking. Every command is discrete — a topic is taken once — so nothing is remembered between updates,
/// which is the opposite of a held movement key.
/// </para>
/// <para>
/// A digital event on the declared leave control carries its command; a payload on the declared contract
/// carries the action it names and whatever it names. Anything else, including a malformed payload, carries
/// no command: an input channel must not throw on hostile bytes, and a caller that receives nothing simply
/// has nothing to apply.
/// </para>
/// </remarks>
public sealed class ConversationInput
{
    private readonly byte[] _leave;
    private readonly byte[] _actionContract;

    /// <summary>Creates the reader for one product's declared conversation controls.</summary>
    /// <param name="names">The intent and contract names the commands arrive on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public ConversationInput(ConversationIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _leave = Encoding.UTF8.GetBytes(names.Leave);
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into the conversation commands it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The commands the screen asked for, in the order they arrived.</returns>
    public IReadOnlyList<ConversationCommand> Read(ReadOnlySpan<ProductInputEvent> input)
    {
        List<ConversationCommand> commands = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (inputEvent.Intent.Span.SequenceEqual(_leave) && IsActivation(inputEvent))
                {
                    commands.Add(ConversationCommand.Leave);
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
    private static ConversationCommand? Command(ReadOnlySpan<byte> utf8)
    {
        ConversationActionDto? action = Parse(utf8);
        if (action?.Action is not { Length: > 0 } name) return null;
        string target = Target(action.Target);
        // A command that names nothing is still a command: the mechanism refuses it by name, where dropping
        // it would show nothing at all.
        return name switch
        {
            ConversationActions.Topic => new ConversationCommand(ConversationCommandKind.Topic, target),
            ConversationActions.Person => new ConversationCommand(ConversationCommandKind.Person, target),
            ConversationActions.Leave => ConversationCommand.Leave,
            _ => null,
        };
    }

    /// <summary>
    /// Reads what a command names, whether the screen wrote it as a string or as a number.
    /// </summary>
    /// <remarks>
    /// An identity is an identity: a topic named "topic-12" and a person named 7 are both identities, and a
    /// screen that wrote one of them as a JSON number should not have its command dropped for it.
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
    private static ConversationActionDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, ConversationActionJsonContext.Default.ConversationActionDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The conversation action payload's wire record: an action name plus whatever it names.</summary>
internal sealed record ConversationActionDto(string? Action, JsonElement? Target);

/// <summary>Source-generated JSON for the conversation action payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConversationActionDto))]
internal sealed partial class ConversationActionJsonContext : JsonSerializerContext;
