using System.Globalization;
using PartyRpg.Kit.Party;
using Rusty.Engine.Entities;

namespace PartyRpg.Kit.Combat;

/// <summary>Which of the two kinds of actor a combatant is.</summary>
/// <remarks>
/// A fight stands over the world that is already there, so its actors come from two owners: the party,
/// whose members are durable and whose identity survives a save, and the place, whose entities are created
/// from content when the party walks in and destroyed when it walks out. The two stores assign their own
/// runtime identities, so an entity id and a member id can be the same number, and the kind is what keeps a
/// fight's two halves from ever naming the same actor by accident.
/// </remarks>
public enum CombatantKind
{
    /// <summary>One of the party's own members, named by its durable member identity.</summary>
    PartyMember,

    /// <summary>One entity standing in the place, named by the identity its visit gave it.</summary>
    WorldActor,
}

/// <summary>One actor's identity inside one fight.</summary>
/// <remarks>
/// <para>
/// This is an identity for the duration of an engagement and nothing more. A member's half is durable —
/// the id a save carries — while a world actor's half is the identity the population's own store gave it
/// for this visit, which is why the kind travels with the value and why nothing outside a fight should
/// record one.
/// </para>
/// <para>
/// The text form is what a projection publishes and a diagnostic names, so a fight's report says which
/// combatant it was about without the reader having to know either identity scheme.
/// </para>
/// </remarks>
public readonly record struct CombatantId
{
    private CombatantId(CombatantKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>Which of the two kinds of actor this combatant is.</summary>
    public CombatantKind Kind { get; }

    /// <summary>The identity that kind names: a member id, or the runtime entity id of a world actor.</summary>
    public string Value { get; }

    /// <summary>The combatant identity of a party member, which is the durable id the party gave it.</summary>
    /// <param name="member">The member's durable identity.</param>
    public static CombatantId Of(PartyMemberId member) =>
        new(CombatantKind.PartyMember, member.Value.ToString(CultureInfo.InvariantCulture));

    /// <summary>The combatant identity of an entity standing in the world.</summary>
    /// <param name="actor">The entity's runtime identity, local to the visit that created it.</param>
    public static CombatantId Of(EntityId actor) =>
        new(CombatantKind.WorldActor, actor.Value.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public override string ToString() =>
        Kind == CombatantKind.PartyMember
            ? $"member:{Value}"
            : string.Create(CultureInfo.InvariantCulture, $"actor:{Value}");
}
