using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Services;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Magic: the catalog content declares, a spell book bought at a counter and consumed into a spellbook, and
/// the one workflow that casts a spell — its refusals, the points it pays, and the effect path it hands the
/// spell to.
/// </summary>
/// <remarks>
/// <para>
/// Every number and every rule here is the test's own, which is the point of the seam: the kit holds no
/// school, no cost, no ladder, and no effect, so the same mechanism serves a test that invents them and a
/// ruleset that owns the shipped ones. Two schools and four spells are enough to prove every answer the
/// mechanism asks for.
/// </para>
/// <para>
/// The effect path is a recording double rather than a real one, because what this suite proves is the
/// delivery: which spell, by which caster, at what target, after what was paid, and what the seam's own
/// answer became. What an effect does with the casting is the effect owner's suite's subject.
/// </para>
/// </remarks>
public sealed class MagicTests
{
    private static readonly SkillId FireSkill = new("Fire");
    private static readonly SkillId MindSkill = new("Mind");
    private static readonly SpellId FireBolt = new("1");
    private static readonly SpellId Fireball = new("2");
    private static readonly SpellId Cure = new("3");
    private static readonly SpellId Torch = new("4");

    [Fact]
    public void A_catalog_reads_the_rows_content_declares_with_their_schools_and_answers_one_it_does_not()
    {
        SpellCatalog catalog = TestSpells.Catalog;

        Assert.Equal(4, catalog.Count);
        Assert.Equal(["fire", "mind"], catalog.Schools());
        Assert.Equal(3, catalog.CountOf("fire"));
        Assert.Equal(1, catalog.CountOf("mind"));
        Assert.Equal(0, catalog.CountOf("water"));

        // Content's own order is what a school's ladder runs in, so the rows come back as the content
        // declared them rather than in an order a reader chose.
        Assert.Equal([FireBolt, Fireball, Torch], [.. catalog.OfSchool("fire").Select(spell => spell.Id)]);

        Assert.True(catalog.Declares(FireBolt));
        Assert.False(catalog.Declares(new SpellId("99")));

        // A spell nothing declares reads as an empty row rather than throwing: a screen, a save, and a rule
        // can all name one, and the answer that lets a caller refuse by name is the useful one.
        SpellDefinition missing = catalog.Read(new SpellId("99"));
        Assert.Equal("99", missing.Id.Value);
        Assert.Equal(string.Empty, missing.School);
        Assert.Equal(SkillTier.None, missing.Tier);

        // A catalog that declares one spell twice is a defect rather than a merge: two rows claiming one
        // identity would make a spell's school and cost ambiguous.
        Assert.Throws<ArgumentException>(() => new SpellCatalog([TestSpells.Bolt, TestSpells.Bolt]));
    }

    [Fact]
    public void A_book_bought_at_a_counter_is_consumed_into_the_spellbook_and_the_fee_settles()
    {
        using PartyEntity party = Party(withFire: true);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(BookLesson());
        PartyServices counter = Serve(rule, party, accounts);

        int before = party.Purse.Coins;
        ServiceResult learned = counter.Transact(new ServiceCommand(ServiceCommandKind.Teach, FireBolt.Value, Member: 0));

        Assert.True(learned.IsApplied, learned.Message);
        Assert.Equal(50, learned.Paid);
        Assert.Equal(before - 50, party.Purse.Coins);
        Assert.True(party.Members[0].Spells.Knows(FireBolt));
        Assert.Contains("learns Fire Bolt", learned.Message, StringComparison.Ordinal);

        // The book is consumed by the learning rather than carried: nothing landed in the pack, so there is
        // no second thing to hold and no second way to learn the same spell.
        Assert.Empty(party.Inventory.Items);

        // The same lesson is not sold twice, and the refusal is the rule's own answer rather than the
        // mechanism inventing one.
        ServiceResult again = counter.Transact(new ServiceCommand(ServiceCommandKind.Teach, FireBolt.Value, Member: 0));
        Assert.False(again.IsApplied);
        Assert.Equal("spell-already-known", again.Code);
        Assert.Equal(before - 50, party.Purse.Coins);
    }

