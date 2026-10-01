using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Services;

/// <summary>One theft a member of the party tries, as the service rule judges it and draws what came of it.</summary>
/// <remarks>
/// <para>
/// A theft is tried in one of two places, and the request says which: at a counter, from a line of its shelves —
/// <see cref="Service"/> and <see cref="Subject"/> are stated — or from a person the party stands with —
/// <see cref="Person"/> is stated, with the place they stand in. A request that states the counter or the person
/// and no subject asks only whether the member could try at all, which is what a panel offers the act on.
/// </para>
/// <para>
/// The party and the clock travel whole for the same reason they do for every other service answer: how good a
/// thief a member is, how the town regards the party, and what the party already owes are this game's readings
/// of the party's own state.
/// </para>
/// </remarks>
/// <param name="Party">The party a member of which is stealing.</param>
/// <param name="Thief">The member who tries.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
/// <param name="Service">The counter stolen from, or null when the theft is from a person.</param>
/// <param name="Subject">The line of the counter's shelves the member reaches for, or null when nothing is named yet.</param>
/// <param name="Place">The place a person stands in, which is the default when the theft is at a counter.</param>
/// <param name="Person">The person stolen from, or null when the theft is at a counter.</param>
public sealed record ServiceTheftRequest(
    PartyEntity Party,
    PartyMemberId Thief,
    GameClock? Clock,
    ServiceDefinition? Service = null,
    ServiceSubject? Subject = null,
    PlaceId Place = default,
    PlacementDefinition? Person = null);

/// <summary>What one theft came to, as the service rule drew it: what was taken, whether the thief was seen, and the cost.</summary>
/// <remarks>
/// <para>
/// <b>The rule draws; the mechanism carries it out.</b> Whether the thief was caught, what came away, what the
/// town fines the party, what the world hears of it, and how long a counter stays shut against the party are
/// this game's answers. Putting the line in the pack, crediting the coins through the party's one ledger, adding
/// the fine to what the party owes, telling the progression owner of the deed, and barring the party from the
/// counter are the mechanism's, and happen in one place whichever kind of theft it was.
/// </para>
/// <para>
/// A theft is not refused for being caught: a thief who is seen still tried, and what it costs the party is the
/// outcome rather than a refusal. A refusal is for a theft that could not be tried at all — nobody here to rob,
/// a member who cannot act or has no hand for it — and it moves nothing.
/// </para>
/// </remarks>
public sealed record ServiceTheft
{
    private ServiceTheft(
        Refusal? refusal,
        bool caught,
        bool taken,
        int coins,
        IReadOnlyList<ItemDefinitionId> items,
        bool marked,
        int fine,
        string account,
        string deed,
        GameDuration ban,
        string message)
    {
        Refusal = refusal;
        Caught = caught;
        Taken = taken;
        Coins = coins;
        Items = items;
        Marked = marked;
        Fine = fine;
        Account = account;
        Deed = deed;
        Ban = ban;
        Message = message;
    }

    /// <summary>A theft that was tried, and what it came to.</summary>
    /// <param name="caught">Whether the thief was seen.</param>
    /// <param name="taken">Whether the line of the counter's shelves the thief reached for came away, one of it.</param>
    /// <param name="coins">What coin was lifted from a person, which cannot be negative.</param>
    /// <param name="items">What was lifted from a person besides coin.</param>
    /// <param name="marked">Whether what came away carries the stolen mark on its own state.</param>
    /// <param name="fine">What the party is fined, added to what it owes, which cannot be negative.</param>
    /// <param name="account">The debt account the fine is owed on, which a fine must name.</param>
    /// <param name="deed">The word the world hears of the theft as, or empty when it hears of nothing.</param>
    /// <param name="ban">How long the counter will not serve the party, or none.</param>
    /// <param name="message">What happened, in the words a person reads.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">The message is blank, or a fine names no account.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The coins or the fine are negative.</exception>
    public static ServiceTheft Tried(
        bool caught,
        bool taken,
        int coins,
        IReadOnlyList<ItemDefinitionId>? items,
        bool marked,
        int fine,
        string account,
        string deed,
        GameDuration ban,
        string message)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        ArgumentOutOfRangeException.ThrowIfNegative(fine);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (fine > 0 && string.IsNullOrWhiteSpace(account))
        {
            throw new ArgumentException("A fine is owed on an account, and this one names none, so nobody could ever collect it.", nameof(account));
        }

        return new ServiceTheft(null, caught, taken, coins, items ?? [], marked, fine, account ?? string.Empty, deed ?? string.Empty, ban, message);
    }

    /// <summary>A theft that could not be tried at all, and why.</summary>
    /// <param name="refusal">Why nothing was tried.</param>
    /// <returns>The refusal.</returns>
    /// <exception cref="ArgumentNullException">No refusal was given.</exception>
    public static ServiceTheft Refused(Refusal refusal) =>
        new(refusal ?? throw new ArgumentNullException(nameof(refusal)), false, false, 0, [], false, 0, string.Empty, string.Empty, GameDuration.None, refusal.Message);

    /// <summary>Why the theft could not be tried, or null when it was.</summary>
    public Refusal? Refusal { get; }

    /// <summary>Whether the theft was tried.</summary>
    public bool IsTried => Refusal is null;

    /// <summary>Whether the thief was seen.</summary>
    public bool Caught { get; }

    /// <summary>Whether the line the thief reached for came away, one of it.</summary>
    public bool Taken { get; }

    /// <summary>What coin was lifted from a person.</summary>
    public int Coins { get; }

    /// <summary>What was lifted from a person besides coin.</summary>
    public IReadOnlyList<ItemDefinitionId> Items { get; }

    /// <summary>Whether what came away carries the stolen mark on its own state.</summary>
    public bool Marked { get; }

    /// <summary>What the party is fined, added to what it owes.</summary>
    public int Fine { get; }

    /// <summary>The debt account the fine is owed on, empty when there is no fine.</summary>
    public string Account { get; }

    /// <summary>The word the world hears of the theft as, or empty when it hears of nothing.</summary>
    public string Deed { get; }

    /// <summary>How long the counter will not serve the party, or none.</summary>
    public GameDuration Ban { get; }

    /// <summary>What happened, or why nothing was tried.</summary>
    public string Message { get; }
}
