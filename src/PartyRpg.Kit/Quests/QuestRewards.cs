namespace PartyRpg.Kit.Quests;

/// <summary>One kind of thing a turn-in hands the party: an item definition and how many of it.</summary>
/// <remarks>
/// The count is how many instances the party receives, and each arrives through the party's own acquisition
/// path, so what a quest pays is taken into the one inventory rather than placed beside it.
/// </remarks>
/// <param name="Item">The item definition's own identity.</param>
/// <param name="Count">How many of it the party receives, which is at least one.</param>
/// <exception cref="ArgumentException">The item's identity is blank, which names no definition.</exception>
/// <exception cref="ArgumentOutOfRangeException">The count is below one.</exception>
public readonly record struct QuestRewardItem
{
    /// <summary>Creates an item reward.</summary>
    /// <param name="item">The item definition's own identity.</param>
    /// <param name="count">How many of it the party receives.</param>
    /// <exception cref="ArgumentException">The item's identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The count is below one.</exception>
    public QuestRewardItem(string item, int count = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Item = item;
        Count = count;
    }

    /// <summary>The item definition's own identity.</summary>
    public string Item { get; }

    /// <summary>How many of it the party receives.</summary>
    public int Count { get; }
}

/// <summary>One record a turn-in leaves on the party: a flag's own identity and the magnitude it reaches.</summary>
/// <remarks>
/// A record is how a deed the party has done is carried, so a later rank, a person's topic, or a quest's own
/// completion condition can ask what the party has accomplished. It is applied through the party's durable
/// records owner, apart from timed effects. An accumulating reward adds on each distinct completed quest.
/// </remarks>
/// <param name="Record">The record's own identity.</param>
/// <param name="Amount">The magnitude the record reaches, which is at least one.</param>
/// <exception cref="ArgumentException">The record's identity is blank.</exception>
/// <exception cref="ArgumentOutOfRangeException">The amount is below one.</exception>
public readonly record struct QuestRewardRecord
{
    /// <summary>Creates a record reward.</summary>
    /// <param name="record">The record's own identity.</param>
    /// <param name="amount">The stated magnitude or earned count.</param>
    /// <param name="accumulate">Whether this adds a count instead of replacing a magnitude.</param>
    /// <exception cref="ArgumentException">The record's identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is below one.</exception>
    public QuestRewardRecord(string record, int amount = 1, bool accumulate = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(record);
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        Record = record;
        Amount = amount;
        Accumulate = accumulate;
    }

    /// <summary>The record's own identity.</summary>
    public string Record { get; }

    /// <summary>The magnitude the record reaches.</summary>
    public int Amount { get; }

    /// <summary>Whether the earning turn-in adds to a counted deed instead of stating a flag or magnitude.</summary>
    public bool Accumulate { get; }
}

/// <summary>What finishing a quest pays, and to which owner each part of it goes.</summary>
/// <remarks>
/// <para>
/// <b>Every part names the owner that already holds it.</b> Experience arrives at the progression owner's
/// one award entry, coin is credited through the party's one ledger, items are taken through the party's own
/// acquisition path, and records are applied to the party's durable records — the same four owners a kill, a sale, a
/// purchase, and a conversation already reach. Nothing here is a second copy of any of them.
/// </para>
/// <para>
/// <b>What a quest pays is content's or the ruleset's number.</b> This type carries the amounts and decides
/// none of them; how an award divides among the members, what it does to the party's standing, and what the
/// coin is worth are the rules the owners already ask.
/// </para>
/// <para>
/// A reward of nothing is legal and ordinary: a quest that pays only a record, or only what finishing it
/// means, states no experience and no coin rather than stating zeroes for them.
/// </para>
/// </remarks>
public sealed record QuestRewards
{
    /// <summary>Creates a reward.</summary>
    /// <param name="experience">How much experience the party earns, which cannot be negative.</param>
    /// <param name="coins">How much coin the party is paid, which cannot be negative.</param>
    /// <param name="items">What the party receives, in the order it is taken; empty when it receives nothing.</param>
    /// <param name="records">What the turn-in leaves on the party, in the order it is applied; empty when it leaves none.</param>
    /// <exception cref="ArgumentOutOfRangeException">An amount is negative.</exception>
    public QuestRewards(
        long experience = 0,
        int coins = 0,
        IReadOnlyList<QuestRewardItem>? items = null,
        IReadOnlyList<QuestRewardRecord>? records = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        Experience = experience;
        Coins = coins;
        Items = items ?? [];
        Records = records ?? [];
    }

    /// <summary>How much experience the party earns.</summary>
    public long Experience { get; }

    /// <summary>How much coin the party is paid.</summary>
    public int Coins { get; }

    /// <summary>What the party receives, in the order it is taken.</summary>
    public IReadOnlyList<QuestRewardItem> Items { get; }

    /// <summary>What the turn-in leaves on the party, in the order it is applied.</summary>
    public IReadOnlyList<QuestRewardRecord> Records { get; }

    /// <summary>A reward of nothing at all, which a quest that pays nothing states.</summary>
    public static QuestRewards None { get; } = new();

    /// <summary>Whether this pays nothing to anybody, which is what a quest that only leaves a record does.</summary>
    public bool IsNothing => Experience == 0 && Coins == 0 && Items.Count == 0 && Records.Count == 0;
}
