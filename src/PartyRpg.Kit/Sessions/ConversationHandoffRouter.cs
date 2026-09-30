using System.Globalization;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// Hands the party from a conversation to the owner an offer belongs to, and reports what became of it.
/// </summary>
/// <remarks>
/// <para>
/// A conversation names the owner an offer belongs to and never carries it out itself; the session holds both,
/// so this is the one place that hands the party over. Every owner in <see cref="HandoffOwner"/> is routed, and
/// an owner the session did not compose — a game that states no ranks, no quests, or no counters — is reported
/// by name rather than treated as done.
/// </para>
/// <para>
/// <b>Business finished closes the conversation; a refusal leaves it open.</b> A counter that opened takes the
/// controls, a rank granted and an errand handed in end the business the party was about, and a refusal stands
/// beside the person who gave it so the party can hear what was missing.
/// </para>
/// </remarks>
internal sealed class ConversationHandoffRouter(SessionOwners owners)
{
    /// <summary>Routes one handoff an answer made.</summary>
    public void Route(ConversationHandoff handoff, PartyConversations conversations)
    {
        switch (handoff.Owner)
        {
            case HandoffOwner.Counter:
                Serve(conversations);
                break;
            case HandoffOwner.Rank:
                Give(handoff, conversations);
                break;
            case HandoffOwner.ErrandOffer or HandoffOwner.ErrandAccept or HandoffOwner.ErrandTurnIn:
                Take(handoff, conversations);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(handoff), handoff.Owner, "No owner routes this handoff.");
        }
    }

    /// <summary>
    /// Opens the counter the person keeps, through the service mechanism's own entry, so a shut shop refuses in
    /// its own words rather than through a second copy of its hours.
    /// </summary>
    private void Serve(PartyConversations conversations)
    {
        if (owners.Services is not { } services || conversations.Placement is not { } placement)
        {
            owners.Diagnostics.Refused(
                "conversation",
                "conversation-handoff-unavailable",
                "What was said offers a counter and this session holds no service mechanism to hand the party to.");
            return;
        }

        ServiceResult? opened = services.OpenTarget(conversations.Place, placement);
        if (opened is { IsApplied: true }) conversations.Close();
    }

    /// <summary>
    /// Offers, agrees to, or hands in an errand through the quest owner, which judges the giver again: a
    /// conversation that offered somebody else's errand is refused by name rather than quietly recorded.
    /// </summary>
    private void Take(ConversationHandoff handoff, PartyConversations conversations)
    {
        if (owners.Quests is not { } quests)
        {
            owners.Diagnostics.Refused(
                "quest",
                "quest-unavailable",
                "What was said offers an errand and this session holds no owner of quest state: the ruleset that states one is not composed.");
            return;
        }

        string person = conversations.Speaker?.Id ?? string.Empty;
        QuestId quest = new(handoff.Target);
        QuestResult result = handoff.Owner switch
        {
            HandoffOwner.ErrandOffer => quests.Offer(quest, person, conversations.Place),
            HandoffOwner.ErrandAccept => quests.Accept(quest),
            _ => quests.TurnIn(quest, person),
        };

        owners.Diagnostics.Report(
            result.IsApplied,
            "quest",
            result.IsApplied ? $"quest-{result.Action.ToString().ToLowerInvariant()}" : result.Refusal!.Code,
            result.Describe());

        // What the owner did is written down where its own answer arrives, named in the owner's own words; a
        // refusal writes nothing, because nothing happened.
        if (result.IsApplied && result.Stage is { } stage && quests.Read(quest) is { } reading)
        {
            owners.Journal?.Record(new JournalEvent(
                stage switch
                {
                    QuestStage.Offered => JournalEntryKind.QuestOffered,
                    QuestStage.Accepted => JournalEntryKind.QuestTaken,
                    _ => JournalEntryKind.QuestFinished,
                },
                Source: "quest",
                Subject: quest.Value,
                Name: reading.Name,
                Place: conversations.Place.Value));
        }

        if (result is { IsApplied: true, Action: QuestAction.TurnIn }) conversations.Close();
    }

    /// <summary>
    /// Gives the rank a person offered through the progression owner, judging the ladder's giver against the
    /// person being spoken with.
    /// </summary>
    private void Give(ConversationHandoff handoff, PartyConversations conversations)
    {
        if (owners.Progression is not { Promotions: not null } progression)
        {
            owners.Diagnostics.Refused(
                "promotion",
                "promotion-unavailable",
                "What was said offers a rank and this session holds no ladder of them to give: the ruleset that states one is not composed.");
            return;
        }

        PromotionResult result = progression.Promote(handoff.Target, conversations.Speaker?.Id ?? string.Empty);
        owners.Diagnostics.Report(
            result.IsGranted,
            "promotion",
            result.IsGranted ? "promotion-granted" : result.Refusal!.Code,
            result.IsGranted
                ? string.Join(" ", result.Granted.Select(grant =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{grant.Name} rose from {grant.FromClass} (rank {grant.FromRank}) to {grant.ToClass} (rank {grant.Rank}) by the rank '{result.Promotion}'.")))
                : result.Refusal!.Message);

        // A rank that landed is one moment in the party's record, named by the class it reached.
        if (!result.IsGranted) return;
        owners.Journal?.Record(new JournalEvent(
            JournalEntryKind.Rank,
            Source: "progression",
            Subject: result.Promotion,
            Name: result.ToClass,
            Place: conversations.Place.Value));
        conversations.Close();
    }
}
