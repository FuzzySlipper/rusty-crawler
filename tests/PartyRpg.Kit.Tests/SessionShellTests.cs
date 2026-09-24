using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>The session shell's own behavior: mode, measured admitted simulation, and its projection.</summary>
public sealed class SessionShellTests
{
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test Ruleset");
    private const double StepSeconds = 1.0 / 60.0;

    private static SessionTick Tick(ulong step, uint admitted = 1) => new(step, admitted, StepSeconds);

    private static PartyRpgSession Started(out RecordingUiProjectionChannel channel)
    {
        channel = new RecordingUiProjectionChannel();
        PartyRpgSession session = new(Composition, channel);
        session.Start();
        return session;
    }

    [Fact]
    public void Session_starts_in_starting_and_runs_after_start()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel);

        Assert.Equal(SessionMode.Starting, session.Mode);
        Assert.Equal("starting", channel.Latest().Field("session").Field("mode").AsString());

        session.Start();

        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.Equal("running", channel.Latest().Field("session").Field("mode").AsString());
    }

    [Fact]
    public void Projection_carries_the_composition_and_the_admitted_simulation()
    {
        using PartyRpgSession session = Started(out RecordingUiProjectionChannel channel);

        for (ulong step = 0; step < 600; step++) session.Advance(Tick(step));

        ProjectedNode root = channel.Latest();
        Assert.Equal("test.ruleset", root.Field("composition").Field("ruleset").AsString());
        Assert.Equal("Test Ruleset", root.Field("composition").Field("title").AsString());
        Assert.Equal("running", root.Field("session").Field("mode").AsString());
        Assert.Equal(10.0, root.Field("session").Field("simulationSeconds").AsNumber(), 3);
        Assert.Equal(600d, root.Field("session").Field("admittedSteps").AsNumber());
        Assert.Equal(600d, root.Field("session").Field("updates").AsNumber());
    }

    [Fact]
    public void The_two_published_measures_of_admitted_simulation_agree()
    {
        using PartyRpgSession session = Started(out _);

        // Contiguous admitted batches, including catch-up batches, must never credit a step twice or
        // lose the batch in flight: the published seconds and the published step count describe the
        // same simulation.
        ulong step = 0;
        foreach (uint admitted in new uint[] { 1, 1, 4, 2, 1, 4, 4, 1, 3 })
        {
            session.Advance(Tick(step, admitted));
            step += admitted;
            Assert.Equal(session.AdmittedSteps * StepSeconds, session.SimulationSeconds, 9);
        }
    }

    [Fact]
    public void A_held_session_freezes_its_measurements_while_updates_keep_arriving()
    {
        using PartyRpgSession session = Started(out RecordingUiProjectionChannel channel);
        for (ulong step = 0; step < 600; step++) session.Advance(Tick(step));
        Assert.Equal(10.0, session.SimulationSeconds, 3);
        Assert.Equal(600ul, session.AdmittedSteps);

        session.Hold();
        int publishedBeforeHold = channel.Count;
        session.Advance(Tick(600));

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.Equal(10.0, session.SimulationSeconds, 3);
        Assert.Equal(600ul, session.AdmittedSteps);
        Assert.True(channel.Count > publishedBeforeHold, "A held session still publishes the state it holds.");

        // Releasing resumes at the next admitted tick, and the held interval is never credited: the
        // published seconds stay equal to the published steps, which is what proves the gap between
        // step 600 and step 700 was excluded.
        session.ReleaseHold();
        session.Advance(Tick(700));
        Assert.Equal(601ul, session.AdmittedSteps);
        Assert.Equal(10.0 + StepSeconds, session.SimulationSeconds, 9);
        Assert.Equal(session.AdmittedSteps * StepSeconds, session.SimulationSeconds, 9);

        session.Advance(Tick(701));
        Assert.Equal(602ul, session.AdmittedSteps);
        Assert.Equal(10.0 + (2 * StepSeconds), session.SimulationSeconds, 9);
        Assert.Equal(session.AdmittedSteps * StepSeconds, session.SimulationSeconds, 9);
    }

    [Fact]
    public void The_engine_pause_and_the_player_hold_are_separate_authorities()
    {
        using PartyRpgSession session = Started(out _);
        Assert.Equal(SessionMode.Running, session.Mode);

        session.Pause();
        Assert.Equal(SessionMode.Paused, session.Mode);
        session.Resume();
        Assert.Equal(SessionMode.Running, session.Mode);

        // A player's hold survives an engine pause/resume cycle: the engine releasing its own pause
        // must not release a hold the player asked for.
        session.Hold();
        Assert.Equal(SessionMode.Paused, session.Mode);
        session.Pause();
        session.Resume();
        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.True(session.IsHeld);

        // And releasing the hold must not undo an engine pause that is still in force.
        session.Pause();
        session.ReleaseHold();
        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.True(session.IsEnginePaused);
        session.Resume();
        Assert.Equal(SessionMode.Running, session.Mode);
    }

    [Fact]
    public void Repeating_a_transition_does_not_republish()
    {
        using PartyRpgSession session = Started(out RecordingUiProjectionChannel channel);
        int afterStart = channel.Count;

        session.Start();
        session.Resume();
        session.ReleaseHold();
        session.Hold();
        session.Hold();

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.Equal(afterStart + 1, channel.Count);
    }

    [Fact]
    public void A_stopped_session_publishes_the_stop_and_then_nothing()
    {
        RecordingUiProjectionChannel channel = new();
        PartyRpgSession session = new(Composition, channel);
        session.Start();
        int published = channel.Count;

        session.Dispose();

        Assert.Equal(SessionMode.Stopped, session.Mode);
        Assert.Equal(published + 1, channel.Count);
        Assert.Equal("stopped", channel.Latest().Field("session").Field("mode").AsString());
        Assert.Throws<ObjectDisposedException>(() => session.Advance(Tick(1)));
        Assert.Throws<ObjectDisposedException>(() => session.Start());
    }
}
