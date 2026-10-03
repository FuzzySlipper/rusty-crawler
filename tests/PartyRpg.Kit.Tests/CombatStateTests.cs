using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one combat state: a fight over the live world, paced by one recovery quantity per actor, with
/// hostility as world state and recovery advanced by the session's one clock.
/// </summary>
/// <remarks>
/// Every number here is the test's own, which is the point of the seam: the kit holds no recovery value, no
/// notice range, and no reach, so a test states its own and demands that the same mechanism serve it.
/// </remarks>
public sealed class CombatStateTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Hall = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");

    /// <summary>What one attack of this suite's actors costs, so a test can count updates against it.</summary>
    private static readonly GameDuration AttackRecovery = GameDuration.FromSeconds(1);

    /// <summary>What a creature of this suite's costs before it acts the first time.</summary>
    private static readonly GameDuration FirstRecovery = GameDuration.FromSeconds(2);

    [Fact]
    public void A_combatant_identity_reads_back_from_the_form_it_is_published_in()
    {
        CombatantId member = CombatantId.Of(new PartyMemberId(3));
        CombatantId actor = CombatantId.Of(new EntityId(3));

        Assert.True(CombatantId.TryParse(member.ToString(), out CombatantId readMember));
        Assert.True(CombatantId.TryParse(actor.ToString(), out CombatantId readActor));
        Assert.Equal(member, readMember);
        Assert.Equal(actor, readActor);

        // The two halves of a fight never name the same actor, even under the same number.
        Assert.NotEqual(readMember, readActor);
        Assert.False(CombatantId.TryParse("stranger:3", out _));
        Assert.False(CombatantId.TryParse("member:three", out _));
        Assert.False(CombatantId.TryParse(string.Empty, out _));
    }

    [Fact]
    public void Observation_reads_a_creatures_live_pose_after_motion_and_the_shared_party_pose()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        CombatState combat = Fight(world, party);
        Arrive(world);
        combat.Step();
        PlacePopulationEntity creature = Assert.Single(combat.Opposition).Subject.Entity!;
        PlacePose moved = new(250, -175, 32, 0, 0);
        creature.MoveTo(moved);
        CombatSnapshot reading = CombatSnapshot.From(combat);
        Assert.Equal(moved, Assert.Single(reading.Enemies).Pose);
        Assert.All(reading.Members, member => Assert.Equal(world.Party.PlacePose, member.Pose));
    }

    [Fact]
    public void Entering_and_leaving_a_fight_leaves_the_place_the_pose_and_the_population_exactly_as_they_were()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        CombatState combat = Fight(world, party);
        Arrive(world);

        // Nothing has been read yet, so nothing is in the fight beyond the party itself.
        Assert.Empty(combat.Combatants);

        combat.Step();
        PlacePose pose = world.Party.PlacePose;
        IReadOnlyList<EntityId> before = [.. world.Population.Entities.Select(entity => entity.Id)];
        IReadOnlyList<PlacePose> standing = [.. world.Population.Entities.Select(entity => entity.Pose)];
        Assert.True(combat.IsEngaged);

        // Acting in the fight changes the fight and nothing else: no scene, no second population, no move.
        Assert.Single(combat.Engage());
        Assert.Equal(Hall, world.Place);
        Assert.Equal(pose, world.Party.PlacePose);
        Assert.Equal(before, [.. world.Population.Entities.Select(entity => entity.Id)]);
        Assert.Equal(standing, [.. world.Population.Entities.Select(entity => entity.Pose)]);

        // Leaving the place ends it: the entities a population creates live only for the visit that made
        // them, so a place the party walked out of holds nobody to fight.
        PlaceTransition outbound = Assert.Single(world.Graph.TransitionsFrom(Hall));
        Assert.True(world.Travel(outbound, TransitionKind.Entrance).Arrived);
        // The population follows the party when the world steps it, which is what an update does: the place
        // the party left holds nobody, and the fight reads exactly that.
        world.Populate();
        combat.Step();
        Assert.False(combat.IsEngaged);
        Assert.Equal(4, combat.Combatants.Count);
        Assert.All(combat.Combatants, combatant => Assert.Equal(CombatSide.Party, combatant.Side));
    }

    [Fact]
    public void A_recovering_actor_cannot_act_and_game_time_alone_releases_it()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        CombatState combat = Fight(world, party);
        Arrive(world);
        combat.Step();

        CombatantId member = combat.Combatants[0].Id;
        CombatResult first = combat.Order(new AttackOrder(member, AttackKind.Melee, null));
        Assert.True(first.IsApplied);
        Assert.False(combat.Find(member)!.IsReady);
        Assert.Equal(AttackRecovery, combat.Find(member)!.Recovery);

        // The same order again is refused by name rather than quietly doing nothing, and nothing about the
        // actor changed: a refusal is not a partial action.
        CombatResult refused = combat.Order(new AttackOrder(member, AttackKind.Melee, null));
        Assert.False(refused.IsApplied);
        Assert.Equal("recovering", refused.Code);
        Assert.Equal(AttackRecovery, combat.Find(member)!.Recovery);
        Assert.Same(refused, combat.LastOrder);
        Assert.NotNull(combat.LastAttack);

        // Game time is the only thing that releases it, and only in the amount that passed.
        combat.Observe(Advance(400));
        Assert.False(combat.Find(member)!.IsReady);
        Assert.Equal(GameDuration.FromMilliseconds(600), combat.Find(member)!.Recovery);
        Assert.Equal("recovering", combat.Order(new AttackOrder(member, AttackKind.Melee, null)).Code);

        combat.Observe(Advance(600));
        Assert.True(combat.Find(member)!.IsReady);
        Assert.True(combat.Order(new AttackOrder(member, AttackKind.Melee, null)).IsApplied);

        // Time that passes while an actor is ready banks nothing: recovery is a debt, not a cycle.
        combat.Observe(Advance(5000));
        Assert.True(combat.Find(member)!.IsReady);
    }

    [Fact]
    public void An_actor_that_is_not_in_the_fight_cannot_be_ordered_and_cannot_be_attacked()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        CombatState combat = Fight(world, party);
        Arrive(world);
        combat.Step();

        CombatantId nobody = CombatantId.Of(new EntityId(9999));
        CombatResult refused = combat.Order(new AttackOrder(nobody, AttackKind.Melee, null));
        Assert.False(refused.IsApplied);
        Assert.Equal("unknown-combatant", refused.Code);

        // A door is not a creature, so it was never a combatant and an order naming it names nothing.
        CombatantId door = CombatantId.Of(world.Population.Entities.Single(entity => entity.Content.Kind == "door").Id);
        Assert.Equal("unknown-target", combat.Order(new AttackOrder(combat.Combatants[0].Id, AttackKind.Melee, door)).Code);

        // The party's own side is not a target: one member attacking another is refused rather than turned
        // on the party.
        CombatResult friendly = combat.Order(new AttackOrder(combat.Combatants[0].Id, AttackKind.Melee, combat.Combatants[1].Id));
        Assert.Equal("friendly-target", friendly.Code);
    }

    [Fact]
    public void Hostility_is_world_state_a_creature_is_hostile_on_sight_and_the_party_can_make_one_hostile()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100, farMonsterAt: 4000);
        CombatState combat = Fight(world, party);
        Arrive(world);
        combat.Step();

        // What it is decides first: the creature inside its notice range is an enemy, the one outside it is
        // not in the fight yet, and the person standing there starts no fight at all.
        Combatant near = combat.Combatants.Single(combatant => combatant.Name == "A beast");
        Combatant far = combat.Combatants.Single(combatant => combatant.Name == "A beast far off");
        Combatant person = combat.Combatants.Single(combatant => combatant.Name == "A person");
        Assert.Equal(CombatSide.Opposition, near.Side);
        Assert.Equal(CombatSide.Neutral, far.Side);
        Assert.Equal(CombatSide.Neutral, person.Side);
        Assert.True(combat.IsEngaged);
        Assert.Equal(1, combat.Combatants.Count(combatant => combatant.Side == CombatSide.Opposition));

        // What the party has done decides second, and it outlives the pacing: attacking the person puts
        // them into the fight even though nothing about them is aggressive.
        Assert.True(combat.Order(new AttackOrder(combat.Combatants[0].Id, AttackKind.Melee, person.Id)).IsApplied);
        Assert.Equal(CombatSide.Opposition, person.Side);

        // The provocation is remembered across a re-read of the world rather than recomputed away.
        combat.Step();
        Assert.Equal(CombatSide.Opposition, combat.Find(person.Id)!.Side);

        // The whole fight is a read of the world every update: nothing is a mode flag, so a party that walks
        // into a creature's notice range is in a fight in the same step that moved it.
        Assert.DoesNotContain(combat.Combatants, combatant => combatant.Side == CombatSide.Neutral && combatant.Name == "A beast");
    }

    [Fact]
    public void The_same_state_produces_the_same_fight_under_a_seeded_random_service()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        SeededRandom random = new();
        CombatState combat = Fight(world, party, random);
        Arrive(world);
        combat.Step();

        // A second, identical state over an identical world draws the same values, so the fight the panel
        // shows is the fight that was played rather than an accident of the order actors were read in.
        using PartyEntity other = Party();
        using SessionWorld second = World(other, monsterAt: 100);
        CombatState repeated = Fight(second, other, new SeededRandom());
        Arrive(second);
        repeated.Step();

        Assert.Equal(
            combat.Combatants.Select(Combatant),
            repeated.Combatants.Select(Combatant));
        // One draw per creature: the beast and the person, each keyed by the actor it belongs to.
        Assert.Equal(2, random.Draws);
        Assert.Equal(6, combat.Combatants.Count);

        static string Combatant(Combatant actor) =>
            string.Create(CultureInfo.InvariantCulture, $"{actor.Id}|{actor.Name}|{actor.Side}|{actor.Recovery.Milliseconds}");
    }

    [Fact]
    public void Applied_blows_are_recorded_in_order_with_growing_serials_and_refusals_are_not()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        CombatState combat = Fight(world, party);
        Arrive(world);
        combat.Step();
        Assert.Empty(combat.RecentBlows);

        CombatantId member = combat.Combatants[0].Id;
        CombatResult first = combat.Order(new AttackOrder(member, AttackKind.Melee, null));
        Assert.Equal("recovering", combat.Order(new AttackOrder(member, AttackKind.Melee, null)).Code);
        CombatBlow only = Assert.Single(combat.RecentBlows);
        Assert.Same(first, only.Result);
        Assert.Equal(member, only.Result.Initiated!.Actor);

        // The record keeps only the latest blows, and a serial never repeats, so a reader can tell what is new.
        for (int blow = 0; blow < CombatState.RecentBlowLimit + 3; blow++)
        {
            combat.Observe(Advance(1000));
            Assert.True(combat.Order(new AttackOrder(member, AttackKind.Melee, null)).IsApplied);
        }

        Assert.Equal(CombatState.RecentBlowLimit, combat.RecentBlows.Count);
        Assert.Equal(CombatState.RecentBlowLimit + 4, combat.RecentBlows[^1].Serial);
        Assert.True(combat.RecentBlows.Zip(combat.RecentBlows.Skip(1)).All(pair => pair.Second.Serial == pair.First.Serial + 1));
    }

    [Fact]
    public void Ordinary_payload_and_declared_cycle_order_only_the_durable_selected_member()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"), channel,
            new SessionOwners(TestClock.Create()), new SessionParty.Playing(World: world, Party: party),
            rules: new SessionRules { Combat = Capabilities.Combat(new TestCombatRule(null)) },
            controls: new SessionControls { Combat = new CombatIntentNames("test.attack", "test.actions", nextMember: "test.next-member") });
        Arrive(world);
        session.Start();
        session.Update(Admitted.Update(1, 0));
        CombatState combat = session.Combat!;
        PartyMemberId first = party.Members[0].Id;
        PartyMemberId second = party.Members[1].Id;
        session.Update(Admitted.Update(2, 0, Payload($$"""{"action":"party.select-member","member":"{{second}}"}"""), Attack()));
        Assert.Equal(second, party.Roster.SelectedMember);
        Assert.Equal(CombatantId.Of(second), combat.LastAttack!.Actor);
        Assert.Equal(AttackRecovery, combat.Selected!.Recovery);
        Assert.All(combat.Combatants.Where(actor => actor.Side == CombatSide.Party && actor.Id != CombatantId.Of(second)), actor => Assert.True(actor.IsReady));

        // Recovery leaves the choice intact and yields the existing named refusal, spending nothing.
        session.Update(Admitted.Update(3, 0, Release()));
        session.Update(Admitted.Update(4, 0, Attack()));
        Assert.Equal(CombatCodes.Recovering, combat.LastOrder!.Refusal!.Code);
        Assert.Equal(second, party.Roster.SelectedMember);
        Assert.Equal(AttackRecovery, combat.Selected.Recovery);
        Assert.True(combat.Find(CombatantId.Of(first))!.IsReady);

        session.Update(Admitted.Update(5, 0, Release()));
        session.Update(Admitted.Update(6, 0, Admitted.Digital("test.next-member"), Attack()));
        Assert.Equal(party.Members[2].Id, party.Roster.SelectedMember);
        Assert.Equal(CombatantId.Of(party.Members[2].Id), combat.LastAttack!.Actor);
        Assert.Equal(AttackRecovery, combat.Selected.Recovery);
        Assert.Equal(AttackRecovery, combat.Find(CombatantId.Of(second))!.Recovery);
        Assert.True(combat.Find(CombatantId.Of(first))!.IsReady);

        // An unknown choice changes neither the selection nor anybody's recovery.
        session.Update(Admitted.Update(7, 0, Release(), Payload("""{"action":"party.select-member","member":"900"}""")));
        Assert.Equal(PartySelectionCodes.UnknownMember, party.Roster.SelectionRefusal!.Code);
        Assert.Equal(party.Members[2].Id, party.Roster.SelectedMember);
        ProjectedNode reading = channel.Latest().Field("combat");
        Assert.Equal(PartySelectionCodes.UnknownMember, reading.Field("selectionCode").AsString());
        Assert.True(reading.Field("members").Item(2).Field("selected").AsBoolean());
    }

    [Fact]
    public void A_selected_ready_member_cannot_spend_another_members_turn()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        CombatState combat = Fight(world, party);
        Arrive(world);
        combat.Step();
        combat.TogglePacing();
        combat.Turns.Next();
        Combatant current = combat.Turns.Current!;
        Assert.Equal(CombatSide.Party, current.Side);
        PartyMember second = party.Members.First(member => CombatantId.Of(member.Id) != current.Id);
        Assert.Null(combat.SelectMember(second.Id));
        Assert.Equal(CombatCodes.SelectedMemberNotTurn, combat.EngageSelected()!.Refusal!.Code);
        Assert.Equal(current.Id, combat.Turns.Current!.Id);
        Assert.Equal(second.Id, party.Roster.SelectedMember);
        Assert.All(combat.Combatants.Where(actor => actor.Side == CombatSide.Party), actor => Assert.True(actor.IsReady));
    }

    [Fact]
    public void Recovery_is_advanced_by_the_admitted_update_and_by_nothing_else()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        GameClock clock = TestClock.Create();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(clock),
            new SessionParty.Playing(World: world, Party: party),
            rules: new SessionRules
            {
                Combat = Capabilities.Combat(new TestCombatRule(new SeededRandom())),
            },
            controls: new SessionControls
            {
                Combat = new CombatIntentNames("test.attack", "test.actions"),
            });

        Arrive(world);
        session.Start();
        session.Update(Admitted.Update(1, 1, Attack()));

        // The order acted and charged the acting member its recovery, inside the admitted update.
        CombatantId actor = session.Combat!.LastAttack!.Actor;
        Assert.False(session.Combat.Find(actor)!.IsReady);

        // The order is applied after the update's own clock advance, so the attack is charged in full: the
        // interval that update covered is time that had already passed, not time the attack gets back.
        Assert.Equal(1.0, session.Combat.Find(actor)!.Recovery.TotalSeconds, 3);

        // The act control is held rather than latched, so letting go is the event that stops the party
        // attacking; the update that carries the release advances the clock without ordering another attack.
        session.Update(Admitted.Update(2, 1, Release()));
        Assert.Equal(0.5, session.Combat.Find(actor)!.Recovery.TotalSeconds, 3);
        Assert.False(session.Combat.Find(actor)!.IsReady);

        // The clock advances by the admitted interval at the shipped scale: one sixtieth of a second of
        // engine time is half a second of game time, so a second update pays the rest of the attack.
        session.Update(Admitted.Update(3, 1));
        Assert.True(session.Combat.Find(actor)!.IsReady);

        // An update that admits no steps advances no game time, so it releases nobody: there is no timer here
        // and no frame counter, only the clock the session already had. An attack in such an update is still
        // an act — an order is an instant — and the recovery it charges simply waits for time to pass.
        session.Update(Admitted.Update(4, 0, Attack()));
        Assert.Equal(1.0, session.Combat.Find(actor)!.Recovery.TotalSeconds, 3);
        session.Update(Admitted.Update(5, 0, Release()));
        for (ulong step = 6; step <= 40; step++) session.Update(Admitted.Update(step, 0));
        Assert.Equal(1.0, session.Combat.Find(actor)!.Recovery.TotalSeconds, 3);

        // A held session admits no interval at all, so a player who holds the world holds the fight with it.
        session.Hold();
        for (ulong step = 41; step <= 80; step++) session.Update(Admitted.Update(step, 1));
        Assert.Equal(1.0, session.Combat.Find(actor)!.Recovery.TotalSeconds, 3);

        // Releasing the hold lets game time move again, and the recovery it was waiting for is paid.
        session.ReleaseHold();
        session.Update(Admitted.Update(81, 1));
        Assert.Equal(0.5, session.Combat.Find(actor)!.Recovery.TotalSeconds, 3);
    }

    [Fact]
    public void The_projection_publishes_who_is_engaged_who_is_ready_and_what_the_party_did()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party();
        using SessionWorld world = World(party, monsterAt: 100);
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(TestClock.Create()),
            new SessionParty.Playing(World: world, Party: party),
            rules: new SessionRules
            {
                Combat = Capabilities.Combat(new TestCombatRule(new SeededRandom())),
            },
            controls: new SessionControls
            {
                Combat = new CombatIntentNames("test.attack", "test.actions"),
            });

        Arrive(world);
        session.Start();
        // Before the first step nothing has been read, so the block says the session holds the mechanism and
        // nothing is in front of it yet.
        ProjectedNode combat = channel.Latest().Field("combat");
        Assert.True(combat.Field("available").AsBoolean());
        Assert.False(combat.Field("engaged").AsBoolean());

        session.Update(Admitted.Update(1, 1));
        combat = channel.Latest().Field("combat");
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1.0, combat.Field("opposition").AsNumber());
        Assert.Equal(4.0, combat.Field("ready").AsNumber());
        Assert.Equal("none", combat.Field("outcome").AsString());
        Assert.Equal(4, combat.Field("members").Length());
        Assert.Equal(1, combat.Field("enemies").Length());
        Assert.Equal("A beast", combat.Field("enemies").Item(0).Field("name").AsString());
        Assert.False(combat.Field("enemies").Item(0).Field("ready").AsBoolean());
        Assert.Equal(1.222, combat.Field("enemies").Item(0).Field("recoverySeconds").AsNumber(), 3);
        Assert.Equal(100.0, combat.Field("enemies").Item(0).Field("distance").AsNumber());

        // Only the selected member acts; the other three stay ready, and the panel shows both facts.
        session.Update(Admitted.Update(2, 1, Attack()));
        combat = channel.Latest().Field("combat");
        Assert.Equal("applied", combat.Field("outcome").AsString());
        Assert.Equal(3.0, combat.Field("ready").AsNumber());
        Assert.Equal(1.0, combat.Field("recoverySeconds").AsNumber());
        Assert.Contains("attacks A beast", combat.Field("message").AsString(), StringComparison.Ordinal);
        Assert.False(combat.Field("members").Item(0).Field("ready").AsBoolean());
        Assert.True(combat.Field("members").Item(0).Field("selected").AsBoolean());
        Assert.All(
            Enumerable.Range(1, 3).Select(index => combat.Field("members").Item(index)),
            member => Assert.True(member.Field("ready").AsBoolean()));

        // An order while the selected member is recovering is refused by name, and the refusal is what the panel shows
        // rather than a fight in which nothing was asked.
        session.Update(Admitted.Update(3, 1, Attack()));
        combat = channel.Latest().Field("combat");
        Assert.Equal("refused", combat.Field("outcome").AsString());
        Assert.Equal("recovering", combat.Field("code").AsString());
    }

    [Fact]
    public void The_act_control_is_read_from_a_held_mapping_a_press_and_a_panel_claim()
    {
        CombatInput reader = new(new CombatIntentNames("test.attack", "test.actions"));

        // A control a product maps as held arrives as a state for every update it stays down, and reports
        // nothing once it comes up — which is the shape the engine's own held mappings emit.
        Assert.True(reader.Read(new([Held()])));
        Assert.True(reader.Read(new([Held()])));
        Assert.False(reader.Read(new([])));

        // A pressed edge latches until its release, which is the shape a press mapping emits.
        Assert.True(reader.Read(new([Attack()])));
        Assert.True(reader.Read(new([])));
        Assert.False(reader.Read(new([Release()])));

        // A panel claim asks for exactly the update it arrives in, because a claim has nobody to send a
        // release: a button that stayed held would keep the party attacking after the player stopped.
        Assert.True(reader.Read(new([Claimed()])));
        Assert.False(reader.Read(new([])));

        // A payload carrying the attack action asks once; anything else on the contract asks for nothing.
        Assert.True(reader.Read(new([Payload("""{ "action": "party.attack" }""")])));
        Assert.False(reader.Read(new([Payload("""{ "action": "party.use" }""")])));
        Assert.False(reader.Read(new([Payload("not json")])));
    }

    /// <summary>One held-edge event on the act control, as the engine emits a held mapping.</summary>
    private static ProductInputEvent Held() => Admitted.Digital("test.attack", InputEdge.Held, InputPhase.Held);

    /// <summary>One direct interface claim on the act control, as the companion's own button sends it.</summary>
    private static ProductInputEvent Claimed() => Admitted.Claimed("test.attack");

    /// <summary>One payload action on this suite's contract, as the companion sends it.</summary>
    private static ProductInputEvent Payload(string json) => Admitted.Payload("test.actions", json);

    /// <summary>A party of four, which is what a fight's own side has to be able to pace.</summary>
    private static PartyEntity Party()
    {
        List<MemberCreation> members = [];
        for (int index = 0; index < 4; index++)
        {
            members.Add(new MemberCreation(new PartyMemberSeed(
                $"Member {index + 1}",
                TestRace,
                Fighter,
                [new AttributeScore(new AttributeId("vigour"), 12)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(10))));
        }

        return new PartyEntityFactory().Create(new PartyCreation(members, 0, 0, ProvisionUnit.Portions, 0, 0));
    }

    /// <summary>The fight this suite exercises: the kit's state over the test's own rule.</summary>
    private static CombatState Fight(SessionWorld world, PartyEntity party, IRandomService? random = null) =>
        new(Capabilities.Combat(new TestCombatRule(random)), party, world, TestClock.Create());

    private static PlacePose Pose() => new(0, 0, 0, 0, 0);

    /// <summary>Puts the party in the hall and populates it, as one admitted update of the session does.</summary>
    private static void Arrive(SessionWorld world)
    {
        world.ArriveAt(Hall, Pose());
        world.Populate();
    }

    /// <summary>One advance of the clock, as the session hands it to the mechanisms that keep time.</summary>
    private static ClockAdvance Advance(long milliseconds) => new(
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameDate(1168, 1, 1, 9, 0, 1),
        GameDuration.FromMilliseconds(milliseconds),
        PeriodCrossings.None,
        []);

    /// <summary>One digital event on the act control, in the shape the engine admits it.</summary>
    private static ProductInputEvent Attack() => Admitted.Digital("test.attack");

    /// <summary>The moment the player lets the act control go, which is what ends a held attack.</summary>
    private static ProductInputEvent Release() => Admitted.Released("test.attack");

    /// <summary>
    /// A world of two places: one hall holding a creature inside its notice range, a creature far outside it,
    /// a person, and a door, and one cave holding nothing.
    /// </summary>
    private static SessionWorld World(PartyEntity party, double monsterAt, double? farMonsterAt = null)
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "world",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [
                        { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                        { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" }
                      ]
                    }
                    """)
                .Add(
                    "packs/world/places.json",
                    $$"""
                    {
                      "documentId": "places",
                      "definitionKind": "place",
                      "entries": [
                        { "id": "1", "kind": "region", "name": "Hall", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                          "placements": [
                            { "id": "beast", "kind": "creature", "x": {{monsterAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7" },
                            { "id": "person-0", "kind": "person", "x": 200, "y": 0, "z": 0, "people": [ "person-1" ] },
                            { "id": "door-0", "kind": "door", "x": 50, "y": 0, "z": 0, "state": 2 }
                            {{(farMonsterAt is { } far ? $$""", { "id": "beast-far", "kind": "creature", "x": {{far.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7" }""" : string.Empty)}} ] },
                        { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
                      ]
                    }
                    """)
                .Add(
                    "packs/world/links.json",
                    """
                    { "documentId": "links", "definitionKind": "travel-link",
                      "entries": [ { "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ] }
                    """),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PartyPoseOwner pose = new(new PartyPose(Hall, Pose()), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        return new SessionWorld(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            TestClock.Create(),
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: TestClock.Create(),
            resources: null,
            partyEntity: party,
            interaction: null,
            schedule: null);
    }

    /// <summary>
    /// The rules this suite's fight is paced by, stated here rather than in the product: a creature is a
    /// placement of the creature kind, its name is the row it names, and everything the kit asks about an
    /// actor is answered from that.
    /// </summary>
    internal sealed class TestCombatRule(IRandomService? random) : ICombatRule
    {
        private const double NoticeRange = 500;

        public string NameOf(CombatSubject subject)
        {
            if (subject.Member is { } member) return member.Profile.Name;
            if (IsCreature(subject)) return subject.Placement!.Content.Id == "beast" ? "A beast" : "A beast far off";
            return subject.Placement?.Content.Kind == "person" ? "A person" : "A door";
        }

        public Hostility NatureOf(CombatSubject subject) => subject.Member is not null
            ? Hostility.Peaceful
            : IsCreature(subject)
                ? Hostility.Aggressive(NoticeRange)
                : subject.Placement?.Content.Kind == "person"
                    ? Hostility.Peaceful
                    : Hostility.Inert;

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) => AttackRecovery;

        /// <summary>
        /// A creature's first recovery is drawn, keyed by the actor, exactly as a ruleset that must not let a
        /// group act in lockstep would draw it: the kit's state never touches randomness itself.
        /// </summary>
        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind)
        {
            if (subject.Member is not null || random is null) return GameDuration.None;
            long milliseconds = random
                .DrawKeyed(new KeyedRngRequest(1, "test.combat.first", $"{subject.Place}/{subject.Id}", 0, FirstRecovery.Milliseconds))
                .Value;
            return GameDuration.FromMilliseconds(milliseconds);
        }

        public double ReachOf(CombatSubject subject, AttackKind kind) => kind == AttackKind.Melee ? 300 : NoticeRange;

        private static bool IsCreature(CombatSubject subject) =>
            subject.Placement?.Content.Kind == "creature";
    }

    /// <summary>
    /// The engine's keyed randomness, answered deterministically: the same key always draws the same value,
    /// so two identical states can be compared without a seed being carried anywhere.
    /// </summary>
    private sealed class SeededRandom : IRandomService
    {
        /// <summary>How many draws were taken, so a test can tell a roll from a guess.</summary>
        public int Draws { get; private set; }

        /// <inheritdoc />
        public KeyedRngReceipt DrawKeyed(KeyedRngRequest request)
        {
            Draws++;
            ulong hash = 14695981039346656037UL;
            foreach (byte value in System.Text.Encoding.UTF8.GetBytes(request.Key))
            {
                hash = (hash ^ value) * 1099511628211UL;
            }

            long span = request.Maximum - request.Minimum + 1;
            return new KeyedRngReceipt(request.Minimum + (long)(hash % (ulong)span));
        }

        /// <inheritdoc />
        public Lcg15Receipt DrawLcg15(Lcg15Request request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public Rng CreateScoped(ScopedRngCreateRequest request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public Rng ForkScoped(ScopedRngForkRequest request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public RngValue NextU64(Rng stream) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public RngValue NextBool(Rng stream) => throw new NotSupportedException("Keyed rolls only.");
    }
}
