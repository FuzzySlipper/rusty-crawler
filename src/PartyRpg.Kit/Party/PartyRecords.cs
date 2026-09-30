using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>One thing a party has on record, and how many times.</summary>
/// <param name="Name">The record's name, which is content's: an errand's record, a flag a line set, a deed.</param>
/// <param name="Count">How many times it is on record, which is one for a mark and more for a counted deed.</param>
public readonly record struct PartyRecord(string Name, int Count);

/// <summary>What the party has deposited in one account.</summary>
/// <param name="Account">The account's name, which is the counter family that keeps it.</param>
/// <param name="Coins">What it holds.</param>
public readonly record struct PartyHolding(string Account, int Coins);

/// <summary>A passage the party holds: the place it reaches, and how many days the journey takes.</summary>
/// <param name="Destination">The place the passage reaches.</param>
/// <param name="Days">How long the journey takes, which tells one counter's journey from another's.</param>
public readonly record struct PartyPassage(PlaceId Destination, int Days);

/// <summary>
/// What the party has on record: the marks an errand, a conversation, and a rank leave, and the deeds a rank
/// counts.
/// </summary>
/// <remarks>
/// <para>
/// A record is a fact about what the party did, kept for as long as the party is: it never runs out, no clock
/// ends it, and nothing that ends a spell touches it. Each writer names its own records — the quest owner an
/// errand's, the conversation a line's flag, the progression owner a rank's — and every reader asks by name, so
/// what one owner leaves another can read without either naming the other.
/// </para>
/// <para>
/// The names are content's and are kept in the order they were first recorded, so two readings of one party
/// list its records the same way.
/// </para>
/// </remarks>
public sealed class PartyRecords
{
    private readonly Tally _records;

    /// <summary>Creates the party's records.</summary>
    /// <param name="records">What is already on record, in the order it was recorded.</param>
    /// <exception cref="ArgumentException">A record is listed twice, is unnamed, or is on record fewer than once.</exception>
    public PartyRecords(IEnumerable<PartyRecord>? records = null)
    {
        _records = new Tally("record", minimum: 1);
        foreach (PartyRecord record in records ?? []) _records.Add(record.Name, record.Count, nameof(records));
    }

    /// <summary>Everything on record, in the order it was first recorded.</summary>
    public IReadOnlyList<PartyRecord> All => [.. _records.Entries.Select(entry => new PartyRecord(entry.Name, entry.Value))];

    /// <summary>Whether something is on record at all.</summary>
    /// <param name="name">The record's name.</param>
    public bool Has(string name) => _records.Has(name);

    /// <summary>How many times something is on record, which is zero when it is not.</summary>
    /// <param name="name">The record's name.</param>
    public int CountOf(string name) => _records.ValueOf(name);

    /// <summary>Puts something on record once, leaving it as it is when it is already there.</summary>
    /// <param name="name">The record's name.</param>
    public void Mark(string name)
    {
        if (!_records.Has(name)) _records.Set(name, 1);
    }

    /// <summary>States how many times something is on record, replacing what was there.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="count">How many times, which must be at least once.</param>
    /// <exception cref="ArgumentException">The name is blank or the count is below one.</exception>
    public void Set(string name, int count) => _records.Set(name, count);

    /// <summary>Takes something off the record, which is how a record that stands for one place moves on.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>Whether it was on record.</returns>
    public bool Remove(string name) => _records.Remove(name);
}

/// <summary>What the party has deposited, account by account: a balance a counter keeps for it.</summary>
/// <remarks>
/// A balance is the party's own coin held elsewhere, so it is kept apart from everything that runs out: no clock
/// ends it, and ending every running effect leaves it exactly as it was. The one writer is the service mechanism's
/// deposit and withdrawal.
/// </remarks>
public sealed class PartyHoldings
{
    private readonly Tally _accounts;

    /// <summary>Creates the party's holdings.</summary>
    /// <param name="holdings">What is already deposited, account by account.</param>
    /// <exception cref="ArgumentException">An account is listed twice, is unnamed, or holds nothing.</exception>
    public PartyHoldings(IEnumerable<PartyHolding>? holdings = null)
    {
        _accounts = new Tally("account", minimum: 1);
        foreach (PartyHolding holding in holdings ?? []) _accounts.Add(holding.Account, holding.Coins, nameof(holdings));
    }

    /// <summary>Every account that holds something, in the order it was opened.</summary>
    public IReadOnlyList<PartyHolding> All => [.. _accounts.Entries.Select(entry => new PartyHolding(entry.Name, entry.Value))];

    /// <summary>What one account holds, which is zero for an account never opened or emptied.</summary>
    /// <param name="account">The account's name.</param>
    public int BalanceOf(string account) => _accounts.ValueOf(account);

