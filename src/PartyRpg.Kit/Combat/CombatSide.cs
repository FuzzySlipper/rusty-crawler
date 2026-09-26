namespace PartyRpg.Kit.Combat;

/// <summary>Which side of a fight an actor stands on.</summary>
/// <remarks>
/// <para>
/// Three values rather than two, because most of what stands in a place is in no fight at all: a person
/// going about their day, or a creature that has not noticed the party yet. Calling those "the party's side"
/// would make attacking one a betrayal of an alliance that does not exist, and calling them enemies would
/// put the whole place in the fight the moment the party walked in.
/// </para>
/// <para>
/// A neutral creature is what makes "hostility is what the party has done" expressible: it is a creature
/// the party may attack, and attacking it is what moves it to <see cref="Opposition"/>.
/// </para>
/// </remarks>
public enum CombatSide
{
    /// <summary>One of the party's own members.</summary>
    Party,

    /// <summary>
    /// A creature standing in the place that is not fighting: it has not noticed the party, or it does not
    /// attack on sight at all. It can be attacked, which is what puts it into the fight.
    /// </summary>
    Neutral,

    /// <summary>
    /// A creature fighting the party — hostile on sight because of what it is, or hostile because of what
    /// the party has already done to it.
    /// </summary>
    Opposition,
}
