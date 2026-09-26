using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What this game does with a spell once the party has paid for it: the one application point behind the
/// casting mechanism.
/// </summary>
/// <remarks>
/// <para>
/// <b>The category this build expresses is harm, and it goes through the fight.</b> A spell read under
/// <see cref="SpellEffects.Damage"/> is applied as an attack of the spell kind ordered through the fight's
/// own gated entry — the same entry a swing, a shot, and a creature's spell go through — so the spell's own
/// dice, its target's resistance to the spell's own kind of harm, the condition a landed hit leaves, the
/// recovery it costs, and the death it may cause are one mechanism rather than a second one beside it. The
/// ability the order names is the spell's own content identity, which is how the fight's resolution reads
/// that spell's numbers back out of this game's table.
/// </para>
/// <para>
/// <b>The other seven categories are named rather than faked.</b> Healing, resistance, condition, light,
/// travel, detection, and the utilities need state this build does not act on yet — a party-carried effect
/// with a deadline, a condition applied to a creature, a place the party is moved to — so the cast still
/// happens, the points are still spent, and the outcome says in the spell's own category that nothing in
/// the world changed. That is what the seam is for: the casting mechanism is complete and the categories
/// arrive behind it, one implementation each, without the mechanism changing.
/// </para>
/// <para>
/// <b>Asked before it is paid for.</b> <see cref="Judge"/> answers the two facts that must stop a cast
/// before its points are spent, and both are the fight's own: whether the caster's recovery has elapsed and
/// whether what is acting on the caster leaves it able to act. They are the same two gates the fight applies
/// to every order, asked a moment earlier so a refused cast costs nothing.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7SpellEffects : ISpellEffectRule
{
    private readonly MightAndMagic7Spells _spells;

    /// <summary>Creates this game's effect path over its own spell table.</summary>
    /// <param name="spells">This game's magic, which states what each spell does and rolls.</param>
    internal MightAndMagic7SpellEffects(MightAndMagic7Spells spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        _spells = spells;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A session standing in no fight has nothing that could refuse a caster: there is no recovery being
    /// paced and no creature laying anybody out, so a casting out of combat is judged by the mechanism
    /// alone. A caster the fight holds is judged by the fight's own two gates, exactly as an order is.
    /// </remarks>
    public SpellRefusal? Judge(SpellApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.Fight is not { } fight || fight.Find(application.CasterId) is not { } caster) return null;
        if (!caster.IsReady)
        {
            return SpellRefusal.CannotAct(
                caster.Name,
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"it is still recovering, with {caster.Recovery.Milliseconds}ms of game time left before it may act again"));
        }

        return fight.IsDown(caster)
            ? SpellRefusal.CannotAct(caster.Name, "what is acting on them leaves them unable to cast")
            : null;
    }

    /// <inheritdoc />
    public SpellApplicationOutcome Apply(SpellApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // Only a harmful spell has numbers a fight can resolve, and only a spell aimed at an opponent has an
        // attack to order: everything else is delivered here and reported as this build's own gap rather than
        // resolved as something it is not.
        if (IsHarm(application.Spell)
            && application.Target is { } target
            && application.Fight is { } fight
            && _spells.Harm(application.Spell) is not null)
        {
            CombatResult result = fight.Order(new AttackOrder(
                application.CasterId,
                AttackKind.Spell,
                target,
                application.Spell.Id.Value));
            return result.IsApplied
                ? SpellApplicationOutcome.Expressed(application.Spell.Effect, result.Message, result)
                : SpellApplicationOutcome.Unexpressed(
                    application.Spell.Effect,
                    $"{application.Spell.Name} was cast and the fight refused the blow it carries: {result.Message}");
        }

        return SpellApplicationOutcome.Unexpressed(
            application.Spell.Effect,
            $"{application.Spell.Name} was cast, and this build expresses no '{application.Spell.Effect}' effect yet, so nothing in the world changed.");
    }

    /// <summary>Whether a spell is one that harms, which is the category this build applies.</summary>
    private static bool IsHarm(SpellDefinition spell) =>
        string.Equals(spell.Effect, SpellEffects.Damage, StringComparison.Ordinal);
}
