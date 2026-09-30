using System.Text;
using PartyRpg.Kit.Input;
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

/// <summary>The conversation commands a player gave, read from the admitted input of each update, in order.</summary>
public sealed class ConversationInput
{
    private readonly byte[] _leave;
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared conversation controls.</summary>
    /// <param name="names">The conversation controls the host declared.</param>
    public ConversationInput(ConversationIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _leave = Encoding.UTF8.GetBytes(names.Leave);
        _actionContract = names.ActionContract;
    }

    /// <summary>The conversation commands this update carried.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<ConversationCommand> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<ConversationCommand> commands = [];
        if (inbox.Activated(_leave)) commands.Add(ConversationCommand.Leave);
        foreach (UiAction action in inbox.Take(
            _actionContract,
            name => name is ConversationActions.Topic or ConversationActions.Person or ConversationActions.Leave))
        {
            commands.Add(action.Name switch
            {
                ConversationActions.Topic => new ConversationCommand(ConversationCommandKind.Topic, action.Text("target")),
                ConversationActions.Person => new ConversationCommand(ConversationCommandKind.Person, action.Text("target")),
                _ => ConversationCommand.Leave,
            });
        }

        return commands;
    }
}
