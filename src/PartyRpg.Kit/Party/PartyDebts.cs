using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Party;

/// <summary>What the party owes on one account.</summary>
/// <param name="Account">The account's name, which is the game's word for what is owed and to whom.</param>
/// <param name="Coins">What is owed on it.</param>
public readonly record struct PartyDebt(string Account, int Coins);

/// <summary>A counter that will not serve the party, and the moment of the one clock it will serve it again.</summary>
/// <remarks>
/// The moment is the clock's own elapsed game time rather than a calendar date, so a ban reads against the same
/// quantity a save restores the clock to and cannot drift from it.
/// </remarks>
/// <param name="Counter">The counter's identity in content.</param>
/// <param name="UntilMilliseconds">The elapsed game time, in milliseconds, at which the counter serves the party again.</param>
public readonly record struct PartyBan(string Counter, long UntilMilliseconds);

/// <summary>What the party owes, account by account: coin it has been charged and has not yet paid.</summary>
/// <remarks>
/// <para>
/// A debt is the other side of a holding: coin the party must hand over rather than coin kept for it. It is
/// party state because the world charges the band, and it is kept apart from the purse because owing is not
/// paying — a charge the purse cannot cover still stands, and the purse is never driven below empty to record
/// it. No clock ends a debt and nothing that ends a spell touches it.
/// </para>
/// <para>
/// The writers are the owners that charge the party — a game's rule for a crime, a counter that catches a hand on its shelf —
/// and the service mechanism's repayment, which is the one way a debt goes down. How much any of them is worth,
/// and which counter collects it, are a game's answers.
/// </para>
/// </remarks>
public sealed class PartyDebts
{
    private readonly Tally _accounts;

    /// <summary>
    /// The change stamp this state took when it last changed, or when it was made: a reader that kept what it built
    /// beside this stamp reads the state again only when the stamp has moved (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp => _accounts.Stamp;

    /// <summary>Creates the party's debts.</summary>
    /// <param name="debts">What is already owed, account by account.</param>
    /// <exception cref="ArgumentException">An account is listed twice, is unnamed, or owes nothing.</exception>
    public PartyDebts(IEnumerable<PartyDebt>? debts = null)
    {
        _accounts = new Tally("debt", minimum: 1);
        foreach (PartyDebt debt in debts ?? []) _accounts.Add(debt.Account, debt.Coins, nameof(debts));
    }

    /// <summary>Every account that is owed something, in the order it was first charged.</summary>
    public IReadOnlyList<PartyDebt> All => [.. _accounts.Entries.Select(entry => new PartyDebt(entry.Name, entry.Value))];

    /// <summary>What is owed on one account, which is zero for an account never charged or paid off.</summary>
    /// <param name="account">The account's name.</param>
    public int OwedOn(string account) => _accounts.ValueOf(account);

    /// <summary>States what is owed on one account; an account left owing nothing is closed rather than kept at zero.</summary>
    /// <param name="account">The account's name.</param>
    /// <param name="coins">What is now owed, which must not be negative.</param>
    /// <exception cref="ArgumentException">The name is blank or the amount is negative.</exception>
    public void Owe(string account, int coins)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        if (coins == 0) _accounts.Remove(account);
        else _accounts.Set(account, coins);
    }
}

/// <summary>The counters that will not serve the party, each until a moment of the one clock.</summary>
/// <remarks>
/// <para>
/// A ban is the party's own state rather than the counter's, because it is the party that was caught and the
/// party a save carries: a counter is content, and the same counter serves every other customer. It ends on the
/// clock rather than on a deadline the clock holds — a ban is read when the party walks up to the counter, and a
/// ban that has run out is simply one that no longer bars anybody — so nothing is scheduled to end it and a save
/// carries the moment rather than a handle.
/// </para>
/// <para>
/// The one writer is the service mechanism, when a counter's answer about a theft bars the party; how long a
/// counter stays shut against the party is a game's answer.
/// </para>
/// </remarks>
public sealed class PartyBans
{
    private readonly List<PartyBan> _bans = [];

    /// <summary>
    /// The change stamp this state took when it last changed, or when it was made: a reader that kept what it built
    /// beside this stamp reads the state again only when the stamp has moved (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>Creates the party's bans.</summary>
    /// <param name="bans">The bans already standing.</param>
    /// <exception cref="ArgumentException">A counter is listed twice or is unnamed.</exception>
    public PartyBans(IEnumerable<PartyBan>? bans = null)
    {
        foreach (PartyBan ban in bans ?? [])
        {
            if (IndexOf(ban.Counter) >= 0)
            {
                throw new ArgumentException($"The ban from '{ban.Counter}' is listed more than once, so which is meant would be ambiguous.", nameof(bans));
            }

            Bar(ban.Counter, GameDuration.FromMilliseconds(ban.UntilMilliseconds));
        }
    }

    /// <summary>Every ban recorded, in the order it was laid, including any that have run out.</summary>
    public IReadOnlyList<PartyBan> All => [.. _bans];

    /// <summary>Until when a counter bars the party, or null when it does not bar it at the given moment.</summary>
    /// <param name="counter">The counter's identity in content.</param>
    /// <param name="now">The clock's elapsed game time now.</param>
    public GameDuration? BarredUntil(string counter, GameDuration now)
    {
        int index = IndexOf(counter);
        if (index < 0) return null;
        long until = _bans[index].UntilMilliseconds;
        return until > now.Milliseconds ? GameDuration.FromMilliseconds(until) : null;
    }

    /// <summary>Bars the party from a counter until a moment, replacing any ban it already stood under.</summary>
    /// <param name="counter">The counter's identity in content.</param>
    /// <param name="until">The clock's elapsed game time at which the counter serves the party again.</param>
    /// <exception cref="ArgumentException">The counter is unnamed.</exception>
    public void Bar(string counter, GameDuration until)
    {
        if (string.IsNullOrWhiteSpace(counter)) throw new ArgumentException("A ban must name the counter it bars the party from.", nameof(counter));
        int index = IndexOf(counter);
        PartyBan ban = new(counter, until.Milliseconds);
        if (index >= 0) _bans[index] = ban;
        else _bans.Add(ban);
        Stamp = ChangeStamp.Next();
    }

    /// <summary>Forgets every ban that has run out by the given moment, so a save carries only what still bars.</summary>
    /// <param name="now">The clock's elapsed game time now.</param>
    public void Lapse(GameDuration now)
    {
        if (_bans.RemoveAll(ban => ban.UntilMilliseconds <= now.Milliseconds) > 0) Stamp = ChangeStamp.Next();
    }

    private int IndexOf(string counter) =>
        _bans.FindIndex(ban => string.Equals(ban.Counter, counter, StringComparison.Ordinal));
}
