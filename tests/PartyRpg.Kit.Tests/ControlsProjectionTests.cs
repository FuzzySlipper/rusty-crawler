using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The facts the projection works out so the panel prints them: when each stand-alone control would be taken, the
/// key it is bound to, the level a training step reaches, the items a counter would work on, the side a spell names,
/// whether a casting could be aimed, and the automap's marker size and corners.
/// </summary>
/// <remarks>
/// These were once rules in the DOM companion. They are the product's answers now, published beside the state they
/// are read from, and these cases are the ones the companion used to decide for itself: a recovering party, a paced
/// round, the party's movement phase, and a paced fight with nothing to pace.
/// </remarks>
public sealed class ControlsProjectionTests
{
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test Ruleset");

    private static SessionSnapshot Session(SessionMode mode) => SessionSnapshots.Bare(Composition, mode, WorldSnapshot.Empty);

    private static CombatSnapshot Fight(int ready, CombatPacing pacing = CombatPacing.RealTime, CombatTurnSnapshot? turn = null) => new(
        Available: true,
        Engaged: true,
        Opposition: 1,
        Ready: ready,
        Members: [new CombatActorSnapshot("member:1", "Roderick", ready > 0, ready > 0 ? 0 : 1, 0, Member: "1", Selected: true)],
        Enemies: [],
        Actor: string.Empty,
        Kind: string.Empty,
        Target: string.Empty,
        Outcome: "none",
        Code: string.Empty,
        Message: string.Empty,
        RecoverySeconds: 0,
        Pacing: pacing,
        Turn: turn);

    private static CombatTurnSnapshot Round(TurnPhase phase, bool playerTurn) =>
        new(phase, 2, "member:1", "Roderick", playerTurn, 0, 20, 4, 6.5, string.Empty, []);

    [Fact]
    public void The_act_control_is_offered_exactly_when_the_fight_would_take_the_order()
    {
        // Real time with everybody recovering: the fight refuses an order now, and there is no round for the two
        // turn actions to pass a turn in.
        ControlsSnapshot recovering = ControlsSnapshot.Read(Session(SessionMode.Running) with { Combat = Fight(ready: 0) });
        Assert.True(recovering.Attack.Enabled); // The selected member gets the named recovery refusal.
        Assert.False(recovering.TurnSkip.Enabled);
        Assert.False(recovering.TurnWait.Enabled);
        Assert.True(recovering.TurnBased.Enabled);

        // Real time with somebody able to act: the same control is offered, because the fight would take it.
        Assert.True(ControlsSnapshot.Read(Session(SessionMode.Running) with { Combat = Fight(ready: 1) }).Attack.Enabled);

        // A paced round with the party's turn out: one press spends the turn, so the control follows the round rather
        // than readiness — even while the acting member still owes recovery, because the refusal is then the answer —
        // and the two turn actions are offered with it.
        ControlsSnapshot turn = ControlsSnapshot.Read(Session(SessionMode.TurnBased) with
        {
            Combat = Fight(ready: 0, CombatPacing.TurnBased, Round(TurnPhase.Action, playerTurn: true)),
        });
        Assert.True(turn.Attack.Enabled);
        Assert.True(turn.TurnSkip.Enabled);
        Assert.True(turn.TurnWait.Enabled);

        // A creature's turn in a paced round is not the party's to act in.
        Assert.False(ControlsSnapshot.Read(Session(SessionMode.TurnBased) with
        {
            Combat = Fight(ready: 1, CombatPacing.TurnBased, Round(TurnPhase.Action, playerTurn: false)),
        }).Attack.Enabled);

        // The party's movement phase: the act control ends it, so it stays offered with nobody ready.
        ControlsSnapshot moving = ControlsSnapshot.Read(Session(SessionMode.TurnBased) with
        {
            Combat = Fight(ready: 0, CombatPacing.TurnBased, Round(TurnPhase.Movement, playerTurn: false)),
        });
        Assert.True(moving.Attack.Enabled);
        Assert.True(moving.TurnSkip.Enabled);

        // Turn-based pacing with nothing to pace: no round is under way, so every control follows readiness again,
        // and the pacing toggle is the one control always offered while a fight mechanism is there.
        ControlsSnapshot idle = ControlsSnapshot.Read(Session(SessionMode.Running) with
        {
            Combat = Fight(ready: 0, CombatPacing.TurnBased, Round(TurnPhase.None, playerTurn: false)),
        });
        Assert.True(idle.Attack.Enabled);
        Assert.False(idle.TurnSkip.Enabled);
        Assert.False(idle.TurnWait.Enabled);
        Assert.True(idle.TurnBased.Enabled);

        // A session with no fight offers none of the four.
        ControlsSnapshot none = ControlsSnapshot.Read(Session(SessionMode.Running));
        Assert.False(none.Attack.Enabled);
        Assert.False(none.TurnBased.Enabled);
    }

