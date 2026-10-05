using System.Globalization;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.World;

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
            case HandoffOwner.Use:
                Use(handoff, conversations);
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
            result.Describe(quests.Read(quest)?.Name));

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
    /// Runs what a topic set going as one use of the speaker's placement, puts what the run said into the conversation
    /// as the person's answer, and follows where it led.
    /// </summary>
    /// <remarks>
    /// The use is the world's (<see cref="SessionWorld.Answer"/>), so what it gives, pays and teaches is settled as any
    /// use's is, and what it taught is handed to the knowledge owner here as an aimed use's is. A use that took the
    /// party somewhere else ends the conversation — the person stayed behind — and one that called somebody over turns
    /// the conversation to them. A use that could not run is the person's answer too: its refusal is what they say,
    /// so the party is never left asking with nobody answering.
    /// </remarks>
    private void Use(ConversationHandoff handoff, PartyConversations conversations)
    {
        if (owners.World is not { } world || conversations.Placement is not { } placement)
        {
            conversations.Hear(
                "There is nothing more to it.",
                "What was said sets something going that this session holds no world to run it in.");
            return;
        }

        PlaceId spokenIn = conversations.Place;
        if (world.Answer(placement, handoff.Target) is not { } result)
        {
            conversations.Hear(
                "There is nothing more to it.",
                "What was said sets something going and this world has no interaction to run it with.");
            return;
        }

        if (result is { IsApplied: true, Learned.Count: > 0 } && owners.Knowledge is { } knowledge)
        {
            foreach (KnowledgeReport report in result.Learned) knowledge.Record(report);
        }

        conversations.Hear(result.Message, result.IsApplied ? result.Residue : string.Empty);
        if (world.Party.Place != spokenIn)
        {
            conversations.Close();
            return;
        }

        if (result is { IsApplied: true, Speaks: { } subject }) conversations.Open(spokenIn, placement, subject);
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
