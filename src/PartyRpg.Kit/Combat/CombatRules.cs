using PartyRpg.Kit.Persistence;

namespace PartyRpg.Kit.Combat;

/// <summary>A game's answers about fighting, each capability named where it is composed.</summary>
/// <remarks>
/// A fight is composed over the capabilities a game states and over nothing it would have to discover: a rule
/// that resolves attacks is handed over as the resolution, not found by asking the pacing rule what else it is.
/// A capability left out is one the fight does without — pacing with no resolution records every attack as an
/// attempt that came to nothing — so a wrapper around a rule cannot silently drop one it did not forward.
/// </remarks>
/// <param name="Rule">What each actor is worth in recovery, what hostility means, and what an attack reaches.</param>
/// <param name="Ai">How a creature decides, or null for a fight whose other side never acts.</param>
/// <param name="Resolution">What an attack does when it lands, and whether an actor may act at all.</param>
/// <param name="Abilities">What a named ability does, beside the ordinary attack the resolution answers for.</param>
/// <param name="Weapons">What each character wields, which decides the kind of its attack.</param>
/// <param name="Deaths">Who hears about each creature's death, once, in the order named.</param>
/// <param name="Reflection">What a wound turns back onto whoever dealt it, or null for a game whose wounds turn nothing back.</param>
/// <param name="Saving">The content-only judge for restoring a fight, when the game supplies one.</param>
/// <param name="Hits">Who hears each landed hit after its canonical health settlement.</param>
/// <param name="Corpses">The canonical body and held-loot owner.</param>
/// <param name="Provocation">Who else an act against one creature turns against the party, or null for a game where it turns only that creature.</param>
public sealed record CombatRules(
    ICombatRule Rule,
    IMonsterAiPolicy? Ai = null,
    ICombatResolutionRule? Resolution = null,
    ICombatAbilityResolutionRule? Abilities = null,
    ICombatWeaponRule? Weapons = null,
    IReadOnlyList<ICreatureDeathObserver>? Deaths = null,
    ICombatReflectionRule? Reflection = null,
    ICombatProvocationRule? Provocation = null,
    ICombatSaveRule? Saving = null,
    CorpseGround? Corpses = null,
    IReadOnlyList<ICombatHitObserver>? Hits = null);
