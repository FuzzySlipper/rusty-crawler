using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The facts of one admitted update that a session measures itself by. This is the only part of an
/// <see cref="ProductUpdate"/> the session needs, which keeps the engine edge thin enough to test
/// without constructing engine values.
/// </summary>
/// <param name="SimulationStep">The engine's admitted simulation step counter.</param>
/// <param name="AdmittedStepCount">How many fixed steps this update admitted.</param>
/// <param name="FixedDeltaSeconds">The length of one admitted simulation step in seconds.</param>
public readonly record struct SessionTick(ulong SimulationStep, uint AdmittedStepCount, double FixedDeltaSeconds)
{
    /// <summary>Reads the tick facts from an engine update.</summary>
    public static SessionTick From(ProductUpdateFacts facts) =>
        new(facts.SimulationStep, facts.AdmittedStepCount, facts.FixedDeltaSeconds);
}
