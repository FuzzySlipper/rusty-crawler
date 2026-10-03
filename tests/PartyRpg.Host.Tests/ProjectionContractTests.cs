using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The projection contract, bound across the two languages: what the product publishes is written to checked-in
/// fixtures, and the DOM companion's suite mounts exactly those fixtures.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fixtures are the product's own output.</b> Every session fixture is <see cref="SessionProjection.Build"/>
/// over a session that carries every block — a fight, a counter, a conversation, a spellbook, alchemy, quests, the
/// five books, the automap, and a party being made — or over the product itself, created and started the way the
/// engine creates it. The companion suite reads each one and fails when any of its readers meets a field that is
/// missing or of another type, so renaming a field here fails there rather than leaving a screen that quietly
/// shows an empty section.
/// </para>
/// <para>
/// <b>The names are bound the same way.</b> <c>contract.json</c> carries the projection and action contracts, every
/// payload action the session reads, and every intent the host declares with the key it is mapped to, and the
/// companion suite fails when the panel sends an action the product does not read.
/// </para>
/// <para>
/// <b>Regenerating them is one command.</b> Setting <c>CRAWLER_WRITE_UI_FIXTURES=1</c> makes this test write the
/// fixtures it would otherwise compare against, which is how they are produced rather than hand-edited. Without it
/// the test compares each file and names the first that differs.
/// </para>
/// </remarks>
public sealed class ProjectionContractTests
{
    /// <summary>The environment variable that writes the fixtures instead of checking them.</summary>
    private const string WriteVariable = "CRAWLER_WRITE_UI_FIXTURES";