    [Theory]
    [InlineData(SessionMode.Running, "session.pause", true)]
    [InlineData(SessionMode.TurnBased, "session.pause", true)]
    [InlineData(SessionMode.Paused, "session.resume", true)]
    [InlineData(SessionMode.Creating, "", false)]
    [InlineData(SessionMode.Starting, "", false)]
    [InlineData(SessionMode.Stopped, "", false)]
    public void The_pause_control_asks_for_whichever_a_press_would_do(SessionMode mode, string action, bool enabled)
    {
        ControlSnapshot pause = ControlsSnapshot.Read(Session(mode)).Pause;
        Assert.Equal(action, pause.Action);
        Assert.Equal(enabled, pause.Enabled);
    }

    [Fact]
    public void Save_and_use_are_offered_only_where_there_is_something_to_save_and_something_faced()
    {
        SaveSnapshot store = new(true, false, "session", SaveState.Never, string.Empty, string.Empty, string.Empty);
        Assert.True(ControlsSnapshot.Read(Session(SessionMode.Running) with { Save = store }).Save.Enabled);
        Assert.True(ControlsSnapshot.Read(Session(SessionMode.Paused) with { Save = store }).Save.Enabled);
        // A paced fight is carried too; a party still being made has no playing state, and a session with no
        // store has nowhere to write one.
        Assert.False(ControlsSnapshot.Read(Session(SessionMode.Creating) with { Save = store }).Save.Enabled);
        Assert.True(ControlsSnapshot.Read(Session(SessionMode.TurnBased) with { Save = store }).Save.Enabled);
        Assert.False(ControlsSnapshot.Read(Session(SessionMode.Running) with { Save = store with { Available = false } }).Save.Enabled);

        InteractionSnapshot door = new(true, "door", "A door", "open", "closed", 128, "ready", [], "none", string.Empty, string.Empty, string.Empty);
        Assert.True(ControlsSnapshot.Read(Session(SessionMode.Running) with { Interaction = door }).Use.Enabled);
        // A use is an instant, so a held session may still use what it faces.
        Assert.True(ControlsSnapshot.Read(Session(SessionMode.Paused) with { Interaction = door }).Use.Enabled);
        Assert.False(ControlsSnapshot.Read(Session(SessionMode.Running) with { Interaction = door with { Label = string.Empty } }).Use.Enabled);
        Assert.False(ControlsSnapshot.Read(Session(SessionMode.Creating) with { Interaction = door }).Use.Enabled);
        Assert.False(ControlsSnapshot.Read(Session(SessionMode.Running) with { Interaction = door with { Available = false } }).Use.Enabled);
    }

    [Fact]
    public void Each_control_carries_the_action_it_sends_and_the_key_the_host_bound()
    {
        ControlKeys keys = new() { Save = "F", TurnBased = "Enter", ConversationLeave = "Escape" };
        ControlsSnapshot controls = ControlsSnapshot.Read(Session(SessionMode.Running) with { Keys = keys });
        Assert.Equal(new ControlSnapshot(SaveActions.Save, false, "F"), controls.Save);
        Assert.Equal(TurnActions.Toggle, controls.TurnBased.Action);
        Assert.Equal("Enter", controls.TurnBased.Key);
        Assert.Equal(ConversationActions.Leave, controls.ConversationLeave.Action);
        Assert.Equal("Escape", controls.ConversationLeave.Key);
        // A control the host bound to no key has none, and a screen names its button alone.
        Assert.Equal(string.Empty, controls.Rest.Key);

        // The projection publishes the same answers.
        ProjectedNode published = Project(Session(SessionMode.Running) with { Keys = keys }).Field(SessionProjection.ControlsField);
        Assert.Equal("session.save", published.Field("save").Field("action").AsString());
        Assert.Equal("F", published.Field("save").Field("key").AsString());
        Assert.False(published.Field("save").Field("enabled").AsBoolean());
        Assert.Equal("session.pause", published.Field("pause").Field("action").AsString());
    }

    [Theory]
    [InlineData(KeyboardControl.KeyF, "F")]
    [InlineData(KeyboardControl.Digit1, "1")]
    [InlineData(KeyboardControl.Space, "Space")]
    [InlineData(KeyboardControl.Enter, "Enter")]
    [InlineData(KeyboardControl.Escape, "Escape")]
    [InlineData(KeyboardControl.ArrowUp, "Arrow Up")]
    [InlineData(KeyboardControl.None, "")]
    public void A_key_reads_as_a_person_names_it(KeyboardControl key, string label) =>
        Assert.Equal(label, ControlKeys.Label(key));

    [Fact]
    public void The_automap_marker_size_and_corners_are_published_and_never_zero()
    {
        MapDrawingSnapshot drawing = new(1, 4, 16, 1000, [], [], 31.25, 31.25, 90);
        Assert.Equal(31.25, drawing.MarkRadius);
        Assert.Equal([31.25, 0, 62.5, 62.5, 0, 62.5], drawing.PartyPoints);

        // A window of no cells is still drawn with a mark a screen can place, rather than a division by nothing.
        Assert.True((drawing with { Cells = 0 }).MarkRadius > 0);

        ProjectedNode map = Project(Session(SessionMode.Running) with
        {
            Map = new MapSnapshot(true, true, "Automap", "1", "Isle", "region", string.Empty, 0, 0, Drawing: drawing),
        }).Field(SessionProjection.MapField).Field("drawing");
        Assert.Equal(31.25, map.Field("markRadius").AsNumber());
        Assert.Equal(6, map.Field("partyPoints").Length());
    }

