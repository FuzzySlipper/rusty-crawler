using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Kit.Progression;

/// <summary>
/// The one owner of a character's growth: experience earned, the level it buys at a training hall, and the
/// skill points a level grants.
/// </summary>
/// <remarks>
/// <para>
/// <b>Experience, level, and skill points are written here and nowhere else.</b> Every award — a creature
/// brought down, a quest finished, an act a ruleset decides is worth something — arrives at
/// <see cref="Award"/>, and every level arrives at <see cref="Train"/>. The members' own progression fields
/// are reachable only from inside the kit, so a second writer is a compile error rather than a review
/// finding, and <see cref="PartyMember"/> no longer offers a skill-point spend of its own: what spends
/// points is <see cref="RaiseSkill"/> here, which is also what raises the skill they pay for.
/// </para>
/// <para>
/// <b>The ruleset supplies the policy; this supplies the mechanism.</b> The curve a level takes, how an
/// award divides among the party, what a level adds to the pools, how many points it grants, how far a
/// class and rank let a skill grow, and what a raise costs are all the ruleset's answers —
/// <see cref="IProgressionRule"/> for growth and <see cref="ISkillRule"/> for the skills it pays for.
/// Nothing here decides a number: this asks, applies, and reports, which is what keeps a formula out of the
/// kit and out of every call site that would otherwise restate it.
/// </para>
/// <para>
/// <b>A raise is judged whole before anything moves.</b> The ceiling the class and rank impose and the
/// points the pool holds are both settled before the pool is charged and the skill rises together, so a
/// raise that was refused leaves the character exactly where they stood — no points spent for nothing and
/// no skill that grew for free. How a skill's <em>rung</em> moves is not here: a rung is taught by a
/// lesson at a counter, which the service mechanism applies, and a raise never moves it.
/// </para>
/// <para>
/// <b>The fee is settled before this is asked.</b> A training step is charged through the party's one
/// ledger, exactly as a purchase, a fare, and a room are, and the settled terms arrive here so the step can
/// be judged against the hall's own ceiling and reported with what it cost. This owner therefore moves no
/// coin: a party that cannot pay is refused by the settlement path with the shortfall named, before any
/// level is granted.
/// </para>
/// <para>
/// <b>Standing is not written wherever the event happened.</b> What an award or a training step does to the
/// party's reputation and fame is asked of the rule and applied to the party's own
/// <see cref="PartyReputation"/>, so a kill, a quest, and a level rise all move the same two numbers through
/// the same owner as everything else about the party.
/// </para>
/// </remarks>
public sealed class PartyProgression
{
    private readonly IProgressionRule _rule;
    private readonly PartyEntity _party;
    private readonly ISkillRule? _skills;

    /// <summary>Creates the owner over the party whose members grow.</summary>
    /// <param name="rule">This game's answers about the curve, the division, the growth, and the standing.</param>
    /// <param name="party">The party whose members' experience, levels, and points this owns.</param>
    /// <param name="skills">
    /// This game's answers about its skills, when its ruleset answered for any: the catalog, the ceiling a
    /// class and rank impose, the price of a raise, and the words a rung reads as. A ruleset that answers
    /// none leaves the owner unable to raise a skill, which is the honest state of a game that has not said
    /// how far one may go.
    /// </param>
    /// <exception cref="ArgumentNullException">The rule or the party is missing.</exception>
    public PartyProgression(IProgressionRule rule, PartyEntity party, ISkillRule? skills = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _skills = skills;
    }

    /// <summary>This game's answers about progression.</summary>
    public IProgressionRule Rule => _rule;

    /// <summary>The party whose members this owns.</summary>
    public PartyEntity Party => _party;

    /// <summary>This game's answers about its skills, or null when its ruleset answered none.</summary>
    public ISkillRule? Skills => _skills;

    /// <summary>What the last award did, or null before the party has earned experience.</summary>
    public ProgressionAwardResult? LastAward { get; private set; }

    /// <summary>What the last training step did, or null before anybody has trained.</summary>
    public ProgressionTrainingResult? LastTraining { get; private set; }

    /// <summary>What the last skill raise did, or null before anybody has spent a point.</summary>
    public SkillRaiseResult? LastRaise { get; private set; }

    /// <summary>How much experience the member must have banked to be trained from the level it stands at.</summary>
    /// <remarks>
    /// This is the curve read for a member rather than for a number, so what a panel shows as "the next
    /// level" is the same figure a training hall judges the member against: one answer, asked twice.
    /// </remarks>
    /// <param name="member">The member to read.</param>
    /// <returns>How much banked experience that member's next level takes.</returns>
    /// <exception cref="ArgumentNullException">No member was supplied.</exception>
    public long ExperienceForNextLevel(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return _rule.ExperienceForLevel(member.Progression.Level);
    }

