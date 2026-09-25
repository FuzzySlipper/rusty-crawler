using System.Text;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The session's creation mode: a party made through the session's own actions, the flow's refusals
/// reaching the screen instead of throwing, the world not stepping while a party is being made, and the
/// accepted party being the one the session then plays.
/// </summary>
/// <remarks>
/// Every definition here is the test's own — races, classes, portraits, skills, and a pool small enough to
/// spend by hand — because that is the seam: the kit knows no race or class name, so a session creating a
/// party must serve this suite's vocabulary as readily as the ruleset's. The last case drives the compiled
/// ruleset's own flow through the same session, which is the path the product takes.
/// </remarks>
public sealed class CreationModeTests
{
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test Ruleset");
    private const double StepSeconds = 1.0 / 60.0;

    /// <summary>The creation controls this suite declares, which are the product's to name.</summary>
    private static readonly CreationIntentNames Controls = new(
        "test.creation-advance",
        "test.creation-accept",
        "test.creation.action.v1");

    private static readonly MovementIntentNames MovementNames = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");

    private static readonly AttributeId Vigour = new("vigour");
    private static readonly AttributeId Wit = new("wit");
    private static readonly RaceId Testfolk = new("testfolk");
    private static readonly RaceId Stonefolk = new("stonefolk");
    private static readonly ClassId Fighter = new("fighter");
    private static readonly ClassId Adept = new("adept");
    private static readonly PortraitId FolkA = new("folk-a");
    private static readonly PortraitId FolkB = new("folk-b");
    private static readonly PortraitId StoneA = new("stone-a");
    private static readonly SkillId Blades = new("blades");
    private static readonly SkillId Bulwark = new("bulwark");
    private static readonly SkillId Axes = new("axes");
    private static readonly SkillId Bows = new("bows");
    private static readonly SkillId Lore = new("lore");
    private static readonly SkillId Aim = new("aim");
    private static readonly SkillId Wards = new("wards");

    [Fact]
    public void A_party_is_created_through_the_sessions_actions_from_start_to_accept()
    {
        using Making making = new(new PartyCreationFlow(Options));
        PartyRpgSession session = making.Session;

        // A session that creates holds no party and no world, and says so: the party does not exist yet,
        // and the world it will walk in is composed when it does.
        Assert.Equal(SessionMode.Creating, session.Mode);
        Assert.Null(session.Party);
        Assert.Null(session.LiveWorld);
        Assert.Null(session.CreationRefusal);
        Assert.False(session.Creation!.HasDefault);
        Assert.Equal("creating", making.Projections.Latest.Field("session").Field("mode").Text());
        Assert.True(making.Projections.Latest.Field("creation").Field("active").Flag());

        // The first member: a portrait, a class, a name, the whole pool, and the two chosen skills — every
        // one of them a command the session reads out of an admitted update.
        ulong step = Drive(session, "Ann", FolkA, Fighter, [(Vigour, 4)], [Axes, Bows]);
        Assert.Equal(1, session.Creation.MemberIndex);
        Assert.Equal(CreationStep.Portrait, session.Creation.Step);

        Drive(session, "Bo", StoneA, Adept, [(Wit, 2), (Vigour, 2)], [Lore, Bows], step);
        Assert.True(session.Creation.IsComplete);

        // Accepting builds the party through the suite's factory, and the session leaves creation for the
        // world with that party in it.
        session.Update(Update(900, 1, Command(CreationActions.Accept)));

        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.Null(session.Creation);
        Assert.Null(session.CreationRefusal);
        PartyEntity party = Assert.Single(making.Built);
        Assert.Same(party, session.Party);
        Assert.NotNull(session.LiveWorld);

        // What was chosen is what the party holds: names, races, classes, and the portraits the races came
        // from, plus the skills the classes fixed and the player picked.
        Assert.Equal(2, party.Members.Count);
        Assert.Equal("Ann", party.Members[0].Profile.Name);
        Assert.Equal(Testfolk, party.Members[0].Profile.Race);
        Assert.Equal(Fighter, party.Members[0].Profile.Class);
        Assert.Equal(FolkA, party.Members[0].Profile.Portrait);
        Assert.Equal([Blades, Bulwark, Axes, Bows], party.Members[0].Skills.Entries.Select(entry => entry.Skill));
        Assert.Equal("Bo", party.Members[1].Profile.Name);
        Assert.Equal(Stonefolk, party.Members[1].Profile.Race);
        Assert.Equal(Adept, party.Members[1].Profile.Class);
        Assert.Equal(StoneA, party.Members[1].Profile.Portrait);
        Assert.Equal([Wards, Aim, Lore, Bows], party.Members[1].Skills.Entries.Select(entry => entry.Skill));

        // The pool was spent exactly, which is what the flow judged when the step was confirmed, and the
        // party starts on the accounts creation stated.
        Assert.Equal(12, AttributeOf(party, 0, Vigour));
        Assert.Equal(6, AttributeOf(party, 0, Wit));
        Assert.Equal(8, AttributeOf(party, 1, Vigour));
        Assert.Equal(8, AttributeOf(party, 1, Wit));
        Assert.Equal(25, party.Purse.Coins);
        Assert.Equal(3, party.Food.Portions);
        Assert.Equal(2, party.Reputation.Reputation);
        Assert.Equal(1, party.Reputation.Fame);

        // And the screen is told the party was accepted, with the members it is now playing.
        Node creation = making.Projections.Latest.Field("creation");
        Assert.False(creation.Field("active").Flag());
        Assert.True(creation.Field("accepted").Flag());
        Assert.Equal(2, creation.Field("party").Count());
        Assert.Equal("Ann", creation.Field("party").Element(0).Field("name").Text());
        Assert.Equal("testfolk", creation.Field("party").Element(0).Field("race").Text());
        Assert.Equal("Bo", creation.Field("party").Element(1).Field("name").Text());
        Assert.Equal("stone-a", creation.Field("party").Element(1).Field("portrait").Text());
        Assert.True(making.Projections.Latest.Field("party").Field("present").Flag());
    }