    [Fact]
    public void The_checked_in_fixtures_are_what_the_product_publishes()
    {
        string directory = Repository.PathOf("tests", "PartyRpg.Ui.Tests", "fixtures");
        Dictionary<string, string> fixtures = new(StringComparer.Ordinal)
        {
            ["contract.json"] = Contract(),
            ["session-creating.json"] = Json(Creating()),
            ["session-running.json"] = Json(SessionProjection.Build(Running())),
            ["session-turn-based.json"] = Json(SessionProjection.Build(TurnBased())),
            ["session-empty.json"] = Json(SessionProjection.Build(Empty())),
        };

        if (string.Equals(Environment.GetEnvironmentVariable(WriteVariable), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(directory);
            foreach ((string name, string text) in fixtures) File.WriteAllText(Path.Combine(directory, name), text);
            return;
        }

        foreach ((string name, string expected) in fixtures)
        {
            string path = Path.Combine(directory, name);
            Assert.True(File.Exists(path), $"{path} is missing; regenerate the fixtures with {WriteVariable}=1.");
            string actual = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.True(
                string.Equals(expected, actual, StringComparison.Ordinal),
                $"{name} is not what the product publishes; regenerate the fixtures with {WriteVariable}=1 and let the companion suite judge the change.");
        }

        // A fixture nobody generates is one nobody keeps in step: the directory holds exactly these.
        Assert.Equal(
            fixtures.Keys.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Asserts that a screen may claim an action: that the action is among the ones <c>contract.json</c> lists, which
    /// is the list the companion suite holds every action the panel sends to.
    /// </summary>
    /// <param name="action">The payload action name.</param>
    internal static void AssertPanelMayClaim(string action) =>
        Assert.True(Actions().Contains(action, StringComparer.Ordinal), $"'{action}' is not among the actions the companion is held to.");

    /// <summary>Every payload action the session reads, which is every action a screen may send.</summary>
    private static IEnumerable<string> Actions() =>
    [
        UiActionPayload.PauseSession,
        UiActionPayload.ResumeSession,
        SaveActions.Save,
        PartyRpg.Kit.Interaction.UseActions.Use,
        CombatActions.Attack,
        CombatActions.SelectMember,
        CombatActions.NextMember,
        TurnActions.Toggle,
        TurnActions.Skip,
        TurnActions.Wait,
        CreationActions.SelectMember,
        CreationActions.SelectPortrait,
        CreationActions.SelectClass,
        CreationActions.SetName,
        CreationActions.RaiseAttribute,
        CreationActions.LowerAttribute,
        CreationActions.ChooseSkill,
        CreationActions.RemoveSkill,
        CreationActions.Advance,
        CreationActions.Accept,
        .. ProductIdentity.ServicePayloadActions,
        ConversationActions.Topic,
        ConversationActions.Person,
        ConversationActions.Follower,
        ConversationActions.Steal,
        ConversationActions.Leave,
        RestActions.Rest,
        RestActions.Camp,
        RestActions.WaitUntilDawn,
        RestActions.WaitAnHour,
        RestActions.WaitFiveMinutes,
        CastActions.Cast,
        CastActions.QuickSpell,
        AlchemyActions.Mix,
        EquipActions.Equip,
        EquipActions.Unequip,
        EquipActions.UseItem,
        PartyRpg.Kit.Progression.SkillRaiseActions.Raise,
    ];

    /// <summary>The names both halves of the product must agree on, as the companion suite reads them.</summary>
    private static string Contract()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteString("projectionStream", ProductIdentity.UiStream);
            writer.WriteString("projectionContract", ProductIdentity.UiContract);
            writer.WriteString("actionIntent", ProductIdentity.UiActionIntent);
            writer.WriteString("actionContract", ProductIdentity.UiActionContract);
            writer.WriteStartArray("actions");
            foreach (string action in Actions().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) writer.WriteStringValue(action);
            writer.WriteEndArray();

            // Every intent the project declares, with the key its mapping binds, in the order the project states them.
            ProductInputMapping[] mappings = ProductTestContext.DeclaredInput().PhysicalMappings.ToArray();
            writer.WriteStartArray("intents");
            foreach (XElement intent in ProductDeclarations.Project.Descendants("RustyEngineProductInputIntent"))
            {
                string name = (string?)intent.Attribute("Include") ?? string.Empty;
                writer.WriteStartObject();
                writer.WriteString("intent", name);
                writer.WriteString("value", (string?)intent.Attribute("Value") ?? string.Empty);
                ProductInputMapping? mapped = mappings
                    .Select(mapping => (ProductInputMapping?)mapping)
                    .FirstOrDefault(mapping => Encoding.UTF8.GetString(mapping!.Value.Intent.Span) == name);
                writer.WriteString("key", mapped is { } found ? ControlKeys.Label(found.Keyboard) : string.Empty);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>What the product itself publishes while it creates a party, with one member reopened to its portrait.</summary>
    private static UiValue Creating()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        product.Start();
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Payload("""{"action":"creation.select-member","member":0}""")));
        Assert.Equal(SessionMode.Creating, product.Mode);
        return ui.Latest().Value;
    }

    /// <summary>Writes one published value as JSON, in the order the product wrote its fields.</summary>
    private static string Json(UiValue value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            Write(writer, value, value.Root);
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void Write(Utf8JsonWriter writer, UiValue value, uint index)
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        switch (node.Kind)
        {
            case StructuredValueKind.Null:
                writer.WriteNullValue();
                break;
            case StructuredValueKind.Bool:
                writer.WriteBooleanValue(node.BoolValue != 0);
                break;
            case StructuredValueKind.Number:
                writer.WriteNumberValue(node.NumberValue);
                break;
            case StructuredValueKind.String:
                writer.WriteStringValue(Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)node.TextOffset, (int)node.TextLen)));
                break;
            case StructuredValueKind.Array:
                writer.WriteStartArray();
                for (uint edge = node.FirstEdge; edge < node.FirstEdge + node.ChildCount; edge++) Write(writer, value, value.Edges.Span[(int)edge]);
                writer.WriteEndArray();
                break;
            case StructuredValueKind.Object:
                writer.WriteStartObject();
                for (uint edge = node.FirstEdge; edge < node.FirstEdge + node.ChildCount; edge++)
                {
                    uint child = value.Edges.Span[(int)edge];
                    StructuredValueNode field = value.Nodes.Span[(int)child];
                    writer.WritePropertyName(Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)field.KeyOffset, (int)field.KeyLen)));
                    Write(writer, value, child);
                }

                writer.WriteEndObject();
                break;
            default:
                throw new InvalidOperationException($"A projection node of kind {node.Kind} has no JSON form.");
        }
    }

    private static readonly SessionComposition Composition = new(
        new RulesetId("mightandmagic7"),
        "Might and Magic VII: For Blood and Honor",
        "partyrpg-default",
        2);

    private static ControlKeys Keys() => ProductControlKeys.Read(ProductTestContext.DeclaredInput());

    /// <summary>A session that composed nothing yet: every block with nothing in it.</summary>
    /// <remarks>
    /// The blocks are each one's no-mechanism value with no outcome word, which is the blank reading this fixture
    /// has always held: a screen must read every field of a block that has nothing to say, including an outcome
    /// that names nothing.
    /// </remarks>
    private static SessionSnapshot Empty()
    {
        SessionSnapshot bare = SessionSnapshots.Bare(Composition with { Bundle = null, ContentPacks = 0, PartyStart = SessionPartyStart.Scenario }, SessionMode.Starting, WorldSnapshot.Empty);
        return bare with
        {
            Interaction = bare.Interaction with { Outcome = string.Empty },
            Service = bare.Service with { Outcome = string.Empty },
            Rest = bare.Rest with { Outcome = string.Empty },
            Conversation = bare.Conversation with { Outcome = string.Empty },
            Combat = bare.Combat with { Outcome = string.Empty },
            Progression = bare.Progression with { Outcome = string.Empty },
            Promotion = bare.Promotion with { Outcome = string.Empty },
            Skills = bare.Skills with { Outcome = string.Empty },
            Magic = bare.Magic with { Outcome = string.Empty },
            Quests = bare.Quests with { Action = string.Empty, Outcome = string.Empty },
            Keys = Keys(),
        };
    }

    /// <summary>A running session in which every block carries something: the fight is in real time.</summary>
    private static SessionSnapshot Running() => new(
        Composition,
        SessionMode.Running,
        12.5,
        750,
        new WorldSnapshot("1", "Emerald Island", "region", new PlacePose(12552, 800, 193, 512, 0), 2, 76, Open: true, Hours: "06:00–18:00", NextChange: "1168-01-02 18:00"),
        new MovementSnapshot(
            Moved: true, Grounded: true, CharacterBlockFlags.Wall, StepRise: 0.5, FallDistance: 0, FallDamage: 0,
            Footing: new FootingSnapshot(
                "water", Harmful: true, Every: 30, NextHarmIn: 12.5,
                [
                    new FootingShelterSnapshot("spell.water-breathing", "Water Breathing", "2", "Aelina", Everybody: false),
                ])),
        new ClockSnapshot(true, "1168-01-02", "09:30", "day", 1),
        new PartySnapshot(
            true, 4, 200, 6, "portions", 12, 3, "weak (1)",
            HitPoints: 90, HitPointsMax: 120, SpellPoints: 30, SpellPointsMax: 40, Pack: 5,
            StandingRead: true, Standing: "Friendly", StandingDetail: "people speak well of the party",
            Awards: [new AwardSnapshot("promotion:rogue", "promotion", "Rogue", "Thief")])
        {
            Debts = [new PartyDebt("fine", 350)],
            Followers = [new FollowerSnapshot("npc-1", "Fool", "701", "hired", true) { Benefits = "Luck +5" }],
        },
        new CreationSnapshot(
            Active: false, Accepted: true, HasDefault: true, MemberIndex: 0, MemberCount: 2, Step: string.Empty,
            PoolRemaining: 0, RefusalCode: string.Empty, RefusalMessage: string.Empty,
            Roster: [], Portraits: [], Classes: [], Skills: [], Attributes: [],
            Party:
            [
                new CreationPartyMemberSnapshot(0, "Roderick", "Human", "Knight", "human-man"),
                new CreationPartyMemberSnapshot(1, "Aelina", "Elf", "Sorcerer", "elf-woman"),
            ]),
        new SaveSnapshot(true, false, "session", SaveState.Saved, "1168-01-02 09:00", string.Empty, "Saved the session to slot 'session' at 1168-01-02 09:00."),
        new InteractionSnapshot(
            true, "door", "A door", "unlock", "closed", 128, "ready", ["the Iron Key"], "refused",
            "interaction-requirement-unmet", "A door requires the Iron Key.", string.Empty, Bodies: 1),
        new ServiceSnapshot(
            Available: true,
            Open: true,
            Id: "sword-and-shield",
            Kind: "Weapon Shop",
            Name: "The Sword and Shield",
            Proprietor: "Bertram",
            State: "open",
            Hours: "06:00–18:00",
            Operations: ["buy", "sell", "identify", "repair", "teach", "fare", "steal", "repay"],
            Memberships: ["Fire Guild membership"],
            Stock:
            [
                new ServiceStockSnapshot("stock:sword", "sword", "A fine sword", 2, 110, false),
                new ServiceStockSnapshot("stock:potion", "potion", "A potion", 0, 30, false),
            ],
            Lessons: [new ServiceLessonSnapshot("skill", "Sword", "Sword", 1, 25, 1)],
            Offers:
            [
                new ServiceOfferSnapshot("fare", "4", "A passage to The Tularean Forest", 2, 25)
                {
                    Choices = [new ServiceOfferChoiceSnapshot("fare", -1, "", 1, 25, 0, true, "")],
                },
                new ServiceOfferSnapshot("notice", string.Empty, "Travellers speak of the roads east.", 1, 0),
                new ServiceOfferSnapshot("debt", "fine", "the party's fine", 350, 90),
            ],
            Sales:
            [
                new ServiceSaleSnapshot("3", "shield", "A shield", 12, 3, false),
                new ServiceSaleSnapshot("4", "dagger", "A dagger", 8, 0, true) { Stolen = true },
            ],
            Members: [new ServiceMemberSnapshot(0, "Roderick"), new ServiceMemberSnapshot(1, "Aelina")],
            Action: "buy",
            Outcome: "applied",
            Code: string.Empty,
            Message: "The party buys 1 × A fine sword for 110 coin(s), and 1 are left.",
            Paid: 110,
            Earned: 0,
            Coins: 90)
        {
            Thieves = [new ServiceMemberSnapshot(1, "Aelina")],
        },
        new RestSnapshot(
            true, "rest", "applied", string.Empty, "The party rests for 8 hour(s).", "1168-01-01 22:00", "1168-01-02 06:00",
            28800, 2, 2, "portions", false, true, 2, "weak", string.Empty, false, "1168-01-03 06:00", 0),
        new ConversationSnapshot(
            Available: true,
            Open: true,
            Subject: "person-0",
            Speaker: "Mira",
            Greeting: "'A fine day for it.'",
            People: [new ConversationPersonSnapshot("mira", "Mira", "709", true), new ConversationPersonSnapshot("simon", "Simon", "707", false)],
            Topics: [new ConversationTopicSnapshot("topic-1", "The contest", true, string.Empty)],
            Withheld: [new ConversationTopicSnapshot("topic-2", "The errand", false, "the errand is not finished")],
            Said: [new ConversationLineSnapshot("mira", "'A fine day for it.'", "the event programs behind a reply are not run")],
            Action: "open",
            Outcome: "applied",
            Code: string.Empty,
            Message: "Mira: 'A fine day for it.'",
            Residue: string.Empty,
            Handoff: string.Empty,
            Topic: string.Empty)
        {
            Thieves = [new ServiceMemberSnapshot(1, "Aelina")],
        },
        new CombatSnapshot(
            Available: true,
            Engaged: true,
            Opposition: 1,
            Ready: 1,
            Members:
            [
                new CombatActorSnapshot("member:1", "Roderick", true, 0, 0, 40, 40, Member: "1", Selected: true),
                new CombatActorSnapshot("member:2", "Aelina", false, 1.5, 0, 0, 24, "Unconscious", true, Member: "2"),
            ],
            Enemies: [new CombatActorSnapshot("actor:1", "A beast", false, 1, 100, 14, 40, "poisoned (1)", false, "attacking")],
            Actor: "Roderick",
            Kind: "melee",
            Target: "A beast",
            Outcome: "applied",
            Code: string.Empty,
            Message: "Roderick hits A beast (melee): 5 Phys damage landed.",
            RecoverySeconds: 22.969,
            Resolved: true,
            Hit: true,
            Chance: 4595,
            DamageRolled: 5,
            Damage: 5,
            DamageKind: "Phys",
            Resistance: "0",
            Condition: string.Empty,
            TargetDown: false,
            ByParty: true),
        new ProgressionSnapshot(
            true,
            [new ProgressionMemberSnapshot(0, "1", "Roderick", 1, 7000, 5, 1000, 10, 5, 2), new ProgressionMemberSnapshot(1, "2", "Aelina", 1, 0, 0, 1000, 0, 0, 2)],
            "awarded",
            "kill",
            7000,
            string.Empty,
            "The party earned 7000 experience from kill."),
        new PromotionSnapshot(
            true,
            [
                new PromotionMemberSnapshot(1, "2", "Aelina", "Sorcerer", 1,
                [
                    new PromotionRankSnapshot("sorcerer-wizard", "Wizard", 2, string.Empty, "npc-48", "Thomas Grey", "Collect the six golem pieces.",
                    [
                        new PromotionRequirementSnapshot("giver", "npc-48", "Thomas Grey", 1, "granted by Thomas Grey"),
                        new PromotionRequirementSnapshot("item", "639", "Golem part", 6, "6 × Golem part"),
                    ]),
                ]),
            ],
            "granted",
            "knight-cavalier",
            "Cavalier",
            2,
            string.Empty,
            [new PromotionGrantSnapshot("1", "Roderick", "Knight", 1, "Cavalier", 2, string.Empty, ["granted by Sir Charles Quixote"])],
            [new PromotionDenialSnapshot("2", "Aelina", "Sorcerer", 1, ["6 × Golem part"])],
            string.Empty,
            "Roderick rose to Cavalier."),
        new SkillsSnapshot(
            true,
            [
                new SkillMemberSnapshot(0, "1", "Roderick", "Knight", 1,
                [
                    new SkillRowSnapshot("Sword", "weapon", 3, "basic", 12, "master", 5, 4, 4, string.Empty, string.Empty),
                    new SkillRowSnapshot("Fire", "magic", 0, "none", 0, "none", 0, 0, 0, "a Knight holds nothing of Fire", "skill-closed"),
                ]),
            ],
            "raised",
            "1",
            "Sword",
            3,
            3,
            string.Empty,
            "Roderick raises Sword to 3 for 3 point(s)."),
        new MagicSnapshot(
            Available: true,
            Members:
            [
                new SpellMemberSnapshot(1, "2", "Aelina", "Sorcerer", 33, 36, "2", "Fire Bolt",
                [
                    new SpellRowSnapshot("2", "Fire Bolt", "Fire", "basic", 1, 2, "foe", "damage", [], "opposition"),
                    new SpellRowSnapshot("1", "Torch Light", "Fire", "basic", 1, 1, "party", "light", []),
                    new SpellRowSnapshot("31", "Town Portal", "Water", "master", 4, 20, "none", "travel", [new SpellAimSnapshot("place:1", "Emerald Island", "town")]),
                    new SpellRowSnapshot("68", "Heal", "Body", "basic", 1, 2, "ally", "healing", [], "party"),
                ]),
            ],
            Targets:
            [
                new SpellTargetSnapshot("member:1", "Roderick", "party"),
                new SpellTargetSnapshot("actor:1", "A beast", "opposition"),
            ],
            Outcome: "cast",
            Member: 1,
            Caster: "Aelina",
            Spell: "2",
            Cost: 2,
            Target: "actor:1",
            Effect: "damage",
            Code: string.Empty,
            Message: "Aelina casts Fire Bolt at A beast.",
            Facts: [new SpellFactSnapshot("A beast", "14/40")],
            Running: [new SpellRunningSnapshot("light", 1, "1168-01-02 10:30")],
            Sight: "light",
            MemberRunning: [new SpellMemberRunningSnapshot("1", "Roderick", "resist-fire", 5, "1168-01-02 11:00")],
            Items:
            [
                new SpellItemSnapshot("7", "A wand of fire", "charged", "2", "Fire Bolt", "foe", 5, 8, true, "Aelina", "opposition"),
                new SpellItemSnapshot("8", "A scroll of light", "consumed", "1", "Torch Light", "party", 0, 0, false, string.Empty),
            ],
            Source: "7"),
        new AlchemySnapshot(
            true,
            [new AlchemyMemberSnapshot(0, "1", "Roderick", 0), new AlchemyMemberSnapshot(1, "2", "Aelina", 2)],
            [
                new AlchemyItemSnapshot("11", "200", "Widowsweep Berries", "reagent", 1, 1),
                new AlchemyItemSnapshot("12", "220", "Potion Bottle", "bottle", 0, 2),
            ],
            [new AlchemyMixtureSnapshot("11", "12", "Widowsweep Berries", "Potion Bottle")],
            new AlchemyOutcomeSnapshot(1, "Aelina", "mixed", "222", "Cure Wounds", 1, 0, 0, string.Empty, 0, string.Empty, "Aelina mixes Cure Wounds.")),
        new QuestSnapshot(
            true,
            [
                new QuestJournalSnapshot(
                    "35", "The Elven Treasury", "accepted", "npc-43", "Raid the Elven Treasury and return.",
                    "the treasury is the castle's own event program, which this build does not run",
                    [new QuestObjectiveSnapshot("reach-0", "Reach Castle Navan", 0, 1, false), new QuestObjectiveSnapshot("kill-1", "Slay the guards", 1, 3, false)],
                    false),
            ],
            "turn-in",
            "refused",
            "35",
            0,
            0,
            [],
            [],
            [],
            "quest-objectives-unmet",
            "'The Elven Treasury' is not finished: Reach Castle Navan."),
        new JournalSnapshot(
            true,
            [
                new JournalBookSnapshot("quests", "Current Quests", true, "1 errand in the journal", []),
                new JournalBookSnapshot("notes", "Auto Notes", true, "1 note", [new JournalRowSnapshot("recipe|200+220|", "Learned the recipe for Cure Wounds", "1168-01-02 09:00", string.Empty, "alchemy", false)]),
                new JournalBookSnapshot("maps", "Maps", true, "2 of 76 places known", [new JournalRowSnapshot("1", "Emerald Island", "region", "visited", "world", true)]),
                new JournalBookSnapshot("calendar", "Calendar", true, "1168-01-02", [new JournalRowSnapshot("date", "Today", "1168-01-02", string.Empty, "clock", false)]),
                new JournalBookSnapshot("history", "History", true, "1 entry", [new JournalRowSnapshot("place|1|1", "Entered Emerald Island", "1168-01-01 09:00", "Emerald Island", "world", false)]),
            ]),
        new MapSnapshot(
            Available: true,
            Mapped: true,
            Title: "Automap",
            Place: "1",
            Name: "Emerald Island",
            Kind: "region",
            State: "16 of 64 squares walked (25%)",
            Seen: 16,
            Total: 64,
            Detection: "wizard-eye",
            DetectionMessage: "Wizard Eye shows what stands nearby.",
            DetectionEnds: "1168-01-02 10:30",
            Drawing: new MapDrawingSnapshot(
                1,
                4,
                16,
                1000,
                [new MapCellSnapshot(0, 0, 62.5, 62.5, "low"), new MapCellSnapshot(62.5, 0, 62.5, 62.5, "upland")],
                [new MapMarkSnapshot("door:a", "door", "a shut door", 31.25, 31.25, false), new MapMarkSnapshot("container:b", "container", "a far chest", 93.75, 31.25, true)],
                31.25,
                31.25,
                90)),
        Keys(),
        new EquipmentSnapshot(
            true,
            ["off hand", "main hand", "bow", "armour"],
            [
                new EquipmentMemberSnapshot(0, "1", "Roderick", [new EquipmentWornSnapshot("main hand", "21", "1", "Crude Longsword")]),
                new EquipmentMemberSnapshot(1, "2", "Aelina", [], "permanent Fire resistance 2"),
            ],
            [
                new EquipmentItemSnapshot("22", "66", "Leather Armor", ["armour"]),
                new EquipmentItemSnapshot("23", "15", "Dagger", ["main hand", "off hand"]),
            ],
            new OutfittingResult(
                OutfittingResult.Refused,
                "equipment-skill-missing",
                "Aelina has not learned Leather, which is what a 66 needs before it can be worn or wielded.",
                1,
                "Aelina",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty))
        {
            Uses = [new EquipmentUseSnapshot("24", "Genie Lamp — permanent resistance gift", "Use")],
            UseOutcome = new ItemUseResult(true, "item-use-applied", "Aelina uses the Genie Lamp: permanent Fire resistance +2; the lamp is consumed."),
        });

    /// <summary>The same session a moment later, paced turn-based with a round under way and the party's turn out.</summary>
    private static SessionSnapshot TurnBased()
    {
        SessionSnapshot running = Running();
        return running with
        {
            Mode = SessionMode.TurnBased,
            Combat = running.Combat with
            {
                Pacing = CombatPacing.TurnBased,
                Ready = 0,
                ByParty = false,
                Turn = new CombatTurnSnapshot(
                    TurnPhase.Action,
                    2,
                    "member:1",
                    "Roderick",
                    PlayerTurn: true,
                    DueSeconds: 0,
                    RoundSeconds: 23.438,
                    ElapsedSeconds: 4,
                    MovementSeconds: 0,
                    Last: "skip",
                    Order:
                    [
                        new TurnOrderActorSnapshot("member:1", "Roderick", CombatSide.Party, 0, true, true, false, true),
                        new TurnOrderActorSnapshot("actor:1", "A beast", CombatSide.Opposition, 1, false, true, true, false),
                        new TurnOrderActorSnapshot("member:2", "Aelina", CombatSide.Party, 1.5, false, false, false, false),
                    ]),
            },
        };
    }
}
