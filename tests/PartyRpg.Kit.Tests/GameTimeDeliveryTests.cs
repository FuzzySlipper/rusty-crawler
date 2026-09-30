using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Game time has one delivery path: the clock tells every owner registered with it about every advance,
/// whoever moved it, so a rest, a wait and a journey reach the same owners an admitted update does.
/// </summary>
public sealed class GameTimeDeliveryTests
{
    private static readonly EffectId Ward = new("ward:magic");

    [Fact]
    public void Every_advance_reaches_every_registered_owner_whoever_moves_the_clock()
    {
        GameClock clock = TestClock.Create();
        Recorder first = new();
        Recorder second = new();
        clock.Observe(first);
        clock.Observe(second);

        ClockAdvance hour = clock.Advance(GameDuration.FromHours(1));
        ClockAdvance admitted = clock.AdvanceAdmittedSeconds(120);

        Assert.Equal([hour, admitted], first.Heard);
        Assert.Equal([hour, admitted], second.Heard);

        // An advance of no time moves nothing and tells nobody anything.
        clock.Advance(GameDuration.None);
        Assert.Equal(2, first.Heard.Count);
    }

    [Fact]
    public void A_released_owner_hears_nothing_more_and_one_registered_twice_is_refused()
    {
        GameClock clock = TestClock.Create();
        Recorder owner = new();
        IDisposable registration = clock.Observe(owner);
        Assert.Throws<ArgumentException>(() => clock.Observe(owner));

        registration.Dispose();
        clock.Advance(GameDuration.FromHours(1));

        Assert.Empty(owner.Heard);
        Assert.Equal(0, clock.Observers);
    }

    [Fact]
    public void An_owner_that_moves_the_clock_while_hearing_it_is_refused()
    {
        GameClock clock = TestClock.Create();
        clock.Observe(new Recorder(onHeard: () => clock.Advance(GameDuration.FromMinutes(1))));

        Assert.Throws<InvalidOperationException>(() => clock.Advance(GameDuration.FromHours(1)));
    }

    [Fact]
    public void A_ward_whose_end_falls_inside_a_wait_ends_in_that_wait()
    {
        using PartyEntity party = Party();
        GameClock clock = TestClock.Create();
        RunningSpellEffects running = new(party, clock);
        clock.Observe(running);
        PartyRest rest = new(new TestNights(), party, clock, new TestRoom());
        clock.Observe(rest);

        // Two hours of ward at nine in the morning, and a wait until the next dawn: the wait's own hours are
        // what the ward runs out in.
        running.Start(Ward, magnitude: 5, GameDuration.FromHours(2));
        Assert.True(rest.Perform(RestKind.WaitUntilDawn).IsApplied);

        Assert.False(running.IsRunning(Ward));
        Assert.False(party.Effects.Has(Ward));
    }

    [Fact]
    public void A_wait_past_the_debt_of_sleep_leaves_the_party_weak()
    {
        using PartyEntity party = Party();
        GameClock clock = TestClock.Create();
        PartyRest rest = new(new TestNights(), party, clock, new TestRoom());
        clock.Observe(rest);

        // Nine in the morning to five the next morning is twenty hours, inside the day the party may stay
        // awake; the next wait to dawn runs past it, and waiting rests nobody.
        Assert.True(rest.Perform(RestKind.WaitUntilDawn).IsApplied);
        Assert.False(party.Members[0].Conditions.Has(TestNights.Weakness));
        Assert.True(rest.Perform(RestKind.WaitUntilDawn).IsApplied);
        Assert.True(party.Members[0].Conditions.Has(TestNights.Weakness));
    }

    [Fact]
    public void No_source_but_the_clock_keeps_a_list_of_the_owners_of_game_time()
    {
        // A second list of owners is a second delivery path, and a second path is how an advance reached some owners
        // and not others: every owner is registered with the clock and nothing else. The law finds every place the
        // owner seam is a type argument — a list, a set, a dictionary's value — anywhere in the runtime.
        ProductSource.OnlyIn(
            ProductSource.Runtime.TypeArgumentUses(typeof(IGameTimeObserver)),
            file => file == "src/PartyRpg.Kit/Time/GameClock.cs",
            "The clock is the one keeper of the owners of game time.");
    }

    private static PartyEntity Party() =>
        new PartyEntityFactory().Create(new PartyCreation(
            [
                new MemberCreation(new PartyMemberSeed(
                    "Nyx",
                    new RaceId("human"),
                    new ClassId("druid"),
                    [new AttributeScore(new AttributeId("Might"), 10)],
                    skills: [],
                    spells: [],
                    experience: 0,
                    level: 1,
                    skillPoints: 0,
                    classRank: 1,
                    conditions: [],
                    hitPoints: ResourcePool.Full(20),
                    spellPoints: ResourcePool.Full(20))),
            ],
            coins: 0,
            foodPortions: 4,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));

    /// <summary>An owner of game time that remembers what it heard.</summary>
    private sealed class Recorder(Action? onHeard = null) : IGameTimeObserver
    {
        public List<ClockAdvance> Heard { get; } = [];

        public void Observe(ClockAdvance advance)
        {
            Heard.Add(advance);
            onHeard?.Invoke();
        }
    }
}
