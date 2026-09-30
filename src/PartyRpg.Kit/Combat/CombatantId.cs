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

    /// <summary>Reads the text form a projection published back into the identity it named.</summary>
    /// <remarks>
    /// A screen hands back what it drew, so this is the one reader of <see cref="ToString"/>'s form: a
    /// mechanism compares identities rather than their words.
    /// </remarks>
    /// <param name="text">The published form: <c>member:</c> or <c>actor:</c> followed by a whole number.</param>
    /// <param name="id">The identity it names, when it names one.</param>
    /// <returns>Whether the text is an identity's published form.</returns>
    public static bool TryParse(string? text, out CombatantId id)
    {
        id = default;
        if (text is null) return false;
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0) return false;
        CombatantKind? kind = text[..colon] switch
        {
            "member" => CombatantKind.PartyMember,
            "actor" => CombatantKind.WorldActor,
            _ => null,
        };
        string value = text[(colon + 1)..];
        if (kind is not { } known || !ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number)) return false;
        id = new CombatantId(known, number.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    /// <inheritdoc />
    public override string ToString() =>
        Kind == CombatantKind.PartyMember
            ? $"member:{Value}"
            : string.Create(CultureInfo.InvariantCulture, $"actor:{Value}");
}
