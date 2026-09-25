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

    internal PartyMember(Actor actor) => _actor = actor;

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

    /// <summary>The member's progression bookkeeping: experience, level, skill points, and class rank.</summary>
    public CharacterProgression Progression => _actor.Get<CharacterProgression>();

    /// <summary>The conditions acting on the member.</summary>
    public CharacterConditions Conditions => _actor.Get<CharacterConditions>();

    /// <summary>What the member has left to spend and to lose.</summary>
    public CharacterResources Resources => _actor.Get<CharacterResources>();

    /// <summary>The member's equipped figure: the only item state the member owns.</summary>
    public CharacterEquipment Equipment => _actor.Get<CharacterEquipment>();

    /// <summary>
    /// Raises a learned skill and charges the skill points the caller computed, in one operation.
    /// </summary>
    /// <remarks>
    /// The cost of a level and the ceiling a class and rank impose are the ruleset's formulas, so the caller
    /// brings them; what happens here is that the points leave the progression pool and the raise lands on
    /// the skill together, which is why the two cannot drift into a character who paid for nothing or
    /// gained for free.
    /// </remarks>
    /// <param name="skill">The skill to raise, which the member must already have learned.</param>
    /// <param name="levels">How many levels to add, which must be at least one.</param>
    /// <param name="points">How many skill points the raise costs, which cannot be negative.</param>
    /// <returns>A refusal when the pool cannot pay, or null when the raise landed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The levels are below one or the points are negative.</exception>
    /// <exception cref="InvalidOperationException">The member has not learned the skill.</exception>
    public PartyRefusal? RaiseSkill(SkillId skill, int levels, int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);
        ArgumentOutOfRangeException.ThrowIfNegative(points);

        // Everything that can fail is settled before the pool is charged: a raise that cost points and then
        // failed would take a character's skill points for nothing.
        if (!Skills.Knows(skill))
        {
            throw new InvalidOperationException(
                $"The member has not learned '{skill}', so there is nothing to raise; learning comes first.");
        }

        if (!Progression.SpendSkillPoints(points))
        {
            return new PartyRefusal(
                "insufficient-skill-points",
                $"Raising '{skill}' costs {points} skill point(s) and {Progression.SkillPoints} remain unspent.");
        }

        Skills.RaiseLevel(skill, levels, points);
        return null;
    }
}