    [Fact]
    public void A_counter_publishes_what_it_would_sell_identify_mend_and_carry_the_party_to()
    {
        ServiceSnapshot shop = ServiceSnapshot.None with
        {
            Available = true,
            Open = true,
            Operations = ["buy", "identify", "repair", "fare"],
            Stock = [new ServiceStockSnapshot("a", "sword", "A sword", 1, 10, false), new ServiceStockSnapshot("b", "potion", "A potion", 0, 5, false)],
            Offers = [new ServiceOfferSnapshot("fare", "4", "A passage", 2, 25), new ServiceOfferSnapshot("notice", string.Empty, "A notice", 1, 0)],
            Sales = [new ServiceSaleSnapshot("3", "shield", "A shield", 12, 3, false), new ServiceSaleSnapshot("4", "dagger", "A dagger", 8, 0, true)],
        };
        ProjectedNode service = Project(Session(SessionMode.Running) with { Service = shop }).Field(SessionProjection.ServiceField);

        Assert.True(service.Field("canBuy").AsBoolean());
        Assert.False(service.Field("canSell").AsBoolean());
        Assert.False(service.Field("canTeach").AsBoolean());
        Assert.True(service.Field("stock").Item(0).Field("canBuy").AsBoolean());
        Assert.False(service.Field("stock").Item(1).Field("canBuy").AsBoolean());
        // The unidentified item is the one to identify, and the damaged one the one to mend.
        Assert.Equal(1, service.Field("identify").Length());
        Assert.Equal("3", service.Field("identify").Item(0).Field("item").AsString());
        Assert.Equal(1, service.Field("repair").Length());
        Assert.Equal("3", service.Field("repair").Item(0).Field("item").AsString());
        // A passage is a row a player can press; a notice is not.
        Assert.Equal(1, service.Field("fares").Length());
        Assert.Equal("4", service.Field("fares").Item(0).Field("subject").AsString());

        // A counter that is not open offers none of it.
        ProjectedNode shut = Project(Session(SessionMode.Running) with { Service = shop with { Open = false } }).Field(SessionProjection.ServiceField);
        Assert.False(shut.Field("canBuy").AsBoolean());
        Assert.Equal(0, shut.Field("identify").Length());
        Assert.Equal(0, shut.Field("fares").Length());
    }

    [Fact]
    public void A_spell_publishes_the_side_it_names_and_whether_a_casting_could_be_aimed()
    {
        MagicSnapshot magic = MagicSnapshot.None with
        {
            Available = true,
            Members =
            [
                new SpellMemberSnapshot(0, "1", "Aelina", "Sorcerer", 10, 10, string.Empty, string.Empty,
                [
                    new SpellRowSnapshot("2", "Fire Bolt", "Fire", "basic", 1, 2, "foe", "damage", [], "opposition"),
                    new SpellRowSnapshot("68", "Heal", "Body", "basic", 1, 2, "ally", "healing", [], "party"),
                    new SpellRowSnapshot("1", "Torch Light", "Fire", "basic", 1, 1, "party", "light", []),
                ]),
            ],
            Targets = [new SpellTargetSnapshot("member:1", "Aelina", "party")],
            Items = [new SpellItemSnapshot("7", "A wand", "charged", "2", "Fire Bolt", "foe", 5, 8, true, "Aelina", "opposition")],
        };
        ProjectedNode published = Project(Session(SessionMode.Running) with { Magic = magic }).Field(SessionProjection.MagicField);
        ProjectedNode spells = published.Field("members").Item(0).Field("spells");

        // Nobody on the opposition's side is listed, so a foe spell could not be aimed; an ally spell and a spell
        // that names nobody could.
        Assert.Equal("opposition", spells.Item(0).Field("targetSide").AsString());
        Assert.False(spells.Item(0).Field("canCast").AsBoolean());
        Assert.True(spells.Item(1).Field("canCast").AsBoolean());
        Assert.Equal(string.Empty, spells.Item(2).Field("targetSide").AsString());
        Assert.True(spells.Item(2).Field("canCast").AsBoolean());
        Assert.False(published.Field("items").Item(0).Field("canUse").AsBoolean());
        Assert.Equal("party", SessionProjection.WireName(SpellTargetings.Side(SpellTargeting.Ally)!.Value));
        Assert.Null(SpellTargetings.Side(SpellTargeting.Party));
    }

    private static ProjectedNode Project(SessionSnapshot snapshot)
    {
        UiValue value = SessionProjection.Build(snapshot);
        return new ProjectedNode(value, value.Root);
    }
}
