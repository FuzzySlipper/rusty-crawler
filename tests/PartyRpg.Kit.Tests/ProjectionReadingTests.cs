using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The projection's blocks are read from their owners only when an owner they read has changed: a running update
/// that only moves the clock hands every other block back as it was, and any owner's change reaches its block on
/// the very next update.
/// </summary>
public sealed class ProjectionReadingTests
{
    private const string Contract = "test.actions";
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test");
    private static readonly SkillId Alchemy = new("Alchemy");
    private static readonly SkillId Blades = new("blades");
    private static readonly SpellId Draught = new("potion:draught");
    private static readonly EquipmentSlot Body = new("body");
    private static readonly ItemDefinitionId Coat = new("coat");
    private static readonly QuestId Patrol = new("patrol");

    [Fact]
    public void A_running_update_that_only_moves_the_clock_reads_none_of_the_blocks_whose_owners_stood_still()
    {
        using Fixture fixture = Fixture.Compose();
        PartyRpgSession session = fixture.Session;
        Step(fixture, 4);
        SessionSnapshot before = session.Inspect();
        int published = fixture.Channel.Count;

        Step(fixture, 30);
        SessionSnapshot after = session.Inspect();

        // The updates ran: the simulation moved and every one of them was published, because the session's own
        // measures are part of what it publishes.
        Assert.True(after.SimulationSeconds > before.SimulationSeconds);
        Assert.Equal(published + 30, fixture.Channel.Count);

        // Every block whose owners carry the same stamp is the very reading the earlier update made.
        Assert.Same(before.Party, after.Party);
        Assert.Same(before.Skills, after.Skills);
        Assert.Same(before.Promotion, after.Promotion);
        Assert.Same(before.Magic, after.Magic);
        Assert.Same(before.Alchemy, after.Alchemy);
        Assert.Same(before.Quests, after.Quests);
        Assert.Same(before.Journal, after.Journal);
        Assert.Same(before.Map, after.Map);
        Assert.Same(before.Equipment, after.Equipment);

        // And those readings are real ones, not a session without the mechanisms.
        Assert.True(after.Skills.Available);
        Assert.True(after.Magic.Available);
        Assert.True(after.Alchemy.Available);
        Assert.True(after.Quests.Available);
        Assert.True(after.Journal.Available);
        Assert.True(after.Map.Available && after.Map.Mapped);
        Assert.True(after.Equipment.Available);
    }

