using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

/// <summary>
/// Reads admitted input into the creation commands a screen asked for.
/// </summary>
/// <remarks>
/// <para>
/// This is the creation screen's counterpart to the movement reader: the session consults it inside its one
/// admitted update while it is creating a party, and not at all while it is playing. Every command is
/// discrete — a choice is made once — so nothing is remembered between updates, unlike a held key.
/// </para>
/// <para>
/// A digital event on a declared control carries its command; a payload on the declared contract carries
/// the action it names. Anything else, including a malformed payload, carries no command: an input channel
/// must not throw on hostile bytes, and a caller that receives nothing simply has nothing to apply.
/// </para>
/// </remarks>
public sealed class CreationInput
{
    private readonly byte[] _advance;
    private readonly byte[] _accept;
    private readonly byte[] _actionContract;

    /// <summary>Creates the reader for one product's declared creation controls.</summary>
    /// <param name="names">The intent and contract names the commands arrive on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public CreationInput(CreationIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _advance = Encoding.UTF8.GetBytes(names.Advance);
        _accept = Encoding.UTF8.GetBytes(names.Accept);
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into the creation commands it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The commands the screen asked for, in the order they arrived.</returns>
    public IReadOnlyList<CreationCommand> Read(ReadOnlySpan<ProductInputEvent> input)
    {
        List<CreationCommand> commands = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (!IsActivation(inputEvent)) continue;
                if (inputEvent.Intent.Span.SequenceEqual(_advance)) commands.Add(CreationCommand.Of(CreationCommandKind.Advance));
                else if (inputEvent.Intent.Span.SequenceEqual(_accept)) commands.Add(CreationCommand.Of(CreationCommandKind.Accept));
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
    private static CreationCommand? Command(ReadOnlySpan<byte> utf8)
    {
        CreationActionDto? action = Parse(utf8);
        if (action?.Action is not { Length: > 0 } name) return null;
        return name switch
        {
            // A member command without an index names member -1, which creation refuses by name: the
            // refusal is the answer a screen can show, where dropping the command would show nothing.
            CreationActions.SelectMember => new CreationCommand(CreationCommandKind.SelectMember, action.Member ?? -1),
            CreationActions.SelectPortrait => Choice(CreationCommandKind.SelectPortrait, action.Portrait),
            CreationActions.SelectClass => Choice(CreationCommandKind.SelectClass, action.Class),
            CreationActions.SetName => new CreationCommand(CreationCommandKind.SetName, Value: action.Name ?? string.Empty),
            CreationActions.RaiseAttribute => Choice(CreationCommandKind.RaiseAttribute, action.Attribute),
            CreationActions.LowerAttribute => Choice(CreationCommandKind.LowerAttribute, action.Attribute),
            CreationActions.ChooseSkill => Choice(CreationCommandKind.ChooseSkill, action.Skill),
            CreationActions.RemoveSkill => Choice(CreationCommandKind.RemoveSkill, action.Skill),
            CreationActions.Advance => CreationCommand.Of(CreationCommandKind.Advance),
            CreationActions.Accept => CreationCommand.Of(CreationCommandKind.Accept),
            _ => null,
        };
    }

    /// <summary>States a choice command, keeping a missing choice blank so the session can refuse it by name.</summary>
    private static CreationCommand Choice(CreationCommandKind kind, string? value) =>
        new(kind, Value: value ?? string.Empty);

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input
    /// channel must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static CreationActionDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, CreationActionJsonContext.Default.CreationActionDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The creation action payload's wire record: an action name plus whatever choice it carries.</summary>
internal sealed record CreationActionDto(
    string? Action,
    int? Member,
    string? Portrait,
    string? Class,
    string? Name,
    string? Attribute,
    string? Skill);

/// <summary>Source-generated JSON for the creation action payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreationActionDto))]
internal sealed partial class CreationActionJsonContext : JsonSerializerContext;
