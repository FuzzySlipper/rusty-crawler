using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Kit.Progression;

/// <summary>
/// The one owner of a character's growth: experience earned, the level it buys at a training hall, the
/// skill points a level grants, and the rank a promotion hands over.
/// </summary>
/// <remarks>
/// <para>
/// <b>Experience, level, skill points, and rank are written here and nowhere else.</b> Every award — a
/// creature brought down, a quest finished, an act a ruleset decides is worth something — arrives at
/// <see cref="Award"/>, every level arrives at <see cref="Train"/>, and every rank arrives at
/// <see cref="Promote"/>. The members' own progression fields
/// are reachable only from inside the kit, so a second writer is a compile error rather than a review
/// finding, and <see cref="PartyMember"/> no longer offers a skill-point spend of its own: what spends
/// points is <see cref="RaiseSkill"/> here, which is also what raises the skill they pay for.
/// </para>
/// <para>
/// <b>The ruleset supplies the policy; this supplies the mechanism.</b> The curve a level takes, how an
/// award divides among the party, what a level adds to the pools, how many points it grants, how far a
/// class and rank let a skill grow, what a raise costs, and which ranks a class leads to are all the
/// ruleset's answers — <see cref="IProgressionRule"/> for growth, <see cref="ISkillRule"/> for the skills it
/// pays for, and <see cref="IPromotionRule"/> for the ladder a rank is given from.
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
    private readonly IPromotionRule? _promotions;

    /// <summary>Creates the owner over the party whose members grow.</summary>
    /// <param name="rule">This game's answers about the curve, the division, the growth, and the standing.</param>
    /// <param name="party">The party whose members' experience, levels, and points this owns.</param>
    /// <param name="skills">
    /// This game's answers about its skills, when its ruleset answered for any: the catalog, the ceiling a
    /// class and rank impose, the price of a raise, and the words a rung reads as. A ruleset that answers
    /// none leaves the owner unable to raise a skill, which is the honest state of a game that has not said
    /// how far one may go.
    /// </param>
    /// <param name="promotions">
    /// This game's ranks, when its ruleset stated a ladder: which classes lead to which ranks, what each rank
    /// asks for, and which alternative a second promotion takes. A ruleset that states none leaves the owner
    /// unable to promote anybody, which is the honest state of a game that has not said how its classes
    /// advance. A rank is progression state, so it moves here and nowhere else, exactly as a level does.
    /// </param>
    /// <exception cref="ArgumentNullException">The rule or the party is missing.</exception>
    public PartyProgression(
        IProgressionRule rule,
        PartyEntity party,
        ISkillRule? skills = null,
        IPromotionRule? promotions = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _skills = skills;
        _promotions = promotions;
    }

    /// <summary>This game's answers about progression.</summary>
    public IProgressionRule Rule => _rule;

    /// <summary>The party whose members this owns.</summary>
    public PartyEntity Party => _party;

    /// <summary>This game's answers about its skills, or null when its ruleset answered none.</summary>
    public ISkillRule? Skills => _skills;

    /// <summary>This game's ranks, or null when its ruleset stated no ladder.</summary>
    public IPromotionRule? Promotions => _promotions;

    /// <summary>What the last award did, or null before the party has earned experience.</summary>
    public ProgressionAwardResult? LastAward { get; private set; }

    /// <summary>What the last training step did, or null before anybody has trained.</summary>
    public ProgressionTrainingResult? LastTraining { get; private set; }

    /// <summary>What the last skill raise did, or null before anybody has spent a point.</summary>
    public SkillRaiseResult? LastRaise { get; private set; }

    /// <summary>What the last rank did, or null before anybody has been promoted.</summary>
    public PromotionResult? LastPromotion { get; private set; }

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
    /// and the skill points the new level grants are added to the pool. A rank moves through
    /// <see cref="Promote"/> on this same owner, so a class and the rank that goes with it are one act, and a
    /// level is raised only by a counter whose fee the party has paid.
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
                // A game that can say why a skill is closed says it here: a class that may hold no magic of a
                // school, and a character whose own path closed it, are different facts, and the second is one
                // only the ruleset can word.
                ceiling.Reason ?? new PartyRefusal(
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
    /// Gives one rank of this game's ladder to every member of the class it promotes from, as the person who
    /// grants it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A rank is progression state, so it moves here.</b> A promotion changes the class a member belongs
    /// to and the rank it holds together, because in this game family the two are one fact: the ceiling a
    /// class and rank impose, the growth a level gives, and the skills a class may hold are all read from the
    /// class the member now belongs to. Changing one without the other would be a character who is a knight
    /// by name and a cavalier by table, which is exactly the state a single owner exists to make impossible.
    /// </para>
    /// <para>
    /// <b>Every requirement is judged before anything moves, and the judgement is named.</b> The requirements
    /// the ladder states are read against the party — the person granting the rank, what the party carries,
    /// and what deeds it holds on record — and a member who is missing one is reported with the missing
    /// requirements in the rank's own words rather than quietly left behind. A member of the class who does
    /// not stand at the rank the promotion continues from is told that instead, because "you are not far
    /// enough along" is a different answer from "you have not brought what was asked for".
    /// </para>
    /// <para>
    /// <b>A rank is given to the class, not to one character.</b> A giver's own line is that they will make
    /// the party's members of a class into the rank above, so every member of that class who meets the
    /// requirements rises in one act, and each of them is reported for themselves. A party of two rogues,
    /// one of whom carries the proof, is one promotion with one grant and one named denial — which is also
    /// what makes a second promotion's alternative a choice per character: each member's own class is what
    /// records which one they took.
    /// </para>
    /// <para>
    /// <b>The record a rank leaves is written here too.</b> A ladder that states an award for a rank has it
    /// applied to the party's own carried state, which is what a later rank, a person's topic, or a quest's
    /// turn-in can then ask about. Nothing else about the member changes: a promotion widens what the next
    /// levels give rather than handing over a level, so no pool is filled and no point is granted.
    /// </para>
    /// </remarks>
    /// <param name="promotion">The rank's identity in this game's ladder, which is what content names it by.</param>
    /// <param name="giver">
    /// Who the party is taking the rank from, as the person's own identity, or empty when it is being taken
    /// from nobody — which is what a caller testing the requirement, or a scripted grant, states.
    /// </param>
    /// <returns>Who rose and what they met, who could not and what was missing, or why nobody did.</returns>
    public PromotionResult Promote(string promotion, string giver = "")
    {
        if (_promotions is null)
        {
            return RecordPromotion(PromotionResult.Refused(
                promotion ?? string.Empty,
                fromClass: string.Empty,
                toClass: string.Empty,
                rank: 0,
                choice: string.Empty,
                new PartyRefusal(
                    "promotion-policy-missing",
                    "This session's ruleset states no ladder of ranks, so nothing says which class leads to which, or what a rank asks for.")));
        }

        if (_promotions.Ladder.Rank(promotion) is not { } rank)
        {
            return RecordPromotion(PromotionResult.Refused(
                promotion ?? string.Empty,
                fromClass: string.Empty,
                toClass: string.Empty,
                rank: 0,
                choice: string.Empty,
                new PartyRefusal(
                    "promotion-unknown",
                    $"This game's ladder of ranks carries no '{promotion}', so there is no rank to be given.")));
        }

        List<PartyMember> candidates = [];
        List<PromotionDenial> denied = [];
        foreach (PartyMember member in _party.Members)
        {
            if (!string.Equals(member.Profile.Class.Value, rank.From.Value, StringComparison.Ordinal)) continue;
            candidates.Add(member);

            // The rank a promotion continues from is stated by the ladder and not asked for as a requirement:
            // a member of the class who stands below it has nothing to meet, so they are told where they
            // stand rather than handed a list of things that would not have helped. A member who stands *at*
            // the rank the promotion reaches is the other case — the class a rank names may not have been
            // taken yet, which is what a character holding a rank without holding one of its classes is —
            // and taking the rank is what names the class, so their rank is left where it is.
            if (member.Progression.ClassRank < rank.Rank - 1)
            {
                denied.Add(new PromotionDenial(
                    member.Id,
                    member.Profile.Name,
                    member.Profile.Class.Value,
                    member.Progression.ClassRank,
                    [
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"the rank of {rank.To}, which continues from rank {rank.Rank - 1} of {rank.From} and not from rank {member.Progression.ClassRank}"),
                    ]));
            }
        }

        if (candidates.Count == 0)
        {
            return RecordPromotion(PromotionResult.Refused(
                rank.Id,
                rank.From.Value,
                rank.To.Value,
                rank.Rank,
                rank.Choice,
                new PartyRefusal(
                    "promotion-class-absent",
                    $"Nobody in the party is a {rank.From}, and the rank of {rank.To} is given to one: the ladder promotes {rank.From} to {rank.To} and no other class.")));
        }

        // What the rank asks for is the party's own state — who it is speaking with, what it carries, what it
        // has done — so it is judged once for the rank rather than once per member, and every member who
        // stands where the rank continues from is measured against the same answer.
        IReadOnlyList<PromotionRequirementVerdict> verdicts = PromotionEligibility.Judge(rank, _party, giver);
        List<PromotionGrant> granted = [];
        foreach (PartyMember member in candidates)
        {
            if (member.Progression.ClassRank < rank.Rank - 1) continue;
            List<string> met = [];
            List<string> missing = [];
            foreach (PromotionRequirementVerdict verdict in verdicts)
            {
                (verdict.IsMet ? met : missing).Add(verdict.Statement);
            }

            if (missing.Count > 0)
            {
                denied.Add(new PromotionDenial(member.Id, member.Profile.Name, member.Profile.Class.Value, member.Progression.ClassRank, missing));
                continue;
            }

            string fromClass = member.Profile.Class.Value;
            int fromRank = member.Progression.ClassRank;

            // The two moves are one act: a member's class and rank are read together by every ceiling, every
            // growth table, and every class condition, so a promotion that moved one of them would leave a
            // character whose abilities and whose name disagree. The rank reached is the higher of the two,
            // so a member who already stood at it — the one whose class had not been named yet — is given the
            // class rather than quietly demoted.
            int reached = Math.Max(member.Progression.ClassRank, rank.Rank);
            member.Progression.SetClassRank(reached);
            member.Profile.ChangeClass(rank.To);

            // The record the rank leaves is the party's own carried state, so it survives a save with the rest
            // of them and a later rank can ask for it. A rank that already left its record keeps the one it
            // has rather than being written twice.
            if (rank.Award.Length > 0)
            {
                EffectId award = new(rank.Award);
                if (!_party.Effects.Has(award)) _party.Effects.Apply(new PartyEffect(award, 1));
            }

            granted.Add(new PromotionGrant(member.Id, member.Profile.Name, fromClass, fromRank, rank.To.Value, reached, rank.Choice, met));
        }

        if (granted.Count == 0)
        {
            // Nobody rose, and the reason is the first member the rank was offered to: the denials travel
            // with the refusal rather than being replaced by it, because a screen shows what each member was
            // missing beside the rank that was not given.
            PromotionDenial first = denied[0];
            return RecordPromotion(new PromotionResult(
                rank.Id,
                rank.From.Value,
                rank.To.Value,
                rank.Rank,
                rank.Choice,
                [],
                denied,
                new PartyRefusal(
                    "promotion-requirements-unmet",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{first.Name}, a {first.Class} of rank {first.Rank}, is missing {string.Join("; ", first.Missing)}, so the rank of {rank.To} was not given."))));
        }

        return RecordPromotion(new PromotionResult(
            rank.Id,
            rank.From.Value,
            rank.To.Value,
            rank.Rank,
            rank.Choice,
            granted,
            denied,
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

    /// <summary>Records a rank as the last one and hands it back.</summary>
    private PromotionResult RecordPromotion(PromotionResult result)
    {
        LastPromotion = result;
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