    [Fact]
    public void The_declared_controls_carry_the_two_commands_a_keyboard_can_express()
    {
        using Making making = new(new PartyCreationFlow(Options, Defaults));
        PartyRpgSession session = making.Session;

        // This suite's default party is finished, so the flow is ready to accept from the start, and the
        // projection says a default was offered.
        Assert.True(session.Creation!.IsComplete);
        Assert.True(making.Projections.Latest.Field("creation").Field("hasDefault").Flag());

        // A digital event on the declared accept control accepts the finished party, exactly as the payload
        // action does: the keyboard and the screen are two ways to the same command.
        session.Update(Update(10, 1, Digital(Controls.Accept)));
        Assert.Equal(SessionMode.Running, session.Mode);
        PartyEntity party = Assert.Single(making.Built);
        Assert.Equal(2, party.Members.Count);

        // A digital event on the declared confirm control confirms the step being worked on: after a
        // portrait is chosen the same control moves creation to the class.
        using Making again = new(new PartyCreationFlow(Options));
        again.Session.Update(Update(10, 1, Choose(CreationActions.SelectPortrait, "portrait", FolkA.Value)));
        again.Session.Update(Update(11, 1, Digital(Controls.Advance)));
        Assert.Equal(CreationStep.Class, again.Session.Creation!.Step);

        // An event on a control this reader does not claim drives nothing: a movement key and a foreign
        // payload action are somebody else's events while a party is being made.
        again.Session.Update(Update(12, 1, Digital(MovementNames.Forward, InputEdge.Pressed)));
        again.Session.Update(Update(13, 1, Payload("""{"action":"party.jump"}""")));
        Assert.Equal(CreationStep.Class, again.Session.Creation.Step);
        Assert.Null(again.Session.CreationRefusal);

        // Confirming a step nothing has been chosen for is the flow's refusal, not a crash: the class step
        // is confirmed with no class, and creation stays on it.
        again.Session.Update(Update(14, 1, Digital(Controls.Advance)));
        Assert.Equal("class-unchosen", again.Session.CreationRefusal!.Code);
        Assert.Equal(CreationStep.Class, again.Session.Creation.Step);
        Assert.Empty(again.Built);
    }

