namespace PartyRpg.Kit.Combat;

/// <summary>
/// How one actor attacks: hand-to-hand, at range, or with magic.
/// </summary>
/// <remarks>
/// <para>
/// Three kinds because the design's one act key resolves them in that order — a quick spell first, then a
/// bow or a wand, then hand-to-hand — and because each is a different way of reaching a target. What each
/// kind costs in recovery, how far it reaches, and what an actor's attack resolves to are the ruleset's
/// answers; this is the vocabulary they are given in.
/// </para>
/// <para>
/// <b>The kind gates nothing on its own.</b> There is deliberately no per-kind cooldown here: a game
/// paces every attack of every kind from one recovery quantity per actor — a character's attack recovery
/// covers a swung weapon, a bow, and a wand alike, and a creature's single recovery value paces all of its
/// attacks. A cooldown invented per kind would be a second pacing beside the one the turn-based mode
/// derives its order from.
/// </para>
/// </remarks>
public enum AttackKind
{
    /// <summary>Hand-to-hand, at arm's length.</summary>
    Melee,

    /// <summary>At range: a bow, a thrown weapon, a wand's bolt.</summary>
    Ranged,

    /// <summary>A spell, cast at a target rather than swung at one.</summary>
    Spell,
}

/// <summary>The one vocabulary the kinds of attack are spelled with, on the wire and in a report.</summary>
/// <remarks>
/// A kind with no word is refused rather than published as an empty string: a panel that could not tell
/// "nothing was attempted" from an attack this wire has no name for would show a mechanism it cannot
/// describe as one that did nothing.
/// </remarks>
public static class AttackKinds
{
    /// <summary>The wire name for a kind of attack.</summary>
    /// <param name="kind">The kind of attack.</param>
    /// <returns>The word the wire spells it as.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no wire name.</exception>
    public static string WireName(AttackKind kind) => kind switch
    {
        AttackKind.Melee => "melee",
        AttackKind.Ranged => "ranged",
        AttackKind.Spell => "spell",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown attack kind."),
    };
}
