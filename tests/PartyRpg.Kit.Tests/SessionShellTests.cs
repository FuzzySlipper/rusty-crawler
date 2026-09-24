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
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel);
        session.Start();

        // The first admitted tick establishes the baseline; the second measures against it.
        session.Advance(Tick(0, 0));
        session.Advance(Tick(600, 600));

        ProjectedNode root = channel.Latest();
        Assert.Equal("test.ruleset", root.Field("composition").Field("ruleset").AsString());
        Assert.Equal("Test Ruleset", root.Field("composition").Field("title").AsString());
        Assert.Equal("running", root.Field("session").Field("mode").AsString());
        Assert.Equal(10.0, root.Field("session").Field("simulationSeconds").AsNumber(), 3);
        Assert.Equal(600d, root.Field("session").Field("admittedSteps").AsNumber());
        Assert.Equal(2d, root.Field("session").Field("updates").AsNumber());
    }

    [Fact]
    public void Paused_session_holds_its_measurements_while_updates_keep_arriving()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel);
        session.Start();
        session.Advance(Tick(0, 0));
        session.Advance(Tick(600, 600));
        Assert.Equal(10.0, session.SimulationSeconds, 3);

        session.Pause();
        int publishedBeforeHold = channel.Count;
        session.Advance(Tick(1200, 600));

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.Equal(10.0, session.SimulationSeconds, 3);
        Assert.Equal(600ul, session.AdmittedSteps);
        Assert.True(channel.Count > publishedBeforeHold, "A held session still publishes the state it holds.");

        // Resuming re-establishes the baseline, so the held interval is never credited to the session.
        session.Resume();
        session.Advance(Tick(1800, 600));
        Assert.Equal(10.0, session.SimulationSeconds, 3);
        session.Advance(Tick(2400, 600));
        Assert.Equal(20.0, session.SimulationSeconds, 3);
    }

    [Fact]
    public void Starting_twice_or_resuming_a_running_session_changes_nothing()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel);
        session.Start();
        int afterStart = channel.Count;

        session.Start();
        session.Resume();
        session.Pause();
        session.Pause();

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.Equal(afterStart + 1, channel.Count);
    }

    [Fact]
    public void A_stopped_session_publishes_nothing_further()
    {
        RecordingUiProjectionChannel channel = new();
        PartyRpgSession session = new(Composition, channel);
        session.Start();
        int published = channel.Count;

        session.Dispose();

        Assert.Equal(SessionMode.Stopped, session.Mode);
        Assert.Equal(published, channel.Count);
        Assert.Throws<ObjectDisposedException>(() => session.Advance(Tick(1)));
        Assert.Throws<ObjectDisposedException>(() => session.Start());
    }
}
