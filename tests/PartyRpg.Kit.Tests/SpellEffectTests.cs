using System.Text.RegularExpressions;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Time;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The mechanisms a spell's effect is applied through: the effects a cast leaves running on the party with
/// the deadline the one clock ends them at, and what a panel reads of them.
/// </summary>
/// <remarks>
/// <para>
/// What this suite proves is the kit's half of the effect seam: that a duration is a deadline on the session's
/// clock and never a count in a step, that the party is the state every reader consults, that a dispelling
/// ends what a spell left and nothing a counter sold, and that a panel shows what a cast changed and where a
/// spell may be pointed without the mechanism knowing a single spell.
/// </para>
/// <para>
/// The categories are the ruleset's and the numbers are its own, so the effects here are named by the test:
/// the mechanism carries an identity and a magnitude it never interprets, which is what the source scan at the
/// end of this file holds it to.
/// </para>
/// </remarks>
public sealed class SpellEffectTests
{
    private static readonly EffectId Ward = new("test.ward");
    private static readonly EffectId Light = new("test.light");

    [Fact]
    public void An_effect_a_cast_leaves_runs_until_the_clocks_own_deadline_reaches_it()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        RunningSpellEffects running = new(party, clock);
        clock.ScheduleEvery(GameDuration.FromHours(1));

        // Two hours of ward, applied at nine in the morning: the party carries it, the panel says when it
        // ends, and the moment is the clock's own arithmetic rather than a count kept beside it.
        RunningSpellEffect ward = running.Start(Ward, magnitude: 12, GameDuration.FromHours(2));

        Assert.True(running.IsRunning(Ward));
        Assert.Equal(12, party.Effects.MagnitudeOf(Ward));
        Assert.Equal(new GameDate(1168, 1, 1, 11, 0), ward.EndsAt);

        // An hour of game time later it is still running, and the advance that reaches the deadline is what
        // ends it: nothing here counted an update, so a party that stood still and one that crossed the world
        // lose the ward at the same moment.
        clock.Advance(GameDuration.FromHours(1));
        (_, _, _, _, IReadOnlyList<DeadlineDue> _) = Split(clock.Advance(GameDuration.None));
        _ = Split(clock.Advance(GameDuration.None));
        Assert.True(running.IsRunning(Ward));

        ClockAdvance reached = clock.Advance(GameDuration.FromHours(1));
        Assert.NotEmpty(reached.Due);
        running.Observe(reached);

