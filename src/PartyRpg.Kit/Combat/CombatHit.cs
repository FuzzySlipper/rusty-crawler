namespace PartyRpg.Kit.Combat;

/// <summary>A completed landed hit, reported after the canonical health owner applied its harm.</summary>
/// <param name="Actor">Who landed it.</param>
/// <param name="Target">Who took it.</param>
/// <param name="Kind">The attack kind.</param>
/// <param name="Damage">Total resolved harm, including separately resisted contributions.</param>
public sealed record CombatHit(CombatSubject Actor, CombatSubject Target, AttackKind Kind, int Damage);

/// <summary>A named participant that hears a completed hit, such as an effect that restores the striker's pool.</summary>
public interface ICombatHitObserver
{
    /// <summary>Reports a landed hit once, after its harm was applied.</summary>
    void Observe(CombatHit hit);
}

/// <summary>One additional damage kind the attack plan contributes; it uses the same hit and its own resistance.</summary>
/// <param name="Kind">The ruleset's damage kind.</param>
/// <param name="Damage">Its dice, drawn under a separate purpose of the admitted attack.</param>
public readonly record struct AttackDamagePart(DamageKindId Kind, DamageRoll Damage);

/// <summary>The resolved additional harm the fight reports.</summary>
/// <param name="Kind">Its damage kind.</param>
/// <param name="Rolled">What its dice drew.</param>
/// <param name="Damage">What landed after its own resistance.</param>
public readonly record struct CombatDamagePart(DamageKindId Kind, int Rolled, int Damage);