    [Fact]
    public void A_book_is_refused_for_a_member_the_rule_will_not_teach_and_moves_nothing()
    {
        using PartyEntity party = Party(withFire: false);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(BookLesson());
        PartyServices counter = Serve(rule, party, accounts);

        int before = party.Purse.Coins;
        ServiceResult refused = counter.Transact(new ServiceCommand(ServiceCommandKind.Teach, FireBolt.Value, Member: 0));

        Assert.False(refused.IsApplied);
        Assert.Equal("spell-school-missing", refused.Code);
        Assert.Contains("Fire", refused.Message, StringComparison.Ordinal);
        Assert.Equal(before, party.Purse.Coins);
        Assert.False(party.Members[0].Spells.Knows(FireBolt));
    }

    [Fact]
    public void A_cast_pays_the_points_the_rule_states_and_hands_the_spell_to_the_effect_path()
    {
        using PartyEntity party = Party(withFire: true);
        RecordingEffects effects = new();
        Spellcasting casting = new(party, new TestSpells(), effects);
        party.Members[0].Spells.Learn(Torch);

        Assert.Equal(1, casting.CostFor(party.Members[0], TestSpells.Catalog.Read(Torch)));
        int before = party.Members[0].Resources.SpellPoints.Current;

        SpellCastResult result = casting.Cast(new SpellCastRequest(0, Torch, Target: string.Empty));

        Assert.True(result.IsCast);
        Assert.Equal(1, result.Cost);
        Assert.Equal(before - 1, party.Members[0].Resources.SpellPoints.Current);

        // What the effect path was handed is the whole application: the spell, the caster with its own
        // state, the fight when there is one, and the actor the spell was aimed at. A spell whose aim is the
        // caster resolves to the caster without a target being named.
        SpellApplication application = Assert.Single(effects.Applications);
        Assert.Equal(Torch, application.Spell.Id);
        Assert.Equal("fire", application.Spell.School);
        Assert.Equal("Nyx", application.Caster.Profile.Name);
        Assert.Equal(CombatantId.Of(party.Members[0].Id), application.CasterId);
        Assert.Equal(CombatantId.Of(party.Members[0].Id), application.Target);
        Assert.Equal("light", application.Spell.Effect);
        Assert.Null(application.Fight);

        // What the seam answered is what the cast reports, unchanged and uninterpreted.
        Assert.NotNull(result.Outcome);
        Assert.True(result.Outcome.IsExpressed);
        Assert.Equal("light", result.Outcome.Effect);
        Assert.Contains("Nyx casts Torch for 1 spell point(s)", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cast_is_refused_by_name_for_every_case_the_mechanism_can_meet()
    {
        using PartyEntity party = Party(withFire: true);
        RecordingEffects effects = new();
        Spellcasting casting = new(
            party,
            new TestSpells(),
            effects,
            fight: null,
            rungName: tier => tier.Value == 2 ? "expert" : "basic");
        PartyMember caster = party.Members[0];
        caster.Spells.Learn(FireBolt);

        // No such spell: content declares nothing by that identity.
        Assert.Equal("spell-unknown", casting.Cast(new SpellCastRequest(0, new SpellId("99"), "")).Code);

        // No such spell in the spellbook: the character never learned it.
        Assert.Equal("spell-not-known", casting.Cast(new SpellCastRequest(0, Cure, "")).Code);

        // Mastery too low: Fireball asks for the second rung of the fire ladder and the caster stands at
        // the first. The refusal names both rungs in the words the skill owner supplied.
        caster.Spells.Learn(Fireball);
        SpellCastResult mastery = casting.Cast(new SpellCastRequest(0, Fireball, ""));
        Assert.Equal("spell-mastery-too-low", mastery.Code);
        Assert.Contains("expert", mastery.Message, StringComparison.Ordinal);
        Assert.Contains("basic", mastery.Message, StringComparison.Ordinal);

        // Not enough points: the pool is spent down and the cast is refused with the shortfall named, and
        // nothing is spent by the refusal.
        while (caster.Resources.TrySpendSpellPoints(1))
        {
        }

        SpellCastResult short1 = casting.Cast(new SpellCastRequest(0, FireBolt, ""));
        Assert.Equal("spell-points-short", short1.Code);
        Assert.Equal(0, caster.Resources.SpellPoints.Current);

        // No valid target: the spell is aimed at an opponent and this session holds no fight to find one in.
        caster.Resources.RestoreSpellPoints(10);
        SpellCastResult noTarget = casting.Cast(new SpellCastRequest(0, FireBolt, Target: string.Empty));
        Assert.Equal("spell-target-missing", noTarget.Code);

        SpellCastResult badTarget = casting.Cast(new SpellCastRequest(0, FireBolt, Target: "actor:7"));
        Assert.Equal("spell-target-invalid", badTarget.Code);

        // Caster cannot act: the fight's own gate, asked before anything is paid. The caster attacks first,
        // which leaves it recovering, and the cast is refused for the recovery it still owes.
        caster.Resources.RestoreSpellPoints(10);
        caster.Spells.Learn(Torch);
        CombatState fight = new(new TestCombat(), party);
        fight.Step();
        Assert.True(fight.Order(new AttackOrder(CombatantId.Of(caster.Id), AttackKind.Melee, null)).IsApplied);
        Spellcasting paced = new(party, new TestSpells(), new MightAndMagic7StyleEffects(), fight);
        int points = caster.Resources.SpellPoints.Current;
        SpellCastResult recovering = paced.Cast(new SpellCastRequest(0, Torch, Target: string.Empty));
        Assert.Equal("spell-caster-cannot-act", recovering.Code);
        Assert.Equal(points, caster.Resources.SpellPoints.Current);

        // A session with no effect path resolves and refuses rather than spending a point on nothing.
        Spellcasting pathless = new(party, new TestSpells());
        Assert.Equal("spell-no-effect-path", pathless.Cast(new SpellCastRequest(0, Torch, "")).Code);

        // A member the party does not have is refused before anything else is asked.
        Assert.Equal("spell-no-such-member", casting.Cast(new SpellCastRequest(9, FireBolt, "")).Code);
    }

    [Fact]
    public void The_seam_is_asked_whether_the_casting_may_go_ahead_before_anything_is_paid()
    {
        using PartyEntity party = Party(withFire: true);
        RecordingEffects effects = new()
        {
            Judge = SpellRefusal.CannotAct("Nyx", "what is acting on them leaves them unable to cast"),
        };

        Spellcasting casting = new(party, new TestSpells(), effects);
        party.Members[0].Spells.Learn(Torch);
        int before = party.Members[0].Resources.SpellPoints.Current;

        SpellCastResult refused = casting.Cast(new SpellCastRequest(0, Torch, ""));

        Assert.False(refused.IsCast);
        Assert.Equal("spell-caster-cannot-act", refused.Code);
        Assert.Equal(before, party.Members[0].Resources.SpellPoints.Current);
        Assert.Empty(effects.Applications);
    }

    [Fact]
    public void A_spell_whose_effect_the_seam_expresses_nothing_for_is_still_cast_and_says_so()
    {
        using PartyEntity party = Party(withFire: true);
        RecordingEffects effects = new() { Expressed = false };
        Spellcasting casting = new(party, new TestSpells(), effects);
        party.Members[0].Spells.Learn(Torch);
        int before = party.Members[0].Resources.SpellPoints.Current;

        SpellCastResult result = casting.Cast(new SpellCastRequest(0, Torch, ""));

        // The casting mechanism carries every spell alike: what a build expresses of an effect is the
        // effect owner's answer, and a cast that changed nothing says exactly that rather than reporting a
        // success nobody had.
        Assert.True(result.IsCast);
        Assert.Equal(before - 1, party.Members[0].Resources.SpellPoints.Current);
        Assert.NotNull(result.Outcome);
        Assert.False(result.Outcome.IsExpressed);
        Assert.Equal("spell-effect-unexpressed", result.Outcome.Code);
        Assert.Contains("nothing changed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_pool_the_panel_reads_is_the_rules_answer_for_the_class_and_the_scores()
    {
        using PartyEntity party = Party(withFire: true);
        Spellcasting casting = new(party, new TestSpells(), new RecordingEffects());
        party.Members[0].Spells.Learn(FireBolt);
        party.Members[0].Spells.Learn(Torch);

        MagicSnapshot magic = MagicSnapshot.From(casting, skills: null);

        Assert.True(magic.Available);
        Assert.Equal(2, magic.Members.Count);
        SpellMemberSnapshot member = magic.Members[0];
        Assert.Equal("Nyx", member.Name);
        Assert.Equal("mage", member.Class);
        Assert.Equal(6, member.SpellPointsMax);
        Assert.Equal(6, member.SpellPoints);
        Assert.Equal(string.Empty, member.QuickSpell);

        // Every spell the member knows is published with what casting it costs that caster, what it is
        // aimed at, and the rung it asks for, so the screen computes nothing.
        Assert.Equal(2, member.Spells.Count);
        SpellRowSnapshot row = Assert.Single(member.Spells, spell => spell.Spell == FireBolt.Value);
        Assert.Equal(FireBolt.Value, row.Spell);
        Assert.Equal("Fire Bolt", row.Name);
        Assert.Equal("fire", row.School);
        Assert.Equal(1, row.TierRung);
        Assert.Equal(3, row.Cost);
        Assert.Equal("foe", row.Targeting);
        Assert.Equal("damage", row.Effect);

        // A session with no casting owner publishes that it holds no magic rather than an empty spellbook.
        Assert.False(MagicSnapshot.None.Available);
        Assert.Empty(MagicSnapshot.None.Members);
    }

    [Fact]
    public void The_quick_spell_is_the_characters_own_slot_and_only_holds_what_they_know()
    {
        using PartyEntity party = Party(withFire: true);
        PartyMember caster = party.Members[0];
        caster.Spells.Learn(FireBolt);

        // A spell the character has not learned cannot be its quick spell: the slot is a spell its owner
        // can cast, and a key that refused every press would be a slot holding nothing usable.
        Assert.Throws<ArgumentException>(() => caster.Spells.SetQuickSpell(Cure));

        Assert.True(caster.Spells.SetQuickSpell(FireBolt));
        Assert.Equal(FireBolt, caster.Spells.QuickSpell);
        Assert.False(caster.Spells.SetQuickSpell(FireBolt));

        // What is forgotten leaves the slot with it.
        Assert.True(caster.Spells.Forget(FireBolt));
        Assert.Null(caster.Spells.QuickSpell);
    }

    [Fact]
    public void A_quick_spell_survives_a_save_and_a_restore()
    {
        using PartyEntity party = Party(withFire: true);
        party.Members[0].Spells.Learn(FireBolt);
        party.Members[0].Spells.SetQuickSpell(FireBolt);

        PartySave save = party.Capture();
        Assert.Equal(FireBolt, save.Members[0].Seed.QuickSpell);

        using PartyEntity restored = new PartyEntityFactory().Restore(save);
        Assert.Equal(FireBolt, restored.Members[0].Spells.QuickSpell);
        Assert.Equal([FireBolt], restored.Members[0].Spells.Known);
    }

    /// <summary>A party of two: a caster who may hold the fire skill, and a companion who may not.</summary>
    private static PartyEntity Party(bool withFire) =>
        new PartyEntityFactory().Create(new PartyCreation(
            [
                Member("Nyx", withFire ? [new SkillEntry(FireSkill, 2, new SkillTier(1), 0)] : [], []),
                Member("Borin", [], []),
            ],
            coins: 200,
            foodPortions: 10,
            reputation: 0,
            fame: 0));

    private static MemberCreation Member(string name, IReadOnlyList<SkillEntry> skills, IReadOnlyList<SpellId> spells) =>
        new(new PartyMemberSeed(
            name,
            new RaceId("testfolk"),
            new ClassId("mage"),
            [new AttributeScore(new AttributeId("Intellect"), 12)],
            skills,
            spells,
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(20),
            spellPoints: ResourcePool.Full(6)));

    /// <summary>Opens a counter that sells one spell book and returns it to the caller.</summary>
    private static PartyServices Serve(ShopRule rule, PartyEntity party, PartyResourceLedger accounts)
    {
        PartyServices services = new(rule, party, accounts, clock: null);
        Assert.True(services.Open(rule.Service).IsApplied);
        return services;
    }

    /// <summary>The one lesson this counter sells: a book of Fire Bolt, priced at its own value.</summary>
    private static ServiceDefinition BookLesson() =>
        new(
            new ServiceId("guild-of-fire"),
            new ServiceKind("Fire Guild"),
            "The Guild of Fire",
            [ServiceOperationKind.Teach],
            [],
            [new ServiceLesson(ServiceLessonKind.Spell, FireBolt.Value, amount: 1, value: 50, "Fire Bolt")]);

    /// <summary>
    /// This suite's rules: two schools, four spells, a pool of three points a level plus the intellect's
    /// own twelve, and the learning rule a book is judged against.
    /// </summary>
    private sealed class TestSpells : ISpellRule
    {
        internal static readonly SpellDefinition Bolt = new(
            FireBolt,
            "Fire Bolt",
            "fire",
            FireSkill,
            new SkillTier(1),
            Cost: 3,
            SpellTargeting.Foe,
            "damage");

        private static readonly SpellDefinition Ball = new(
            Fireball,
            "Fireball",
            "fire",
            FireSkill,
            new SkillTier(2),
            Cost: 5,
            SpellTargeting.Foe,
            "damage");

        private static readonly SpellDefinition Mend = new(
            Cure,
            "Cure",
            "mind",
            MindSkill,
            new SkillTier(1),
            Cost: 1,
            SpellTargeting.Ally,
            "healing");

        private static readonly SpellDefinition Light = new(
            Torch,
            "Torch",
            "fire",
            FireSkill,
            new SkillTier(1),
            Cost: 1,
            SpellTargeting.Caster,
            "light");

        internal static SpellCatalog Catalog { get; } = new([Bolt, Ball, Light, Mend]);

        SpellCatalog ISpellRule.Catalog => Catalog;

        public int SpellPointCapacity(PartyMember member) =>
            3 * member.Progression.Level + (member.Attributes.TryGet(Intellect, out int score) ? score / 4 : 0);

        public int CostFor(PartyMember member, SpellDefinition spell) => spell.Cost;

        public SpellRefusal? MayLearn(PartyMember member, SpellDefinition spell)
        {
            if (member.Spells.Knows(spell.Id)) return SpellRefusal.AlreadyKnown(member.Profile.Name, spell.Name);
            return member.Skills.LevelOf(spell.SchoolSkill) > 0
                ? null
                : SpellRefusal.SchoolMissing(member.Profile.Name, spell.Name, spell.SchoolSkill.Value);
        }

        private static readonly AttributeId Intellect = new("Intellect");
    }

    /// <summary>
    /// The effect path this suite records into: every casting handed over is kept, and what it answers is
    /// what a test states.
    /// </summary>
    private sealed class RecordingEffects : ISpellEffectRule
    {
        internal List<SpellApplication> Applications { get; } = [];

        /// <summary>Whether the seam expresses anything for the effects it is handed.</summary>
        internal bool Expressed { get; init; } = true;

        /// <summary>What the seam answers when it is asked whether the casting may go ahead.</summary>
        internal SpellRefusal? Judge { get; init; }

        SpellRefusal? ISpellEffectRule.Judge(SpellApplication application) => Judge;

        public SpellApplicationOutcome Apply(SpellApplication application)
        {
            Applications.Add(application);
            return Expressed
                ? SpellApplicationOutcome.Expressed(application.Spell.Effect, "the effect was applied.")
                : SpellApplicationOutcome.Unexpressed(application.Spell.Effect, "nothing changed.");
        }
    }

    /// <summary>A counter that sells one spell book, judging the spell the way a ruleset would.</summary>
    private sealed class ShopRule : IServiceRule
    {
        private readonly TestSpells _spells = new();

        internal ShopRule(ServiceDefinition service) => Service = service;

        internal ServiceDefinition Service { get; }

        public ServiceDefinition? Describe(ServiceTargetRequest request) =>
            string.Equals(request.Placement.Content.Id, Service.Id.Value, StringComparison.Ordinal) ? Service : null;

        public IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request) => [];

        public IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request) => Service.Lessons;

        public IReadOnlyList<ServiceOffer> Offers(ServiceOfferRequest request) => [];

        public IReadOnlyList<string> Access(ServiceAccessRequest request) => [];

        public ServiceEligibility Judge(ServiceEligibilityRequest request)
        {
            if (request.Subject.Lesson is not { Kind: ServiceLessonKind.Spell } lesson) return ServiceEligibility.Allowed;
            PartyMember learner = request.Party.Member(request.Member);
            SpellDefinition spell = TestSpells.Catalog.Read(new SpellId(lesson.Subject));
            return ((ISpellRule)_spells).MayLearn(learner, spell) is { } refused
                ? ServiceEligibility.Refused(refused.Code, refused.Message)
                : ServiceEligibility.Allowed;
        }

        public ServiceQuote Quote(ServiceQuoteRequest request) =>
            ServiceQuote.Charging(request.Subject.Value, request.Subject.Value);
    }

    /// <summary>A fight's answers, stated by this suite: everybody swings, everybody recovers a second.</summary>
    private sealed class TestCombat : ICombatRule, ICombatResolutionRule
    {
        public string NameOf(CombatSubject subject) => subject.Member?.Profile.Name ?? "a creature";

        public Hostility NatureOf(CombatSubject subject) => Hostility.Peaceful;

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        public Time.GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) =>
            Time.GameDuration.FromSeconds(1);

        public Time.GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => Time.GameDuration.None;

        public double ReachOf(CombatSubject subject, AttackKind kind) => 100;

        public IAttackRolls? RollsFor(CombatSubject attacker, string key) => null;

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) =>
            new(HitChance.Always, new DamageKindId("Phys"), DamageRoll.Flat(0), Resistance.Of(0));

        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls) => damage;

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) => null;

        public bool CanAct(CombatSubject subject) => true;

        public int HitPointsOf(CombatSubject subject) => 10;
    }

    /// <summary>
    /// An effect path that judges exactly what this game's own does — the fight's two gates — so the
    /// recovering-caster refusal is proved against the same rule the product uses.
    /// </summary>
    private sealed class MightAndMagic7StyleEffects : ISpellEffectRule
    {
        public SpellRefusal? Judge(SpellApplication application)
        {
            if (application.Fight is { } fight && fight.Find(application.CasterId) is { } caster && !caster.IsReady)
            {
                return SpellRefusal.CannotAct(caster.Name, "it is still recovering");
            }

            return null;
        }

        public SpellApplicationOutcome Apply(SpellApplication application) =>
            SpellApplicationOutcome.Unexpressed(application.Spell.Effect, "nothing changed.");
    }
}
