namespace PartyRpg.Kit.Services;

/// <summary>What one command did at a service, or why it did nothing, as a report a panel can show.</summary>
/// <remarks>
/// <para>
/// A result always says what was asked and what came of it, whether it happened: a refusal changes nothing,
/// so a caller can read the result unconditionally and never has to guess a shelf's state from the branch
/// it took. It carries the coins that moved — what the party paid and what it was paid — because "the purse
/// actually moved" is the fact a player checks, and a report that only said "bought" would leave a silent
/// no-op looking exactly like a transaction.
/// </para>
/// <para>
/// A refusal is never silence. It carries the code a caller branches on — the counter is shut, the party is
/// not a member, the shelves are out of it, the purse is short, there is no such item — and the sentence a
/// person reads.
/// </para>
/// </remarks>
public sealed record ServiceResult
{
    private ServiceResult(bool isApplied, string action, string code, string message, string subject, int paid, int earned, int coins)
    {
        IsApplied = isApplied;
        Action = action;
        Code = code;
        Message = message;
        Subject = subject;
        Paid = paid;
        Earned = earned;
        Coins = coins;
    }

    /// <summary>The command happened, and this is what it did.</summary>
    /// <param name="action">The word for what happened: opened, bought, sold, identified, repaired, taught, left.</param>
    /// <param name="message">What happened, in the words a person reads.</param>
    /// <param name="subject">
    /// What the command acted on, as the identity a command naming it names, or empty for a command that acted
    /// on nothing a caller could name again — opening and leaving a counter.
    /// </param>
    /// <param name="paid">How many coins the party paid, zero when it paid none.</param>
    /// <param name="earned">How many coins the party was paid, zero when it was paid none.</param>
    /// <param name="coins">What the party's one purse holds afterwards.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">The action or the message is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An amount is negative.</exception>
    public static ServiceResult Applied(string action, string message, string subject = "", int paid = 0, int earned = 0, int coins = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentOutOfRangeException.ThrowIfNegative(paid);
        ArgumentOutOfRangeException.ThrowIfNegative(earned);
        return new ServiceResult(true, action, string.Empty, message, subject, paid, earned, coins);
    }

    /// <summary>The command changed nothing, and this is why.</summary>
    /// <param name="action">The word for what was asked and refused.</param>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why nothing happened, in terms a person can act on.</param>
    /// <param name="coins">What the party's one purse holds, which a refusal leaves untouched.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">The action, the code, or the message is blank.</exception>
    public static ServiceResult Refused(string action, string code, string message, int coins = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ServiceResult(false, action, code, message, string.Empty, 0, 0, coins);
    }

    /// <summary>Whether the command happened. A refusal changed nothing at all.</summary>
    public bool IsApplied { get; }

    /// <summary>The word for what happened, or for what was refused.</summary>
    public string Action { get; }

    /// <summary>The refusal's code, or empty when the command happened.</summary>
    public string Code { get; }

    /// <summary>What the command did, or why it did nothing.</summary>
    public string Message { get; }

    /// <summary>
    /// What the command acted on, as the identity a command naming it names, or empty when it acted on
    /// nothing nameable.
    /// </summary>
    /// <remarks>
    /// A refusal acted on nothing, so it states nothing here. An applied command states the thing it changed
    /// in content's own words — the place a passage reaches, the lot a purchase took, the instance the party
    /// sold — which is what lets a caller that must go on to another owner, as the counter's passage goes on
    /// to the road, name the same thing without keeping its own copy of the screen's request.
    /// </remarks>
    public string Subject { get; }

    /// <summary>How many coins the party paid.</summary>
    public int Paid { get; }

    /// <summary>How many coins the party was paid.</summary>
    public int Earned { get; }

    /// <summary>What the party's one purse holds after the command.</summary>
    public int Coins { get; }

    /// <inheritdoc />
    public override string ToString() => IsApplied ? $"{Action}: {Message}" : $"{Action} refused ({Code}): {Message}";
}
