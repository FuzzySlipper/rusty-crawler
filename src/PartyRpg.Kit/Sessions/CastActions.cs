using PartyRpg.Kit.Input;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The control a product declares for casting: the contract its two payload actions arrive on.</summary>
/// <remarks>
/// <para>
/// The actions' names are the kit's (<see cref="CastActions"/>), as every semantic action's is; a product
/// declares the contract they arrive on.
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
    /// <summary>Creates the declared casting control.</summary>
    /// <param name="actionContract">The payload contract a screen's casting commands arrive on.</param>
    /// <exception cref="ArgumentException">The contract is missing, so no event could ever be claimed on it.</exception>
    public CastIntentNames(string actionContract)
    {
        ActionContract = !string.IsNullOrWhiteSpace(actionContract)
            ? actionContract
            : throw new ArgumentException(
                "The casting control declares no contract, so no event could ever be claimed on it.",
                nameof(actionContract));
    }

    /// <summary>The payload contract a screen's casting commands arrive on.</summary>
    public string ActionContract { get; }
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

/// <summary>The castings and quick-slot choices a player made, read from the admitted input of each update.</summary>
public sealed class CastInput
{
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared casting control.</summary>
    /// <param name="names">The casting control the host declared.</param>
    public CastInput(CastIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _actionContract = names.ActionContract;
    }

    /// <summary>The castings this update carried, in the order they arrived; one that names no spell is dropped.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<CastRequest> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<CastRequest> casts = [];
        foreach (UiAction action in inbox.Take(_actionContract, CastActions.Cast))
        {
            string spell = action.Text("spell");
            if (spell.Length == 0) continue;
            ItemInstanceId? item = action.Identity("item") is { } id ? new ItemInstanceId(id) : null;
            casts.Add(new CastRequest(action.Int("member") ?? 0, new SpellId(spell), action.Text("target"), item));
        }

        return casts;
    }

    /// <summary>The quick-slot choices this update carried; one that names no spell clears the slot.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<QuickSpellRequest> ReadQuick(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<QuickSpellRequest> choices = [];
        foreach (UiAction action in inbox.Take(_actionContract, CastActions.QuickSpell))
        {
            string spell = action.Text("spell");
            choices.Add(new QuickSpellRequest(action.Int("member") ?? 0, spell.Length == 0 ? null : new SpellId(spell)));
        }

        return choices;
    }
}
