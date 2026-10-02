using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;

namespace PartyRpg.Kit.Quests;

/// <summary>Which operation a quest result reports.</summary>
/// <remarks>
/// The three operations are the whole of what a player does with a quest, and they are one vocabulary rather
/// than three because they share one shape: an errand's identity, what the party now stands at, and either
/// what happened or why nothing did.
/// </remarks>
public enum QuestAction
{
    /// <summary>The party heard the errand and the offer was recorded.</summary>
    Offer,

    /// <summary>The party agreed to the errand.</summary>
    Accept,

    /// <summary>The party finished the errand and was paid for it.</summary>
    TurnIn,
}

/// <summary>What a turn-in paid, and to which owner each part of it went.</summary>
/// <remarks>
/// Each part is the owner's own answer rather than a copy of the reward: the award is the progression
/// owner's result, what the purse holds is the party's own reading, and the items named are the instances
/// the acquisition path actually took. A part that could not be paid would have refused the turn-in whole
/// before anything moved, so everything here landed.
/// </remarks>
/// <param name="Experience">What the award entry did, or null when the quest paid no experience.</param>
/// <param name="Coins">How much coin the ledger credited.</param>
/// <param name="Items">What the acquisition path took, one entry per reward line.</param>
/// <param name="Records">What was applied to the party's durable records, in the order it was applied.</param>
/// <param name="Delivered">What the party handed over, one entry per item a delivery objective asked for.</param>
public sealed record QuestPayment(
    ProgressionAwardResult? Experience,
    int Coins,
    IReadOnlyList<QuestRewardItem> Items,
    IReadOnlyList<QuestRewardRecord> Records,
    IReadOnlyList<QuestRewardItem> Delivered)
{
    /// <summary>The found reward's portion paid away before Coins entered the purse.</summary>
    public int GoldShare { get; init; }
    /// <summary>A payment of nothing, which a refused or unremarkable operation carries.</summary>
    public static QuestPayment None { get; } = new(null, 0, [], [], []);

    /// <summary>Whether anything at all moved.</summary>
    public bool IsAnything =>
        Experience is not null || Coins > 0 || Items.Count > 0 || Records.Count > 0 || Delivered.Count > 0;
}

/// <summary>What one quest operation did: where the party stands, what it paid, or why nothing happened.</summary>
/// <remarks>
/// <para>
/// A refused operation is a result rather than an exception for the same reason a refused service command
/// is: an errand nobody offered, one that is already finished, and one whose objectives are unmet are
/// ordinary states a caller reports, and a player acts on them differently — which is why each refusal
/// carries its own code and a sentence that names what was missing.
/// </para>
/// <para>
/// <b>A refusal carries no payment.</b> Everything a turn-in would move is judged before anything moves, so
/// a caller cannot read a half-paid errand out of a refusal.
/// </para>
/// </remarks>
/// <param name="Action">Which operation this reports.</param>
/// <param name="Quest">The quest's identity, or an unset one when the operation named none.</param>
/// <param name="Stage">Where the party stands with it, or null when there is no instance.</param>
/// <param name="Payment">What the operation paid, or nothing when it paid nothing or was refused.</param>
/// <param name="Refusal">Why nothing happened, or null when it did.</param>
public sealed record QuestResult(
    QuestAction Action,
    QuestId Quest,
    QuestStage? Stage,
    QuestPayment Payment,
    Refusal? Refusal)
{
    /// <summary>Whether the operation happened.</summary>
    public bool IsApplied => Refusal is null;

    /// <summary>An operation that happened.</summary>
    /// <param name="action">Which operation it was.</param>
    /// <param name="quest">The quest's identity.</param>
    /// <param name="stage">Where the party now stands with it.</param>
    /// <param name="payment">What it paid, or nothing.</param>
    /// <returns>The result.</returns>
    public static QuestResult Applied(
        QuestAction action,
        QuestId quest,
        QuestStage stage,
        QuestPayment? payment = null) => new(action, quest, stage, payment ?? QuestPayment.None, Refusal: null);

    /// <summary>An operation that did nothing, with the party exactly where it stood.</summary>
    /// <param name="action">Which operation was asked for.</param>
    /// <param name="quest">The quest's identity, or an unset one when none was named.</param>
    /// <param name="refusal">Why nothing happened.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    public static QuestResult Refused(QuestAction action, QuestId quest, Refusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new QuestResult(action, quest, Stage: null, QuestPayment.None, refusal);
    }

    /// <summary>How this reads to a person, in one sentence.</summary>
    /// <returns>The sentence.</returns>
    public string Describe()
    {
        if (Refusal is not null) return Refusal.Message;
        return Action switch
        {
            QuestAction.Offer => $"The errand '{Quest}' was offered and is in the party's journal.",
            QuestAction.Accept => $"The errand '{Quest}' was taken.",
            _ => $"The errand '{Quest}' was finished." + (Payment.GoldShare > 0 ? $" Companions take {Payment.GoldShare} of the reward; the party keeps {Payment.Coins}." : string.Empty),
        };
    }

    /// <inheritdoc />
    public override string ToString() => $"{Action} {Quest}: {(Refusal is null ? Stage?.ToString() ?? "applied" : Refusal.Code)}";
}

/// <summary>One quest that needs an item the party is about to part with.</summary>
/// <remarks>
/// This is what a refusal names: the item's owner reports that something needs it, and the quest and the
/// objective that need it are named so a player hears which errand they would be breaking rather than only
/// that the sale was refused.
/// </remarks>
/// <param name="Quest">The quest's identity.</param>
/// <param name="Name">What a person calls the quest.</param>
/// <param name="Objective">What the quest asks for, as it reads.</param>
public readonly record struct QuestNeed(QuestId Quest, string Name, string Objective)
{
    /// <summary>How it reads inside a refusal.</summary>
    public string Statement => $"{Objective} for '{Name}'";

    /// <inheritdoc />
    public override string ToString() => Statement;
}
