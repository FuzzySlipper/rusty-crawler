using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one owner of experience, levels, and skill points: the award both a kill and a quest arrive at, the
/// training step a counter settles through it, the growth a level gives, and what an award does to the
/// party's standing.
/// </summary>
/// <remarks>
/// <para>
/// The curve, the division, the growth, and the standing are the test's own rule, which is the point of the
/// seam: the kit holds no number a game would recognize, so a test states its own and demands the same
/// mechanism serve it. What only this suite can prove is that the owner is the one place those values move
/// — the source scan at the end of the file fails the kit if any other source names a transition — and that
/// a kill reaches the same entry a quest does.
/// </para>
/// <para>
/// The kill is driven through <see cref="ProgressionAwards"/> over a real <see cref="CorpseGround"/>: the
/// bodies owner is the kit's own, so the serials that name deaths are the ones a fight would really read,
/// and the reading is fed the same records a fight reports.
/// </para>
/// </remarks>
public sealed class ProgressionTests
{
    private static readonly PlaceId Here = new("1");

    [Fact]
    public void A_kill_and_a_quest_both_award_through_the_one_entry()
    {
        using PartyEntity party = PartyOfTwo();
        PartyProgression progression = new(new TestRule(), party);
        Bodies ground = new();
        ProgressionAwards awards = new(placement => Worth(placement), () => progression, ground);

        // A fight reports the creature it brought down, and the kill is worth what its own row states. The
        // award divides among the two members and lands on their experience: 250 each, which is the rule's
        // own equal division.
        IReadOnlyList<Corpse> bodies = awards.Observe(Here, [Fallen("beast-1", 250)]);
        ProgressionAwardResult kill = progression.LastAward!;
        Assert.True(kill.IsAwarded);
        Assert.Equal(ProgressionAwards.KillSource, kill.Source);
        Assert.Equal(250, kill.Amount);
        Assert.Equal(2, kill.Shares.Count);
        Assert.Equal(125, party.Members[0].Progression.Experience);
        Assert.Equal(125, party.Members[1].Progression.Experience);
        Assert.Equal(1, awards.PaidDeaths);
        Assert.Single(bodies);

        // The same reading on the next update is the same death, not a second one: a fight re-reads the
        // place every update, and a death that paid twice would be experience nobody earned.
        awards.Observe(Here, [Fallen("beast-1", 250)]);
        Assert.Equal(125, party.Members[0].Progression.Experience);
        Assert.Equal(1, awards.PaidDeaths);

        // A second creature is a second death, and it pays.
        awards.Observe(Here, [Fallen("beast-1", 250), Fallen("beast-2", 100)]);
        Assert.Equal(175, party.Members[0].Progression.Experience);
        Assert.Equal(2, awards.PaidDeaths);

        // A creature the fight no longer reads as down is forgotten with its body: when it is read as down
        // again — the world restored the place, or the party walked out and back in — that is a new death
        // with a new serial, and it pays again.
        awards.Observe(Here, []);
        Assert.Equal(0, awards.PaidDeaths);
        awards.Observe(Here, [Fallen("beast-1", 250)]);
        Assert.Equal(300, party.Members[0].Progression.Experience);

        // The second source: a completed quest arrives at the same entry, with the same division and the
        // same landing place. Nothing about the kill's path is special, which is what makes one award path
        // rather than one per source.
        ProgressionAwardResult quest = progression.Award(new PartyExperienceAward("quest", 1000));
        Assert.True(quest.IsAwarded);
        Assert.Equal("quest", quest.Source);
        Assert.Equal(500, quest.Shares[0].Amount);
        Assert.Equal(800, party.Members[0].Progression.Experience);
        Assert.Equal(800, party.Members[1].Progression.Experience);
        Assert.Equal("quest", progression.LastAward!.Source);
    }

    [Fact]
    public void An_award_nobody_can_take_and_an_award_of_nothing_are_refused_by_name()
    {
        using PartyEntity party = PartyOfTwo();
        PartyProgression progression = new(new TestRule(), party);

        ProgressionAwardResult empty = progression.Award(new PartyExperienceAward("quest", 0));
        Assert.False(empty.IsAwarded);
        Assert.Equal("progression-award-empty", empty.Refusal!.Code);
        Assert.Empty(empty.Shares);

        // A party entirely laid out takes no share — the donor's own division leaves nobody able to earn —
        // so the award is refused rather than credited to characters who cannot grow.
        foreach (PartyMember member in party.Members) member.Conditions.Apply(new ActiveCondition(LaidOut));
        ProgressionAwardResult unshared = progression.Award(new PartyExperienceAward("kill", 500));
        Assert.False(unshared.IsAwarded);
        Assert.Equal("progression-award-unshared", unshared.Refusal!.Code);
        Assert.Equal(0, party.Members[0].Progression.Experience);
    }

