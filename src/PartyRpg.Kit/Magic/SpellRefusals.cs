namespace PartyRpg.Kit.Magic;

/// <summary>Why one spell was not learned, cast, or applied, in the mechanism's own vocabulary.</summary>
/// <remarks>
/// <para>
/// A spell's refusal is the kit's one <see cref="Refusal"/>, a code and a sentence: the code is what a test and
/// a diagnostic compare, and the sentence is what a person reads. Every refusal names the fact it broke,
/// because "the spell did not happen" hides which of a missing skill, a mastery, a short pool, a bad aim,
/// and an actor who cannot act it was — and a player acts differently on each.
/// </para>
/// <para>
/// The codes here are the mechanism's and are deliberately few. A ruleset answers its own sentences through
/// them rather than inventing codes of its own, so a screen can read one vocabulary wherever a casting is
/// refused.
/// </para>
/// </remarks>
public static class SpellRefusals
{
    /// <summary>Content declares no such spell, so there is nothing to learn or cast.</summary>
    /// <param name="spell">The spell that was named.</param>
    /// <returns>The refusal.</returns>
    public static Refusal Unknown(string spell) => new(
        SpellCodes.SpellUnknown,
        $"No spell '{spell}' is declared by this game's content, so nothing could be learned or cast.");

    /// <summary>The caster's spellbook does not hold the spell.</summary>
    /// <param name="name">What the caster is called.</param>
    /// <param name="spell">What the spell is called.</param>
    /// <returns>The refusal.</returns>
    public static Refusal NotKnown(string name, string spell) => new(
        SpellCodes.SpellNotKnown,
        $"{name} does not know {spell}, so there is nothing in their spellbook to cast.");

    /// <summary>The character's spellbook already holds the spell.</summary>
    /// <param name="name">What the character is called.</param>
    /// <param name="spell">What the spell is called.</param>
    /// <returns>The refusal.</returns>
    public static Refusal AlreadyKnown(string name, string spell) => new(
        SpellCodes.SpellAlreadyKnown,
        $"{name} already knows {spell}, so the book would teach nothing.");

    /// <summary>The character has never learned the school's skill, so none of its spells is theirs.</summary>
    /// <param name="name">What the character is called.</param>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="skill">What the school's skill is called.</param>
    /// <returns>The refusal.</returns>
    public static Refusal SchoolMissing(string name, string spell, string skill) => new(
        SpellCodes.SpellSchoolMissing,
        $"{name} has never learned {skill}, and a spell of that school is learned by a caster who holds its skill.");

    /// <summary>The caster's own class closes the spell's school, so no mastery of it is theirs to hold.</summary>
    /// <remarks>
    /// A school a character may not hold at all is a different fact from a rung they have not reached: the
    /// first is a limit their class states, and a game that can say why — the path the character took, the
    /// promotion they did not — says it in <paramref name="because"/> rather than leaving a caster to guess
    /// which of their own choices shut the door.
    /// </remarks>
    /// <param name="name">What the caster is called.</param>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="school">What the school's skill is called.</param>
    /// <param name="because">Why the school is closed, in the game's own words.</param>
    /// <returns>The refusal.</returns>
    public static Refusal SchoolClosed(string name, string spell, string school, string because) => new(
        SpellCodes.SpellSchoolClosed,
        $"{name} cannot cast {spell}: the school of {school} is closed to them, {because}");

    /// <summary>The caster's school mastery is below the rung the spell requires.</summary>
    /// <param name="name">What the caster is called.</param>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="required">What the rung the spell requires is called.</param>
    /// <param name="held">What the rung the caster stands at is called.</param>
    /// <returns>The refusal.</returns>
    public static Refusal MasteryTooLow(string name, string spell, string required, string held) => new(
        SpellCodes.SpellMasteryTooLow,
        $"{spell} asks for {required} mastery of its school, and {name} stands at {held}.");

    /// <summary>The caster has not enough spell points left for one casting.</summary>
    /// <param name="name">What the caster is called.</param>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="cost">What one casting costs.</param>
    /// <param name="available">How many spell points are left.</param>
    /// <returns>The refusal.</returns>
    public static Refusal NotEnoughPoints(string name, string spell, int cost, int available) => new(
        SpellCodes.SpellPointsShort,
        $"{name} has {available} spell point(s) left and {spell} costs {cost}.");

    /// <summary>The casting named no target, and the spell's aim requires one.</summary>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="targeting">What the spell is aimed at, as the wire spells it.</param>
    /// <returns>The refusal.</returns>
    public static Refusal NoTarget(string spell, string targeting) => new(
        SpellCodes.SpellTargetMissing,
        $"{spell} is aimed at {targeting} and the casting named no target.");

    /// <summary>The casting named a target this session cannot offer for that aim.</summary>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="target">What the casting named.</param>
    /// <returns>The refusal.</returns>
    public static Refusal NoValidTarget(string spell, string target) => new(
        SpellCodes.SpellTargetInvalid,
        $"{spell} has no valid target in '{target}', which is neither a member of the party nor a creature this fight holds.");

    /// <summary>The spell named a fight target beyond the reach of its attack, so it was not paid for.</summary>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="target">What the casting named.</param>
    /// <returns>The refusal.</returns>
    public static Refusal TargetOutOfReach(string spell, string target) => new(
        SpellCodes.SpellTargetOutOfReach,
        $"{spell} cannot reach {target}, so the spell was not cast and no spell point was spent.");

