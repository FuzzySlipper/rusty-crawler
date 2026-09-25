namespace PartyRpg.Kit.Party;

/// <summary>Why a follower travels with the party, which decides whether a limit applies to them.</summary>
/// <remarks>
/// A game hires a small number of companions and also lets characters join for its own story reasons, and
/// the two are counted differently: the hired limit binds the first kind and never the second. Keeping the
/// distinction as a value means the limit is applied by the party rather than by whoever remembers to check
/// before adding.
/// </remarks>
public enum FollowerKind
{
    /// <summary>A companion the party hired, who counts against the hired limit.</summary>
    Hired,

    /// <summary>A character travelling with the party for the story's reasons, outside the hired limit.</summary>
    Story,
}
