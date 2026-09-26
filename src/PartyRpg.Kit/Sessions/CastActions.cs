using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The controls a product declares for casting: one payload action for a spell, one for the quick slot.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, use, service, rest, and
/// skill controls are: the kit claims what a product declares and invents no control of its own.
/// </para>
/// <para>
/// <b>Casting has no key of its own, and that is deliberate.</b> A casting names a spell and a target, and
/// no single key can say which of a character's spells and which of the creatures in front of the party the
/// player meant; the act control already answers a fight with the members' own quick answers, and this is
/// how a chosen spell is cast on a chosen target. The quick-spell control is the same shape: it names the
/// member and the spell the slot should hold, so the panel's own rows drive it.
/// </para>
/// </remarks>
public sealed record CastIntentNames
{
    /// <summary>Creates the declared casting control names.</summary>
    /// <param name="cast">The payload action that casts one named spell at one named target.</param>
    /// <param name="quickSpell">The payload action that sets or clears one member's quick spell.</param>
    /// <param name="actionContract">The payload contract a screen's casting commands arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public CastIntentNames(string cast, string quickSpell, string actionContract)
    {
        Cast = Require(cast, nameof(cast));
        QuickSpell = Require(quickSpell, nameof(quickSpell));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The payload action that casts one member's named spell at a named target.</summary>
    public string Cast { get; }

    /// <summary>The payload action that puts a spell in a member's quick slot, or clears it.</summary>
    public string QuickSpell { get; }

    /// <summary>The payload contract a screen's casting commands arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The casting control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The payload actions a spellbook screen sends, on the payload contract the product declares.</summary>
public static class CastActions
{
    /// <summary>Casts one member's named spell at the target the screen drew.</summary>
    public const string Cast = "party.cast";

    /// <summary>Puts one named spell in one member's quick slot, or clears it when the spell is empty.</summary>
    public const string QuickSpell = "party.quick-spell";
}

/// <summary>One casting a screen asked for: which member, which spell, what it is aimed at, and what carries it.</summary>
/// <param name="Member">The caster's place in the party, counted from zero.</param>
/// <param name="Spell">The spell the screen drew.</param>
/// <param name="Target">The identity the projection published for the spell's target, empty when none.</param>
/// <param name="Item">
/// The item the spell is cast from, or null when it comes from the caster's own spellbook. A screen that drew
/// a row for a scroll or a wand it holds names the instance that row was published for, and the workflow takes
/// that item as the spell's source — which is why the payload carries it rather than the session guessing
/// which of a character's spells came from where.
/// </param>
public readonly record struct CastRequest(int Member, SpellId Spell, string Target, ItemInstanceId? Item = null);

/// <summary>One quick-slot choice a screen made: which member, and which spell the slot should hold.</summary>
/// <param name="Member">The member's place in the party, counted from zero.</param>
/// <param name="Spell">The spell to keep in the slot, empty to clear it.</param>
public readonly record struct QuickSpellRequest(int Member, SpellId? Spell);

/// <summary>
/// Reads admitted input into the castings and quick-slot choices a screen asked for.
/// </summary>
/// <remarks>
/// The counterpart to the creation, service, and skill readers: the session consults it inside its one
/// admitted update. A payload that names no spell produces no request, because there is nothing to cast and
/// no refusal worth wording about it; a payload that names a spell this game does not declare is carried
/// through and refused by name there. A malformed payload produces nothing at all, because an input channel
/// must not throw on hostile bytes.
/// </remarks>
public sealed class CastInput
{
    private readonly byte[] _actionContract;
    private readonly string _cast;
    private readonly string _quickSpell;

    /// <summary>Creates the reader for one product's declared casting controls.</summary>
    /// <param name="names">The actions and the contract a casting arrives on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public CastInput(CastIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _cast = names.Cast;
        _quickSpell = names.QuickSpell;
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into the castings it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The castings the screen asked for, in the order they arrived.</returns>
    public IReadOnlyList<CastRequest> Read(ReadOnlySpan<ProductInputEvent> input)
    {
        List<CastRequest> casts = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            if (Cast(inputEvent.PayloadData.Span) is { } cast) casts.Add(cast);
        }

        return casts;
    }

    /// <summary>Reads one update's admitted input into the quick-slot choices it carries, in order.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>The choices the screen made, in the order they arrived.</returns>
    public IReadOnlyList<QuickSpellRequest> ReadQuick(ReadOnlySpan<ProductInputEvent> input)
    {
        List<QuickSpellRequest> choices = [];
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            if (Quick(inputEvent.PayloadData.Span) is { } choice) choices.Add(choice);
        }

        return choices;
    }

    /// <summary>Reads one payload into the casting it names, or null when it names none of ours.</summary>
    private CastRequest? Cast(ReadOnlySpan<byte> utf8)
    {
        CastDto? action = Parse(utf8);
        if (action is null || !string.Equals(action.Action, _cast, StringComparison.Ordinal)) return null;
        string spell = Spell(action.Spell);
        if (spell.Length == 0) return null;
        return new CastRequest(action.Member ?? 0, new SpellId(spell), action.Target ?? string.Empty, Item(action.Item));
    }

    /// <summary>
    /// The item a payload names as a casting's source, or null when it names none or an unreadable one.
    /// </summary>
    /// <remarks>
    /// An identity that is not a whole number above zero names no instance, and a casting with no source is a
    /// casting from the spellbook rather than a defect: an input channel must not throw on hostile bytes, and
    /// the workflow refuses what it cannot resolve by name where a person can see it.
    /// </remarks>
    private static ItemInstanceId? Item(JsonElement? element)
    {
        string written = Spell(element);
        return ulong.TryParse(written, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) && value > 0
            ? new ItemInstanceId(value)
            : null;
    }

    /// <summary>
    /// Reads one payload into the quick-slot choice it names, or null when it names none of ours.
    /// </summary>
    /// <remarks>
    /// A choice that names no spell is a cleared slot rather than nothing at all: a player who empties the
    /// slot asked for exactly that, and the reader must not turn the request into silence.
    /// </remarks>
    private QuickSpellRequest? Quick(ReadOnlySpan<byte> utf8)
    {
        CastDto? action = Parse(utf8);
        if (action is null || !string.Equals(action.Action, _quickSpell, StringComparison.Ordinal)) return null;
        string spell = Spell(action.Spell);
        return new QuickSpellRequest(action.Member ?? 0, spell.Length == 0 ? null : new SpellId(spell));
    }

    /// <summary>A spell identity read from a payload, which may be a string or a number.</summary>
    private static string Spell(JsonElement? element) => element?.ValueKind switch
    {
        JsonValueKind.String => element.Value.GetString() ?? string.Empty,
        JsonValueKind.Number => element.Value.GetRawText(),
        _ => string.Empty,
    };

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input
    /// channel must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static CastDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, CastJsonContext.Default.CastDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The casting payload's wire record: an action name plus the member, spell, target, and item.</summary>
internal sealed record CastDto(string? Action, int? Member, JsonElement? Spell, string? Target, JsonElement? Item);

/// <summary>Source-generated JSON for the casting payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CastDto))]
internal sealed partial class CastJsonContext : JsonSerializerContext;
