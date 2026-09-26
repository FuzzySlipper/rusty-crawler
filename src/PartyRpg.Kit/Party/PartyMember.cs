using Rusty.Engine.Entities;

namespace PartyRpg.Kit.Party;

/// <summary>
/// One member of the party: a facade over the engine entity that carries everything about a character.
/// </summary>
/// <remarks>
/// <para>
/// The engine's <see cref="Actor"/> <i>is</i> the member. This type attaches no state
/// of its own and caches none: every named property reads the component the entity actually carries, so a
/// ruleset that reads a member reads the same class instance the party holds, and a ruleset that attaches a
/// component of its own to <see cref="Actor"/> extends this member rather than a copy of it.
/// </para>
/// <para>
/// <b>Equipment is the only item state a member has.</b> There is no pack, no carried list, and no member
/// property that returns an instance the member is not wearing: everything else the party owns lies in the
/// party's one shared pack, and only <see cref="PartyEntity"/> moves an instance between the two.
/// </para>
/// <para>
/// <b>Two identities, deliberately different.</b> <see cref="RuntimeId"/> is the engine's entity identity:
/// local to the store that holds this party, never reused by that store, and rebuilt on every load, so
/// nothing durable may name it. <see cref="Id"/> is the member's durable identity, minted once, written in
/// every reference inside the party, and the only one a save carries.
/// </para>
/// </remarks>
public sealed class PartyMember
{
    /// <summary>The kind of engine entity a party member is, which is fixed when the entity is created.</summary>
    public const string EntityKind = "party-member";

    private readonly Actor _actor;
    private readonly ICharacterHealthRule? _health;

    /// <summary>Wraps a member's entity, with the rule the member's own health obeys.</summary>
    /// <remarks>
    /// The rule is not state and no copy of anything: it is the answer this game gives about what a wound
    /// leaves on a character, held on the facade because a member is the one thing that knows both the harm
    /// that landed and the numbers it is judged against. A member built without one takes harm into its pool
    /// and no condition follows, which is the honest state of a product whose ruleset has not answered yet.
    /// </remarks>
    /// <param name="actor">The entity carrying everything about this character.</param>
    /// <param name="health">What this game makes of a wound, or null when it answers nothing.</param>
    internal PartyMember(Actor actor, ICharacterHealthRule? health = null)
    {
        _actor = actor;
        _health = health;
    }

    /// <summary>The engine actor for this member, through which components are attached and read.</summary>
    public Actor Actor => _actor;

    /// <summary>
    /// The member's runtime identity: the entity id inside the store that holds this party. It is per-visit,
    /// never reused by that store, and never part of a save.
    /// </summary>
    public EntityId RuntimeId => _actor.Entity;

    /// <summary>The member's durable identity, which a save round-trips and every reference names.</summary>
    public PartyMemberId Id => Profile.Id;

    /// <summary>Whether the member's entity is still alive in the store.</summary>
    public bool IsAlive => _actor.IsAlive;

    /// <summary>Who this member is: durable identity, name, race, and class.</summary>
    public CharacterProfile Profile => _actor.Get<CharacterProfile>();

    /// <summary>The member's attribute scores, named by the game's own attribute identities.</summary>
    public CharacterAttributes Attributes => _actor.Get<CharacterAttributes>();

    /// <summary>The member's skills, with the level and tier of each.</summary>
    public CharacterSkills Skills => _actor.Get<CharacterSkills>();

    /// <summary>The member's spellbook.</summary>
    public CharacterSpells Spells => _actor.Get<CharacterSpells>();

    /// <summary>
    /// The member's progression bookkeeping: experience, level, skill points, and class rank.
    /// </summary>
    /// <remarks>
    /// A reading. What moves these values is <see cref="Progression.PartyProgression"/>, the one owner of
    /// every award, every level, and every skill point spent, and the fields' own transitions are internal
    /// to the kit so that a second writer is a compile error rather than a review finding.
    /// </remarks>
    public CharacterProgression Progression => _actor.Get<CharacterProgression>();

    /// <summary>The conditions acting on the member.</summary>
    public CharacterConditions Conditions => _actor.Get<CharacterConditions>();

    /// <summary>What the member has left to spend and to lose.</summary>
    public CharacterResources Resources => _actor.Get<CharacterResources>();

    /// <summary>The member's equipped figure: the only item state the member owns.</summary>
    public CharacterEquipment Equipment => _actor.Get<CharacterEquipment>();

    /// <summary>
    /// Takes harm, and whatever this game's own answer makes of the wound.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a character's one damage entry.</b> Every way harm can reach a member — a creature's bite
    /// through a fight, a sprung trap, a fall — arrives here rather than at the pool directly, so the same
    /// wound leaves the same condition however it was taken and no caller has to remember the rule. What
    /// lands in the pool is the same number either way; what is added is the state that follows it, applied
    /// through the member's own conditions rather than as a silent change to a number.
    /// </para>
    /// <para>
    /// The pool's own <see cref="CharacterResources.TakeDamage"/> remains what it says it is: a pool
    /// operation that stops at empty, for a caller that is moving a number rather than wounding a person.
    /// </para>
    /// </remarks>
    /// <param name="amount">How much harm lands, which cannot be negative.</param>
    /// <returns>What the wound took, how far past empty it went, and what it left on the member.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The harm is negative, which would heal rather than wound.</exception>
    public CharacterWound TakeDamage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        Resources.TakeDamage(amount);

        // What the wound came to is read after the pool took it: the pool stops at empty, and how far past
        // empty the harm has now gone is the character's own running depth, so a second blow on someone
        // already down deepens the wound rather than starting it again.
        int after = Resources.HitPoints.Current;
        int deficit = Resources.Deficit;
        CharacterCollapse collapse = _health?.Collapse(this, after, deficit) ?? CharacterCollapse.None;
        foreach (ConditionId ended in collapse.Replaces) Conditions.Clear(ended);
        if (collapse.Condition is { } condition) Conditions.Apply(condition);
        return new CharacterWound(amount, after, deficit, collapse.Condition);
    }
}
