using System.Text;
using PartyRpg.Kit.Input;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The creation controls a product declares, by the intent each one arrives on.
/// </summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement controls are: the kit claims what a
/// product declares and invents no key of its own, so the same reader serves a product that names its
/// controls differently or maps them to other keys.
/// </para>
/// <para>
/// Two controls are digital because a keyboard can express them without a value — confirming the step
/// being worked on and accepting the finished party. Everything else names a choice, and a choice travels
/// as a payload action on the declared contract: that is what carries a portrait, a class, a skill, an
/// attribute, a member, or a name.
/// </para>
/// </remarks>
public sealed record CreationIntentNames
{
    /// <summary>Creates the declared creation control names.</summary>
    /// <param name="advance">The intent a confirmation of the step being worked on arrives on.</param>
    /// <param name="accept">The intent an acceptance of the finished party arrives on.</param>
    /// <param name="actionContract">The payload contract the screen's creation actions arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public CreationIntentNames(string advance, string accept, string actionContract)
    {
        Advance = Require(advance, nameof(advance));
        Accept = Require(accept, nameof(accept));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The intent that confirms the step the member being created is on.</summary>
    public string Advance { get; }

    /// <summary>The intent that accepts the finished party and leaves creation for the world.</summary>
    public string Accept { get; }

    /// <summary>The payload contract the screen's creation actions arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The creation control '{parameterName}' declares no intent name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// The actions a creation screen sends, on the payload contract the product declares.
/// </summary>
/// <remarks>
/// These are wire names, not rules: the screen reports what the player chose, the session applies it
/// through the flow, and the flow decides whether the choice was legal. A name here that the session never
/// reads is a control that does nothing, which is why every one of them is exercised by the suite.
/// </remarks>
public static class CreationActions
{
    /// <summary>Moves creation onto a member, reopening a confirmed one so its choices can be changed.</summary>
    public const string SelectMember = "creation.select-member";

    /// <summary>Chooses the portrait, which is what decides the character's race.</summary>
    public const string SelectPortrait = "creation.select-portrait";

    /// <summary>Chooses the class, which is what decides the skills on offer.</summary>
    public const string SelectClass = "creation.select-class";

    /// <summary>Names the character.</summary>
    public const string SetName = "creation.set-name";

    /// <summary>Spends attribute points on one attribute, raising it by one adjustment.</summary>
    public const string RaiseAttribute = "creation.raise-attribute";

    /// <summary>Lowers one attribute by one adjustment, returning what it cost to the pool.</summary>
    public const string LowerAttribute = "creation.lower-attribute";

    /// <summary>Chooses one of the skills the class does not fix.</summary>
    public const string ChooseSkill = "creation.choose-skill";

    /// <summary>Takes back one of the chosen skills.</summary>
    public const string RemoveSkill = "creation.remove-skill";

    /// <summary>Confirms the step being worked on and moves creation to the next one.</summary>
    public const string Advance = "creation.advance";

    /// <summary>Accepts the finished party and leaves creation for the world.</summary>
    public const string Accept = "creation.accept";

    /// <summary>Clears the member being created back to its first step, every choice undone.</summary>
    public const string ResetMember = "creation.reset-member";

    /// <summary>Starts the whole party again from the ruleset's default party.</summary>
    public const string ApplyDefault = "creation.apply-default";
}

/// <summary>Which creation command a screen asked for.</summary>
public enum CreationCommandKind
{
    /// <summary>Move creation onto a member.</summary>
    SelectMember,

    /// <summary>Choose the portrait, which decides the race.</summary>
    SelectPortrait,

    /// <summary>Choose the class, which decides the skills on offer.</summary>
    SelectClass,

    /// <summary>Name the character.</summary>
    SetName,

    /// <summary>Raise one attribute by one adjustment.</summary>
    RaiseAttribute,

    /// <summary>Lower one attribute by one adjustment.</summary>
    LowerAttribute,

    /// <summary>Choose one of the skills the class does not fix.</summary>
    ChooseSkill,

    /// <summary>Take back one of the chosen skills.</summary>
    RemoveSkill,

    /// <summary>Confirm the step being worked on.</summary>
    Advance,

    /// <summary>Accept the finished party.</summary>
    Accept,

    /// <summary>Clear the member being created back to its first step.</summary>
    ResetMember,

    /// <summary>Start the whole party again from the ruleset's default party.</summary>
    ApplyDefault,
}

/// <summary>One creation command, with the choice it carries.</summary>
/// <remarks>
/// A command is a value rather than a call, so what a screen asked for can be read, applied, and reported
/// in that order inside one admitted update. A command that arrives without the choice it names is still a
/// command: the session refuses it by name, because a control that silently does nothing is exactly the
/// failure this kit's named refusals exist to prevent.
/// </remarks>
/// <param name="Kind">Which command this is.</param>
/// <param name="Member">Which member a member command names, counted from zero; unused by the others.</param>
/// <param name="Value">
/// What a choice command names: a portrait, class, skill, or attribute id, or the name to give. Blank when
/// the payload carried no choice, which the session refuses rather than treating as a choice of nothing.
/// </param>
public sealed record CreationCommand(CreationCommandKind Kind, int Member = 0, string Value = "")
{
    /// <summary>A command that carries no choice.</summary>
    /// <param name="kind">Which command to state.</param>
    /// <returns>The command.</returns>
    public static CreationCommand Of(CreationCommandKind kind) => new(kind);
}

/// <summary>The creation commands a player gave, read from the admitted input of each update, in order.</summary>
public sealed class CreationInput
{
    private readonly byte[] _advance;
    private readonly byte[] _accept;
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared creation controls.</summary>
    /// <param name="names">The creation controls the host declared.</param>
    public CreationInput(CreationIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _advance = Encoding.UTF8.GetBytes(names.Advance);
        _accept = Encoding.UTF8.GetBytes(names.Accept);
        _actionContract = names.ActionContract;
    }

    /// <summary>The creation commands this update carried.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<CreationCommand> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<CreationCommand> commands = [];
        foreach (ProductInputEvent inputEvent in inbox.Digital)
        {
            if (!InputEvents.IsActivation(inputEvent)) continue;
            if (inputEvent.Intent.Span.SequenceEqual(_advance)) commands.Add(CreationCommand.Of(CreationCommandKind.Advance));
            else if (inputEvent.Intent.Span.SequenceEqual(_accept)) commands.Add(CreationCommand.Of(CreationCommandKind.Accept));
        }

        foreach (UiAction action in inbox.Take(_actionContract, name => Command(name) is not null))
        {
            commands.Add(Command(action.Name)!(action));
        }

        return commands;
    }

    /// <summary>How a named creation action becomes a command, or null for a name creation does not take.</summary>
    private static Func<UiAction, CreationCommand>? Command(string name) => name switch
    {
        CreationActions.SelectMember => action => new CreationCommand(CreationCommandKind.SelectMember, action.Int("member") ?? -1),
        CreationActions.SelectPortrait => action => Choice(CreationCommandKind.SelectPortrait, action.Text("portrait")),
        CreationActions.SelectClass => action => Choice(CreationCommandKind.SelectClass, action.Text("class")),
        CreationActions.SetName => action => new CreationCommand(CreationCommandKind.SetName, Value: action.Text("name")),
        CreationActions.RaiseAttribute => action => Choice(CreationCommandKind.RaiseAttribute, action.Text("attribute")),
        CreationActions.LowerAttribute => action => Choice(CreationCommandKind.LowerAttribute, action.Text("attribute")),
        CreationActions.ChooseSkill => action => Choice(CreationCommandKind.ChooseSkill, action.Text("skill")),
        CreationActions.RemoveSkill => action => Choice(CreationCommandKind.RemoveSkill, action.Text("skill")),
        CreationActions.Advance => action => CreationCommand.Of(CreationCommandKind.Advance),
        CreationActions.Accept => action => CreationCommand.Of(CreationCommandKind.Accept),
        CreationActions.ResetMember => action => CreationCommand.Of(CreationCommandKind.ResetMember),
        CreationActions.ApplyDefault => action => CreationCommand.Of(CreationCommandKind.ApplyDefault),
        _ => null,
    };

    private static CreationCommand Choice(CreationCommandKind kind, string value) => new(kind, Value: value);
}
