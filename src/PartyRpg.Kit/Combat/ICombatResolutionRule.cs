namespace PartyRpg.Kit.Combat;

/// <summary>
/// This game's answers about resolving an attack: how likely it is to land, what it rolls, what the target
/// resists, what a landed hit leaves behind, and what an actor's conditions leave it able to do.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mechanism is the kit's and every number here is the ruleset's.</b> The fight decides when an
/// attack happens, rolls the numbers this states, applies what comes out to whoever owns the target's health,
/// records what it did, and reports it. Nothing in the kit knows what a hit chance is made of, what dice a
/// fist rolls, which resistance a creature has, or which condition a bite leaves — a game answers all of
/// that through this seam, and a game that answers none of it fights without resolving anything.
/// </para>
/// <para>
/// <b>It is a separate seam from <see cref="ICombatRule"/>, and optional.</b> Pacing a fight and resolving
/// one are different answers, and a product may hold the first without the second: the fight stills paces
/// actors, still refuses an order while one is recovering, and simply records that an attack was attempted
/// and nothing came of it. That is why a fight asks whether the rule it was handed also answers resolution
/// rather than requiring every ruleset to state damage it does not have yet.
/// </para>
/// <para>
/// <b>Conditions are asked about, never assumed.</b> Whether an actor may act, and what a wound leaves on a
/// character, are the ruleset's readings of its own condition definitions, so the kit applies and reports
/// conditions without learning a single one of their names.
/// </para>
/// </remarks>
public interface ICombatResolutionRule
{
    /// <summary>
    /// Where one attack's rolls come from, or null when this product cannot draw.
    /// </summary>
    /// <remarks>
    /// The ruleset holds the engine's random service, so it is what prepares an attack's rolls: the same seed
    /// and the same key produce the same values, and the key is the attack's own name inside the fight. A
    /// product whose composition reached no random service answers null, and the fight then resolves nothing
    /// rather than resolving against an invented value.
    /// </remarks>
    /// <param name="attacker">The actor making the attack.</param>
    /// <param name="key">What this attack is called inside the fight, which must not be blank.</param>
    /// <returns>The attack's rolls, or null when nothing can be drawn.</returns>
    IAttackRolls? RollsFor(CombatSubject attacker, string key);

    /// <summary>What this attack against this target is worth before anything is rolled.</summary>
    /// <param name="attacker">The actor making the attack.</param>
    /// <param name="target">The actor it is aimed at.</param>
    /// <param name="kind">How the attack is made.</param>
    /// <returns>The chance, the damage, the kind of harm, and what the target resists.</returns>
    AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind);

    /// <summary>What is left of a landed hit once the target's resistance has had its say.</summary>
    /// <remarks>
    /// The target's resistance was read in <see cref="PlanOf"/>; this turns it into arithmetic, which is
    /// where a game's own resistance rules live. An immune target answers zero here whatever the damage was,
    /// and the fight reports that as immunity rather than as a number that happened to be small.
    /// </remarks>
    /// <param name="target">The actor being hit.</param>
    /// <param name="kind">What kind of harm the hit does.</param>
    /// <param name="damage">What the hit rolled, before resistance.</param>
    /// <param name="rolls">The attack's rolls, for a resistance that is checked rather than certain.</param>
    /// <returns>The harm that lands, which cannot be negative.</returns>
    int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls);

    /// <summary>What a landed hit leaves on its target besides harm, or null when it leaves nothing.</summary>
    /// <remarks>
    /// A bite that poisons, a gaze that petrifies, a touch that curses: the ruleset reads the attacker's own
    /// nature and the target's own defences, and answers with the condition and its source or with nothing.
    /// Whether the target is a party member matters here — a game's conditions may be things only characters
    /// suffer — and the ruleset can see which it is from the subject it was handed.
    /// </remarks>
    /// <param name="attacker">The actor that landed the hit.</param>
    /// <param name="target">The actor that took it.</param>
    /// <param name="kind">What kind of harm the hit did.</param>
    /// <param name="rolls">The attack's rolls, for a chance and a saving throw.</param>
    /// <returns>The condition the hit leaves, or null.</returns>
    CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls);

    /// <summary>Whether this actor's conditions leave it able to act at all.</summary>
    /// <remarks>
    /// The fight's gate is the same for everybody: an actor whose recovery has not elapsed cannot act, and an
    /// actor its conditions have laid out cannot either. Asking here rather than inside the fight is what
    /// keeps a condition's effect the ruleset's answer — a game may decide that sleep, paralysis, and death
    /// stop an actor while fear only makes it worse at fighting.
    /// </remarks>
    /// <param name="subject">The actor about to act.</param>
    /// <returns>Whether it may act.</returns>
    bool CanAct(CombatSubject subject);

    /// <summary>How much harm an actor can take before it is down.</summary>
    /// <remarks>
    /// The party owns its members' pools, so for a member this is the pool's capacity and the fight reads the
    /// pool itself. A world actor's health has no owner until the monsters-and-AI stone gives creatures their
    /// own, so the fight holds what it has done to one and this states what that is measured against — a
    /// monster row's own hit points for a creature, and this game's reading for anything else that can be
    /// attacked. It is the handoff that stone fills in, not a second health store beside its own.
    /// </remarks>
    /// <param name="subject">The actor being measured.</param>
    /// <returns>The total harm it can take, zero when nothing here can say.</returns>
    int HitPointsOf(CombatSubject subject);
}