        Assert.False(running.IsRunning(Ward));
        Assert.False(party.Effects.Has(Ward));
        Assert.Empty(running.Running);
    }

    [Fact]
    public void Applying_an_effect_again_replaces_its_magnitude_and_its_deadline_rather_than_stacking()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        RunningSpellEffects running = new(party, clock);

        running.Start(Ward, magnitude: 4, GameDuration.FromHours(1));
        RunningSpellEffect stronger = running.Start(Ward, magnitude: 9, GameDuration.FromHours(3));

        // One effect per identity, at the stronger reading, and the earlier deadline is dropped with it: a
        // second casting must not leave the first one's end standing over the new one.
        Assert.Single(party.Effects.Active);
        Assert.Equal(9, party.Effects.MagnitudeOf(Ward));
        Assert.Equal(new GameDate(1168, 1, 1, 12, 0), stronger.EndsAt);
        Assert.Single(running.Running);

        ClockAdvance hour = clock.Advance(GameDuration.FromHours(1));

        // The hour the first casting would have ended in passes and the newer ward stands: the deadline that
        // was replaced is not still waiting to end it.
        running.Observe(hour);
        Assert.True(running.IsRunning(Ward));
    }

    [Fact]
    public void An_effect_with_no_clock_to_end_it_says_so_rather_than_inventing_a_length()
    {
        using PartyEntity party = Party();
        RunningSpellEffects running = new(party);

        RunningSpellEffect ward = running.Start(Ward, magnitude: 3, GameDuration.FromHours(1));

        // A product that keeps no time still carries the ward and still reads it; what it cannot state is when
        // the ward ends, and it says that instead of counting down in something that is not the game's clock.
        Assert.Null(ward.EndsAt);
        Assert.True(running.IsRunning(Ward));
        Assert.True(running.End(Ward));
        Assert.False(running.IsRunning(Ward));
    }

    [Fact]
    public void A_dispelling_ends_what_a_spell_left_running_and_leaves_what_a_counter_sold()
    {
        using PartyEntity party = Party();
        RunningSpellEffects running = new(party, Clock());
        running.Start(Ward, magnitude: 5, GameDuration.FromHours(1));
        running.Start(Light, magnitude: 2, GameDuration.FromHours(1));

        // Something a counter sold is carried on the party too — a passage to a place is the same shape of
        // state — and burning it would be a dispelling that took a player's ticket rather than their magic.
        EffectId passage = new("passage:2");
        party.Effects.Apply(new PartyEffect(passage, 3));

        IReadOnlyList<RunningSpellEffect> ended = running.EndAll();

        Assert.Equal(2, ended.Count);
        Assert.False(party.Effects.Has(Ward));
        Assert.False(party.Effects.Has(Light));
        Assert.True(party.Effects.Has(passage));
        Assert.Equal(3, party.Effects.MagnitudeOf(passage));
    }

    [Fact]
    public void The_panel_reads_what_a_cast_changed_and_what_the_effect_path_offers()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        RecordingEffects effects = new(party, clock);
        Spellcasting casting = new(party, new TestSpells(), effects);
        party.Members[0].Spells.Learn(Travel);

        effects.Expressed = SpellApplicationOutcome.Expressed(
            "test.effect",
            "the effect was applied.",
            [new SpellEffectFact("hitPoints", "Nyx 12/20")]);
        effects.Offered = [new SpellAim("2", "Cave", "place")];

        SpellCastResult result = casting.Cast(new SpellCastRequest(0, Travel, "2"));

        // The screen named no actor: the word travels to the effect path unread, and the cast reports it as
        // what the spell was aimed at.
        Assert.True(result.IsCast);
        Assert.Equal("2", result.Target);
        Assert.Equal("2", Assert.Single(effects.Applications).TargetName);

        MagicSnapshot magic = MagicSnapshot.From(casting);

        // What the cast changed, what is running, what the party sees by, and what the spell may be pointed
        // at: four answers the effect path owns, published without the panel or the mechanism reading one of
        // them as a rule.
        SpellFactSnapshot fact = Assert.Single(magic.Facts);
        Assert.Equal("hitPoints", fact.Name);
        Assert.Equal("Nyx 12/20", fact.Value);

        SpellRunningSnapshot running = Assert.Single(magic.Running);
        Assert.Equal("test.ward", running.Effect);
        Assert.Equal(7, running.Magnitude);
        Assert.Equal("1168-01-01 10:00", running.EndsAt);
        Assert.Equal("light", magic.Sight);

        SpellRowSnapshot row = Assert.Single(magic.Members[0].Spells);
        SpellAimSnapshot aim = Assert.Single(row.Aims);
        Assert.Equal("2", aim.Aim);
        Assert.Equal("Cave", aim.Name);
        Assert.Equal("place", aim.Kind);
    }

    [Fact]
    public void The_kits_effect_application_names_no_spell_identity_and_branches_on_none()
    {
        // What a spell's effect is belongs to the ruleset, which applies it by category behind the seam. The
        // kit's own effect application therefore names no spell and compares no effect: it carries an identity
        // and hands it back, which is what keeps ninety-nine spells from becoming ninety-nine code paths.
        string magic = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit", "Magic");
        string effects = Path.Combine(magic, "Effects");
        string[] applied =
        [
            .. Directory.EnumerateFiles(effects, "*.cs", SearchOption.AllDirectories)
                .Where(file => !Ignored(file)),
        ];
        Assert.NotEmpty(applied);

        Regex namesASpell = new(@"new\s+SpellId\s*\(\s*""", RegexOptions.CultureInvariant);
        // Equality between two identities the mechanism was handed is how a ledger finds its own entry; what
        // this forbids is a comparison against a word the kit itself states, which is a branch on which effect
        // a spell carries.
        Regex branchesOnAnEffect = new(
            "\\.Effect\\s*(==|!=)\\s*\"|string\\.Equals\\([^;]*\\.Effect\\s*,\\s*\"",
            RegexOptions.CultureInvariant);
        string[] all =
        [
            .. Directory.EnumerateFiles(magic, "*.cs", SearchOption.AllDirectories)
                .Where(file => !Ignored(file)),
        ];
        Assert.NotEmpty(all);

        foreach (string source in applied)
        {
            string text = File.ReadAllText(source);

            // The effect application does not know that spells have identities at all.
            Assert.False(
                text.Contains("SpellId", StringComparison.Ordinal),
                $"{Path.GetFileName(source)} names a spell identity: an effect is applied by its category, and a path that could name a spell is a path that could branch on one.");
        }

        foreach (string source in all)
        {
            string text = File.ReadAllText(source);
            Match named = namesASpell.Match(text);
            Assert.False(
                named.Success,
                $"{Path.GetFileName(source)} builds a spell identity from a literal ('{named.Value.Trim()}'): which spells exist is content's answer, and a spell the mechanism can name is a spell it can special-case.");
            Match branch = branchesOnAnEffect.Match(text);
            Assert.False(
                branch.Success,
                $"{Path.GetFileName(source)} branches on an effect identity ('{branch.Value.Trim()}'): what a spell does is the effect owner's answer, and the mechanism hands the identity over unread.");
        }
    }

    /// <summary>Whether a source file is build output rather than a source the kit ships.</summary>
    private static bool Ignored(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>Splits an advance into its parts, which a case that only needs one of them reads.</summary>
    private static (GameDate From, GameDate To, GameDuration Elapsed, PeriodCrossings Crossings, IReadOnlyList<DeadlineDue> Due) Split(
        ClockAdvance advance) => (advance.From, advance.To, advance.Elapsed, advance.Crossings, advance.Due);

    /// <summary>A clock at nine in the morning of this kit's own calendar.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>A party of two, which is the state every effect here is carried by.</summary>
    private static PartyEntity Party() =>
        new PartyEntityFactory().Create(new PartyCreation(
            [
                Member("Nyx", [new SkillEntry(WaterSkill, 2, new SkillTier(1), 0)]),
                Member("Borin", []),
            ],
            coins: 100,
            foodPortions: 6,
            reputation: 0,
            fame: 0));

    private static MemberCreation Member(string name, IReadOnlyList<SkillEntry> skills) =>
        new(new PartyMemberSeed(
            name,
            new RaceId("testfolk"),
            new ClassId("mage"),
            [new AttributeScore(new AttributeId("Intellect"), 12)],
            skills,
            [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(20),
            spellPoints: ResourcePool.Full(6)));

    private static readonly SpellId Travel = new("31");
    private static readonly SkillId WaterSkill = new("Water");

    /// <summary>One spell, so a cast has something to hand the effect path.</summary>
    private sealed class TestSpells : ISpellRule
    {
        private static readonly SpellDefinition Portal = new(
            Travel,
            "A portal",
            "water",
            WaterSkill,
            new SkillTier(1),
            Cost: 1,
            SpellTargeting.None,
            "travel");

        private static readonly SpellCatalog Spells = new([Portal]);

        public SpellCatalog Catalog => Spells;

        public int SpellPointCapacity(PartyMember member) => 10;

        public int CostFor(PartyMember member, SpellDefinition spell) => spell.Cost;

        public SpellRefusal? MayLearn(PartyMember member, SpellDefinition spell) => null;
    }

    /// <summary>
    /// The effect path this suite casts through: it applies what a test states, leaves one effect running with
    /// a deadline, offers one aim, and answers what the party sees by.
    /// </summary>
    private sealed class RecordingEffects : ISpellEffectRule, ISpellAimRule, IRunningSpellEffects, IPartySightRule
    {
        private readonly RunningSpellEffects _running;

        internal RecordingEffects(PartyEntity party, GameClock clock) => _running = new RunningSpellEffects(party, clock);

        internal List<SpellApplication> Applications { get; } = [];

        internal SpellApplicationOutcome Expressed { get; set; } = SpellApplicationOutcome.Unexpressed("test.effect", "nothing changed.");

        internal IReadOnlyList<SpellAim> Offered { get; set; } = [];

        public IReadOnlyList<RunningSpellEffect> Running => _running.Running;

        public bool IsRunning(EffectId effect) => _running.IsRunning(effect);

        public PartySight Sight => _running.IsRunning(Ward) ? PartySight.Light : PartySight.Dark;

        public SpellRefusal? Judge(SpellApplication application) => null;

        public SpellApplicationOutcome Apply(SpellApplication application)
        {
            Applications.Add(application);
            _running.Start(Ward, magnitude: 7, GameDuration.FromHours(1));
            return Expressed;
        }

        public IReadOnlyList<SpellAim> AimsOf(SpellDefinition spell) => Offered;
    }

    /// <summary>The repository root, found the way the kit's other source scans find it.</summary>
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

        throw new InvalidOperationException("Could not find the repository root above the test assembly.");
    }
}