    [Fact]
    public void An_award_moves_the_party_standing_through_the_party_s_own_component()
    {
        using PartyEntity party = PartyOfTwo();
        PartyProgression progression = new(new TestRule(), party);
        Assert.Equal(0, party.Reputation.Fame);
        Assert.Equal(0, party.Reputation.Reputation);

        // What an award does to the party's standing is the rule's answer, and the owner applies it to the
        // party's own reputation component rather than to a copy of its own: a quest worth four deeds moves
        // fame by four and leaves reputation alone, which is what the test's rule states.
        ProgressionAwardResult awarded = progression.Award(new PartyExperienceAward("quest", 400));
        Assert.Equal(4, awarded.Standing.Fame);
        Assert.Equal(0, awarded.Standing.Reputation);
        Assert.Equal(4, party.Reputation.Fame);
        Assert.Equal(0, party.Reputation.Reputation);
    }

    [Fact]
    public void Training_debits_the_purse_raises_the_level_and_refuses_when_the_fee_is_short()
    {
        using PartyEntity party = PartyOfTwo();
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestRule(), party);
        PartyServices hall = Hall(party, accounts, progression, fee: 30, cap: 4);
        PartyMember trainee = party.Members[0];
        int purse = party.Purse.Coins;

        // A member short of the experience a level takes is refused by name before anything is charged: the
        // curve the owner reads and the sentence it refuses with are one answer.
        ServiceResult short_ = hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.Equal("service-experience-short", short_.Code);
        Assert.Equal(purse, party.Purse.Coins);
        Assert.Equal(1, trainee.Progression.Level);

        // With the level banked, one step is one level, the purse pays the fee, and the experience is not
        // spent: a level is bought with what has been earned, not with the earning.
        progression.Award(new PartyExperienceAward("kill", 2000));
        long earned = trainee.Progression.Experience;
        ServiceResult trained = hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.True(trained.IsApplied);
        Assert.Equal(30, trained.Paid);
        Assert.Equal(purse - 30, party.Purse.Coins);
        Assert.Equal(2, trainee.Progression.Level);
        Assert.Equal(earned, trainee.Progression.Experience);

