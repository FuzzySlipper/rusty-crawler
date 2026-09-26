using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PartyRpg.Kit.Party;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The control a product declares for mixing: one payload action, and the contract it arrives on.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, use, service, rest,
/// casting, and skill controls are: the kit claims what a product declares and invents no control of its own.
/// </para>
/// <para>
/// <b>Mixing has no key of its own, and that is deliberate.</b> A mixture names two things out of the party's
/// own pack, and no single press can say which two of the things it carries the player meant — the donor's own
/// mixing is two clicks on two items rather than a key. It is a payload action for the same reason casting is:
/// the screen's own rows name what was chosen.
/// </para>
/// </remarks>
public sealed record MixIntentNames
{
    /// <summary>Creates the declared mixing control names.</summary>
    /// <param name="mix">The payload action that mixes two named items.</param>
    /// <param name="actionContract">The payload contract a screen's mixing commands arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public MixIntentNames(string mix, string actionContract)
    {
        Mix = Require(mix, nameof(mix));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The payload action that mixes the two items a screen named.</summary>
    public string Mix { get; }

    /// <summary>The payload contract a screen's mixing commands arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The mixing control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The payload action a pack screen sends when a player mixes two things.</summary>
public static class AlchemyActions
{
    /// <summary>Mixes the two items the screen drew, through the session's one mixing workflow.</summary>
    public const string Mix = "party.mix";
}

/// <summary>One mixture a screen asked for: which member mixes, and which two things they mix.</summary>
/// <remarks>
/// The member is the party's own index rather than a durable identity, exactly as a casting's member is: the
/// screen was shown the party in its order and names the row it drew. The two items are named by the identity
/// the projection published for the pack's own rows, because two potions of one kind are two things at two
/// strengths and the workflow refuses an identity the pack does not hold.
/// </remarks>
/// <param name="Member">The mixing character's place in the party, counted from zero.</param>
/// <param name="First">One ingredient, as the pack holds it.</param>
/// <param name="Second">The other ingredient, as the pack holds it.</param>
public readonly record struct MixRequest(int Member, ItemInstanceId First, ItemInstanceId Second);

/// <summary>Reads admitted input into the mixtures a screen asked for.</summary>
/// <remarks>
/// The counterpart to the casting, service, and skill readers: the session consults it inside its one admitted
/// update. A payload that names both items produces a request, and a payload naming an identity that is not a
/// whole number above zero produces nothing at all, because an input channel must not throw on hostile bytes
/// and the workflow refuses what it cannot resolve by name where a person can see it.
/// </remarks>
public sealed class MixInput
{
    private readonly byte[] _actionContract;
    private readonly string _mix;

    /// <summary>Creates the reader for one product's declared mixing control.</summary>
    /// <param name="names">The action and the contract a mixture arrives on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public MixInput(MixIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _mix = names.Mix;
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into the mixtures it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The mixtures the screen asked for, in the order they arrived.</returns>
    public IReadOnlyList<MixRequest> Read(ReadOnlySpan<ProductInputEvent> input)
    {
        List<MixRequest> mixes = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            if (Mix(inputEvent.PayloadData.Span) is { } mix) mixes.Add(mix);
        }

        return mixes;
    }

    /// <summary>Reads one payload into the mixture it names, or null when it names none of ours.</summary>
    private MixRequest? Mix(ReadOnlySpan<byte> utf8)
    {
        MixDto? action = Parse(utf8);
        if (action is null || !string.Equals(action.Action, _mix, StringComparison.Ordinal)) return null;
        if (Item(action.First) is not { } first || Item(action.Second) is not { } second) return null;
        return new MixRequest(action.Member ?? 0, first, second);
    }

    /// <summary>
    /// The item a payload names, or null when it names none or an unreadable one.
    /// </summary>
    /// <remarks>
    /// An identity that is not a whole number above zero names no instance, and a mixture naming one is a
    /// request nothing could carry out rather than a defect: the workflow refuses what it cannot resolve by
    /// name where a person can see it.
    /// </remarks>
    private static ItemInstanceId? Item(JsonElement? element)
    {
        string written = Text(element);
        return ulong.TryParse(written, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) && value > 0
            ? new ItemInstanceId(value)
            : null;
    }

    /// <summary>An identity read from a payload, which may be a string or a number.</summary>
    private static string Text(JsonElement? element) => element?.ValueKind switch
    {
        JsonValueKind.String => element.Value.GetString() ?? string.Empty,
        JsonValueKind.Number => element.Value.GetRawText(),
        _ => string.Empty,
    };

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input
    /// channel must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static MixDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, MixJsonContext.Default.MixDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The mixing payload's wire record: an action name plus the member and the two items.</summary>
internal sealed record MixDto(string? Action, int? Member, JsonElement? First, JsonElement? Second);

/// <summary>Source-generated JSON for the mixing payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MixDto))]
internal sealed partial class MixJsonContext : JsonSerializerContext;
