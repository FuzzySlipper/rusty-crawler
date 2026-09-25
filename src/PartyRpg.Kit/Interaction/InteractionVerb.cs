namespace PartyRpg.Kit.Interaction;

/// <summary>What a player's use asks of a target: the workflow's own vocabulary, not a game's.</summary>
/// <remarks>
/// <para>
/// One use workflow serves every verb, which is why a verb is a value rather than a class: searching a
/// container, opening a door, defeating a lock, defeating a trap, pulling a lever, speaking to a person,
/// and reading a sign all identify a target, check what it requires, apply what the use produces, and
/// report it. What differs between them is the policy that answers for them, never the path taken.
/// </para>
/// <para>
/// The verb a target offers is the ruleset's answer about that content, so a game that uses a door
/// differently changes its ruleset, not this list. A target <em>kind</em> — a door, a chest, a lever, a
/// sign, a person — is content's word and needs no change here at all, which is what keeps a new kind from
/// becoming a new class.
/// </para>
/// </remarks>
public enum InteractionVerb
{
    /// <summary>Look something over and take what it holds: a chest, a pile, a corpse.</summary>
    Search,

    /// <summary>Open something that stands closed: a door, a gate, a lid.</summary>
    Open,

    /// <summary>Defeat what holds something shut, using whatever it requires.</summary>
    Unlock,

    /// <summary>
    /// Defeat what would hurt the party when the target is used: a trap, a ward, a rune. It is a use of its
    /// own rather than part of searching or opening, because a party that knows about a trap decides what
    /// to do about it, and because an attempt can fail and cost them.
    /// </summary>
    Disarm,

    /// <summary>Work something that moves or fires when it is used: a lever, a switch, a chain.</summary>
    Pull,

    /// <summary>Speak with somebody who can answer.</summary>
    Talk,

    /// <summary>Read what something says: a sign, a plaque, a book.</summary>
    Read,
}
