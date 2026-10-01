using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Testing;

/// <summary>Session snapshots a projection case starts from, so each case states only the blocks it is about.</summary>
public static class SessionSnapshots
{
    /// <summary>
    /// A session that holds no mechanism at all: every block is its own no-mechanism value and no key is bound,
    /// which is what a session whose ruleset composed nothing would read from its owners.
    /// </summary>
    /// <param name="composition">The composition the session runs.</param>
    /// <param name="mode">The session's mode.</param>
    /// <param name="world">Where the party is, or <see cref="WorldSnapshot.Empty"/>.</param>
    /// <param name="simulationSeconds">Admitted simulation time accumulated while running.</param>
    /// <param name="admittedSteps">Admitted fixed steps accumulated while running.</param>
    /// <returns>The snapshot, for a case to replace the blocks it is about with <c>with</c>.</returns>
    public static SessionSnapshot Bare(
        SessionComposition composition,
        SessionMode mode,
        WorldSnapshot world,
        double simulationSeconds = 0,
        ulong admittedSteps = 0) => new(
        composition,
        mode,
        simulationSeconds,
        admittedSteps,
        world,
        MovementSnapshot.None,
        ClockSnapshot.None,
        PartySnapshot.None,
        CreationSnapshot.None,
        SaveSnapshot.None(available: false, resumed: false, slot: string.Empty),
        InteractionSnapshot.None,
        ServiceSnapshot.None,
        RestSnapshot.None,
        ConversationSnapshot.None,
        CombatSnapshot.None,
        ProgressionSnapshot.None,
        PromotionSnapshot.None,
        SkillsSnapshot.None,
        PartyRpg.Kit.Magic.MagicSnapshot.None,
        AlchemySnapshot.None,
        QuestSnapshot.None,
        JournalSnapshot.None,
        MapSnapshot.None,
        ControlKeys.None);
}
