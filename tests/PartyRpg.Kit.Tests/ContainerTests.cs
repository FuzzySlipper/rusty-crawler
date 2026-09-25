using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Xunit;
using InteractionReason = Rusty.Engine.Interaction.InteractionReason;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Containers, and the three things between the party and what one holds: the lock on it, the trap in it,
/// and the pack that has to take what it gives.
/// </summary>
/// <remarks>
/// The rule below is this suite's own game: it decides what a container's contents are, what a trap's
/// numbers are, and what a lock needs. The kit is what notices a trap before defeating it, spends a
/// sprung one on the members' own resources, moves what a search found through the party's acquisition
/// path, and refuses a transfer that does not fit — which is exactly what these tests are about, and why
/// none of the numbers here come from the kit.
/// </remarks>
public sealed class ContainerTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId CellarPlace = new("7");
    private const double StepSeconds = 1.0 / 60.0;

    [Fact]
    public void A_trap_the_party_does_not_notice_costs_them_before_the_container_opens()
    {
        // The party has no sense for traps, so the trap in this chest is never noticed: searching sets it
        // off, the members pay for it, and what the chest holds waits for a second attempt.
        ContainerRule rule = new()
        {
            Armed = Armed(detect: 4, disarm: 8, harm: 6),
            Contents = [new InteractionItemYield(new ItemDefinitionId("brass-lamp"))],
        };

        using PartyEntity party = Party(perception: 0, disarmTraps: 0, hitPoints: 20);
        using Cellar cellar = Cellar.Build(rule, party);
        cellar.Interaction.Update();

        // What the party faces is the container, and the act it offers is the search: nothing has told them
        // there is a trap in it yet.
        Assert.Equal(InteractionVerb.Search, cellar.Interaction.FocusedTarget?.Verb);

        InteractionResult sprung = cellar.Interaction.Use();
        Assert.True(sprung.IsApplied);
        Assert.Equal("sprung", sprung.State);
        Assert.Contains("goes off", sprung.Message, StringComparison.Ordinal);
        Assert.Contains("3 member(s) take 6 damage each", sprung.Message, StringComparison.Ordinal);
        Assert.Contains("still there", sprung.Residue, StringComparison.Ordinal);

        // The consequence is a change in the world and not only a sentence: every member lost what the
        // trap stated, through their own resources.
        foreach (PartyMember member in party.Members) Assert.Equal(14, member.Resources.HitPoints.Current);

        // A trap that went off did not also hand over what it was guarding, and it did not say the search
        // happened: nothing entered the pack, and the target reads as sprung.
        Assert.Empty(party.Inventory.Items);
        Assert.Equal("sprung", cellar.Interaction.FocusedTarget?.State.State);

        // The second attempt is an ordinary search: the trap has spent itself.
        InteractionResult searched = cellar.Interaction.Use();
        Assert.True(searched.IsApplied);
        Assert.Equal("searched", searched.State);
        Assert.Contains("party takes", searched.Message, StringComparison.Ordinal);
        Assert.Single(party.Inventory.Items);
        Assert.Equal(new ItemDefinitionId("brass-lamp"), party.Inventory.Items[0].Definition);

        // And nothing else was taken along the way.
        foreach (PartyMember member in party.Members) Assert.Equal(14, member.Resources.HitPoints.Current);
    }

    [Fact]
    public void A_trap_the_party_notices_is_defeated_before_the_container_is_opened()
    {
        // The same chest, searched by a party that can see the trap and knows how to defeat it: the search
        // finds the trap instead of the contents, the next act is the disarm attempt, and the loot follows
        // with nobody hurt.
        ContainerRule rule = new()
        {
            Armed = Armed(detect: 4, disarm: 8, harm: 6),
            Contents = [new InteractionItemYield(new ItemDefinitionId("brass-lamp"))],
        };

        using PartyEntity party = Party(perception: 6, disarmTraps: 9, hitPoints: 20);
        using Cellar cellar = Cellar.Build(rule, party);
        cellar.Interaction.Update();

        InteractionResult noticed = cellar.Interaction.Use();
        Assert.True(noticed.IsApplied);
        Assert.Equal("trapped", noticed.State);
        Assert.Contains("notices it", noticed.Message, StringComparison.Ordinal);
        Assert.Contains("Perception 6 against 4", noticed.Message, StringComparison.Ordinal);
        Assert.Empty(party.Inventory.Items);
        foreach (PartyMember member in party.Members) Assert.Equal(20, member.Resources.HitPoints.Current);

        // A trap the party knows about is what the target now offers, and the ruleset says so by giving the
        // target the verb that defeats it rather than by the kit branching on a state word.
        Assert.Equal(InteractionVerb.Disarm, cellar.Interaction.FocusedTarget?.Verb);
        Assert.Equal("trapped", cellar.Interaction.FocusedTarget?.Definition.State);

        InteractionResult defeated = cellar.Interaction.Use();
        Assert.True(defeated.IsApplied);
        Assert.Equal("disarmed", defeated.State);
        Assert.Contains("is defeated", defeated.Message, StringComparison.Ordinal);
        Assert.Contains("Disarm Traps 9 against 8", defeated.Message, StringComparison.Ordinal);
        foreach (PartyMember member in party.Members) Assert.Equal(20, member.Resources.HitPoints.Current);

        // Defeated once is defeated: the search now happens, and no trap is answered for it any more.
        Assert.Equal(InteractionVerb.Search, cellar.Interaction.FocusedTarget?.Verb);
        InteractionResult searched = cellar.Interaction.Use();
        Assert.True(searched.IsApplied);
        Assert.Equal("searched", searched.State);
        Assert.Single(party.Inventory.Items);
        foreach (PartyMember member in party.Members) Assert.Equal(20, member.Resources.HitPoints.Current);
    }

    [Fact]
    public void A_failed_disarm_attempt_costs_the_party_and_the_container_is_still_there()
    {
        // Noticing a trap is not defeating it: a party that sees it and cannot beat it pays on the attempt,
        // and the container is left for the search that follows.
        ContainerRule rule = new()
        {
            Armed = Armed(detect: 4, disarm: 8, harm: 6),
            Contents = [new InteractionItemYield(new ItemDefinitionId("brass-lamp"))],
        };

        using PartyEntity party = Party(perception: 6, disarmTraps: 7, hitPoints: 20);
        using Cellar cellar = Cellar.Build(rule, party);
        cellar.Interaction.Update();
        Assert.True(cellar.Interaction.Use().IsApplied);

        // Seven against the eight the disarm demands: the attempt fails and the trap goes off on it.
        InteractionResult failed = cellar.Interaction.Use();
        Assert.True(failed.IsApplied);
        Assert.Equal("sprung", failed.State);
        Assert.Contains("Disarm Traps 7 against 8", failed.Message, StringComparison.Ordinal);
        foreach (PartyMember member in party.Members) Assert.Equal(14, member.Resources.HitPoints.Current);

        // The trap is spent, so the search that follows costs nothing more.
        InteractionResult searched = cellar.Interaction.Use();
        Assert.True(searched.IsApplied);
        Assert.Equal("searched", searched.State);
        foreach (PartyMember member in party.Members) Assert.Equal(14, member.Resources.HitPoints.Current);
    }

    [Fact]
    public void What_a_container_gives_enters_the_shared_inventory_and_what_does_not_fit_is_refused_whole()
    {
        // Two definitions, one of which the pack has no room for: the transfer is judged before anything
        // moves, so the party ends up with neither rather than with half of what the chest held.
        ContainerRule rule = new()
        {
            Contents =
            [
                new InteractionItemYield(new ItemDefinitionId("brass-lamp"), 2),
                new InteractionItemYield(new ItemDefinitionId("iron-key")),
            ],
        };

        using PartyEntity roomy = Party(perception: 0, disarmTraps: 0, hitPoints: 20);
        using (Cellar cellar = Cellar.Build(rule, roomy))
        {
            cellar.Interaction.Update();
            InteractionResult searched = cellar.Interaction.Use();
            Assert.True(searched.IsApplied);
            // No stacking rule is composed here, which is what a game that has not said what bundles does:
            // two of one definition are two instances, and the pack counts them as two.
            Assert.Equal(3, roomy.Inventory.Items.Count);
            Assert.Equal(2, roomy.Inventory.TotalOf(new ItemDefinitionId("brass-lamp")));
            Assert.Equal(1, roomy.Inventory.TotalOf(new ItemDefinitionId("iron-key")));
            Assert.Contains("party takes", searched.Message, StringComparison.Ordinal);

            // Emptied once is emptied: the ledger's word is what the second attempt refuses by name.
            InteractionResult again = cellar.Interaction.Use();
            Assert.False(again.IsApplied);
            Assert.Equal("container-emptied", again.Code);
        }

        using PartyEntity cramped = Party(perception: 0, disarmTraps: 0, hitPoints: 20, capacity: 1);
        using (Cellar cellar = Cellar.Build(rule, cramped))
        {
            cellar.Interaction.Update();
            InteractionResult refused = cellar.Interaction.Use();

            Assert.False(refused.IsApplied);
            Assert.Equal("pack-full", refused.Code);
            Assert.Contains("takes at most 1", refused.Message, StringComparison.Ordinal);
            Assert.Empty(cramped.Inventory.Items);

            // A refused transfer left the container as it was: nothing was taken, so nothing is emptied,
            // and the state it reads as is the state the use found.
            Assert.Equal(string.Empty, cellar.Interaction.FocusedTarget?.State.State);
            Assert.Equal(0, cellar.Interaction.FocusedTarget?.State.Revision);
        }
    }

    [Fact]
    public void A_locked_container_refuses_by_name_and_opens_once_its_requirement_is_met()
    {
        // The lock is a requirement in the same vocabulary a door uses, and the party's own pack is what
        // answers it: the refusal names the key, and the key turns the lock.
        ContainerRule rule = new()
        {
            Requires = [new InteractionRequirement(InteractionRequirementKind.Item, "brass-key", label: "the Brass Key")],
            Contents = [new InteractionItemYield(new ItemDefinitionId("brass-lamp"))],
        };

        using PartyEntity party = Party(perception: 0, disarmTraps: 0, hitPoints: 20);
        using Cellar cellar = Cellar.Build(rule, party);
        cellar.Interaction.Update();

        // A locked container offers the act that turns the lock, never the search.
        Assert.Equal(InteractionVerb.Unlock, cellar.Interaction.FocusedTarget?.Verb);

        InteractionResult locked = cellar.Interaction.Use();
        Assert.False(locked.IsApplied);
        Assert.Equal("interaction-requirement-unmet", locked.Code);
        Assert.Contains("requires the Brass Key", locked.Message, StringComparison.Ordinal);
        Assert.Contains("carries none of it", locked.Message, StringComparison.Ordinal);
        Assert.Empty(party.Inventory.Items);
        Assert.Equal(0, cellar.Interaction.FocusedTarget?.State.Revision);

        // The key is the party's own item, so acquiring it is what changes the answer: nothing else about
        // the container is different.
        Assert.True(party.AcquireItem(new ItemDefinitionId("brass-key")).Admitted);
        InteractionResult turned = cellar.Interaction.Use();
        Assert.True(turned.IsApplied);
        Assert.Equal("unlocked", turned.State);
        Assert.Contains("lock falls open", turned.Message, StringComparison.Ordinal);

        // What the lock was holding is still there, and the search after it is the ordinary one.
        Assert.Equal(InteractionVerb.Search, cellar.Interaction.FocusedTarget?.Verb);
        InteractionResult searched = cellar.Interaction.Use();
        Assert.True(searched.IsApplied);
        Assert.Equal("searched", searched.State);
        Assert.Equal(1, party.Inventory.TotalOf(new ItemDefinitionId("brass-lamp")));
    }

    [Fact]
    public void A_trap_with_no_party_to_catch_refuses_the_use_instead_of_quietly_spending_it()
    {
        // A world with no party is a world where a sprung trap has nowhere to land. That is a refusal with
        // a name rather than a state word recorded as if the consequence had happened.
        ContainerRule rule = new() { Armed = Armed(detect: 4, disarm: 8, harm: 6) };

        using Cellar cellar = Cellar.Build(rule, party: null);
        cellar.Interaction.Update();

        InteractionResult result = cellar.Interaction.Use();
        Assert.False(result.IsApplied);
        Assert.Equal("interaction-no-party", result.Code);
        Assert.Contains("no party for it to catch", result.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, cellar.Interaction.FocusedTarget?.State.State);
    }

    [Fact]
    public void The_kit_moves_through_the_session_and_still_answers_about_what_the_party_faces()
    {
        // The engine's own selection decides what is faced, and a container out of reach is not faced at
        // all: the walk from the arrival point to the chest is what turns the search into a refusal by
        // reach, which is the same mechanism every other target goes through.
        ContainerRule rule = new() { Contents = [new InteractionItemYield(new ItemDefinitionId("brass-lamp"))] };
        using PartyEntity party = Party(perception: 0, disarmTraps: 0, hitPoints: 20);
        using Cellar cellar = Cellar.Build(rule, party);
        cellar.Interaction.Update();
        Assert.True(cellar.Interaction.Use().IsApplied);

        cellar.Move(new PlacePose(0, 0, 0, 512, 0));
        cellar.Interaction.Update();
        Assert.Null(cellar.Interaction.FocusedTarget);
        Assert.Equal(InteractionReason.NoCandidate, cellar.Interaction.FocusReason);
        Assert.Equal("interaction-no-target", cellar.Interaction.Use().Code);
    }

    [Fact]
    public void A_container_reaches_the_product_through_the_worlds_own_use_and_not_a_second_path()
    {
        // The world's own admitted use is the product's path: the reticle is refreshed, the use is applied,
        // and the result is reported. A container is searched through it exactly as a door is opened, which
        // is what "nothing enters the inventory except through the transfer path" has to mean in practice.
        ContainerRule rule = new() { Contents = [new InteractionItemYield(new ItemDefinitionId("brass-lamp"))] };
        using PartyEntity party = Party(perception: 0, disarmTraps: 0, hitPoints: 20);
        using Cellar cellar = Cellar.Build(rule, party);

        InteractionResult? result = cellar.World.Interact(use: true);
        Assert.NotNull(result);
        Assert.True(result.IsApplied);
        Assert.Equal("searched", result.State);
        Assert.Equal(result, cellar.World.Interaction?.LastResult);
        Assert.Single(party.Inventory.Items);

        // A world without an interaction policy answers nothing rather than pretending: the container is
        // still standing there, and there is no mechanism to reach it with.
        using Cellar silent = Cellar.Build(rule, party: null, interactive: false);
        Assert.Null(silent.World.Interact(use: true));
    }

    private static InteractionTrap Armed(int detect, int disarm, int harm) => new(
        "a trap",
        new InteractionChallenge("Perception", attempt: 0, difficulty: detect),
        new InteractionChallenge("Disarm Traps", attempt: 0, difficulty: disarm),
        new InteractionHarm(harm),
        isKnown: false,
        noticedState: "trapped",
        disarmedState: "disarmed",
        sprungState: "sprung");

    private static PartyEntity Party(int perception, int disarmTraps, int hitPoints, int capacity = int.MaxValue)
    {
        // Three members, so a trap that catches the party is visibly a loss to each of them rather than to
        // one character: this product's party is a band sharing one pose, and a container is used by the band.
        List<SkillEntry> skilled = [];
        if (perception > 0) skilled.Add(new SkillEntry(new SkillId("perception"), perception, SkillTier.None, perception));
        if (disarmTraps > 0) skilled.Add(new SkillEntry(new SkillId("disarm-traps"), disarmTraps, SkillTier.None, disarmTraps));
        return new PartyEntityFactory(inventoryCapacity: capacity == int.MaxValue ? null : new PackLimit(capacity)).Create(
            new PartyCreation(
                [
                    Member("Tester", skilled, hitPoints),
                    Member("Second", [], hitPoints),
                    Member("Third", [], hitPoints),
                ],
                coins: 0,
                foodPortions: 4,
                ProvisionUnit.Portions,
                reputation: 0,
                fame: 0));
    }

    private static MemberCreation Member(string name, IReadOnlyList<SkillEntry> skills, int hitPoints) =>
        new(new PartyMemberSeed(
            name,
            new RaceId("testfolk"),
            new ClassId("fighter"),
            [new AttributeScore(new AttributeId("vigour"), 12)],
            skills,
            spells: [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(hitPoints),
            spellPoints: ResourcePool.Full(5)));

    /// <summary>A pack that takes what a test allows and refuses the rest by name.</summary>
    private sealed class PackLimit(int maximum) : IInventoryCapacityRule
    {
        public PartyRefusal? Judge(IReadOnlyList<ItemInstance> held, ItemDefinitionId definition, int count) =>
            held.Count + count > maximum
                ? new PartyRefusal("pack-full", $"The pack holds {held.Count} and takes at most {maximum}.")
                : null;
    }

    /// <summary>
    /// This suite's game: a container that may hold a trap, may be locked, and holds what the test says.
    /// </summary>
    /// <remarks>
    /// Every number and word a real ruleset would own is here instead: the trap's challenge attempts come
    /// from the party's own skills, so a test states what the party knows by giving it a member with the
    /// skill, and the kit's workflow is what turns that into noticing, defeating, or paying.
    /// </remarks>
    private sealed class ContainerRule : IInteractionRule
    {
        internal InteractionTrap? Armed { get; set; }

        internal IReadOnlyList<InteractionItemYield> Contents { get; set; } = [];

        internal IReadOnlyList<InteractionRequirement> Requires { get; set; } = [];

        public InteractionTargetDefinition? Describe(InteractionTargetRequest request)
        {
            if (!string.Equals(request.Placement.Content.Kind, "container", StringComparison.Ordinal)) return null;

            string state = request.State;
            bool locked = Requires.Count > 0 && state != "unlocked";
            InteractionVerb verb = locked
                ? InteractionVerb.Unlock
                : state == "trapped" ? InteractionVerb.Disarm : InteractionVerb.Search;
            return new InteractionTargetDefinition(
                new InteractionTargetKind("container"),
                "A chest",
                verb,
                reach: 512,
                state,
                [.. Requires]);
        }

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context)
        {
            if (Armed is not { } trap) return null;
            if (target.Verb == InteractionVerb.Unlock) return null;

            // A trap that has been defeated or has already gone off guards nothing, which is what a ruleset
            // states about its own state words: the kit never decides that for it.
            if (target.State is "disarmed" or "sprung") return null;

            // The attempts are the party's own, which is what a ruleset reads and the kit never does.
            return trap with
            {
                Detect = new InteractionChallenge(trap.Detect.Label, Best(context.Party, "perception"), trap.Detect.Difficulty),
                Disarm = new InteractionChallenge(trap.Disarm.Label, Best(context.Party, "disarm-traps"), trap.Disarm.Difficulty),
                IsKnown = string.Equals(target.State, trap.NoticedState, StringComparison.Ordinal),
            };
        }

        public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context)
        {
            if (requirement.Kind != InteractionRequirementKind.Item || context.Party is not { } party)
            {
                return InteractionRequirementVerdict.Unsatisfied($"nothing here answers {requirement.Describe()}");
            }

            int carried = party.Inventory.TotalOf(new ItemDefinitionId(requirement.Name));
            return carried >= requirement.Amount
                ? InteractionRequirementVerdict.Satisfied
                : InteractionRequirementVerdict.Unsatisfied($"the party carries none of it ({carried} of {requirement.Describe()})");
        }

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context)
        {
            _ = context;
            if (target.Verb == InteractionVerb.Unlock) return InteractionOutcome.Applied("unlocked", "The lock falls open.");
            if (target.State == "searched") return InteractionOutcome.Refused("container-emptied", $"{target.Name} has already been emptied.");
            if (Contents.Count == 0) return InteractionOutcome.Applied("searched", $"{target.Name} is empty.");
            return InteractionOutcome.Applied("searched", $"{target.Name} holds what it holds.", items: Contents);
        }

        private static int Best(PartyEntity? party, string skill)
        {
            if (party is null) return 0;
            SkillId id = new(skill);
            int best = 0;
            foreach (PartyMember member in party.Members) best = Math.Max(best, member.Skills.LevelOf(id));
            return best;
        }
    }

    /// <summary>The world these tests search in: one interior holding one container, and a party.</summary>
    private sealed class Cellar : IDisposable
    {
        private readonly PartyEntity? _party;

        private Cellar(SessionWorld world, PartyEntity? party) => (World, _party) = (world, party);

        internal SessionWorld World { get; }

        internal PartyInteraction Interaction => World.Interaction ?? throw new InvalidOperationException("This cellar was built without an interaction policy.");

        internal void Move(PlacePose pose) => World.ArriveAt(CellarPlace, pose);

        internal static Cellar Build(ContainerRule rule, PartyEntity? party, bool interactive = true)
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new InMemoryContentSource()
                    .Add("packs/world/pack.json", Manifest())
                    .Add("packs/world/places.json", Document("places", "place", Place())),
                Layout).RequireValid();

            PlaceGraph graph = PlaceGraphLoader.Load(catalog);
            PartyPoseOwner owner = new(
                new PartyPose(CellarPlace, new PlacePose(0, 0, 0, 1536, 0)),
                new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
            InteractionPolicy policy = new(
                rule,
                PlaceSpace.HeightIsThird(new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512), radiansAtZeroFacing: 0),
                new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31));

            SessionWorld world = new(
                graph,
                owner,
                new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
                new TestCostRule(),
                time: new FixedTime(),
                mover: null,
                resources: party is null ? null : new PartyResourceLedger(party),
                partyEntity: party,
                interaction: interactive ? policy : null);
            return new Cellar(world, party);
        }

        public void Dispose()
        {
            World.Dispose();
            _party?.Dispose();
        }

        /// <summary>The place: one container standing where the party faces, and nothing else usable.</summary>
        private static string Place() =>
            """
            { "id": "7", "kind": "interior", "name": "Cellar", "respawnDays": 3,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "container-0", "kind": "container", "x": 100, "y": 0, "z": 0, "flags": 1 },
                { "id": "container-1", "kind": "container", "x": 0, "y": 4000, "z": 0, "flags": 0 } ] }
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

    /// <summary>A time source the world reads its days from, which never moves here.</summary>
    private sealed class FixedTime : IWorldTimeSource
    {
        public int ElapsedGameDays => 0;
    }

    /// <summary>Nothing in a cellar costs anything to reach.</summary>
    private sealed class TestCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}