    /// <summary>The named foe is actually standing on the party's side, so the spell is refused before payment.</summary>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="target">The friendly combatant the casting named.</param>
    /// <returns>The refusal.</returns>
    public static Refusal FriendlyTarget(string spell, string target) => new(
        SpellCodes.SpellTargetFriendly,
        $"{spell} cannot harm or afflict friendly target {target}, so the spell was not cast and no spell point was spent.");

    /// <summary>
    /// The spell acts on something this build cannot aim at, so the casting is refused before it is paid for.
    /// </summary>
    /// <remarks>
    /// This is the refusal for a spell whose effect needs a target the session has no way to name — an item
    /// in the pack, a thing across the room, a follower. It stops the cast before a point is spent, because a
    /// spell that would be paid for and then change nothing is worse than one that never happened; the
    /// sentence names the owner that would make the target nameable.
    /// </remarks>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="missing">What the spell would act on.</param>
    /// <param name="receiver">Which owner would have to supply a way to name it.</param>
    /// <returns>The refusal.</returns>
    public static Refusal TargetUnavailable(string spell, string missing, string receiver) => new(
        SpellCodes.SpellTargetUnavailable,
        $"{spell} acts on {missing}, and this build has no way to aim a spell at one (receiver: {receiver}).");

    /// <summary>
    /// The game states the spell and this build applies none of what it does, so a casting would be paid for
    /// and change nothing.
    /// </summary>
    /// <remarks>
    /// It is refused before a point or an item is spent, for the same reason an unaimable target is: the
    /// sentence names what the spell would do and the owner that would have to exist for it to happen.
    /// </remarks>
    /// <param name="spell">What the spell is called.</param>
    /// <param name="missing">What the spell would do.</param>
    /// <param name="receiver">Which owner would have to exist for it to happen.</param>
    /// <returns>The refusal.</returns>
    public static Refusal NotApplied(string spell, string missing, string receiver) => new(
        SpellCodes.SpellNotApplied,
        $"{spell} would leave {missing}, and this build applies none of it yet (receiver: {receiver}), so it was not cast and nothing was spent.");

    /// <summary>The party has no such member, so a casting has nobody to go to.</summary>
    /// <param name="position">The place in the party the casting named, counted from zero.</param>
    /// <returns>The refusal.</returns>
    public static Refusal NoSuchMember(int position) => new(
        SpellCodes.SpellNoSuchMember,
        $"The party has no member {position + 1}, so a casting has nobody to go to.");

    /// <summary>This session composes no effect path, so nothing could apply a spell.</summary>
    /// <returns>The refusal.</returns>
    public static Refusal NoEffectPath() => new(
        SpellCodes.SpellNoEffectPath,
        "This session composes no effect path, so a spell could be resolved but nothing could apply it.");

    /// <summary>The party holds no such item, so a casting has no source to take its spell from.</summary>
    /// <param name="item">The instance identity the casting named.</param>
    /// <returns>The refusal.</returns>
    public static Refusal ItemNotHeld(string item) => new(
        SpellCodes.SpellItemNotHeld,
        $"The party holds no item {item}, so no spell could be cast from it.");

    /// <summary>The item carries no spell this game can read, so casting from it would do nothing.</summary>
    /// <param name="item">The item definition the instance is a copy of.</param>
    /// <returns>The refusal.</returns>
    public static Refusal ItemCarriesNoSpell(string item) => new(
        SpellCodes.SpellItemCarriesNone,
        $"Item '{item}' carries no spell this game reads, so casting from it would use it up for nothing.");

    /// <summary>The item carries a different spell than the casting named.</summary>
    /// <param name="item">What the item is called.</param>
    /// <param name="carried">What the item carries.</param>
    /// <param name="named">What the casting named.</param>
    /// <returns>The refusal.</returns>
    public static Refusal ItemCarriesAnother(string item, string carried, string named) => new(
        SpellCodes.SpellItemCarriesAnother,
        $"Item '{item}' carries {carried}, and the casting named {named}, so which spell the item would cast is ambiguous.");

    /// <summary>An item that spends charges is used as a weapon, so it must be wielded rather than lying in the pack.</summary>
    /// <param name="item">What the item is called.</param>
    /// <returns>The refusal.</returns>
    public static Refusal ItemNotWielded(string item) => new(
        SpellCodes.SpellItemNotWielded,
        $"Item '{item}' holds charges and is used as a weapon, so it has to be wielded before its spell can be aimed.");

    /// <summary>The item holds no charges left, so using it would spend nothing.</summary>
    /// <param name="item">What the item is called.</param>
    /// <param name="charges">How many uses its kind holds when full.</param>
    /// <returns>The refusal.</returns>
    public static Refusal ItemSpent(string item, int charges) => new(
        SpellCodes.SpellItemNoCharges,
        $"Item '{item}' holds none of the {charges} charge(s) its kind states, so using it would spend nothing.");

    /// <summary>What acts on the caster leaves it unable to cast, or it has not recovered yet.</summary>
    /// <param name="name">What the caster is called.</param>
    /// <param name="because">The sentence that says what stopped it.</param>
    /// <returns>The refusal.</returns>
    public static Refusal CannotAct(string name, string because) => new(
        SpellCodes.SpellCasterCannotAct,
        $"{name} cannot cast: {because}");
}
