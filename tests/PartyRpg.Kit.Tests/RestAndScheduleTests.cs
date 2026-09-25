using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The schedule a place's doors are locked by, and the one mechanism that turns a rest, a camp, or a wait
/// into time.
/// </summary>
/// <remarks>
/// <para>
/// Every rule a game would recognize here belongs to the test's own policy — which places keep hours, what a
/// night costs, what breaks it, and what going without sleep does — which is the point of the seams: the kit
/// holds no shop, no clock reading of its own, and no window content states, so the same mechanism serves a
/// test that invents them and a ruleset that owns them. The places are written inline, so what a door is and
/// what a place keeps come from content here exactly as they do from an imported pack in the product.
/// </para>
/// <para>
/// The clock is a real <see cref="GameClock"/> throughout, not a stand-in: the whole point of these facts is
/// that a door's hours, a night's cost, and a party's fatigue are all game time on the one clock, so a double
/// that answered a date would prove nothing about the wiring.
/// </para>
/// </remarks>
public sealed class RestAndScheduleTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Hall = new("1");
    private static readonly ConditionId Weakness = new("weak");

    /// <summary>The calendar these tests run on, which is the product's authored one.</summary>
    private static GameCalendar Calendar => GameCalendar.TwelveMonthsOfFourWeeks;

    /// <summary>The window these tests' places keep, which is the shipped building table's commonest pair.</summary>
    private static readonly OpeningHours Business = new(6, 18);

    [Fact]
    public void A_window_opens_and_closes_at_the_hours_it_states()
    {
        // The opening hour is included and the closing hour excluded, so a place open 6 to 18 is shut at
        // 18:00 exactly — which is the hour the panel shows and the hour a door locks.
        Assert.True(Business.IsOpenAt(new GameDate(1168, 1, 1, 6, 0, 0)));
        Assert.True(Business.IsOpenAt(new GameDate(1168, 1, 1, 17, 59, 0)));
        Assert.False(Business.IsOpenAt(new GameDate(1168, 1, 1, 18, 0, 0)));
        Assert.False(Business.IsOpenAt(new GameDate(1168, 1, 1, 5, 59, 0)));
        Assert.Equal("06:00–18:00", Business.ToString());

        // Night trade wraps past midnight, which a naive range comparison would get backwards.
        OpeningHours night = new(18, 6);
        Assert.True(night.IsOpenAt(new GameDate(1168, 1, 1, 23, 0, 0)));
        Assert.True(night.IsOpenAt(new GameDate(1168, 1, 2, 3, 0, 0)));
        Assert.False(night.IsOpenAt(new GameDate(1168, 1, 2, 12, 0, 0)));

        // Something that never closes states the whole day, and every hour of it is open.
        OpeningHours always = new(0, 24);
        Assert.True(always.IsOpenAt(new GameDate(1168, 1, 1, 0, 0, 0)));
        Assert.True(always.IsOpenAt(new GameDate(1168, 1, 1, 23, 59, 0)));

        // A window that opens and closes at one hour has no hours to be open in, and is refused where it is
        // built rather than given a meaning nobody stated.
        Assert.Throws<ArgumentOutOfRangeException>(() => new OpeningHours(9, 9));

        // The next change is a point on the calendar and not a countdown: the close today while it is still
        // ahead, and tomorrow's opening once it is not.
        Assert.Equal(new GameDate(1168, 1, 1, 18, 0, 0), Business.NextChangeAfter(Calendar, new GameDate(1168, 1, 1, 9, 0, 0)));
        Assert.Equal(new GameDate(1168, 1, 2, 6, 0, 0), Business.NextChangeAfter(Calendar, new GameDate(1168, 1, 1, 18, 0, 0)));
        Assert.Equal(new GameDate(1168, 1, 2, 6, 0, 0), Business.NextChangeAfter(Calendar, new GameDate(1168, 1, 1, 23, 0, 0)));
        Assert.Equal(new GameDate(1168, 1, 2, 6, 0, 0), night.NextChangeAfter(Calendar, new GameDate(1168, 1, 1, 23, 0, 0)));
    }

    [Fact]
    public void A_schedule_answers_which_places_are_clocked_and_when_they_change()
    {
        PlaceSchedule schedule = new([new PlaceHours(Hall, Business)]);

        Assert.Equal("open", schedule.StateOf(Hall, new GameDate(1168, 1, 1, 9, 0, 0)));
        Assert.Equal("closed", schedule.StateOf(Hall, new GameDate(1168, 1, 1, 20, 0, 0)));
        Assert.Equal(Business, schedule.HoursOf(Hall));

        // A place the schedule says nothing about is open at every hour and reads as neither open nor shut:
        // a schedule states when something is clocked, and inventing a window for the rest would lock doors
        // content never closed.
        PlaceId elsewhere = new("2");
        Assert.True(schedule.IsOpenAt(elsewhere, new GameDate(1168, 1, 1, 3, 0, 0)));
        Assert.Equal(string.Empty, schedule.StateOf(elsewhere, new GameDate(1168, 1, 1, 3, 0, 0)));
        Assert.Null(schedule.HoursOf(elsewhere));
        Assert.Null(schedule.NextChangeAfter(elsewhere, new GameDate(1168, 1, 1, 3, 0, 0), Calendar));

        // One place with two windows would answer two different things about one door, so it is refused.
        Assert.Throws<ArgumentException>(() => new PlaceSchedule([new PlaceHours(Hall, Business), new PlaceHours(Hall, new OpeningHours(8, 20))]));
    }

    [Fact]
    public void A_doors_access_changes_with_the_clock_at_open_closing_and_closed_times()
    {
        // Ten at night: the place keeps 6 to 18, so the same door the party faces is shut and says what it
        // keeps, what the clock reads, and when it opens again.
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party();
        PlaceSchedule schedule = new([new PlaceHours(Hall, Business)]);
        using SessionWorld world = World(clock, party, new ScheduleRule(schedule));

        world.Interact(use: false);
        Assert.Equal("A door", world.Interaction!.FocusedTarget!.Definition.Name);
        InteractionResult shut = world.Interact(use: true)!;
        Assert.False(shut.IsApplied);
        Assert.Equal("interaction-requirement-unmet", shut.Code);
        Assert.Contains("the hours 06:00–18:00", shut.Message, StringComparison.Ordinal);
        Assert.Contains("the clock stands at 22:00", shut.Message, StringComparison.Ordinal);

        // Two minutes before six the door is still shut, and at six — the hour the window opens at — the very
        // same door opens in the very same use, because nothing about it was remembered.
        clock.Advance(GameDuration.FromHours(7) + GameDuration.FromMinutes(58));
        Assert.False(world.Interact(use: true)!.IsApplied);
        clock.Advance(GameDuration.FromMinutes(2));
        Assert.Equal(new GameDate(1168, 1, 2, 6, 0, 0), clock.Now);
        InteractionResult opened = world.Interact(use: true)!;
        Assert.True(opened.IsApplied);
        Assert.Equal("open", opened.State);
        Assert.Contains("swings open", opened.Message, StringComparison.Ordinal);

        // Six in the evening: the shut hours are judged again on the same door, which is open in state and
        // still refused — the schedule is a clock read rather than a lock somebody turned once.
        clock.Advance(GameDuration.FromHours(12));
        InteractionResult again = world.Interact(use: true)!;
        Assert.False(again.IsApplied);
        Assert.Equal("interaction-requirement-unmet", again.Code);
        Assert.Contains("the clock stands at 18:00", again.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rest_moves_the_clock_restores_the_party_and_spends_the_day()
    {
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party(foodPortions: 4, wounded: 25, weak: true);
        PartyResourceLedger accounts = Ledger(party);
        TestRestRule rule = new(campCharge: 3);
        using SessionWorld world = Site(clock, party, accounts);
        world.Populate();
        PartyRest rest = new(rule, party, clock, world, accounts);

        // Rest is refused in the open, which is what makes camping the act that belongs there.
        Assert.Equal("rest-in-the-open", rest.Perform(RestKind.Rest).Code);

        // A roofed place is where this test's party waits out the night — and camping under that roof is
        // refused for the same reason a rest in the open is, because the two are different acts rather than
        // one command with two names.
        using SessionWorld roofed = Site(clock, party, accounts, kind: PlaceKind.Interior);
        roofed.Populate();
        PartyRest indoors = new(rule, party, clock, roofed, accounts);
        Assert.Equal("camp-under-a-roof", indoors.Perform(RestKind.Camp).Code);
        Assert.Equal(GameDuration.None, clock.Elapsed);

        // Eight hours on the clock, both pools filled, the weakness a night ends cleared, and the day settled
        // through the party's own ledger.
        RestResult rested = indoors.Perform(RestKind.Rest);

        Assert.True(rested.IsApplied);
        Assert.Equal(RestKind.Rest, rested.Kind);
        Assert.Equal(8 * 3600d, rested.Elapsed.TotalSeconds);
        Assert.Equal(new GameDate(1168, 1, 2, 6, 0, 0), rested.To);
        Assert.True(rested.Recovered);
        Assert.Equal(1, rested.Restored);
        Assert.Equal([Weakness], rested.Cleared);
        Assert.False(party.Members[0].Conditions.Has(Weakness));
        Assert.Equal(40, party.Members[0].Resources.HitPoints.Current);
        Assert.Equal(10, party.Members[0].Resources.SpellPoints.Current);
        Assert.Equal(new Provisions(2, ProvisionUnit.Portions), rested.Charge);
        Assert.Equal(2, rested.Covered);
        Assert.Equal(2, party.Food.Portions);
        Assert.Contains("every member is restored", rested.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rest_that_eats_the_last_of_the_larder_leaves_the_party_weak()
    {
        // Recovery happens before the day is settled, so the larder's own rule has the last word: a night that
        // spends the last portion feeds nobody, and the state the provisions rule applies is the one the party
        // wakes with — rather than a rest clearing a hunger it just caused.
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party(foodPortions: 2, wounded: 10, weak: false);
        PartyResourceLedger accounts = Ledger(party);
        TestRestRule rule = new(campCharge: 3);
        using SessionWorld world = Site(clock, party, accounts, kind: PlaceKind.Interior);
        world.Populate();
        PartyRest rest = new(rule, party, clock, world, accounts);

        RestResult rested = rest.Perform(RestKind.Rest);

        Assert.True(rested.IsApplied);
        Assert.True(rested.Recovered);
        Assert.Equal(0, party.Food.Portions);
        Assert.True(party.Members[0].Conditions.Has(Weakness));
        Assert.Equal(Weakness, rested.Shortage!.Value.Condition);
        Assert.Equal(40, party.Members[0].Resources.HitPoints.Current);
    }

    [Fact]
    public void A_rest_the_larder_cannot_provision_is_refused_before_it_starts()
    {
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party(foodPortions: 1, wounded: 25);
        PartyResourceLedger accounts = Ledger(party);
        TestRestRule rule = new(campCharge: 3);
        using SessionWorld world = Site(clock, party, accounts, kind: PlaceKind.Interior);
        world.Populate();
        PartyRest rest = new(rule, party, clock, world, accounts);

        RestResult refused = rest.Perform(RestKind.Rest);

        Assert.False(refused.IsApplied);
        Assert.Equal("rest-larder-short", refused.Code);
        Assert.Equal(GameDuration.None, clock.Elapsed);
        Assert.Equal(1, party.Food.Portions);
        Assert.Equal(25, party.Members[0].Resources.HitPoints.Current);
    }

    [Fact]
    public void A_wait_moves_the_clock_and_restores_nobody()
    {
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party(wounded: 25, weak: true);
        PartyResourceLedger accounts = Ledger(party);
        TestRestRule rule = new(campCharge: 3);
        using SessionWorld world = Site(clock, party, accounts, kind: PlaceKind.Interior);
        world.Populate();
        PartyRest rest = new(rule, party, clock, world, accounts);

        // An hour is an hour, and nothing about the party changes: waiting passes time and heals nobody.
        RestResult waited = rest.Perform(RestKind.WaitAnHour);
        Assert.True(waited.IsApplied);
        Assert.Equal(GameDuration.FromHours(1), waited.Elapsed);
        Assert.False(waited.Recovered);
        Assert.Equal(0, waited.Restored);
        Assert.True(waited.Charge.IsNone);
        Assert.Equal(25, party.Members[0].Resources.HitPoints.Current);
        Assert.True(party.Members[0].Conditions.Has(Weakness));
        Assert.Contains("nothing was restored", waited.Message, StringComparison.Ordinal);

        // Waiting until dawn is a clock read: the party waits for the next five in the morning, which from one
        // in the morning is four hours and never a fraction of one.
        clock.Advance(GameDuration.FromHours(2));
        Assert.Equal(new GameDate(1168, 1, 2, 1, 0, 0), clock.Now);
        RestResult dawn = rest.Perform(RestKind.WaitUntilDawn);
        Assert.Equal(GameDuration.FromHours(4), dawn.Elapsed);
        Assert.Equal(new GameDate(1168, 1, 2, 5, 0, 0), dawn.To);

        // A short wait is the manual's own five minutes, and it is not a sleep either.
        RestResult shortWait = rest.Perform(RestKind.WaitFiveMinutes);
        Assert.Equal(GameDuration.FromMinutes(5), shortWait.Elapsed);
        Assert.False(shortWait.Recovered);
    }

    [Fact]
    public void A_camp_charges_the_ground_and_refuses_where_the_policy_will_not_take_it()
    {
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party(foodPortions: 6, wounded: 25);
        PartyResourceLedger accounts = Ledger(party);
        TestRestRule rule = new(campCharge: 3);
        using SessionWorld empty = Site(clock, party, accounts);
        empty.Populate();
        PartyRest rest = new(rule, party, clock, empty, accounts);

        // The ground's own charge is the policy's, and three portions is what three portions means.
        RestResult camped = rest.Perform(RestKind.Camp);
        Assert.True(camped.IsApplied);
        Assert.Equal(3, camped.Charge.Amount);
        Assert.Equal(3, camped.Covered);
        Assert.Equal(3, party.Food.Portions);
        Assert.True(camped.Recovered);

        // A living creature a hundred units away is near enough that the party will not lie down, and a
        // refusal leaves the clock and the larder exactly where they were.
        using SessionWorld occupied = Site(clock, party, accounts, spawn: true);
        occupied.Populate();
        PartyRest wary = new(rule, party, clock, occupied, accounts);
        GameDate before = clock.Now;
        RestResult refused = wary.Perform(RestKind.Camp);

        Assert.False(refused.IsApplied);
        Assert.Equal("camp-hostiles-near", refused.Code);
        Assert.Equal(before, clock.Now);
        Assert.Equal(3, party.Food.Portions);
    }

    [Fact]
    public void A_broken_camp_passes_only_the_part_that_happened_and_costs_nothing()
    {
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party(foodPortions: 6, wounded: 25, weak: true);
        PartyResourceLedger accounts = Ledger(party);
        using SessionWorld world = Site(clock, party, accounts);
        world.Populate();

        // What breaks the night is the policy's answer, and the period it leaves the party with is the part
        // before the break: the clock is advanced by that and nothing else.
        RestInterruption broke = RestInterruption.Broke("camp-broken", "something finds the camp", GameDuration.FromHours(1) + GameDuration.FromMinutes(4));
        PartyRest rest = new(new TestRestRule(campCharge: 3, interruption: broke), party, clock, world, accounts);
        RestResult result = rest.Perform(RestKind.Camp);

        Assert.True(result.IsApplied);
        Assert.True(result.Interrupted);
        Assert.Equal(64 * 60d, result.Elapsed.TotalSeconds);
        Assert.Equal(new GameDate(1168, 1, 1, 23, 4, 0), result.To);
        Assert.False(result.Recovered);
        Assert.Equal(0, result.Restored);
        Assert.True(result.Charge.IsNone);
        Assert.Equal(6, party.Food.Portions);
        Assert.Equal(25, party.Members[0].Resources.HitPoints.Current);
        Assert.True(party.Members[0].Conditions.Has(Weakness));
        Assert.Contains("something finds the camp", result.Message, StringComparison.Ordinal);
        Assert.Contains("no provisions were spent", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Going_without_sleep_weakens_on_the_clocks_deadline_and_a_sleep_clears_it()
    {
        GameClock clock = Clock(hour: 22);
        using PartyEntity party = Party(memberCount: 2, foodPortions: 6);
        PartyResourceLedger accounts = Ledger(party);
        TestRestRule rule = new(campCharge: 3);
        using SessionWorld world = Site(clock, party, accounts, kind: PlaceKind.Interior);
        world.Populate();
        PartyRest rest = new(rule, party, clock, world, accounts);
        Assert.NotNull(rest.Fatigue);
        Assert.Equal(GameDuration.FromHours(24), rest.Fatigue!.Interval);
        Assert.False(rest.Fatigue.IsWeak);

        // A day of game time passed through the clock's own advance, which is how a journey and an update
        // both move it: the deadline the debt is held on comes due and the state lands on every member.
        ClockAdvance advance = clock.Advance(GameDuration.FromHours(24));
        rest.Observe(advance);

        Assert.Equal(1, rest.Fatigue.Landed);
        Assert.True(rest.Fatigue.IsWeak);
        Assert.All(party.Members, member => Assert.Equal(1, member.Conditions.SeverityOf(Weakness)));

        // A night's sleep pays the debt: the state is cleared, and the next one is a day from the moment the
        // party wakes rather than a day from when the debt landed.
        RestResult slept = rest.Perform(RestKind.Rest);
        Assert.True(slept.IsApplied);
        Assert.False(rest.Fatigue.IsWeak);
        Assert.False(party.Members[0].Conditions.Has(Weakness));
        Assert.Equal(2, slept.Restored);
        Assert.Equal(clock.Calendar.Add(clock.Now, GameDuration.FromHours(24)), rest.Fatigue.Due);

        // A wait pays nothing: a party that stays awake another day is tired again when the debt lands, and
        // the landing is the clock's own report rather than a count of how many updates went by.
        ClockAdvance next = clock.Advance(GameDuration.FromHours(24));
        rest.Observe(next);
        Assert.Equal(2, rest.Fatigue.Landed);
        Assert.True(rest.Fatigue.IsWeak);
    }

    /// <summary>The clock these tests run on: the authored calendar, a stated hour, and the product's rate.</summary>
    private static GameClock Clock(int hour) => new(
        Calendar,
        new GameDate(1168, 1, 1, hour, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>A party a night is spent on: wounded, and by default unweakened.</summary>
    private static PartyEntity Party(
        int foodPortions = 0,
        int wounded = 0,
        bool weak = false,
        int memberCount = 1)
    {
        List<MemberCreation> members = [];
        for (int index = 0; index < memberCount; index++)
        {
            members.Add(new MemberCreation(new PartyMemberSeed(
                $"Member {index + 1}",
                new RaceId("testfolk"),
                new ClassId("fighter"),
                [new AttributeScore(new AttributeId("vigour"), 12)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: weak ? [new ActiveCondition(new ConditionId("weak"), 1)] : [],
                hitPoints: ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(10))));
        }

        PartyEntity party = new PartyEntityFactory().Create(new PartyCreation(
            members,
            coins: 0,
            foodPortions: foodPortions,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));
        if (wounded > 0)
        {
            foreach (PartyMember member in party.Members) member.Resources.TakeDamage(member.Resources.HitPoints.Maximum - wounded);
        }

        return party;
    }

    /// <summary>The one path into a party's accounts, under the day rule this test states.</summary>
    private static PartyResourceLedger Ledger(PartyEntity party) => new(party, provisioning: new Rations());

    /// <summary>
    /// The world a stop is judged against: the same place, with no interaction policy, because a rest reads
    /// the place, its population, and the party rather than anything the party can face.
    /// </summary>
    private static SessionWorld Site(
        GameClock clock,
        PartyEntity party,
        PartyResourceLedger? accounts = null,
        PlaceKind kind = PlaceKind.Region,
        bool spawn = false) => World(clock, party, rule: null, accounts, kind, spawn);

    /// <summary>A world over one place, which is where a door stands and what a rest is judged against.</summary>
    private static SessionWorld World(
        GameClock clock,
        PartyEntity party,
        IInteractionRule? rule,
        PartyResourceLedger? accounts = null,
        PlaceKind kind = PlaceKind.Region,
        bool spawn = false)
    {
        PlaceGraph graph = Graph(kind, spawn);
        PartyPoseOwner pose = new(new PartyPose(Hall, PlacePose.Origin), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        PlaceSchedule schedule = new([new PlaceHours(Hall, Business)]);
        return new SessionWorld(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new TestCostRule(),
            clock,
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: clock,
            resources: accounts,
            partyEntity: party,
            interaction: rule is null ? null : new InteractionPolicy(rule, Space, new InteractionTuning(0.20, 0.31)),
            schedule: schedule);
    }

    /// <summary>The place space the ruleset walks by, so a placement at the party's facing is the one it faces.</summary>
    private static PlaceSpace Space => PlaceSpace.HeightIsThird(new FacingRule(2048, -512, 512), Math.PI / 2);

    /// <summary>One place with a door the party faces, and optionally a creature standing near it.</summary>
    private static PlaceGraph Graph(PlaceKind kind, bool spawn) => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "world",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
                    }
                    """)
                .Add(
                    "packs/world/places.json",
                    $$"""
                    {
                      "documentId": "places",
                      "definitionKind": "place",
                      "entries": [
                        { "id": "1", "kind": "{{(kind == PlaceKind.Interior ? "interior" : "region")}}", "name": "The Hall", "respawnDays": 3,
                          "placements": [
                            { "id": "door-0", "kind": "door", "x": 100, "y": 0, "z": 0, "state": 2 }
                            {{(spawn ? """, { "id": "spawn-0", "kind": "spawn", "x": 100, "y": 0, "z": 0, "radius": 32 }""" : string.Empty)}} ] }
                      ]
                    }
                    """),
            Layout).RequireValid());

    /// <summary>Walking is free: nothing in these tests is about what a road costs.</summary>
    private sealed class TestCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }

    /// <summary>What a day costs this suite's party, and what a larder left short does to it.</summary>
    private sealed class Rations : IProvisionDayRule
    {
        public Provisions DailyCharge(int members, int followers) => new(1, ProvisionUnit.Portions);

        public ActiveCondition? Consequence(int portionsAfter, int members, int followers) =>
            portionsAfter >= 1 ? null : new ActiveCondition(Weakness, 1);
    }

    /// <summary>
    /// The rules a door's hours are read by, stated here rather than in the product: what a door requires,
    /// what the schedule says about it, and what opening it makes of it.
    /// </summary>
    private sealed class ScheduleRule(PlaceSchedule schedule) : IInteractionRule
    {
        /// <summary>The requirement name a place's hours are asked for under, as a time-of-day requirement.</summary>
        private const string Open = "open";

        public InteractionTargetDefinition? Describe(InteractionTargetRequest request)
        {
            if (!string.Equals(request.Placement.Content.Kind, "door", StringComparison.Ordinal)) return null;
            List<InteractionRequirement> requires = [];
            if (schedule.HoursOf(request.Place) is { } hours)
            {
                requires.Add(new InteractionRequirement(InteractionRequirementKind.TimeOfDay, Open, 1, $"the hours {hours}"));
            }

            return new InteractionTargetDefinition(
                new InteractionTargetKind("door"),
                "A door",
                InteractionVerb.Open,
                reach: 512,
                state: request.State.Length > 0 ? request.State : "closed",
                requires);
        }

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context)
        {
            if (requirement.Kind != InteractionRequirementKind.TimeOfDay) return InteractionRequirementVerdict.Satisfied;
            if (schedule.HoursOf(context.Place) is not { } hours || context.Clock is not { } clock)
            {
                return InteractionRequirementVerdict.Satisfied;
            }

            GameDate now = clock.Now;
            return hours.IsOpenAt(now)
                ? InteractionRequirementVerdict.Satisfied
                : InteractionRequirementVerdict.Unsatisfied($"It keeps {hours} and the clock stands at {now.Hour:00}:{now.Minute:00}.");
        }

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) =>
            string.Equals(target.State, "open", StringComparison.Ordinal)
                ? InteractionOutcome.Refused("door-already-open", "It already stands open.")
                : InteractionOutcome.Applied("open", "It swings open.");
    }

    /// <summary>
    /// This suite's game: a roof to rest under, the open to camp in, a ground that costs what the test says,
    /// creatures that keep a party from lying down, and a night that may be broken.
    /// </summary>
    private sealed class TestRestRule(int campCharge, RestInterruption? interruption = null) : IRestRule
    {
        /// <summary>How near a creature keeps a party from camping, in the place's own units.</summary>
        private const double HostileRange = 512;

        public RestQuote Quote(RestRequest request)
        {
            if (request.Kind == RestKind.Camp)
            {
                if (request.Site.Place.Kind != PlaceKind.Region)
                {
                    return RestQuote.Refused(new PartyRefusal("camp-under-a-roof", "The party stands under a roof and will not camp here."));
                }

                int hostiles = HostilesNear(request);
                if (hostiles > 0)
                {
                    return RestQuote.Refused(new PartyRefusal("camp-hostiles-near", $"{hostiles} hostile creature(s) stand near enough that the party will not camp."));
                }

                return RestQuote.Planned(GameDuration.FromHours(8), new Provisions(campCharge, ProvisionUnit.Portions));
            }

            return request.Site.Place.Kind == PlaceKind.Interior
                ? RestQuote.Planned(GameDuration.FromHours(8), new Provisions(2, ProvisionUnit.Portions))
                : RestQuote.Refused(new PartyRefusal("rest-in-the-open", "The party stands in the open and will not sleep there."));
        }

        public RestInterruption? Interrupt(RestRequest request) => request.Kind == RestKind.Camp ? interruption : null;

        public IReadOnlyList<ConditionId> RecoveredBy(RestRequest request) => [Weakness];

        public ActiveCondition Fatigue => new(Weakness, 1);

        public GameDuration SleepInterval => GameDuration.FromHours(24);

        /// <summary>How many creatures stand within this suite's range of the party.</summary>
        private static int HostilesNear(RestRequest request)
        {
            PlacePose party = request.Site.Pose;
            int hostiles = 0;
            foreach (PlacePopulationEntity entity in request.Site.Population)
            {
                if (!string.Equals(entity.Content.Kind, "spawn", StringComparison.Ordinal)) continue;
                PlacePose at = entity.Pose;
                double x = at.X - party.X;
                double y = at.Y - party.Y;
                double z = at.Z - party.Z;
                if ((x * x) + (y * y) + (z * z) < HostileRange * HostileRange) hostiles++;
            }

            return hostiles;
        }
    }

    /// <summary>Content staged in memory, so a place's placements are read without touching the file system.</summary>
    private sealed class InMemoryContentSource : IContentSource
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        internal InMemoryContentSource Add(string path, string text)
        {
            _files[path] = text;
            return this;
        }

        public IReadOnlyList<string> ListDirectories(string relativePath)
        {
            string prefix = Normalize(relativePath);
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (string path in _files.Keys)
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string remainder = path[prefix.Length..];
                int separator = remainder.IndexOf('/', StringComparison.Ordinal);
                if (separator > 0) names.Add(remainder[..separator]);
            }

            return [.. names.Order(StringComparer.Ordinal)];
        }

        public IReadOnlyList<string> ListFiles(string relativePath)
        {
            string prefix = Normalize(relativePath);
            return [.. _files.Keys
                .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
                .Select(path => path[prefix.Length..])
                .Where(remainder => remainder.Length > 0 && !remainder.Contains('/', StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)];
        }

        public bool FileExists(string relativePath) => _files.ContainsKey(relativePath);

        public string ReadText(string relativePath) =>
            _files.TryGetValue(relativePath, out string? text) ? text : throw new FileNotFoundException(relativePath);

        private static string Normalize(string relativePath) =>
            relativePath.Length == 0 ? string.Empty : $"{relativePath.TrimEnd('/')}/";
    }
}
