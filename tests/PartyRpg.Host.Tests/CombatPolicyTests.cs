using System.Text.RegularExpressions;
using System.Xml.Linq;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's answers to the kit's combat seams, and the whole trip from a key to what the panel shows.
/// </summary>
/// <remarks>
/// <para>
/// What no kit test can prove is what this game states: a creature's recovery is the monster table's own
/// column converted into game time, its hostility band is the distance at which it notices the party, a
/// character is paced by the donor's own character recovery, and an attack reaches as far as the donor's own
/// ranges. The ruleset's policy types are internal because nothing outside the product composes them, so this
/// suite reaches them through the ruleset's own friend declaration.
/// </para>
/// <para>
/// The live half is proved over a staged world: a monster row and a creature placement, the act control
/// pressed through the product's own intent, and the projection read back — so the declaration, the reader,
/// the state, and the panel are one path rather than four claims.
/// </para>
/// </remarks>
public sealed class CombatPolicyTests
{
    [Fact]
    public void The_act_control_is_declared_in_code_in_the_project_file_and_in_the_companion()
    {
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));
        string project = File.ReadAllText(ProjectFile());
        string product = File.ReadAllText(Path.Combine(SourceDirectory(), "CrawlerProduct.cs"));
        string ui = File.ReadAllText(Path.Combine(SourceDirectory(), "..", "ui", "main.ts"));

        string intent = Constant(source, "AttackIntent");
        string action = Constant(source, "AttackAction");

        // Declared in code and in the project file, and mapped there: the engine refuses a mapping whose
        // intent it was never told about, so both halves are what make the key a control.
        Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", project, StringComparison.Ordinal);
        Assert.Contains($"Intent=\"{intent}\"", project, StringComparison.Ordinal);

        // The key the product actually declared, named so a change to it is a decision rather than a silent
        // edit. The donor's own act control is B, triggered with key repeat so holding it keeps attacking.
        Assert.Contains("Trigger=\"key:key-b:held\"", project, StringComparison.Ordinal);

        // The host hands the ruleset the declared names, and the companion sends the declared action on the
        // product's own contract: the reader is composed over exactly these names.
        Assert.Contains("new CombatIntentNames(", product, StringComparison.Ordinal);
        Assert.Contains("Combat: _combat", product, StringComparison.Ordinal);
        AssertUiConstant(ui, "ACTION_ATTACK", action);
        AssertUiConstant(ui, "UI_ACTION_CONTRACT", Constant(source, "UiActionContract"));
    }

    [Fact]
    public void A_creature_the_content_places_is_hostile_on_sight_and_the_party_acts_by_recovery()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), Monsters(hostility: 2, recovery: 100), PartyDocument()]);

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;

        // Nothing is in front of the party before the fight has read the world.
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("available").AsBoolean());
        Assert.False(combat.Field("engaged").AsBoolean());

        session.Update(ProductTestContext.Update(++step, 1));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");

        // The creature is hostile because of what it is: its row's band is two, which notices the party from
        // 2560 units, and the placement stands a hundred units away.
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1d, combat.Field("opposition").AsNumber());
        Assert.Equal(2d, combat.Field("members").Length());
        Assert.Equal(2d, combat.Field("ready").AsNumber());
        ProjectedNode enemy = combat.Field("enemies").Item(0);
        Assert.Equal("A beast", enemy.Field("name").AsString());
        Assert.Equal(100d, enemy.Field("distance").AsNumber());

        // A creature's first recovery is drawn inside the row's own, which the donor's two constants make
        // 23.437 seconds of game time: one tick is 1000/128 of a real second and a real second is thirty game
        // seconds, so the row's hundred ticks are that long and no first recovery is longer.
        Assert.InRange(enemy.Field("recoverySeconds").AsNumber(), 0, 23.437);

        // The act control orders the party to attack: both members act, each pays its own recovery, and the
        // panel shows which of them may still act. What each pays is the donor's character recovery: a
        // character holding nothing swings on the staff's hundred ticks less the speed bonus its Speed
        // attribute is worth — two ticks at seventeen, five at twenty-five — which is the whole of the sum
        // this build can read, because a party cannot wear anything yet.
        session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("applied", combat.Field("outcome").AsString());
        Assert.Equal(0d, combat.Field("ready").AsNumber());
        Assert.Contains("attacks A beast", combat.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(22.969, combat.Field("members").Item(0).Field("recoverySeconds").AsNumber(), 3);
        Assert.Equal(22.266, combat.Field("members").Item(1).Field("recoverySeconds").AsNumber(), 3);
        Assert.False(combat.Field("members").Item(0).Field("ready").AsBoolean());

        // Letting go of the control and asking again while everybody recovers is refused by name rather than
        // quietly doing nothing.
        session.Update(ProductTestContext.Update(++step, 1, Released(ProductIdentity.AttackIntent)));
        session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("refused", combat.Field("outcome").AsString());
        Assert.Equal("recovering", combat.Field("code").AsString());

        // And game time is what releases them: let go of the control, hold the world for thirty game
        // seconds, and the party is ready again — which is more than the twenty-three seconds a swing costs.
        session.Update(ProductTestContext.Update(++step, 1, Released(ProductIdentity.AttackIntent)));
        for (int index = 0; index < 60; index++) session.Update(ProductTestContext.Update(++step, 1));
        Assert.Equal(2d, ProjectedNode.Of(ui.Latest().Value).Field("combat").Field("ready").AsNumber());
    }

    [Fact]
    public void What_the_party_has_done_is_what_makes_a_person_hostile()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100, person: true), Monsters(hostility: 4, recovery: 100), PartyDocument()]);

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        // The creature's band is four, which notices the party from 10240 units, so the fight is already on
        // when the party walks in; the person standing there is in no fight at all.
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1d, combat.Field("opposition").AsNumber());
        Assert.Equal("A beast", combat.Field("enemies").Item(0).Field("name").AsString());
    }

    [Fact]
    public void A_creature_naming_a_monster_no_row_describes_refuses_the_session_by_name()
    {
        (string Path, string Text)[] files =
        [
            .. World(monsterAt: 100, monsterRow: "4711"),
            Monsters(hostility: 2, recovery: 100, id: "7"),
            PartyDocument(),
        ];

        // A creature the content cannot price is a defect of the content, named where the session would be
        // composed rather than met as a monster that never acts.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(files);
        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true)));
        Assert.Contains(error.Issues, issue => issue.Code == "creature-monster-unknown");
    }

    [Fact]
    public void A_creatures_first_recovery_is_a_keyed_roll_so_the_same_world_fights_the_same_fight()
    {
        // The engine's random service takes an explicit seed and reads no clock, so the same content and the
        // same roll produce the same first recovery: nothing about a fight has to be recorded to replay it.
        double first = FirstRecoverySeconds(roll: 10_000);
        double second = FirstRecoverySeconds(roll: 10_000);
        double other = FirstRecoverySeconds(roll: 2_000);

        Assert.Equal(first, second, 3);
        Assert.NotEqual(first, other);
        Assert.Equal(10.0, first, 3);

        // A draw past the row's own recovery is clamped to it rather than lengthening it: the row's hundred
        // ticks are the most a creature of this row can owe before its first action.
        // The row's hundred ticks are 23437.5 milliseconds of game time, rounded to the millisecond the
        // kit counts in, and a draw past them is clamped to exactly that.
        Assert.Equal(23.438, FirstRecoverySeconds(roll: 999_999), 3);
    }

    /// <summary>The first recovery a creature of a staged world is seen with, for one roll of the engine's service.</summary>
    private static double FirstRecoverySeconds(long roll)
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), Monsters(hostility: 2, recovery: 100), PartyDocument()]);
        context.Engine.Random.DrawKeyed(new KeyedRngRequest(0, "unused", "unused", 0, 0));
        FakeEngineContext fake = (FakeEngineContext)context.Engine;
        fake.RandomService.Roll = roll;

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));
        return ProjectedNode.Of(ui.Latest().Value)
            .Field("combat")
            .Field("enemies")
            .Item(0)
            .Field("recoverySeconds")
            .AsNumber();
    }

    /// <summary>What this game's own recovery answers are worth, read from the policy the product composes.</summary>
    [Fact]
    public void This_games_recovery_answers_are_the_donors_own_numbers()
    {
        ContentCatalog catalog = Catalog([.. World(monsterAt: 100), Monsters(hostility: 2, recovery: 100), PartyDocument()]);
        Assert.NotNull(catalog);
        Assert.Equal(1, MightAndMagic7Combat.Compose(catalog, random: null).MonsterCount);
    }


    [Fact]
    public void An_attack_resolves_into_the_donors_own_damage_and_the_rows_own_resistance()
    {
        // The chance is the donor's own test, stated here independently of the code that computes it
        // (OpenEnroth src/Engine/Objects/Character.cpp:6263-6300): the roll is uniform over the target's
        // armour class plus twice the attack bonus plus thirty, and it lands when it beats the armour class
        // plus fifteen at close range. Five points of armour and one point of attack bonus — an accuracy of
        // fifteen — are thirty-seven outcomes, twenty of which do not land, so seventeen do.
        double expected = Math.Round(17d * 10000d / 37d, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, Strike(physicalResistance: "0").Chance);
        Assert.Equal(expected, Strike(physicalResistance: "30", halves: 1).Chance);
        Assert.Equal(expected, Strike(physicalResistance: "Imm").Chance);
    }

    /// <summary>What one blow of a character's own hand does through a row's resistance, step by step.</summary>
    [Fact]
    public void A_resistant_target_takes_measurably_less_and_an_immune_one_takes_none()
    {
        // A character's own hand against a stated monster row: a level-two creature of five points of armour
        // and forty hit points, and a physical resistance this game reads from the table's own column. The
        // engine service this suite hands the product answers its maximum for every draw, so the character's
        // three-sided die is a three and their might of seventeen is worth two more.
        StrikeFacts plain = Strike(physicalResistance: "0");
        Assert.Equal(5d, plain.Rolled);
        Assert.Equal(5d, plain.Damage);
        Assert.Equal(35d, plain.HitPoints);

        // Thirty points of physical resistance is checked as the donor checks it (OpenEnroth
        // src/Engine/Objects/Actor.cpp:3743-3758): a roll over the resistance plus thirty, halving what is
        // left, four checks at most. This test's draws pass the first check and fail the second, so the blow
        // lands for two rather than five — measurably less, and not nothing.
        StrikeFacts tough = Strike(physicalResistance: "30", halves: 1);
        Assert.Equal(5d, tough.Rolled);
        Assert.Equal(2d, tough.Damage);
        Assert.Equal(38d, tough.HitPoints);

        // The table writes full immunity as "Imm", which is no harm at all however hard the blow was: the hit
        // landed, the dice rolled, and the pool did not move.
        StrikeFacts warded = Strike(physicalResistance: "Imm");
        Assert.Equal(5d, warded.Rolled);
        Assert.Equal(0d, warded.Damage);
        Assert.Equal(40d, warded.HitPoints);
    }

    /// <summary>What one monster blow of a row's own dice does to a character, read from the panel.</summary>
    [Fact]
    public void A_monsters_blow_rolls_its_rows_own_dice_against_a_character()
    {
        // The row's `2D8+10` against a character wearing nothing: the service answers its maximum per die, so
        // sixteen plus ten is twenty-six, and nothing in this build resists it — a character's resistances
        // come from items and spells, and this party can wear nothing and knows none.
        BlowFacts blow = Blow(special: "0", level: 1, roll: 100);

        // A creature striking a character is the donor's other hit test (OpenEnroth
        // src/Engine/Objects/Actor.cpp:3691-3707): the roll is uniform over the character's armour class plus
        // twice the monster's level plus ten, and it lands when it beats the armour class plus five. A speed
        // of seventeen is two points of armour class, and a level-one creature rolls over fourteen outcomes
        // of which the seven above seven land — the armour class cancels out of the count, so a creature's
        // level is the whole of what its aim is worth.
        double expected = Math.Round(7d * 10000d / 14d, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, blow.Combat.Field("chance").AsNumber());
        Assert.Equal(26d, blow.Combat.Field("damageRolled").AsNumber());
        Assert.Equal(26d, blow.Combat.Field("damage").AsNumber());
        Assert.Equal(14, blow.HitPoints);
    }

    [Fact]
    public void A_monsters_own_attack_leaves_the_condition_its_row_states_and_the_temple_claims_it()
    {
        // A creature whose row states a special attack: this one poisons, at a level that always tries. The
        // service this suite hands the product draws its minimum, so the special attack comes in under its
        // chance and the character's saving throw — a hundred-sided roll under thirty — fails.
        BlowFacts poisoned = Blow(special: "Poison2", level: 5, roll: 0);

        // The row's own dice again, rolled low this time: two dice of one plus ten is twelve, and the
        // character resists nothing of it.
        Assert.Equal(12d, poisoned.Combat.Field("damage").AsNumber());
        Assert.Contains("Poison Medium", poisoned.Combat.Field("condition").AsString(), StringComparison.Ordinal);
        Assert.Contains("Poison Medium", poisoned.Conditions, StringComparison.Ordinal);
        Assert.Contains(MightAndMagic7Conditions.PoisonMedium, poisoned.Active);

        // And what the fight left is exactly what a counter claims: the affliction family clears it, so the
        // condition is a state a player can act on rather than one only the fight can see.
        Assert.Contains(poisoned.Cures, offer => offer.ClearsCondition(MightAndMagic7Conditions.PoisonMedium));

        // Sleep is the same path with a different effect: a sleeping character may not act, so the order that
        // would have them swing is refused by name and costs them nothing.
        BlowFacts asleep = Blow(special: "Asleep", level: 5, roll: 0);
        Assert.Contains("Sleep", asleep.Conditions, StringComparison.Ordinal);
        Assert.Contains(MightAndMagic7Conditions.Sleep, asleep.Active);
    }

    [Fact]
    public void A_member_taken_below_empty_is_unconscious_then_dead_and_the_offers_that_bring_them_back_claim_it()
    {
        // A character of twelve hit points and an endurance of fifteen — a bonus of one — against a blow of
        // twelve: the pool empties exactly, which the donor calls unconsciousness, and one more point past
        // empty is death (OpenEnroth src/Engine/Objects/Character.cpp:1310-1316: a character is unconscious
        // while health plus base endurance is at least one, and dead otherwise).
        using Falling fall = new();
        ProjectedNode first = fall.Strike();
        Assert.Equal(0d, first.Field("members").Item(0).Field("hitPoints").AsNumber());
        Assert.Contains("Unconscious", first.Field("members").Item(0).Field("conditions").AsString(), StringComparison.Ordinal);
        Assert.True(first.Field("members").Item(0).Field("down").AsBoolean());
        Assert.Contains(MightAndMagic7Conditions.Unconscious, fall.Active);

        // The member is still in the party and still in the fight: death is a condition, not a removal.
        Assert.Single(fall.Party.Members);

        // A second blow takes them past empty, and the ladder moves from unconsciousness to death.
        ProjectedNode second = fall.Strike();
        Assert.Contains("Dead", second.Field("members").Item(0).Field("conditions").AsString(), StringComparison.Ordinal);
        Assert.Contains(MightAndMagic7Conditions.Dead, fall.Active);
        Assert.DoesNotContain(MightAndMagic7Conditions.Unconscious, fall.Active);

        // Every condition this game can leave is one a stated path ends: a cure the temple offers, or the
        // night's rest the ruleset names. Nothing here is a state nothing can clear.
        foreach (ConditionId condition in new[]
        {
            MightAndMagic7Conditions.Cursed, MightAndMagic7Conditions.Weak, MightAndMagic7Conditions.Sleep,
            MightAndMagic7Conditions.Fear, MightAndMagic7Conditions.Drunk, MightAndMagic7Conditions.Insane,
            MightAndMagic7Conditions.PoisonWeak, MightAndMagic7Conditions.PoisonMedium, MightAndMagic7Conditions.PoisonSevere,
            MightAndMagic7Conditions.DiseaseWeak, MightAndMagic7Conditions.DiseaseMedium, MightAndMagic7Conditions.DiseaseSevere,
            MightAndMagic7Conditions.Paralyzed, MightAndMagic7Conditions.Unconscious,
            MightAndMagic7Conditions.Dead, MightAndMagic7Conditions.Petrified, MightAndMagic7Conditions.Eradicated,
        })
        {
            List<ActiveCondition> suffering = [new ActiveCondition(condition)];
            bool cured = MightAndMagic7Conditions.Cures(suffering).Any(offer => offer.ClearsCondition(condition));
            bool rested = MightAndMagic7Conditions.RestClears.Contains(condition);
            Assert.True(cured || rested, $"'{condition}' is a condition nothing can clear.");
        }

        // Eradication is the far end and it is not a depth of damage: the table states it as a monster's own
        // attack, and the counter charges ten times as much to undo it.
        BlowFacts eradicated = Blow(special: "Errad", level: 5, roll: 0);
        Assert.Contains("Eradicated", eradicated.Conditions, StringComparison.Ordinal);
        Assert.Contains(
            eradicated.Cures,
            offer => offer.ClearsCondition(MightAndMagic7Conditions.Eradicated) &&
                     offer.Value == MightAndMagic7Conditions.EradicatedMultiplier);
    }

    /// <summary>One party attack against a monster row of a stated physical resistance, read from the panel.</summary>
    /// <param name="physicalResistance">What the row states in its physical resistance column.</param>
    /// <param name="halves">
    /// How many of the donor's four resistance checks pass: every check draws its maximum unless this says
    /// otherwise, and a check below thirty ends the halving, so this is how many times the blow is halved.
    /// </param>
    private static StrikeFacts Strike(string physicalResistance, int halves = 4)
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), MonsterRow(physicalResistance: physicalResistance), FighterParty()]);
        FakeEngineContext fake = (FakeEngineContext)context.Engine;
        fake.RandomService.Answer = request => request.Key.Contains("/resistance/", StringComparison.Ordinal)
            ? request.Key.EndsWith("/resistance/0", StringComparison.Ordinal) && halves == 1 ? 30 : halves == 1 ? 0 : 100
            : null;

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));
        session.Update(ProductTestContext.Update(2, 1, Digital(ProductIdentity.AttackIntent)));

        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("resolved").AsBoolean());
        return new StrikeFacts(
            combat.Field("chance").AsNumber(),
            combat.Field("damageRolled").AsNumber(),
            combat.Field("damage").AsNumber(),
            combat.Field("enemies").Item(0).Field("hitPoints").AsNumber());
    }

    /// <summary>One monster blow against a member, read off the panel before the session is disposed.</summary>
    private static BlowFacts Blow(string special, int level, long roll)
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), MonsterRow(special: special, level: level), FighterParty()]);
        FakeEngineContext fake = (FakeEngineContext)context.Engine;
        fake.RandomService.Roll = roll;

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        // A creature's first recovery is drawn, so a moment of game time passes before it may act; the one
        // clock advancing is what releases it, exactly as it releases a character.
        for (int index = 0; index < 5; index++) session.Update(ProductTestContext.Update(2, 1));

        // The creature strikes the party's member through the fight's own one entry, which is the same gated
        // door the player's control and a later AI owner use.
        MightAndMagic7Session played = (MightAndMagic7Session)session;
        PartyMember member = played.Party!.Members[0];
        CombatState fight = played.Combat!;
        CombatResult result = fight.Order(new AttackOrder(fight.Opposition[0].Id, AttackKind.Melee, fight.Combatants[0].Id));
        Assert.True(result.IsApplied, $"{result.Code}: {result.Message}");
        session.Update(ProductTestContext.Update(3, 1));

        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        return new BlowFacts(
            combat,
            member.Conditions.Active.Select(condition => condition.Condition).ToArray(),
            member.Resources.HitPoints.Current,
            MightAndMagic7Conditions.Cures(member.Conditions.Active));
    }

    /// <summary>What one blow of the party's hand came to, as the panel published it.</summary>
    private sealed record StrikeFacts(double Chance, double Rolled, double Damage, double HitPoints);

    /// <summary>What one monster's blow left, read before the session that owns the party is disposed.</summary>
    private sealed record BlowFacts(
        ProjectedNode Combat,
        IReadOnlyList<ConditionId> Active,
        int HitPoints,
        IReadOnlyList<ServiceOffer> Cures)
    {
        /// <summary>What is acting on the member, as the panel published it.</summary>
        internal string Conditions => Combat.Field("members").Item(0).Field("conditions").AsString();
    }

    /// <summary>
    /// A session whose creature empties a small member's pool blow by blow, kept alive while a test reads
    /// what each blow left.
    /// </summary>
    private sealed class Falling : IDisposable
    {
        private readonly IGameSession _session;
        private readonly RecordingUiService _ui;
        private ulong _step;

        internal Falling()
        {
            (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
                [.. World(monsterAt: 100), MonsterRow(damage: "1D4+8", hitPoints: 200), FragileParty()]);
            FakeEngineContext fake = (FakeEngineContext)context.Engine;
            fake.RandomService.Answer = request => request.Key.EndsWith("/hit", StringComparison.Ordinal) ? 0 : 10_000;
            _ui = ui;
            _session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
            _session.Start();
            _session.Update(ProductTestContext.Update(1, 1));
        }

        /// <summary>The party the session plays, which its member count is read from.</summary>
        internal PartyEntity Party => Played.Party!;

        /// <summary>What is acting on the member now.</summary>
        internal IReadOnlyList<ConditionId> Active =>
            [.. Played.Party!.Members[0].Conditions.Active.Select(condition => condition.Condition)];

        private MightAndMagic7Session Played => (MightAndMagic7Session)_session;

        /// <summary>Lets the creature recover, strikes the member, and returns what the panel now shows.</summary>
        internal ProjectedNode Strike()
        {
            // A row's hundred ticks are twenty-three and a half game seconds, and one admitted update of a
            // sixtieth of a second at this game's scale is half a second of game time: sixty of them are half
            // a minute, which is past what the creature owes between blows.
            for (int index = 0; index < 60; index++) _session.Update(ProductTestContext.Update(++_step, 1));
            CombatState fight = Played.Combat!;
            CombatResult result = fight.Order(new AttackOrder(fight.Opposition[0].Id, AttackKind.Melee, fight.Combatants[0].Id));
            Assert.True(result.IsApplied, $"{result.Code}: {result.Message}");
            _session.Update(ProductTestContext.Update(++_step, 1));
            return ProjectedNode.Of(_ui.Latest().Value).Field("combat");
        }

        /// <inheritdoc />
        public void Dispose() => _session.Dispose();
    }

    /// <summary>A monster row shaped the way the importer emits one: typed columns and the whole raw row.</summary>
    private static (string Path, string Text) MonsterRow(
        string physicalResistance = "0",
        string special = "0",
        int level = 2,
        string damage = "2D8+10",
        string attackType = "Phys",
        int hitPoints = 40,
        int armorClass = 5,
        int recovery = 100,
        int hostility = 2)
    {
        string[] cells = new string[39];
        for (int index = 0; index < cells.Length; index++) cells[index] = "0";
        cells[0] = "7";
        cells[1] = "A beast";
        cells[3] = level.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[4] = hitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[5] = armorClass.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[12] = hostility.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[14] = recovery.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[16] = special;
        cells[17] = attackType;
        cells[18] = damage;
        cells[37] = physicalResistance;
        string columns = string.Join(", ", cells.Select(cell => $"\"{cell}\""));
        return ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "level": {{level}}, "hitPoints": {{hitPoints}},
                  "armorClass": {{armorClass}}, "hostility": {{hostility}}, "recovery": {{recovery}},
                  "columns": [ {{columns}} ] }
              ]
            }
            """);
    }

    /// <summary>A party of one character whose attributes and pools the resolution tests are read against.</summary>
    private static (string Path, string Text) FighterParty() => PartyOf(hitPoints: 40, endurance: 15);

    /// <summary>A party of one character a blow of twelve empties exactly.</summary>
    private static (string Path, string Text) FragileParty() => PartyOf(hitPoints: 12, endurance: 15);

    private static (string Path, string Text) PartyOf(int hitPoints, int endurance) =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party",
                  "coins": 200,
                  "food": 6,
                  "reputation": 0,
                  "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1,
                      "hitPoints": {{hitPoints}}, "spellPoints": 0, "attributes": [
                        { "id": "Might", "value": 17 }, { "id": "Accuracy", "value": 15 },
                        { "id": "Endurance", "value": {{endurance}} }, { "id": "Luck", "value": 11 },
                        { "id": "Speed", "value": 17 }, { "id": "Personality", "value": 11 },
                        { "id": "Intellect", "value": 11 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """);


    private static string SourceDirectory() => Path.Combine(RepositoryRoot(), "src", "PartyRpg.Host");

    private static string ProjectFile() => Path.Combine(SourceDirectory(), "PartyRpg.Host.csproj");

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

    private static string Constant(string source, string name)
    {
        Match match = Regex.Match(source, $@"const string {name} = ""([^""]*)"";", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"ProductIdentity.cs must declare {name}.");
        return match.Groups[1].Value;
    }

    private static void AssertUiConstant(string uiSource, string name, string expected)
    {
        Match match = Regex.Match(uiSource, $@"const {name} = '([^']*)';", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"src/ui/main.ts must declare {name}.");
        Assert.Equal(expected, match.Groups[1].Value);
    }

    /// <summary>One digital event on the act control, as the engine admits a key.</summary>
    private static ProductInputEvent Digital(string intent) => ProductTestContext.Digital(intent);

    /// <summary>The key coming back up, which is what ends a held control.</summary>
    private static ProductInputEvent Released(string intent) => ProductTestContext.Digital(intent, InputEdge.Released);

    private static ContentCatalog Catalog((string Path, string Text)[] files)
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(files);
        return ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
    }

    /// <summary>
    /// A world of two places: the party's starting region holds a creature where the test says and, when a
    /// test asks for one, a person standing near it.
    /// </summary>
    private static (string Path, string Text)[] World(double monsterAt, bool person = false, string monsterRow = "7")
    {
        string placements = $$"""
            { "id": "beast", "kind": "monster", "monster": "{{monsterRow}}", "x": {{monsterAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "y": 0, "z": 0 }
            """;
        if (person)
        {
            placements += ", { \"id\": \"person-1\", \"kind\": \"person\", \"x\": 200, \"y\": 0, \"z\": 0, \"people\": [ \"person-1\" ] }";
        }

        return
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
                    { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" },
                    { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                    { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                    { "path": "people.json", "documentId": "people", "definitionKind": "person" }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
                $$"""
                {
                  "documentId": "places",
                  "definitionKind": "place",
                  "entries": [
                    { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                      "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ],
                      "placements": [ {{placements}} ] },
                    { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                      "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
                """
                { "documentId": "people", "definitionKind": "person", "entries": [ { "id": "person-1", "name": "A bystander" } ] }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
                """
                { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
                """),
        ];
    }

    /// <summary>The monster table a creature placement names a row in.</summary>
    private static (string Path, string Text) Monsters(int hostility, int recovery, string id = "7") =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [ { "id": "{{id}}", "name": "A beast", "hostility": {{hostility}}, "recovery": {{recovery}} } ]
            }
            """);

    /// <summary>The scenario's party, as this suite's other cases stage it.</summary>
    private static (string Path, string Text) PartyDocument() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party",
                  "coins": 200,
                  "food": 6,
                  "reputation": 0,
                  "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40,
                      "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 17 }, { "id": "Accuracy", "value": 15 },
                                      { "id": "Endurance", "value": 15 }, { "id": "Luck", "value": 11 },
                                      { "id": "Speed", "value": 17 } ],
                      "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": [] },
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 1, "hitPoints": 24,
                      "spellPoints": 15,
                      "attributes": [ { "id": "Might", "value": 11 }, { "id": "Accuracy", "value": 15 },
                                      { "id": "Endurance", "value": 11 }, { "id": "Luck", "value": 15 },
                                      { "id": "Speed", "value": 25 } ],
                      "skills": [ { "id": "Staff", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """);
}
