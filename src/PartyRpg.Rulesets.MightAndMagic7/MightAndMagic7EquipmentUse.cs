using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answer about what a character may wear or wield: a weapon or a piece of armour needs the skill
/// its own table row names, and the five things that need no skill need none.
/// </summary>
/// <remarks>
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
/// A slot answers that question, because the slot is where the item goes: the game's own words for those
/// five places — the item table's <c>Belt</c>, <c>Boots</c>, <c>Cloak</c>, <c>Helm</c>, and
/// <c>Gauntlets</c> — are what this recognises, so an authored pack that writes "helmet" is understood as
/// the same place.
/// </para>
/// <para>
/// <b>What this cannot judge is refused rather than allowed.</b> An item whose row names a skill this game's
/// skill table does not carry — the shipped table files three clubs under a hidden skill it never declares —
/// cannot be said to be usable by anybody, and it is refused by name. Guessing that an unknown requirement
/// is no requirement would hand a character a weapon the game's own table says is not theirs.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7EquipmentUse : IEquipmentUseRule
{
    /// <summary>The definition kind the shipped item table is declared under.</summary>
    internal const string ItemDefinitionKind = "item";

    /// <summary>
    /// The slots that need no skill, as the game's own table words them.
    /// </summary>
    /// <remarks>
    /// The manual's five exceptions above, in the item table's own equipment words and in the plainer words a
    /// person writes: a pack that says "Boots", "boots", or "Cape" is describing the same place on the
    /// figure.
    /// </remarks>
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

    private static readonly string[] NoSkillSlots =
        ["belt", "boots", "cloak", "cape", "helm", "helmet", "gauntlets", "gauntlet"];

    private readonly Dictionary<ItemDefinitionId, string> _required;
    private readonly MightAndMagic7Skills _skills;

    private MightAndMagic7EquipmentUse(Dictionary<ItemDefinitionId, string> required, MightAndMagic7Skills skills)
    {
        _required = required;
        _skills = skills;
    }

    /// <summary>Reads what each item definition needs, or null when there is no content to read.</summary>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <param name="skills">This game's skill policy, which resolves an item's skill word to a declared skill.</param>
    /// <returns>This game's equipment rule, or null when nothing states what an item needs.</returns>
    internal static MightAndMagic7EquipmentUse? Read(ContentCatalog? catalog, MightAndMagic7Skills? skills)
    {
        if (catalog is null || skills is null) return null;

        Dictionary<ItemDefinitionId, string> required = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(ItemDefinitionKind))
        {
            string skill = entry.GetString("skill").Trim();
            if (skill.Length == 0) continue;
            required[new ItemDefinitionId(entry.Id)] = skill;
        }

        return new MightAndMagic7EquipmentUse(required, skills);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The slot is read before the item, because the manual's five exceptions are about the place rather than
    /// about the goods: a helmet needs no skill whatever it is made of, and the game's own table gives its
    /// helmets no skill column to read anyway.
    /// </remarks>
    public PartyRefusal? Judge(PartyMember member, EquipmentSlot slot, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(item);
        if (NeedsNoSkill(slot.Value)) return null;

        // An item whose definition names no skill is not a thing a character may be said to be trained for:
        // that is what a potion, a ring, or a scroll is.
        if (!_required.TryGetValue(item.Definition, out string? named))
        {
            return null;
        }

        // The shipped table files its wands under the group the donor reads as its own hidden misc skill
        // (OpenEnroth src/Engine/Tables/ItemTable.cpp:146, `valueOr(equipSkillMap, tokens[5], SKILL_MISC)`,
        // and `src/GUI/UI/NPC2.cpp`-era tables where misc is hidden and available to every class), and a wand
        // is fired at the donor's own fixed skill value rather than at its bearer's
        // (src/Engine/Spells/CastSpellInfo.h:61, WANDS_SKILL_VALUE) — so a wand needs no skill this game can
        // train, which is what this group means here.
        if (string.Equals(named, MiscGroup, StringComparison.OrdinalIgnoreCase)) return null;

        if (_skills.Resolve(named) is not { } skill)
        {
            return new PartyRefusal(
                "equipment-skill-unknown",
                $"The item table gives {item.Definition} the skill '{named}', which this game's skill table does not carry, so nobody can be said to have it.");
        }

        if (member.Skills.LevelOf(skill) > 0) return null;

        return new PartyRefusal(
            "equipment-skill-missing",
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{member.Profile.Name} has not learned {skill}, which is what a {item.Definition} needs before it can be worn or wielded."));
    }

    /// <summary>Whether a slot is one of the five places that need no skill.</summary>
    private static bool NeedsNoSkill(string slot)
    {
        foreach (string exempt in NoSkillSlots)
        {
            if (string.Equals(slot, exempt, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
