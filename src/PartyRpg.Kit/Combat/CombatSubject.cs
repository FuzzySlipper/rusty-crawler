using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Entities;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// One actor a fight can be about, as the kit knows it: a member of the party, or an entity standing in the
/// place the party is in.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of what the kit hands a ruleset. It carries the live façades and not copies of them —
/// the member's own components, the engine actor the population created, the placement the entity was
/// created from — so a ruleset that reads a class, a skill, a component, or a content field reads the actor
/// itself, and a component attached to a world entity by whoever owns its behavior is visible here without
/// the kit learning anything about it.
/// </para>
/// <para>
/// A subject is created per read and owns nothing: it borrows the party's member or the population's entity
/// for the duration of one question, which is what keeps a fight from holding a stale handle on an entity
/// the party has already left behind.
/// </para>
/// </remarks>
public sealed class CombatSubject
{
    internal CombatSubject(
        CombatantId id,
        PlaceId place,
        PlacePose pose,
        PartyMember? member,
        PlacePopulationEntity? entity)
    {
        Id = id;
        Place = place;
        Pose = pose;
        Member = member;
        Entity = entity;
    }

    /// <summary>The identity this actor is a combatant under.</summary>
    public CombatantId Id { get; }

    /// <summary>The place this actor stands in.</summary>
    public PlaceId Place { get; }

    /// <summary>
    /// Where the actor stands in that place.
    /// </summary>
    /// <remarks>
    /// A party member's pose is the party's own: the party moves as one, and a member standing somewhere
    /// else is a shape this game does not have yet. A world actor's pose is the one its placement put it at.
    /// </remarks>
    public PlacePose Pose { get; }

    /// <summary>The party member this actor is, or null when it is an entity standing in the world.</summary>
    public PartyMember? Member { get; }

    /// <summary>The world entity this actor is, or null when it is a member of the party.</summary>
    public PlacePopulationEntity? Entity { get; }

    /// <summary>
    /// The engine actor behind this subject, which is where a ruleset reads or attaches components.
    /// </summary>
    /// <remarks>
    /// A party member's actor lives in the party's own store and a world entity's in the population's, which
    /// is why the identity carries a kind: the two stores assign their own numbers and the same number can
    /// name an actor in each.
    /// </remarks>
    public Actor Actor => Member is { } member ? member.Actor : Entity!.Actor;

    /// <summary>The runtime entity this subject is, in whichever store holds it.</summary>
    public EntityId RuntimeId => Member is { } member ? member.RuntimeId : Entity!.Id;

    /// <summary>Whether this subject is one of the party's own members.</summary>
    public bool IsMember => Member is not null;

    /// <summary>
    /// The content the world entity was created from, or null for a member.
    /// </summary>
    /// <remarks>
    /// The placement is what a ruleset reads a world actor's meaning from wherever it keeps that meaning in
    /// content — the same record the interaction and service mechanisms read their own meanings from — so
    /// nothing here has to grow a field per kind of creature.
    /// </remarks>
    public PlacementDefinition? Placement => Entity?.Placement;

    /// <inheritdoc />
    public override string ToString() => $"{Id} in place '{Place}' at {Pose}";
}
