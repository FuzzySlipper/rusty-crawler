namespace PartyRpg.Kit.Quests;

/// <summary>The owner a quest offer, its acceptance, and its turn-in are handed to, as a conversation's words.</summary>
/// <remarks>
/// <para>
/// A person who has an errand does not record it themselves: they offer it, and the mechanism that owns
/// quest state takes it from there. That is what a handoff is — a topic naming the owner it belongs to and
/// the quest it means — so a conversation never grows a second copy of what the quest owner already does.
/// </para>
/// <para>
/// <b>Three words rather than one, because hearing an errand and agreeing to it are different facts.</b>
/// The original states a quest and then sets its bit when the party agrees; a journal that could not show
/// an errand the party was offered and walked away from would lose half of what a player was told. Each
/// word names one operation of the owner, so a topic that offers carries the offer word and a topic that
/// agrees carries the acceptance word.
/// </para>
/// <para>
/// The words are named here rather than in the conversation's own list of kinds for the same reason a
/// promotion's is: the words belong to the owner, and the conversation names the owners it knows about.
/// </para>
/// </remarks>
public static class QuestHandoffs
{
    /// <summary>The quest owner, taking an errand a person stated.</summary>
    public const string Offer = "quest-offer";

    /// <summary>The quest owner, taking the party's agreement to an errand it was offered.</summary>
    public const string Accept = "quest-accept";

    /// <summary>The quest owner, taking an errand the party has finished back to its giver.</summary>
    public const string TurnIn = "quest-turn-in";
}