    /// <summary>Awards experience to the party through this one entry, and reports who took what.</summary>
    /// <remarks>
    /// <para>
    /// <b>Every source comes through here.</b> A creature's death, a quest's reward, and a ruleset's own deed
    /// differ only in what they report as the source and how much they are worth; how the award divides,
    /// what it does to the party's standing, and where the experience lands are the same three steps
    /// whichever it was. That is what makes "a fight and a completed quest both produce experience through
    /// the same award path" a fact about the code rather than a promise about it.
    /// </para>
    /// <para>
    /// An award of nothing and an award nobody can take are refused by name and move nothing: the second is
    /// a party that is entirely down, which the donor's own division leaves with no share at all.
    /// </para>
    /// </remarks>
    /// <param name="award">What earned the experience and how much it was.</param>
    /// <returns>Who took what, what the world made of it, or why nothing was awarded.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public ProgressionAwardResult Award(PartyExperienceAward award)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(award.Amount);
        if (award.Amount == 0)
        {
            return Record(ProgressionAwardResult.Refused(
                award.Source,
                award.Amount,
                new PartyRefusal("progression-award-empty", $"An award of nothing from '{award.Source}' is not an award.")));
        }

        IReadOnlyList<ProgressionShare> shares = _rule.Divide(new ProgressionDivision(_party, award.Source, award.Amount));
        if (shares.Count == 0)
        {
            return Record(ProgressionAwardResult.Refused(
                award.Source,
                award.Amount,
                new PartyRefusal(
                    "progression-award-unshared",
                    $"Nobody in the party could take the {award.Amount} experience '{award.Source}' was worth.")));
        }

        List<ProgressionShare> taken = [];
        foreach (ProgressionShare share in shares)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(share.Amount);
            PartyMember member = _party.Member(share.Member);