    [Fact]
    public void An_illegal_choice_is_refused_and_named_without_leaving_creation()
    {
        using Making making = new(new PartyCreationFlow(Options, Defaults));
        PartyRpgSession session = making.Session;

        // Reopening a confirmed member puts creation back on it; its steps begin again so a change to the
        // portrait or the class re-derives what depends on it.
        session.Update(Update(10, 1, Member(0)));
        Assert.Equal(CreationStep.Portrait, session.Creation!.Step);

        // A class out of step is refused by name, and creation stays exactly where it was.
        session.Update(Update(11, 1, Choose(CreationActions.SelectClass, "class", Fighter.Value)));
        PartyRefusal outOfStep = Refused(session);
        Assert.Equal("creation-step", outOfStep.Code);
        Assert.Contains("steps are taken in order", outOfStep.Message, StringComparison.Ordinal);
        Assert.Equal(CreationStep.Portrait, session.Creation.Step);
        Assert.Equal(SessionMode.Creating, session.Mode);
        Assert.Empty(making.Built);

        // A portrait creation does not offer is refused with the rule it broke, and the projection carries
        // both the code a caller branches on and the message a person reads.
        session.Update(Update(12, 1, Choose(CreationActions.SelectPortrait, "portrait", "nobody")));
        PartyRefusal unknown = Refused(session);
        Assert.Equal("portrait-unknown", unknown.Code);
        Assert.Contains("is not a portrait creation offers", unknown.Message, StringComparison.Ordinal);
        Node creation = making.Projections.Latest.Field("creation");
        Assert.Equal("portrait-unknown", creation.Field("refusalCode").Text());
        Assert.Equal(unknown.Message, creation.Field("refusalMessage").Text());
        Assert.True(creation.Field("active").Flag());

        // A choice command that arrived naming no choice is refused rather than silently dropped.
        session.Update(Update(13, 1, Payload("""{"action":"creation.select-portrait"}""")));
        PartyRefusal missing = Refused(session);
        Assert.Equal("creation-choice-missing", missing.Code);
        Assert.Contains("naming no portrait", missing.Message, StringComparison.Ordinal);

        // Accepting an unfinished party is refused with the members and the steps that are unfinished, and
        // nothing is built: creation is what the player goes back to.
        session.Update(Update(14, 1, Command(CreationActions.Accept)));
        PartyRefusal incomplete = Refused(session);
        Assert.Equal("creation-incomplete", incomplete.Code);
        Assert.Contains("member 1 is at the Portrait step", incomplete.Message, StringComparison.Ordinal);
        // The member that is finished is not named: what is unfinished is what stands between the player and
        // the party, and a message that listed finished members too would not say what is left to do.
        Assert.DoesNotContain("member 2", incomplete.Message, StringComparison.Ordinal);
        Assert.Equal(SessionMode.Creating, session.Mode);
        Assert.Empty(making.Built);

        // The next legal choice clears the refusal, so the screen shows the answer to the last choice.
        session.Update(Update(15, 1, Choose(CreationActions.SelectPortrait, "portrait", FolkA.Value)));
        Assert.Null(session.CreationRefusal);
        Assert.Equal(FolkA, session.Creation.Member(0).Portrait);
        Assert.Equal(string.Empty, making.Projections.Latest.Field("creation").Field("refusalCode").Text());
    }

    [Fact]
    public void The_world_does_not_advance_and_no_movement_is_read_while_creating()
    {
        using Making making = new(new PartyCreationFlow(Options, Defaults));
        PartyRpgSession session = making.Session;
        GameDate began = session.Clock!.Now;

        // Twelve seconds of admitted updates with the forward key held: the session counts the updates it
        // consumed and measures no simulation at all, because creating admits no interval.
        ulong step = 0;
        for (int update = 0; update < 120; update++)
        {
            session.Update(Update(step, 4, Digital(MovementNames.Forward, InputEdge.Pressed)));
            step += 4;
        }

        Assert.Equal(SessionMode.Creating, session.Mode);
        Assert.Equal(120ul, session.Updates);
        Assert.Equal(0d, session.SimulationSeconds);
        Assert.Equal(0ul, session.AdmittedSteps);
        Assert.Equal(began, session.Clock.Now);
        Assert.Equal(MovementDiagnostics.None, session.Movement);
        // The movement reader was never consulted, so the key that was held all this time was never read,
        // and no world exists yet to step: it is composed when the party is accepted.
        Assert.Empty(making.Mover.Steps);
        Assert.Null(session.LiveWorld);
        Assert.Equal(0d, making.Projections.Latest.Field("world").Field("places").AsNumber());

        session.Update(Update(step, 1, Digital(Controls.Accept)));
        step++;
        Assert.NotNull(session.LiveWorld);

        // The accepted world starts where its scenario places the party, its scene is entered like any
        // other, and the party is still where the scenario put it.
        Assert.Equal(new PlaceId("1"), session.LiveWorld.Place);
        Assert.Equal([new PlaceId("1")], making.Mover.Entered);
        Assert.Equal(4, session.LiveWorld.Party.PlacePose.X, 6);

        // The first running update steps the party with the intent the reader holds — which is *still*,
        // because the press during creation was never read. A held key surviving creation would walk the
        // party the moment the game began, and this is what proves it does not.
        session.Update(Update(step, 1, []));
        (MovementIntent intent, double seconds) = Assert.Single(making.Mover.Steps);
        Assert.True(intent.IsStill);
        Assert.Equal(StepSeconds, seconds, 9);
        Assert.Equal(StepSeconds, session.SimulationSeconds, 9);
        Assert.Equal(1ul, session.AdmittedSteps);
        Assert.Equal(began, session.Clock.Now);

        // And once playing, the same key does move the party: the reader is live and the world is stepped.
        session.Update(Update(step + 1, 1, Digital(MovementNames.Forward, InputEdge.Pressed)));
        Assert.Equal(2, making.Mover.Steps.Count);
        Assert.Equal(1, making.Mover.Steps[1].Intent.Forward);
        Assert.True(session.LiveWorld.Party.PlacePose.X > 4);
    }

