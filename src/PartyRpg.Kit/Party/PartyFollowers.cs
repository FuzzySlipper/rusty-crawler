namespace PartyRpg.Kit.Party;

/// <summary>The followers travelling with the party, under the hired limit the game sets.</summary>
/// <remarks>
/// The hired limit is policy — a number from tuning, handed in when the party is built — and it binds only
/// hired followers: a character who joins for the story's reasons travels outside it. Enforcing the limit
/// here rather than at each call site is what keeps a party from quietly growing a fifth hired companion
/// because one code path forgot to count.
/// </remarks>
public sealed class PartyFollowers
{
    private readonly List<PartyFollower> _followers = [];
    private readonly int _hiredLimit;

    /// <summary>Creates the party's followers.</summary>
    /// <param name="hiredLimit">How many hired followers the party may have at once, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The limit is negative.</exception>
    public PartyFollowers(int hiredLimit = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hiredLimit);
        _hiredLimit = hiredLimit;
    }

    /// <summary>How many hired followers the party may have at once.</summary>
    public int HiredLimit => _hiredLimit;

    /// <summary>The followers, in the order they joined.</summary>
    public IReadOnlyList<PartyFollower> Followers => _followers;

    /// <summary>How many followers are travelling with the party.</summary>
    public int Count => _followers.Count;

    /// <summary>How many of them are hired, which is what the limit counts.</summary>
    public int HiredCount
    {
        get
        {
            int hired = 0;
            foreach (PartyFollower follower in _followers)
            {
                if (follower.IsHired) hired++;
            }

            return hired;
        }
    }

    /// <summary>Whether a person with this durable identity is travelling with the party.</summary>
    /// <param name="id">The follower's durable identity.</param>
    public bool Contains(PartyMemberId id) => IndexOf(id) >= 0;

    /// <summary>Adds a follower, refusing a hired one the limit has no room for.</summary>
    /// <param name="follower">The follower joining.</param>
    /// <returns>A refusal when the hired limit is full or the identity is already with the party, or null when the follower joined.</returns>
    public PartyRefusal? Add(PartyFollower follower)
    {
        ArgumentNullException.ThrowIfNull(follower);
        if (IndexOf(follower.Id) >= 0)
        {
            return new PartyRefusal(
                "follower-already-present",
                $"{follower.Name} is already travelling with the party, so nobody joined twice.");
        }

        if (follower.IsHired && HiredCount >= _hiredLimit)
        {
            return new PartyRefusal(
                "hired-limit-reached",
                $"The party may hire {_hiredLimit} companion(s) and already has {HiredCount}, so {follower.Name} cannot be hired.");
        }

        _followers.Add(follower);
        return null;
    }

    /// <summary>Removes a follower, which a dismissal or the end of a story does.</summary>
    /// <param name="id">The follower's durable identity.</param>
    /// <returns>Whether the follower was travelling with the party.</returns>
    public bool Remove(PartyMemberId id)
    {
        int index = IndexOf(id);
        if (index < 0) return false;
        _followers.RemoveAt(index);
        return true;
    }

    private int IndexOf(PartyMemberId id)
    {
        for (int index = 0; index < _followers.Count; index++)
        {
            if (_followers[index].Id == id) return index;
        }

        return -1;
    }
}
