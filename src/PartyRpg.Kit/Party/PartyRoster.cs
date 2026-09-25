using System.Diagnostics.CodeAnalysis;

namespace PartyRpg.Kit.Party;

/// <summary>
/// The party's members, in the order the party stands in.
/// </summary>
/// <remarks>
/// <para>
/// The roster is the party's one list of people, and its order is the party's order — the order a
/// formation, a marching line, and a portrait row all read. A member is an engine entity, so this holds the
/// facades over those entities rather than copying anything about them: reading a member here reads the
/// components attached to its own entity.
/// </para>
/// <para>
/// A party with no members is refused, because a party is the thing the rest of the game attaches to and an
/// empty one has nothing to attach to. Adding and removing members is the creation and party-management
/// work, not the roster's: this holds what exists now and answers for it.
/// </para>
/// </remarks>
public sealed class PartyRoster
{
    private readonly List<PartyMember> _members;

    /// <summary>Creates a roster from the members a party is made of.</summary>
    /// <param name="members">The members, in the order the party stands in.</param>
    /// <exception cref="ArgumentException">The party has no members, or two members share a durable identity.</exception>
    public PartyRoster(IEnumerable<PartyMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        _members = [];
        HashSet<PartyMemberId> seen = [];
        foreach (PartyMember member in members)
        {
            if (!seen.Add(member.Id))
            {
                throw new ArgumentException(
                    $"Member {member.Id} appears twice in the roster, so two people would answer to one identity.",
                    nameof(members));
            }

            _members.Add(member);
        }

        if (_members.Count == 0)
        {
            throw new ArgumentException(
                "A party has at least one member; an empty roster is not a party the rest of the game can attach to.",
                nameof(members));
        }
    }

    /// <summary>The members, in the order the party stands in.</summary>
    public IReadOnlyList<PartyMember> Members => _members;

    /// <summary>How many members the party has.</summary>
    public int Count => _members.Count;

    /// <summary>Whether a person with this durable identity is a member.</summary>
    /// <param name="id">The member's durable identity.</param>
    public bool Contains(PartyMemberId id) => TryMember(id, out _);

    /// <summary>Reads one member by durable identity.</summary>
    /// <param name="id">The member's durable identity.</param>
    /// <exception cref="ArgumentException">The party has no such member.</exception>
    public PartyMember Member(PartyMemberId id) =>
        TryMember(id, out PartyMember? member)
            ? member
            : throw new ArgumentException($"The party has no member {id}, so there is nobody to address.", nameof(id));

    /// <summary>Reads one member by durable identity.</summary>
    /// <param name="id">The member's durable identity.</param>
    /// <param name="member">The member, when the party has one with that identity.</param>
    /// <returns>Whether the party has that member.</returns>
    public bool TryMember(PartyMemberId id, [NotNullWhen(true)] out PartyMember? member)
    {
        foreach (PartyMember candidate in _members)
        {
            if (candidate.Id != id) continue;
            member = candidate;
            return true;
        }

        member = null;
        return false;
    }
}