    [Fact]
    public void The_accepted_party_is_the_one_the_session_plays_with()
    {
        using Making making = new(new PartyCreationFlow(Options, Defaults));
        PartyRpgSession session = making.Session;
        session.Update(Update(10, 1, Digital(Controls.Accept)));

        PartyEntity party = Assert.Single(making.Built);
        Assert.Same(party, session.Party);

        // The world was composed over the accepted party, so what a road costs comes out of the accounts the
        // player's own characters filled: the crossing takes a portion the created party started with.
        int before = party.Food.Portions;
        PlaceTransition outbound = Assert.Single(session.LiveWorld!.Graph.TransitionsFrom(new PlaceId("1")));
        Assert.True(session.LiveWorld.Travel(outbound, TransitionKind.Walking).Arrived);
        Assert.Equal(before - 1, party.Food.Portions);
        Assert.Equal(new PlaceId("2"), session.LiveWorld.Place);

        // The panel's party block is the created party's own numbers, and its accepted list is the created
        // members — read from the party being played rather than from the flow that described it.
        session.PublishWorld();
        Node published = making.Projections.Latest;
        Assert.True(published.Field("party").Field("present").Flag());
        Assert.Equal(2d, published.Field("party").Field("members").AsNumber());
        Assert.Equal(25d, published.Field("party").Field("coins").AsNumber());
        Assert.Equal(before - 1d, published.Field("party").Field("provisions").AsNumber());
        Assert.Equal("Ann", published.Field("creation").Field("party").Element(0).Field("name").Text());
        Assert.Equal("folk-a", published.Field("creation").Field("party").Element(0).Field("portrait").Text());
        Assert.Equal("fighter", published.Field("creation").Field("party").Element(0).Field("class").Text());
        Assert.Equal("bo", published.Field("creation").Field("party").Element(1).Field("name").Text().ToLowerInvariant());
    }