    [Fact]
    public void A_change_to_an_owner_a_block_reads_reaches_that_block_on_the_next_update()
    {
        using Fixture fixture = Fixture.Compose();
        PartyRpgSession session = fixture.Session;
        PartyEntity party = fixture.Party;
        Step(fixture, 2);

        // The purse is the party's: the party block is read again and the panel is sent the new coins, while the
        // journal, which reads nothing of the party, is handed back as it was.
        SessionSnapshot before = session.Inspect();
        party.Purse.Credit(7);
        Step(fixture, 1);
        SessionSnapshot after = session.Inspect();
        Assert.NotSame(before.Party, after.Party);
        Assert.Equal(before.Party.Coins + 7, after.Party.Coins);
        Assert.Equal(after.Party.Coins, fixture.Channel.Latest().Field(SessionProjection.PartyField).Field("coins").AsNumber());
        Assert.Same(before.Journal, after.Journal);
        Assert.Same(before.Map, after.Map);

        // A skill learned is the member's own: the skills block grows a row.
        before = after;
        party.Members[1].Skills.Learn(Blades, new SkillTier(1));
        Step(fixture, 1);
        after = session.Inspect();
        Assert.NotSame(before.Skills, after.Skills);
        Assert.Contains(after.Skills.Members[1].Skills, row => row.Skill == Blades.Value);

        // A spell learned reaches the spellbook.
        before = after;
        party.Members[0].Spells.Learn(Draught);
        Step(fixture, 1);
        after = session.Inspect();
        Assert.NotSame(before.Magic, after.Magic);
        Assert.Contains(after.Magic.Members[0].Spells, row => row.Spell == Draught.Value);

        // A mixture asked for on the panel is the mixing owner's last outcome, and it changes the pack too.
        before = after;
        session.Update(Admitted.Update(fixture.Next(), 1, Admitted.Payload(
            Contract,
            $$"""{"action":"party.mix","member":0,"first":"{{fixture.Berry.Id}}","second":"{{fixture.Bottle.Id}}"}""")));
        after = session.Inspect();
        Assert.NotSame(before.Alchemy, after.Alchemy);
        Assert.Equal("mixture-mixed", after.Alchemy.Outcome?.Code);
        Assert.Equal(
            "mixture-mixed",
            fixture.Channel.Latest().Field(SessionProjection.AlchemyField).Field("outcome").Field("code").AsString());

        // A coat taken into the pack is something a member could wear.
        before = after;
        AlchemyTests.Take(party, Coat);
        Step(fixture, 1);
        after = session.Inspect();
        Assert.NotSame(before.Equipment, after.Equipment);
        Assert.Contains(after.Equipment.Items, item => item.Definition == Coat.Value);

        // An errand offered is the quest owner's.
        before = after;
        Assert.True(session.Quests!.Offer(Patrol, "marshal").IsApplied);
        Step(fixture, 1);
        after = session.Inspect();
        Assert.NotSame(before.Quests, after.Quests);
        Assert.Contains(after.Quests.Journal, quest => quest.Quest == Patrol.Value);

        // A line written in the history and a note taken are the journal's, and the party does not move for them.
        before = after;
        Assert.True(session.Journal!.Record(new JournalEvent(JournalEntryKind.QuestFinished, "test", "a-deed", "a deed")));
        Step(fixture, 1);
        after = session.Inspect();
        Assert.NotSame(before.Journal, after.Journal);
        Assert.Same(before.Party, after.Party);
        Assert.Contains(after.Journal.Books.Single(book => book.Kind == "history").Rows, row => row.Label.Contains("a deed", StringComparison.Ordinal));

        before = after;
        Assert.True(session.Knowledge!.Record(new KnowledgeReport(KnowledgeKind.Clue, "test", "a-clue", "a clue")));
        Step(fixture, 1);
        after = session.Inspect();
        Assert.NotSame(before.Journal, after.Journal);
        Assert.Contains(after.Journal.Books.Single(book => book.Kind == "notes").Rows, row => row.Label.Contains("a clue", StringComparison.Ordinal));

        // Ground seen from somewhere new is the map owner's.
        before = after;
        Assert.True(session.Maps!.Observe(AutomapTests.Region, new PlacePose(28, 28, 0, 0, 0)));
        Step(fixture, 1);
        after = session.Inspect();
        Assert.NotSame(before.Map, after.Map);
        Assert.True(after.Map.Seen > before.Map.Seen);

        // A turn of the clock's minute is a fact the calendar book shows, so the journal is read again for it alone.
        before = after;
        session.Update(Admitted.Update(fixture.Next(), 60, 1.0));
        after = session.Inspect();
        Assert.NotSame(before.Journal, after.Journal);
        Assert.Same(before.Party, after.Party);
        Assert.Same(before.Skills, after.Skills);
    }

    /// <summary>Steps the session through this many running updates that carry nothing but the admitted interval.</summary>
    private static void Step(Fixture fixture, int updates)
    {
        for (int index = 0; index < updates; index++) fixture.Session.Update(Admitted.Update(fixture.Next(), 1));
    }

    /// <summary>One running session over every mechanism whose block the projection keeps between readings.</summary>
    private sealed class Fixture : IDisposable
    {
        private ulong _step;

        private Fixture(PartyRpgSession session, PartyEntity party, RecordingUiProjectionChannel channel, ItemInstance berry, ItemInstance bottle)
        {
            Session = session;
            Party = party;
            Channel = channel;
            Berry = berry;
            Bottle = bottle;
        }

