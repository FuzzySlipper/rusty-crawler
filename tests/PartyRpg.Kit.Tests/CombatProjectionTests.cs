using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Interaction;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What the interface can read about a fight: every fact the panel shows, read back from the state the
/// session actually holds, through the same path the DOM companion uses to act.
/// </summary>
/// <remarks>
/// <para>
/// The other combat suites prove what the fight does. This one proves what a player can see it doing, and it
/// does that by driving the session the way the companion does — a payload action on the declared contract,
/// never a channel only a test can reach — and then reading the published projection. A fact that is in the
/// fight but not in the projection, or in the projection but not the fight's own, fails here.
/// </para>
/// <para>
/// The rules are the suite's own, as they are in every kit suite: a creature's recovery, what a blow is
/// worth, and which conditions leave a member unable to act are all stated in the fixture, so an assertion
/// about a readiness number or a refusal is an assertion about the mechanism rather than about this game.
/// </para>
/// </remarks>
public sealed class CombatProjectionTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Hall = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");

    /// <summary>What one action of a party member of this suite costs, in game time.</summary>
    private static readonly GameDuration MemberRecovery = GameDuration.FromSeconds(2);

    /// <summary>What one action of a creature of this suite costs, in game time.</summary>
    private static readonly GameDuration CreatureRecovery = GameDuration.FromSeconds(1);

    /// <summary>The condition this suite's rules leave a member unable to act with.</summary>
    private static readonly ConditionId LaidOut = new("Laid out");

    /// <summary>How far a creature of this suite notices the party from, in the place's own units.</summary>
    private const double NoticeRange = 500;

    /// <summary>What a blow of this suite lands for, which is one whole creature of this suite's own pool.</summary>
    private const int BlowDamage = 6;

    [Fact]
    public void The_panel_reads_the_fights_own_facts_and_none_of_its_own()
    {
        using Fixture fixture = new(creatureAt: 100, creatureHitPoints: 40);
        fixture.Start();

        // Before anything has been read there is a mechanism and no fight: "the session holds none", "nothing
        // is hostile", and "the party is fighting" are three facts a panel must be able to tell apart.
        ProjectedNode combat = fixture.Combat;
        Assert.True(combat.Field("available").AsBoolean());
        Assert.False(combat.Field("engaged").AsBoolean());
        Assert.Equal("none", combat.Field("outcome").AsString());

        fixture.Update();

        // Who is in the fight, who is hostile and how far off, and the pacing it is being played in.
        combat = fixture.Combat;
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1d, combat.Field("opposition").AsNumber());
        Assert.Equal(4d, combat.Field("ready").AsNumber());
        Assert.Equal("realtime", combat.Field("pacing").AsString());
        Assert.Equal("none", combat.Field("turn").Field("phase").AsString());
        Assert.Equal(4, combat.Field("members").Length());

        // Each member's readiness, health, and conditions are published per member, from the party that owns
        // them: the panel renders each row from these numbers and computes none of them.
        ProjectedNode member = combat.Field("members").Item(0);
        Assert.Equal("Member 1", member.Field("name").AsString());
        Assert.True(member.Field("ready").AsBoolean());
        Assert.Equal(0d, member.Field("recoverySeconds").AsNumber());
        Assert.Equal(40d, member.Field("hitPoints").AsNumber());
        Assert.Equal(40d, member.Field("hitPointsMax").AsNumber());
        Assert.Equal(string.Empty, member.Field("conditions").AsString());
        Assert.False(member.Field("down").AsBoolean());

        // What is hostile, how far away, what it has left to lose, and what it is doing: a creature's own
        // facts, read from its own entity rather than copied beside it.
        ProjectedNode enemy = combat.Field("enemies").Item(0);
        Assert.Equal("A beast", enemy.Field("name").AsString());
        Assert.Equal(100d, enemy.Field("distance").AsNumber());
        Assert.Equal(40d, enemy.Field("hitPoints").AsNumber());
        Assert.Equal(40d, enemy.Field("hitPointsMax").AsNumber());
        Assert.False(enemy.Field("down").AsBoolean());

        // The order the panel sends is the companion's own payload action, and what the last one did is
        // published in the fight's numbers: who acted, at what, what it rolled, and what was left.
        fixture.Act();
        combat = fixture.Combat;
        Assert.Equal("applied", combat.Field("outcome").AsString());
        Assert.True(combat.Field("resolved").AsBoolean());
        Assert.True(combat.Field("hit").AsBoolean());
        Assert.Equal(10_000d, combat.Field("chance").AsNumber());
        Assert.Equal(BlowDamage, combat.Field("damageRolled").AsNumber());
        Assert.Equal(BlowDamage, combat.Field("damage").AsNumber());
        Assert.Equal("Phys", combat.Field("damageKind").AsString());
        Assert.Equal("Member 4", combat.Field("actor").AsString());
        Assert.Equal("A beast", combat.Field("target").AsString());
        Assert.True(combat.Field("byParty").AsBoolean());
        Assert.False(combat.Field("targetDown").AsBoolean());
        Assert.Equal(40d - (4 * BlowDamage), combat.Field("enemies").Item(0).Field("hitPoints").AsNumber());
        Assert.Equal(MemberRecovery.TotalSeconds, combat.Field("recoverySeconds").AsNumber(), 3);

        // And the party's own pools and standing are published beside the fight, which is where a player reads
        // what a fight has cost: health, spell points, and the conditions acting on the party.
        ProjectedNode party = fixture.Party;
        Assert.True(party.Field("present").AsBoolean());
        Assert.Equal(160d, party.Field("hitPoints").AsNumber());
        Assert.Equal(160d, party.Field("hitPointsMax").AsNumber());
        Assert.Equal(40d, party.Field("spellPoints").AsNumber());
        Assert.Equal(40d, party.Field("spellPointsMax").AsNumber());
        Assert.Equal(string.Empty, party.Field("conditions").AsString());
    }

    [Fact]
    public void A_recovering_member_is_refused_when_the_panel_sends_the_order_anyway()
    {
        using Fixture fixture = new(creatureAt: 100, creatureHitPoints: 40);
        fixture.Start();
        fixture.Update();

        // The panel's own action applies, and it spends every member's recovery: readiness is the state the
        // fight holds, published in the same update the order arrived in.
        fixture.Act();
        ProjectedNode combat = fixture.Combat;
        Assert.Equal("applied", combat.Field("outcome").AsString());
        Assert.Equal(0d, combat.Field("ready").AsNumber());
        Assert.All(
            Enumerable.Range(0, 4).Select(index => combat.Field("members").Item(index)),
            member => Assert.False(member.Field("ready").AsBoolean()));

        // The same control asked again in the next update is refused by name rather than quietly doing
        // nothing — the refusal is the product's answer, and it is what the panel has to show. This is the
        // order a panel that offered a disabled-looking control would have sent anyway.
        fixture.Act();
        combat = fixture.Combat;
        Assert.Equal("refused", combat.Field("outcome").AsString());
        Assert.Equal("recovering", combat.Field("code").AsString());
        Assert.Contains("still recovering", combat.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("Member 4", combat.Field("message").AsString(), StringComparison.Ordinal);
        // A refusal attacked nothing, so nothing about an attack travels beside it: the panel shows the
        // refusal rather than the shape of the swing before it.
        Assert.False(combat.Field("resolved").AsBoolean());
        Assert.Equal(string.Empty, combat.Field("kind").AsString());
        Assert.Equal(string.Empty, combat.Field("target").AsString());
        Assert.Equal(0d, combat.Field("recoverySeconds").AsNumber());
        Assert.Equal(40d - (4 * BlowDamage), combat.Field("enemies").Item(0).Field("hitPoints").AsNumber());
    }

    [Fact]
    public void Readiness_is_released_by_the_clock_and_the_projection_follows_in_that_update()
    {
        using Fixture fixture = new(creatureAt: 100, creatureHitPoints: 40);
        fixture.Start();
        fixture.Update();
        fixture.Act();
        Assert.Equal(0d, fixture.Combat.Field("ready").AsNumber());

        // One admitted update of a sixtieth of a second is half a game second at this suite's clock, so the
        // members' two seconds are owed for three updates and released by the fourth. What is asserted is
        // which update: the projection published by the update that released them is the one that says so,
        // rather than one the panel would have had to count down for itself.
        fixture.Update();
        Assert.Equal(1.5, fixture.Combat.Field("members").Item(0).Field("recoverySeconds").AsNumber(), 3);
        Assert.Equal(0d, fixture.Combat.Field("ready").AsNumber());

        fixture.Update();
        Assert.Equal(1.0, fixture.Combat.Field("members").Item(0).Field("recoverySeconds").AsNumber(), 3);
        Assert.Equal(0d, fixture.Combat.Field("ready").AsNumber());

        fixture.Update();
        Assert.Equal(0.5, fixture.Combat.Field("members").Item(0).Field("recoverySeconds").AsNumber(), 3);
        Assert.Equal(0d, fixture.Combat.Field("ready").AsNumber());

        fixture.Update();
        ProjectedNode combat = fixture.Combat;
        Assert.Equal(0d, combat.Field("members").Item(0).Field("recoverySeconds").AsNumber());
        Assert.True(combat.Field("members").Item(0).Field("ready").AsBoolean());
        Assert.Equal(4d, combat.Field("ready").AsNumber());

        // A held session releases nobody: the same fight, the same clock, and no admitted time.
        fixture.Hold();
        fixture.Update();
        Assert.Equal(0d, fixture.Combat.Field("ready").AsNumber() - 4d);
        fixture.Release();
    }

    [Fact]
    public void A_member_the_fight_laid_out_reads_as_down_and_is_never_counted_ready()
    {
        using Fixture fixture = new(creatureAt: 100, creatureHitPoints: 40);
        fixture.Start();
        fixture.Update();
        fixture.Act();

        // The member is laid out the way this suite's rules say: the pool is emptied and the condition that
        // leaves them unable to act is applied. Everything about it is state the party owns.
        PartyMember hurt = fixture.PartyEntity.Members[1];
        hurt.Resources.TakeDamage(40);
        hurt.Conditions.Apply(new ActiveCondition(LaidOut, 0));
        for (int update = 0; update < 4; update++) fixture.Update();

        ProjectedNode combat = fixture.Combat;
        ProjectedNode down = combat.Field("members").Item(1);
        Assert.True(down.Field("down").AsBoolean());
        Assert.Equal(0d, down.Field("hitPoints").AsNumber());
        Assert.Equal(40d, down.Field("hitPointsMax").AsNumber());
        Assert.Contains(LaidOut.Value, down.Field("conditions").AsString(), StringComparison.Ordinal);
        // The light stays off while the actor lies there, however much recovery has elapsed: a panel that
        // read readiness as "recovery is zero" would light up somebody who cannot act.
        Assert.False(down.Field("ready").AsBoolean());
        Assert.Equal(0d, down.Field("recoverySeconds").AsNumber());
        Assert.Equal(3d, combat.Field("ready").AsNumber());
        Assert.Equal(0d, combat.Field("members").Item(1).Field("recoverySeconds").AsNumber());

        // And the fight refuses that member's share of the order by name while the others still act, which
        // is exactly the answer the disabled light stands in for.
        IReadOnlyList<CombatResult> order = fixture.Fight.Engage();
        CombatResult refused = order.Single(result => !result.IsApplied);
        Assert.Equal("incapacitated", refused.Code);
        Assert.Contains("Member 2", refused.Message, StringComparison.Ordinal);
        Assert.Equal(3, order.Count(result => result.IsApplied));
    }

    [Fact]
    public void A_creature_that_notices_the_party_is_hostile_in_the_update_it_does()
    {
        // The creature stands four thousand units off with a notice range of five hundred, so the party is
        // in no fight at all: nothing hostile, nothing listed as an enemy.
        using Fixture fixture = new(creatureAt: 4000, creatureHitPoints: 40);
        fixture.Start();
        fixture.Update();
        Assert.False(fixture.Combat.Field("engaged").AsBoolean());
        Assert.Equal(0d, fixture.Combat.Field("opposition").AsNumber());
        Assert.Equal(0d, fixture.Combat.Field("enemies").Length());

        // The party walks inside the range, which is world state rather than anything about the fight, and the
        // very update that reads the world again publishes the fight: aggro is visible as it happens.
        fixture.WalkTo(3600);
        fixture.Update();
        ProjectedNode combat = fixture.Combat;
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1d, combat.Field("opposition").AsNumber());
        Assert.Equal(400d, combat.Field("enemies").Item(0).Field("distance").AsNumber());

        // And walking back out of range ends it again, because the side is re-read from the world every
        // update rather than remembered once it has been noticed.
        fixture.WalkTo(0);
        fixture.Update();
        Assert.False(fixture.Combat.Field("engaged").AsBoolean());
        Assert.Equal(0d, fixture.Combat.Field("opposition").AsNumber());
    }

    [Fact]
    public void The_bodies_a_place_holds_are_read_from_the_mechanism_that_found_them()
    {
        using Fixture fixture = new(creatureAt: 100, creatureHitPoints: 4 * BlowDamage);
        fixture.Start();
        fixture.Update();
        Assert.Equal(0d, fixture.Interaction.Field("bodies").AsNumber());

        // One order of this suite's own worth brings the creature down, so this update leaves a body: the
        // fight publishes the death it caused, and the place's own rows keep the actor so a reader can see it
        // is down rather than gone.
        fixture.Act();
        ProjectedNode combat = fixture.Combat;
        Assert.True(combat.Field("targetDown").AsBoolean());
        Assert.Equal(0d, combat.Field("opposition").AsNumber());
        ProjectedNode body = combat.Field("enemies").Item(0);
        Assert.True(body.Field("down").AsBoolean());
        Assert.False(body.Field("ready").AsBoolean());
        Assert.Equal(0d, body.Field("hitPoints").AsNumber());

        // The body is here in the update after the one that made it: the mechanism that discovers what the
        // place holds is stepped inside the same admitted update it is read in, so a kill this update is a
        // body the panel counts in the next one rather than an update later still.
        fixture.Update();
        Assert.Equal(1d, fixture.Interaction.Field("bodies").AsNumber());
    }

    [Fact]
    public void The_paced_round_the_panel_reads_is_the_pacings_own_reading_of_the_same_fight()
    {
        using Fixture fixture = new(creatureAt: 100, creatureHitPoints: 40);
        fixture.Start();
        fixture.Update();

        // The round is published whole: which pacing the one fight is in, the phase, the round, whose turn it
        // is, whether the session is waiting for the player, and the order ascending the recovery that paces
        // real time. Everything here is the pacing's own reading rather than a second initiative.
        fixture.TurnBased();
        ProjectedNode combat = fixture.Combat;
        Assert.Equal("turnbased", combat.Field("pacing").AsString());
        Assert.Equal("action", combat.Field("turn").Field("phase").AsString());
        Assert.Equal(1d, combat.Field("turn").Field("round").AsNumber());
        Assert.True(combat.Field("turn").Field("playerTurn").AsBoolean());
        Assert.Equal("Member 1", combat.Field("turn").Field("actorName").AsString());
        ProjectedNode first = combat.Field("turn").Field("order").Item(0);
        Assert.True(first.Field("current").AsBoolean());
        Assert.True(first.Field("ready").AsBoolean());

        // The panel's two turn actions are committed turns like any other, and what the last one did is
        // published as the round's own word rather than as something the screen remembers.
        fixture.Action("combat.turn-skip");
        combat = fixture.Combat;
        Assert.Equal("skip", combat.Field("turn").Field("last").AsString());
        Assert.NotEqual("Member 1", combat.Field("turn").Field("actorName").AsString());

        fixture.Action("combat.turn-wait");
        combat = fixture.Combat;
        Assert.Equal("wait", combat.Field("turn").Field("last").AsString());
        Assert.Contains(
            Enumerable.Range(0, combat.Field("turn").Field("order").Length()),
            index => combat.Field("turn").Field("order").Item(index).Field("waiting").AsBoolean());

        // Acting is the same act the real-time pacing orders: the panel's own action spends the actor's
        // recovery, which the round then reads as the member's place in the order.
        fixture.Act();
        combat = fixture.Combat;
        Assert.Equal("applied", combat.Field("outcome").AsString());
        Assert.Contains(
            Enumerable.Range(0, combat.Field("members").Length()),
            index => combat.Field("members").Item(index).Field("recoverySeconds").AsNumber() >= MemberRecovery.TotalSeconds);
    }

    /// <summary>
    /// One session over one hall, with the party, the fight, the bodies they leave, and the clock composed
    /// exactly as the product composes them.
    /// </summary>
    /// <remarks>
    /// The fixture is the panel's path and nothing else: every action goes through the declared payload
    /// contract, and every assertion reads the projection the session published. The fight's own state is
    /// reachable too, which is what lets a test say that a refusal in the projection is a refusal of the
    /// fight rather than something the projection decided.
    /// </remarks>
    private sealed class Fixture : IDisposable
    {
        private readonly CorpseGround _ground = new();
        private readonly RecordingUiProjectionChannel _channel = new();
        private readonly PartyEntity _party;
        private readonly SessionWorld _world;
        private readonly PartyRpgSession _session;
        private ulong _step;

        /// <summary>Composes the session with a creature standing where the test says.</summary>
        /// <param name="creatureAt">Where the creature stands along the hall's first ground axis.</param>
        /// <param name="creatureHitPoints">What the creature can take.</param>
        internal Fixture(double creatureAt, int creatureHitPoints)
        {
            _party = Party();
            Rules rules = new(_ground, creatureHitPoints);
            _world = World(_party, rules, creatureAt);
            _session = new PartyRpgSession(
                new SessionComposition(new RulesetId("test.ruleset"), "Test"),
                _channel,
                _world,
                clock: Clock(),
                party: _party,
                combat: rules,
                combatInput: new CombatIntentNames(
                    "test.attack",
                    // The companion's own action name, so the test drives the control the product declares
                    // rather than one invented for it.
                    "party.attack",
                    "test.actions",
                    new TurnIntentNames("combat.turn-based", "combat.turn-skip", "combat.turn-wait", "test.actions")));
        }

        /// <summary>The fight the session holds, for the assertions that are about the fight itself.</summary>
        internal CombatState Fight => _session.Combat ?? throw new InvalidOperationException("The session composed no fight.");

        /// <summary>The party the session plays, which a test wounds through the party's own entry.</summary>
        internal PartyEntity PartyEntity => _party;

        /// <summary>The fight block the panel renders.</summary>
        internal ProjectedNode Combat => _channel.Latest().Field("combat");

        /// <summary>The party block the panel renders.</summary>
        internal ProjectedNode Party => _channel.Latest().Field("party");

        /// <summary>The interaction block, which is where the bodies lying here are published.</summary>
        internal ProjectedNode Interaction => _channel.Latest().Field("interaction");

        /// <summary>Starts the session, which is what publishes its first projection.</summary>
        internal void Start() => _session.Start();

        /// <summary>Consumes one admitted update, which is a sixtieth of a real second and half a game second.</summary>
        internal void Update()
        {
            _session.Update(Tick(++_step, 1));
        }

        /// <summary>Orders the attack from the panel's own control.</summary>
        internal void Act() => Action("party.attack");

        /// <summary>Asks for a semantic action on the payload contract the companion claims.</summary>
        /// <param name="action">The action name to send.</param>
        internal void Action(string action) => _session.Update(Tick(++_step, 1, Payload($$"""{ "action": "{{action}}" }""")));

        /// <summary>Holds the session, which is what stops the clock releasing anybody.</summary>
        internal void Hold() => _session.Hold();

        /// <summary>Releases the hold.</summary>
        internal void Release() => _session.ReleaseHold();

        /// <summary>Walks the party to a position in the hall, which is world state the fight re-reads.</summary>
        /// <param name="x">Where along the hall's first ground axis the party now stands.</param>
        internal void WalkTo(double x) => _world.ArriveAt(Hall, new PlacePose(x, 0, 0, 0, 0));

        /// <summary>Switches the fight to the other pacing, from the panel's own control.</summary>
        internal void TurnBased() => Action("combat.turn-based");

        /// <inheritdoc />
        public void Dispose()
        {
            _session.Dispose();
            _world.Dispose();
            _party.Dispose();
            _channel.Dispose();
        }
    }

    // ---- fixtures -----------------------------------------------------------------------------------------

    /// <summary>One admitted update of the engine's own shape: a step, and the input it carried.</summary>
    private static ProductUpdate Tick(ulong step, uint admitted, params ProductInputEvent[] input)
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

    /// <summary>One action on the declared contract, in the bytes the DOM companion sends.</summary>
    private static ProductInputEvent Payload(string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        "test.actions"u8.ToArray(), System.Text.Encoding.UTF8.GetBytes(json));

    /// <summary>A party of four, each with the same pools, so a wound is visible in the totals.</summary>
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

        return new PartyEntityFactory().Create(new PartyCreation(members, 0, 0, ProvisionUnits, 0, 0));
    }

    /// <summary>The unit the party's larder is stated in; nothing here is about provisions.</summary>
    private static ProvisionUnit ProvisionUnits => ProvisionUnit.Portions;

    /// <summary>The session's one clock, at this game's own rate, so an admitted update is a stated amount of game time.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>
    /// A world of two places whose hall holds one creature, and which composes the interaction mechanism
    /// because the rules answer for it: a body is reachable there, and that is where the panel counts it.
    /// </summary>
    private static SessionWorld World(PartyEntity party, Rules rules, double creatureAt)
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
                            { "id": "beast", "kind": "creature", "x": {{creatureAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7" } ] },
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
        FacingRule facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
        PartyPoseOwner pose = new(new PartyPose(Hall, new PlacePose(0, 0, 0, 0, 0)), facing);
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
            interaction: new InteractionPolicy(
                rules,
                PlaceSpace.HeightIsThird(facing, radiansAtZeroFacing: 0),
                new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31)),
            schedule: null);
    }

    /// <summary>Walking is free: nothing in these tests is about what a road costs.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }

    /// <summary>
    /// What this suite's fight is paced and resolved by, and what it leaves behind: the numbers are stated
    /// here rather than in the product, which is the seam under test.
    /// </summary>
    private sealed class Rules(CorpseGround ground, int creatureHitPoints)
        : ICombatRule, ICombatResolutionRule, IFallenCreatureObserver, IInteractionRule, ICorpseSource
    {
        /// <summary>The creature's own placement identity, which is also the identity of its body.</summary>
        private const string Creature = "beast";

        public string NameOf(CombatSubject subject) => subject.Member?.Profile.Name ??
            (subject.Placement?.Content.Id == Creature ? "A beast" : "nothing");

        /// <summary>One creature kind, and it attacks on sight within the suite's own notice range.</summary>
        public Hostility NatureOf(CombatSubject subject) => subject.Member is not null
            ? Hostility.Peaceful
            : subject.Placement?.Content.Kind == "creature"
                ? Hostility.Aggressive(NoticeRange)
                : Hostility.Inert;

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) =>
            subject.Member is not null ? MemberRecovery : CreatureRecovery;

        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => RecoveryAfter(subject, kind);

        public double ReachOf(CombatSubject subject, AttackKind kind) => NoticeRange;

        /// <summary>Every attack lands for the suite's own harm, so what is left is arithmetic a test can state.</summary>
        public IAttackRolls? RollsFor(CombatSubject attacker, string key) => new Lowest();

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) => new(
            HitChance.Always,
            new DamageKindId("Phys"),
            DamageRoll.Flat(BlowDamage),
            Resistance.Of(0));

        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls) => damage;

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) => null;

        /// <summary>
        /// A member acts unless the suite's own condition is on them; a creature answers for its own body.
        /// </summary>
        public bool CanAct(CombatSubject subject) => subject.Member is not { } member ||
            !member.Conditions.Active.Any(condition => condition.Condition == LaidOut);

        public int HitPointsOf(CombatSubject subject) => subject.Member is { } member
            ? member.Resources.HitPoints.Maximum
            : creatureHitPoints;

        /// <summary>What this suite's fight read as down, handed to the one owner of the bodies.</summary>
        public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen) =>
            ground.Observe(place, fallen);

        /// <summary>Every body lying in a place, as the placements the mechanism discovers beside content.</summary>
        public IReadOnlyList<PlacementDefinition> CorpsesOf(PlaceId place) =>
            [.. ground.In(place).Select(body => body.Body)];

        /// <summary>This suite's world declares nothing usable, so no placement offers anything.</summary>
        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) => null;

        /// <summary>Nothing here is trapped.</summary>
        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        /// <summary>Nothing here requires anything.</summary>
        public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context) =>
            InteractionRequirementVerdict.Satisfied;

        /// <summary>Nothing usable is ever reached, so no use can apply.</summary>
        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) =>
            InteractionOutcome.Refused("nothing-usable", "This suite's world declares nothing to use.");

        /// <summary>One attack's draws, all of them the lowest the attack allows.</summary>
        private sealed class Lowest : IAttackRolls
        {
            public int Roll(string name, int minimum, int maximum) => minimum;
        }
    }
}
