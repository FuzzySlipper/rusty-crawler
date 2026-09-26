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
/// The second pacing of one fight: initiative derived from the recovery that paces real time, a round of an
/// action phase and the party's movement phase, the turn actions with their stated consequences, and a
/// session that waits for the player's committed turn instead of stepping the world.
/// </summary>
/// <remarks>
/// <para>
/// Every number here is the suite's own, which is the point of the seam: the kit holds no recovery value, so
/// a test states the recoveries of its actors and demands that the order, the round's length, and how often
/// an actor acts all follow from them.
/// </para>
/// <para>
/// The suite's world states its creatures' recovery and hit points as placement fields — content in this
/// suite's own vocabulary — so the arithmetic under test is visible in the fixture rather than hidden in a
/// rule: a creature placed with a one-second recovery acts four times in the four-second round its slow
/// neighbour's own recovery makes.
/// </para>
/// </remarks>
public sealed class TurnBasedTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Hall = new("1");
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");

    /// <summary>What one action of a party member of this suite costs, in game time.</summary>
    private static readonly GameDuration MemberRecovery = GameDuration.FromSeconds(2);

    /// <summary>What a swing leaves, so the six-hit-point creature goes down to one blow.</summary>
    private const int BlowDamage = 6;

    [Fact]
    public void Switching_the_pacing_mid_fight_changes_the_pacing_and_nothing_else()
    {
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120, slowAt: 200, quickHitPoints: 6);
        Bodies bodies = new();
        CombatState combat = Fight(world, party, bodies);
        Arrive(world);
        combat.Step();
        Assert.True(combat.IsEngaged);

        // The party acts: one creature is brought down and the other is wounded, which is what gives the
        // portrait below a body, a pool that has moved, and a provocation.
        Assert.All(combat.Engage(), result => Assert.True(result.IsApplied));

        // A member is left with a condition, applied through the party's own state exactly as a landed hit
        // would leave it, because a condition surviving the switch is one of the facts under test.
        party.Members[1].Conditions.Apply(new ActiveCondition(new ConditionId("Weak"), 1));
        combat.Step();
        Assert.Single(bodies.Lying(Hall));

        string[] asItStood = Portrait(combat, bodies);
        Assert.Contains(asItStood, line => line.Contains("quick", StringComparison.Ordinal) && line.Contains("|True|", StringComparison.Ordinal));
        Assert.Contains(asItStood, line => line.Contains("Weak", StringComparison.Ordinal));
        Assert.Contains(asItStood, line => line.Contains("|Opposition|", StringComparison.Ordinal));

        // Switch, switch back, and switch again: at every step the fight is the same fight, down to the
        // recovery each actor owes and the serial of the body on the floor.
        Assert.Equal(CombatPacing.TurnBased, combat.TogglePacing());
        Assert.Equal(asItStood, Portrait(combat, bodies));
        Assert.Equal(CombatPacing.RealTime, combat.TogglePacing());
        Assert.Equal(asItStood, Portrait(combat, bodies));
        Assert.Equal(CombatPacing.TurnBased, combat.TogglePacing());
        Assert.Equal(CombatPacing.RealTime, combat.TogglePacing());
        Assert.Equal(asItStood, Portrait(combat, bodies));

        // The world is untouched too: the same place, the same pose, and the same live population.
        Assert.Equal(Hall, world.Place);
        Assert.Equal(Pose(), world.Party.PlacePose);
        Assert.Equal(2, world.Population.Entities.Count());
    }

    [Fact]
    public void The_pacing_can_be_switched_with_nothing_to_fight_and_the_round_begins_when_a_fight_does()
    {
        using PartyEntity party = Party(members: 1);
        using SessionWorld world = World(party, quickAt: 4000, slowAt: 5000);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);

        // Nothing has been read yet, so there is no fight: switching the pacing holds nothing, and the round
        // begins by itself when something becomes hostile.
        Assert.Equal(CombatPacing.RealTime, combat.Pacing);
        Assert.False(combat.Turns.IsHolding);
        Assert.Equal(CombatPacing.TurnBased, combat.TogglePacing());
        Assert.Equal(TurnPhase.None, combat.Turns.Phase);
        Assert.Equal(0, combat.Turns.Round);
        Assert.False(combat.Turns.WaitsForPlayer);

        combat.Step();
        Assert.True(combat.IsEngaged);
        Assert.Equal(TurnPhase.Action, combat.Turns.Phase);
        Assert.Equal(1, combat.Turns.Round);

        // The round's first turn is the party's, and it is handed out when the pacing is asked for it — which
        // is what the session does in the very update that begins the round.
        Assert.True(combat.Turns.Next().IsNone);
        Assert.True(combat.Turns.WaitsForPlayer);
        Assert.Equal("Member 1", combat.Turns.Current!.Name);

        // And it lets go again when the fight does: the party walks out, the population goes with the visit,
        // and a pacing that held a round nobody can fight would wait forever.
        Assert.True(world.Travel(Assert.Single(world.Graph.TransitionsFrom(Hall)), TransitionKind.Entrance).Arrived);
        world.Populate();
        combat.Step();
        Assert.False(combat.Turns.IsHolding);
        Assert.Equal(CombatPacing.TurnBased, combat.Pacing);
    }

    [Fact]
    public void The_order_is_ascending_remaining_recovery_with_the_fights_own_order_breaking_ties()
    {
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120, slowAt: 200);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);
        combat.Step();
        combat.TogglePacing();

        // Every party member begins ready and every creature is seen with the recovery its placement states,
        // so the order is the members in roster order and then the creatures by how much they owe.
        Assert.Equal(["Member 1", "Member 2", "quick", "slow"], combat.Turns.Order.Select(entry => entry.Name));

        // Re-read: the same state states the same order, and nothing about it was drawn or remembered.
        Assert.Equal(
            combat.Turns.Order.Select(entry => entry.Id),
            combat.Turns.Order.Select(entry => entry.Id));

        // Time moves one actor at a time and the order follows the quantity each actor holds: the member that
        // has spent its action is behind the creature that has not acted yet.
        Assert.True(combat.Engage(combat.Combatants[0].Id).IsApplied);
        Assert.Equal(["Member 2", "quick", "Member 1", "slow"], combat.Turns.Order.Select(entry => entry.Name));
        Assert.Equal(MemberRecovery, combat.Turns.Order[2].Remaining);
        Assert.False(combat.Turns.Order[2].Ready);

        // Ready is exactly zero: an actor whose debt is paid is at the front of the order and says so.
        Assert.True(combat.Turns.Order[0].Ready);
        Assert.True(combat.Turns.Order[0].Remaining.IsNone);
        Assert.True(combat.Turns.Order[0].CanAct);
    }

    [Fact]
    public void A_faster_actor_acts_more_than_once_in_a_round_as_arithmetic_rather_than_as_a_special_case()
    {
        using PartyEntity party = Party(members: 1);
        using SessionWorld world = World(party, quickAt: 120, slowAt: 200);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);
        combat.Step();
        combat.TogglePacing();

        // The slow creature's own recovery is the round's length, so a round lasts long enough for the
        // slowest actor to act once — and everyone whose action costs less acts again inside it.
        Assert.Equal(GameDuration.FromSeconds(4), combat.Turns.Length);
        Dictionary<string, int> acts = TakeTurns(combat);

        // The creature that owes a second acts at one, two, three, and four seconds: exactly the round's
        // length divided by its own recovery. The member whose action costs two seconds acts at nought, two,
        // and four: it is ready when the round opens, and the last instant of the round is one its recovery
        // has reached. The slow creature acts at the round's end, once. Nothing counted turns here — the one
        // recovery quantity says all of it.
        Assert.Equal(4, acts["quick"]);
        Assert.Equal(3, acts["Member 1"]);
        Assert.Equal(1, acts["slow"]);
    }

    [Fact]
    public void A_round_takes_its_action_phase_then_the_partys_movement_phase_and_then_the_next_round()
    {
        using PartyEntity party = Party(members: 1);
        using SessionWorld world = World(party, quickAt: 120);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);
        combat.Step();
        combat.TogglePacing();

        TakeTurns(combat);

        Assert.Equal(TurnPhase.Movement, combat.Turns.Phase);
        Assert.Equal(1, combat.Turns.Round);
        Assert.Equal(combat.Turns.Length, combat.Turns.Elapsed);
        // The movement phase is the party's own step, priced by the party's own recovery.
        Assert.Equal(MemberRecovery, combat.Turns.MovementLeft);
        Assert.False(combat.Turns.WaitsForPlayer);

        // It lasts as long as its allowance, and then the next round begins with the recovery the fight holds
        // rather than with a reset: an actor that acted late in the last round still owes most of its action.
        combat.Turns.SpendMovement(GameDuration.FromSeconds(1));
        Assert.Equal(GameDuration.FromSeconds(1), combat.Turns.MovementLeft);
        Assert.True(combat.Turns.EndMovement());
        Assert.Equal(2, combat.Turns.Round);
        Assert.Equal(TurnPhase.Action, combat.Turns.Phase);
        Assert.True(combat.Turns.Elapsed.IsNone);
        Assert.False(combat.Turns.EndMovement());

        // A round boundary is not a reset: the creature still owes exactly the second of recovery it was
        // part way through when the round ended.
        Assert.Equal(GameDuration.FromSeconds(1), combat.Find(IdOf(combat, "quick"))!.Recovery);
    }

    [Fact]
    public void An_actor_that_can_do_nothing_is_passed_over_and_the_round_still_completes()
    {
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);
        combat.Step();
        combat.TogglePacing();

        // One member is laid out: it owes nothing and takes no turn, and the round runs to its end without it
        // rather than waiting for an action it could never give.
        PartyMember downed = party.Members[1];
        downed.TakeDamage(downed.Resources.HitPoints.Current);
        combat.Step();
        Assert.True(combat.IsDown(combat.Find(CombatantId.Of(downed.Id))!));

        Dictionary<string, int> acts = TakeTurns(combat);
        Assert.Equal(2, acts["Member 1"]);
        Assert.Equal(2, acts["quick"]);
        Assert.DoesNotContain(downed.Profile.Name, acts.Keys);
        Assert.Equal(TurnPhase.Movement, combat.Turns.Phase);
    }

    [Fact]
    public void Skipping_forfeits_the_rest_of_the_round_and_owes_the_action_that_was_not_taken()
    {
        using PartyEntity party = Party(members: 3);
        using SessionWorld world = World(party, quickAt: 120);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);
        combat.Step();
        combat.TogglePacing();

        // The first member passes: it attacks nothing, owes the recovery its own action would have cost, and
        // is not offered another turn this round.
        combat.Turns.Next();
        Combatant skipping = combat.Turns.Current!;
        combat.Turns.Skipped();
        Assert.Equal(TurnAction.Skip, combat.Turns.Last);
        Assert.Equal(MemberRecovery, combat.Find(skipping.Id)!.Recovery);
        Assert.Null(combat.LastAttack);

        Dictionary<string, int> acts = TakeTurns(combat);
        Assert.DoesNotContain(skipping.Name, acts.Keys);
        Assert.Contains(party.Members[1].Profile.Name, acts.Keys);
        Assert.Contains(party.Members[2].Profile.Name, acts.Keys);
    }

    [Fact]
    public void Waiting_defers_the_turn_to_the_end_of_the_round_and_can_only_be_done_once()
    {
        using PartyEntity party = Party(members: 1);
        using SessionWorld world = World(party, quickAt: 120, slowAt: 200);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);
        combat.Step();
        combat.TogglePacing();

        // The member yields its turn: it owes nothing for the wait, and everybody else who is still due this
        // round acts before it does.
        combat.Turns.Next();
        Combatant waiter = combat.Turns.Current!;
        Assert.True(combat.Turns.Waited());
        Assert.Equal(TurnAction.Wait, combat.Turns.Last);
        Assert.True(combat.Find(waiter.Id)!.IsReady);
        Assert.True(combat.Turns.Order.Single(entry => entry.Id == waiter.Id).Waiting);

        List<string> acted = [];
        bool waitedAgain = false;
        GameDuration whenWaited = GameDuration.None;
        while (combat.Turns.Phase == TurnPhase.Action)
        {
            GameDuration due = combat.Turns.Next();
            if (!due.IsNone) combat.Observe(Advance(due.Milliseconds));
            if (combat.Turns.Current is not { } actor) break;
            if (actor.Id == waiter.Id)
            {
                // Waiting twice in one round would hold the round open for a decision nothing has changed, so
                // the second attempt is refused rather than deferring the same turn again.
                waitedAgain = !combat.Turns.Waited();
                whenWaited = combat.Turns.Elapsed;
            }

            acted.Add(actor.Name);
            combat.Turns.Took();
        }

        Assert.True(waitedAgain);
        Assert.Equal(combat.Turns.Length, whenWaited);
        Assert.Contains("quick", acted);
        Assert.Contains("slow", acted);
        // The deferral is what puts it last: every other actor in the round has already acted, including the
        // two due at the instant the round ends.
        Assert.Equal(waiter.Name, acted[^1]);
        Assert.Equal(3, acted.IndexOf(waiter.Name));
    }

    [Fact]
    public void The_session_steps_nothing_while_it_waits_for_the_players_turn_and_resumes_when_it_passes()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120);
        using PartyRpgSession session = Session(channel, world, party);

        session.Start();
        session.Update(Update(1, 1));
        Assert.Equal(SessionMode.Running, session.Mode);

        // The toggle switches the pacing, and the update after it waits: the fight is at the party's first
        // turn, so the session steps no world and measures no interval of its own.
        session.Update(Update(2, 1, Turn("combat.turn-based")));
        ProjectedNode combat = channel.Latest().Field("combat");
        Assert.Equal("turnbased", combat.Field("pacing").AsString());
        Assert.Equal("turnbased", channel.Latest().Field("session").Field("mode").AsString());
        Assert.Equal("action", combat.Field("turn").Field("phase").AsString());
        Assert.True(combat.Field("turn").Field("playerTurn").AsBoolean());
        Assert.Equal("Member 1", combat.Field("turn").Field("actorName").AsString());

        double elapsed = session.Clock!.Elapsed.TotalSeconds;
        List<GameDuration> owed = [.. session.Combat!.Combatants.Select(actor => actor.Recovery)];
        for (ulong step = 3; step <= 60; step++) session.Update(Update(step, 1, Move()));
        Assert.Equal(SessionMode.TurnBased, session.Mode);

        // Nothing moved: no game time passed, no recovery was released, and the turn is still the same
        // actor's. A held session and a paced one differ in what they wait for, not in whether time passes.
        Assert.Equal(elapsed, session.Clock.Elapsed.TotalSeconds);
        Assert.Equal(owed, [.. session.Combat.Combatants.Select(actor => actor.Recovery)]);
        Assert.Equal("Member 1", channel.Latest().Field("combat").Field("turn").Field("actorName").AsString());

        // The turn is committed by a press of the act control, and the next of the party's turns waits: the
        // panel says whose. No game time passes for it, because the member it hands the turn to is ready.
        session.Update(Update(61, 1, Attack()));
        Assert.Equal(SessionMode.TurnBased, session.Mode);
        Assert.Equal("Member 2", channel.Latest().Field("combat").Field("turn").Field("actorName").AsString());
        Assert.Equal(elapsed, session.Clock.Elapsed.TotalSeconds);
        Assert.Contains("attacks quick", channel.Latest().Field("combat").Field("message").AsString(), StringComparison.Ordinal);

        // Every turn of the party's is followed by the creature's own turn, resolved inside the same admitted
        // update — and that turn costs real game time, which the one clock spends on it.
        session.Update(Update(62, 1, Released()));
        session.Update(Update(63, 1, Attack()));
        Assert.True(session.Clock.Elapsed.TotalSeconds > elapsed);
        Assert.Equal(SessionMode.TurnBased, session.Mode);
        ProjectedNode turn = channel.Latest().Field("combat").Field("turn");
        Assert.Equal("Member 1", turn.Field("actorName").AsString());
        Assert.Equal(1, turn.Field("round").AsNumber());

        // Switching the pacing back releases the session: the world steps again with the interval the update
        // admitted, which is what the panel publishes as the session's own mode.
        session.Update(Update(64, 1, Turn("combat.turn-based")));
        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.Equal("realtime", channel.Latest().Field("combat").Field("pacing").AsString());
    }

    [Fact]
    public void A_paced_turn_is_committed_by_one_press_so_a_held_key_cannot_spend_turn_after_turn()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120);
        using PartyRpgSession session = Session(channel, world, party);

        session.Start();
        session.Update(Update(1, 1));
        session.Update(Update(2, 1, Turn("combat.turn-based")));
        Assert.Equal("Member 1", channel.Latest().Field("combat").Field("turn").Field("actorName").AsString());

        // The act control is held down: the first update commits the first member's turn, and the updates
        // after it commit nothing, because a held key is not a new decision.
        for (ulong step = 3; step <= 12; step++) session.Update(Update(step, 1, Attack()));
        ProjectedNode combat = channel.Latest().Field("combat");
        Assert.Equal("Member 2", combat.Field("turn").Field("actorName").AsString());
        Assert.Equal("applied", combat.Field("outcome").AsString());

        // Letting go and pressing again is a decision, and it spends the turn it is offered. The turn after
        // that belongs to the creature that owes a second of recovery, and then to the party again.
        session.Update(Update(13, 1, Released()));
        session.Update(Update(14, 1, Attack()));
        Assert.Equal("Member 1", channel.Latest().Field("combat").Field("turn").Field("actorName").AsString());
        Assert.Equal(1, channel.Latest().Field("combat").Field("turn").Field("round").AsNumber());
    }

    [Fact]
    public void A_key_held_across_the_switch_orders_nothing_until_it_is_let_go()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120);
        using PartyRpgSession session = Session(channel, world, party);

        session.Start();
        session.Update(Update(1, 1));

        // The act control held in real time: the party acts as its members recover.
        session.Update(Update(2, 1, Held("test.attack")));
        Assert.Equal("applied", channel.Latest().Field("combat").Field("outcome").AsString());

        // The pacing is switched while the key is still down. The hold belonged to real time, so nothing is
        // ordered in the pacing the player has just asked for: the fight waits, however long the key stays
        // down and however many updates report it.
        session.Update(Update(3, 1, Turn("combat.turn-based")));
        Assert.Equal(SessionMode.TurnBased, session.Mode);
        string waiting = channel.Latest().Field("combat").Field("turn").Field("actorName").AsString();
        for (ulong step = 4; step <= 20; step++) session.Update(Update(step, 1, Held("test.attack")));
        Assert.Equal(waiting, channel.Latest().Field("combat").Field("turn").Field("actorName").AsString());
        Assert.True(channel.Latest().Field("combat").Field("turn").Field("playerTurn").AsBoolean());

        // Letting go lifts it, and the next press is the decision that spends the turn.
        session.Update(Update(21, 1, Released()));
        session.Update(Update(22, 1, Attack()));
        Assert.NotEqual(waiting, channel.Latest().Field("combat").Field("turn").Field("actorName").AsString());
        Assert.Equal("applied", channel.Latest().Field("combat").Field("outcome").AsString());
    }

    [Fact]
    public void The_toggle_and_the_turn_controls_are_read_from_their_own_intents_and_the_panel_contract()
    {
        TurnInput reader = new(new TurnIntentNames("test.turn-based", "test.turn-skip", "test.turn-wait", "test.actions"));

        Assert.Equal(TurnControls.None, reader.Read([]));

        // Each control is its own intent, and a held key is a key still down rather than a new decision: a
        // toggle that repeated while its key stayed down would switch the pacing every update.
        Assert.True(reader.Read([Pressed("test.turn-based")]).Toggle);
        Assert.True(reader.Read([Pressed("test.turn-skip")]).Skip);
        Assert.True(reader.Read([Pressed("test.turn-wait")]).Wait);
        Assert.Equal(TurnControls.None, reader.Read([Held("test.turn-based")]));
        Assert.Equal(TurnControls.None, reader.Read([Pressed("test.something-else")]));

        // The companion's own buttons ask for exactly the same acts on the declared contract.
        Assert.True(reader.Read([Payload("""{ "action": "combat.turn-based" }""")]).Toggle);
        Assert.True(reader.Read([Payload("""{ "action": "combat.turn-skip" }""")]).Skip);
        Assert.True(reader.Read([Payload("""{ "action": "combat.turn-wait" }""")]).Wait);
        Assert.Equal(TurnControls.None, reader.Read([Payload("""{ "action": "party.attack" }""")]));
        Assert.Equal(TurnControls.None, reader.Read([Payload("not json")]));

        // A direct interface claim carries no edge at all, which is how a panel's own control arrives.
        Assert.True(reader.Read([Claimed("test.turn-based")]).Toggle);
    }

    [Fact]
    public void The_projection_publishes_the_round_whose_turn_it_is_and_what_is_left_to_act()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120, slowAt: 200);
        using PartyRpgSession session = Session(channel, world, party);

        session.Start();
        session.Update(Update(1, 1));

        // Real time: there is a fight and no round, which is a different fact from a round nobody's turn is
        // in — and the difference is what a player needs to read before pressing the toggle.
        ProjectedNode combat = channel.Latest().Field("combat");
        Assert.Equal("realtime", combat.Field("pacing").AsString());
        Assert.Equal("none", combat.Field("turn").Field("phase").AsString());
        Assert.Equal(0, combat.Field("turn").Field("round").AsNumber());
        Assert.Equal(string.Empty, combat.Field("turn").Field("actorName").AsString());
        Assert.False(combat.Field("turn").Field("playerTurn").AsBoolean());

        session.Update(Update(2, 1, Turn("combat.turn-based")));
        combat = channel.Latest().Field("combat");
        ProjectedNode turn = combat.Field("turn");

        // The round is published with whose turn it is, what that actor still owes, and the order the fight
        // will act in — every number a length of game time, and none of it counted down by a screen.
        Assert.Equal("turnbased", combat.Field("pacing").AsString());
        Assert.Equal("action", turn.Field("phase").AsString());
        Assert.Equal(1, turn.Field("round").AsNumber());
        Assert.Equal("Member 1", turn.Field("actorName").AsString());
        Assert.True(turn.Field("playerTurn").AsBoolean());
        Assert.Equal(4.0, turn.Field("roundSeconds").AsNumber(), 3);
        Assert.Equal(0.0, turn.Field("dueSeconds").AsNumber(), 3);
        Assert.Equal(4, turn.Field("order").Length());
        Assert.Equal("Member 1", turn.Field("order").Item(0).Field("name").AsString());
        Assert.Equal("party", turn.Field("order").Item(0).Field("side").AsString());
        Assert.True(turn.Field("order").Item(0).Field("current").AsBoolean());
        Assert.Equal("quick", turn.Field("order").Item(2).Field("name").AsString());
        Assert.Equal("opposition", turn.Field("order").Item(2).Field("side").AsString());
        // The creature's own second of recovery was already half spent by the world stepping before the
        // switch: the switch changes the pacing, and the recovery the fight holds is what the round reads.
        Assert.Equal(0.5, turn.Field("order").Item(2).Field("remainingSeconds").AsNumber(), 3);

        // A committed turn says what it was, so a skip and a wait are as visible as an attack — and the turn
        // it leaves behind is the next actor's.
        session.Update(Update(3, 1, Action("combat.turn-skip")));
        turn = channel.Latest().Field("combat").Field("turn");
        Assert.Equal("skip", turn.Field("last").AsString());
        Assert.Equal("Member 2", turn.Field("actorName").AsString());

        session.Update(Update(4, 1, Action("combat.turn-wait")));
        turn = channel.Latest().Field("combat").Field("turn");
        Assert.Equal("wait", turn.Field("last").AsString());
        Assert.Contains(
            Enumerable.Range(0, (int)turn.Field("order").Length()),
            index => turn.Field("order").Item(index).Field("waiting").AsBoolean()
                && turn.Field("order").Item(index).Field("name").AsString() == "Member 2");
    }

    [Fact]
    public void A_party_that_can_no_longer_act_leaves_the_pacing_holding_nothing()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = Party(members: 1);
        using SessionWorld world = World(party, quickAt: 120);
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            world,
            clock: Clock(),
            party: party,
            combatInput: new CombatIntentNames(
                "test.attack",
                "test.attack",
                "test.actions",
                new TurnIntentNames("combat.turn-based", "combat.turn-skip", "combat.turn-wait", "test.actions")),
            combat: new Refusing());

        session.Start();
        session.Update(Update(1, 1));
        Assert.Equal(SessionMode.Running, session.Mode);

        // This ruleset leaves every member unable to act. There is therefore no turn of the party's to wait
        // for, so the pacing holds nothing: the fight goes on being paced as it is outside the mode rather
        // than waiting forever for an action nobody can give.
        session.Update(Update(2, 1, Turn("combat.turn-based")));
        ProjectedNode combat = channel.Latest().Field("combat");
        Assert.Equal("turnbased", combat.Field("pacing").AsString());
        Assert.Equal("none", combat.Field("turn").Field("phase").AsString());
        Assert.False(combat.Field("turn").Field("playerTurn").AsBoolean());
        Assert.Equal(SessionMode.Running, session.Mode);

        // And a turn the pacing has nowhere to put is reported rather than swallowed: passing a turn while
        // the fight is being played in real time names exactly that.
        session.Update(Update(3, 1, Action("combat.turn-skip")));
        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.Equal("none", channel.Latest().Field("combat").Field("turn").Field("phase").AsString());
    }

    [Fact]
    public void The_same_fight_produces_the_same_turns_and_the_same_rolls_in_either_pacing()
    {
        // The first blow of the same seeded fight, played twice in each pacing: nothing about the pacing adds
        // a draw, so the attack a member makes is the attack it would have made in real time.
        string realTime = FirstBlow(CombatPacing.RealTime);
        Assert.Equal(realTime, FirstBlow(CombatPacing.RealTime));
        string paced = FirstBlow(CombatPacing.TurnBased);
        Assert.Equal(paced, FirstBlow(CombatPacing.TurnBased));
        Assert.Contains("hit=True", paced, StringComparison.Ordinal);
        Assert.Contains("damage=6", paced, StringComparison.Ordinal);

        // And a paced fight's order is the same order twice, read from the same state.
        using PartyEntity party = Party(members: 2);
        using SessionWorld world = World(party, quickAt: 120, slowAt: 200);
        CombatState combat = Fight(world, party, new Bodies());
        Arrive(world);
        combat.Step();
        combat.TogglePacing();
        Assert.Equal(
            combat.Turns.Order.Select(entry => $"{entry.Name}:{entry.Remaining.Milliseconds}"),
            combat.Turns.Order.Select(entry => $"{entry.Name}:{entry.Remaining.Milliseconds}"));
    }

    /// <summary>One seeded fight's first blow, read after the pacing it was played in was set.</summary>
    private static string FirstBlow(CombatPacing pacing)
    {
        using PartyEntity party = Party(members: 1);
        using SessionWorld world = World(party, quickAt: 120);
        CombatState combat = Fight(world, party, new Bodies(), new SeededRandom());
        Arrive(world);
        combat.Step();
        if (pacing == CombatPacing.TurnBased) combat.TogglePacing();
        combat.Engage(combat.Combatants[0].Id);
        CombatResolution? resolution = combat.LastResolution;
        return resolution is null
            ? "no resolution"
            : $"hit={resolution.Hit} rolled={resolution.Rolled} damage={resolution.Damage} left={resolution.TargetHitPoints}/{resolution.TargetHitPointsMax}";
    }

    // ---- fixtures -----------------------------------------------------------------------------------------

    /// <summary>
    /// Takes every turn of the round the fight is in, and answers with how many each actor took.
    /// </summary>
    /// <remarks>
    /// The loop is the pacing's own protocol, driven from outside as the session drives it: ask whose turn is
    /// next and how much game time passes first, hand that time to the fight — which is where recovery is
    /// released — make the actor act, and mark the turn taken. The action each actor makes is an attack at
    /// nothing, through the fight's one gated entry: what a turn costs is the recovery that entry spends, and
    /// this suite is counting turns rather than wounds.
    /// </remarks>
    private static Dictionary<string, int> TakeTurns(CombatState combat)
    {
        Dictionary<string, int> acts = [];
        while (combat.Turns.Phase == TurnPhase.Action)
        {
            GameDuration due = combat.Turns.Next();
            if (!due.IsNone) combat.Observe(Advance(due.Milliseconds));
            if (combat.Turns.Current is not { } actor) break;
            acts[actor.Name] = acts.GetValueOrDefault(actor.Name) + 1;
            combat.Order(new AttackOrder(actor.Id, actor.PreferredKind, Target: null));
            combat.Turns.Took();
        }

        return acts;
    }

    /// <summary>
    /// The whole fight as one value: who is in it, on which side, how far off, what they owe, what they have
    /// left, what is acting on them, and which body lies on the floor.
    /// </summary>
    private static string[] Portrait(CombatState combat, Bodies bodies)
    {
        List<string> lines = [];
        foreach (Combatant combatant in combat.Combatants)
        {
            (int current, int maximum) = combat.Vitals(combatant);
            string conditions = combatant.Subject.Member is { } member && member.Conditions.Count > 0
                ? string.Join(",", member.Conditions.Active)
                : string.Empty;
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{combatant.Name}|{combatant.Side}|{combatant.Distance}|{combatant.Recovery.Milliseconds}|{current}/{maximum}|{conditions}|{combat.IsDown(combatant)}|{combatant.Subject.Place}|{combatant.Subject.Pose}"));
        }

        lines.Add($"party|{combat.PartyPose}");
        lines.Add($"bodies|{string.Join(",", bodies.Lying(Hall).Select(body => $"{body.Name}@{body.Pose}#{body.Serial}"))}");
        return [.. lines];
    }

    /// <summary>What this suite's fights leave behind: the bodies a fight read, one serial per death.</summary>
    private sealed class Bodies : IFallenCreatureObserver
    {
        private readonly Dictionary<PlaceId, Dictionary<string, Corpse>> _places = [];
        private readonly Dictionary<string, long> _serials = [];
        private long _next;

        /// <summary>The bodies lying in one place, which is what a toggle must not disturb.</summary>
        internal IReadOnlyList<Corpse> Lying(PlaceId place) =>
            _places.TryGetValue(place, out Dictionary<string, Corpse>? held) ? [.. held.Values] : [];

        /// <inheritdoc />
        public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen)
        {
            if (!_places.TryGetValue(place, out Dictionary<string, Corpse>? held))
            {
                held = [];
                _places[place] = held;
            }

            List<Corpse> bodies = [];
            foreach (FallenCreature creature in fallen)
            {
                string key = creature.Placement.Content.Id;
                if (!_serials.TryGetValue(key, out long serial))
                {
                    serial = ++_next;
                    _serials[key] = serial;
                }

                Corpse body = new(place, creature.Placement, creature.Name, serial);
                held[key] = body;
                bodies.Add(body);
            }

            return bodies;
        }
    }

    /// <summary>The identity of the creature a placement of this suite's world put there.</summary>
    private static CombatantId IdOf(CombatState combat, string placement) =>
        combat.Combatants.Single(combatant => combatant.Subject.Placement?.Content.Id == placement).Id;

    private static PartyEntity Party(int members)
    {
        List<MemberCreation> creations = [];
        for (int index = 0; index < members; index++)
        {
            creations.Add(new MemberCreation(new PartyMemberSeed(
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

        return new PartyEntityFactory().Create(new PartyCreation(creations, 0, 0, ProvisionUnit.Portions, 0, 0));
    }

    private static CombatState Fight(SessionWorld world, PartyEntity party, Bodies bodies, IRandomService? random = null) =>
        new(new TestRule(bodies, random), party, world, Clock());

    private static PartyRpgSession Session(RecordingUiProjectionChannel channel, SessionWorld world, PartyEntity party) =>
        new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            world,
            clock: Clock(),
            party: party,
            combat: new TestRule(new Bodies()),
            combatInput: new CombatIntentNames(
                "test.attack",
                "test.attack",
                "test.actions",
                new TurnIntentNames("combat.turn-based", "combat.turn-skip", "combat.turn-wait", "test.actions")));

    private static PlacePose Pose() => new(0, 0, 0, 0, 0);

    private static void Arrive(SessionWorld world)
    {
        world.ArriveAt(Hall, Pose());
        world.Populate();
    }

    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    private static ClockAdvance Advance(long milliseconds) => new(
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameDate(1168, 1, 1, 9, 0, 1),
        GameDuration.FromMilliseconds(milliseconds),
        PeriodCrossings.None,
        []);

    private static ProductUpdate Update(ulong step, uint admitted, params ProductInputEvent[] input)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            step,
            60,
            admitted,
            0,
            1.0 / 60.0);
        return new ProductUpdate(facts, input);
    }

    /// <summary>One press of the act control, as the engine admits a held mapping's edge.</summary>
    private static ProductInputEvent Attack() => Pressed("test.attack");

    /// <summary>The act control coming back up, which is what ends a hold.</summary>
    private static ProductInputEvent Released() => new(
        InputEventKind.MappedDigital, InputEdge.Released, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Released, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, "test.attack"u8.ToArray(),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>A key walking the party forward, held, which a paced fight must not act on.</summary>
    private static ProductInputEvent Move() => new(
        InputEventKind.MappedDigital, InputEdge.Held, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Held, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, "test.forward"u8.ToArray(),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static ProductInputEvent Turn(string intent) => Pressed(intent);

    /// <summary>A panel control's action on the declared contract.</summary>
    private static ProductInputEvent Action(string action) => Payload($$"""{ "action": "{{action}}" }""");

    private static ProductInputEvent Pressed(string intent) => new(
        InputEventKind.MappedDigital, InputEdge.Pressed, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, System.Text.Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static ProductInputEvent Held(string intent) => new(
        InputEventKind.MappedDigital, InputEdge.Held, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Held, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, System.Text.Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static ProductInputEvent Claimed(string intent) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, System.Text.Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static ProductInputEvent Payload(string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        System.Text.Encoding.UTF8.GetBytes("test.actions"), System.Text.Encoding.UTF8.GetBytes(json));

    /// <summary>
    /// A world of two places whose hall holds a creature placed where the test says, with the recovery and hit
    /// points the test states, and a second slow one when a test wants a round with a slow end.
    /// </summary>
    private static SessionWorld World(PartyEntity party, double quickAt, double? slowAt = null, int quickHitPoints = 40)
    {
        string slow = slowAt is { } slowX
            ? $$""", { "id": "slow", "kind": "creature", "x": {{slowX.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7", "recovery": 4000, "hitPoints": 40 }"""
            : string.Empty;
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
                            { "id": "quick", "kind": "creature", "x": {{quickAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7", "recovery": 1000, "hitPoints": {{quickHitPoints}} }
                            {{slow}} ] },
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
            Clock(),
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: Clock(),
            resources: null,
            partyEntity: party,
            interaction: null,
            schedule: null);
    }

    /// <summary>Walking is free: nothing in these tests is about what a road costs.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }

    /// <summary>
    /// The rules this suite's fights are paced and resolved by, stated here rather than in the product: a
    /// party member owes the suite's member recovery, a creature owes the recovery its own placement states,
    /// and every blow lands for the suite's own harm.
    /// </summary>
    private sealed class TestRule(Bodies bodies, IRandomService? random = null) : ICombatRule, ICombatResolutionRule, IFallenCreatureObserver
    {
        private const double NoticeRange = 5000;

        public string NameOf(CombatSubject subject) =>
            subject.Member?.Profile.Name ?? subject.Placement?.Content.Id ?? "nothing";

        public Hostility NatureOf(CombatSubject subject) => subject.Member is not null
            ? Hostility.Peaceful
            : subject.Placement?.Content.Kind == "creature"
                ? Hostility.Aggressive(NoticeRange)
                : Hostility.Inert;

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) =>
            subject.Member is not null
                ? MemberRecovery
                : GameDuration.FromMilliseconds(subject.Placement?.Source.GetInt32("recovery") ?? 2000);

        /// <summary>No first recovery is drawn: what a creature owes is what its placement states.</summary>
        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => RecoveryAfter(subject, kind);

        public double ReachOf(CombatSubject subject, AttackKind kind) => NoticeRange;

        public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen) => bodies.Observe(place, fallen);

        /// <summary>
        /// The attack's rolls: this suite's own draws when it states a seed, and the lowest the attack allows
        /// when it does not, so a blow's numbers are the suite's rather than an engine's.
        /// </summary>
        public IAttackRolls? RollsFor(CombatSubject attacker, string key) =>
            random is null ? new Lowest() : new Keyed(random, key);

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) => new(
            HitChance.Always,
            new DamageKindId("Phys"),
            DamageRoll.Flat(BlowDamage),
            Resistance.Of(0));

        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls) => damage;

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) => null;

        /// <summary>A member with nothing left to lose cannot act; a creature answers for its own body.</summary>
        public bool CanAct(CombatSubject subject) => subject.Member is not { } member || member.Resources.HitPoints.Current > 0;

        public int HitPointsOf(CombatSubject subject) => subject.Member is { } member
            ? member.Resources.HitPoints.Maximum
            : subject.Placement?.Source.GetInt32("hitPoints") ?? 0;

        /// <summary>One attack's draws, all of them the lowest the attack allows.</summary>
        private sealed class Lowest : IAttackRolls
        {
            public int Roll(string name, int minimum, int maximum) => minimum;
        }

        /// <summary>One attack's draws, taken from the engine's keyed service under the attack's own key.</summary>
        private sealed class Keyed(IRandomService random, string key) : IAttackRolls
        {
            public int Roll(string name, int minimum, int maximum) =>
                (int)random.DrawKeyed(new KeyedRngRequest(1, "test.combat", $"{key}/{name}", minimum, maximum)).Value;
        }
    }

    /// <summary>
    /// A fight whose resolution leaves every actor unable to act, which is how a committed turn comes to be
    /// refused without the session having to invent a second reason.
    /// </summary>
    private sealed class Refusing : ICombatRule, ICombatResolutionRule
    {
        public string NameOf(CombatSubject subject) => subject.Member?.Profile.Name ?? "nothing";

        public Hostility NatureOf(CombatSubject subject) => subject.Member is not null
            ? Hostility.Peaceful
            : Hostility.Aggressive(5000);

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) => MemberRecovery;

        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => MemberRecovery;

        public double ReachOf(CombatSubject subject, AttackKind kind) => 5000;

        public IAttackRolls? RollsFor(CombatSubject attacker, string key) => null;

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) => new(
            HitChance.Always,
            new DamageKindId("Phys"),
            DamageRoll.Flat(0),
            Resistance.Of(0));

        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls) => damage;

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) => null;

        /// <summary>Members are laid out, so no member may act — which is what a refusal needs to be about.</summary>
        public bool CanAct(CombatSubject subject) => subject.Member is null;

        public int HitPointsOf(CombatSubject subject) => 40;
    }

    /// <summary>
    /// The engine's keyed randomness, answered deterministically: the same key always draws the same value,
    /// so two identical states can be compared without a seed being carried anywhere.
    /// </summary>
    private sealed class SeededRandom : IRandomService
    {
        public KeyedRngReceipt DrawKeyed(KeyedRngRequest request)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte value in System.Text.Encoding.UTF8.GetBytes(request.Key))
            {
                hash = (hash ^ value) * 1099511628211UL;
            }

            long span = request.Maximum - request.Minimum + 1;
            return new KeyedRngReceipt(request.Minimum + (long)(hash % (ulong)span));
        }

        public Lcg15Receipt DrawLcg15(Lcg15Request request) => throw new NotSupportedException("Keyed rolls only.");

        public Rng CreateScoped(ScopedRngCreateRequest request) => throw new NotSupportedException("Keyed rolls only.");

        public Rng ForkScoped(ScopedRngForkRequest request) => throw new NotSupportedException("Keyed rolls only.");

        public RngValue NextU64(Rng stream) => throw new NotSupportedException("Keyed rolls only.");

        public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => throw new NotSupportedException("Keyed rolls only.");

        public RngValue NextBool(Rng stream) => throw new NotSupportedException("Keyed rolls only.");
    }
}
