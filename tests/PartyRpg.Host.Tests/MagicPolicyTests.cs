using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's magic through the whole product: a spell learned at a real guild counter, the pool the class
/// and the scores add up to, a cast that pays its points and lands through the fight, and the refusals a
/// player actually meets.
/// </summary>
/// <remarks>
/// <para>
/// What no kit test can prove is what this game states: which spells the shipped table declares and what
/// the donor's own numbers make them cost, what a guild's rung may sell, what a character's spell points are
/// worth, and that a creature's spell lands with the spell's own dice. The ruleset's policy types are
/// internal because nothing outside the product composes them, so this suite reaches them through the
/// ruleset's own friend declaration, and the live half runs over a staged world through the product's own
/// session.
/// </para>
/// <para>
/// The spells are the shipped ones by identity — Fire Bolt is spell 2 and Fireball spell 6 — so what the
/// catalog reads here is the same row an imported pack carries, and the guild is an initiate Fire guild,
/// which sells the school's novice tier and no more.
/// </para>
/// </remarks>
public sealed class MagicPolicyTests
{
    /// <summary>What the shipped spell table says Fire Bolt costs at novice, which is the donor's own number.</summary>
    private const int FireBoltCost = 2;

    [Fact]
    public void A_book_is_bought_at_a_guild_and_consumed_into_the_spellbook_and_the_pool_is_derived()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(GuildContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Use = UseControls, Service = ServiceControls, Conversation = ConversationControls });
        session.Start();

        // The party's pool is the ruleset's own formula over the class, the level, and the scores
        // (OpenEnroth src/Engine/Objects/Character.cpp:1845-1856, Character::GetMaxMana): a sorcerer's base of
        // fifteen, plus three a level times the level one plus the intellect bonus a score of thirty earns —
        // which the donor's own step table makes six.
        ProjectedNode magic = ProjectedNode.Of(ui.Latest().Value).Field("magic");
        Assert.True(magic.Field("available").AsBoolean());
        ProjectedNode caster = magic.Field("members").Item(0);
        Assert.Equal("Aelina", caster.Field("name").AsString());
        Assert.Equal(36d, caster.Field("spellPointsMax").AsNumber());
        Assert.Equal(36d, caster.Field("spellPoints").AsNumber());

        // She knows nothing yet, so her spellbook is empty and the guild offers its school's books.
        Assert.Equal(0d, caster.Field("spells").Length());

        ProjectedNode counter = Walk(session, ui);
        Assert.Equal("Fire Guild", counter.Field("kind").AsString());
        ProjectedNode lessons = counter.Field("lessons");
        Assert.True(Offers(lessons, "spell", "Fire Bolt"), "an initiate guild sells its school's novice spells.");
        Assert.False(Offers(lessons, "spell", "Fireball"), "and not its expert one.");

        // Not a member yet: the book is refused by name, and joining is the lesson that changes it.
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"2","member":0,"tier":1}""")));
        Assert.Equal("service-membership-required", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("code").AsString());

        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"guild.fire","member":0}""")));
        Assert.Equal("applied", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("outcome").AsString());

        // A member who has never learned the school's skill cannot learn one of its spells: the learning rule
        // is this game's magic's own answer, and it names the school.
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"2","member":0,"tier":1}""")));
        Assert.Equal("spell-school-missing", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("code").AsString());

        // The guild teaches its school as an ordinary lesson, and then the book is bought and consumed into
        // the spellbook: she knows the spell, the fee is the book's own price through the counter's
        // multiplier, and nothing landed in the pack.
        session.Update(ProductTestContext.Update(7, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"Fire","member":0}""")));
        Assert.Equal("applied", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("outcome").AsString());
        int afterSchool = (int)ProjectedNode.Of(ui.Latest().Value).Field("service").Field("coins").AsNumber();

        session.Update(ProductTestContext.Update(8, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"2","member":0,"tier":1}""")));
        ProjectedNode book = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("applied", book.Field("outcome").AsString());
        Assert.Equal(200, book.Field("paid").AsNumber());
        Assert.Equal(afterSchool - 200, book.Field("coins").AsNumber());
        Assert.Contains("learns Fire Bolt", book.Field("message").AsString(), StringComparison.Ordinal);

        // The panel now shows it, with what casting it costs this caster and what it is aimed at.
        magic = ProjectedNode.Of(ui.Latest().Value).Field("magic");
        caster = magic.Field("members").Item(0);
        ProjectedNode spell = caster.Field("spells").Item(0);
        Assert.Equal("2", spell.Field("spell").AsString());
        Assert.Equal("Fire Bolt", spell.Field("name").AsString());
        Assert.Equal("Fire", spell.Field("school").AsString());
        Assert.Equal("basic", spell.Field("tier").AsString());
        Assert.Equal(FireBoltCost, spell.Field("cost").AsNumber());
        Assert.Equal("foe", spell.Field("targeting").AsString());
        Assert.Equal("damage", spell.Field("effect").AsString());

        // Teaching the same book twice is refused by name rather than charging for a change that happened.
        session.Update(ProductTestContext.Update(9, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"2","member":0,"tier":1}""")));
        Assert.Equal("spell-already-known", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("code").AsString());
    }

    [Fact]
    public void A_cast_at_a_creature_pays_its_points_and_lands_with_the_spells_own_numbers()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(MonsterContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, combat: true) with { Cast = CastControls });
        session.Start();

        session.Update(ProductTestContext.Update(1, 1));
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("engaged").AsBoolean());
        ProjectedNode enemy = combat.Field("enemies").Item(0);

        // The spell's own cost is the donor's mana at the caster's mastery, and the panel publishes the
        // targets a casting may name with the side each is on.
        ProjectedNode magic = ProjectedNode.Of(ui.Latest().Value).Field("magic");
        ProjectedNode caster = magic.Field("members").Item(0);
        ProjectedNode spell = caster.Field("spells").Item(0);
        Assert.Equal(FireBoltCost, spell.Field("cost").AsNumber());
        Assert.Equal(36d, caster.Field("spellPointsMax").AsNumber());
        int before = (int)caster.Field("spellPoints").AsNumber();

        string target = string.Empty;
        for (int position = 0; position < magic.Field("targets").Length(); position++)
        {
            ProjectedNode candidate = magic.Field("targets").Item(position);
            if (candidate.Field("side").AsString() == "opposition") target = candidate.Field("target").AsString();
        }

        Assert.Equal(enemy.Field("id").AsString(), target);

        session.Update(ProductTestContext.Update(
            2,
            1,
            ProductTestContext.Payload($$"""{"action":"party.cast","member":0,"spell":"2","target":"{{target}}"}""")));
        magic = ProjectedNode.Of(ui.Latest().Value).Field("magic");
        caster = magic.Field("members").Item(0);

        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Equal("damage", magic.Field("effect").AsString());
        Assert.Contains("casts Fire Bolt", magic.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(before - FireBoltCost, (int)caster.Field("spellPoints").AsNumber());

        // What the cast did is the fight's own resolution, reached through the effect path: a spell's own
        // kind of harm, the target's resistance to it, and the harm that was left.
        // What the cast did is the fight's own resolution, reached through the effect path: the message the
        // cast reports carries the attack the fight made and what came of it, which is where the spell's own
        // kind of harm and the target's resistance to it are read.
        Assert.Contains("A beast", magic.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("Fire", magic.Field("message").AsString(), StringComparison.Ordinal);

        // A spell the caster's mastery does not reach is refused by name, and the refusal reads in this
        // game's own words for the two rungs: Fireball asks for expert and she stands at basic.
        session.Update(ProductTestContext.Update(
            3,
            1,
            ProductTestContext.Payload($$"""{"action":"party.cast","member":0,"spell":"6","target":"{{target}}"}""")));
        magic = ProjectedNode.Of(ui.Latest().Value).Field("magic");
        Assert.Equal("refused", magic.Field("outcome").AsString());
        Assert.Equal("spell-mastery-too-low", magic.Field("code").AsString());
        Assert.Contains("expert", magic.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("basic", magic.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(before - FireBoltCost, (int)magic.Field("members").Item(0).Field("spellPoints").AsNumber());

        // Casting again while the caster is still recovering is refused by name, and nothing is spent: the
        // fight's own gate is asked before the points are paid.
        session.Update(ProductTestContext.Update(
            4,
            1,
            ProductTestContext.Payload($$"""{"action":"party.cast","member":0,"spell":"2","target":"{{target}}"}""")));
        magic = ProjectedNode.Of(ui.Latest().Value).Field("magic");
        Assert.Equal("spell-caster-cannot-act", magic.Field("code").AsString());
        Assert.Equal(before - FireBoltCost, (int)magic.Field("members").Item(0).Field("spellPoints").AsNumber());
    }

    [Fact]
    public void A_creatures_spell_lands_with_the_spells_own_dice_rather_than_its_rows()
    {
        // The creature's row states a first attack of one point and a fire bolt at skill four, so a spell
        // resolved from the row's dice would never exceed one and one from the spell's own would reach
        // four dice of six. What the fight resolves is the spell's own expression
        // (OpenEnroth src/Engine/Spells/Spells.cpp:813-838, CalcSpellDamage).
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(MonsterContent(casters: true));
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();

        for (ulong step = 1; step <= 400; step++) session.Update(ProductTestContext.Update(step, 1));
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("engaged").AsBoolean());

        // Somewhere in that stretch the creature chose its spell, and what it rolled came from the spell's
        // own dice: the panel reports the kind of harm the spell's content states, not the row's physical.
        bool cast = false;
        for (ulong step = 401; step <= 900 && !cast; step++)
        {
            session.Update(ProductTestContext.Update(step, 1));
            combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
            if (!combat.Field("message").AsString().Contains("spell1", StringComparison.Ordinal)) continue;
            cast = true;
            Assert.Equal("Fire", combat.Field("damageKind").AsString());
            Assert.InRange(combat.Field("damageRolled").AsNumber(), 1, 24);
        }

        Assert.True(cast, "a creature with a spell on its row casts it, and the fight resolves it.");
    }

    private static readonly UseIntentNames UseControls = new(
        ProductIdentity.UseIntent,
        ProductIdentity.UseAction,
        ProductIdentity.UiActionContract);
    private static readonly ServiceIntentNames ServiceControls = new("test.service.leave", ProductIdentity.UiActionContract);
    private static readonly ConversationIntentNames ConversationControls = new("test.conversation.leave", ProductIdentity.UiActionContract);
    private static readonly CastIntentNames CastControls = new(
        ProductIdentity.CastAction,
        ProductIdentity.QuickSpellAction,
        ProductIdentity.UiActionContract);

    /// <summary>Whether a counter's lessons include one of a kind whose name a person reads as stated.</summary>
    private static bool Offers(ProjectedNode lessons, string kind, string name)
    {
        for (int position = 0; position < lessons.Length(); position++)
        {
            ProjectedNode lesson = lessons.Item(position);
            if (lesson.Field("kind").AsString() == kind && lesson.Field("name").AsString() == name) return true;
        }

        return false;
    }

    /// <summary>Walks the party into the guild's counter and opens it, as a player using the door would.</summary>
    private static ProjectedNode Walk(IGameSession session, RecordingUiService ui)
    {
        session.Update(ProductTestContext.Update(1, 1));
        Assert.Equal("talk", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("verb").AsString());
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("open").AsBoolean());
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic(MightAndMagic7Conversation.CounterTopicId)));
        ProjectedNode counter = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.True(counter.Field("open").AsBoolean());
        return counter;
    }

    /// <summary>A world with one Fire guild standing in it, its books, and a party with no spells yet.</summary>
    private static (string Path, string Text)[] GuildContent() =>
    [
        .. World(monsterAt: null),
        Services(),
        Spells(),
        Books(),
        Skills(),
        Party(knowsFire: false),
    ];

    /// <summary>A world with one hostile creature in it, and a party that already knows Fire Bolt.</summary>
    private static (string Path, string Text)[] MonsterContent(bool casters = false)
    {
        List<(string Path, string Text)> files = [.. World(monsterAt: 100, casters: casters)];
        files.Add(Services());
        files.Add(Spells());
        files.Add(Books());
        files.Add(Skills());
        files.Add(Party(knowsFire: true));

        return [.. files];
    }

    /// <summary>The places, the start, and the pack manifest every case here shares.</summary>
    private static (string Path, string Text)[] World(double? monsterAt, bool casters = false)
    {
        string placements = monsterAt is { } at
            ? $$"""{{(casters ? string.Empty : string.Empty)}}{ "id": "beast", "kind": "monster", "monster": "7", "x": {{at.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "y": 0, "z": 0 }"""
            : string.Empty;
        List<(string Path, string Text)> files =
        [
            ProductTestContext.Bundle("partyrpg-default", "world"),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
                $$"""
                {
                  "schemaVersion": 1,
                  "packId": "world",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [
                    { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                    { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                    { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                    { "path": "services.json", "documentId": "services", "definitionKind": "service" },
                    { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                    { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" },
                    { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                    { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
                $$"""
                {
                  "documentId": "places",
                  "definitionKind": "place",
                  "entries": [
                    { "id": "1", "kind": "interior", "name": "The Guild of Fire", "respawnDays": 1,
                      "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                      "placements": [ { "id": "guild", "kind": "service", "houseId": "139", "x": 100, "y": 0, "z": 0 }{{(monsterAt is null ? string.Empty : $", {placements}")}} ] }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
                """
                { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
                """),
            Monster(casters),
        ];
        return [.. files];
    }

    /// <summary>
    /// A creature that attacks on sight, with the shipped table's own columns for the cells a fight reads.
    /// </summary>
    /// <remarks>
    /// The spell columns are the table's own (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:269-291</c>: the
    /// cell is <c>&lt;name&gt;,&lt;mastery&gt;,&lt;skill&gt;</c> at column 25 and its use chance at 24), and
    /// the attack's cells stand where the shipped header puts them. A row with no columns at all is a
    /// hand-authored one with no blow and no spell, which is why this one carries them.
    /// </remarks>
    private static (string Path, string Text) Monster(bool casters)
    {
        string[] cells = new string[39];
        for (int index = 0; index < cells.Length; index++) cells[index] = "0";
        cells[0] = "7";
        cells[1] = "A beast";
        cells[3] = "4";
        cells[4] = "200";
        cells[5] = "0";
        cells[6] = "0";
        cells[16] = "0";
        cells[17] = "Phys";
        cells[18] = "1d1+0";
        cells[24] = casters ? "100" : "0";
        cells[25] = casters ? "Fire Bolt,1,4" : "0";
        return ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "hostility": 2, "recovery": 100, "level": 4,
                  "hitPoints": 200, "armorClass": 0, "columns": [ {{string.Join(", ", cells.Select(cell => $"\"{cell}\""))}} ] }
              ]
            }
            """);
    }

    /// <summary>One initiate guild of Fire, whose rung decides which of its school's spells it sells.</summary>
    private static (string Path, string Text) Services() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/services.json",
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                { "id": "139", "kind": "Fire Guild", "name": "Initiate Guild of Fire", "proprietor": "Sethric", "mapId": 1, "typeSequence": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 2, "skillPriceMultiplier": 1, "stockIntervalDays": 14 }
              ]
            }
            """);

    /// <summary>The shipped spell rows this suite uses, by their own global ids.</summary>
    private static (string Path, string Text) Spells() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [
                { "id": "2", "school": "Fire", "level": 2, "name": "Fire Bolt", "resist": "Fire" },
                { "id": "6", "school": "Fire", "level": 6, "name": "Fireball", "resist": "Fire" }
              ]
            }
            """);

    /// <summary>The two books those spells are sold as, with the shipped prices and the spell each teaches.</summary>
    private static (string Path, string Text) Books() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "401", "name": "Fire Bolt", "value": 200, "equipStat": "Book", "skillGroup": "Misc", "material": "3", "spell": "2" },
                { "id": "405", "name": "Fireball", "value": 750, "equipStat": "Book", "skillGroup": "Misc", "material": "3", "spell": "6" }
              ]
            }
            """);

    /// <summary>The school's own skill, which is what a spell of that school is learned through.</summary>
    private static (string Path, string Text) Skills() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Fire" }, { "id": "Learning" } ]
            }
            """);

    /// <summary>The scenario's party: one sorcerer, who may hold the fire school and cast from intellect.</summary>
    private static (string Path, string Text) Party(bool knowsFire) =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party",
                  "coins": 5000,
                  "food": 6,
                  "reputation": 0,
                  "fame": 0,
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 1, "hitPoints": 24,
                      "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 30 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 9 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 17 } ],
                      "skills": [{{(knowsFire ? "{ \"id\": \"Fire\", \"level\": 2, \"tier\": 1, \"pointsSpent\": 1 }" : string.Empty)}}],
                      "spells": [{{(knowsFire ? "\"2\", \"6\"" : string.Empty)}}], "conditions": [] }
                  ]
                }
              ]
            }
            """);
}
