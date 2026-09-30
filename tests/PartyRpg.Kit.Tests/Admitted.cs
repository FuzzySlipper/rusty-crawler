using Rusty.Engine;

namespace PartyRpg.Kit.Tests;

/// <summary>Admitted updates in the shape the engine admits them, for suites that need no input of their own.</summary>
internal static class Admitted
{
    /// <summary>The fixed step the kit's suites admit at.</summary>
    internal const double StepSeconds = 1.0 / 60.0;

    /// <summary>One admitted update of a number of fixed steps, carrying the input given.</summary>
    internal static ProductUpdate Update(ulong step, uint admitted, params ProductInputEvent[] input) =>
        new(
            new ProductUpdateFacts(
                ProductUpdateMode.Realtime,
                ProductLifecycleState.Running,
                Generation: 1,
                ControlRevision: 1,
                ObservedHostTimeNanoseconds: 0,
                SimulationStep: step,
                FixedStepHz: 60,
                AdmittedStepCount: admitted,
                DroppedStepCount: 0,
                FixedDeltaSeconds: StepSeconds),
            input);

    /// <summary>
    /// An update that admits no time and carries nothing: the session steps nothing, and publishes what the world
    /// now stands on — which is how a suite that moved the party by hand reads it back.
    /// </summary>
    internal static ProductUpdate Nothing(ulong step) => Update(step, 0);
}