        public PartyRpgSession Session { get; }

        public PartyEntity Party { get; }

        public RecordingUiProjectionChannel Channel { get; }

        public ItemInstance Berry { get; }

        public ItemInstance Bottle { get; }

        public static Fixture Compose()
        {
            // A clock that keeps real time, so a handful of updates never turns its minute.
            GameClock clock = TestClock.Create(scale: 1);
            PartyEntity party = AlchemyTests.Party(alchemyLevel: 3, alchemyTier: 1);
            ItemInstance berry = AlchemyTests.Take(party, AlchemyTests.Berry);
            ItemInstance bottle = AlchemyTests.Take(party, AlchemyTests.Bottle);
            AlchemyTests.Rule alchemy = new();
            AlchemyCatalog mixtures = new([
                new PotionMixture(AlchemyTests.Berry, AlchemyTests.Bottle, MixtureOutcome.Produces(AlchemyTests.Draught), new SkillTier(1), Power: 5, Note: 58),
            ]);
            RecordingUiProjectionChannel channel = new();
            PartyRpgSession session = new(
                Composition,
                channel,
                new SessionOwners(clock),
                new SessionParty.Playing(World: AutomapTests.World(clock), Party: party),
                new SessionRules
                {
                    Names = alchemy,
                    Progression = new ProgressionRules(new Curve()),
                    Skills = new Skills(),
                    Magic = Capabilities.Magic(new AlchemyTests.Spells(AlchemyTests.Draught)),
                    Alchemy = new AlchemyRules(alchemy, mixtures),
                    Quests = new Errands(),
                    Journal = new AutomapTests.TestJournal(),
                    Knowledge = new AutomapTests.TestKnowledge(),
                    Map = new MapRules(new AutomapTests.TestRule(), new AutomapTests.TestMaps(AutomapTests.Region)),
                    Equipment = new Figure(),
                },
                new SessionControls { Mix = new MixIntentNames(Contract) });
            session.Start();
            return new Fixture(session, party, channel, berry, bottle);
        }

        public ulong Next() => _step++;

        public void Dispose()
        {
            Session.Dispose();
            Channel.Dispose();
        }
    }

    /// <summary>A curve no award reaches, which is all the skills and promotion blocks need of one.</summary>
    private sealed class Curve : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 1000;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) => [];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }

    /// <summary>Two skills, a generous ceiling, and two points a level.</summary>
    private sealed class Skills : ISkillRule
    {
        public SkillCatalog Catalog { get; } = new(
        [
            new SkillDefinition(Alchemy, SkillBlock.Miscellaneous),
            new SkillDefinition(Blades, SkillBlock.Weapon),
        ]);

        public SkillCeiling Ceiling(PartyMember member, SkillId skill) => new(60, new SkillTier(4));

        public int RaiseCost(SkillEntry skill, int levels) => 2 * levels;
    }

    /// <summary>One errand to walk somewhere, which anybody may be offered.</summary>
    private sealed class Errands : IQuestRule
    {
        private readonly QuestDefinition _patrol = new(
            Patrol,
            "The long patrol",
            "marshal",
            [new QuestObjective("beat", QuestObjectiveKind.Reach, "2", label: "Walk the cellar")]);

        public IReadOnlyList<QuestDefinition> Definitions => [_patrol];

        public QuestDefinition? Definition(QuestId quest) => quest == Patrol ? _patrol : null;

        public int Counts(QuestKillRequest request) => 0;

        public bool Holds(QuestConditionRequest request) => request.Condition.Kind == ConversationConditionKind.Flag;
    }

    /// <summary>A figure with one slot a coat fills.</summary>
    private sealed class Figure : IEquipmentFigure
    {
        public IReadOnlyList<EquipmentSlot> Slots { get; } = [Body];

        public IReadOnlyList<EquipmentSlot> SlotsFor(ItemInstance item) => item.Definition == Coat ? [Body] : [];
    }
}