    [Fact]
    public void The_projection_publishes_the_creation_the_flow_holds()
    {
        using Making making = new(new PartyCreationFlow(Options));
        PartyRpgSession session = making.Session;

        // A member answered down to its attributes: the screen is told the step, the pool, the member's own
        // answers, and every choice creation offers for the class and race it has.
        session.Update(Update(10, 1, Choose(CreationActions.SelectPortrait, "portrait", FolkA.Value)));
        session.Update(Update(11, 1, Command(CreationActions.Advance)));
        session.Update(Update(12, 1, Choose(CreationActions.SelectClass, "class", Fighter.Value)));
        session.Update(Update(13, 1, Command(CreationActions.Advance)));
        session.Update(Update(14, 1, Choose(CreationActions.SetName, "name", "Ann")));
        session.Update(Update(15, 1, Command(CreationActions.Advance)));
        session.Update(Update(16, 1, Choose(CreationActions.RaiseAttribute, "attribute", Vigour.Value)));

        Node creation = making.Projections.Latest.Field("creation");
        Assert.True(creation.Field("active").Flag());
        Assert.False(creation.Field("accepted").Flag());
        Assert.False(creation.Field("hasDefault").Flag());
        Assert.Equal(0d, creation.Field("member").AsNumber());
        Assert.Equal(2d, creation.Field("members").AsNumber());
        Assert.Equal("attributes", creation.Field("step").Text());
        Assert.Equal(3d, creation.Field("pool").AsNumber());

        // The roster is the flow's own answer for every member, including the ones not reached yet.
        Assert.Equal(2, creation.Field("roster").Count());
        Assert.Equal("Ann", creation.Field("roster").Element(0).Field("name").Text());
        Assert.Equal("testfolk", creation.Field("roster").Element(0).Field("race").Text());
        Assert.Equal("fighter", creation.Field("roster").Element(0).Field("class").Text());
        Assert.Equal("folk-a", creation.Field("roster").Element(0).Field("portrait").Text());
        Assert.Equal("attributes", creation.Field("roster").Element(0).Field("step").Text());
        Assert.Equal("portrait", creation.Field("roster").Element(1).Field("step").Text());
        Assert.Equal(string.Empty, creation.Field("roster").Element(1).Field("name").Text());

        // The portraits and classes are the options creation offers, with the chosen one marked.
        Assert.Equal(3, creation.Field("portraits").Count());
        Assert.True(creation.Field("portraits").Element(0).Field("selected").Flag());
        Assert.Equal("Folk A", creation.Field("portraits").Element(0).Field("name").Text());
        Assert.Equal("testfolk", creation.Field("portraits").Element(0).Field("race").Text());
        Assert.False(creation.Field("portraits").Element(1).Field("selected").Flag());
        Assert.Equal(2, creation.Field("classes").Count());
        Assert.True(creation.Field("classes").Element(0).Field("selected").Flag());
        Assert.Equal("Fighter", creation.Field("classes").Element(0).Field("name").Text());

        // The skills are the class's own: the two it fixes, the three it offers, and nothing else — with the
        // state word the screen renders instead of working out which list an id belongs to.
        Assert.Equal(5, creation.Field("skills").Count());
        Assert.Equal(
            ["blades", "bulwark", "axes", "bows", "lore"],
            Enumerable.Range(0, 5).Select(index => creation.Field("skills").Element(index).Field("id").Text()));
        Assert.Equal(
            ["fixed", "fixed", "available", "available", "available"],
            Enumerable.Range(0, 5).Select(index => creation.Field("skills").Element(index).Field("state").Text()));

        // The attributes carry the race's own bounds and what the pool may do to each.
        Assert.Equal(2, creation.Field("attributes").Count());
        Node vigour = creation.Field("attributes").Element(0);
        Assert.Equal("vigour", vigour.Field("id").Text());
        Assert.Equal(9d, vigour.Field("value").AsNumber());
        Assert.Equal(6d, vigour.Field("minimum").AsNumber());
        Assert.Equal(12d, vigour.Field("maximum").AsNumber());
        Assert.True(vigour.Field("canRaise").Flag());
        Assert.True(vigour.Field("canLower").Flag());
        Assert.Equal(string.Empty, creation.Field("refusalCode").Text());
        Assert.Equal(string.Empty, creation.Field("refusalMessage").Text());

        // A refusal is published beside the choices, so the screen can show the rule and keep the screen.
        session.Update(Update(17, 1, Choose(CreationActions.SelectPortrait, "portrait", "nobody")));
        creation = making.Projections.Latest.Field("creation");
        Assert.Equal("creation-step", creation.Field("refusalCode").Text());
        Assert.Contains(
            "Choosing a portrait happens at the Portrait step",
            creation.Field("refusalMessage").Text(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_session_that_creates_holds_no_other_party_or_world()
    {
        using CapturedProjections projections = new();
        using PartyEntity party = new PartyEntityFactory().Create(OneMemberParty());

        // A session either creates its party or holds one. Handing it both is refused by name rather than
        // leaving two parties for one expedition.
        ArgumentException both = Assert.Throws<ArgumentException>(() => new PartyRpgSession(
            Composition,
            projections,
            party: party,
            creationInput: new CreationInput(Controls),
            creation: Creation()));
        Assert.Contains("leave two of them", both.Message, StringComparison.Ordinal);

        using SessionWorld world = World(party);
        ArgumentException withWorld = Assert.Throws<ArgumentException>(() => new PartyRpgSession(
            Composition,
            projections,
            world: world,
            creationInput: new CreationInput(Controls),
            creation: Creation()));
        Assert.Contains("leave two of them", withWorld.Message, StringComparison.Ordinal);

        // Creating without the controls its commands arrive on would be a screen nobody could choose
        // anything on, which is refused where it is composed.
        ArgumentException noControls = Assert.Throws<ArgumentException>(() => new PartyRpgSession(
            Composition,
            projections,
            creation: Creation()));
        Assert.Contains("needs the controls its commands arrive on", noControls.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_save_round_trips_the_party_the_session_accepted()
    {
        using Making making = new(new PartyCreationFlow(Options, Defaults));
        PartyRpgSession session = making.Session;
        session.Update(Update(10, 1, Digital(Controls.Accept)));

        // A created party is captured and rebuilt through the same factory a restored one is, which is what
        // makes the party the session plays the durable shape a save carries.
        SessionSave save = session.Capture();
        using PartyEntity restored = new PartyEntityFactory().Restore(save.Party);

        Assert.Equal(["Ann", "Bo"], restored.Members.Select(member => member.Profile.Name));
        Assert.Equal([Testfolk, Stonefolk], restored.Members.Select(member => member.Profile.Race));
        Assert.Equal([Fighter, Adept], restored.Members.Select(member => member.Profile.Class));
        Assert.Equal([FolkA, StoneA], restored.Members.Select(member => member.Profile.Portrait));
        Assert.Equal(25, restored.Purse.Coins);
        Assert.Equal(3, restored.Food.Portions);
    }

    [Fact]
    public void This_games_own_creation_flow_runs_through_the_session()
    {
        // The compiled ruleset's flow, over its own choices and its own default party, driven through the
        // session exactly as the product drives it: this is the path a player takes, and its four members
        // are the ones the ruleset's default party names.
        using Making making = new(MightAndMagic7Creation.Start());
        PartyRpgSession session = making.Session;

        Assert.Equal(SessionMode.Creating, session.Mode);
        Assert.True(session.Creation!.HasDefault);
        Assert.Equal(MightAndMagic7Creation.MemberCount, session.Creation.MemberCount);
        Assert.Equal(0, session.Creation.PoolRemaining);

        session.Update(Update(10, 1, Digital(Controls.Accept)));

        Assert.Equal(SessionMode.Running, session.Mode);
        PartyEntity party = Assert.Single(making.Built);
        Assert.Equal(4, party.Members.Count);
        Assert.Equal(["Roderick", "Aelina", "Borin", "Nyx"], party.Members.Select(member => member.Profile.Name));
        Assert.Equal(["Human", "Elf", "Dwarf", "Goblin"], party.Members.Select(member => member.Profile.Race.Value));
        Assert.Equal(
            ["Knight", "Sorcerer", "Cleric", "Thief"],
            party.Members.Select(member => member.Profile.Class.Value));
        Assert.Equal(
            ["human-man", "elf-woman", "dwarf-man", "goblin-woman"],
            party.Members.Select(member => member.Profile.Portrait!.Value.Value));
        Assert.Equal(MightAndMagic7Creation.StartingCoins, party.Purse.Coins);
        Assert.Equal(MightAndMagic7Creation.StartingFoodPortions, party.Food.Portions);
    }

    /// <summary>Walks one member through every step with the choices given, as the session's commands.</summary>
    /// <returns>The admitted step the walk finished on, so a caller can carry on from there.</returns>
    private static ulong Drive(
        PartyRpgSession session,
        string name,
        PortraitId portrait,
        ClassId characterClass,
        IReadOnlyList<(AttributeId Attribute, int Clicks)> attributes,
        IReadOnlyList<SkillId> chosenSkills,
        ulong from = 0)
    {
        ulong step = Math.Max(from, session.Updates) + 100;
        session.Update(Update(step++, 1, Choose(CreationActions.SelectPortrait, "portrait", portrait.Value)));
        session.Update(Update(step++, 1, Command(CreationActions.Advance)));
        session.Update(Update(step++, 1, Choose(CreationActions.SelectClass, "class", characterClass.Value)));
        session.Update(Update(step++, 1, Command(CreationActions.Advance)));
        session.Update(Update(step++, 1, Choose(CreationActions.SetName, "name", name)));
        session.Update(Update(step++, 1, Command(CreationActions.Advance)));
        foreach ((AttributeId attribute, int clicks) in attributes)
        {
            for (int click = 0; click < clicks; click++)
            {
                session.Update(Update(step++, 1, Choose(CreationActions.RaiseAttribute, "attribute", attribute.Value)));
            }
        }

        Assert.Equal(0, session.Creation!.PoolRemaining);
        session.Update(Update(step++, 1, Command(CreationActions.Advance)));
        foreach (SkillId skill in chosenSkills)
        {
            session.Update(Update(step++, 1, Choose(CreationActions.ChooseSkill, "skill", skill.Value)));
        }

        session.Update(Update(step++, 1, Command(CreationActions.Advance)));
        return step;
    }

    private static PartyRefusal Refused(PartyRpgSession session) =>
        session.CreationRefusal ?? throw new InvalidOperationException("Creation refused nothing.");

    private static int AttributeOf(PartyEntity party, int member, AttributeId attribute) =>
        party.Members[member].Attributes.Scores.First(score => score.Attribute == attribute).Value;

    /// <summary>The creation a session is composed with in this suite, over the flow it is handed.</summary>
    private static SessionCreation Creation(PartyCreationFlow? flow = null) => new(
        flow ?? new PartyCreationFlow(Options),
        creation => new PartyEntityFactory().Create(creation),
        party => World(party));

    /// <summary>A world over the party, composed the way the ruleset composes its own.</summary>
    private static SessionWorld World(PartyEntity party)
    {
        PlaceGraph graph = TestGraph();
        PartyPoseOwner pose = Pose();
        RecordingMover mover = new(pose);
        GameClock clock = Clock();
        return new SessionWorld(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new ProvisionsRoad(),
            time: clock,
            mover: mover,
            diagnostics: null,
            entrances: null,
            clock: clock,
            resources: new PartyResourceLedger(party));
    }

    /// <summary>A one-member party built through the factory, the way a creation finishes.</summary>
    private static PartyCreation OneMemberParty()
    {
        PartyCreationFlow flow = new(SingleOptions);
        Assert.Null(flow.SelectPortrait(FolkA));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SelectClass(Fighter));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SetName("Ann"));
        Assert.Null(flow.Advance());
        while (flow.PoolRemaining > 0) Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.Advance());
        Assert.Null(flow.ChooseSkill(Axes));
        Assert.Null(flow.ChooseSkill(Bows));
        Assert.Null(flow.Advance());
        return flow.ToCreation();
    }

    private static PartyPoseOwner Pose() => new(
        new PartyPose(new PlaceId("1"), new PlacePose(4, 0, 0, Yaw: 0, Pitch: 0)),
        new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));

    /// <summary>The one clock this suite's session keeps, in the shape this game composes its own.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    private static PartyCreationOptions Options { get; } = new(
        memberCount: 2,
        races:
        [
            new CreationRace(Testfolk, "Testfolk",
            [
                new AttributeCreationRange(Vigour, "Vigour", start: 8, minimum: 6, maximum: 12, stepSize: 1, stepCost: 1),
                new AttributeCreationRange(Wit, "Wit", start: 6, minimum: 4, maximum: 12, stepSize: 2, stepCost: 1),
            ]),
            new CreationRace(Stonefolk, "Stonefolk",
            [
                new AttributeCreationRange(Vigour, "Vigour", start: 6, minimum: 4, maximum: 12, stepSize: 1, stepCost: 1),
                new AttributeCreationRange(Wit, "Wit", start: 6, minimum: 4, maximum: 12, stepSize: 1, stepCost: 1),
            ]),
        ],
        classes:
        [
            new CreationClass(Fighter, "Fighter", [Blades, Bulwark], [Axes, Bows, Lore], startingHitPoints: 40, startingSpellPoints: 0, startingRank: 1),
            new CreationClass(Adept, "Adept", [Wards, Aim], [Lore, Bows, Axes], startingHitPoints: 20, startingSpellPoints: 10, startingRank: 1),
        ],
        portraits:
        [
            new CreationPortrait(FolkA, Testfolk, "Folk A"),
            new CreationPortrait(FolkB, Testfolk, "Folk B"),
            new CreationPortrait(StoneA, Stonefolk, "Stone A"),
        ],
        attributePool: 4,
        chosenSkillCount: 2,
        nameMaximumLength: 8,
        startingSkillTier: new SkillTier(1),
        startingLevel: 1,
        startingCoins: 25,
        startingFoodPortions: 3,
        startingReputation: 2,
        startingFame: 1);

