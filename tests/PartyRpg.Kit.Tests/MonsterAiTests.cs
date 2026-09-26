using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Monsters: the driver that gives the opposition its half of a fight, the policy that decides for it, the
/// state that says who stands where, and the health a creature owns.
/// </summary>
/// <remarks>
/// <para>
/// Every number here is the test's own, which is the point of the seams: the kit holds no aggression, no
/// speed, no chance, and no monster row, so a suite states its own policy and demands that the mechanism
/// serve it. What the mechanism owns — that a creature is driven through the same gate a character is, that
/// a place the party empties stays empty until the clock restores it, that harm lands on the entry a trap
/// uses, and that the same state decides the same way twice — is what these tests are about.
/// </para>
/// <para>
/// The movements here are a stand-in and not a claim about the engine: the engine-backed mover is composed
/// only inside a host with a spatial service, so what a suite can prove is that the driver asks for a step,
/// reads where the creature ended up, and decides the next moment from there. Walking a real creature
/// through real geometry is the live check's business.
/// </para>
/// </remarks>
public sealed class MonsterAiTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Hall = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");

    /// <summary>What one of this suite's attacks costs, so a test can count game time against it.</summary>
    private static readonly GameDuration Swing = GameDuration.FromSeconds(1);

    /// <summary>How far this suite's creatures notice the party.</summary>
    private const double NoticeRange = 500;

    /// <summary>What this suite's creatures can take, which is their placement's own statement.</summary>
    private const int CreatureHitPoints = 10;

    /// <summary>What one of this suite's blows does, which is more than a small member can take.</summary>
    private const int BlowDamage = 10;

    [Fact]
    public void A_creature_spawns_from_a_placement_and_a_cleared_place_stays_cleared_until_the_clock_restores_it()
    {
        using PartyEntity party = Party();
        Days days = new();

        // One creature in the hall, so the place is cleared by bringing it down: a second hostile creature
        // out of the party's sight would still be something the place holds, and the place would rightly stay
        // uncleared while it stood.
        using SessionWorld world = World(party, days, single: true);
        CombatState fight = Fight(world, party, resolving: true);
        Walking walker = new();
        Mind mind = new();
        CombatDirector director = new(fight, mind, walker, world.Places);

        // Walking in populates the place from its placements: the creature exists because content placed one,
        // and the fight reads it as an enemy because its own placement puts it inside its notice range.
        world.ArriveAt(Hall, Pose());
        world.Populate();
        fight.Step();
        Assert.Contains(world.Population.Entities, entity => entity.Content.Kind == "monster");
        Assert.Single(fight.Opposition);
        Assert.False(world.Places.StateOf(Hall).Cleared);

        // It is driven like any actor: the driver decides for it and gives the order through the fight's gate.
        Assert.NotEmpty(director.Step(Hall, 0.5));
        Assert.Equal("attacking", Assert.Single(director.Activity).Action);

        // The party brings it down, and the place the party has emptied is marked cleared: a place state, not
        // a counter in the update, and the creature's live position goes with it.
        Combatant beast = fight.Opposition[0];
        Combatant member = fight.Combatants.First(combatant => combatant.Side == CombatSide.Party);
        for (int swing = 0; swing < 4 && !fight.IsDown(beast); swing++)
        {
            Assert.True(fight.Order(new AttackOrder(member.Id, AttackKind.Melee, beast.Id)).IsApplied);
            fight.Observe(Advance(Swing.Milliseconds));
        }

        Assert.True(fight.IsDown(beast));
        director.Step(Hall, 0.5);
        Assert.True(world.Places.StateOf(Hall).Cleared, $"state={world.Places.StateOf(Hall)} activity={string.Join(",", director.Activity.Select(a => a.Action))} vitals={fight.Vitals(beast)} down={fight.IsDown(beast)}");
        Assert.Equal("down", Assert.Single(director.Activity).Action);

        // A cleared place stays cleared: walking out finds the cave's population, which is nobody, and the
        // hall is still marked as the party left it — the state belongs to the place and not to the visit.
        Assert.True(world.Travel(Assert.Single(world.Graph.TransitionsFrom(Hall)), TransitionKind.Entrance).Arrived);
        world.Populate();
        fight.Step();
        Assert.DoesNotContain(world.Population.Entities, entity => entity.Content.Kind == "monster");
        Assert.False(fight.IsEngaged);
        Assert.True(world.Places.StateOf(Hall).Cleared);
        Assert.Equal(0, world.Places.StateOf(Hall).RespawnCount);

        // Game time is what brings it back: the place's own interval elapses, the world restores it, and the
        // population is rebuilt from the same placements — a fresh creature, not the one that fell.
        days.ElapsedGameDays = 7;
        Assert.NotEmpty(world.AdvanceTime());
        Assert.False(world.Places.StateOf(Hall).Cleared);
        Assert.Equal(1, world.Places.StateOf(Hall).RespawnCount);

        Assert.True(world.Travel(Assert.Single(world.Graph.TransitionsFrom(Cave)), TransitionKind.Entrance).Arrived);
        world.Populate();
        Assert.Contains(world.Population.Entities, entity => entity.Content.Kind == "monster");
        fight.Step();
        Assert.Single(fight.Opposition);
        Assert.Equal(CreatureHitPoints, fight.Vitals(fight.Opposition[0]).Maximum);
    }

    [Fact]
    public void Aggression_decides_at_what_distance_a_creature_notices_the_party()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, new Days());
        CombatState fight = Fight(world, party);
        world.ArriveAt(Hall, Pose());
        world.Populate();
        fight.Step();

        // The placement's own position is the whole of it: the creature a hundred units away is inside its
        // notice range and in the fight, the one four thousand units away is in no fight at all, and the
        // person standing there starts none.
        Assert.Equal(CombatSide.Opposition, fight.Combatants.Single(combatant => combatant.Name == "A beast").Side);
        Assert.Equal(CombatSide.Neutral, fight.Combatants.Single(combatant => combatant.Name == "A beast far off").Side);
        Assert.Equal(CombatSide.Neutral, fight.Combatants.Single(combatant => combatant.Name == "A person").Side);
        Assert.Single(fight.Opposition);

        // A neutral creature is not driven: the driver drives the opposition and nothing else, so the one out
        // of range is left standing where content put it — which is what makes walking up to it a change.
        Walking walker = new();
        CombatDirector director = new(fight, new Mind(), walker, world.Places);
        director.Step(Hall, 0.5);
        CreatureActivity activity = Assert.Single(director.Activity);
        Assert.Equal("A beast", activity.Name);
        Assert.Equal("attacking", activity.Action);

        // It was inside its own reach, so nothing asked the world to move it: the drive is a decision, and a
        // decision to strike is not a step.
        Assert.Equal(0, walker.Moves);
    }

    [Fact]
    public void A_creature_attacks_through_the_same_gate_the_player_uses_and_is_refused_while_recovering()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, new Days());
        CombatState fight = Fight(world, party, resolving: true);
        world.ArriveAt(Hall, Pose());
        world.Populate();
        fight.Step();

        Combatant beast = fight.Opposition[0];
        Combatant member = fight.Combatants.First(combatant => combatant.Side == CombatSide.Party);

        // One gate, whoever drives the actor: the creature's order is judged exactly as a member's is, pays
        // its own recovery, and produces the same record of what was attempted and what came of it.
        CombatResult ordered = fight.Order(new AttackOrder(beast.Id, AttackKind.Melee, member.Id));
        Assert.True(ordered.IsApplied);
        Assert.Equal(Swing, fight.Find(beast.Id)!.Recovery);
        Assert.Equal(beast.Id, fight.LastAttack!.Actor);
        Assert.Equal(member.Id, fight.LastAttack.Target);

        // A recovering creature is refused by name and spends nothing, which is the same refusal a character
        // gets: there is no per-kind pacing and no second cooldown for the other side of a fight.
        CombatResult refused = fight.Order(new AttackOrder(beast.Id, AttackKind.Melee, member.Id));
        Assert.False(refused.IsApplied);
        Assert.Equal("recovering", refused.Code);
        Assert.Equal(Swing, fight.Find(beast.Id)!.Recovery);

        // Game time is what releases it, exactly as it releases a member.
        fight.Observe(Advance(Swing.Milliseconds));
        Assert.True(fight.Order(new AttackOrder(beast.Id, AttackKind.Melee, member.Id)).IsApplied);

        // And a driven creature goes through that same gate: its attack is an initiation and a resolution,
        // not a private path of its own.
        fight.Observe(Advance(Swing.Milliseconds));
        CombatDirector director = new(fight, new Mind(), new Walking(), world.Places);
        director.Step(Hall, 0.5);
        Assert.True(fight.LastOrder!.IsApplied);
        Assert.Equal("attacking", Assert.Single(director.Activity).Action);
        Assert.NotNull(fight.LastResolution);
    }

    [Fact]
    public void A_creatures_blow_lands_on_a_member_through_the_same_health_entry_a_trap_uses()
    {
        using PartyEntity party = Party(health: new CollapseAtEmpty());
        using SessionWorld world = World(party, new Days());
        CombatState fight = Fight(world, party, resolving: true);
        world.ArriveAt(Hall, Pose());
        world.Populate();
        fight.Step();

        // A member of eight hit points takes a blow of ten: the pool stops at empty, the two points past it
        // are the member's own depth, and the condition the party's own health rule leaves is on them.
        PartyMember struck = party.Members[0];
        struck.Resources.SetMaximumHitPoints(8);
        struck.Resources.RestoreAll();
        Combatant beast = fight.Opposition[0];
        Assert.True(fight.Order(new AttackOrder(beast.Id, AttackKind.Melee, fight.Find(CombatantId.Of(struck.Id))!.Id)).IsApplied);

        CharacterWound landed = new(BlowDamage, struck.Resources.HitPoints.Current, struck.Resources.Deficit, null);
        Assert.Equal(0, struck.Resources.HitPoints.Current);
        Assert.Equal(2, struck.Resources.Deficit);
        Assert.Contains(struck.Conditions.Active, condition => condition.Condition == CollapseAtEmpty.LaidOut);

        // The same harm through the entry a trap uses leaves exactly the same state: the fight does not keep
        // a damage record of its own for a member, and a sprung trap and a bite are one path into a person.
        PartyMember trapped = party.Members[1];
        trapped.Resources.SetMaximumHitPoints(8);
        trapped.Resources.RestoreAll();
        CharacterWound sprung = trapped.TakeDamage(BlowDamage);
        Assert.Equal(landed.HitPoints, sprung.HitPoints);
        Assert.Equal(landed.Deficit, sprung.Deficit);
        Assert.Equal(
            struck.Conditions.Active.Select(condition => condition.Condition),
            trapped.Conditions.Active.Select(condition => condition.Condition));
        Assert.False(sprung.Condition is null);
    }

    [Fact]
    public void The_same_state_produces_the_same_decisions_under_a_seeded_random_service()
    {
        // Two identical worlds, two identical policies, and the engine's keyed service answering both: the
        // creatures' decisions, their blows, and the state the fight is left in are the same, so a fight is
        // reproducible from its own content rather than an accident of when an update ran.
        string first = Play();
        string second = Play();
        Assert.Equal(first, second);
        Assert.Contains("attacking", first, StringComparison.Ordinal);

        static string Play()
        {
            using PartyEntity party = Party();
            using SessionWorld world = World(party, new Days());
            CombatState fight = Fight(world, party, resolving: true);
            world.ArriveAt(Hall, Pose());
            world.Populate();
            fight.Step();

            Mind mind = new() { Random = new SeededRandom() };
            CombatDirector director = new(fight, mind, new Walking(), world.Places);
            List<string> lines = [];
            for (int update = 0; update < 4; update++)
            {
                director.Step(Hall, 0.5);
                fight.Observe(Advance(Swing.Milliseconds));
                foreach (CreatureActivity activity in director.Activity)
                {
                    lines.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{update} {activity.Name} {activity.Action} {activity.Target} {activity.Applied}"));
                }

                (int current, int maximum) = fight.Vitals(fight.Combatants.First(combatant => combatant.Side == CombatSide.Party));
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"member {current}/{maximum}"));
            }

            return string.Join('\n', lines);
        }
    }

    [Fact]
    public void A_creature_that_closes_moves_through_the_mover_and_the_fight_reads_where_it_stands()
    {
        using PartyEntity party = Party();
        Walking walker = new();
        using SessionWorld world = World(party, new Days(), beastAt: 900, farBeastAt: 100000, creatures: walker);
        CombatState fight = Fight(world, party, noticeRange: 2000);
        CombatDirector director = new(fight, new Mind { EngageRange = 100 }, walker, world.Places);

        // A world that owns live positions is what the fight reads them through: the creature is at nine
        // hundred units, outside the hundred its policy strikes within, so it closes.
        world.ArriveAt(Hall, Pose());
        world.Populate();
        fight.Step();
        double before = fight.Opposition[0].Distance;
        Assert.Equal(900, before);

        director.Step(Hall, 1.0);
        Assert.Equal("closing", Assert.Single(director.Activity, entry => entry.Name == "A beast").Action);
        Assert.True(walker.Moves > 0);

        // The fight re-reads the world through the same seam, so every distance, notice range, and target it
        // measures is where the creature is now rather than where content placed it.
        fight.Step();
        Assert.True(fight.Opposition[0].Distance < before, $"{fight.Opposition[0].Distance} should be under {before}");

        // Closing far enough brings it into reach, and the next decision is a blow.
        for (int update = 0; update < 20 && fight.Opposition[0].Distance > 100; update++)
        {
            director.Step(Hall, 1.0);
            fight.Step();
        }

        Assert.True(fight.Opposition[0].Distance <= 100);
        director.Step(Hall, 1.0);
        Assert.Equal("attacking", Assert.Single(director.Activity, entry => entry.Name == "A beast").Action);
    }

    [Fact]
    public void A_policy_that_calls_another_creature_an_enemy_turns_the_creature_on_it()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, new Days(), twoKinds: true);
        CombatState fight = Fight(world, party, resolving: true);
        Walking walker = new();

        // Two kinds of creature stand in the place and both are hostile to the party. The policy is what says
        // whether they are each other's enemies: told that they are, a creature goes for the nearer one,
        // which is the second creature rather than the party — the matrix's own answer, made by content, with
        // no table of kinds in the kit.
        Mind mind = new() { Enemies = (self, other) => true };
        CombatDirector director = new(fight, mind, walker, world.Places);
        world.ArriveAt(Hall, Pose());
        world.Populate();
        fight.Step();

        Combatant beast = fight.Combatants.Single(combatant => combatant.Name == "A beast");
        Combatant rival = fight.Combatants.Single(combatant => combatant.Name == "A rival beast");
        Assert.True(rival.Distance < beast.Distance);

        // The creature attacks a creature of its own side's kind: the fight refuses a blow at a member of the
        // party's own side, and a creature is not one, so this is where kinds turn on each other.
        CreatureActivity activity = Assert.Single(director.Step(Hall, 0.5), entry => entry.Creature == beast.Id);
        Assert.Equal("attacking", activity.Action);
        Assert.Equal("A rival beast", activity.Target);
        Assert.True(fight.LastOrder!.IsApplied);
        Assert.Equal(rival.Id, fight.LastAttack!.Target);

        // With the policy answering that nothing is anybody's enemy, the same creature goes for the party.
        Mind alone = new() { Enemies = (_, _) => false };
        CombatDirector other = new(fight, alone, walker, world.Places);
        fight.Observe(Advance(Swing.Milliseconds * 2));
        CreatureActivity partyBlow = other.Step(Hall, 0.5).Single(entry => entry.Creature == beast.Id);
        Assert.Equal("attacking", partyBlow.Action);
        Assert.Equal("Member 1", partyBlow.Target);
    }

    [Fact]
    public void The_projection_publishes_what_the_opposition_is_doing_and_who_acted()
    {
        using PartyEntity party = Party();
        using SessionWorld world = World(party, new Days());
        CombatState fight = Fight(world, party, resolving: true);
        world.ArriveAt(Hall, Pose());
        world.Populate();
        fight.Step();
        CombatDirector director = new(fight, new Mind(), new Walking(), world.Places);
        director.Step(Hall, 0.5);

        // The panel shows the creature as an enemy with what it has left to lose, what it is doing, and
        // whether the last thing that happened was the party's own blow or the creature's.
        CombatSnapshot snapshot = CombatSnapshot.From(fight, director);
        CombatActorSnapshot enemy = Assert.Single(snapshot.Enemies);
        Assert.Equal("A beast", enemy.Name);
        Assert.Equal(CreatureHitPoints, enemy.HitPointsMax);
        Assert.Equal("attacking", enemy.Activity);
        Assert.False(enemy.Down);
        Assert.False(snapshot.ByParty);
        Assert.Equal("A beast", snapshot.Actor);

        // A member's own order is published as the party's, which is how a panel tells a wound it gave from
        // one it took.
        Combatant member = fight.Combatants.First(combatant => combatant.Side == CombatSide.Party);
        fight.Observe(Advance(Swing.Milliseconds));
        Assert.True(fight.Order(new AttackOrder(member.Id, AttackKind.Melee, fight.Opposition[0].Id)).IsApplied);
        Assert.True(CombatSnapshot.From(fight, director).ByParty);

        // A session with no driver publishes no activity at all rather than inventing some.
        Assert.All(CombatSnapshot.From(fight).Enemies, actor => Assert.Equal(string.Empty, actor.Activity));
    }

    /// <summary>One game-time advance, as the session hands it to the mechanisms that keep time.</summary>
    private static ClockAdvance Advance(long milliseconds) => new(
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameDate(1168, 1, 1, 9, 0, 1),
        GameDuration.FromMilliseconds(milliseconds),
        PeriodCrossings.None,
        []);

    private static PlacePose Pose() => new(0, 0, 0, 0, 0);

    /// <summary>The session's one clock, at this game's own rate and on its own calendar.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>A party of four, one of which a blow of ten can take past empty.</summary>
    private static PartyEntity Party(ICharacterHealthRule? health = null)
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

        return new PartyEntityFactory(health: health).Create(new PartyCreation(members, 0, 0, ProvisionUnit.Portions, 0, 0));
    }

    /// <summary>The fight this suite exercises: the kit's state over the test's own rule.</summary>
    private static CombatState Fight(SessionWorld world, PartyEntity party, bool resolving = false, double noticeRange = NoticeRange) =>
        new(resolving ? new Biting(noticeRange) : new TestRule(noticeRange), party, world, Clock());

    /// <summary>
    /// A world of two places: one hall holding a creature inside its notice range, a creature far outside it,
    /// a second kind of creature, a person, and a door, and one cave holding nothing.
    /// </summary>
    private static SessionWorld World(
        PartyEntity party,
        Days days,
        double farBeastAt = 4000,
        double beastAt = 100,
        bool twoKinds = false,
        ICreatureMover? creatures = null,
        bool single = false)
    {
        string rival = twoKinds
            ? $$""", { "id": "rival", "kind": "monster", "x": 50, "y": 0, "z": 0, "monster": "8", "hitPoints": 10 }"""
            : string.Empty;
        string far = single
            ? string.Empty
            : $$"""{ "id": "beast-far", "kind": "monster", "x": {{farBeastAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7", "hitPoints": {{CreatureHitPoints}} },""";
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
                            { "id": "beast", "kind": "monster", "x": {{beastAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7", "hitPoints": {{CreatureHitPoints}} },
                            {{far}}
                            { "id": "person-0", "kind": "person", "x": 200, "y": 0, "z": 0, "people": [ "person-1" ] },
                            { "id": "door-0", "kind": "door", "x": 50, "y": 0, "z": 0, "state": 2 }
                            {{rival}} ] },
                        { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
                      ]
                    }
                    """)
                .Add(
                    "packs/world/links.json",
                    """
                    { "documentId": "links", "definitionKind": "travel-link",
                      "entries": [ { "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" },
                                   { "id": "1", "fromPlace": "2", "toPlace": "1", "entryPoint": "Party Start" } ] }
                    """),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PartyPoseOwner pose = new(new PartyPose(Hall, Pose()), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        return new SessionWorld(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            days,
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: Clock(),
            resources: null,
            partyEntity: party,
            interaction: null,
            schedule: null,
            creatures: creatures);
    }

    /// <summary>The days the world has reached, which is what a respawn interval is measured against.</summary>
    private sealed class Days : IWorldTimeSource
    {
        public int ElapsedGameDays { get; set; }
    }

    /// <summary>Walking is free: nothing in these tests is about what a road costs.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }

    /// <summary>
    /// The movements of a creature, as a world with no engine can offer them: the step is a straight line
    /// towards or away from the target, and where the creature ended up is what the fight then reads.
    /// </summary>
    private sealed class Walking : ICreatureMover
    {
        private readonly Dictionary<CombatantId, PlacePose> _poses = [];

        /// <summary>How many steps were asked for, so a test can tell a decision from a movement.</summary>
        internal int Moves { get; private set; }

        /// <summary>How far a creature walks per second of admitted time.</summary>
        internal double Speed { get; set; } = 400;

        /// <summary>
        /// Where a creature stands, or null when nothing has moved it: a missing entry is not a position at
        /// the origin, and a mover that answered one would have every creature standing on top of the party.
        /// </summary>
        public PlacePose? PoseOf(CombatantId creature) =>
            _poses.TryGetValue(creature, out PlacePose pose) ? pose : null;

        public CreatureMoveOutcome Move(CreatureMoveRequest request)
        {
            Moves++;
            PlacePose from = _poses.GetValueOrDefault(request.Creature, request.From);
            double x = request.TargetPose.X - from.X;
            double y = request.TargetPose.Y - from.Y;
            double z = request.TargetPose.Z - from.Z;
            double distance = Math.Sqrt((x * x) + (y * y) + (z * z));
            double step = Math.Min(distance, Speed * request.ElapsedSeconds);
            int sign = request.Purpose == CreatureMovePurpose.Toward ? 1 : -1;
            PlacePose to = distance <= 0 || step <= 0
                ? from
                : new PlacePose(
                    from.X + (sign * x / distance * step),
                    from.Y + (sign * y / distance * step),
                    from.Z + (sign * z / distance * step),
                    from.Yaw,
                    from.Pitch);
            _poses[request.Creature] = to;
            return new CreatureMoveOutcome(step > 0, to, step, Grounded: true);
        }

        public void Forget(CombatantId creature) => _poses.Remove(creature);

        public void ForgetAll() => _poses.Clear();
    }

    /// <summary>
    /// A policy of this suite's own: it closes and strikes within a range it states, and answers who is
    /// whose enemy from whatever the test told it.
    /// </summary>
    private sealed class Mind : IMonsterAiPolicy
    {
        /// <summary>Whether two creatures are each other's enemies, as the test states it.</summary>
        internal Func<CombatSubject, CombatSubject, bool> Enemies { get; set; } = (_, _) => false;

        /// <summary>How far the creature strikes from, so a test can watch it close first.</summary>
        internal double EngageRange { get; set; } = NoticeRange;

        /// <summary>The engine's randomness, which a decision here draws a chance from.</summary>
        internal IRandomService? Random { get; set; }


        public bool AreEnemies(CombatSubject self, CombatSubject other) => Enemies(self, other);

        public double SpeedOf(CombatSubject subject) => 400;

        public CreatureDecision Decide(CreatureSituation situation)
        {
            CreatureCandidate? target = situation.NearestEnemy;
            if (target is not { } chosen) return CreatureDecision.Wait;

            // The one chance this policy takes: a draw keyed by the creature and its own round, which is what
            // makes two identical fights decide identically. It stands in for a game's own spell or second
            // attack chance, so the kit's claim that a decision is reproducible is exercised end to end.
            if (Random is { } random &&
                random.DrawKeyed(new KeyedRngRequest(1, "test.ai", $"{situation.Self.Subject.Place}/{situation.Self.Id}/{situation.Round}", 0, 99)).Value < 50)
            {
                return CreatureDecision.Advance(chosen.Actor.Id);
            }

            if (situation.Ready && chosen.Distance <= EngageRange)
            {
                return CreatureDecision.Attack(chosen.Actor.Id, AttackKind.Melee, "swing");
            }

            return CreatureDecision.Advance(chosen.Actor.Id);
        }
    }

    /// <summary>
    /// The rules this suite's fight is paced by: a creature is a placement of the creature kind, its own
    /// health is the placement's statement, and everything the kit asks about an actor is answered here.
    /// </summary>
    private class TestRule(double noticeRange) : ICombatRule
    {
        public string NameOf(CombatSubject subject)
        {
            if (subject.Member is { } member) return member.Profile.Name;
            return subject.Placement?.Content.Id switch
            {
                "rival" => "A rival beast",
                "beast" => "A beast",
                "beast-far" => "A beast far off",
                _ => subject.Placement?.Content.Kind == "person" ? "A person" : "A door",
            };
        }

        /// <summary>
        /// What each actor is by nature: a creature starts fights as far off as this suite says, and a person
        /// starts none and can still be attacked, which is what makes the party's own act the thing that puts
        /// somebody into a fight.
        /// </summary>
        public Hostility NatureOf(CombatSubject subject) => subject.Member is not null
            ? Hostility.Peaceful
            : IsCreature(subject)
                ? Hostility.Aggressive(noticeRange)
                : subject.Placement?.Content.Kind == "person"
                    ? Hostility.Peaceful
                    : Hostility.Inert;

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) => Swing;

        /// <summary>Nothing waits: a creature in this suite acts the moment the fight reads it.</summary>
        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => GameDuration.None;

        public double ReachOf(CombatSubject subject, AttackKind kind) => kind == AttackKind.Melee ? NoticeRange : 5120;

        private protected static bool IsCreature(CombatSubject subject) =>
            subject.Placement?.Content.Kind == "monster";
    }

    /// <summary>A fight that resolves: a creature's blow lands for a stated amount of harm.</summary>
    private sealed class Biting(double noticeRange) : TestRule(noticeRange), ICombatResolutionRule
    {
        /// <summary>
        /// The attack's rolls, answered by this suite rather than by an engine service: a fight that cannot
        /// draw resolves nothing, so a suite that wants a blow to land states the draws it lands with.
        /// </summary>
        public IAttackRolls? RollsFor(CombatSubject attacker, string key) => new Scripted();

        /// <summary>One attack's draws, all of them the same number.</summary>
        private sealed class Scripted : IAttackRolls
        {
            public int Roll(string name, int minimum, int maximum) => minimum;
        }

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) => new(
            HitChance.Always,
            new DamageKindId("Phys"),
            DamageRoll.Flat(BlowDamage),
            Resistance.Of(0));

        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls) => damage;

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) => null;

        public bool CanAct(CombatSubject subject) => true;

        /// <summary>What a creature can take: its own placement's statement, which is content's.</summary>
        public int HitPointsOf(CombatSubject subject) => subject.Member is { } member
            ? member.Resources.HitPoints.Maximum
            : subject.Placement?.Source.GetInt32("hitPoints") ?? 0;
    }

    /// <summary>
    /// The party's own answer about a wound that empties a character: they are laid out, which is what makes
    /// the health entry a creature's blow arrives at observable rather than a number that moved.
    /// </summary>
    private sealed class CollapseAtEmpty : ICharacterHealthRule
    {
        internal static readonly ConditionId LaidOut = new("Laid Out");

        public CharacterCollapse Collapse(PartyMember member, int hitPoints, int deficit) =>
            hitPoints > 0 ? CharacterCollapse.None : new CharacterCollapse(new ActiveCondition(LaidOut, 1), []);
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
