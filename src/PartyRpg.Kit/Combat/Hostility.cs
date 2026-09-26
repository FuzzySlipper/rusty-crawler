namespace PartyRpg.Kit.Combat;

/// <summary>
/// What something standing in the world is, as far as a fight is concerned: whether it can fight at all,
/// and whether it starts one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hostility is a fact about a thing, not a flag a session sets.</b> A creature's kind carries a
/// hostility band whose value doubles as the distance at which it notices the party, that value is carried
/// on the live actor, and the actor's own state is what a fight is decided from — including the case where
/// something that made it friendly wears off and the creature's own nature is what is left. A mode flag
/// saying "in combat" would have to be kept in step with every creature in the place and would say nothing
/// about a creature nobody has met yet.
/// </para>
/// <para>
/// This is the half a ruleset answers — what a thing is. The other half is what the party has done to it,
/// which is state a fight keeps; a peaceful creature the party attacks becomes an enemy without anything
/// about what it is having changed.
/// </para>
/// <para>
/// The absence of a fight is the absence of creatures, which is why <see cref="Inert"/> is a value and not
/// a null: a door is not a creature that happens to be friendly, and a policy that answered "not hostile"
/// for one could never say why the party cannot attack it.
/// </para>
/// </remarks>
public readonly record struct Hostility
{
    private Hostility(bool isCreature, bool attacksOnSight, double noticeRange)
    {
        IsCreature = isCreature;
        AttacksOnSight = attacksOnSight;
        NoticeRange = noticeRange;
    }

    /// <summary>
    /// Not a creature at all: a door, a chest, a light, a decoration. It can neither fight nor be fought,
    /// and the party attacking it is not a fight but a mistake the mechanism refuses by name.
    /// </summary>
    public static Hostility Inert { get; } = new(isCreature: false, attacksOnSight: false, noticeRange: 0);

    /// <summary>
    /// A creature that does not start fights: a person going about their day, a creature the party has not
    /// provoked. It can be attacked, and attacking it is what makes it an enemy.
    /// </summary>
    public static Hostility Peaceful { get; } = new(isCreature: true, attacksOnSight: false, noticeRange: 0);

    /// <summary>
    /// A creature that attacks the party on sight: it becomes an enemy the moment the party is inside
    /// <paramref name="noticeRange"/> of it, which is what "hostile on sight" means in world state.
    /// </summary>
    /// <param name="noticeRange">How far off the creature notices the party, in the place's own units.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not a finite, positive distance.</exception>
    public static Hostility Aggressive(double noticeRange)
    {
        if (!double.IsFinite(noticeRange) || noticeRange <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(noticeRange),
                noticeRange,
                "A creature that attacks on sight notices the party from some finite, positive distance; a range that is zero, negative, or unmeasurable would make it either blind or everywhere.");
        }

        return new Hostility(isCreature: true, attacksOnSight: true, noticeRange: noticeRange);
    }

    /// <summary>Whether this is a creature that can fight and be fought at all.</summary>
    public bool IsCreature { get; }

    /// <summary>Whether it attacks the party the moment the party comes within <see cref="NoticeRange"/>.</summary>
    public bool AttacksOnSight { get; }

    /// <summary>How far off it notices the party, zero when it does not attack on sight.</summary>
    public double NoticeRange { get; }

    /// <summary>Whether the party is inside the distance at which this creature attacks on sight.</summary>
    /// <param name="distance">How far the creature is from the party, in the place's own units.</param>
    /// <returns>Whether it has noticed the party.</returns>
    public bool Notices(double distance) => AttacksOnSight && distance <= NoticeRange;
}
