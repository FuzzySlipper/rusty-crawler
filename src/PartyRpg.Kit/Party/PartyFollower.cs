namespace PartyRpg.Kit.Party;

/// <summary>One follower travelling with the party.</summary>
/// <remarks>
/// A follower is a person in the party but not a member: they are not created, own no equipment, and are
/// not in the marching order, so they carry their own durable identity, the definition they came from, and
/// the name a player reads. What a follower does for the party — a service, a bonus, a piece of the story —
/// is content and ruleset work over the definition this names.
/// </remarks>
public sealed class PartyFollower
{
    /// <summary>Creates a follower.</summary>
    /// <param name="id">The follower's durable identity, minted from the party's identity source.</param>
    /// <param name="definition">The follower definition this person came from, in content.</param>
    /// <param name="name">The name a player reads, which must not be blank.</param>
    /// <param name="kind">Why the follower travels with the party.</param>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    public PartyFollower(PartyMemberId id, FollowerDefinitionId definition, string name, FollowerKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Definition = definition;
        Name = name;
        Kind = kind;
    }

    /// <summary>The follower's durable identity, which a save round-trips.</summary>
    public PartyMemberId Id { get; }

    /// <summary>The follower definition this person came from, in content.</summary>
    public FollowerDefinitionId Definition { get; }

    /// <summary>The name a player reads.</summary>
    public string Name { get; }

    /// <summary>Why the follower travels with the party.</summary>
    public FollowerKind Kind { get; }

    /// <summary>Whether this follower counts against the hired limit.</summary>
    public bool IsHired => Kind == FollowerKind.Hired;

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Definition}, {Kind})";
}
