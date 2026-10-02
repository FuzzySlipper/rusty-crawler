using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Conversation;

/// <summary>The game's reading of a companion the party already carries, without a world placement.</summary>
public interface IFollowerConversationRule
{
    /// <summary>The companion's authored name and portrait, or null when that identity is not declared.</summary>
    ConversationPerson? Follower(FollowerDefinitionId definition);
}
