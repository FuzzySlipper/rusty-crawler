using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PartyRpg.Kit.Party;
using Rusty.Engine;

namespace PartyRpg.Kit.Progression;

/// <summary>The names a product declares for spending skill points, as its screen asks for a raise.</summary>
/// <remarks>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, use, service, and rest
/// controls are: the kit claims what a product declares and invents no control of its own. A raise is the
/// one panel action the progression owner reads, because a raise is the one thing the owner does that a
/// player asks for directly — experience is awarded by the world and a level is bought at a counter.
/// </remarks>
/// <param name="action">The payload action a screen's raise control sends.</param>
/// <param name="actionContract">The payload contract the screen's actions arrive on.</param>
/// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
public sealed record SkillRaiseIntentNames
{
    /// <summary>Creates the declared skill-spend control names.</summary>
    /// <param name="action">The payload action a screen's raise control sends.</param>
    /// <param name="actionContract">The payload contract the screen's actions arrive on.</param>
    public SkillRaiseIntentNames(string action, string actionContract)
    {
        Action = Require(action, nameof(action));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The payload action that asks to raise a member's skill.</summary>
    public string Action { get; }

    /// <summary>The payload contract the screen's actions arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The skill control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The actions a skills screen sends, on the payload contract the product declares.</summary>
/// <remarks>
/// A wire name, not a rule: the screen reports which member's skill the player asked to raise, and the
/// owner judges the ceiling, the price, and the pool before anything moves.
/// </remarks>
public static class SkillRaiseActions
{
    /// <summary>Raises one member's skill by the levels the action states.</summary>
    /// <remarks>
    /// The action carries the member, the skill, and how many levels, because a raise is about one
    /// character's one skill: a bare "raise something" would leave the owner guessing which of a party's
    /// entries the player meant.
    /// </remarks>
    public const string Raise = "party.raise-skill";
}

/// <summary>One raise a screen asked for: which member, which skill, and how many levels.</summary>
/// <remarks>
/// The member is the party's own index rather than a durable identity, exactly as a service lesson's member
/// is: the screen was shown the party in its order and names the row it drew, and the session resolves that
/// row against the party it holds inside the same update. A row the party no longer has is refused by name
/// rather than applied to whoever now stands there.
/// </remarks>
/// <param name="Member">The member's place in the party, counted from zero.</param>
/// <param name="Skill">The skill the screen named.</param>
/// <param name="Levels">How many levels to add, at least one.</param>
public readonly record struct SkillRaiseRequest(int Member, SkillId Skill, int Levels);

/// <summary>Reads admitted input into the raises a screen asked for.</summary>
/// <remarks>
/// <para>
/// The counterpart to the creation and service readers: the session consults it inside its one admitted
/// update, and a raise is discrete — points are spent once per press — so nothing is held between updates.
/// </para>
/// A payload that names no skill produces no request, because there is nothing to raise and no refusal the
/// owner could word about it; a payload that names a skill this game does not declare is carried through
/// and refused by name there. A malformed payload produces nothing at all, because an input channel must
/// not throw on hostile bytes.
/// </para>
/// </remarks>
public sealed class SkillRaiseInput
{
    private readonly byte[] _actionContract;
    private readonly string _action;

    /// <summary>Creates the reader for one product's declared skill-spend controls.</summary>
    /// <param name="names">The action and contract names a raise arrives on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public SkillRaiseInput(SkillRaiseIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _action = names.Action;
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into the raises it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The raises the screen asked for, in the order they arrived.</returns>
    public IReadOnlyList<SkillRaiseRequest> Read(ReadOnlySpan<ProductInputEvent> input)
    {
        List<SkillRaiseRequest> raises = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            if (Raise(inputEvent.PayloadData.Span) is { } raise) raises.Add(raise);
        }

        return raises;
    }

    /// <summary>Reads one payload into the raise it names, or null when it names none of ours.</summary>
    private SkillRaiseRequest? Raise(ReadOnlySpan<byte> utf8)
    {
        SkillRaiseDto? action = Parse(utf8);
        if (action is null || !string.Equals(action.Action, _action, StringComparison.Ordinal)) return null;
        string skill = action.Skill?.ValueKind switch
        {
            JsonValueKind.String => action.Skill.Value.GetString() ?? string.Empty,
            JsonValueKind.Number => action.Skill.Value.GetRawText(),
            _ => string.Empty,
        };

        if (skill.Length == 0) return null;

        // A raise of no levels is read as one level: the action is a request to raise the skill, and the
        // owner refuses what it will not do rather than the reader silently turning a request into nothing.
        return new SkillRaiseRequest(
            action.Member ?? 0,
            new SkillId(skill),
            action.Levels is { } levels && levels > 0 ? levels : 1);
    }

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input
    /// channel must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static SkillRaiseDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, SkillRaiseJsonContext.Default.SkillRaiseDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The skill raise payload's wire record: an action name plus the member, skill, and levels.</summary>
internal sealed record SkillRaiseDto(
    string? Action,
    int? Member,
    JsonElement? Skill,
    int? Levels);

/// <summary>Source-generated JSON for the skill raise payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SkillRaiseDto))]
internal sealed partial class SkillRaiseJsonContext : JsonSerializerContext;
