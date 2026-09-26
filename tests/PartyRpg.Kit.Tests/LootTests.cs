using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What a kill leaves and what searching it gives: the corpse a fight reports, the loot a table generates
/// under a key, and the transfer into the party's one shared pack.
/// </summary>
/// <remarks>
/// <para>
/// The rule below is this suite's own game: it decides what a creature is, what a death holds, and what a
/// body offers. What the kit is being asked about is everything between those answers — that a fight reports
/// what it read as down and where it fell, that a body reaches the reticle through the same mechanism a
/// chest does, that generation is drawn under a key so the same death yields the same loot, and that a pack
/// which cannot take what a body holds refuses the whole search rather than half of it.
/// </para>
/// <para>
/// Every number here is the test's, which is why the loot table it generates from is stated in the test
/// rather than taken from anywhere: the kit's part is the draw, the weighting, and the transfer.
/// </para>
/// </remarks>
public sealed class LootTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId DenPlace = new("9");
    private const int CreatureHitPoints = 5;
    private const int CreatureDamage = 5;
    private const ulong LootSeed = 0x1007UL;

    [Fact]
    public void A_kill_leaves_a_corpse_where_the_creature_fell_and_not_where_content_placed_it()
    {
        // The creature walked to the party before it died, so its body lies at the position the fight read
        // rather than at the placement's own: a fight that reported the placement would put the body back
        // where the creature started.
        using Den den = Den.Build();
        den.CreatureMovedTo(new PlacePose(260, 40, 0, 1536, 0));

        den.Kill();

        // One body, at the position it fell, named for what it was.
        Corpse body = Assert.Single(den.Corpses.In(DenPlace));
        Assert.Equal(new PlacementContentId("monster", "beast"), body.Content);
        Assert.Equal(260, body.Pose.X, 3);
        Assert.Equal(40, body.Pose.Y, 3);
        Assert.Equal("A beast", body.Name);

        // And the mechanism finds it lying there, through the same discovery a chest goes through.
        den.Interaction.Update();
        Assert.Single(den.Interaction.Bodies);
        Assert.Equal("The body of A beast", den.Interaction.FocusedTarget?.Definition.Name);
        Assert.Equal(InteractionVerb.Search, den.Interaction.FocusedTarget?.Verb);
    }

    [Fact]
    public void Searching_a_body_takes_what_the_death_left_into_the_shared_pack_and_purse()
    {
        using Den den = Den.Build();
        den.Kill();

        // What the death left is decided when it falls, not when it is searched: the body already holds it.
        Corpse body = Assert.Single(den.Corpses.In(DenPlace));
        LootYield held = Assert.IsType<LootYield>(den.Corpses.Held(body));
        Assert.True(held.Coins > 0);
        Assert.Single(held.Items);

        InteractionResult searched = den.Use();
        Assert.True(searched.IsApplied);
        Assert.Equal("searched", searched.State);

        // The items are in the party's one pack, and the coin in its one purse: there is nowhere else for
        // either to have gone.
        Assert.Single(den.Party.Inventory.Items);
        Assert.Equal(held.Items[0].Definition, den.Party.Inventory.Items[0].Definition);
        Assert.Equal(held.Coins, den.Party.Purse.Coins);

        // The report names what was found rather than counting it.
        Assert.Contains(held.Items[0].Definition.Value, searched.Message, StringComparison.Ordinal);
        Assert.Contains($"{held.Coins} gold", searched.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_body_a_pack_cannot_take_is_refused_whole_and_still_holds_everything_it_did()
    {
        // A pack that takes nothing: the transfer is judged before anything moves, so the party ends up with
        // neither the item nor the coin rather than with half of what the body held.
        using Den den = Den.Build(capacity: 0);
        den.Kill();
        Corpse body = Assert.Single(den.Corpses.In(DenPlace));
        LootYield held = Assert.IsType<LootYield>(den.Corpses.Held(body));

        InteractionResult refused = den.Use();
        Assert.False(refused.IsApplied);
        Assert.Equal("pack-full", refused.Code);
        Assert.Empty(den.Party.Inventory.Items);
        Assert.Equal(0, den.Party.Purse.Coins);

        // Nothing was taken, so nothing was marked as taken: the body reads as it did, and it still holds
        // exactly what the death left, which is what makes the retry a retry rather than a reroll.
        Assert.Equal(string.Empty, den.Interaction.FocusedTarget?.State.State);
        Assert.Equal(held, den.Corpses.Held(body));

        // The same body searched by a party with room gives what the first attempt would have.
        using Den roomy = Den.Build();
        roomy.Kill();
        roomy.Use();
        Assert.Single(roomy.Party.Inventory.Items);
        Assert.Equal(held.Items[0].Definition, roomy.Party.Inventory.Items[0].Definition);
    }

    [Fact]
    public void A_corpses_loot_is_generated_once_and_a_second_search_gives_nothing_more()
    {
        using Den den = Den.Build();
        den.Kill();

        Assert.True(den.Use().IsApplied);
        int drawn = den.Generations;
        Assert.Equal(1, drawn);

        // The second search is refused by name before anything is generated: the loot a death left is
        // decided once, and the party's own state word is what says the body has been emptied.
        InteractionResult again = den.Use();
        Assert.False(again.IsApplied);
        Assert.Equal("container-emptied", again.Code);
        Assert.Equal(drawn, den.Generations);
        Assert.Single(den.Party.Inventory.Items);

        // Reading the place again — which every update does — does not generate a second lot either.
        for (int update = 0; update < 3; update++) den.Fight.Step();
        Assert.Equal(drawn, den.Generations);
    }

    [Fact]
    public void The_same_kill_under_the_same_seed_leaves_the_same_loot()
    {
        // Two sessions built the same way, each with its own keyed service seeded from the same key: the
        // same death draws the same values, so the loot is a fact about the death rather than about when it
        // was looked at.
        using Den first = Den.Build();
        using Den second = Den.Build();
        first.Kill();
        second.Kill();

        LootYield one = Assert.IsType<LootYield>(first.Corpses.Held(Assert.Single(first.Corpses.In(DenPlace))));
        LootYield two = Assert.IsType<LootYield>(second.Corpses.Held(Assert.Single(second.Corpses.In(DenPlace))));
        Assert.Equal(one.Coins, two.Coins);
        Assert.Equal(one.Items, two.Items);

        // And the draws were actually taken: a run that generated nothing would compare equal for the wrong
        // reason.
        Assert.True(first.Draws > 0);
    }

    [Fact]
    public void A_corpse_lasts_for_the_visit_and_goes_when_the_creature_stands_again()
    {
        // The stated rule, both halves of it: leaving the place, and the place being rebuilt under the
        // party. A body is the creature's own, so it lasts exactly as long as the fight still reads the
        // creature as down.
        using Den den = Den.Build();
        den.Kill();
        Assert.Single(den.Corpses.In(DenPlace));

        // The party walks out: the next reading is of another place, and the body belongs to the visit that
        // made it.
        den.LeaveTo(new PlaceId("10"));
        den.Fight.Step();
        Assert.Empty(den.Corpses.In(DenPlace));

        // Back in the place the creature stands again — content rebuilds a place from its placements — and
        // no body lies where one did.
        den.LeaveTo(DenPlace);
        den.Fight.Step();
        Assert.Empty(den.Corpses.In(DenPlace));

        // Killing it again leaves a fresh body with its own loot: the serial names the death, so the state
        // the party left on the first body is not read as the second's.
        den.Kill();
        Corpse second = Assert.Single(den.Corpses.In(DenPlace));
        Assert.NotNull(den.Corpses.Held(second));
    }

    [Fact]
    public void A_creature_that_stands_again_leaves_no_body_behind()
    {
        // The place is rebuilt under the party: the population is fresh, every creature is standing, and a
        // ground that kept what it read before would leave a corpse where a living creature walks.
        using Den den = Den.Build();
        den.Kill();
        Corpse body = Assert.Single(den.Corpses.In(DenPlace));

        den.RebuildPopulation();
        den.Fight.Step();

        // The creature is standing again with the health a rebuilt population has, and nothing lies at the
        // placement it fell at: what the death left went with the body.
        Assert.Empty(den.Corpses.In(DenPlace));
        Assert.Null(den.Corpses.Held(body));
    }

    [Fact]
    public void A_treasure_level_draws_by_content_weight_and_only_what_the_request_accepts()
    {
        // The table is content's own pool: an item no level weighs is never drawn, a request that names a
        // kind gets that kind, and a request for anything gets the whole pool. The draws are the rolls'
        // business, so the same key picks the same thing twice.
        LootTable table = new(
        [
            new LootCandidate(new ItemDefinitionId("sword"), [10, 0], "single-handed", "sword"),
            new LootCandidate(new ItemDefinitionId("cloak"), [0, 10], "cloak", string.Empty),
        ]);

        LootRolls rolls = new(new KeyedRandom(11UL), seed: 7, scope: "test.loot", key: "a-chest/0");

        // One item weighs at each level, so each level's pool is exactly the one that is weighed there.
        Assert.Equal(10, table.WeightAt(1, LootFilter.Any));
        Assert.Equal(10, table.WeightAt(2, LootFilter.Any));
        Assert.Equal(0, table.WeightAt(3, LootFilter.Any));
        Assert.Equal("sword", table.Pick(1, LootFilter.Any, rolls)?.Definition.Value);
        Assert.Equal("cloak", table.Pick(2, LootFilter.Any, rolls)?.Definition.Value);
        Assert.Null(table.Pick(3, LootFilter.Any, rolls));

        // A request narrows the pool to what it accepts, and a level it empties answers nothing rather than
        // quietly widening to the whole table.
        Assert.Equal("sword", table.Pick(1, new LootFilter("single-handed", string.Empty), rolls)?.Definition.Value);
        Assert.Equal("sword", table.Pick(1, new LootFilter(string.Empty, "sword"), rolls)?.Definition.Value);
        Assert.Null(table.Pick(1, new LootFilter("cloak", string.Empty), rolls));
        Assert.Null(table.Pick(2, new LootFilter("single-handed", string.Empty), rolls));
    }

    [Fact]
    public void Two_draws_of_one_generation_are_different_and_one_key_is_not()
    {
        // A purpose of its own per draw, and the same purpose is the same value: that is the whole of what
        // makes generation reproducible without anything being recorded.
        LootRolls rolls = new(new KeyedRandom(3UL), seed: 5, scope: "test.loot", key: "death/1");
        LootRolls same = new(new KeyedRandom(3UL), seed: 5, scope: "test.loot", key: "death/1");

        Assert.Equal(rolls.Between(1, 1000), same.Between(1, 1000));
        Assert.Equal(rolls.Dice(3, 6), same.Dice(3, 6));

        // A repeated rule draws under a name of its own, so five findings are five draws rather than one
        // repeated five times.
        int[] findings = [.. Enumerable.Range(0, 8).Select(index => rolls.Under($"finding/{index}").Between(1, 1000))];
        Assert.True(findings.Distinct().Count() > 1, "eight findings of one generation drew one value");

        // A different key is a different generation.
        LootRolls other = new(new KeyedRandom(3UL), seed: 5, scope: "test.loot", key: "death/2");
        Assert.NotEqual(rolls.Between(1, 100000), other.Between(1, 100000));
    }

    [Fact]
    public void A_body_past_the_partys_reach_is_not_something_it_faces()
    {
        // The corpse is discovered from the place and the party's own pose, exactly as every other target
        // is: a body it has walked away from is not in front of it, and the refusal names that rather than
        // searching something across the room.
        using Den den = Den.Build();
        den.Kill();
        Assert.True(den.Use().IsApplied);

        den.Move(new PlacePose(0, 4000, 0, 1536, 0));
        den.Interaction.Update();
        Assert.Null(den.Interaction.FocusedTarget);
        Assert.Equal("interaction-no-target", den.Use().Code);
    }

    /// <summary>The engine's keyed randomness, answered deterministically without the engine.</summary>
    private sealed class KeyedRandom(ulong seed) : IRandomService
    {
        /// <summary>How many draws were taken, so a test can tell a roll from a guess.</summary>
        internal int Draws { get; private set; }

        /// <inheritdoc />
        public KeyedRngReceipt DrawKeyed(KeyedRngRequest request)
        {
            Draws++;
            ulong hash = 14695981039346656037UL ^ seed;
            foreach (byte value in System.Text.Encoding.UTF8.GetBytes($"{request.Scope}|{request.Key}"))
            {
                hash = (hash ^ value) * 1099511628211UL;
            }

            long span = request.Maximum - request.Minimum + 1;
            return new KeyedRngReceipt(request.Minimum + (long)(hash % (ulong)span));
        }

        /// <inheritdoc />
        public Lcg15Receipt DrawLcg15(Lcg15Request request) => throw new NotSupportedException("Keyed draws only.");

        /// <inheritdoc />
        public Rng CreateScoped(ScopedRngCreateRequest request) => throw new NotSupportedException("Keyed draws only.");

        /// <inheritdoc />
        public Rng ForkScoped(ScopedRngForkRequest request) => throw new NotSupportedException("Keyed draws only.");

        /// <inheritdoc />
        public RngValue NextU64(Rng stream) => throw new NotSupportedException("Keyed draws only.");

        /// <inheritdoc />
        public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => throw new NotSupportedException("Keyed draws only.");

        /// <inheritdoc />
        public RngValue NextBool(Rng stream) => throw new NotSupportedException("Keyed draws only.");
    }

    /// <summary>A mover that reports one live position, which is how a creature that closed on the party lies
    /// somewhere other than where content placed it.</summary>
    private sealed class MovedCreature(PlacePose? pose) : ICreatureMover
    {
        private PlacePose? _pose = pose;

        internal void Move(PlacePose? to) => _pose = to;

        public PlacePose? PoseOf(CombatantId creature) => _pose;

        public CreatureMoveOutcome Move(CreatureMoveRequest request) => CreatureMoveOutcome.Still(request.From);

        public void Forget(CombatantId creature) => _pose = null;

        public void ForgetAll() => _pose = null;
    }

    /// <summary>
    /// This suite's game: one creature worth one blow, and a death that leaves a stated treasure.
    /// </summary>
    /// <remarks>
    /// The corpse machinery is real — a <see cref="CorpseGround"/>, the fight's own report, and a
    /// <see cref="LootTable"/> drawn through the keyed service — so what is being proved is the kit's
    /// mechanism with this test's numbers rather than a stub of either half.
    /// </remarks>
    private sealed class Rules : ICombatRule, ICombatResolutionRule, IFallenCreatureObserver, ICorpseSource, IInteractionRule
    {
        private readonly KeyedRandom _random;
        private readonly LootTable _table;

        internal Rules(KeyedRandom random)
        {
            _random = random;
            Ground = new CorpseGround();
            _table = new LootTable(
            [
                new LootCandidate(new ItemDefinitionId("sword"), [10], "single-handed", "sword"),
                new LootCandidate(new ItemDefinitionId("cloak"), [1], "cloak", string.Empty),
            ]);
        }

        internal CorpseGround Ground { get; }

        internal int Generations { get; private set; }

        internal int Draws => _random.Draws;

        /// <summary>What one death leaves: two six-sided dice of coin and one item of the first level.</summary>
        private static readonly TreasureRoll Death = new(100, 2, 6, 1, LootFilter.Any);

        public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen)
        {
            IReadOnlyList<Corpse> bodies = Ground.Observe(place, fallen);
            foreach (Corpse body in bodies)
            {
                if (Ground.Held(body) is not null) continue;
                LootRolls rolls = new(_random, seed: LootSeed, scope: "test.loot", key: $"death/{place}/{body.Content}/{body.Serial}");
                Generations++;
                int coins = rolls.Dice(Death.GoldRolls, Death.GoldSides);
                LootCandidate picked = Assert.IsType<LootCandidate>(_table.Pick(Death.Level, Death.Filter, rolls));
                Ground.Hold(body, new LootYield([new LootItem(picked.Definition)], coins));
            }

            return bodies;
        }

        public IReadOnlyList<PlacementDefinition> CorpsesOf(PlaceId place) =>
            [.. Ground.In(place).Select(body => body.Body)];

        public string NameOf(CombatSubject subject) => subject.Member?.Profile.Name ?? "A beast";

        public Hostility NatureOf(CombatSubject subject) =>
            subject.Member is not null ? Hostility.Peaceful
            : subject.Placement?.Content.Kind == "monster" ? Hostility.Aggressive(2000)
            : Hostility.Inert;

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        // Nothing in this den owes recovery, so a test can order the party to act as often as it likes: what
        // is being proved here is the corpse, not the pacing.
        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) => GameDuration.None;

        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => GameDuration.None;

        public double ReachOf(CombatSubject subject, AttackKind kind) => 512;

        public IAttackRolls? RollsFor(CombatSubject attacker, string key) =>
            new AttackRolls(_random, seed: 1, scope: "test.attack", key);

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) => new(
            HitChance.Always,
            new DamageKindId("physical"),
            attacker.Member is not null ? DamageRoll.Flat(CreatureDamage) : DamageRoll.Flat(1),
            Resistance.Of(0));

        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls) => damage;

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) => null;

        public bool CanAct(CombatSubject subject) => true;

        public int HitPointsOf(CombatSubject subject) => CreatureHitPoints;

        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) =>
            Ground.At(request.Place, request.Placement.Content) is { } body
                ? new InteractionTargetDefinition(
                    new InteractionTargetKind("container"),
                    $"The body of {body.Name}",
                    InteractionVerb.Search,
                    reach: 512,
                    request.State)
                : null;

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context) =>
            InteractionRequirementVerdict.Satisfied;

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context)
        {
            if (Ground.At(context.Place, context.Placement.Content) is not { } body) return InteractionOutcome.Refused("corpse-gone", "Nothing lies there.");
            if (target.State == "searched") return InteractionOutcome.Refused("container-emptied", $"{target.Name} has already been emptied.");
            if (Ground.Held(body) is not { } held || held.IsEmpty) return InteractionOutcome.Applied("searched", $"{target.Name} holds nothing.");

            return InteractionOutcome.Applied(
                "searched",
                $"{target.Name} holds {string.Join(" and ", held.Items.Select(item => item.Definition.Value))} and {held.Coins} gold.",
                items: [.. held.Items.Select(item => new InteractionItemYield(item.Definition, item.Count))],
                gain: PartyCost.OfGold(held.Coins));
        }
    }

    /// <summary>A pack that takes what a test allows and refuses the rest by name.</summary>
    private sealed class PackLimit(int maximum) : IInventoryCapacityRule
    {
        public PartyRefusal? Judge(IReadOnlyList<ItemInstance> held, ItemDefinitionId definition, int count) =>
            held.Count + count > maximum
                ? new PartyRefusal("pack-full", $"The pack holds {held.Count} and takes at most {maximum}.")
                : null;
    }

    /// <summary>The place these tests kill and search in: one creature, one party, and one fight.</summary>
    private sealed class Den : IDisposable
    {
        private readonly PartyEntity _party;
        private readonly MovedCreature _mover;
        private readonly MovingTime _time;

        private Den(SessionWorld world, PartyEntity party, CombatState fight, Rules rules, MovedCreature mover, MovingTime time) =>
            (World, _party, Fight, Rules, _mover, _time) = (world, party, fight, rules, mover, time);

        internal SessionWorld World { get; }

        internal CombatState Fight { get; }

        internal Rules Rules { get; }

        internal PartyEntity Party => _party;

        internal CorpseGround Corpses => Rules.Ground;

        internal int Generations => Rules.Generations;

        internal int Draws => Rules.Draws;

        internal PartyInteraction Interaction => World.Interaction ?? throw new InvalidOperationException("This den was built without an interaction policy.");

        /// <summary>Brings the creature down, and reads the place again as the next update would.</summary>
        /// <remarks>
        /// The fight reads the place at the start of an update and the party's order comes after it, so a
        /// body is published by the reading that follows the blow. That is one update of latency in the
        /// product too, and stating it here keeps every test honest about which reading it is looking at.
        /// </remarks>
        internal void Kill()
        {
            Fight.Step();
            Fight.Engage();
            Fight.Step();
        }

        internal void Move(PlacePose pose) => World.ArriveAt(DenPlace, pose);

        /// <summary>Walks the party into another place, and populates it as the session's own update does.</summary>
        internal void LeaveTo(PlaceId place)
        {
            World.ArriveAt(place, new PlacePose(0, 0, 0, 1536, 0));
            World.Populate();
        }

        internal void CreatureMovedTo(PlacePose pose) => _mover.Move(pose);

        /// <summary>Lets the place's own interval elapse, which is what rebuilds its population from content.</summary>
        internal void RebuildPopulation()
        {
            _time.Days = 30;
            World.AdvanceTime();
        }

        /// <summary>Uses what the party faces through the world's own admitted use.</summary>
        internal InteractionResult Use() =>
            World.Interact(use: true) ?? throw new InvalidOperationException("This den was built without an interaction policy.");

        internal static Den Build(int capacity = int.MaxValue)
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new InMemoryContentSource()
                    .Add("packs/world/pack.json", Manifest())
                    .Add("packs/world/places.json", Document("places", "place", Place(), Place("10"))),
                Layout).RequireValid();

            PlaceGraph graph = PlaceGraphLoader.Load(catalog);
            PartyEntity party = NewParty(capacity);
            PartyPoseOwner owner = new(
                new PartyPose(DenPlace, new PlacePose(0, 0, 0, 1536, 0)),
                new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
            KeyedRandom random = new(41UL);
            Rules rules = new(random);
            MovedCreature mover = new(pose: null);
            PlaceStateLedger places = new(graph, PlaceRespawnRule.FromContent());
            MovingTime time = new();
            SessionWorld world = new(
                graph,
                owner,
                places,
                new TestCostRule(),
                time: time,
                mover: null,
                resources: new PartyResourceLedger(party),
                partyEntity: party,
                interaction: new InteractionPolicy(
                    rules,
                    PlaceSpace.HeightIsThird(new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512), radiansAtZeroFacing: 0),
                    new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31)),
                creatures: mover);

            // The party's place is populated exactly as the session populates it on the first update after
            // arriving, so the fight reads the creatures that are actually standing there.
            world.Populate();

            // The fight is composed over the same world the interaction mechanism reads, exactly as the
            // session composes it, so a kill and a search are two halves of one world rather than two.
            return new Den(world, party, new CombatState(rules, party, world), rules, mover, time);
        }

        public void Dispose()
        {
            World.Dispose();
            _party.Dispose();
        }

        private static PartyEntity NewParty(int capacity)
        {
            MemberCreation member = new(new PartyMemberSeed(
                "Tester",
                new RaceId("testfolk"),
                new ClassId("fighter"),
                [new AttributeScore(new AttributeId("vigour"), 12)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(20),
                spellPoints: ResourcePool.Full(5)));
            return new PartyEntityFactory(inventoryCapacity: capacity == int.MaxValue ? null : new PackLimit(capacity)).Create(
                new PartyCreation(
                    [member],
                    coins: 0,
                    foodPortions: 4,
                    ProvisionUnit.Portions,
                    reputation: 0,
                    fame: 0));
        }

        /// <summary>The place the party stands in, with one creature a hundred units in front of it.</summary>
        private static string Place() =>
            """
            { "id": "9", "kind": "interior", "name": "Den", "respawnDays": 3,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "beast", "kind": "monster", "monster": 4, "x": 100, "y": 0, "z": 0, "yaw": 0 } ] }
            """;

        /// <summary>A second place, so a party can leave the one it killed in.</summary>
        private static string Place(string id) =>
            $$"""
            { "id": "{{id}}", "kind": "interior", "name": "Room {{id}}", "respawnDays": 3,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [] }
            """;

        private static string Manifest() =>
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "test content" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" }
              ]
            }
            """;

        private static string Document(string documentId, string definitionKind, params string[] entries) =>
            $$"""
            { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
            """;
    }

    /// <summary>A time source the world reads its days from, which a test moves to let an interval elapse.</summary>
    private sealed class MovingTime : IWorldTimeSource
    {
        internal int Days { get; set; }

        public int ElapsedGameDays => Days;
    }

    /// <summary>Nothing in this den costs anything to reach.</summary>
    private sealed class TestCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}
