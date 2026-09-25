namespace PartyRpg.Kit.Party;

/// <summary>
/// The origin of the party's durable identities: the next member identity and the next item identity.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place a durable identity is minted, and it is a party component rather than a static
/// counter because the cursor is state: a save records it, and a restored party must never mint a value
/// that save already carries. Restoration therefore reads the recorded cursor, and the factory refuses a
/// save whose cursor has fallen behind an identity the same save holds.
/// </para>
/// <para>
/// Nothing minted here is runtime identity — the engine's entity ids belong to the store that holds the
/// party and are rebuilt on every load — and nothing here is content identity, because a definition's id
/// is written by content or a ruleset and must never be minted from this counter.
/// </para>
/// </remarks>
public sealed class PartyIdentitySource
{
    private ulong _nextMemberValue;
    private ulong _nextItemValue;

    /// <summary>Creates the source at the value the party it belongs to has not yet used.</summary>
    /// <param name="nextMemberValue">The next member identity to mint, which must not be zero.</param>
    /// <param name="nextItemValue">The next item instance identity to mint, which must not be zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">A cursor is zero, which would mint an identity that names nothing.</exception>
    public PartyIdentitySource(ulong nextMemberValue = 1, ulong nextItemValue = 1)
    {
        if (nextMemberValue == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextMemberValue),
                nextMemberValue,
                "A member identity cursor starts at one; zero would mint an identity that names no member.");
        }

        if (nextItemValue == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextItemValue),
                nextItemValue,
                "An item identity cursor starts at one; zero would mint an identity that names no item instance.");
        }

        _nextMemberValue = nextMemberValue;
        _nextItemValue = nextItemValue;
    }

    /// <summary>The next member identity this party would mint, which is what a save records.</summary>
    public ulong NextMemberValue => _nextMemberValue;

    /// <summary>The next item instance identity this party would mint, which is what a save records.</summary>
    public ulong NextItemValue => _nextItemValue;

    /// <summary>Mints the identity of a person joining the party, whether a member or a follower.</summary>
    /// <exception cref="InvalidOperationException">The cursor has no value left to mint.</exception>
    public PartyMemberId MintMemberId()
    {
        if (_nextMemberValue == ulong.MaxValue)
        {
            throw new InvalidOperationException(
                "The member identity cursor is exhausted, so no further person can be given an identity a save could carry.");
        }

        return new PartyMemberId(_nextMemberValue++);
    }

    /// <summary>Mints the identity of an item instance the party takes.</summary>
    /// <exception cref="InvalidOperationException">The cursor has no value left to mint.</exception>
    public ItemInstanceId MintItemId()
    {
        if (_nextItemValue == ulong.MaxValue)
        {
            throw new InvalidOperationException(
                "The item identity cursor is exhausted, so no further item can be given an identity a save could carry.");
        }

        return new ItemInstanceId(_nextItemValue++);
    }
}