        // The fee is the party's before the level is its: a member who has earned the level but cannot pay
        // for it is refused by the settlement path, which names the shortfall, and the level does not rise.
        Assert.True(party.Purse.TryDebit(party.Purse.Coins));
        progression.Award(new PartyExperienceAward("quest", 2000));
        ServiceResult broke = hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.Equal("purse-short", broke.Code);
        Assert.Equal(2, trainee.Progression.Level);
    }

    [Fact]
    public void A_member_at_the_counter_s_ceiling_cannot_train()
    {
        using PartyEntity party = PartyOfTwo();
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestRule(), party);
        PartyServices hall = Hall(party, accounts, progression, fee: 10, cap: 2);
        PartyMember trainee = party.Members[0];

        // Content's own ceiling is where the counter stops: the ruleset's answer refuses the step by name
        // when the member already stands there, and the owner refuses it too when it is asked directly,
        // which is what keeps a level from being granted past the hall that sold it.
        progression.Award(new PartyExperienceAward("kill", 2000));
        Assert.True(hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0)).IsApplied);
        Assert.Equal(2, trainee.Progression.Level);

        ServiceResult capped = hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.False(capped.IsApplied);
        Assert.Equal("progression-training-capped", capped.Code);
        Assert.Equal(2, trainee.Progression.Level);

        ProgressionTrainingResult direct = progression.Train(
            trainee.Id,
            new ProgressionTrainingTerms("A hall", fee: 10, cap: 2));
        Assert.False(direct.IsTrained);
        Assert.Equal("progression-training-capped", direct.Refusal!.Code);
        Assert.Equal(2, trainee.Progression.Level);
    }

    [Fact]
    public void A_level_gives_exactly_what_the_rule_states_and_fills_both_pools()
    {
        using PartyEntity party = PartyOfTwo();
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestRule(), party);
        PartyServices hall = Hall(party, accounts, progression, fee: 10, cap: 10);
        PartyMember trainee = party.Members[0];
        trainee.Resources.TakeDamage(4);
        trainee.Resources.TrySpendSpellPoints(3);
        Assert.Equal(80, trainee.Resources.HitPoints.Maximum);
        Assert.Equal(20, trainee.Resources.SpellPoints.Maximum);

        progression.Award(new PartyExperienceAward("kill", 2000));
        ServiceResult trained = hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.True(trained.IsApplied);

        // The growth the rule answers is what the member's pools grow by, and the points it answers are what
        // the member holds. The values are the rule's own, so this proves the owner applies them rather than
        // deciding any of them.
        ProgressionTrainingResult step = progression.LastTraining!;
        Assert.Equal(2, step.Level);
        Assert.Equal(new ProgressionGrowth(5, 3, 7), step.Growth);
        Assert.Equal(85, trainee.Resources.HitPoints.Maximum);
        Assert.Equal(23, trainee.Resources.SpellPoints.Maximum);

        // A level is a fuller character as well as a larger one, which is what the donor's own training
        // does at the moment of the rise: both pools are filled, and the wound the member carried is gone
        // with them.
        Assert.Equal(85, trainee.Resources.HitPoints.Current);
        Assert.Equal(23, trainee.Resources.SpellPoints.Current);
        Assert.Equal(0, trainee.Resources.Deficit);
        Assert.Equal(7, trainee.Progression.SkillPoints);

        // What the step cost and what the counter is called travel into the report, so a panel reads the
        // fee the purse paid rather than a number it worked out.
        Assert.Equal(10, step.Fee);
        Assert.Equal("A hall", step.Counter);
    }

    [Fact]
    public void Spending_skill_points_is_the_owner_s_entry_and_charges_the_raise_with_them()
    {
        using PartyEntity party = PartyOfTwo();
        PartyProgression progression = new(new TestRule(), party, new TestSkills());
        PartyMember student = party.Members[0];
        student.Skills.Learn(Blades, new SkillTier(1));

        SkillRaiseResult nothing = progression.RaiseSkill(student.Id, Blades, levels: 1);
        Assert.False(nothing.IsRaised);
        Assert.Equal("insufficient-skill-points", nothing.Refusal!.Code);
        Assert.Equal(1, student.Skills.LevelOf(Blades));

        // The points a spend has to work with are a level's own grant, so the member trains one: the pool
        // the owner fills and the pool the owner charges are the same one.
        progression.Award(new PartyExperienceAward("quest", 2000));
        ProgressionTrainingResult step = progression.Train(
            student.Id,
            new ProgressionTrainingTerms("A hall", fee: 0, cap: 10));
        Assert.True(step.IsTrained);
        Assert.Equal(7, student.Progression.SkillPoints);

        // The price is the rule's, not the caller's: this test's own rule charges two points a level, so
        // two levels cost four of the seven the level granted and three are left.
        SkillRaiseResult raised = progression.RaiseSkill(student.Id, Blades, levels: 2);
        Assert.True(raised.IsRaised);
        Assert.Equal(4, raised.Points);
        Assert.Equal(3, raised.Remaining);
        Assert.Equal(3, student.Skills.LevelOf(Blades));
        Assert.Equal(3, student.Progression.SkillPoints);
    }

    [Fact]
    public void The_owner_is_the_only_source_that_moves_experience_a_level_or_a_skill_point()
    {
        // The transitions a member's progression keeps are internal to the kit and named in exactly one
        // place, so this scan is the second half of the proof: a source that named one of them outside the
        // owner would be a second writer even if it compiled. It reads the product's own sources rather
        // than what the compiler produced, in the style the kit's ambient-time scan uses, and it covers the
        // ruleset and the host as well — a second writer in either would be the same defect.
        string[] mutators =
        [
            "AwardExperience(",
            "SetLevel(",
            "GrantSkillPoints(",
            "SpendSkillPoints(",
            "SetClassRank(",
            // What a skill entry itself holds is moved by the same owner and by the counter's own lesson: a
            // raise spends points through the owner, and a lesson bought with coin grants a skill, moves its
            // rung, and sets the first level it is learned at. Those two are the only writers of an entry, so
            // a third one anywhere in the product fails here rather than becoming a skill that grew unasked.
            "RaiseLevel(",
            "SetTier(",
            // A rank and the class that goes with it are one fact, so the promotion that moves the rank moves
            // the class reference in the same act: a source that changed a member's class on its own would be
            // a character whose name and whose abilities disagree, which is exactly the drift one owner exists
            // to prevent.
            "ChangeClass(",
        ];

        string root = RepositoryRoot();
        string[] sources =
        [
            .. Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
        ];
        Assert.NotEmpty(sources);

        string owner = Path.Combine(root, "src", "PartyRpg.Kit", "Progression");
        string fields = Path.Combine(root, "src", "PartyRpg.Kit", "Party", "CharacterProgression.cs");
        // The class reference is held by the profile, which is content's statement of who a character is: the
        // promotion rewrites it through that owner's own operation, and the file that defines the operation
        // names it by definition rather than by use.
        string profile = Path.Combine(root, "src", "PartyRpg.Kit", "Party", "CharacterProfile.cs");
        string lessons = Path.Combine(root, "src", "PartyRpg.Kit", "Services", "PartyServices.cs");
        string entries = Path.Combine(root, "src", "PartyRpg.Kit", "Party", "CharacterSkills.cs");
        foreach (string source in sources)
        {
            if (source.StartsWith(owner, StringComparison.Ordinal)
                || string.Equals(source, fields, StringComparison.Ordinal)
                || string.Equals(source, profile, StringComparison.Ordinal)
                || string.Equals(source, lessons, StringComparison.Ordinal)
                || string.Equals(source, entries, StringComparison.Ordinal))
            {
                continue;
            }
            string text = File.ReadAllText(source);
            foreach (string mutator in mutators)
            {
                Assert.False(
                    text.Contains(mutator, StringComparison.Ordinal),
                    $"{Path.GetFileName(source)} names '{mutator}': experience, a level, and a skill point are moved by the progression owner and by nothing else.");
            }
        }
    }

    private static readonly SkillId Blades = new("blades");
    private static readonly ConditionId LaidOut = new("laid-out");

    /// <summary>Two members, so an award's division is visible: a share is half of what was earned.</summary>
    private static PartyEntity PartyOfTwo() =>
        new PartyEntityFactory().Create(new PartyCreation(
            [Member("Ann"), Member("Borin")],
            coins: 200,
            foodPortions: 10,
            reputation: 0,
            fame: 0));

    private static MemberCreation Member(string name) => new(new PartyMemberSeed(
        name,
        new RaceId("testfolk"),
        new ClassId("fighter"),
        [new AttributeScore(new AttributeId("vigour"), 12)],
        skills: [],
        spells: [],
        experience: 0,
        level: 1,
        skillPoints: 0,
        classRank: 1,
        conditions: [],
        hitPoints: ResourcePool.Full(80),
        spellPoints: ResourcePool.Full(20)));

    /// <summary>What one placement's death is worth, which the ruleset reads from the creature's own row.</summary>
    private static long Worth(PlacementDefinition placement) =>
        placement.Source.GetInt32("experience") ?? 0;

    /// <summary>One creature a fight read as down, as the fight reports it.</summary>
    private static FallenCreature Fallen(string id, int experience) => new(
        Placement("monster", id, experience),
        PlacePose.Origin,
        $"A creature {id}");

    private static PlacementDefinition Placement(string kind, string id, int experience) => new(
        new PlacementContentId(kind, id),
        "placements",
        0,
        PlacePose.Origin,
        new ContentEntry(id, JsonDocument.Parse(
            $$"""{ "id": "{{id}}", "kind": "{{kind}}", "experience": {{experience}} }""").RootElement));

    /// <summary>A training hall of the test's own: one offer whose fee and ceiling the test states.</summary>
    private static PartyServices Hall(PartyEntity party, PartyResourceLedger accounts, PartyProgression progression, int fee, int cap)
    {
        HallRule rule = new(fee, cap);
        PartyServices services = new(rule, party, accounts, clock: null, progression);
        Assert.True(services.Open(rule.Service).IsApplied);
        return services;
    }

    /// <summary>
    /// The policy every test here runs on: a flat curve of a thousand experience a level, an equal division
    /// among the members who are standing, a growth of five hit points, three spell points, and seven skill
    /// points a level, and a fame of one per hundred experience earned.
    /// </summary>
    private sealed class TestRule : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 1000;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division)
        {
            List<PartyMember> earning = [.. division.Party.Members.Where(member => !member.Conditions.Has(LaidOut))];
            if (earning.Count == 0) return [];
            long each = division.Amount / earning.Count;
            return [.. earning.Select(member => new ProgressionShare(member.Id, member.Profile.Name, each))];
        }

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => new(5, 3, 7);

        public ProgressionStanding Standing(ProgressionStandingRequest request) =>
            request.Event == ProgressionEventKind.Award
                ? new ProgressionStanding(0, (int)(request.Amount / 100))
                : ProgressionStanding.None;
    }

    /// <summary>
    /// The skill policy these tests run on: one skill content declares, a ceiling generous enough that the
    /// raise itself is what is under test, and a flat two points a level.
    /// </summary>
    /// <remarks>
    /// The price is deliberately this rule's own rather than the game's — the numbers are the ruleset's, and
    /// the kit's suite is checking that the owner asks for them rather than that any particular game charges
    /// what it charges.
    /// </remarks>
    private sealed class TestSkills : ISkillRule
    {
        public SkillCatalog Catalog { get; } = new([new SkillDefinition(new SkillId("blades"), SkillBlock.Weapon)]);

        public SkillCeiling Ceiling(PartyMember member, SkillId skill) => new(60, new SkillTier(4));

        public int RaiseCost(SkillEntry skill, int levels) => 2 * levels;

        public string TierName(SkillTier tier) => tier.Value switch
        {
            0 => "untrained",
            1 => "basic",
            2 => "expert",
            3 => "master",
            _ => "grand master",
        };
    }

    /// <summary>
    /// The bodies owner a fight reports to, which is the kit's own ground behind the observer the fight asks.
    /// </summary>
    /// <remarks>
    /// The ground is the kit's real owner of what the fallen left; what this adds is the observer shape a
    /// fight asks, which is the same two lines the loot owner's own ruleset adapter writes.
    /// </remarks>
    private sealed class Bodies : IFallenCreatureObserver
    {
        private readonly CorpseGround _ground = new();

        public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen) =>
            _ground.Observe(place, fallen);
    }

    /// <summary>One counter that offers nothing but training, priced and capped as the test states.</summary>
    private sealed class HallRule : IServiceRule
    {
        private readonly int _fee;
        private readonly int _cap;

        internal HallRule(int fee, int cap)
        {
            _fee = fee;
            _cap = cap;
            Service = new ServiceDefinition(
                new ServiceId("hall"),
                new ServiceKind("training"),
                "A hall",
                operations: [ServiceOperationKind.Train],
                stock: [],
                lessons: []);
        }

        internal ServiceDefinition Service { get; }

        public ServiceDefinition? Describe(ServiceTargetRequest request) =>
            string.Equals(request.Placement.Content.Id, Service.Id.Value, StringComparison.Ordinal) ? Service : null;

        public IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request) => [];

        public IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request) => [];

        public IReadOnlyList<ServiceOffer> Offers(ServiceOfferRequest request) =>
            [new ServiceOffer(ServiceOfferKind.Training, "Training", Value: 0, Amount: 1, Limit: _cap)];

        public IReadOnlyList<string> Access(ServiceAccessRequest request) => [];

        /// <summary>Whether the member has banked the level, which is the one condition a hall judges.</summary>
        public ServiceEligibility Judge(ServiceEligibilityRequest request)
        {
            if (request.Operation != ServiceOperationKind.Train) return ServiceEligibility.Allowed;
            PartyMember member = request.Party.Member(request.Member);
            int level = member.Progression.Level;
            return member.Progression.Experience >= 1000
                ? ServiceEligibility.Allowed
                : ServiceEligibility.Refused(
                    "service-experience-short",
                    $"{member.Profile.Name} needs {1000 - member.Progression.Experience} more experience to train to level {level + 1}.");
        }

        public ServiceQuote Quote(ServiceQuoteRequest request) =>
            request.Operation == ServiceOperationKind.Train
                ? ServiceQuote.Charging(_fee, _fee)
                : ServiceQuote.Free;
    }

    /// <summary>The repository root above the test assembly, which is where the product's sources are.</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"The repository root is not above {AppContext.BaseDirectory}.");
    }
}
