using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Presentation;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// Keeps the answer to the party's latest act: it watches each owner's own last result and takes whichever is new.
/// </summary>
/// <remarks>
/// <para>
/// Each owner replaces its last result with a new one when it answers an act, so a result that is not the one seen
/// last is an answer given since. The owners are read in the order their answers outrank one another when one act
/// produces two — a casting resolves through the fight, so the casting is the answer and its blow is not — and every
/// result read is remembered, so an answer is taken once.
/// </para>
/// <para>
/// The fight retains its latest party order separately from opposition retaliation, so a creature acting later
/// in the same update cannot hide the member's answer. The save state is replaced on every attempt and watched the same way.
/// </para>
/// <para>
/// Errands and growth are read last: a counter's training, a word that finishes an errand and a blow that earns
/// experience each answer through their own owner first, and the errand or growth owner answers alone only when
/// it is what was asked — a raise or a rank from the character book, or an errand settled outside a conversation.
/// </para>
/// </remarks>
internal sealed class ActionFeedback
{
    private object?[] _seen = [];
    private bool _primed;

    /// <summary>The latest answer.</summary>
    public FeedbackSnapshot Current { get; private set; } = FeedbackSnapshot.None;

    /// <summary>Reads every owner's last result and takes the first new one, in the order answers outrank one another.</summary>
    /// <param name="owners">The session's owners.</param>
    /// <param name="save">The session's save state, replaced on every attempt.</param>
    /// <returns>The latest answer.</returns>
    public FeedbackSnapshot Observe(SessionOwners owners, SaveSnapshot save)
    {
        object?[] now =
        [
            owners.Casting?.Last,
            owners.Combat?.LastPartyOrder,
            owners.World?.Interaction?.LastResult,
            owners.Rest?.Last,
            save.State == SaveState.Never ? null : save,
            owners.Services?.Last,
            owners.Conversations?.Last,
            owners.ItemUses?.Last,
            owners.Mixing?.Last,
            owners.Outfitting?.Last,
            owners.Quests?.Last,
            owners.Progression?.LastPromotion,
            owners.Progression?.LastTraining,
            owners.Progression?.LastRaise,
            owners.Progression?.LastAward,
        ];

        // What the owners held when the watch began answered nothing the player did since.
        if (!_primed)
        {
            _primed = true;
            _seen = now;
            return Current;
        }

        FeedbackSnapshot? taken = null;
        for (int index = 0; index < now.Length; index++)
        {
            if (now[index] is not { } result || ReferenceEquals(result, _seen[index])) continue;

            taken ??= Read(owners, result, Current.Serial + 1);
        }

        _seen = now;
        if (taken is not null) Current = taken;
        return Current;
    }

    /// <summary>One owner's result as the answer it gives.</summary>
    private static FeedbackSnapshot? Read(SessionOwners owners, object result, long serial) => result switch
    {
        Magic.SpellCastResult cast => Of(serial, FeedbackSources.Cast, cast.Caster, cast.SpellName, cast.Code, cast.Message),
        CombatResult attack => Of(serial, FeedbackSources.Attack, attack.ActorName, attack.Initiated?.TargetName ?? string.Empty, attack.Code, attack.Message),
        Interaction.InteractionResult use => Of(serial, FeedbackSources.Use, string.Empty, use.TargetName, use.Code, use.Message),
        Time.RestResult stop => Of(serial, FeedbackSources.Stop, string.Empty, SessionProjection.WireName(stop.Kind), stop.Code, stop.Message),
        SaveSnapshot saved => Of(serial, FeedbackSources.Save, string.Empty, saved.Slot, saved.Code, saved.Message, refused: saved.IsFailed),
        Services.ServiceResult counter => Of(serial, FeedbackSources.Counter, string.Empty, counter.Subject, counter.Code, counter.Message),
        Conversation.ConversationResult said => Of(serial, FeedbackSources.Conversation, string.Empty, said.Speaker, said.Code, said.Message),
        Party.ItemUseResult item => Of(serial, FeedbackSources.Item, string.Empty, string.Empty, item.Applied ? string.Empty : item.Code, item.Message, refused: !item.Applied),
        Alchemy.MixingResult mixed => Of(serial, FeedbackSources.Mix, mixed.Mixer, mixed.ResultName, mixed.Code, mixed.Message),
        Party.OutfittingResult outfit => Of(serial, FeedbackSources.Equip, outfit.Wearer, outfit.ItemName, outfit.Code, outfit.Message, refused: !outfit.Changed),
        Quests.QuestResult quest => Of(serial, FeedbackSources.Quest, string.Empty, owners.Quests?.Read(quest.Quest)?.Name ?? quest.Quest.Value, quest.Refusal?.Code ?? string.Empty, quest.Describe(owners.Quests?.Read(quest.Quest)?.Name)),
        Promotion.PromotionResult rank => Of(serial, FeedbackSources.Promotion, string.Empty, rank.ToClass, rank.Refusal?.Code ?? string.Empty, rank.Describe(), refused: !rank.IsGranted),
        Progression.ProgressionTrainingResult trained => Of(serial, FeedbackSources.Training, trained.Name, trained.Counter, trained.Refusal?.Code ?? string.Empty, trained.Describe()),
        Progression.SkillRaiseResult raise => Of(serial, FeedbackSources.Raise, raise.Name, raise.Skill.Value, raise.Refusal?.Code ?? string.Empty, raise.Describe()),
        Progression.ProgressionAwardResult award => Of(serial, FeedbackSources.Award, string.Empty, award.Source, award.Refusal?.Code ?? string.Empty, award.Describe()),
        _ => null,
    };

    private static FeedbackSnapshot Of(long serial, string source, string actor, string subject, string code, string message, bool? refused = null) =>
        new(serial, source, actor, subject, (refused ?? code.Length > 0) ? "refused" : "applied", code, message);
}