    /// <summary>States what one account holds; an account left with nothing is closed rather than kept at zero.</summary>
    /// <param name="account">The account's name.</param>
    /// <param name="coins">What it now holds, which must not be negative.</param>
    /// <exception cref="ArgumentException">The name is blank or the balance is negative.</exception>
    public void Hold(string account, int coins)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        if (coins == 0) _accounts.Remove(account);
        else _accounts.Set(account, coins);
    }
}

/// <summary>The passages the party holds, one per destination: a seat bought at a counter and not yet taken.</summary>
/// <remarks>
/// A passage is honoured on the road and spent by boarding it, so it is the party's own state until the journey
/// is taken — kept apart from what runs out, so no clock ends it and no dispel takes it. The one writer is the
/// service mechanism's fare and the boarding that spends it.
/// </remarks>
public sealed class PartyPassages
{
    private readonly Tally _passages;

    /// <summary>Creates the party's passages.</summary>
    /// <param name="passages">The passages already held.</param>
    /// <exception cref="ArgumentException">A destination is listed twice or a passage takes no days.</exception>
    public PartyPassages(IEnumerable<PartyPassage>? passages = null)
    {
        _passages = new Tally("passage", minimum: 1);
        foreach (PartyPassage passage in passages ?? []) _passages.Add(passage.Destination.Value, passage.Days, nameof(passages));
    }

    /// <summary>Every passage held, in the order it was bought.</summary>
    public IReadOnlyList<PartyPassage> All => [.. _passages.Entries.Select(entry => new PartyPassage(new PlaceId(entry.Name), entry.Value))];

    /// <summary>How many days the passage to a place takes, or zero when the party holds none.</summary>
    /// <param name="destination">The place the passage reaches.</param>
    public int DaysTo(PlaceId destination) => _passages.ValueOf(destination.Value);

    /// <summary>Holds a passage to a place, replacing any passage there already was.</summary>
    /// <param name="destination">The place the passage reaches.</param>
    /// <param name="days">How many days the journey takes, which must be at least one.</param>
    public void Hold(PlaceId destination, int days) => _passages.Set(destination.Value, days);

    /// <summary>Spends the passage to a place.</summary>
    /// <param name="destination">The place the passage reaches.</param>
    /// <returns>Whether the party held one.</returns>
    public bool Spend(PlaceId destination) => _passages.Remove(destination.Value);
}

/// <summary>The memberships the party has been granted: the guilds and orders whose counters serve it.</summary>
/// <remarks>
/// A membership is granted once and kept, so it is neither a count nor something that runs out. The one writer is
/// the service mechanism's lesson that grants it; a counter reads it back to decide whom it serves.
/// </remarks>
public sealed class PartyMemberships
{
    private readonly Tally _memberships;

    /// <summary>Creates the party's memberships.</summary>
    /// <param name="memberships">The memberships already granted, in the order they were granted.</param>
    /// <exception cref="ArgumentException">A membership is listed twice or is unnamed.</exception>
    public PartyMemberships(IEnumerable<string>? memberships = null)
    {
        _memberships = new Tally("membership", minimum: 1);
        foreach (string membership in memberships ?? []) _memberships.Add(membership, 1, nameof(memberships));
    }

    /// <summary>Every membership granted, in the order it was granted.</summary>
    public IReadOnlyList<string> All => [.. _memberships.Entries.Select(entry => entry.Name)];

    /// <summary>Whether the party holds a membership.</summary>
    /// <param name="membership">The membership's name.</param>
    public bool Holds(string membership) => _memberships.Has(membership);

    /// <summary>Grants a membership, leaving it as it is when the party already holds it.</summary>
    /// <param name="membership">The membership's name.</param>
    public void Grant(string membership)
    {
        if (!_memberships.Has(membership)) _memberships.Set(membership, 1);
    }
}

/// <summary>Names and whole numbers in the order they were first stated, which each party-carried family keeps.</summary>
internal sealed class Tally(string what, int minimum)
{
    private readonly List<(string Name, int Value)> _entries = [];

    public IReadOnlyList<(string Name, int Value)> Entries => _entries;

    public bool Has(string name) => IndexOf(name) >= 0;

    public int ValueOf(string name)
    {
        int index = IndexOf(name);
        return index >= 0 ? _entries[index].Value : 0;
    }

    public void Add(string name, int value, string parameter)
    {
        if (Has(name))
        {
            throw new ArgumentException($"The {what} '{name}' is listed more than once, so which is meant would be ambiguous.", parameter);
        }

        Set(name, value);
    }

    public void Set(string name, int value)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"A {what} must be named.", nameof(name));
        if (value < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"A {what} is at least {minimum}; one with less is not held at all.");
        }

        int index = IndexOf(name);
        if (index >= 0) _entries[index] = (name, value);
        else _entries.Add((name, value));
    }

    public bool Remove(string name)
    {
        int index = IndexOf(name);
        if (index < 0) return false;
        _entries.RemoveAt(index);
        return true;
    }

    private int IndexOf(string name) => _entries.FindIndex(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
}
