using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Combat;

/// <summary>What an actor's own hand brings to an attack: how it strikes, what it strikes with, and what the attack spends.</summary>
/// <remarks>
/// <para>
/// A character's answer is not always a pair of fists. A weapon that carries a spell — a wand charged with
/// one — strikes as that spell, names the ability the fight's own resolution reads the spell's numbers from,
/// and spends one of the item's charges. All three travel together because they are one answer about one
/// item, and the fight reads them at the one moment an attack is initiated.
/// </para>
/// <para>
/// <b>The charge is the item's, and the fight spends it through the party.</b> How many charges the item's
/// kind holds when full is the game's own reading of its row, so it travels in <see cref="Charges"/>; how
/// many it has spent is the party's item state. The fight hands both to the party's own entry rather than
/// keeping a count, so a wand fired in a fight, one used from a panel, and one restored from a save are one
/// arithmetic.
/// </para>
/// </remarks>
/// <param name="Kind">Which kind of attack the weapon makes, which is the spell's kind for an item that carries one.</param>
/// <param name="Ability">What the attack is made with, named for the fight's own ability reading, empty when the kind alone states it.</param>
/// <param name="Charge">The item the attack spends a charge of, or null when the attack spends nothing.</param>
/// <param name="Charges">How many charges an item of that kind holds when full, which is the game's own reading of its row.</param>
public readonly record struct CombatWeapon(AttackKind Kind, string Ability, ItemInstanceId? Charge = null, int Charges = 0)
{
    /// <summary>Whether this attack spends a charge of an item.</summary>
    public bool SpendsACharge => Charge is not null;
}

/// <summary>What an actor brings to an attack of its own, as the game that owns its items answers.</summary>
/// <remarks>
/// <para>
/// <b>This is the weapon answer, asked where a weapon matters.</b> The fight asks it when it re-reads its
/// actors — so a member with a wand in hand is paced and offered as a spell-caster rather than as a pair of
/// fists, which is the donor's own act order of quick spell, then bow or wand, then hand-to-hand — and again
/// at the one moment an attack is initiated, where the ability and the charge are spent. A game that answers
/// nothing leaves every actor striking as its kind alone states, which is what a product with no items does.
/// </para>
/// <para>
/// <b>Asked about the actor, not about a kind of actor.</b> Whether a hand holds a wand, whether it has
/// charges left, and what spell it carries are all answers about one actor's own equipment, so a creature
/// and a character are the same question — and a ruleset that has nothing to say about either answers
/// nothing for both.
/// </para>
/// </remarks>
public interface ICombatWeaponRule
{
    /// <summary>What the actor attacks with now, or null when it fights with what its kind alone states.</summary>
    /// <param name="attacker">The actor whose weapon is read, with its equipment and its own state.</param>
    /// <returns>The weapon, or null when the actor brings none.</returns>
    CombatWeapon? WeaponOf(CombatSubject attacker);
}
