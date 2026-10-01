using System.Globalization;
using PartyRpg.Kit;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answer about what a character may wear or wield: the item must be shaped for the place, the hands
/// must be free for it, a weapon or a piece of armour needs the skill its own table row names, and the five
/// things that need no skill need none.
/// </summary>
/// <remarks>
/// <para>
/// <b>The place comes first.</b> Every slot is one of this game's figure (<see cref="MightAndMagic7Figure"/>), and
/// an item goes only where its kind goes, so a sword is refused in the boots and a helmet in the hand by name
/// rather than worn somewhere the fight never reads.
/// </para>
/// <para>
/// <b>The hands are the donor's.</b> A two-handed weapon needs the off hand empty, and nothing goes in the off hand
/// beside one; a second weapon in the off hand needs a dagger at expert or a sword at master
/// (OpenEnroth <c>src/GUI/UI/UICharacter.cpp:1908-1910</c>). Where the donor's doll hands the displaced shield back
/// to the cursor (<c>UICharacter.cpp:1948-1972</c>), this game refuses and names what to take off first:
/// one change moves one item, and a player who meant it takes the shield off and tries again. That is ours.
/// </para>
/// <para>
/// <b>The requirement is the item table's own column.</b> Every row of the shipped item table names the
/// skill its goods belong to — "sword", "leather", "plate" — and this reads that column rather than a list
/// of item categories kept beside it: a shop's stock, a chest's treasure, and a rule's question all name the
/// same definition, so what a character may use is one reading of one table.
/// </para>
/// <para>
/// <b>The exceptions are the manual's own five.</b> "A character cannot equip a weapon or armor without the
/// matching skill, but belts, boots, capes, helmets and gauntlets require no skill"
/// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §2, printed p.39).
/// A slot answers that question, because the slot is where the item goes: the figure's <c>belt</c>,
/// <c>boots</c>, <c>cloak</c>, <c>helm</c>, and <c>gauntlets</c>.
/// </para>
/// <para>
/// <b>What this cannot judge is refused rather than allowed.</b> An item whose row names a skill this game's
/// skill table does not carry — the shipped table files three clubs under a hidden skill it never declares —
/// cannot be said to be usable by anybody, and it is refused by name. Guessing that an unknown requirement
/// is no requirement would hand a character a weapon the game's own table says is not theirs. Content that
/// declares no skill table at all states no requirement to judge, and only the place and the hands are judged.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7EquipmentUse : IEquipmentUseRule
{
    /// <summary>The definition kind the shipped item table is declared under.</summary>
    internal const string ItemDefinitionKind = "item";

    /// <summary>
    /// The skill group the shipped item table files its wands, rings, and potions under.
    /// </summary>
    /// <remarks>
    /// The donor's reader turns any word its skill map does not name into its hidden misc skill
    /// (OpenEnroth <c>src/Engine/Tables/ItemTable.cpp:146</c>), and the shipped table writes that group as
    /// "Misc": no character trains it, and the donor's own wand path never reads a skill at all. Naming it
    /// here is what lets a wand be wielded while a row filed under a skill this game does not carry — the
    /// shipped table's clubs — is still refused.
    /// </remarks>
    private const string MiscGroup = "misc";

    /// <summary>The rung a dagger needs before it goes in the off hand: expert.</summary>
    private const int OffHandDaggerTier = 2;

    /// <summary>The rung a sword needs before it goes in the off hand: master.</summary>
    private const int OffHandSwordTier = 3;

    /// <summary>The figure's five places the manual says need no skill.</summary>
    private static readonly EquipmentSlot[] NoSkillSlots =
    [
        MightAndMagic7Figure.Belt, MightAndMagic7Figure.Boots, MightAndMagic7Figure.Cloak,
        MightAndMagic7Figure.Helm, MightAndMagic7Figure.Gauntlets,
    ];

    private readonly Dictionary<ItemDefinitionId, string> _required;
    private readonly MightAndMagic7Skills? _skills;

    private MightAndMagic7EquipmentUse(
        Dictionary<ItemDefinitionId, string> required,
        MightAndMagic7Skills? skills,
        MightAndMagic7Figure figure)
    {
        _required = required;
        _skills = skills;
        Figure = figure;
    }

    /// <summary>This game's figure, which the rule judges a place against.</summary>
    internal MightAndMagic7Figure Figure { get; }

    /// <summary>Reads what each item definition needs, or null when there is no content to read.</summary>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <param name="skills">
    /// This game's skill policy, which resolves an item's skill word to a declared skill, or null when content
    /// declares no skill table.
    /// </param>
    /// <returns>This game's equipment rule, or null when there is no content.</returns>
    internal static MightAndMagic7EquipmentUse? Read(ContentCatalog? catalog, MightAndMagic7Skills? skills)
    {
        if (MightAndMagic7Figure.Read(catalog) is not { } figure) return null;

        Dictionary<ItemDefinitionId, string> required = [];
        foreach ((_, _, ContentEntry entry) in catalog!.Entries(ItemDefinitionKind))
        {
            string skill = entry.GetString("skill").Trim();
            if (skill.Length == 0) continue;
            required[new ItemDefinitionId(entry.Id)] = skill;
        }

        return new MightAndMagic7EquipmentUse(required, skills, figure);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The place is judged before the hands and the hands before the skill, so a refusal names the first thing a
    /// player would have to change: a sword in the boots is in the wrong place whoever holds it.
    /// </remarks>
    public Refusal? Judge(PartyMember member, EquipmentSlot slot, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(item);

        if (!MightAndMagic7Figure.Has(slot))
        {
            return new Refusal(
                MightAndMagic7Codes.EquipmentSlotUnknown,
                $"A character of this game has no '{slot}'; the places are {string.Join(", ", Figure.Slots)}.");
        }

        if (Figure.Worn(item.Definition) is not { } worn)
        {
            return new Refusal(
                MightAndMagic7Codes.EquipmentNotWearable,
                $"The item table states nothing worn for {item.Definition}, so it cannot go in '{slot}' or anywhere else.");
        }

        IReadOnlyList<EquipmentSlot> shaped = MightAndMagic7Figure.SlotsFor(worn.Kind);
        if (!shaped.Contains(slot))
        {
            return new Refusal(
                MightAndMagic7Codes.EquipmentWrongSlot,
                $"{item.Definition} goes in {string.Join(" or ", shaped.Select(place => $"'{place}'"))}, not in '{slot}'.");
        }

        if (Hands(member, slot, item, worn) is { } full) return full;
        if (NoSkillSlots.Contains(slot)) return null;

        // An item whose definition names no skill is not a thing a character may be said to be trained for:
        // that is what a potion, a ring, or a scroll is.
        if (!_required.TryGetValue(item.Definition, out string? named)) return null;

        // The shipped table files its wands under the group the donor reads as its own hidden misc skill
        // (OpenEnroth src/Engine/Tables/ItemTable.cpp:146, `valueOr(equipSkillMap, tokens[5], SKILL_MISC)`), and a
        // wand is fired at the donor's own fixed skill value rather than at its bearer's
        // (src/Engine/Spells/CastSpellInfo.h:61, WANDS_SKILL_VALUE) — so a wand needs no skill this game can
        // train, which is what this group means here.
        if (string.Equals(named, MiscGroup, StringComparison.OrdinalIgnoreCase)) return null;
        if (_skills is null) return null;

        if (_skills.Resolve(named) is not { } skill)
        {
            return new Refusal(
                MightAndMagic7Codes.EquipmentSkillUnknown,
                $"The item table gives {item.Definition} the skill '{named}', which this game's skill table does not carry, so nobody can be said to have it.");
        }

        if (member.Skills.LevelOf(skill) > 0) return null;

        return new Refusal(
            MightAndMagic7Codes.EquipmentSkillMissing,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{member.Profile.Name} has not learned {skill}, which is what a {item.Definition} needs before it can be worn or wielded."));
    }

    /// <summary>Whether the member's hands have room for the item in the slot, or why not.</summary>
    private Refusal? Hands(PartyMember member, EquipmentSlot slot, ItemInstance item, MightAndMagic7WornItem worn)
    {
        if (slot == MightAndMagic7Figure.MainHand &&
            worn.Kind == MightAndMagic7WornKind.TwoHanded &&
            member.Equipment.ItemIn(MightAndMagic7Figure.OffHand) is { } held &&
            held.Id != item.Id)
        {
            return new Refusal(
                MightAndMagic7Codes.EquipmentHandsFull,
                $"{item.Definition} takes both hands, and {member.Profile.Name} holds {held.Definition} in the off hand; take it off first.");
        }

        if (slot != MightAndMagic7Figure.OffHand) return null;

        if (member.Equipment.ItemIn(MightAndMagic7Figure.MainHand) is { } main && main.Id != item.Id)
        {
            // What the main hand holds is read whether or not it is broken: a broken two-handed weapon still fills
            // both hands until it is taken off.
            if (Figure.Worn(main.Definition)?.Kind == MightAndMagic7WornKind.TwoHanded)
            {
                return new Refusal(
                    MightAndMagic7Codes.EquipmentHandsFull,
                    $"{member.Profile.Name} holds {main.Definition} in both hands, so nothing goes in the off hand until it comes off.");
            }
        }

        if (worn.Kind != MightAndMagic7WornKind.OneHanded) return null;

        // A second weapon is the donor's two exceptions: a dagger at expert, or a sword at master.
        int needed = worn.IsSkill("dagger") ? OffHandDaggerTier : worn.IsSkill("sword") ? OffHandSwordTier : 0;
        if (needed > 0 && SkillTierOf(member, worn.Skill) >= needed) return null;

        return new Refusal(
            MightAndMagic7Codes.EquipmentOffHandUntrained,
            needed == 0
                ? $"Only a dagger or a sword goes in the off hand as a second weapon, so {item.Definition} goes in the main hand."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{member.Profile.Name} needs {worn.Skill} at {(needed == OffHandDaggerTier ? "expert" : "master")} to hold {item.Definition} in the off hand."));
    }

    /// <summary>The rung a member stands at in the skill a word names, or zero when they have not learned it.</summary>
    internal static int SkillTierOf(PartyMember member, string word)
    {
        foreach (SkillEntry entry in member.Skills.Entries)
        {
            if (entry.Level > 0 && string.Equals(entry.Skill.Value, word, StringComparison.OrdinalIgnoreCase)) return entry.Tier.Value;
        }

        return 0;
    }
}
