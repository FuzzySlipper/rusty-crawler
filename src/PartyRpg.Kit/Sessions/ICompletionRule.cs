using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;

namespace PartyRpg.Kit.Sessions;

/// <summary>Reads an earned ending from canonical party state; it creates no second completion store.</summary>
public interface ICompletionRule
{
    /// <summary>The ending earned by this party, or the unfinished value.</summary>
    CompletionSnapshot Read(PartyEntity? party);
}
