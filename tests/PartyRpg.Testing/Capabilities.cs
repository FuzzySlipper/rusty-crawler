using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Testing;

/// <summary>
/// Composes a suite's fakes into the named capabilities the kit's mechanisms take.
/// </summary>
/// <remarks>
/// A product names each capability where it composes a mechanism; a suite's fake usually answers several of them
/// in one small class, and states which by the interfaces it implements. This reads that statement once, here,
/// so a suite composes a fight or a casting with exactly what its fake says it answers.
/// </remarks>
public static class Capabilities
{
    /// <summary>A fight over a fake's answers, with every capability the fake implements.</summary>
    public static CombatRules Combat(ICombatRule rule, IMonsterAiPolicy? ai = null) =>
        new(
            rule,
            ai,
            rule as ICombatResolutionRule,
            rule as ICombatAbilityResolutionRule,
            rule as ICombatWeaponRule,
            rule is ICreatureDeathObserver deaths ? [deaths] : null);

    /// <summary>What a suite's creature can take, as its own placement states it under <c>hitPoints</c>.</summary>
    public static ICreatureVitals PlacementHitPoints { get; } = new HitPointsInPlacement();

    /// <summary>A casting over a fake's answers, with every capability the two fakes implement.</summary>
    public static MagicRules Magic(ISpellRule spells, ISpellEffectRule? effects = null) =>
        new(
            spells,
            effects,
            effects as IRunningSpellEffects,
            effects as IGameTimeObserver,
            effects as ISpellAimRule,
            effects as IMemberSpellEffects,
            effects as IPartySightRule,
            spells as ISpellItemRule);

    private sealed class HitPointsInPlacement : ICreatureVitals
    {
        public int HitPointsOf(CombatSubject subject) => subject.Member is { } member
            ? member.Resources.HitPoints.Maximum
            : subject.Placement?.Source.GetInt32("hitPoints") ?? 0;
    }
}
