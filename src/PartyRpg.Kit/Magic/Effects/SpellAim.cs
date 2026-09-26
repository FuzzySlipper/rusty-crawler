namespace PartyRpg.Kit.Magic;

/// <summary>Something a spell whose aim names no actor can be pointed at, as the effect path offers it.</summary>
/// <remarks>
/// <para>
/// A travel spell moves the party to a place and a spell that acts on a thing names an object: neither aim is
/// an actor, so neither can be offered from the fight's own target list. What the effect path can offer is
/// what it alone knows — the places a portal may reach, the things a hand may move — and this is that offer,
/// published so a screen can draw the choice and send back the identity it drew. The mechanism judges the
/// identity it is handed exactly as it judges a named actor.
/// </para>
/// <para>
/// <b>The kit never reads one of these.</b> The identity, the name a person reads, and the kind are the
/// game's own words, handed back untouched, which is what keeps a destination out of the mechanism.
/// </para>
/// </remarks>
/// <param name="Aim">The identity a casting names, as content or the game spells it.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Kind">What sort of thing it is, as the game's own word.</param>
public readonly record struct SpellAim
{
    /// <summary>Creates an aim.</summary>
    /// <param name="aim">The identity a casting names, which must not be blank.</param>
    /// <param name="name">What a person reads for it, which must not be blank.</param>
    /// <param name="kind">What sort of thing it is, which must not be blank.</param>
    /// <exception cref="ArgumentException">A part of the aim is blank, which would offer a choice nothing can name.</exception>
    public SpellAim(string aim, string name, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aim);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Aim = aim;
        Name = name;
        Kind = kind;
    }

    /// <summary>The identity a casting names to choose this.</summary>
    public string Aim { get; }

    /// <summary>What a person reads for it.</summary>
    public string Name { get; }

    /// <summary>What sort of thing it is.</summary>
    public string Kind { get; }
}

/// <summary>What a spell whose aim names no actor may be pointed at, as the effect path answers.</summary>
/// <remarks>
/// Asked per spell rather than once for the session, because what a spell may name is a property of the spell
/// and of the state the world is in: a portal reaches the places the party has been to, and the list is
/// therefore different at every step. A spell that names no such thing answers nothing, and its casting is
/// made with no target at all.
/// </remarks>
public interface ISpellAimRule
{
    /// <summary>What one spell may be pointed at right now, in the order a screen should draw it.</summary>
    /// <param name="spell">The spell being cast.</param>
    /// <returns>The aims it may name, empty when its aim names no such thing.</returns>
    IReadOnlyList<SpellAim> AimsOf(SpellDefinition spell);
}