            // The member's own answer is credited rather than the share's name: a rule that named somebody
            // other than the member it divided for would otherwise report experience that landed elsewhere.
            member.Progression.AwardExperience(share.Amount);
            taken.Add(share with { Name = member.Profile.Name });
        }

        ProgressionStanding standing = ApplyStanding(ProgressionEventKind.Award, award.Amount);
        return Record(new ProgressionAwardResult(award.Source, award.Amount, taken, standing, Refusal: null));
    }

    /// <summary>Raises one member a level at a counter whose fee the party's accounts have already paid.</summary>
    /// <remarks>
    /// <para>
    /// The step is judged against the two conditions a training hall has: the member must have banked the
    /// experience the curve takes, and must stand below the ceiling content gives this counter. Both are
    /// refused by name before anything moves, and the order matters — a member who has reached the ceiling
    /// is told that rather than how much more experience a level they cannot buy would take.
    /// </para>
    /// <para>
    /// What a level does then happens here and in one place: the level rises, the pools grow by the growth
    /// table's own numbers for the class and rank, both pools are filled as the donor's own training does,
    /// and the skill points the new level grants are added to the pool. A later stone that raises a rank or
    /// chooses a promotion path moves the member's rank through this same owner; nothing else may move a
    /// level.
    /// </para>
    /// </remarks>
    /// <param name="member">The member to train.</param>
    /// <param name="terms">The counter, what the step cost, and the ceiling it trains to.</param>
    /// <returns>The level reached and what it gave, or why nobody trained.</returns>
    public ProgressionTrainingResult Train(PartyMemberId member, ProgressionTrainingTerms terms)
    {
        PartyMember trainee = _party.Member(member);
        int level = trainee.Progression.Level;
        if (level >= terms.Cap)
        {
            return RecordTraining(ProgressionTrainingResult.Refused(
                member,
                trainee.Profile.Name,
                level,
                terms.Counter,
                new PartyRefusal(
                    "progression-training-capped",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{trainee.Profile.Name} stands at level {level} and {terms.Counter} trains no further than level {terms.Cap}."))));
        }

        long wanted = _rule.ExperienceForLevel(level);
        if (trainee.Progression.Experience < wanted)
        {
            return RecordTraining(ProgressionTrainingResult.Refused(
                member,
                trainee.Profile.Name,
                level,
                terms.Counter,
                new PartyRefusal(
                    "progression-experience-short",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{trainee.Profile.Name} needs {wanted - trainee.Progression.Experience} more experience to train to level {level + 1}."))));
        }

        int reached = level + 1;
        ProgressionGrowth growth = _rule.Growth(new ProgressionGrowthRequest(trainee, reached));
        ArgumentOutOfRangeException.ThrowIfNegative(growth.HitPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(growth.SpellPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(growth.SkillPoints);

        trainee.Progression.SetLevel(reached);
        // The pools grow by the table's own numbers and are then filled, which is what the donor's training
        // does at the moment of the rise: a level is a fuller character, not only a larger one.
        trainee.Resources.SetMaximumHitPoints(checked(trainee.Resources.HitPoints.Maximum + growth.HitPoints));
        trainee.Resources.SetMaximumSpellPoints(checked(trainee.Resources.SpellPoints.Maximum + growth.SpellPoints));
        trainee.Resources.RestoreAll();
        trainee.Progression.GrantSkillPoints(growth.SkillPoints);

        ProgressionStanding standing = ApplyStanding(ProgressionEventKind.Training, terms.Fee);
        return RecordTraining(new ProgressionTrainingResult(
            member,
            trainee.Profile.Name,
            reached,
            growth,
            terms.Fee,
            terms.Counter,
            standing,
            Refusal: null));
    }

    /// <summary>Reads what raising a member's skill would cost and how far it would reach, without spending.</summary>
    /// <remarks>
    /// <para>
    /// The same judgement <see cref="RaiseSkill"/> performs, performed as a read: a screen shows what a
    /// raise would cost or why it would be refused, and what it shows is the answer the raise itself acts
    /// on. A panel that multiplied a level by a price would be a second copy of the ruleset's arithmetic,
    /// and the two would disagree the first time either changed.
    /// </para>
    /// <para>
    /// Nothing here moves: the plan is a value, and a caller that wants the raise performs it through
    /// <see cref="RaiseSkill"/>.
    /// </para>
    /// </remarks>
    /// <param name="member">The member whose skill is being asked about.</param>
    /// <param name="skill">The skill to ask about.</param>
    /// <param name="levels">How many levels the raise would add, which must be at least one.</param>
    /// <returns>What the raise would cost and reach, or why it would be refused.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The levels are below one.</exception>
    public SkillRaisePlan Plan(PartyMemberId member, SkillId skill, int levels = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);
        PartyMember character = _party.Member(member);

        // A session whose ruleset answered no skill policy can state no ceiling, no price, and no ladder:
        // the refusal names that rather than pretending a skill may grow for free.
        if (_skills is null)
        {
            return new SkillRaisePlan(
                skill,
                levels,
                character.Skills.LevelOf(skill),
                character.Skills.LevelOf(skill),
                SkillCeiling.None,
                Points: 0,
                new PartyRefusal(
                    "skill-policy-missing",
                    "This session's ruleset states no skill policy, so nothing says how far a skill may grow or what raising one costs."));
        }

        if (!character.Skills.TryGet(skill, out SkillEntry entry))
        {
            return new SkillRaisePlan(
                skill,
                levels,
                Level: 0,
                Reached: levels,
                _skills.Ceiling(character, skill),
                Points: 0,
                new PartyRefusal(
                    "skill-not-learned",
                    $"{character.Profile.Name} has not learned {Describe(skill)}, so there is nothing to raise; a lesson comes first."));
        }

        SkillCeiling ceiling = _skills.Ceiling(character, skill);
        int reached = checked(entry.Level + levels);

        // The ceiling is judged before the price, because the two refusals answer different questions: a
        // member asking to pass a limit they can never pass should be told the limit rather than what the
        // levels they cannot buy would have cost.
        if (ceiling.IsNone)
        {
            return new SkillRaisePlan(
                skill,
                levels,
                entry.Level,
                reached,
                ceiling,
                Points: 0,
                new PartyRefusal(
                    "skill-not-permitted",
                    $"{character.Profile.Name} is a {character.Profile.Class} and this game's table lets that class hold no {Describe(skill)} at all."));
        }

        if (reached > ceiling.MaximumLevel)
        {
            return new SkillRaisePlan(
                skill,
                levels,
                entry.Level,
                reached,
                ceiling,
                Points: 0,
                new PartyRefusal(
                    "skill-ceiling-reached",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Describe(skill)} stands at level {entry.Level} and {character.Profile.Name}, a {character.Profile.Class} of rank {character.Progression.ClassRank}, may raise it to {ceiling.MaximumLevel} and no further; a promotion raises the ceiling.")));
        }

        int points = _skills.RaiseCost(entry, levels);
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        if (character.Progression.SkillPoints < points)
        {
            return new SkillRaisePlan(
                skill,
                levels,
                entry.Level,
                reached,
                ceiling,
                points,
                new PartyRefusal(
                    "insufficient-skill-points",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Raising {Describe(skill)} to level {reached} costs {points} skill point(s) and {character.Progression.SkillPoints} remain unspent.")));
        }

        return new SkillRaisePlan(skill, levels, entry.Level, reached, ceiling, points, Refusal: null);
    }

    /// <summary>Spends skill points on raising a skill the member knows, in one operation.</summary>
    /// <remarks>
    /// <para>
    /// The ceiling a class and rank impose and the cost of the levels are the ruleset's, so they are asked
    /// of it rather than handed in: the points leave the pool this owner holds and the raise lands on the
    /// skill together, which is why the two cannot drift into a character who paid for nothing or gained
    /// for free. A raise past the ceiling is refused with the limit named, and one the pool cannot cover is
    /// refused with what it costs and what remains.
    /// </para>
    /// <para>
    /// This is the one entry that spends skill points. A member's own type offers no spend of its own, so a
    /// skill raised without paying, or points spent without a raise, are both shapes the code does not have.
    /// A lesson bought at a counter raises a skill's <em>rung</em> and its first level for coin rather than
    /// points, which is the mechanism's own path and spends none of this pool.
    /// </para>
    /// </remarks>
    /// <param name="member">The member whose pool pays and whose skill rises.</param>
    /// <param name="skill">The skill to raise, which the member must already have learned.</param>
    /// <param name="levels">How many levels to add, which must be at least one.</param>
    /// <returns>What the raise reached and cost, or why nothing was raised.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The levels are below one.</exception>
    public SkillRaiseResult RaiseSkill(PartyMemberId member, SkillId skill, int levels = 1)
    {
        SkillRaisePlan plan = Plan(member, skill, levels);
        PartyMember character = _party.Member(member);
        if (!plan.IsPossible)
        {
            return RecordRaise(SkillRaiseResult.Refused(
                member,
                character.Profile.Name,
                skill,
                levels,
                plan.Level,
                character.Skills.TierOf(skill),
                character.Progression.SkillPoints,
                plan.Refusal!));
        }

        // Everything that can fail has been settled before the pool is charged: a raise that cost points and
        // then failed would take a character's skill points for nothing.
        if (!character.Progression.SpendSkillPoints(plan.Points))
        {
            throw new InvalidOperationException(
                $"Raising '{skill}' planned {plan.Points} skill point(s) and the member holds {character.Progression.SkillPoints}, so the plan and the pool disagree.");
        }

        character.Skills.RaiseLevel(skill, levels, plan.Points);
        return RecordRaise(new SkillRaiseResult(
            member,
            character.Profile.Name,
            skill,
            levels,
            plan.Reached,
            character.Skills.TierOf(skill),
            plan.Points,
            character.Progression.SkillPoints,
            Refusal: null));
    }

    /// <summary>
    /// Asks the rule what an event does to the party's standing and moves the party's own numbers by it.
    /// </summary>
    /// <remarks>
    /// This is the only place a progression event reaches reputation or fame, and it reaches them through
    /// the party's own component rather than through fields of its own: the party already carries both, and
    /// a second copy kept beside them is exactly what a save would disagree about.
    /// </remarks>
    private ProgressionStanding ApplyStanding(ProgressionEventKind kind, long amount)
    {
        ProgressionStanding standing = _rule.Standing(new ProgressionStandingRequest(_party, kind, amount));
        if (standing.Reputation != 0) _party.Reputation.ChangeReputation(standing.Reputation);
        if (standing.Fame != 0) _party.Reputation.ChangeFame(standing.Fame);
        return standing;
    }

    /// <summary>Records an award as the last one and hands it back.</summary>
    private ProgressionAwardResult Record(ProgressionAwardResult result)
    {
        LastAward = result;
        return result;
    }

    /// <summary>Records a training step as the last one and hands it back.</summary>
    private ProgressionTrainingResult RecordTraining(ProgressionTrainingResult result)
    {
        LastTraining = result;
        return result;
    }

    /// <summary>Records a raise as the last one and hands it back.</summary>
    private SkillRaiseResult RecordRaise(SkillRaiseResult result)
    {
        LastRaise = result;
        return result;
    }

    /// <summary>How one skill reads in a sentence a person acts on: its name, in the game's own spelling.</summary>
    /// <remarks>
    /// The catalog is what turns a skill's identity into a word, and a skill content does not declare still
    /// has to read as something: the identity itself is the honest answer there, because a message that
    /// dropped the name would leave a player unable to tell which of their skills was refused.
    /// </remarks>
    private string Describe(SkillId skill) =>
        _skills is { } policy && policy.Catalog.Declares(skill) ? skill.Value : $"'{skill.Value}'";
}
