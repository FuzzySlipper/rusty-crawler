using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
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
        GameClock clock = TestClock.Create();
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
        GameClock clock = TestClock.Create();
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
        RunningSpellEffects running = new(party, TestClock.Create());
        running.Start(Ward, magnitude: 5, GameDuration.FromHours(1));
        running.Start(Light, magnitude: 2, GameDuration.FromHours(1));

        // Something a counter sold is carried on the party too, in a home of its own: a dispelling that took a
        // player's ticket rather than their magic would be ending state the running-effect owner does not hold.
        party.Passages.Hold(new PlaceId("2"), "coach");

        IReadOnlyList<RunningSpellEffect> ended = running.EndAll();

        Assert.Equal(2, ended.Count);
        Assert.False(party.Effects.Has(Ward));
        Assert.False(party.Effects.Has(Light));
        Assert.Equal("coach", party.Passages.RouteTo(new PlaceId("2")));
    }

    [Fact]
    public void The_panel_reads_what_a_cast_changed_and_what_the_effect_path_offers()
    {
        using PartyEntity party = Party();
        GameClock clock = TestClock.Create();
        RecordingEffects effects = new(party, clock);
        Spellcasting casting = new(party, Capabilities.Magic(new TestSpells(), effects));
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
        // What a spell's effect is belongs to the ruleset, which applies it by category behind the seam. The kit's own
        // effect application therefore names no spell and compares no effect: it carries an identity and hands it
        // back, which is what keeps ninety-nine spells from becoming ninety-nine code paths.
        SourceCode kit = ProductSource.Kit;
        const string Effects = "src/PartyRpg.Kit/Magic/Effects/";
        const string Magic = "src/PartyRpg.Kit/Magic/";
        Assert.NotEmpty(kit.TreesUnder(Effects));

        // The effect application does not know that spells have identities at all.
        SourceSite[] named = [.. kit.UsesOfType(typeof(SpellId)).Where(site => site.File.StartsWith(Effects, StringComparison.Ordinal))];
        Assert.True(named.Length == 0, "An effect is applied by its category, and a path that could name a spell could branch on one:\n" + string.Join('\n', named.Select(site => site.ToString())));

        // Nowhere in the magic mechanism is a spell identity built from a word the kit states — which spells exist is
        // content's answer — however the construction is spelled.
        SourceSite[] literal = [.. kit.Creations(typeof(SpellId))
            .Where(site => site.File.StartsWith(Magic, StringComparison.Ordinal) && site.Text.Contains('"', StringComparison.Ordinal))];
        Assert.True(literal.Length == 0, "A spell the mechanism can name is a spell it can special-case:\n" + string.Join('\n', literal.Select(site => site.ToString())));

        // And nothing in it compares an effect identity with a word of its own: equality between two identities the
        // mechanism was handed is how a ledger finds its own entry, and a comparison against a literal is a branch on
        // which effect a spell carries.
        SourceSite[] branches = [.. EffectBranches(kit).Where(site => site.File.StartsWith(Magic, StringComparison.Ordinal))];
        Assert.True(branches.Length == 0, "What a spell does is the effect owner's answer:\n" + string.Join('\n', branches.Select(site => site.ToString())));
    }

    [Fact]
    public void The_effect_branch_detector_sees_an_operator_an_equals_call_and_a_switch()
    {
        SourceCode probe = SourceCode.FromText(("src/PartyRpg.Kit/Magic/Probe.cs", """
            internal sealed record Row(string Effect);
            internal static class Probe
            {
                // A comparison written in a comment, row.Effect == "ward", is not a branch.
                private static bool Ward(Row row) => row.Effect == "ward";
                private static bool Light(Row row) => string.Equals(row.Effect, "light", System.StringComparison.Ordinal);
                private static int Kind(Row row) => row.Effect switch { "haste" => 1, _ => 0 };
                private static bool Same(Row row, Row other) => row.Effect == other.Effect;
            }
            """));

        Assert.Equal([5, 6, 7], EffectBranches(probe).Select(site => site.Line).Order());
    }

    /// <summary>Splits an advance into its parts, which a case that only needs one of them reads.</summary>
    private static (GameDate From, GameDate To, GameDuration Elapsed, PeriodCrossings Crossings, IReadOnlyList<DeadlineDue> Due) Split(
        ClockAdvance advance) => (advance.From, advance.To, advance.Elapsed, advance.Crossings, advance.Due);

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

        public Refusal? MayLearn(PartyMember member, SpellDefinition spell) => null;
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

        public Refusal? Judge(SpellApplication application) => null;

        public SpellApplicationOutcome Apply(SpellApplication application)
        {
            Applications.Add(application);
            _running.Start(Ward, magnitude: 7, GameDuration.FromHours(1));
            return Expressed;
        }

        public IReadOnlyList<SpellAim> AimsOf(SpellDefinition spell) => Offered;
    }

    [Fact]
    public void Only_the_running_effect_owner_writes_a_running_effect()
    {
        // The writers are internal to the kit, so nothing outside it can write one; inside it, the one owner of running
        // effects is the only source that does, which is what keeps a dispel from reaching a record, a balance, or a
        // passage. The law binds every call to the component's two writers across the runtime.
        SourceCode code = ProductSource.Runtime;
        ProductSource.OnlyIn(
            code.Uses([.. code.Members(typeof(ActiveEffects), "Apply"), .. code.Members(typeof(ActiveEffects), "Remove")]),
            file => file == "src/PartyRpg.Kit/Magic/Effects/RunningSpellEffects.cs",
            "A running effect is written by the running effect owner and by nothing else.");
    }

    /// <summary>
    /// Every place code compares an effect identity with a word of its own: an equality operator, an equality call,
    /// a switch, or a pattern whose other side is a string literal.
    /// </summary>
    private static IEnumerable<SourceSite> EffectBranches(SourceCode code)
    {
        foreach (SyntaxTree tree in code.Trees)
        {
            SemanticModel model = code.Model(tree);
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                bool branch = node switch
                {
                    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression) =>
                        (IsEffect(model, binary.Left) && IsWord(binary.Right)) || (IsEffect(model, binary.Right) && IsWord(binary.Left)),
                    InvocationExpressionSyntax invocation when model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "Equals" } =>
                        invocation.ArgumentList.Arguments.Any(argument => IsEffect(model, argument.Expression)) &&
                        invocation.ArgumentList.Arguments.Any(argument => IsWord(argument.Expression)),
                    SwitchExpressionSyntax switched =>
                        IsEffect(model, switched.GoverningExpression) && switched.Arms.Any(arm => HasWord(arm.Pattern)),
                    SwitchStatementSyntax switched =>
                        IsEffect(model, switched.Expression) && switched.Sections.SelectMany(section => section.Labels).Any(HasWord),
                    IsPatternExpressionSyntax matched => IsEffect(model, matched.Expression) && HasWord(matched.Pattern),
                    _ => false,
                };
                if (branch) yield return SourceSite.At(node);
            }
        }
    }

    /// <summary>Whether an expression reads an effect identity, or the text inside one.</summary>
    private static bool IsEffect(SemanticModel model, ExpressionSyntax expression) =>
        model.GetSymbolInfo(expression).Symbol is IPropertySymbol { Name: "Effect" } or IFieldSymbol { Name: "Effect" } ||
        (expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Value" } access && IsEffect(model, access.Expression));

    private static bool IsWord(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.StringLiteralExpression);

    private static bool HasWord(SyntaxNode node) =>
        node.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>().Any(literal => literal.IsKind(SyntaxKind.StringLiteralExpression));
}
