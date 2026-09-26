namespace PartyRpg.Kit.Magic;

/// <summary>What one spell is aimed at, as the mechanism's own small vocabulary of aims.</summary>
/// <remarks>
/// <para>
/// A spell's aim is what decides whether a casting needs a target at all and which of the things the
/// session holds may be one: the caster alone, one of its own side, one of the other side, or the whole
/// band with nobody named. It is deliberately a vocabulary rather than a rule — how far a spell reaches,
/// whether an area is involved, and what a game calls its own aims are the ruleset's answers over its own
/// definitions, and the mechanism reads only which of these five a definition states.
/// </para>
/// <para>
/// <b>The words are the wire's, not a game's.</b> A projection publishes them so a screen can offer the
/// targets a casting may name without holding a rule of its own, and a refusal can say which aim it was
/// judging.
/// </para>
/// </remarks>
public enum SpellTargeting
{
    /// <summary>The spell names nobody: a utility, a travel, or a detection whose subject is the world.</summary>
    None,

    /// <summary>The spell is cast on the caster alone.</summary>
    Caster,

    /// <summary>The spell is cast on one of the party's own members.</summary>
    Ally,

    /// <summary>The spell is cast on one member of the opposition.</summary>
    Foe,

    /// <summary>The spell is cast on the whole band and nobody is named.</summary>
    Party,
}

/// <summary>The one vocabulary a spell's aim is spelled with, on the wire and in a refusal.</summary>
/// <remarks>
/// An aim with no word is refused rather than published as an empty string, exactly as an attack kind is:
/// a panel that could not tell "this spell names nobody" from an aim this wire has no name for would show a
/// mechanism it cannot describe as one that needs nothing.
/// </remarks>
public static class SpellTargetings
{
    /// <summary>The wire name for one aim.</summary>
    /// <param name="targeting">The aim to spell.</param>
    /// <returns>The word the wire spells it as.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The aim has no wire name.</exception>
    public static string WireName(SpellTargeting targeting) => targeting switch
    {
        SpellTargeting.None => "none",
        SpellTargeting.Caster => "caster",
        SpellTargeting.Ally => "ally",
        SpellTargeting.Foe => "foe",
        SpellTargeting.Party => "party",
        _ => throw new ArgumentOutOfRangeException(nameof(targeting), targeting, "Unknown spell targeting."),
    };

    /// <summary>Whether a casting with this aim names a target at all.</summary>
    /// <param name="targeting">The aim being asked about.</param>
    /// <returns>Whether a target must be named.</returns>
    public static bool NamesTarget(SpellTargeting targeting) =>
        targeting is SpellTargeting.Ally or SpellTargeting.Foe;
}