    /// <summary>The same choices for a party of one, which is what a composition case is handed.</summary>
    private static PartyCreationOptions SingleOptions { get; } = new(
        memberCount: 1,
        races: Options.Races,
        classes: Options.Classes,
        portraits: Options.Portraits,
        attributePool: Options.AttributePool,
        chosenSkillCount: Options.ChosenSkillCount,
        nameMaximumLength: Options.NameMaximumLength,
        startingSkillTier: Options.StartingSkillTier,
        startingLevel: Options.StartingLevel,
        startingCoins: Options.StartingCoins,
        startingFoodPortions: Options.StartingFoodPortions,
        startingReputation: Options.StartingReputation,
        startingFame: Options.StartingFame);

    /// <summary>The default party this suite's flow starts from: two finished members.</summary>
    private static PartyCreationDefaults Defaults { get; } = new(
    [
        new CreationMemberDefaults(
            FolkA,
            Fighter,
            "Ann",
            [new AttributeScore(Vigour, 12), new AttributeScore(Wit, 6)],
            [Axes, Bows]),
        new CreationMemberDefaults(
            StoneA,
            Adept,
            "Bo",
            [new AttributeScore(Vigour, 6), new AttributeScore(Wit, 10)],
            [Lore, Bows]),
    ]);

    /// <summary>One admitted update carrying the given input, in the shape the engine admits it.</summary>
    private static ProductUpdate Update(ulong simulationStep, uint admittedSteps, params ProductInputEvent[] input)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            simulationStep,
            60,
            admittedSteps,
            0,
            StepSeconds);
        return new ProductUpdate(facts, input);
    }

    /// <summary>One digital engine event on a declared intent.</summary>
    private static ProductInputEvent Digital(string intent, InputEdge edge = InputEdge.Pressed) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>One payload action, exactly as the DOM companion sends it.</summary>
    private static ProductInputEvent Payload(string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(Controls.ActionContract), Encoding.UTF8.GetBytes(json));

    /// <summary>A creation action that carries a choice.</summary>
    private static ProductInputEvent Choose(string action, string field, string value) =>
        Payload($$"""{"action":"{{action}}","{{field}}":"{{value}}"}""");

    /// <summary>A creation action that carries no choice.</summary>
    private static ProductInputEvent Command(string action) => Payload($$"""{"action":"{{action}}"}""");

    /// <summary>A creation action that names a member.</summary>
    private static ProductInputEvent Member(int index) =>
        Payload($$"""{"action":"{{CreationActions.SelectMember}}","member":{{index}}}""");

    /// <summary>
    /// A session making a party, with the world it will play in composed when the party is accepted.
    /// </summary>
    /// <remarks>
    /// The world here is the test's own composition, built the way the ruleset builds its own: the party's
    /// ledger over the party creation just built, and a mover that records what it was asked and moves the
    /// party through the world's own pose owner.
    /// </remarks>
    private sealed class Making : IDisposable
    {
        private readonly PlaceGraph _graph;
        private readonly PartyPoseOwner _pose;

        internal Making(PartyCreationFlow flow)
        {
            Clock = Clock();
            _graph = TestGraph();
            _pose = Pose();
            Mover = new RecordingMover(_pose);
            Projections = new CapturedProjections();
            Session = new PartyRpgSession(
                Composition,
                Projections,
                movementInput: new MovementInput(MovementNames, turnRatePerSecond: 512),
                clock: Clock,
                creationInput: new CreationInput(Controls),
                creation: new SessionCreation(flow, Build, Compose));
            Session.Start();
        }

        internal PartyRpgSession Session { get; }

        internal CapturedProjections Projections { get; }

        internal GameClock Clock { get; }

        internal RecordingMover Mover { get; }

        /// <summary>Every party the factory built, so a case can prove one was built and which one.</summary>
        internal List<PartyEntity> Built { get; } = [];

        public void Dispose()
        {
            Session.Dispose();
            foreach (PartyEntity party in Built) party.Dispose();
        }

        private PartyEntity Build(PartyCreation creation)
        {
            PartyEntity party = new PartyEntityFactory().Create(creation);
            Built.Add(party);
            return party;
        }

        /// <summary>Composes a world over the party, which is what the session does when creation is accepted.</summary>
        private SessionWorld Compose(PartyEntity party) => new(
            _graph,
            _pose,
            new PlaceStateLedger(_graph, PlaceRespawnRule.FromContent()),
            new ProvisionsRoad(),
            time: Clock,
            mover: Mover,
            diagnostics: null,
            entrances: null,
            clock: Clock,
            resources: new PartyResourceLedger(party));
    }

    /// <summary>Records the projections one session published, as values a case can navigate.</summary>
    private sealed class CapturedProjections : IUiProjectionChannel
    {
        private readonly List<UiValue> _published = [];

        internal Node Latest => new(_published[^1], _published[^1].Root);

        public void Publish(UiValue value) => _published.Add(value);

        public void Dispose()
        {
        }
    }

    /// <summary>Navigates one published projection by field name and array position.</summary>
    private readonly struct Node(UiValue value, uint index)
    {
        internal Node Field(string key)
        {
            StructuredValueNode node = value.Nodes.Span[(int)index];
            for (uint edge = node.FirstEdge; edge < node.FirstEdge + node.ChildCount; edge++)
            {
                uint child = value.Edges.Span[(int)edge];
                if (Key(child) == key) return new Node(value, child);
            }

            throw new KeyNotFoundException($"Projection has no field '{key}'.");
        }

        internal Node Element(int position)
        {
            StructuredValueNode node = value.Nodes.Span[(int)index];
            if (position < 0 || position >= node.ChildCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    position,
                    $"The projected array holds {node.ChildCount} elements.");
            }

            return new Node(value, value.Edges.Span[(int)node.FirstEdge + position]);
        }

        internal int Count() => (int)value.Nodes.Span[(int)index].ChildCount;

        internal string Text()
        {
            StructuredValueNode node = value.Nodes.Span[(int)index];
            if (node.Kind != StructuredValueKind.String)
            {
                throw new InvalidOperationException($"Projection node is {node.Kind}, not a string.");
            }

            return Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)node.TextOffset, (int)node.TextLen));
        }

        internal double AsNumber()
        {
            StructuredValueNode node = value.Nodes.Span[(int)index];
            if (node.Kind != StructuredValueKind.Number)
            {
                throw new InvalidOperationException($"Projection node is {node.Kind}, not a number.");
            }

            return node.NumberValue;
        }

        internal bool Flag()
        {
            StructuredValueNode node = value.Nodes.Span[(int)index];
            if (node.Kind != StructuredValueKind.Bool)
            {
                throw new InvalidOperationException($"Projection node is {node.Kind}, not a boolean.");
            }

            return node.BoolValue != 0;
        }

        private string Key(uint child)
        {
            StructuredValueNode node = value.Nodes.Span[(int)child];
            return Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)node.KeyOffset, (int)node.KeyLen));
        }
    }

    /// <summary>The movement as the test drives it: it records what it was asked and moves the party.</summary>
    private sealed class RecordingMover(PartyPoseOwner party) : IPartyMover
    {
        internal List<(MovementIntent Intent, double Seconds)> Steps { get; } = [];

        internal List<PlaceId> Entered { get; } = [];

        public PlaceGeometryAdmission Enter(PlaceId place)
        {
            Entered.Add(place);
            return PlaceGeometryAdmission.Empty(place);
        }

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
        {
            Steps.Add((intent, elapsedSeconds));

            // What the engine resolves, in miniature: the party is moved through its pose owner and nowhere
            // else, so the test holds the same single-writer rule the product does.
            double forward = intent.Forward * elapsedSeconds * 180;
            party.Move(forward, 0, 0);
            return new MovementOutcome(
                party.Capture().Pose,
                new System.Numerics.Vector3((float)forward, 0, 0),
                Grounded: true,
                default,
                CharacterBlockFlags.None,
                default,
                SurfaceEffect.Ordinary,
                FallOutcome.None);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A road that quotes a portion, so what the world charges lands in the party's own larder.</summary>
    private sealed class ProvisionsRoad : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) =>
            TravelCostQuote.Payable(new TravelCost(TravelTime.None, new Provisions(1, ProvisionUnit.Portions)));
    }

    /// <summary>Two places joined by one transition, which is all a world needs to be walked in.</summary>
    private static PlaceGraph TestGraph() => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json",
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
                .Add("packs/world/places.json",
                    """
                    {
                      "documentId": "places",
                      "definitionKind": "place",
                      "entries": [
                        { "id": "1", "kind": "region", "name": "Home", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 4, "y": 0, "z": 0, "yaw": 0 } ] },
                        { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
                      ]
                    }
                    """)
                .Add("packs/world/links.json",
                    """
                    {
                      "documentId": "links",
                      "definitionKind": "travel-link",
                      "entries": [ { "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ]
                    }
                    """),
            new ContentLayout("packs", "imports", "bundles")).RequireValid());
}
